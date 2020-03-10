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
using System.Runtime.InteropServices;

namespace Neo.PerfectWorking
{
	#region -- struct MOUSEINPUT ------------------------------------------------------

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

	#endregion

	#region -- struct KEYBDINPUT ------------------------------------------------------

	[StructLayout(LayoutKind.Sequential)]
	internal struct KEYBDINPUT
	{
		public ushort wVk;
		public ushort wScan;
		public uint dwFlags;
		public uint time;
		public IntPtr dwExtraInfo;
	} // struct KEYBDINPUT

	#endregion

	#region -- struct HARDWAREINPUT ---------------------------------------------------

	[StructLayout(LayoutKind.Sequential)]
	internal struct HARDWAREINPUT
	{
		public int uMsg;
		public short wParamL;
		public short wParamH;
	} // struct HARDWAREINPUT

	#endregion

	#region -- struct INPUT -----------------------------------------------------------

	[StructLayout(LayoutKind.Explicit)]
	internal struct INPUT
	{
		[FieldOffset(0)]
		public int type;

		[FieldOffset(8)]
		public MOUSEINPUT mi;
		[FieldOffset(8)]
		public KEYBDINPUT ki;
		[FieldOffset(8)]
		public HARDWAREINPUT hi;
	} // struct INPUT

	#endregion

	#region -- class NativeMethods ----------------------------------------------------

	internal static class NativeMethods
	{
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


		private const string user32 = "user32.dll";

		[DllImport(user32)]
		public static extern IntPtr GetMessageExtraInfo();

		[DllImport(user32, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

		[DllImport(user32, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern ushort VkKeyScan(char ch);

		[DllImport(user32, SetLastError = true)]
		public static extern ushort GetKeyState(int nVirtKey);
	} // class NativeMethods

	#endregion
}
