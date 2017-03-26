using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Neo.PerfectWorking.QuickConnect
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	internal struct NETRESOURCE
	{
		public uint dwScope;
		public uint dwType;
		public uint dwDisplayType;
		public uint dwUsage;
		public string lpLocalName;
		public string lpRemoteName;
		public string lpComment;
		public string lpProvider;
	} // class NETRESOURCE

	internal static class NativeMethods
	{
		private const string mpr = "mpr.dll";

		[DllImport(mpr, EntryPoint = "WNetOpenEnumW", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
		public static extern int WNetOpenEnum(uint dwScope, uint dwType, uint dwUsage, IntPtr p, out IntPtr lphEnum);

		[DllImport(mpr, EntryPoint = "WNetCloseEnum", CallingConvention = CallingConvention.Winapi)]
		public static extern int WNetCloseEnum(IntPtr hEnum);

		[DllImport(mpr, EntryPoint = "WNetEnumResourceW", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
		public static extern int WNetEnumResource(IntPtr hEnum, ref int lpcCount, IntPtr buffer, ref int lpBufferSize);

		[DllImport(mpr, EntryPoint = "WNetAddConnection3W", CharSet = CharSet.Unicode)]
		public static extern int WNetAddConnection3(IntPtr hWndOwner, ref NETRESOURCE lpNetResource, IntPtr lpPassword, string lpUserName, int dwFlags);
		[DllImport(mpr, EntryPoint = "WNetCancelConnection2W", CharSet = CharSet.Unicode)]
		public static extern int WNetCancelConnection2(string lpName, uint dwFlags, bool fForce);

		[DllImport(mpr, EntryPoint = "WNetGetLastErrorW", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
		public static extern int WNetGetLastError(ref int lpError, StringBuilder lpErrorBuf, int nErrorBufSize, StringBuilder lpNameBuf, int nNameBufSize);
	} // class NativeMethods
}
