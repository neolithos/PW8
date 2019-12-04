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

	#endregion

	#region -- class NativeMethods ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal static class NativeMethods
	{
		//private const string shell32 = "shell32.dll";
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
		public static extern bool SetForegroundWindow(IntPtr hWnd);
		[DllImport(user32)]
		public static extern IntPtr SetActiveWindow(IntPtr hWnd);
	} // class NativeMethods

	#endregion
}
