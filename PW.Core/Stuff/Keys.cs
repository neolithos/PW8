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
using System.ComponentModel.Design.Serialization;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using static Neo.PerfectWorking.NativeMethods;

namespace Neo.PerfectWorking.Stuff
{
	#region -- enum PwKeyModifiers ----------------------------------------------------

	/// <summary></summary>
	[Flags]
	public enum PwKeyModifiers : int
	{
		/// <summary></summary>
		None = 0,
		/// <summary></summary>
		Alt = 1,
		/// <summary></summary>
		Control = 2,
		/// <summary></summary>
		Shift = 4,
		/// <summary></summary>
		Win = 8,
	} // enum PwKeyModifiers

	#endregion

	#region -- enum PwKeySend ---------------------------------------------------------

	[Flags]
	public enum PwKeySend
	{
		None = 0,
		Up = 1,
		Down = 2,
		Both = 3
	} // enum PwKeySend

	#endregion

	#region -- struct PwKey -----------------------------------------------------------

	/// <summary></summary>
	[TypeConverter(typeof(PwKeyConverter))]
	public struct PwKey : IEquatable<PwKey>
	{
		private int rawValue;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="value"></param>
		public PwKey(int value)
			=> rawValue = ValidateKey(value);

		/// <summary></summary>
		/// <param name="modifiers"></param>
		/// <param name="virtualKey"></param>
		public PwKey(PwKeyModifiers modifiers, int virtualKey)
			=> rawValue = ValidateKey(EncodeKeyModifiers((int)modifiers) | virtualKey);

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
			=> obj is PwKey k ? rawValue == k.rawValue : base.Equals(obj);

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Equals(PwKey other)
			=> rawValue == other.rawValue;

		/// <summary></summary>
		/// <returns></returns>
		public override int GetHashCode()
			=> rawValue.GetHashCode();

		#endregion

		#region -- ToString -----------------------------------------------------------

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> ToString(CultureInfo.CurrentCulture);

		public string ToString(CultureInfo culture)
		{
			if (rawValue == 0)
				return "None";
			else
			{
				var keyParts = new string[] { null, null, null, null, null };
				var mods = Modifiers;

				// create modifiers
				if ((mods & PwKeyModifiers.Control) != 0)
					keyParts[0] = GetModifierName(1, culture);
				if ((mods & PwKeyModifiers.Win) != 0)
					keyParts[1] = GetModifierName(3, culture);
				if ((mods & PwKeyModifiers.Alt) != 0)
					keyParts[2] = GetModifierName(0, culture);
				if ((mods & PwKeyModifiers.Shift) != 0)
					keyParts[3] = GetModifierName(2, culture);

				// build key code
				keyParts[4] = GetKeyNameFromVirtualKeyCode(VirtualKey);

				return String.Join("+", keyParts.Where(c => c != null));
			}
		} // func ToString

		#endregion

		/// <summary>Send a single key to the focused application.</summary>
		/// <param name="flag"></param>
		public void SendKey(PwKeySend flag)
		{
			if (flag == PwKeySend.None)
				return;

			new PwKeyList().Append(this, flag).Send();
		} // proc SendKey

		/// <summary>Raw key value.</summary>
		public int Value { get => rawValue; set => rawValue = ValidateKey(value); }

		/// <summary>Key modifiers</summary>
		public PwKeyModifiers Modifiers { get => (PwKeyModifiers)DecodeKeyModifiers(rawValue); set => Value = EncodeKeyModifiers((int)value) | GetKeyCode(rawValue); }
		/// <summary>Get virtual key code</summary>
		public ushort VirtualKey { get => GetKeyCode(rawValue); set => Value = MaskModifiers(rawValue) | GetKeyCode(value); }

		/// <summary>Is this key valid.</summary>
		public bool IsValid => rawValue > 0;

		// -- Static ----------------------------------------------------------

		private const int noRepeatModifier = 0x400;

		#region -- keyNams, modifierNames ---------------------------------------------

		#region -- keyNames --

		private static readonly string[] keyNames =
		{
			null, // 0
			"RButton", // 1 mouse right
			"LButton", // 2 mouse left
			"Cancel", // 3
			"MButton", // 4
			"XButton1", // 5
			"XButton2", // 6
			null,
			"Back", //  8
			"Tab", //  9
			null,
			null,
			"Clear", //  12
			"Return", //  13
			null,
			null,
			null,
			null,
			null,
			"Pause", //  19
			"Capital", //  20
			"KanaMode", //  21
			null,
			"JunjaMode", //  23
			"FinalMode", //  24
			"HanjaMode", //  25
			null,
			"Escape", //  27
			"ImeConvert", //  28
			"ImeNonConvert", //  29
			"ImeAccept", //  30
			"ImeModeChange", //  31
			"Space", //  32
			"Prior", //  33
			"Next", //  34
			"End", //  35
			"Home", //  36
			"Left", // 37
			"Up", // 38
			"Right", // 39
			"Down", // 40
			"Select", // 41
			"Print", // 42
			"Execute", // 43
			"Snapshot", // 44
			"Insert", // 45
			"Delete", // 46
			"Help", // 47
			"D0", // 48
			"D1", // 49
			"D2", // 50
			"D3", // 51
			"D4", // 52
			"D5", // 53
			"D6", // 54
			"D7", // 55
			"D8", // 56
			"D9", // 57
			null,
			null,
			null, // 60
			null,
			null,
			null,
			null,
			"A", // 65
			"B", // 66
			"C", // 67
			"D", // 68
			"E", // 69
			"F", // 70
			"G", // 71
			"H", // 72
			"I", // 73
			"J", // 74
			"K", // 75
			"L", // 76
			"M", // 77
			"N", // 78
			"O", // 79
			"P", // 80
			"Q", // 81
			"R", // 82
			"S", // 83
			"T", // 84
			"U", // 85
			"V", // 86
			"W", // 87
			"X", // 88
			"Y", // 89
			"Z", // 90
			"LWin", // 91
			"RWin", // 92
			"Apps", // 93
			null,
			"Sleep", // 95
			"NumPad0", // 96
			"NumPad1", // 97
			"NumPad2", // 98
			"NumPad3", // 99
			"NumPad4", // 100
			"NumPad5", // 101
			"NumPad6", // 102
			"NumPad7", // 103
			"NumPad8", // 104
			"NumPad9", // 105
			"Multiply", // 106
			"Add", // 107
			"Separator", // 108
			"Subtract", // 109
			"Decimal", // 110
			"Divide", // 111
			"F1", // 112
			"F2", // 113
			"F3", // 114
			"F4", // 115
			"F5", // 116
			"F6", // 117
			"F7", // 118
			"F8", // 119
			"F9", // 120
			"F10", // 121
			"F11", // 122
			"F12", // 123
			"F13", // 124
			"F14", // 125
			"F15", // 126
			"F16", // 127
			"F17", // 128
			"F18", // 129
			"F19", // 130
			"F20", // 131
			"F21", // 132
			"F22", // 133
			"F23", // 134
			"F24", // 135
			null,
			null,
			null,
			null,
			null, // 140
			null,
			null,
			null,
			"NumLock", // 144
			"Scroll", // 145
			null,
			null,
			null,
			null,
			null, // 150
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			"LeftShift", // 160
			"RightShift", // 161
			"LeftCtrl", // 162
			"RightCtrl", // 163
			"LeftAlt", // 164
			"RightAlt", // 165
			"BrowserBack", // 166
			"BrowserForward", // 167
			"BrowserRefresh", // 168
			"BrowserStop", // 169
			"BrowserSearch", // 170
			"BrowserFavorites", // 171
			"BrowserHome", // 172
			"VolumeMute", // 173
			"VolumeDown", // 174
			"VolumeUp", // 175
			"MediaNextTrack", // 176
			"MediaPreviousTrack", // 177
			"MediaStop", // 178
			"MediaPlayPause", // 179
			"LaunchMail", // 180
			"SelectMedia", // 181
			"LaunchApplication1", // 182
			"LaunchApplication2", // 183
			null,
			null,
			"Oem1", // 186
			"OemPlus", // 187
			"OemComma", // 188
			"OemMinus", // 189
			"OemPeriod", // 190
			"Oem2", // 191
			"Oem3", // 192
			"AbntC1", // 193
			"AbntC2", // 194
			null,
			null,
			null,
			null,
			null,
			null, // 200
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null, // 210
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			"Oem4", // 219
			"Oem5", // 220
			"Oem6", // 221
			"Oem7", // 222
			"Oem8", // 223
			null,
			null,
			"Oem102", // 226
			null,
			null,
			"ImeProcessed", // 229
			null, // 230
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			"OemAttn", // 240
			"OemFinish", // 241
			"OemCopy", // 242
			"OemAuto", // 243
			"OemEnlw", // 244
			"OemBackTab", // 245
			"Attn", // 246
			"CrSel", // 247
			"ExSel", // 248
			"EraseEof", // 249
			"Play", // 250
			"Zoom", // 251
			"NoName", // 252
			"Pa1", // 253
			"OemClear", // 254
		}; // keyNames

		#endregion

		#region -- modifierNames --

		private static readonly string[] modifierNames = { "Alt", "Ctrl", "Shift", "Win" };
		private static readonly string[] modifierNamesDE = { "Alt", "Strg", "Umschalt", "Win" };

		#endregion

		private static string GetModifierName(int idx, CultureInfo culture)
		{
			var mn = GetModifierNames(culture);
			return idx >= 0 && idx < mn.Length ? mn[idx] : null;
		} // func GetModifierName

		private static int FindModifierIndex(string modifierName, CultureInfo culture, bool ignoreCase = true)
		{
			var mn = GetModifierNames(culture);
			return ignoreCase
				? Array.FindIndex(modifierNames, c => String.Compare(c, modifierName, StringComparison.OrdinalIgnoreCase) == 0)
				: Array.IndexOf(modifierNames, modifierName);
		} // func FindModifierIndex

		/// <summary></summary>
		/// <param name="culture"></param>
		/// <returns></returns>
		public static string[] GetModifierNames(CultureInfo culture)
			=> culture.TwoLetterISOLanguageName == "de" ? modifierNamesDE : modifierNames;

		/// <summary></summary>
		/// <param name="keyName"></param>
		/// <param name="ignoreCase"></param>
		/// <returns></returns>
		public static int GetVirtualKeyFromString(string keyName, bool ignoreCase = true)
			=> ignoreCase
				? Array.FindIndex(keyNames, c => String.Compare(c, keyName, StringComparison.OrdinalIgnoreCase) == 0)
				: Array.IndexOf(keyNames, keyName);

		/// <summary></summary>
		/// <param name="virtualKeyCode"></param>
		/// <returns></returns>
		public static string GetKeyNameFromVirtualKeyCode(int virtualKeyCode)
			=> virtualKeyCode >= 0 && virtualKeyCode < keyNames.Length ? keyNames[virtualKeyCode] : null;

		#endregion

		#region -- Primitives ---------------------------------------------------------

		private static int ValidateKey(int newValue)
		{
			if (newValue <= 0)
				return 0;

			// check key code
			var keyCode = GetKeyCode(newValue);
			if (GetKeyNameFromVirtualKeyCode(keyCode) == null)
				return 0;

			// check key modifiers
			var keyModifiers = DecodeKeyModifiers(newValue);
			if ((keyModifiers & noRepeatModifier) != 0) // may be repeat setted
				keyModifiers &= ~noRepeatModifier;
			if (keyModifiers > 15)
				return 0;

			return newValue;
		} // func ValidateKey

		private static ushort GetKeyCode(int key)
			=> (ushort)(key & 0xFFFF);

		private static int DecodeKeyModifiers(int rawValue)
			=> rawValue >> 16;

		private static int EncodeKeyModifiers(int modifiers)
			=> modifiers << 16;

		private static int MaskModifiers(int rawValue)
			=> rawValue & 0xF0000;

		#endregion

		#region -- Parse, TryParse ----------------------------------------------------

		/// <summary></summary>
		/// <param name="keyString"></param>
		/// <returns></returns>
		public static PwKey Parse(string keyString)
			=> Parse(keyString, CultureInfo.CurrentCulture);

		/// <summary></summary>
		/// <param name="keyString"></param>
		/// <param name="culture"></param>
		/// <returns></returns>
		public static PwKey Parse(string keyString, CultureInfo culture)
			=> TryParse(keyString, culture, out var k) ? k : throw new FormatException();

		/// <summary></summary>
		/// <param name="keyString"></param>
		/// <param name="key"></param>
		/// <returns></returns>
		public static bool TryParse(string keyString, out PwKey key)
			=> TryParse(keyString, CultureInfo.CurrentCulture, out key);

		/// <summary></summary>
		/// <param name="keyString"></param>
		/// <param name="culture"></param>
		/// <param name="key"></param>
		/// <returns></returns>
		public static bool TryParse(string keyString, CultureInfo culture, out PwKey key)
		{
			// test key string
			if (String.IsNullOrWhiteSpace(keyString)
				|| String.Compare(keyString, "None", StringComparison.OrdinalIgnoreCase) == 0)
			{
				key = None;
				return true;
			}

			// split parts
			var keyParts = keyString.Split(new char[] { '+', ',' }, StringSplitOptions.RemoveEmptyEntries);
			if (keyParts.Length == 0)
				goto returnError;

			// read key code
			var virtualKey = GetVirtualKeyFromString(keyParts[keyParts.Length - 1], ignoreCase: true);
			if (virtualKey == -1)
				goto returnError;

			// read modifiers
			var modifiers = PwKeyModifiers.None;
			for (var i = 0; i < keyParts.Length - 1; i++)
			{
				var idx = FindModifierIndex(keyParts[i].Trim(), culture, ignoreCase: true);
				if (idx == -1)
					goto returnError;

				modifiers |= (PwKeyModifiers)(1 << idx);
			}

			key = new PwKey(modifiers, virtualKey);
			return true;

			returnError:
			key = None;
			return false;
		} // func TryParse

		#endregion

		/// <summary>Invalid key, a none key.</summary>
		public static PwKey None { get; } = new PwKey(0);

		public static bool operator ==(PwKey a, PwKey b)
			=> a.Equals(b);
		public static bool operator !=(PwKey a, PwKey b)
			=> !a.Equals(b);
	} // struct PwKey

	#endregion

	#region -- class PwKeyConverter ---------------------------------------------------

	public sealed class PwKeyConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
			=> sourceType == typeof(string)
				|| sourceType == typeof(int)
				|| sourceType == typeof(PwKey);

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			switch (value)
			{
				case null:
					return PwKey.None;
				case string keyString:
					return PwKey.Parse(keyString, culture);
				case int keyCode:
					return new PwKey(keyCode);
				case PwKey key:
					return value;
				default:
					return base.ConvertFrom(context, culture, value);
			}
		} // func ConvertFrom

		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
			=> destinationType == typeof(string)
				|| destinationType == typeof(int)
				|| destinationType == typeof(PwKey)
				|| destinationType == typeof(InstanceDescriptor);

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			var key = (PwKey)value;
			if (destinationType == typeof(PwKey))
				return value;
			else if (destinationType == typeof(string))
				return key.ToString(culture);
			else if (destinationType == typeof(int))
				return key.Value;
			else if (destinationType == typeof(InstanceDescriptor))
			{
				var ci = typeof(PwKey).GetConstructor(new Type[] { typeof(int) });
				return new InstanceDescriptor(ci, new object[] { key.Value }, true);
			}
			else
				return base.ConvertTo(context, culture, value, destinationType);
		} // func ConvertTo
	} // class PwKeyConverter

	#endregion

	#region -- class PwKeyList --------------------------------------------------------

	public class PwKeyList
	{
		private INPUT[] inputs = new INPUT[8];
		private int count = 0;

		private PwKeyModifiers state = PwKeyModifiers.None;
		private PwKeyModifiers firstState = PwKeyModifiers.None;

		public PwKeyList()
		{
		} // ctor

		private void Next()
		{
			if (++count >= inputs.Length)
			{
				var n = new INPUT[inputs.Length * 2];
				Array.Copy(inputs, 0, n, 0, count);
				inputs = n;
			}
		} // proc Next

		private void AppendKeyInput(bool keyUp, ushort virtualKey, bool isExtended = false)
		{
			ref var cur = ref inputs[count];

			var flags = keyUp ? KEYEVENTF_KEYUP : 0;
			if (isExtended
				|| (virtualKey >= 33 && virtualKey <= 46) // PageUp/Next to Delete
				|| (virtualKey >= 91 && virtualKey <= 93)) // LWin to Apps
				flags |= KEYEVENTF_EXTENDEDKEY;

			cur.type = INPUT_KEYBOARD;
			cur.ki.wVk = virtualKey;
			cur.ki.wScan = 0;
			cur.ki.dwFlags = flags;
			cur.ki.time = 0;
			cur.ki.dwExtraInfo = GetMessageExtraInfo();

			Next();
		} // proc AppendKeyInput

		private void CreateMouseInput(int x, int y, uint flags, uint data = 0)
		{
			ref var cur = ref inputs[count];

			cur.type = INPUT_MOUSE;
			cur.mi.dx = x;
			cur.mi.dy = y;
			cur.mi.mouseData = data;
			cur.mi.dwFlags = flags;
			cur.mi.time = 0;
			cur.mi.dwExtraInfo = GetMessageExtraInfo();

			Next();
		} // proc CreateMouseInput

		private void AppendModifiers(PwKeyModifiers newState, PwKeyModifiers currentState)
		{
			var diff = currentState ^ newState;

			void CheckState(PwKeyModifiers test, ushort virtualKey)
			{
				if ((diff & test) == test)
					AppendKeyInput((currentState & test) == test, virtualKey);
			}

			CheckState(PwKeyModifiers.Shift, 16);
			CheckState(PwKeyModifiers.Control, 17);
			CheckState(PwKeyModifiers.Alt, 18);
			CheckState(PwKeyModifiers.Win, 91);
		} // proc AppendModifiers

		private void SetModifiers(PwKeyModifiers newState)
		{
			if (count == 0)
				firstState = newState;
			else
				AppendModifiers(newState, state);
			state = newState;
		} // proc SetModifiers

		public PwKeyList Append(char c)
		{
			var scan = VkKeyScan(c);

			// update modifiers
			var modifiers = PwKeyModifiers.None;
			if ((scan & 0x100) == 0x100)
				modifiers |= PwKeyModifiers.Shift;
			if ((scan & 0x200) == 0x200)
				modifiers |= PwKeyModifiers.Control;
			if ((scan & 0x400) == 0x400)
				modifiers |= PwKeyModifiers.Alt;

			SetModifiers(modifiers);

			// set key/down
			var virtualKey = (ushort)(scan & 0xFF);
			AppendKeyInput(false, virtualKey);
			AppendKeyInput(true, virtualKey);

			return this;
		} // proc Append

		public PwKeyList Append(PwKey key, PwKeySend flag)
		{
			var virtualKey = key.VirtualKey;

			if ((flag & PwKeySend.Down) == PwKeySend.Down)
			{
				SetModifiers(key.Modifiers);
				switch (virtualKey)
				{
					case 1: // left
						CreateMouseInput(0, 0, MOUSEEVENTF_LEFTDOWN);
						break;
					case 2: // right
						CreateMouseInput(0, 0, MOUSEEVENTF_RIGHTDOWN);
						break;
					case 4: // middle
						CreateMouseInput(0, 0, MOUSEEVENTF_MIDDLEDOWN);
						break;
					case 5: // x1
						CreateMouseInput(0, 0, MOUSEEVENTF_XDOWN, 1);
						break;
					case 6: // x2
						CreateMouseInput(0, 0, MOUSEEVENTF_XDOWN, 2);
						break;
					default:
						AppendKeyInput(false, virtualKey);
						break;
				}
			}

			if ((flag & PwKeySend.Up) == PwKeySend.Up)
			{
				switch (virtualKey)
				{
					case 1: // left
						CreateMouseInput(0, 0, MOUSEEVENTF_LEFTUP);
						break;
					case 2: // right
						CreateMouseInput(0, 0, MOUSEEVENTF_RIGHTUP);
						break;
					case 4: // middle
						CreateMouseInput(0, 0, MOUSEEVENTF_MIDDLEUP);
						break;
					case 5: // x1
						CreateMouseInput(0, 0, MOUSEEVENTF_XUP, 1);
						break;
					case 6: // x2
						CreateMouseInput(0, 0, MOUSEEVENTF_XUP, 2);
						break;
					default:
						AppendKeyInput(true, virtualKey);
						break;
				}
			}

			return this;
		} // proc Append

		public void Send()
		{
			if (count > 0)
			{
				var systemState = PwKeyModifiers.None;
				var beforeList = new PwKeyList();

				if ((GetKeyState(20) & 1) != 0) // caps lock state is active
				{
					beforeList.AppendKeyInput(false, 20, true);  // caps lock
					beforeList.AppendKeyInput(true, 20, true);
					AppendKeyInput(false, 20, true); // caps lock
					AppendKeyInput(true, 20, true);
				}

				if ((GetKeyState(16) & 0x80) != 0)
					systemState |= PwKeyModifiers.Shift;

				if ((GetKeyState(17) & 0x80) != 0)
					systemState |= PwKeyModifiers.Control;
				if ((GetKeyState(18) & 0x80) != 0)
					systemState |= PwKeyModifiers.Alt;
				if ((GetKeyState(91) & 0x80) != 0)
					systemState |= PwKeyModifiers.Win;

				// toggle state to none
				if (systemState != PwKeyModifiers.None)
					beforeList.AppendModifiers(firstState, systemState);

				// clear modifiers to system state
				SetModifiers(systemState);

				if (beforeList.count > 0)
				{
					var n = new INPUT[beforeList.count + count];
					var nCount = beforeList.count + count;
					Array.Copy(beforeList.inputs, 0, n, 0, beforeList.count);
					Array.Copy(inputs, 0, n, beforeList.count, count);
					inputs = n;
					count = nCount;
				}

				var sz = Marshal.SizeOf<INPUT>();
				if (SendInput((uint)count, inputs, sz) == 0)
					throw new Win32Exception();
			}
		} // proc Send

		public bool IsEmpty => count == 0;
	} // class PwKeyList

	#endregion
}
