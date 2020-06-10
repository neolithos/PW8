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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Neo.IronLua;
using Neo.PerfectWorking.Data;
using TecWare.DE.Stuff;

namespace Neo.PerfectWorking.Stuff
{
	public static partial class ProcsPW
	{
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

		public static void OnException(this Task task, IPwShellUI ui)
			=> Procs.Silent(task, e => ui.ShowExceptionAsync(e));

		public static T SafeCall<T>(Func<T> f, T @default = default)
		{
			try
			{
				return f();
			}
			catch
			{
				return @default;
			}
		} // func SafeCall

		public static bool IsWin7
			=> Environment.OSVersion.Version > new Version(6, 0, 0, 0);

		public static bool IsWin10
			=> Environment.OSVersion.Version >= new Version(10, 0, 0, 0);
	} // class Procs
}
