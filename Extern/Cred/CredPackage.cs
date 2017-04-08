#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using Neo.PerfectWorking.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.IO;
using System.Xml;
using System.Globalization;
using System.ComponentModel;
using System.Security;
using System.Collections.Specialized;
using System.Xml.Linq;
using Neo.PerfectWorking.Cred.Data;
using System.Windows.Media;
using Neo.PerfectWorking.Cred.Provider;
using Neo.PerfectWorking.Stuff;

namespace Neo.PerfectWorking.Cred
{
	public sealed class CredPackage : PwPackageBase, ICredentials
	{
		private readonly IPwCollection<ICredentialProvider> credentialProviders;
		private readonly IPwCollection<ICredentialProtector> credentialProtectors;

		#region -- Ctor -----------------------------------------------------------------

		public CredPackage(IPwGlobal global) 
			: base(global, nameof(CredPackage))
		{
			this.credentialProviders = global.RegisterCollection<ICredentialProvider>(this);
			this.credentialProtectors = global.RegisterCollection<ICredentialProtector>(this);

			global.RegisterObject(this, nameof(CredPackagePane), new CredPackagePane(this));
		} // ctor

		#endregion

		#region -- Protector creation ---------------------------------------------------

		public ICredentialProtector CreateXorProtector(string prefix, SecureString key)
			=> new XorEncryptProtector(prefix, key);

		public ICredentialProtector CreateBinaryDesProtector(SecureString key)
			=> new DesEncryptProtectorBinary(key);

		public ICredentialProtector CreateStaticDesProtector(string prefix, byte[] keyInfo)
			=> new DesEncryptProtectorStatic(prefix, keyInfo);

		public ICredentialProtector CreateStringDesProtector(SecureString key)
			=> new DesEncryptProtectorString(key);

		public ICredentialProtector CreateWindowsCryptProtectorbool(bool emitBinary = false, bool localMachine = false, byte[] secureKey = null)
			=> new WindowsCryptProtector(emitBinary, localMachine, secureKey);

		public ICredentialProtector CreatePowerShellProtector(object key)
		{
			switch (key)
			{
				case null:
					return new PowerShellProtector();
				case SecureString ss:
					return new PowerShellProtector(ss);
				case string pwd:
					return new PowerShellProtector(Encoding.Unicode.GetBytes(pwd));
				case byte[] b:
					return new PowerShellProtector(b);
				default:
					throw new ArgumentException("Invalid argument.", nameof(key));
			}
		} // func CreatePowerShellProtector

		#endregion

		#region -- SecureString ---------------------------------------------------------

		public SecureString CreateSecureString(object data)
		{
			switch (data)
			{
				case string plain:
					{
						return plain.CreateSecureString();
					}
				case char[] chars:
					{
						var ss = new SecureString();
						for (var i = 0; i < chars.Length; i++)
							ss.AppendChar(chars[i]);
						return ss;
					}
				case null:
					return null;
				default:
					throw new ArgumentException("Unknown argument type.");
			}
		} // func CreateSecureString

		public SecureString LoadPsSecureString(string fileName, string key = null)
		{
			return GetPsSecureString(File.ReadAllText(fileName), key);
		} // func LoadSecureString

		public SecureString GetPsSecureString(string data, string key)
		{
			using (var p = CreatePowerShellProtector(key))
			{
				if (p.TryDecrypt(data, out var pwd))
					return pwd;
				else
					throw new IOException("Can not decrypt password.");
			}
		} // func GetPsSecureString

		public NetworkCredential GetCredential(Uri uri, string authType)
		{
			NetworkCredential found = null;
			lock (credentialProviders.SyncRoot)
			{
				foreach (var cp in credentialProviders)
					found = cp.GetCredential(uri, authType);
			}
			return found;
		} // func GetCredential

		public object EncryptPassword(SecureString password, ICredentialProtector protector)
		{
			if (password == null || password.Length == 0)
				return null;
			if (protector == null)
				throw new ArgumentNullException(nameof(protector));

			// encrypt the password
			return protector.Encrypt(password);
		} // proc EncryptPasswordFromString

		public SecureString DeryptPassword(object encryptedPassword, ICredentialProtector protector = null)
		{
			if (protector != null && protector.TryDecrypt(encryptedPassword, out var password))
				return password;
			else
			{
				foreach (var p in credentialProtectors)
				{
					if (p.TryDecrypt(encryptedPassword, out password))
						return password;
				}
				return null;
			}
		} // proc EncryptPasswordFromString

		#endregion

		#region -- Protector creation ---------------------------------------------------

		public ICredentialProvider CreateFileCredentialProvider(string fileName, ICredentialProtector protector = null, bool readOnly = false)
		{
			if (!Path.IsPathRooted(fileName))
			{
				if (fileName.IndexOf('\\') >= 0)
					throw new Exception("Relative paths are not allowed.");

				fileName = Path.Combine(Global.UI.ApplicationLocalDirectory.FullName, fileName + ".xcred");
			}
			return new FileCredentialProvider(this, fileName, readOnly, protector);
		} // func CreateCredentialFileProvider

		public ICredentialProvider CreateWindowsCredentialProviderReadOnly(string name, params string[] filter)
			=> new WindowsCredentialProviderReadOnly(Global, name, filter);

		public ICredentialProvider CreateWindowsCredentialProvider(string name, string prefix, ICredentialProtector protector = null, bool readOnly = false, bool persitEnterprise = false)
			=> new WindowsCredentialProvider(Global, name, prefix, protector, persitEnterprise);

		#endregion

		public IPwCollection<ICredentialProvider> CredentialProviders => credentialProviders;

		/// <summary>Publish the no protector</summary>
		public static ICredentialProtector NoProtector => Protector.NoProtector;
		/// <summary>Publish the user protector</summary>
		public static ICredentialProtector UserProtector => Protector.UserProtector;
	} // class CredPackage
}
