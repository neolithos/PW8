using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Neo.PerfectWorking.Stuff
{
	#region -- class InteropSecurePassword ----------------------------------------------

	public sealed class InteropSecurePassword : IDisposable
	{
		private bool isDisposed;
		private readonly IntPtr passwordPtr;

		public InteropSecurePassword(SecureString password, bool unicode = true)
		{
			passwordPtr = password == null ? IntPtr.Zero : Marshal.SecureStringToGlobalAllocUnicode(password);
		} // ctor

		~InteropSecurePassword()
		{
			Dispose(false);
		} // dtor

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		private void Dispose(bool disposed)
		{
			if (isDisposed)
				throw new ObjectDisposedException(nameof(InteropSecurePassword));

			if (passwordPtr != IntPtr.Zero)
				Marshal.ZeroFreeGlobalAllocUnicode(passwordPtr);
			isDisposed = true;
		} // proc Dispose

		public IntPtr Value => passwordPtr;

		public static implicit operator IntPtr (InteropSecurePassword v)
			=> v.Value;
	} // class InteropSecurePassword

	#endregion

	#region -- class PasswordHelper -----------------------------------------------------

	public static class PasswordHelper
	{
		public static SecureString CreateSecureString(this string password)
			=> CreateSecureString(password, 0, password.Length);

		public unsafe static SecureString CreateSecureString(this string password, int offset, int length)
		{
			if (String.IsNullOrEmpty(password))
				throw new ArgumentNullException(nameof(password));

			fixed (char* c = password)
				return new SecureString(c + offset, length);
		} // func CereateSecureString

		public static string GetPassword(this SecureString ss)
		{
			if (ss == null)
				return null;
			else if (ss.Length == 0)
				return String.Empty;
			else
			{
				var pwdPtr = Marshal.SecureStringToGlobalAllocUnicode(ss);
				try
				{
					return Marshal.PtrToStringUni(pwdPtr);
				}
				finally
				{
					Marshal.ZeroFreeGlobalAllocUnicode(pwdPtr);
				}
			}
		} // func GetPassword

	} // class PasswordHelper

	#endregion
}
