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
using System.Globalization;

namespace Neo.PerfectWorking.Calc
{
	#region -- class CalcWindowPane -----------------------------------------------------

	/// <summary>
	/// Interaction logic for CalcWindowPane.xaml
	/// </summary>
	public partial class CalcWindowPane : PwWindowPane
	{
		public static readonly RoutedCommand ExecuteFormularCommand = new RoutedCommand("Execute", typeof(CalcWindowPane));

		public static readonly DependencyProperty CurrentFormularTextProperty = DependencyProperty.Register(nameof(CurrentFormularText), typeof(string), typeof(CalcWindowPane));
		private static readonly DependencyPropertyKey currentAnsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentAns), typeof(object), typeof(CalcWindowPane), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty CurrentAnsProperty = currentAnsPropertyKey.DependencyProperty;

		private readonly CalcPackage package;
		private readonly FormularEnvironment currentEnvironment;

		public CalcWindowPane(CalcPackage package)
		{
			this.package = package;

			InitializeComponent();

			this.currentEnvironment = package.CreateNewEnvironment();

			SetValue(currentAnsPropertyKey, 1023);

			CommandBindings.Add(
				new CommandBinding(ExecuteFormularCommand,
					(sender, e) =>
					{
						ProcessFormular();
						e.Handled = true;
					},
					(sender, e) =>
					{
						e.CanExecute = true;
						e.Handled = true;
					}
				)
			);
		} // ctor

		private void ProcessFormular()
		{
			try
			{
				var formular = new Formular(currentEnvironment, CurrentFormularText);
				SetValue(currentAnsPropertyKey, formular.GetResult());
			}
			catch (Exception e)
			{
				package.Global.UI.ShowException(e);
			}
		} // proc ProcessFormular

		public string CurrentFormularText { get => (string)GetValue(CurrentFormularTextProperty); set => SetValue(CurrentFormularTextProperty, value); }
		public object CurrentAns { get => GetValue(CurrentAnsProperty); }
	} // class CalcWindowPane

	#endregion

	#region -- class AnsConverter -------------------------------------------------------

	public sealed class AnsConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return String.Empty;
			else
			{
				if (targetType != typeof(string))
					throw new ArgumentOutOfRangeException(nameof(targetType), targetType, "Inalid target type.");

				switch(Base)
				{
					case 2:
						return "0b";
					case 8:
						return "0o";
					case 10:
						return value.ToString();
					case 16:
						return "0x";
					default:
						return "<error>";
				}
			}
		} // func Convert

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();

		public int Base { get; set; } = 10;
	} // class AnsConverter

	#endregion
}
