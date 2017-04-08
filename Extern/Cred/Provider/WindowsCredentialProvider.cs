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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Neo.PerfectWorking.Data;
using static Neo.PerfectWorking.Cred.Provider.NativeMethods;

namespace Neo.PerfectWorking.Cred.Provider
{
	#region -- class WindowsCredentialInfo ----------------------------------------------

	internal sealed class WindowsCredentialInfo : ICredentialInfo
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly WindowsCredentialProviderBase provider;
		private readonly string targetName;
		private readonly CredentialType type;
		private readonly CredentialPersist persist;
		private string userName;
		private string comment;
		private DateTime lastWritten;

		private SecureString password;

		private bool isModified;

		internal unsafe WindowsCredentialInfo(WindowsCredentialProviderBase provider, ref CREDENTIAL nativeCred)
		{
			this.provider = provider;

			// unpack data
			this.targetName = nativeCred.TargetName;
			this.type = nativeCred.Type;
			this.persist = nativeCred.Persist;

			

			//if (nativeCred.CredentialBlobSize > 0)
			//{
			//	Console.WriteLine($"{targetName} ==> {nativeCred.Type}  {nativeCred.CredentialBlobSize}");

			//	if (nativeCred.Type == CredentialType.Generic)
			//	{
			//		var saltStr = "abe2869f-9b47-4cd9-a358-c22904dba7f7\0";
			//		var salt = new short[37];

			//		for (var i = 0; i < 37; i++)
			//			salt[i] = (short)(((short)saltStr[i]) << 2);

			//		fixed (short* ptr = salt)
			//		{
			//			var entropie = new DATA_BLOB()
			//			{
			//				DataSize = 74,
			//				DataPtr = new IntPtr(ptr)
			//			};
			//			var inData = new DATA_BLOB()
			//			{
			//				DataSize = nativeCred.CredentialBlobSize,
			//				DataPtr = nativeCred.CredentialBlob
			//			};
			//			var outData = new DATA_BLOB();
			//			if (CryptUnprotectData(ref inData, null, ref entropie, IntPtr.Zero, IntPtr.Zero, 0, ref outData))
			//			{
			//				Console.WriteLine(Marshal.PtrToStringUni(outData.DataPtr, outData.DataSize / 2));
			//			}
			//			else
			//				Console.WriteLine("error: " + Marshal.PtrToStringUni(nativeCred.CredentialBlob, nativeCred.CredentialBlobSize / 2));
			//		}
			//	}
			//	else if (nativeCred.Type == CredentialType.DomainVisiblePassword)
			//	{
			//		var saltStr = "82BD0E67-9FEA-4748-8672-D5EFE5B779B0";
			//		var salt = new short[37];

			//		for (var i = 0; i < 37; i++)
			//			salt[i] = (short)(((short)saltStr[i]) << 2);

			//		fixed (short* ptr = salt)
			//		{
			//			var entropie = new DATA_BLOB()
			//			{
			//				DataSize = 74,
			//				DataPtr = new IntPtr(ptr)
			//			};
			//			var inData = new DATA_BLOB()
			//			{
			//				DataSize = nativeCred.CredentialBlobSize,
			//				DataPtr = nativeCred.CredentialBlob
			//			};
			//			var outData = new DATA_BLOB();
			//			if (CryptUnprotectData(ref inData, null, ref entropie, IntPtr.Zero, IntPtr.Zero, 0, ref outData))
			//			{
			//				Console.WriteLine(Marshal.PtrToStringUni(outData.DataPtr, outData.DataSize / 2));
			//			}
			//			else
			//				throw new Win32Exception();
			//		}

			//	}
			//	else
			//		Console.WriteLine(Marshal.PtrToStringUni(nativeCred.CredentialBlob, nativeCred.CredentialBlobSize / 2));
			//}

			Refresh(ref nativeCred, false);
		} // ctor

		public void ClearPassword()
		{
			password?.Dispose();
			password = null;
		} // proc ClearPassword

		internal unsafe void Refresh(ref CREDENTIAL nativeCred, bool notify)
		{
			SetValue(nameof(UserName), ref userName, nativeCred.UserName, notify);
			SetValue(nameof(LastWritten), ref lastWritten, DateTime.FromFileTimeUtc(nativeCred.LastWritten), notify);

			// todo: use comment as attr
			SetValue(nameof(Comment), ref comment, nativeCred.Comment, notify);

			ClearPassword();

			//var attr = nativeCred.Attributes;
			//var c = nativeCred.AttributeCount;
			//while(c-- > 0)
			//{
			//	var a = Marshal.PtrToStructure<CREDENTIAL_ATTRIBUTE>(attr);
			//	Console.WriteLine($"Attr: {a.Keyword}, {a.Flags}, {a.ValueSize}");
			//	attr = new IntPtr(Marshal.SizeOf<CREDENTIAL_ATTRIBUTE>() + attr.ToInt64());
			//}

			this.isModified = false;
		} // proc Refresh

		internal void Update()
		{
			if (!isModified)
				return;

			CREDENTIAL nativeCred;
			if (CredRead(targetName, type, 0, out var nativeCredPtr))
			{
				try
				{
					nativeCred = Marshal.PtrToStructure<CREDENTIAL>(nativeCredPtr);
				}
				finally
				{
					CredFree(nativeCredPtr);
				}
			}
			else
			{
				nativeCred = new CREDENTIAL()
				{
					TargetName = targetName,
					TargetAlias = null,
					Type = CredentialType.Generic,
					Persist = persist,
					Flags = 0,
					LastWritten = 0, // is ignored
					AttributeCount = 0,
					Attributes = IntPtr.Zero,
					CredentialBlob = IntPtr.Zero,
					CredentialBlobSize = 0
				};
			}

			nativeCred.UserName = userName;
			nativeCred.Comment = comment; // todo: use attribute

			if (password != null)
				ProtectPassword(password, out nativeCred.CredentialBlob, out nativeCred.CredentialBlobSize);

			if (!CredWrite(ref nativeCred, 0))
				throw new Win32Exception();

			isModified = false;
			OnPropertyChanged(nameof(IsModified));
		} // proc Update

		public SecureString GetPassword()
		{
			if (password == null)
			{
				if (!CredRead(targetName, type, 0, out var nativeCredPtr))
					throw new Win32Exception();
				try
				{
					var nativeCred = Marshal.PtrToStructure<CREDENTIAL>(nativeCredPtr);
					return password = UnprotectPassword(nativeCred.CredentialBlob, nativeCred.CredentialBlobSize);
				}
				finally
				{
					CredFree(nativeCredPtr);
				}
			}
			else
				return password;
		} // func GetPassword

		private void ProtectPassword(SecureString password, out IntPtr credentialBlob, out int credentialBlobSize) 
			=> throw new NotImplementedException();

		private SecureString UnprotectPassword(IntPtr credentialBlob, int credentialBlobSize) 
			=> throw new NotImplementedException();

		public void SetPassword(SecureString password)
		{
			this.password = password;
			SetModified(true);
		} // proc SetPassword

		private void SetValue<T>(string propertyName, ref T value, T newValue, bool notify)
		{
			if (!Object.Equals(value, newValue))
			{
				value = newValue;
				SetModified(notify);

				if (notify)
					OnPropertyChanged(propertyName);
			}
		} // proc SetValue

		private void SetModified(bool notify)
		{
			if (!isModified)
			{
				isModified = true;
				if (notify)
					OnPropertyChanged(nameof(IsModified));
			}
		} // proc SetModified

		private void OnPropertyChanged(string propertyName) 
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		public string TargetName => targetName;

		public string UserName
		{
			get => userName;
			set
			{
				if (!provider.IsReadOnly)
					SetValue(nameof(UserName), ref userName, value, true);
			}
		} // prop UserName

		public string Comment
		{
			get => comment;
			set
			{
				if (!provider.IsReadOnly)
					SetValue(nameof(Comment), ref comment, value, true);
			}
		} // prop Comment

		public DateTime LastWritten => lastWritten;

		public bool IsModified => isModified;

		public object Image => null;
		public ICredentialProvider Provider => provider;
	} // class WindowsCredentialInfo

	#endregion

	#region -- class WindowsCredentialProviderBase --------------------------------------

	internal abstract class WindowsCredentialProviderBase : ICredentialProvider, IPwIdleAction, IDisposable
	{
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly IPwGlobal global;
		private readonly string name;

		private readonly Dictionary<string, WindowsCredentialInfo> credentials = new Dictionary<string, WindowsCredentialInfo>(StringComparer.OrdinalIgnoreCase);

		private DateTime lastRefreshTime;

		#region -- Ctor/Dtor ------------------------------------------------------------

		public WindowsCredentialProviderBase(IPwGlobal global, string name)
		{
			this.global = global ?? throw new ArgumentNullException(nameof(global));
			this.name = name ?? throw new ArgumentNullException(nameof(name));

			global.UI.AddIdleAction(this);
		} // ctor

		public void Dispose()
		{
			if (global == null)
				return;

			// remove idle
			global.UI.RemoveIdleAction(this);

			// dispose passwords
			foreach (var v in credentials.Values)
				v.ClearPassword();
			credentials.Clear();
		} // proc Dispose

		#endregion

		#region -- Refresh --------------------------------------------------------------

		public void Refresh()
			=> Refresh(true);

		private void Refresh(bool force)
		{
			var newestCredentialDate = GetNewestCredentialFileDate();
			if (force || newestCredentialDate > lastRefreshTime)
			{
				RefreshCore();
				lastRefreshTime = newestCredentialDate;
			}
		} // proc Refresh

		private unsafe void RefreshCore()
		{
			if (!CredEnumerate(null, 0, out var count, out var credentialInfos))
			{
				var hr = Marshal.GetLastWin32Error();
				if (hr == 1168)
					count = 0;
				else
					throw new Win32Exception(hr);
			}
			try
			{
				// mark all values
				var notRefresh = new List<WindowsCredentialInfo>(credentials.Values);

				// add new values
				var cur = (void**)credentialInfos.ToPointer();
				while (count-- > 0)
				{
					// unpack windows credential
					var data = Marshal.PtrToStructure<CREDENTIAL>(new IntPtr(*cur));
					var targetName = data.TargetName;
					if (!String.IsNullOrEmpty(targetName) && IsFiltered(targetName, data.Type, data.Persist))
					{
						if (credentials.TryGetValue(targetName, out var cred))
						{
							cred.Refresh(ref data, true);
							notRefresh.Remove(cred);
						}
						else
						{
							cred = new WindowsCredentialInfo(this, ref data);
							credentials.Add(targetName, cred);
							CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, cred));
						}
					}
					cur++;
				}

				// remove not refreshed values
				foreach (var cred in notRefresh)
				{
					credentials.Remove(cred.TargetName);
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, cred));
				}
			}
			finally
			{
				CredFree(credentialInfos);
			}
		} // proc RefreshCore

		protected virtual bool IsFiltered(string targetName, CredentialType type, CredentialPersist persist)
			=> (persist == CredentialPersist.LocalMachine || persist == CredentialPersist.Enterprise)
				&& (type == CredentialType.Generic || type == CredentialType.DomainPassword || type == CredentialType.DomainVisiblePassword);

		bool IPwIdleAction.OnIdle(int elapsed)
		{
			if (elapsed > 100)
			{
				if (!IsReadOnly)
				{
					foreach (var c in credentials.Values)
						c.Update();
				}
				Refresh(false);
			}
			return true;
		} // func OnIdle

		#endregion

		public abstract ICredentialInfo Append(ICredentialInfo newItem);

		public abstract bool Remove(string targetName);

		public NetworkCredential GetCredential(Uri uri, string authType)
			=> throw new NotImplementedException();

		public IEnumerator<ICredentialInfo> GetEnumerator()
			=> credentials.Values.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		public string Name => name;
		public abstract bool IsReadOnly { get; }

		protected abstract ICredentialProtector DefaultProtector { get; }

		// -- Static ----------------------------------------------------------

		private static DateTime newestCredentialFileDate = DateTime.MinValue;
		private static int lastCredentialCheck = 0;

		private static DirectoryInfo localCredentialDirectoryInfo;
		private static DirectoryInfo roamingCredentialDirectoryInfo;

		static WindowsCredentialProviderBase()
		{
			localCredentialDirectoryInfo = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Credentials"));
			roamingCredentialDirectoryInfo = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Credentials"));

			GetNewestCredentialFileDate(true);
		} // ctor

		private static void CheckDirectoryFiles(DirectoryInfo di)
		{
			if (di.Exists)
			{
				foreach (var fi in di.GetFiles())
				{
					var lastWriteTime = fi.LastWriteTime;
					if (newestCredentialFileDate < lastWriteTime)
						newestCredentialFileDate = lastWriteTime;
				}
			}
		} // proc CheckDirectoryFiles

		private static DateTime GetNewestCredentialFileDate(bool force = false)
		{
			lock (localCredentialDirectoryInfo)
			{
				if (force || unchecked(Environment.TickCount - lastCredentialCheck) > 3000)
				{
					try { CheckDirectoryFiles(localCredentialDirectoryInfo); }
					catch (Exception e) { Debug.Print(e.ToString()); }

					try { CheckDirectoryFiles(roamingCredentialDirectoryInfo); }
					catch (Exception e) { Debug.Print(e.ToString()); }

					lastCredentialCheck = Environment.TickCount;
				}
				return newestCredentialFileDate;
			}
		} // func GetNewestCredentialFileDate
	} // class WindowsCredentialProviderBase

	#endregion

	#region -- class WindowsCredentialProviderReadOnly ----------------------------------

	internal sealed class WindowsCredentialProviderReadOnly : WindowsCredentialProviderBase
	{
		#region -- class WindowsDecrypter -----------------------------------------------

		private sealed class WindowsDecrypter : ICredentialProtector
		{
			public bool CanDecryptPrefix(object encrypted) => throw new NotImplementedException();
			public void Dispose() { }
			public object Encrypt(SecureString password) 
				=> throw new NotImplementedException();

			public bool TryDecrypt(object encrypted, out SecureString password)
			{
				password = null;
				return false;
			} // func TryDecrypt
		} // class WindowsDecrypter

		#endregion

		private readonly Regex[] filter;

		#region -- Ctor/Dtor ------------------------------------------------------------

		public WindowsCredentialProviderReadOnly(IPwGlobal global, string name, params string[] filter)
			: base(global, name)
		{
			// check for exclude filter or nil
			this.filter = (filter?.Select(ConvertFilterToRegEx).ToArray()) ?? Array.Empty<Regex>();

			Refresh();
		} // ctor

		private static Regex ConvertFilterToRegEx(string filter)
		{
			if (String.IsNullOrEmpty(filter))
				return new Regex("*.", RegexOptions.Compiled | RegexOptions.IgnoreCase);

			var sb = new StringBuilder("^");
			foreach (var c in filter)
			{
				if (c == '*')
					sb.Append("*.");
				else if (c == '?')
					sb.Append('.');
				else if (Char.IsLetterOrDigit(c))
					sb.Append(c);
				else
					sb.Append('\\').Append(c);
			}
			sb.Append('$');

			return new Regex(sb.ToString(), RegexOptions.Compiled | RegexOptions.IgnoreCase);
		} // func ConvertFilterToRegEx

		#endregion

		public override ICredentialInfo Append(ICredentialInfo newItem)
			=> throw new NotSupportedException();

		public override bool Remove(string targetName)
			=> throw new NotSupportedException();

		protected override bool IsFiltered(string targetName, CredentialType type, CredentialPersist persist) 
			=> base.IsFiltered(targetName, type, persist) && NotExludeFiltered(targetName);

		private bool NotExludeFiltered(string targetName)
		{
			if (filter.Length == 0)
				return true;
			else
			{
				foreach (var f in filter)
				{
					if (f.IsMatch(targetName))
						return false;
				}
				return true;
			}
		} // func NotExludeFiltered

		public override bool IsReadOnly => true;

		protected override ICredentialProtector DefaultProtector => windowsDecrypter;

		private static readonly WindowsDecrypter windowsDecrypter = new WindowsDecrypter();
	} // class WindowsCredentialProviderReadOnly

	#endregion

	#region -- class WindowsCredentialProvider ------------------------------------------

	internal sealed class WindowsCredentialProvider : WindowsCredentialProviderBase
	{
		private static Regex checkPrefix = new Regex(@"^\w+\:?\*?$");
		private readonly string prefix;
		private readonly CredentialPersist credentialPersist;
		private readonly ICredentialProtector protector;

		#region -- Ctor/Dtor ------------------------------------------------------------

		public WindowsCredentialProvider(IPwGlobal global, string name, string prefix, ICredentialProtector protector, bool persitEnterprise = false)
			: base(global, name)
		{
			// check for a prefix filter
			if (String.IsNullOrEmpty(prefix))
				throw new ArgumentNullException("Prefix is missing.", nameof(prefix));

			if (checkPrefix.IsMatch(prefix))
			{
				var remove = 0;
				if (prefix[prefix.Length - 1] == '*')
					remove = 1;
				if (prefix[prefix.Length - 1 - remove] == ':')
					remove++;
				if (remove > 0)
					prefix = prefix.Substring(0, prefix.Length - remove);
			}
			else
				throw new ArgumentException($"Prefix is invalid.");

			this.prefix = prefix;
			this.credentialPersist = persitEnterprise ? CredentialPersist.Enterprise : CredentialPersist.LocalMachine;
			this.protector = protector ?? Protector.UserProtector;

			Refresh();
		} // ctor

		#endregion

		public override ICredentialInfo Append(ICredentialInfo newItem) 
			=> throw new NotImplementedException();

		public override bool Remove(string targetName) 
			=> throw new NotImplementedException();

		protected override bool IsFiltered(string targetName, CredentialType type, CredentialPersist persist)
			=> persist == credentialPersist
			&& type == CredentialType.Generic
			&& targetName.Length > prefix.Length + 2
			&& targetName[prefix.Length] == ':'
			&& String.Compare(targetName.Substring(0, prefix.Length), prefix, StringComparison.OrdinalIgnoreCase) == 0;

		public override bool IsReadOnly => false;

		protected override ICredentialProtector DefaultProtector => protector;
	} // class WindowsCredentialProvider

	#endregion

	#region -- class NativeMethods ------------------------------------------------------

	internal static class NativeMethods
	{
		public enum CredentialType : uint
		{
			None = 0,
			Generic = 1,
			DomainPassword = 2,
			DomainCertificate = 3,
			DomainVisiblePassword = 4,
			GenericCertificate = 5,
			DomainExtended = 6
		} // enum CredentialType

		public enum CredentialPersist : uint
		{
			None = 0,
			Session = 1,
			LocalMachine = 2,
			Enterprise = 3
		} // enum CredentialType

		[StructLayout(LayoutKind.Sequential)]
		public struct CREDENTIAL
		{
			public int Flags;
			public CredentialType Type;
			[MarshalAs(UnmanagedType.LPWStr)]
			public string TargetName;
			[MarshalAs(UnmanagedType.LPWStr)]
			public string Comment;
			public long LastWritten;
			public int CredentialBlobSize;
			public IntPtr CredentialBlob;
			public CredentialPersist Persist;
			public int AttributeCount;
			public IntPtr Attributes;
			[MarshalAs(UnmanagedType.LPWStr)]
			public string TargetAlias;
			[MarshalAs(UnmanagedType.LPWStr)]
			public string UserName;
		} // struct struct CREDENTIAL

		[StructLayout(LayoutKind.Sequential)]
		public struct CREDENTIAL_ATTRIBUTE
		{
			[MarshalAs(UnmanagedType.LPWStr)]
			public string Keyword;
			public uint Flags;
			public uint ValueSize;
			public IntPtr Value;
		} // struct CREDENTIAL_ATTRIBUTE

		public enum CRED_PROTECTION_TYPE : int
		{
			Unprotected = 0,
			UserProtection = 1,
			TrustedProtection = 2
		} // enum CRED_PROTECTION_TYPE

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct DATA_BLOB
		{
			public int DataSize;
			public IntPtr DataPtr;
		} // struct DATA_BLOB

		public const int CRED_MAX_STRING_LENGTH = 256;
		public const int CRED_MAX_CREDENTIAL_BLOB_SIZE = 512;

		private const string advapi32 = "Advapi32.dll";
		private const string crypt32 = "Crypt32.dll";
		private const string kernel32 = "Kernel32.dll";

		[DllImport(advapi32, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern bool CredRead(string target, CredentialType type, int reservedFlag, out IntPtr CredentialPtr);

		[DllImport(advapi32, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

		[DllImport(advapi32, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern bool CredEnumerate([In] string filter, [In] uint flags, [Out] out int count, [Out]  out IntPtr credentials);

		[DllImport(advapi32, SetLastError = true)]
		public static extern void CredFree([In] IntPtr cred);

		[DllImport(advapi32, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern bool CredDelete(string target, CredentialType type, int flags);

		[DllImport(advapi32, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern bool CredUnprotect(bool fAsSelf, string pszProtectedCredentials, int cchCredentials, IntPtr pszCredentials, ref int pcchMaxChars);

		[DllImport(advapi32, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern bool CredProtect(bool fAsSelf, IntPtr pszCredentials, int cchCredentials, StringBuilder pszProtectedCredentials, ref int pcchMaxChars, out CRED_PROTECTION_TYPE type);

		[DllImport(crypt32, SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool CryptProtectData(ref DATA_BLOB pDataIn, string szDataDescr, ref DATA_BLOB pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DATA_BLOB pDataOut);

		[DllImport(crypt32, SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, string szDataDescr, ref DATA_BLOB pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DATA_BLOB pDataOut);

		[DllImport(kernel32, EntryPoint = "RtlZeroMemory", SetLastError = false)]
		public static extern void ZeroMemory(IntPtr dest, IntPtr size);
		[DllImport(kernel32, SetLastError = true)]
		public static extern IntPtr LocalFree(IntPtr handle);
	} // NativeMethods

	#endregion
}
