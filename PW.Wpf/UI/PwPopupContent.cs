using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Neo.PerfectWorking.UI
{
	public class PwPopupContent : ContentControl
	{
		static PwPopupContent()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PwPopupContent), new FrameworkPropertyMetadata(typeof(PwPopupContent)));
		}
	} // class PwPopupContent
}
