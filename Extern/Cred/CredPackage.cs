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

namespace Neo.PerfectWorking.Cred
{
	public sealed class CredPackage : PwPackageBase, ICredentials
	{
		private readonly IPwCollection<ICredentialProvider> credentialProviders;
		private readonly IPwCollection<ICredentialProtector> credentialProtectors;

		public CredPackage(IPwGlobal global) 
			: base(global, nameof(CredPackage))
		{
			this.credentialProviders = global.RegisterCollection<ICredentialProvider>(this);
			this.credentialProtectors = global.RegisterCollection<ICredentialProtector>(this);

			global.RegisterObject(this, nameof(CredPackagePane), new CredPackagePane(this));
		} // ctor

		public ICredentialProvider CreateCredentialProvider(string fileName, bool readOnly = false)
		{
			if (!Path.IsPathRooted(fileName))
			{
				if (fileName.IndexOf('\\') >= 0)
					throw new Exception("Relative paths are not allowed.");

				fileName = Path.Combine(Global.UI.ApplicationLocalDirectory.FullName, fileName + ".xcred");
			}
			throw new NotImplementedException(); //return new FileCredProvider(fileName, readOnly);
		} // func RegisterFileProvider

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
			SecureString password;
			if (protector != null && protector.TryDecrypt(encryptedPassword, out password))
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

		public IPwCollection<ICredentialProvider> CredentialProviders => credentialProviders;
	} // class CredPackage
}
