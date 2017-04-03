using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Neo.PerfectWorking.Stuff;
using static Neo.PerfectWorking.Cred.Provider.NativeMethods;

namespace Neo.PerfectWorking.Cred.Provider
{
	#region -- class XorEncryptProtector ------------------------------------------------

	public sealed class XorEncryptProtector : ICredentialProtectorUI
	{
		private const string cryptSimpleKeyChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0987654321-_";

		private readonly string name;
		private readonly string prefix;
		private readonly SecureString protectorKey;

		public XorEncryptProtector(string name, string prefix, SecureString key)
		{
			this.name = name;
			this.prefix = prefix;
			this.protectorKey = key.Copy();
		} // ctor

		public bool CanDecryptPrefix(object encrypted)
			=> CanDecryptPrefix(encrypted, out var t);

		public bool CanDecryptPrefix(object encrypted, out string encryptedString)
		{
			if (encrypted is string s && s.Length > prefix.Length && ((s.Length - prefix.Length) & 1) == 1 && s.StartsWith(prefix))
			{
				encryptedString = s.Substring(prefix.Length);
				return true;
			}
			else
			{
				encryptedString = null;
				return false;
			}
		} // func CanDecryptPrefix

		public bool TryDecrypt(object encrypted, out SecureString password)
		{
			if (CanDecryptPrefix(encrypted, out var encryptedString))
			{
				var key = (byte)encryptedString[encryptedString.Length - 1];
				var k = key % protectorKey.Length;
				var l = (encryptedString.Length - 1) >> 1;
				var ss = new SecureString();

				var keyPtr = Marshal.SecureStringToGlobalAllocUnicode(protectorKey);
				var keyLength = protectorKey.Length * 2;
				try
				{
					byte GetNextKey()
					{
						var r = (byte)Marshal.ReadByte(keyPtr, k);
						k += 2;
						if (k >= keyLength)
							k = 0;
						return r;
					}

					for (var i = 0; i < encryptedString.Length - 1; i += 4)
					{
						var b1 = (byte)(Convert.ToByte(encryptedString.Substring(i, 2), 16) ^ key ^ GetNextKey());
						var b2 = (byte)(Convert.ToByte(encryptedString.Substring(i + 2, 2), 16) ^ key ^ GetNextKey());

						ss.AppendChar((char)(b2 << 8 | b1));
					}
				}
				finally
				{
					Marshal.ZeroFreeGlobalAllocUnicode(keyPtr);
				}

				password = ss;
				return true;
			}
			else
			{
				password = null;
				return false;
			}
		} // func TryDecrypt

		public unsafe object Encrypt(SecureString password)
		{
			if (password == null || password.Length == 0)
				return null;

			var pwdPtr = Marshal.SecureStringToGlobalAllocUnicode(password);
			var keyPtr = Marshal.SecureStringToGlobalAllocUnicode(protectorKey);
			var keyLength = protectorKey.Length * 2;
			try
			{
				var p = (byte*)pwdPtr;
				var rand = new Random(Environment.TickCount);
				var key = cryptSimpleKeyChars[rand.Next(cryptSimpleKeyChars.Length)];
				var sb = new StringBuilder(password.Length << 2);
				var j = (int)key % protectorKey.Length;
				var l = password.Length << 1;
				for (var i = 0; i < l; i++)
				{
					var c = unchecked((byte)(*p ^ Marshal.ReadByte(keyPtr, j) ^ (byte)key));
					j += 2;
					if (j >= keyLength)
						j = 0;
					sb.AppendFormat("{0:X2}", c);

					p++;
				}

				return prefix + sb.ToString() + key;
			}
			finally
			{
				Marshal.ZeroFreeGlobalAllocUnicode(pwdPtr);
			}
		} // func Encrypt

		public string Name => name;
	} // class XorEncryptProtector

	#endregion

	#region -- class DesEncryptProtectorBase --------------------------------------------

	public abstract class DesEncryptProtectorBase : ICredentialProtector
	{
		protected DesEncryptProtectorBase()
		{
		} // ctor

		private SymmetricAlgorithm GetKey()
			=> new DESCryptoServiceProvider()
			{
				Key = new byte[] { },
				IV = new byte[] {   }
			};

		public virtual bool CanDecryptPrefix(object encrypted)
		{
			var r = CanDecryptPrefix(encrypted, out var t, out var k);
			k.Dispose();
			return r;
		} // func CanDecryptPrefix

		protected abstract bool CanDecryptPrefix(object encrypted, out byte[] encryptedBytes, out SymmetricAlgorithm key);

		public bool TryDecrypt(object encrypted, out SecureString password)
		{
			if (CanDecryptPrefix(encrypted, out var encryptedBytes, out var key))
			{
				var ss = new SecureString();
				using (key)
				using (var m = new MemoryStream(encryptedBytes, 10, encryptedBytes.Length - 10, false))
				using (var src = new CryptoStream(m, key.CreateDecryptor(), CryptoStreamMode.Read))
				{
					while (true)
					{
						var bLow = src.ReadByte();
						if (bLow == -1)
						{
							password = ss;
							return true;
						}
						var bHigh = src.ReadByte();

						ss.AppendChar(unchecked((char)((byte)bLow | ((byte)bHigh << 8))));
					}
				}
			}
			else
			{
				password = null;
				return false;
			}
		} // func TryDecrypt

		protected unsafe byte[] EncryptCore(SecureString password, SymmetricAlgorithm key)
		{
			if (password == null || password.Length == 0)
				throw new ArgumentNullException(nameof(password));

			using (var m = new MemoryStream())
			using (var e = new CryptoStream(m, key.CreateEncryptor(), CryptoStreamMode.Write))
			{
				var pwdPtr = Marshal.SecureStringToGlobalAllocUnicode(password);
				try
				{
					var l = password.Length * 2;
					var b = (byte*)pwdPtr;
					for (var i = 0; i < l; i++, b++)
						e.WriteByte(*b);
				}
				finally
				{
					Marshal.ZeroFreeGlobalAllocUnicode(pwdPtr);
				}
				e.Close();
				return m.ToArray();
			}
		} // func EncryptCore

		public abstract object Encrypt(SecureString password);
	} // class DesEncryptProtectorBase

	#endregion

	#region -- class DesEncryptProtectorBinary ------------------------------------------

	public class DesEncryptProtectorBinary : DesEncryptProtectorBase
	{
		private readonly SecureString protectorKey;

		public DesEncryptProtectorBinary(SecureString protectorKey)
		{
			if (protectorKey.Length < 8)
				throw new ArgumentOutOfRangeException(nameof(protectorKey), "The key must at least 8 chars.");

			this.protectorKey = protectorKey.Copy();
		} // ctor

		private unsafe SymmetricAlgorithm BuildKey(byte[] ivData = null, int offset = 0)
		{
			var key = new byte[8];
			var protectorKeyPtr = Marshal.SecureStringToGlobalAllocUnicode(protectorKey);
			try
			{
				var i = 0;
				var c = (ushort*)protectorKeyPtr.ToPointer();
				var k = 0;
				while (i < protectorKey.Length)
				{
					key[k++] ^= unchecked((byte)*c);

					if (k >= 8)
						k = 0;

					i += 2;
					c++;
				}
			}
			finally
			{
				Marshal.ZeroFreeGlobalAllocUnicode(protectorKeyPtr);
			}

			var algoritm = new DESCryptoServiceProvider()
			{
				Key = key
			};

			if (ivData == null)
				algoritm.GenerateIV();
			else
			{
				var iv = new byte[8];
				Array.Copy(ivData, offset, iv, 0, 8);
				algoritm.IV = iv;
				Array.Clear(iv, 0, 8);
			}

			// zero memory
			Array.Clear(key, 0, key.Length);

			return algoritm;
		} // func BuildKey

		private bool CanDecryptPrefix(object encrypted, out byte[] encryptedBytes)
		{
			if (encrypted is byte[] v && v.Length > 0 && v[0] == 0x26 && CalcSimpleCheckSum(v) == v[1])
			{
				encryptedBytes = v;
				return true;
			}
			else
			{
				encryptedBytes = null;
				return false;
			}
		} // func CanDecryptPrefix

		public override bool CanDecryptPrefix(object encrypted)
			=> CanDecryptPrefix(encrypted, out var t);

		protected override bool CanDecryptPrefix(object encrypted, out byte[] encryptedBytes, out SymmetricAlgorithm key)
		{
			if (CanDecryptPrefix(encrypted, out encryptedBytes))
			{	
				key = BuildKey(encryptedBytes, 2);
				return true;
			}
			else
			{
				key = null;
				encryptedBytes = null;
				return false;
			}
		} // func CanDecryptPrefix

		protected byte[] EncryptCore(SecureString password)
		{
			if (password == null || password.Length == 0)
				return null;

			using (var key = BuildKey())
			{
				var encryptedPassword = EncryptCore(password, key);
				var encryptedData = new byte[10 + encryptedPassword.Length];

				// magic
				encryptedData[0] = 0x26;
				// iv
				var iv = key.IV;
				Array.Copy(iv, 0, encryptedData, 2, 8);
				Array.Clear(iv, 0, 8);
				// payload
				Array.Copy(encryptedPassword, 0, encryptedData, 10, encryptedPassword.Length);
				// checksum
				encryptedData[1] = CalcSimpleCheckSum(encryptedData);

				return encryptedData;
			}
		} // func EncryptCore

		public override object Encrypt(SecureString password)
			=> EncryptCore(password);


		private static byte CalcSimpleCheckSum(byte[] encryptedData)
		{
			var sum = (byte)0x82;
			for (var i = 2; i < encryptedData.Length; i++)
				sum ^= encryptedData[i];
			return sum;
		} // func CalcSimpleCheckSum
	} // class DesEncryptProtectorBinary

	#endregion

	#region -- class DesEncryptProtectorStatic ------------------------------------------

	public sealed class DesEncryptProtectorStatic : DesEncryptProtectorBase, ICredentialProtectorUI
	{
		private readonly string name;
		private readonly string prefix;
		private readonly byte[] keyPart;
		private readonly byte[] ivPart;

		public DesEncryptProtectorStatic(string name, string prefix, byte[] keyInfo)
		{
			if (keyInfo == null || keyInfo.Length != 16)
				throw new ArgumentOutOfRangeException(nameof(keyInfo), "The key must be 16 bytes.");

			this.name = name ?? throw new ArgumentNullException(nameof(name));
			this.prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));

			keyPart = new byte[8];
			ivPart = new byte[8];
			Array.Copy(keyInfo, 0, keyPart, 0, 8);
			Array.Copy(keyInfo, 8, ivPart, 0, 8);
		} // ctor

		private SymmetricAlgorithm CreateKey() 
			=> new DESCryptoServiceProvider()
			{
				Key = keyPart,
				IV = ivPart
			};

		private bool CanDecryptPrefix(object encrypted, out byte[] encryptedBytes)
		{
			if (encrypted is string s && s.Length > 0 && s.StartsWith(prefix))
			{
				encryptedBytes = Convert.FromBase64String(s.Substring(prefix.Length));
				return true;
			}
			else
			{
				encryptedBytes = null;
				return false;
			}
		} // func CanDecryptPrefix

		public override bool CanDecryptPrefix(object encrypted)
			=> CanDecryptPrefix(encrypted, out var t);

		protected override bool CanDecryptPrefix(object encrypted, out byte[] encryptedBytes, out SymmetricAlgorithm key)
		{
			if (CanDecryptPrefix(encrypted, out encryptedBytes))
			{
				key = CreateKey();
				return true;
			}
			else
			{
				key = null;
				encryptedBytes = null;
				return false;
			}
		} // func CanDecryptPrefix

		public override object Encrypt(SecureString password)
		{
			if (password == null || password.Length == 0)
				return null;

			using (var key = CreateKey())
				return prefix + Convert.ToBase64String(EncryptCore(password, key));
		} // func Encrypt

		public string Name => name;
	} // class DesEncryptProtectorStatic

	#endregion

	#region -- class DesEncryptProtectorBinary ------------------------------------------

	public sealed class DesEncryptProtectorString : DesEncryptProtectorBinary, ICredentialProtectorUI
	{
		private readonly string name;

		public DesEncryptProtectorString(string name, SecureString protectorKey)
			: base(protectorKey)
		{
			this.name = name ?? throw new ArgumentNullException(nameof(name));
		} // ctor


		public override bool CanDecryptPrefix(object encrypted)
			=> encrypted is string s
				&& TryConvertFromBase64(s, out var b)
				&& base.CanDecryptPrefix(b);

		protected override bool CanDecryptPrefix(object encrypted, out byte[] encryptedBytes, out SymmetricAlgorithm key)
		{
			if (encrypted is string s
				&& TryConvertFromBase64(s, out var b)
				&& base.CanDecryptPrefix(b, out encryptedBytes, out key))
				return true;
			else
			{
				key = null;
				encryptedBytes = null;
				return false;
			}
		} // func CanDecryptPrefix

		public override object Encrypt(SecureString password)
			=> ConvertToBase64(base.EncryptCore(password));
		
		private static string ConvertToBase64(byte[] v)
			=> v == null ? null : Convert.ToBase64String(v);

		private static bool TryConvertFromBase64(string s, out byte[] v)
		{
			try
			{
				v = Convert.FromBase64String(s);
				return true;
			}
			catch (FormatException)
			{
				v = null;
				return false;
			}
		} // func TryConvertFromBase64

		public string Name => name;
	} // class DesEncryptProtectorString

	#endregion

	#region -- class Protector ----------------------------------------------------------

	public static class Protector
	{
		#region -- class NoEncryptImplementation ----------------------------------------

		private sealed class NoEncryptImplementation : ICredentialProtector, ICredentialProtectorUI
		{
			public NoEncryptImplementation()
			{
			} // ctor

			public bool CanDecryptPrefix(object encrypted)
				=> CanDecryptPrefix(encrypted, out var t);

			public bool CanDecryptPrefix(object encrypted, out string encryptedString)
			{
				if (encrypted is string s && s.Length > 0 && s[0] == '0')
				{
					encryptedString = s;
					return true;
				}
				else
				{
					encryptedString = null;
					return false;
				}
			} // func CanDecryptPrefix

			public bool TryDecrypt(object encrypted, out SecureString password)
			{
				if (CanDecryptPrefix(encrypted, out var encryptedString))
				{
					password = encryptedString.CreateSecureString(1, encryptedString.Length - 1);
					return true;
				}
				else
				{
					password = null;
					return false;
				}
			} // func TryDecrypt

			public object Encrypt(SecureString password)
				=> "0" + password.GetPassword();

			public string Name => "No encryption";
		} // class NoEncryptImplementation

		#endregion

		#region -- class WindowsCredentialProtector -------------------------------------

		private sealed class WindowsCredentialProtector : ICredentialProtector
		{
			private const string prefix = "$user:";

			public object Encrypt(SecureString password)
			{
				var maxLength = 1024;
				var protectedCredentials = new StringBuilder(maxLength);
				var passwordPtr = Marshal.SecureStringToGlobalAllocUnicode(password);
				try
				{
					if (!CredProtect(false, passwordPtr, password.Length, protectedCredentials, ref maxLength, out var type))
						throw new Win32Exception();

					return prefix + protectedCredentials.ToString(0, maxLength);
				}
				finally
				{
					Marshal.ZeroFreeGlobalAllocUnicode(passwordPtr);
				}
			} // func Encrypt

			public bool CanDecryptPrefix(object encrypted)
				=> CanDecryptPrefix(encrypted, out var t);

			public bool CanDecryptPrefix(object encrypted, out string encryptedString)
			{
				if (encrypted is string s && s.Length > prefix.Length && s.StartsWith(prefix))
				{
					encryptedString = s.Substring(prefix.Length);
					return true;
				}
				else
				{
					encryptedString = null;
					return false;
				}
			} // func CanDecryptPrefix

			public unsafe bool TryDecrypt(object encrypted, out SecureString password)
			{
				if (CanDecryptPrefix(encrypted, out var encryptedString))
				{
					var maxLength = 1024;
					var passwordPtr = Marshal.AllocHGlobal(maxLength);
					try
					{
						if (!CredUnprotect(false, encryptedString, encryptedString.Length, passwordPtr, ref maxLength))
							throw new Win32Exception();

						var c = (char*)passwordPtr.ToPointer();
						password = new SecureString(c, maxLength);
						return true;
					}
					finally
					{
						ZeroMemory(passwordPtr, new IntPtr(1024));
						Marshal.FreeHGlobal(passwordPtr);
					}
				}
				else
				{
					password = null;
					return false;
				}
			} // func TryDecrypt
		} // class WindowsCredentialProtector

		#endregion

		public static bool HasValue(object encryptedPassword)
		{
			if (encryptedPassword == null)
				return false;
			else if (encryptedPassword is string s)
				return s.Length > 0;
			else if (encryptedPassword is byte[] v)
				return v.Length > 0;
			else
				return false;
		} // func HasValue

		public static bool EqualValue(object v1, object v2)
		{
			if (Object.ReferenceEquals(v1, v2))
				return true;
			else if (v1 is string s1 && v2 is string s2)
				return String.Compare(s1, s2, StringComparison.OrdinalIgnoreCase) == 0;
			else if (v1 is byte[] b1 && v2 is byte[] b2)
				return Procs.EqualBytes(b1, b2);
			else
				return false;
		} // func EqualValue

		public static ICredentialProtector NoProtector { get; } = new NoEncryptImplementation();
		public static ICredentialProtector UserProtector { get; } = new WindowsCredentialProtector();
	} // class Protector

	#endregion
}
