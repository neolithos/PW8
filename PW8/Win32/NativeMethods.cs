using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Neo.PerfectWorking.Win32
{
	#region -- enum NotifyIconMessage ---------------------------------------------------

	internal enum NotifyIconMessage : uint
	{
		Add,
		Modify = 1,
		Delete = 2,
		SetFocus = 3,
		SetVersion = 4
	} // enum NotifyIconMessage

	#endregion

	#region -- enum NotifyIconFlags -----------------------------------------------------

	[Flags]
	internal enum NotifyIconFlags : uint
	{
		Message = 0x01,
		Icon = 0x02,
		Tip = 0x04,
		State = 0x08,
		Info = 0x10,
		Guid = 0x20,
		RealTime = 0x40,
		ShowTip = 0x80
	} // enum NotifyIconFlags

	#endregion

	#region -- struct NotifyIconData ----------------------------------------------------

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	internal struct NotifyIconData
	{
		public uint cbSize;
		public IntPtr hWnd;
		public uint uID;
		public NotifyIconFlags uFlags;
		public uint uCallbackMessage;
		public IntPtr hIcon;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string szTip;
		public uint dwState;
		public uint dwStateMask;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
		public string szInfo;
		public uint uTimeoutOrVersion;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
		public string szInfoTitle;
		public uint dwInfoFlags;
		public Guid guidItem;
		public IntPtr hBalloonIcon;
	} // class struct NotifyIconData

	#endregion

	#region -- struct RECT --------------------------------------------------------------

	[StructLayout(LayoutKind.Sequential)]
	public struct RECT
	{
		public int Left, Top, Right, Bottom;
	} // struct Rect

	#endregion

	internal static class NativeMethods
	{
		private const string shell32 = "shell32.dll";
		private const string user32 = "user32.dll";
		private const string kernel32 = "kernel32.dll";
		private const string comctl32 = "comctl32.dll";

		public const int WM_USER = 0x0400;

		public const int NIM_SELECT = WM_USER + 0;
		public const int NIM_KEYSELECT = WM_USER + 3;
		public const int NIM_POPUPOPEN = WM_USER + 6;
		public const int NIM_POPUPCLOSE = WM_USER + 7;

		public const int WM_SIZE = 0x0005;
		public const int WM_CONTEXTMENU = 0x007B;
		public const int WM_SYSCOMMAND = 0x0112;
		public const int WM_MOUSEMOVE = 0x0200;
		public const int WM_LBUTTONUP = 0x0202;
		public const int WM_RBUTTONUP = 0x0205;
		public const int WM_SIZING = 0x0214;
		public const int WM_MOVING = 0x0216;
		public const int WM_MOUSELEAVE = 0x02A3;
		public const int WM_MOUSEHOVER = 0x02A1;

		public const int GWL_STYLE = -16;

		public const int WS_MINIMIZEBOX = 0x00020000;
		public const int WS_MAXIMIZEBOX = 0x00010000;

		public const uint WS_POPUP = 0x80000000;

		[DllImport(user32, SetLastError = true)]
		public extern static uint SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
		[DllImport(user32, SetLastError = true)]
		public extern static uint GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport(shell32, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool Shell_NotifyIcon(NotifyIconMessage dwMessage, ref NotifyIconData lpdata);

		[DllImport(user32)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SetForegroundWindow(IntPtr hWnd);
		[DllImport(user32, SetLastError = true, CharSet = CharSet.Auto)]
		public static extern uint RegisterWindowMessage(string lpString);
		[DllImport(user32)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetCursorPos(ref Point pt);

		[DllImport(kernel32, SetLastError = true)]
		public static extern IntPtr GetModuleHandle(string lpModuleName);
		[DllImport(comctl32, SetLastError = true)]
		public static extern int LoadIconMetric(IntPtr instanceHandle, IntPtr iconId, int desiredMetric, out IntPtr icon);
	} // class NativeMethods
}
