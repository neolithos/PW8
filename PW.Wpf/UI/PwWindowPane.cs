using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Neo.PerfectWorking.Data;

namespace Neo.PerfectWorking.UI
{
	public class PwWindowPane : UserControl, IPwWindowPane
	{
		public static DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(PwWindowPane));
		public static DependencyProperty ImageProperty = DependencyProperty.Register(nameof(Image), typeof(ImageSource), typeof(PwWindowPane));

		public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
		public object Control => this;
		public object Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }
	} // class PwWindowPane
}
