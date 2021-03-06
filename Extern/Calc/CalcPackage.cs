﻿#region -- copyright --
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
using Neo.IronLua;
using Neo.PerfectWorking.Data;

namespace Neo.PerfectWorking.Calc
{
	public sealed class CalcPackage : PwPackageBase
	{
		private readonly FormularFunctions functions;
		
		public CalcPackage(IPwGlobal global) 
			: base(global, nameof(CalcPackage))
		{
			this.functions = new FormularFunctions();

			global.RegisterObject(this, nameof(CalcWindowPane), new CalcWindowPane(this));
		} // ctor

		public FormularEnvironment CreateNewEnvironment()
			=> new FormularEnvironment(functions);

		public LuaTable Functions
		{
			get => functions;
			set => functions.ImportMember(value);
		} // func Functions
	} // class CalcPackage
}
