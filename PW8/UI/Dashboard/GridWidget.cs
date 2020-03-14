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
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Neo.IronLua;

namespace Neo.PerfectWorking.UI.Dashboard
{
	internal sealed class GridWidget : Grid
	{
		#region -- class GridWidgetFactory --------------------------------------------

		private sealed class GridWidgetFactory : PwWidgetFactory<GridWidget>
		{
			private static TypeConverter gridLengthConvert = new GridLengthConverter();

			private static GridLength ConvertLength(string value)
				=> (GridLength)gridLengthConvert.ConvertFrom(null, CultureInfo.InvariantCulture, value);

			private static ColumnDefinition ConvertColumn(object column)
			{
				switch (column)
				{
					case string t:
						return new ColumnDefinition { Width = ConvertLength(t) };
					default:
						throw new ArgumentException();
				}
			} // func ConvertColumn

			private static RowDefinition ConvertRow(object column)
			{
				switch (column)
				{
					case string t:
						return new RowDefinition { Height = ConvertLength(t) };
					default:
						throw new ArgumentException();
				}
			} // func ConvertColumn

			protected override void SetMember(GridWidget control, string memberName, object value)
			{
				// read columns/rows
				switch (memberName)
				{
					case "Columns":
						if (value is LuaTable columns)
						{
							foreach (var c in columns.ArrayList)
								control.ColumnDefinitions.Add(ConvertColumn(c));
						}
						else if (value is int columnCount)
						{
							while (columnCount-- > 0)
								control.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
						}
						break;
					case "Rows":
						if (value is LuaTable rows)
						{
							foreach (var c in rows.ArrayList)
								control.RowDefinitions.Add(ConvertRow(c));
						}
						else if (value is int rowCount)
						{
							while (rowCount-- > 0)
								control.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
						}
						break;
					default:
						base.SetMember(control, memberName, value);
						break;
				}
			} // proc SetMember

			protected override void SetIndex(FrameworkElement parent, GridWidget control, int index, object value)
			{
				if (value is IPwWidgetFactoryOrder order)
				{
					var c = order.Create(control);

					if (order.Arguments["Row"] is int row)
						SetRow(c, row - 1);
					else if (order.Arguments["Column"] is int column)
						SetColumn(c, column - 1);

					control.Children.Add(c);
				}
				else
					base.SetIndex(parent, control, index, value);
			} // proc SetIndex
		} // class StackWidgetFactory

		#endregion

		public GridWidget(IPwWidgetWindow window)
		{
			if (window == null)
				throw new ArgumentNullException(nameof(window));
		} // ctor

		public static IPwWidgetFactory Factory { get; } = new GridWidgetFactory();
	} // class GridWidget
}
