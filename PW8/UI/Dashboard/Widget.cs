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
using Neo.IronLua;
using Neo.PerfectWorking.Data;

namespace Neo.PerfectWorking.UI.Dashboard
{
	#region -- class GlobalWidgetFactory ----------------------------------------------

	internal sealed class GlobalWidgetFactory : DynamicObject
	{
		private readonly PwGlobal global;
		private readonly DashBoardWindow window;
		private readonly IPwCollection<IWidgetFactory> factories;

		public GlobalWidgetFactory(PwGlobal global, DashBoardWindow window)
		{
			this.global = global ?? throw new ArgumentNullException(nameof(global));
			this.window = window ?? throw new ArgumentNullException(nameof(window));

			 factories = global.RegisterCollection<IWidgetFactory>(global);
		} // ctor

		private IWidgetFactory FindFactory(string name)
			=> factories.First(c => c.Name == name);

		private object CreateDashboard(LuaTable t)
		{
			var g = new Grid();
			var gridLengthConvert = new GridLengthConverter();

			GridLength ConvertLength(string value)
				=> (GridLength)gridLengthConvert.ConvertFrom(null, CultureInfo.InvariantCulture, value);

			ColumnDefinition ConvertColumn(object column)
			{
				switch(column)
				{
					case string t:
						return new ColumnDefinition { Width = ConvertLength(t) };
					default:
						throw new ArgumentException();
				}
			} // func ConvertColumn

			RowDefinition ConvertRow(object column)
			{
				switch (column)
				{
					case string t:
						return new RowDefinition { Height = ConvertLength(t) };
					default:
						throw new ArgumentException();
				}
			} // func ConvertColumn

			// read columns/rows
			if (t["Columns"] is LuaTable columns)
			{
				foreach (var c in columns.ArrayList)
					g.ColumnDefinitions.Add(ConvertColumn(c));
			}
			if (t["Rows"] is LuaTable rows)
			{
				foreach (var c in rows.ArrayList)
					g.RowDefinitions.Add(ConvertRow(c));
			}

			// read elements
			foreach (var v in t.ArrayList.OfType<UIElement>())
				g.Children.Add(v);

			return window.SetDashboard(g);
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
				result = new Func<LuaTable, FrameworkElement>(t => f.Create(global, t));
				return true;
			}
		} // func TryGetMember

		public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
		{
			if (args.Length == 1 && args[0] is LuaTable t)
			{
				var f = FindFactory(binder.Name);
				result = f?.Create(global, t);
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
