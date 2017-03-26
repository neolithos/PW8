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
using Neo.PerfectWorking.UI;
using Neo.PerfectWorking.Data;

namespace Neo.PerfectWorking.Calc
{
	/// <summary>
	/// Interaction logic for CalcWindowPane.xaml
	/// </summary>
	public partial class CalcWindowPane : PwWindowPane
	{
		public CalcWindowPane(IPwGlobal glonal)
		{
			InitializeComponent();
		}
	}
}
