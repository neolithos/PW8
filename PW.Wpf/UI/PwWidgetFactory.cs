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

	/// <summary>Implemented by the dash window.</summary>
	public interface IPwWidgetWindow
	{
		/// <summary>Basic background color</summary>
		Color BackgroundColor { get; }
		/// <summary>Basic background color</summary>
		Brush BackgroundBrush { get; }
		/// <summary>Text Color</summary>
		Color ForegroundColor { get; }
		/// <summary>Text Color</summary>
		Brush ForegroundBrush { get; }
		/// <summary>Main border color</summary>
		Color BorderColor { get; }
		/// <summary>Main border color</summary>
		Brush BorderBrush { get; }

		/// <summary>Assigned global environment</summary>
		IPwGlobal Global { get; }
	} // interface IPwWidgetWindow

	#endregion

	#region -- interface IPwWidgetFactory ---------------------------------------------

	public interface IPwWidgetFactory
	{
		UIElement Create(FrameworkElement parent, IPwWidgetWindow window, LuaTable t);

		string Name { get; }
	} // interface IPwWidgetFactory

	#endregion

	#region -- interface IPwWidgetFactoryOrder ----------------------------------------

	public interface IPwWidgetFactoryOrder
	{
		UIElement Create(FrameworkElement parent);

		LuaTable Arguments { get; }
	} // interface IPwWidgetFactoryOrder

	#endregion

	#region -- class PwWidgetFactory --------------------------------------------------

	public class PwWidgetFactory<T> : IPwWidgetFactory
		where T : UIElement
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
			var pi = typeof(T).GetMember(memberName, BindingFlags.Public | BindingFlags.Instance).OfType<PropertyInfo>().FirstOrDefault();
			if (pi != null)
				pi.SetValue(control, UIHelper.ConvertValue(pi.PropertyType, value, pi.GetValue(control)));
		} // proc SetMember

		protected virtual void SetIndex(FrameworkElement parent, T control, int index, object value)
		{
		} // proc SetIndex

		protected virtual T Create(FrameworkElement parent, IPwWidgetWindow window, LuaTable t)
		{
			var c = CreateControl(window);

			// set color style
			if (c is Control control)
			{
				control.Foreground = window.ForegroundBrush;
				control.BorderBrush = window.BorderBrush;
			}

			// set member
			foreach (var kv in t.Members)
				SetMember(c, kv.Key, kv.Value);

			// set indices
			var idx = 0;
			foreach (var v in t.ArrayList)
				SetIndex(parent, c, ++idx, v);

			return c;
		} // func Create

		UIElement IPwWidgetFactory.Create(FrameworkElement parent, IPwWidgetWindow window, LuaTable t)
			=> Create(parent, window, t);

		public string Name { get; }
	} // class PwWidgetFactory

	#endregion
}
