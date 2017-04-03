using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.IronLua;
using System.Net;

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

		public static string ToHexString(this byte[] bytes)
		{
			if (bytes == null)
				return null;

			var c = new string[bytes.Length];
			for (var i = 0; i < bytes.Length; i++)
				c[i] = bytes[i].ToString("X2");

			return String.Concat(c);
		} // func ToHexString

		public static string GetUserName(this NetworkCredential cred)
			=> String.IsNullOrEmpty(cred.Domain)
				? cred.UserName
				: cred.Domain + "\\" + cred.UserName;

		public static bool IsWin7
			=> Environment.OSVersion.Version > new Version(6,0,0,0);

		public static bool IsWin10
			=> Environment.OSVersion.Version >= new Version(10, 0, 0, 0);
	} // class Procs
}
