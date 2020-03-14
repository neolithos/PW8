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
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Neo.IronLua;
using Neo.PerfectWorking.Data;

namespace Neo.PerfectWorking.UI.Dashboard
{
	#region -- class GlobalWidgetFactory ----------------------------------------------

	internal sealed class GlobalWidgetFactory : DynamicObject
	{
		#region -- class WidgetFactoryOrder -------------------------------------------

		private sealed class WidgetFactoryOrder : IPwWidgetFactoryOrder
		{
			private readonly IPwWidgetFactory factory;
			private readonly IPwWidgetWindow window;
			private readonly LuaTable arguments;

			public WidgetFactoryOrder(IPwWidgetFactory factory, IPwWidgetWindow window, LuaTable arguments)
			{
				this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
				this.window = window ?? throw new ArgumentNullException(nameof(window));
				this.arguments = arguments ?? new LuaTable();
			} // ctor

			public UIElement Create(FrameworkElement parent)
				=> factory.Create(parent, window, arguments);

			public LuaTable Arguments => arguments;
		} // class WidgetFactoryOrder

		#endregion

		private readonly PwGlobal global;
		private readonly DashBoardWindow window;
		private readonly IPwCollection<IPwWidgetFactory> factories;

		public GlobalWidgetFactory(PwGlobal global, DashBoardWindow window)
		{
			this.global = global ?? throw new ArgumentNullException(nameof(global));
			this.window = window ?? throw new ArgumentNullException(nameof(window));

			 factories = global.RegisterCollection<IPwWidgetFactory>(global);
		} // ctor

		private IPwWidgetFactory FindFactory(string name)
			=> factories.First(c => c.Name == name);

		private object CreateDashboard(LuaTable t)
		{
			// check for basic properties for the window
			window.ForegroundColor = UIHelper.ConvertValue(t.GetMemberValue("Foreground"), SystemColors.WindowTextColor);
			window.BackgroundColor = UIHelper.ConvertValue(t.GetMemberValue("Background"), SystemColors.WindowColor);
			window.BorderColor = UIHelper.ConvertValue(t.GetMemberValue("Border"), SystemColors.ActiveBorderColor);

			window.Opacity = t.GetOptionalValue("Opacity", 1.0);
			window.BorderThickness = UIHelper.ConvertValue(t.GetMemberValue("BorderThickness"), new Thickness(0));

			var orders = t.ArrayList.OfType<IPwWidgetFactoryOrder>().ToArray();
			if (orders.Length == 0)
				return null;
			else if (orders.Length == 1)
				return window.SetDashboard(orders[0].Create(window));
			else
			{
				t["Foreground"] = null;
				t["Background"] = null;
				t["Border"] = null;
				t["Opacity"] = null;
				t["BorderThickness"] = null;

				if (t.Members.ContainsKey("Columns") || t.Members.ContainsKey("Rows")) // use a grid
					return window.SetDashboard(GridWidget.Factory.Create(window, window, t));
				else
					return window.SetDashboard(StackWidget.Factory.Create(window, window, t));
			}
		} // func CreateDashboard

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			var f = FindFactory(binder.Name);
			if (f == null)
			{
				result = true;
				return true;
			}
			else
			{
				result = new Func<LuaTable, WidgetFactoryOrder>(t => new WidgetFactoryOrder(f, window, t));
				return true;
			}
		} // func TryGetMember

		public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
		{
			if (args.Length == 1 && args[0] is LuaTable t)
			{
				var f = FindFactory(binder.Name);
				result = f == null ? null : new WidgetFactoryOrder(f, window, t);
				return true;
			}
			else
				return base.TryInvokeMember(binder, args, out result);
		} // func TryInvokeMember

		public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
		{
			if (args.Length == 1 && args[0] is LuaTable t)
			{
				result = CreateDashboard(t);
				return true;
			}
			else
				return base.TryInvoke(binder, args, out result);
		} // func TryInvoke
	} // class GlobalWidgetFactory

	#endregion
}
