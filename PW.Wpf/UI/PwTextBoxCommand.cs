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
		public static DependencyProperty CommandProperty = ButtonBase.CommandProperty.AddOwner(typeof(PwTextBoxCommand));
		public static DependencyProperty CommandParameterProperty = ButtonBase.CommandParameterProperty.AddOwner(typeof(PwTextBoxCommand));
		public static DependencyProperty CommandTargetProperty = ButtonBase.CommandTargetProperty.AddOwner(typeof(PwTextBoxCommand));
		public static DependencyProperty CommandContentProperty = DependencyProperty.Register(nameof(CommandContent), typeof(object), typeof(PwTextBoxCommand));
		public static DependencyProperty CommandContentTemplateProperty = DependencyProperty.Register(nameof(CommandContentTemplate), typeof(DataTemplate), typeof(PwTextBoxCommand));
		public static DependencyProperty CommandContentTemplateSelectorProperty = DependencyProperty.Register(nameof(CommandContentTemplateSelector), typeof(DataTemplateSelector), typeof(PwTextBoxCommand));
		public static DependencyProperty CommandContentStringFormatProperty = DependencyProperty.Register(nameof(CommandContentStringFormat), typeof(string), typeof(PwTextBoxCommand));

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
