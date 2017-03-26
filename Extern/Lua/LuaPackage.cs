using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.PerfectWorking.Data;

namespace Neo.PerfectWorking.Lua
{
	public sealed class LuaPackage : PwPackageBase
	{
		public LuaPackage(IPwGlobal global) 
			: base(global, nameof(LuaPackage))
		{
			global.RegisterObject(this, nameof(LuaWindowPane), new LuaWindowPane());
		} // ctor
	} // class LuaPackage
}
