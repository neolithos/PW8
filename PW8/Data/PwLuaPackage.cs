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
