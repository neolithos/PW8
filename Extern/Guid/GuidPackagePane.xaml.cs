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
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Neo.PerfectWorking.Data;
using Neo.PerfectWorking.UI;

namespace Neo.PerfectWorking
{
	#region -- class GuidPackagePane --------------------------------------------------

	public partial class GuidPackagePane : PwContentPane
	{
		private readonly GuidPackage package;
		private readonly IPwShellUI ui;

		public GuidPackagePane(GuidPackage package)
		{
			this.package = package;
			ui = package.Global.UI;

			InitializeComponent();

			CommandBindings.Add(new CommandBinding(ApplicationCommands.New,
				(sender, e) => 
				{
					package.CurrentGuid = Guid.NewGuid();
					e.Handled = true;
				}
			));

			DataContext = package;
		} // ctor

		private void ChangeFormatStyle(char newStyle)
		{
			package.CurrentFormat =
				Char.IsUpper(package.CurrentFormat)
					? Char.ToUpper(newStyle)
					: Char.ToLower(newStyle);
		} // proc ChangeFormatStyle

		private void ToggleFormatUpper()
		{
			package.CurrentFormat = Char.IsUpper(package.CurrentFormat)
				? Char.ToLower(package.CurrentFormat)
				: Char.ToUpper(package.CurrentFormat);
		} // proc ToggleFormatUpper

		private void FormatChangeNClicked(object sender, RoutedEventArgs e)
			=> ChangeFormatStyle('N');

		private void FormatChangeDClicked(object sender, RoutedEventArgs e)
			=> ChangeFormatStyle('D');

		private void FormatChangeBClicked(object sender, RoutedEventArgs e)
			=> ChangeFormatStyle('B');

		private void FormatChangeCClicked(object sender, RoutedEventArgs e)
			=> ChangeFormatStyle('C');

		private void FormatChangeUpperClicked(object sender, RoutedEventArgs e)
			=> ToggleFormatUpper();
	} // class GuidPackagePane

	#endregion

	#region -- class CurrentFormatConverter -------------------------------------------

	internal sealed class CurrentFormatConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var fmt = (char)value;
			return Part switch
			{
				"N" => fmt == 'n' || fmt == 'N',
				"D" => fmt == 'd' || fmt == 'D',
				"B" => fmt == 'b' || fmt == 'B',
				"C" => fmt == 'c' || fmt == 'C',
				"u" => Char.IsUpper(fmt),
				_ => false,
			};
		} // func Convert

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();

		public string Part { get; set; } = null;
	} // class CurrentFormatConverter

	#endregion
}
