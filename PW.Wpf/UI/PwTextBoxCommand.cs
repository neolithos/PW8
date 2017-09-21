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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Neo.PerfectWorking.UI
{
	public class PwTextBoxCommand : TextBox, ICommandSource
	{
		public static readonly DependencyProperty CommandProperty = ButtonBase.CommandProperty.AddOwner(typeof(PwTextBoxCommand));
		public static readonly DependencyProperty CommandParameterProperty = ButtonBase.CommandParameterProperty.AddOwner(typeof(PwTextBoxCommand));
		public static readonly DependencyProperty CommandTargetProperty = ButtonBase.CommandTargetProperty.AddOwner(typeof(PwTextBoxCommand));
		public static readonly DependencyProperty CommandContentProperty = DependencyProperty.Register(nameof(CommandContent), typeof(object), typeof(PwTextBoxCommand));
		public static readonly DependencyProperty CommandContentTemplateProperty = DependencyProperty.Register(nameof(CommandContentTemplate), typeof(DataTemplate), typeof(PwTextBoxCommand));
		public static readonly DependencyProperty CommandContentTemplateSelectorProperty = DependencyProperty.Register(nameof(CommandContentTemplateSelector), typeof(DataTemplateSelector), typeof(PwTextBoxCommand));
		public static readonly DependencyProperty CommandContentStringFormatProperty = DependencyProperty.Register(nameof(CommandContentStringFormat), typeof(string), typeof(PwTextBoxCommand));

		public ICommand Command
		{
			get => (ICommand)GetValue(CommandProperty);
			set => SetValue(CommandProperty, value);
		} // prop Command

		public object CommandParameter
		{
			get => GetValue(CommandParameterProperty);
			set => SetValue(CommandParameterProperty, value);
		} // prop CommandParameter

		public IInputElement CommandTarget
		{
			get => (IInputElement)GetValue(CommandTargetProperty);
			set => SetValue(CommandTargetProperty, value);
		} // prop CommandTarget

		public object CommandContent
		{
			get => GetValue(CommandContentProperty);
			set => SetValue(CommandContentProperty, value);
		} // func CommandContent

		public DataTemplate CommandContentTemplate
		{
			get => (DataTemplate)GetValue(CommandContentTemplateProperty);
			set => SetValue(CommandContentTemplateProperty, value);
		} // prop CommandContentTemplate

		public DataTemplateSelector CommandContentTemplateSelector
		{
			get => (DataTemplateSelector)GetValue(CommandContentTemplateSelectorProperty);
			set => SetValue(CommandContentTemplateSelectorProperty, value);
		} // prop CommandContentTemplateSelector

		public string CommandContentStringFormat
		{
			get => (string)GetValue(CommandContentStringFormatProperty);
			set => SetValue(CommandContentStringFormatProperty, value);
		} // prop CommandContentStringFormat

		static PwTextBoxCommand()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PwTextBoxCommand), new FrameworkPropertyMetadata(typeof(PwTextBoxCommand)));
		} // sctor
	} // class TextBoxCommand
}
