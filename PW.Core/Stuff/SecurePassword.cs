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
using System.Net;
using System.Runtime.InteropServices;
using System.Security;

namespace Neo.PerfectWorking.Stuff
{
	#region -- class InteropSecurePassword --------------------------------------------

	public sealed class InteropSecurePassword : IDisposable
	{
		private bool isDisposed;
		private readonly IntPtr passwordPtr;
		private readonly int dataLength;

		public InteropSecurePassword(SecureString password, bool unicode = true)
		{
			this.passwordPtr = password == null ? IntPtr.Zero : Marshal.SecureStringToGlobalAllocUnicode(password);
			this.dataLength = password == null ? 0 : (unicode ? password.Length * 2 : password.Length);
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
		public int Size => dataLength;

		public static implicit operator IntPtr (InteropSecurePassword v)
			=> v.Value;
	} // class InteropSecurePassword

	#endregion

	#region -- class PasswordHelper ---------------------------------------------------

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

		public static void SplitUserName(string userName, out string domain, out string user)
		{
			var domainSplit = userName.IndexOf('\\');
			if(domainSplit >= 0)
			{
				domain = userName.Substring(0, domainSplit);
				user = userName.Substring(domainSplit + 1);
			}
			else
			{
				domainSplit = userName.IndexOf('@');
				if (domainSplit >= 0)
				{
					domain = userName.Substring(domainSplit + 1);
					user = userName.Substring(0, domainSplit);
				}
				else
				{
					domain = null;
					user = userName;
				}
			}
		} // SplitUserName

		public static NetworkCredential CreateNetworkCredential(string userName, SecureString password)
		{
			SplitUserName(userName, out var domain, out var user);
			return domain == null
				? new NetworkCredential(user, password)
				: new NetworkCredential(user, password, domain);
		} // CreateNetworkCredential
	} // class PasswordHelper

	#endregion
}
