using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.PerfectWorking.Data;
using Neo.IronLua;

namespace Neo.PerfectWorking.Calc
{
	public sealed class CalcPackage : PwPackageBase
	{
		private readonly FormularFunctions functions;
		
		public CalcPackage(IPwGlobal global) 
			: base(global, nameof(CalcPackage))
		{
			this.functions = new FormularFunctions();

			global.RegisterObject(this, nameof(CalcWindowPane), new CalcWindowPane(global));
		} // ctor

		public LuaTable Functions
		{
			get => functions;
			set => functions.ImportMember(value);
		} // func Functions
	} // class CalcPackage
}
