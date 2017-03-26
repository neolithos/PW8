using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Neo.PerfectWorking.UI
{	
	public class ConsoleViewer : FrameworkElement
	{
		protected override Size MeasureOverride(Size availableSize) 
			=> availableSize;

		protected override void OnRender(DrawingContext drawingContext)
		{
			drawingContext.DrawLine(new Pen(Brushes.Black, 4), new Point(0, 0), new Point(ActualWidth, ActualHeight));
		}
	}
}
