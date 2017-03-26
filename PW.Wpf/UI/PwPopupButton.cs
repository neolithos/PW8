using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Neo.PerfectWorking.UI
{
	public class PwPopupButton : Button
	{
		public static readonly DependencyProperty PopupProperty = DependencyProperty.Register(nameof(Popup), typeof(Popup), typeof(PwPopupButton));

		protected override void OnClick()
		{
			base.OnClick();

			if (Popup == null)
				return;

			if (Popup.IsOpen)
				Popup.IsOpen = false;
			else
			{
				Popup.PlacementTarget = this;
				Popup.IsOpen = true;
			}
		} // proc OnClick

		public Popup Popup { get { return (Popup)GetValue(PopupProperty); } set { SetValue(PopupProperty, value); } }
	} // class PwPopupButton
}
