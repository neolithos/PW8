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
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Neo.IronLua;

namespace Neo.PerfectWorking.Data
{
	internal class PwLuaPackage :  LuaTable, IPwPackage
	{
		private readonly PwGlobal global;
		private readonly string name;

		private Dictionary<string, IPwObject> objectRegistration = new Dictionary<string, IPwObject>();

		public PwLuaPackage(PwGlobal global, string name)
		{
			this.global = global ?? throw new ArgumentNullException(nameof(global));
			this.name = name ?? throw new ArgumentNullException(nameof(name));
		} // ctor

		protected override object OnIndex(object key)
			=> base.OnIndex(key) ?? global[key];
		
		protected override void OnPropertyChanged(string propertyName)
		{
			var value = GetMemberValue(propertyName, rawGet: true);
			if (value == null)
			{
				if (objectRegistration.TryGetValue(propertyName, out var obj))
				{
					obj.Dispose();
					objectRegistration.Remove(propertyName);
				}
			}
			else if (TypeInfo.GetTypeCode(value.GetType()) == TypeCode.Object)
			{
				if (objectRegistration.TryGetValue(propertyName, out var obj))
				{
					obj.Value = value; // replace value
				}
				else // register object
				{
					obj = global.RegisterObject(this, propertyName, value);
					if (obj != null)
						objectRegistration[propertyName] = obj;
				}
			}

			base.OnPropertyChanged(propertyName);
		} // proc OnPropertyChanged

		[LuaMember]
		public IPwObject RegisterObject(string name, object value)
			=> global.RegisterObject(this, name, value);
		
		string IPwPackage.Name => name;
	} // class PwLuaPackage 
}
