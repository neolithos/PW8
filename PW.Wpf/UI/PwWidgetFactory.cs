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
using System.Windows.Media;
using Neo.IronLua;
using Neo.PerfectWorking.Data;

namespace Neo.PerfectWorking.UI
{
	#region -- interface IPwWidgetWindow ----------------------------------------------

	public interface IPwWidgetWindow
	{
		Brush Foreground { get; }

		IPwGlobal Global { get; }
	} // interface IPwWidgetWindow

	#endregion

	#region -- interface IPwWidgetFactory ---------------------------------------------

	public interface IPwWidgetFactory
	{
		FrameworkElement Create(IPwWidgetWindow window, LuaTable t);

		string Name { get; }
	} // interface IPwWidgetFactory

	#endregion

	#region -- class PwWidgetFactory --------------------------------------------------

	public class PwWidgetFactory<T> : IPwWidgetFactory
		where T : FrameworkElement
	{
		public PwWidgetFactory()
			: this(FormatName(typeof(T).Name))
		{
		} // ctor

		public PwWidgetFactory(string name)
			=> Name = name ?? throw new ArgumentNullException(nameof(name));

		private static string FormatName(string name)
			=> name.EndsWith("Widget") ? name.Substring(0, name.Length - 6) : name;

		protected virtual T CreateControl(IPwWidgetWindow window)
			=> (T)Activator.CreateInstance(typeof(T), window);

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

		protected virtual T Create(IPwWidgetWindow window, LuaTable t)
		{
			var c = CreateControl(window);

			// update default properties
			if (c is Control control)
			{
				control.Foreground = window.Foreground;
				control.Background = Brushes.Transparent;
			}

			// set member
			foreach (var kv in t)
			{
				if (kv.Key is int idx)
					SetIndex(c, idx, kv.Value);
				else if (kv.Key is string m)
					SetMember(c, m, kv.Value);
			}

			return c;
		} // func Create

		FrameworkElement IPwWidgetFactory.Create(IPwWidgetWindow window, LuaTable t)
			=> Create(window, t);

		public string Name { get; }
	} // class PwWidgetFactory

	#endregion
}
