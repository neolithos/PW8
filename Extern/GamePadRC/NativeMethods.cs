using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Neo.PerfectWorking.GamePadRC
{
	#region -- class NotifyIconData -----------------------------------------------------

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	internal class NotifyIconData
	{
		public uint cbSize = (uint)Marshal.SizeOf(typeof(NotifyIconData));
		public IntPtr hWnd;
		public uint uID;
		public uint uFlags;
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

	#region -- RAW-Input ----------------------------------------------------------------

	[StructLayout(LayoutKind.Sequential)]
	internal struct RAWINPUTDEVICELIST
	{
		public IntPtr hDevice;
		public uint dwType;
	} // struct RAWINPUTDEVICELIST

	[StructLayout(LayoutKind.Sequential)]
	internal struct RID_DEVICE_INFO_MOUSE
	{
		public uint dwId;
		public uint dwNumberOfButtons;
		public uint dwSampleRate;
		//[MarshalAs(UnmanagedType.U4)]
		public bool lHasHorizonalWheel;
	} // struct RID_DEVICE_INFO_MOUSE

	[StructLayout(LayoutKind.Sequential)]
	internal struct RID_DEVICE_INFO_KEYBOARD
	{
		public uint dwType;
		public uint dwSubType;
		public uint dwKeyboardMode;
		public uint dwNumberOfFunctionKeys;
		public uint dwNumberOfIndicators;
		public uint dwNumberOfKeysTotal;
	} // struct RID_DEVICE_INFO_KEYBOARD

	[StructLayout(LayoutKind.Sequential)]
	internal struct RID_DEVICE_INFO_HID
	{
		public uint dwVendorId;
		public uint dwProductId;
		public uint dwVersionNumber;
		public ushort usUsagePage;
		public ushort usUsage;
	} // struct RID_DEVICE_INFO_HID

	[StructLayout(LayoutKind.Explicit)]
	internal struct RID_DEVICE_INFO
	{
		[FieldOffset(0)]
		public uint dwSize;
		[FieldOffset(4)]
		public uint dwType;

		[FieldOffset(8)]
		public RID_DEVICE_INFO_MOUSE mouse;
		[FieldOffset(8)]
		public RID_DEVICE_INFO_KEYBOARD keyboard;
		[FieldOffset(8)]
		public RID_DEVICE_INFO_HID hid;
	} // struct RID_DEVICE_INFO

	[StructLayout(LayoutKind.Sequential)]
	internal struct RAWINPUTDEVICE
	{
		[MarshalAs(UnmanagedType.U2)]
		public ushort wUsagePage;
		[MarshalAs(UnmanagedType.U2)]
		public ushort wUsage;
		[MarshalAs(UnmanagedType.U4)]
		public uint dwFlags;
		public IntPtr hwndTarget;
	} // struct RAWINPUTDEVICE

	[StructLayout(LayoutKind.Explicit)]
	internal struct RAWINPUT
	{
		[FieldOffset(0)]
		public RAWINPUTHEADER header;
		//[FieldOffset(16 oder 24)]
		//public RAWMOUSE mouse;
		//[FieldOffset(16 oder 24)]
		//public RAWKEYBOARD keyboard;
		//[FieldOffset(16 oder 24)]
		//public RAWHID hid;
	} // struct RAWINPUT

	[StructLayout(LayoutKind.Sequential)]
	internal struct RAWINPUTHEADER
	{
		[MarshalAs(UnmanagedType.U4)]
		public int dwType;
		[MarshalAs(UnmanagedType.U4)]
		public int dwSize;
		public IntPtr hDevice;
		public IntPtr wParam;
	} // struct RAWINPUTHEADER

	[StructLayout(LayoutKind.Sequential)]
	internal struct RAWKEYBOARD
	{
		[MarshalAs(UnmanagedType.U2)]
		public ushort MakeCode;
		[MarshalAs(UnmanagedType.U2)]
		public ushort Flags;
		[MarshalAs(UnmanagedType.U2)]
		public ushort Reserved;
		[MarshalAs(UnmanagedType.U2)]
		public ushort VKey;
		[MarshalAs(UnmanagedType.U4)]
		public uint Message;
		//[MarshalAs(UnmanagedType.U4)]
		public IntPtr ExtraInformation;
	} // struct RAWKEYBOARD

	[StructLayout(LayoutKind.Sequential)]
	internal struct RAWHID
	{
		/// <summary>Size of the HID data in bytes.</summary>
		public uint dwSize;
		/// <summary>Number of HID in Data.</summary>
		public uint dwCount;
		// <summary>Data for the HID.</summary>
		//public IntPtr pData;
	} // struct RAWHID

	[StructLayout(LayoutKind.Sequential)]
	internal struct MOUSEINPUT
	{
		public int dx;
		public int dy;
		public uint mouseData;
		public uint dwFlags;
		public uint time;
		public IntPtr dwExtraInfo;
	} // struct MOUSEINPUT

	[StructLayout(LayoutKind.Sequential)]
	internal struct KEYBDINPUT
	{
		public ushort wVk;
		public ushort wScan;
		public uint dwFlags;
		public uint time;
		public IntPtr dwExtraInfo;
	} // struct KEYBDINPUT

	[StructLayout(LayoutKind.Sequential)]
	internal struct HARDWAREINPUT
	{
		public int uMsg;
		public short wParamL;
		public short wParamH;
	} // struct HARDWAREINPUT

	[StructLayout(LayoutKind.Explicit)]
	internal struct MOUSEKEYBDHARDWAREINPUT
	{
		[FieldOffset(0)]
		public MOUSEINPUT mi;
		[FieldOffset(0)]
		public KEYBDINPUT ki;
		[FieldOffset(0)]
		public HARDWAREINPUT hi;
	} // struct MOUSEKEYBDHARDWAREINPUT

	[StructLayout(LayoutKind.Sequential)]
	internal struct INPUT
	{
		public int type;
		public MOUSEKEYBDHARDWAREINPUT mkhi;
	} // struct INPUT

	#endregion

	#region -- class NativeMethods ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal static class NativeMethods
	{
		private const string shell32 = "shell32.dll";
		private const string user32 = "user32.dll";

		public const uint RID_INPUT = 0x10000003;

		public const uint RIM_TYPEMOUSE = 0;
		public const uint RIM_TYPEKEYBOARD = 1;
		public const uint RIM_TYPEHID = 2;

		public const uint RIDI_PREPARSEDDATA = 0x20000005;
		public const uint RIDI_DEVICENAME = 0x20000007;  // the return valus is the character length, not the byte size
		public const uint RIDI_DEVICEINFO = 0x2000000b;

		public const uint RIDEV_EXINPUTSINK = 0x00001000;
		public const uint RIDEV_INPUTSINK = 0x00000200;
		//public const uint RIDEV_PAGEONLY = 0x00000020;
		//public const int RIDI_DEVICENAME = 0x20000007;
		//public const uint RIDI_DEVICEINFO = 0x2000000B;

		public const int INPUT_MOUSE = 0;
		public const int INPUT_KEYBOARD = 1;
		public const int INPUT_HARDWARE = 2;

		public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
		public const uint KEYEVENTF_KEYUP = 0x0002;
		public const uint KEYEVENTF_UNICODE = 0x0004;
		public const uint KEYEVENTF_SCANCODE = 0x0008;

		public const uint XBUTTON1 = 0x0001;
		public const uint XBUTTON2 = 0x0002;

		public const uint MOUSEEVENTF_MOVE = 0x0001;
		public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
		public const uint MOUSEEVENTF_LEFTUP = 0x0004;
		public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
		public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
		public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
		public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
		public const uint MOUSEEVENTF_XDOWN = 0x0080;
		public const uint MOUSEEVENTF_XUP = 0x0100;
		public const uint MOUSEEVENTF_WHEEL = 0x0800;
		public const uint MOUSEEVENTF_HWHEEL = 0x1000;
		public const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
		public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

		[DllImport(user32, SetLastError = true)]
		public extern static bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevice, int iNumDevices, int cbSize);
		[DllImport(user32, SetLastError = true)]
		public extern static uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, int cbSizeHeader);
		[DllImport(user32)]
		public static extern int GetRawInputDeviceList(IntPtr pRawInputDeviceList, ref uint dwNumDevices, int dwSize);
		[DllImport(user32)]
		public static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint dwCommand, IntPtr pData, ref uint dataSize);
		[return: MarshalAs(UnmanagedType.Bool)]
		[DllImport(user32, SetLastError = true)]
		public extern static bool DefRawInputProc(IntPtr paRawInput, uint dwInput, int cbSizeHeader);

		[DllImport(user32, ExactSpelling = true)]
		public static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, IntPtr lpTimerFunc);
		[DllImport(user32, ExactSpelling = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool KillTimer(IntPtr hWnd, IntPtr uIDEvent);

		[DllImport(user32)]
		public static extern IntPtr GetMessageExtraInfo();

		[DllImport(user32, SetLastError = true)]
		public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

		[DllImport(user32)]
		public static extern bool SetForegroundWindow(IntPtr hWnd);
		[DllImport(user32)]
		public static extern IntPtr SetActiveWindow(IntPtr hWnd);
	} // class NativeMethods

	#endregion
}
