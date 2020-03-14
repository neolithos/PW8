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
using System.Windows;
using System.Windows.Controls;

namespace Neo.PerfectWorking.UI.Dashboard
{
	internal sealed class StackWidget : StackPanel
	{
		#region -- class StackWidgetFactory -------------------------------------------

		private sealed class StackWidgetFactory : PwWidgetFactory<StackWidget>
		{
			protected override void SetIndex(FrameworkElement parent, StackWidget control, int index, object value)
			{
				if (value is IPwWidgetFactoryOrder order)
					control.Children.Add(order.Create(parent));
				else
					base.SetIndex(parent, control, index, value);
			} // proc SetIndex
		} // class StackWidgetFactory

		#endregion

		public StackWidget(IPwWidgetWindow window)
		{
			if (window == null)
				throw new ArgumentNullException(nameof(window));
		} // ctor

		public static IPwWidgetFactory Factory { get; } = new StackWidgetFactory();
	} // class StackWidget
}
