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

namespace Neo.PerfectWorking.UI
{
	internal sealed class BorderWidget : Border
	{
		#region -- class BorderFactory ------------------------------------------------

		private sealed class BorderFactory : PwWidgetFactory<BorderWidget>
		{
			protected override void SetIndex(FrameworkElement parent, BorderWidget control, int index, object value)
			{
				if (index == 1 && value is IPwWidgetFactoryOrder order)
					control.Child = order.Create(parent);
				else
					base.SetIndex(parent, control, index, value);
			} // proc SetIndex
		} // class BorderFactory

		#endregion

		public BorderWidget(IPwWidgetWindow window)
		{
			if (window == null)
				throw new ArgumentNullException(nameof(window));

			BorderBrush = window.BorderBrush;
		} // ctor

		public static IPwWidgetFactory Factory { get; } = new BorderFactory();
	} // class BorderWidget
}
