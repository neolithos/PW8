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
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Neo.PerfectWorking.Stuff;
using TecWare.DE.Stuff;
using static Neo.PerfectWorking.Cred.Provider.NativeMethods;

namespace Neo.PerfectWorking.Cred.Provider
{
	#region -- class XorEncryptProtector ------------------------------------------------

	public sealed class XorEncryptProtector : ICredentialProtector
	{
		private const string cryptSimpleKeyChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0987654321-_";

		private readonly string prefix;
		private readonly SecureString protectorKey;

		#region -- Ctor/Dtor ------------------------------------------------------------

		public XorEncryptProtector(string prefix, SecureString key)
		{
			this.prefix = prefix;
			this.protectorKey = key.Copy();
		} // ctor

		public void Dispose()
		{
			protectorKey?.Dispose();
		} // proc Dispose

		#endregion

		#region -- Decrypt --------------------------------------------------------------

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

				using (var keyPtr = new InteropSecurePassword(protectorKey))
				{
					byte GetNextKey()
					{
						var r = (byte)Marshal.ReadByte(keyPtr, k);
						k += 2;
						if (k >= keyPtr.Size)
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

				password = ss;
				return true;
			}
			else
			{
				password = null;
				return false;
			}
		} // func TryDecrypt

		#endregion

		#region -- Encrypt --------------------------------------------------------------

		public unsafe object Encrypt(SecureString password)
		{
			if (password == null || password.Length == 0)
				return null;

			using (var pwdPtr = new InteropSecurePassword(password))
			using (var keyPtr = new InteropSecurePassword(protectorKey))
			{
				var p = (byte*)pwdPtr.Value;
				var rand = new Random(Environment.TickCount);
				var key = cryptSimpleKeyChars[rand.Next(cryptSimpleKeyChars.Length)];
				var sb = new StringBuilder(password.Length << 2);
				var j = (int)key % protectorKey.Length;
				var l = password.Length << 1;
				for (var i = 0; i < l; i++)
				{
					var c = unchecked((byte)(*p ^ Marshal.ReadByte(keyPtr, j) ^ (byte)key));
					j += 2;
					if (j >= keyPtr.Size)
						j = 0;
					sb.AppendFormat("{0:X2}", c);

					p++;
				}

				return prefix + sb.ToString() + key;
			}
		} // func Encrypt

		#endregion
	} // class XorEncryptProtector

	#endregion

	#region -- class DesEncryptProtectorBase --------------------------------------------

	public abstract class DesEncryptProtectorBase : ICredentialProtector
	{
		#region -- Ctor/Dtor ------------------------------------------------------------

		protected DesEncryptProtectorBase()
		{
		} // ctor

		~DesEncryptProtectorBase()
		{
			Dispose(false);
		} // dtor

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		} // proc Dispose

		protected virtual void Dispose(bool disposing)
		{
		} // proc Dispose

		#endregion

		#region -- GetKey ---------------------------------------------------------------

		private SymmetricAlgorithm GetKey()
			=> new DESCryptoServiceProvider()
			{
				Key = new byte[] { },
				IV = new byte[] { }
			};

		#endregion

		#region -- Decrypt --------------------------------------------------------------

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
				try
				{
					using (key)
					using (var ss = new SecureString())
					using (var m = new MemoryStream(encryptedBytes, false))
					using (var src = new CryptoStream(m, key.CreateDecryptor(), CryptoStreamMode.Read))
					{
						while (true)
						{
							var bLow = src.ReadByte();
							if (bLow == -1)
							{
								password = ss.Copy();
								return true;
							}
							var bHigh = src.ReadByte();

							ss.AppendChar(unchecked((char)((byte)bLow | ((byte)bHigh << 8))));
						}
					}
				}
				catch (CryptographicException)
				{
					password = ErrorPassword;
					return true;
				}
			}
			else
			{
				password = null;
				return false;
			}
		} // func TryDecrypt

		#endregion

		#region -- Encrypt --------------------------------------------------------------

		protected unsafe byte[] EncryptCore(SecureString password, SymmetricAlgorithm key)
		{
			if (password == null || password.Length == 0)
				throw new ArgumentNullException(nameof(password));

			using (var m = new MemoryStream())
			using (var e = new CryptoStream(m, key.CreateEncryptor(), CryptoStreamMode.Write))
			{
				using (var pwdPtr = new InteropSecurePassword(password))
				{
					var b = (byte*)pwdPtr.Value;
					for (var i = 0; i < pwdPtr.Size; i++, b++)
						e.WriteByte(*b);
				}
				e.FlushFinalBlock();
				return m.ToArray();
			}
		} // func EncryptCore

		public abstract object Encrypt(SecureString password);

		#endregion

		static DesEncryptProtectorBase()
		{
			var ss = new SecureString();
			ss.AppendChar('E');
			ss.AppendChar('R');
			ss.AppendChar('R');
			ss.AppendChar('O');
			ss.AppendChar('R');
			ss.MakeReadOnly();
			ErrorPassword = ss;
		}
		public static SecureString ErrorPassword { get; }
	} // class DesEncryptProtectorBase

	#endregion

	#region -- class DesEncryptProtectorBinary ------------------------------------------

	public class DesEncryptProtectorBinary : DesEncryptProtectorBase
	{
		private readonly SecureString protectorKey;

		#region -- Ctor/Dtor ------------------------------------------------------------

		public DesEncryptProtectorBinary(SecureString protectorKey)
		{
			if (protectorKey.Length < 8)
				throw new ArgumentOutOfRangeException(nameof(protectorKey), "The key must at least 8 chars.");

			this.protectorKey = protectorKey.Copy();
		} // ctor

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			protectorKey?.Dispose();
		} // proc Dispose

		#endregion

		#region -- BuildKey -------------------------------------------------------------

		private unsafe SymmetricAlgorithm BuildKey(byte[] ivData = null, int offset = 0)
		{
			var key = new byte[8];
			using (var protectorKeyPtr = new InteropSecurePassword(protectorKey))
			{
				var i = 0;
				var c = (ushort*)protectorKeyPtr.Value;
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

		#endregion

		#region -- Decrypt --------------------------------------------------------------

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
			if (CanDecryptPrefix(encrypted, out var data))
			{
				key = BuildKey(data, 2);
				encryptedBytes = new byte[data.Length - 10];
				Array.Copy(data, 10, encryptedBytes, 0, data.Length - 10);
				return true;
			}
			else
			{
				key = null;
				encryptedBytes = null;
				return false;
			}
		} // func CanDecryptPrefix

		#endregion

		#region -- Encrypt --------------------------------------------------------------

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

		#endregion

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

	public sealed class DesEncryptProtectorStatic : DesEncryptProtectorBase
	{
		private readonly string prefix;
		private readonly byte[] keyPart;
		private readonly byte[] ivPart;

		#region -- Ctor/Dtor ------------------------------------------------------------

		public DesEncryptProtectorStatic(string prefix, byte[] keyInfo)
		{
			if (keyInfo == null || keyInfo.Length != 16)
				throw new ArgumentOutOfRangeException(nameof(keyInfo), "The key must be 16 bytes.");

			this.prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));

			keyPart = new byte[8];
			ivPart = new byte[8];
			Array.Copy(keyInfo, 0, keyPart, 0, 8);
			Array.Copy(keyInfo, 8, ivPart, 0, 8);
		} // ctor

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (keyPart != null)
				Array.Clear(keyPart, 0, keyPart.Length);
			if (ivPart != null)
				Array.Clear(ivPart, 0, ivPart.Length);
		} // proc Dispose

		#endregion

		#region -- CreateKey ------------------------------------------------------------

		private SymmetricAlgorithm CreateKey()
			=> new DESCryptoServiceProvider()
			{
				Key = keyPart,
				IV = ivPart
			};

		#endregion

		#region -- Decrypt --------------------------------------------------------------

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

		#endregion

		#region -- Encrypt --------------------------------------------------------------

		public override object Encrypt(SecureString password)
		{
			if (password == null || password.Length == 0)
				return null;

			using (var key = CreateKey())
				return prefix + Convert.ToBase64String(EncryptCore(password, key));
		} // func Encrypt

		#endregion
	} // class DesEncryptProtectorStatic

	#endregion

	#region -- class DesEncryptProtectorString ------------------------------------------

	public sealed class DesEncryptProtectorString : DesEncryptProtectorBinary
	{
		#region -- Ctor/Dtor ------------------------------------------------------------

		public DesEncryptProtectorString(SecureString protectorKey)
			: base(protectorKey)
		{
		} // ctor

		#endregion

		#region -- Decrypt --------------------------------------------------------------

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

		#endregion

		#region -- Encrypt --------------------------------------------------------------

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

		#endregion
	} // class DesEncryptProtectorString

	#endregion

	#region -- class WindowsCryptProtector ----------------------------------------------

	public sealed class WindowsCryptProtector : ICredentialProtector
	{
		private readonly bool emitBinary;

		private readonly bool localMachine = false;
		private readonly byte[] secureKey;

		#region -- Ctor/Dtor ------------------------------------------------------------

		public WindowsCryptProtector(bool emitBinary = false, bool localMachine = false, byte[] secureKey = null)
		{
			this.emitBinary = emitBinary;
			this.localMachine = localMachine;
			this.secureKey = secureKey;
		} // ctor

		~WindowsCryptProtector()
		{
			Dispose();
		} // dtor

		public void Dispose()
		{
			if (secureKey != null)
				Array.Clear(secureKey, 0, secureKey.Length);
		} // proc Dispose

		#endregion

		#region -- Decrypt --------------------------------------------------------------

		public bool CanDecryptPrefix(object encrypted)
		{
			if (TryDecrypt(encrypted, false, out var ss))
			{
				ss?.Dispose();
				return true;
			}
			else
				return false;
		} // func CanDecryptPrefix

		public bool TryDecrypt(object encrypted, out SecureString password)
			=> TryDecrypt(encrypted, true, out password);

		private unsafe bool TryDecrypt(object encrypted, bool needPassword, out SecureString password)
		{
			byte[] GetBytes()
			{
				if (encrypted is byte[] b)
					return b;
				else if (encrypted is string s && Procs.TryConvertToBytes(s, out var b2))
					return b2;
				else
					return null;
			} // func GetBytes

			var hEncryptedBytes = default(GCHandle);
			var dataOut = default(DATA_BLOB);
			var dataIV = default(DATA_BLOB);
			var hIV = default(GCHandle);
			try
			{
				// get byte array
				var encryptedBytes = GetBytes();
				if (encryptedBytes == null)
				{
					password = null;
					return false;
				}

				// allocate structure
				hEncryptedBytes = GCHandle.Alloc(encryptedBytes, GCHandleType.Pinned);
				var dataIn = new DATA_BLOB()
				{
					DataSize = encryptedBytes.Length,
					DataPtr = hEncryptedBytes.AddrOfPinnedObject()
				};

				// create iv
				if (secureKey != null)
				{
					hIV = GCHandle.Alloc(secureKey, GCHandleType.Pinned);
					dataIV.DataPtr = hIV.AddrOfPinnedObject();
					dataIV.DataSize = secureKey.Length;
				}

				// crypt data
				var flags = 1;
				if (localMachine)
					flags |= 4;
				if (!CryptUnprotectData(ref dataIn, null, ref dataIV, IntPtr.Zero, IntPtr.Zero, flags, ref dataOut))
					throw new Win32Exception();

				// unpack data
				if (dataOut.DataPtr == IntPtr.Zero)
					throw new OutOfMemoryException();

				password = needPassword ? new SecureString((char*)dataOut.DataPtr, dataOut.DataSize / 2) : null;
				return true;
			}
			catch
			{
				password = null;
				return false;
			}
			finally
			{
				if (hEncryptedBytes.IsAllocated)
					hEncryptedBytes.Free();
				if (hIV.IsAllocated)
					hIV.Free();

				if (dataOut.DataPtr != IntPtr.Zero)
				{
					ZeroMemory(dataOut.DataPtr, new IntPtr(dataOut.DataSize));
					LocalFree(dataOut.DataPtr);
				}
			}
		} // func TryDecrypt

		#endregion

		#region -- Encrypt --------------------------------------------------------------

		public object Encrypt(SecureString password)
		{
			var passwordPtr = new InteropSecurePassword(password);
			var dataOut = default(DATA_BLOB);
			var dataIV = default(DATA_BLOB);
			var hIV = default(GCHandle);
			try
			{
				// pack password
				var dataIn = new DATA_BLOB()
				{
					DataSize = passwordPtr.Size,
					DataPtr = passwordPtr.Value
				};

				// create iv
				if (secureKey != null)
				{
					hIV = GCHandle.Alloc(secureKey, GCHandleType.Pinned);
					dataIV.DataPtr = hIV.AddrOfPinnedObject();
					dataIV.DataSize = secureKey.Length;
				}

				// crypt data
				var flags = 1;
				if (localMachine)
					flags |= 4;
				if (!CryptProtectData(ref dataIn, null, ref dataIV, IntPtr.Zero, IntPtr.Zero, flags, ref dataOut))
					throw new Win32Exception();

				// unpack data
				if (dataOut.DataPtr == IntPtr.Zero)
					throw new OutOfMemoryException();

				var data = new byte[dataOut.DataSize];
				Marshal.Copy(dataOut.DataPtr, data, 0, dataOut.DataSize);

				return emitBinary
					? (object)data
					: (object)Procs.ConvertToString(data);
			}
			finally
			{
				passwordPtr.Dispose();
				if (hIV.IsAllocated)
					hIV.Free();

				if (dataOut.DataPtr != IntPtr.Zero)
				{
					ZeroMemory(dataOut.DataPtr, new IntPtr(dataOut.DataSize));
					LocalFree(dataOut.DataPtr);
				}
			}
		} // func Encrypt

		#endregion
	} // class WindowsCryptProtector 

	#endregion

	#region -- class PowerShellProtector ------------------------------------------------

	public sealed class PowerShellProtector : ICredentialProtector
	{
		private const string secureStringHeader = "76492d1116743f0423413b16050a5345";

		private readonly byte[] secureKey;

		#region -- Ctor/Dtor ------------------------------------------------------------

		public PowerShellProtector(byte[] secureKey)
		{
			if (secureKey != null)
			{
				if (secureKey.Length != 16 && secureKey.Length != 24 && secureKey.Length != 32)
					throw new ArgumentOutOfRangeException(nameof(secureKey), "Key must 128,192,256 bits long.");
			}
			this.secureKey = secureKey;
		} // ctor

		public PowerShellProtector()
			: this((byte[])null)
		{
		} // ctor

		public PowerShellProtector(SecureString secureKey)
			: this(GetUnicodeBytes(secureKey))
		{
		} // ctor

		~PowerShellProtector()
		{
			Dispose();
		} // dtor

		public void Dispose()
		{
			if (secureKey != null)
				Array.Clear(secureKey, 0, secureKey.Length);
		} // proc Dispose

		#endregion

		#region -- Decrypt --------------------------------------------------------------

		public bool CanDecryptPrefix(object encrypted)
		{
			if (TryDecrypt(encrypted, false, out var ss))
			{
				ss?.Dispose();
				return true;
			}
			else
				return false;
		} // func CanDecryptPrefix

		public bool TryDecrypt(object encrypted, out SecureString password)
			=> TryDecrypt(encrypted, true, out password);

		private bool TryDecrypt(object encrypted, bool needPassword, out SecureString password)
		{
			if (encrypted is string data)
			{
				if (data.StartsWith(secureStringHeader)) // AES encrypted
					return TryDecryptAes(data, needPassword, out password);
				else if ((data.Length & 1) == 0 && TryWindowsDecrypt(data, needPassword, out password)) // Windows Crypt protected
					return true;
				else
				{
					password = null;
					return false;
				}
			}
			else
			{
				password = null;
				return false;
			}
		} // func TryDecrypt

		private bool TryDecryptAes(string data, bool needPassword, out SecureString password)
		{
			try
			{
				// unpack parameter
				var byteData = Convert.FromBase64String(data.Substring(secureStringHeader.Length, data.Length - secureStringHeader.Length));
				var unpackedData = Encoding.Unicode.GetString(byteData).Split('|');
				if (unpackedData.Length != 3 || unpackedData[0] != "2")
					throw new FormatException();

				if (!Procs.TryConvertToBytes(unpackedData[2], out var encryptedPassword))
				{
					password = null;
					return false;
				}
				var iv = Convert.FromBase64String(unpackedData[1]);

				// decrypt
				using var aes = Aes.Create();
				using var encrypt = aes.CreateDecryptor(secureKey, iv);
				using var srcMem = new MemoryStream(encryptedPassword, false);
				using var srcCrypt = new CryptoStream(srcMem, encrypt, CryptoStreamMode.Read);

				if (needPassword)
				{
					using var ss = new SecureString();
					while (true)
					{
						var bLow = srcCrypt.ReadByte();
						if (bLow == -1)
						{
							password = ss.Copy();
							return true;
						}
						var bHigh = srcCrypt.ReadByte();

						ss.AppendChar(unchecked((char)((byte)bLow | ((byte)bHigh << 8))));
					}
				}
				else
					password = null;
				return true;
			}
			catch (FormatException)
			{
				password = null;
				return false;
			}
		} // func TryDecryptAes

		private unsafe bool TryWindowsDecrypt(string dataString, bool needPassword, out SecureString password)
		{
			var dataOut = default(DATA_BLOB);
			var dataIV = default(DATA_BLOB);
			var hData = default(GCHandle);
			try
			{
				if (!Procs.TryConvertToBytes(dataString, out var data))
				{
					password = null;
					return false;
				}

				hData = GCHandle.Alloc(data, GCHandleType.Pinned);
				var dataIn = new DATA_BLOB()
				{
					DataSize = data.Length,
					DataPtr = hData.AddrOfPinnedObject()
				};

				// crypt data
				if (!CryptUnprotectData(ref dataIn, null, ref dataIV, IntPtr.Zero, IntPtr.Zero, 1, ref dataOut))
					throw new Win32Exception();

				// unpack data
				if (dataOut.DataPtr == IntPtr.Zero)
					throw new OutOfMemoryException();

				password = needPassword ? new SecureString((char*)dataOut.DataPtr, dataOut.DataSize / 2) : null;
				return true;
			}
			catch
			{
				password = null;
				return false;
			}
			finally
			{
				if (hData.IsAllocated)
					hData.Free();

				if (dataOut.DataPtr != IntPtr.Zero)
				{
					ZeroMemory(dataOut.DataPtr, new IntPtr(dataOut.DataSize));
					LocalFree(dataOut.DataPtr);
				}
			}
		} // func TryWindowsDecrypt

		#endregion

		#region -- Encrypt --------------------------------------------------------------

		public object Encrypt(SecureString password)
		{
			using (var passwordPtr = new InteropSecurePassword(password))
			{
				return secureKey == null
					? ProtectCurrentUser(passwordPtr)
					: ProtectAES(passwordPtr);
			}
		} // func Encrypt

		private static string ProtectCurrentUser(InteropSecurePassword password)
		{
			var dataOut = default(DATA_BLOB);
			var dataIV = default(DATA_BLOB);
			try
			{
				var dataIn = new DATA_BLOB()
				{
					DataSize = password.Size,
					DataPtr = password.Value
				};

				// crypt data
				if (!CryptProtectData(ref dataIn, null, ref dataIV, IntPtr.Zero, IntPtr.Zero, 1, ref dataOut))
					throw new Win32Exception();

				// unpack data
				if (dataOut.DataPtr == IntPtr.Zero)
					throw new OutOfMemoryException();

				var data = new byte[dataOut.DataSize];
				Marshal.Copy(dataOut.DataPtr, data, 0, dataOut.DataSize);

				return Procs.ConvertToString(data);
			}
			finally
			{
				if (dataOut.DataPtr != IntPtr.Zero)
				{
					ZeroMemory(dataOut.DataPtr, new IntPtr(dataOut.DataSize));
					LocalFree(dataOut.DataPtr);
				}
			}
		} // func ProtectCurrentUser

		private unsafe object ProtectAES(InteropSecurePassword password)
		{
			var aes = Aes.Create();
			var iv = aes.IV;

			using var encrypt = aes.CreateEncryptor(secureKey, aes.IV);
			using var destMem = new MemoryStream();
			using var destCrypt = new CryptoStream(destMem, encrypt, CryptoStreamMode.Write);

			var c = (byte*)password.Value;
			for (var i = 0; i < password.Size; i++)
			{
				destCrypt.WriteByte(*c);
				c++;
			}
			destCrypt.FlushFinalBlock();

			return secureStringHeader + Convert.ToBase64String(Encoding.Unicode.GetBytes("2|" + Convert.ToBase64String(iv) + "|" + Procs.ConvertToString(destMem.ToArray())));
		} // func ProtectAES

		#endregion

		private static byte[] GetUnicodeBytes(SecureString secureKey)
		{
			using var pwd = new InteropSecurePassword(secureKey);
			var r = new byte[pwd.Size];
			Marshal.Copy(pwd.Value, r, 0, pwd.Size);
			return r;
		} // func GetUnicodeByte
	} // class PowerShellProtector 

	#endregion

	#region -- class Protector ----------------------------------------------------------

	public static class Protector
	{
		#region -- class NoEncryptImplementation ----------------------------------------

		private sealed class NoEncryptImplementation : ICredentialProtector
		{
			public NoEncryptImplementation()
			{
			} // ctor

			public void Dispose()
			{
			} // proc Dispose

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
				using (var passwordPtr = new InteropSecurePassword(password))
				{
					if (!CredProtect(false, passwordPtr, password.Length, protectedCredentials, ref maxLength, out var type))
						throw new Win32Exception();

					return prefix + protectedCredentials.ToString(0, maxLength);
				}
			} // func Encrypt

			public void Dispose()
			{
			} // proc Dispose

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
			if (ReferenceEquals(v1, v2))
				return true;
			else if (v1 is string s1 && v2 is string s2)
				return String.Compare(s1, s2, StringComparison.OrdinalIgnoreCase) == 0;
			else if (v1 is byte[] b1 && v2 is byte[] b2)
				return Procs.CompareBytes(b1, b2);
			else
				return false;
		} // func EqualValue

		public static ICredentialProtector NoProtector { get; } = new NoEncryptImplementation();
		public static ICredentialProtector UserProtector { get; } = new WindowsCredentialProtector();
	} // class Protector

	#endregion
}
