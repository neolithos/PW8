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
using System.Globalization;
using System.Net;
using Neo.IronLua;

namespace Neo.PerfectWorking.Stuff
{
	public static class Procs
	{
		public static bool EqualBytes(byte[] v1, byte[] v2)
		{
			if (Object.ReferenceEquals(v1, v2))
				return true;
			else if (v1 == null || v2 == null || v1.Length != v2.Length)
				return false;
			else
			{
				var l = v1.Length;
				for (var i = 0; i < l; i++)
				{
					if (v1[i] != v2[i])
						return false;
				}
				return true;
			}
		} // func EqualBytes

		public static object ChangeType(object value, Type typeTo)
			=> Lua.RtConvertValue(value, typeTo);

		public static T ChangeType<T>(this object value)
			=> (T)ChangeType(value, typeof(T));

		public static bool TryFromHexString(this string dataString, out byte[] data)
			=> TryFromHexString(dataString, 0, dataString.Length, out data);

		public static bool TryFromHexString(this string dataString, int offset, int length, out byte[] data)
		{
			if ((length & 2) == 1)
				throw new FormatException();

			data = new byte[length >> 1];
			for (var i = 0; i < data.Length; i++)
			{
				if (Byte.TryParse(dataString.Substring(i << 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var b))
					data[i] = b;
				else
				{
					data = null;
					return false;
				}
			}

			return true;
		} // func FromHexString

		public static string ToHexString(this byte[] bytes)
			=> ToHexString(bytes, 0, bytes.Length);

		public static string ToHexString(this byte[] bytes, int offset, int length)
		{
			if (bytes == null)
				return null;

			var c = new string[length];
			var e = offset + length;
			for (var i = offset; i < e; i++)
				c[i] = bytes[i].ToString("X2");

			return String.Concat(c);
		} // func ToHexString

		public static string GetUserName(this NetworkCredential cred)
			=> String.IsNullOrEmpty(cred.Domain)
				? cred.UserName
				: cred.Domain + "\\" + cred.UserName;

		public static LuaTable GetLuaTable(this LuaTable table, string propertyName)
		{
			if (table[propertyName] is LuaTable t)
				return t;

			t = new LuaTable();
			table[propertyName] = t;
			return t;
		} // func GetLuaTable

		public static bool IsWin7
			=> Environment.OSVersion.Version > new Version(6, 0, 0, 0);

		public static bool IsWin10
			=> Environment.OSVersion.Version >= new Version(10, 0, 0, 0);
	} // class Procs
}
