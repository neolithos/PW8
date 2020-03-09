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
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Neo.IronLua;
using Neo.PerfectWorking.Data;

namespace Neo.PerfectWorking.UI
{
	#region -- interface IWidgetFactory -----------------------------------------------

	public interface IWidgetFactory
	{
		FrameworkElement Create(IPwGlobal global, LuaTable t);

		string Name { get; }
	} // interface IWidgetFactory

	#endregion

	#region -- class WidgetFactory ----------------------------------------------------

	public class WidgetFactory<T> : IWidgetFactory
		where T : FrameworkElement
	{
		public WidgetFactory()
			: this(FormatName(typeof(T).Name))
		{
		} // ctor

		public WidgetFactory(string name)
			=> Name = name ?? throw new ArgumentNullException(nameof(name));

		private static string FormatName(string name)
			=> name.EndsWith("Widget") ? name.Substring(0, name.Length - 6) : name;

		protected virtual T CreateControl(IPwGlobal global)
			=> (T)Activator.CreateInstance(typeof(T), global);

		protected virtual void SetMember(T control, string memberName, object value)
		{
			switch (memberName)
			{
				case "Row":
					Grid.SetRow(control, Convert.ToInt32(value) - 1);
					break;
				case "Column":
					Grid.SetColumn(control, Convert.ToInt32(value) - 1);
					break;
				default:
					var pi = typeof(T).GetMember(memberName, BindingFlags.Public | BindingFlags.Instance).OfType<PropertyInfo>().FirstOrDefault();
					if (pi != null)
						pi.SetValue(control, value);
					break;
			}
		} // proc SetMember

		protected virtual void SetIndex(T control, int index, object value)
		{
		} // proc SetIndex

		protected virtual T Create(IPwGlobal global, LuaTable t)
		{
			var c = CreateControl(global);
			
			foreach (var kv in t)
			{
				if (kv.Key is int idx)
					SetIndex(c, idx, kv.Value);
				else if (kv.Key is string m)
					SetMember(c, m, kv.Value);
			}

			return c;
		} // func Create

		FrameworkElement IWidgetFactory.Create(IPwGlobal global, LuaTable t)
			=> Create(global, t);

		public string Name { get; }
	} // class WidgetFactory

	#endregion
}
