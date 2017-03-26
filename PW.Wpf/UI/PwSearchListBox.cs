using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Neo.PerfectWorking.UI
{
	public class PwSearchListBox : ListBox
	{
		public static DependencyProperty CurrentFilterProperty = DependencyProperty.Register(nameof(CurrentFilter), typeof(string), typeof(PwSearchListBox),new UIPropertyMetadata(String.Empty, CurrentFilterChanged));

		private FrameworkElement itemsControl;
		private FrameworkElement textBoxControl;

		public PwSearchListBox()
		{
		} // ctor

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			itemsControl = (FrameworkElement)GetTemplateChild("PART_Items");
			textBoxControl = (FrameworkElement)GetTemplateChild("PART_TextBox");

			textBoxControl.LostFocus += (sender, e) => UpdateState();
			
			UpdateState();
		} // proc OnApplyTemplate
		
		private void UpdateState()
		{
			if (String.IsNullOrEmpty(CurrentFilter) && !textBoxControl.IsFocused)
				textBoxControl.Visibility = Visibility.Collapsed;
			else
				textBoxControl.Visibility = Visibility.Visible;
		} // proc UpdateState

		protected override void OnPreviewTextInput(TextCompositionEventArgs e)
		{
			base.OnPreviewTextInput(e);

			if (!textBoxControl.IsFocused)
			{
				textBoxControl.Visibility = Visibility.Visible;
				textBoxControl.Focus();
			}
		} // proc OnPreviewTextInput

		protected override void OnKeyUp(KeyEventArgs e)
		{
			base.OnKeyUp(e);
			if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
			{
				CurrentFilter = String.Empty;
				if (textBoxControl.IsFocused)
					MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
			}
			else if (textBoxControl.IsFocused && (e.Key == Key.Up || e.Key == Key.Down))
			{
				if (textBoxControl.IsFocused)
					MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
			}
		} // proc OnKeyUp

		public static void CurrentFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var collectionView = d.GetValue(ItemsControl.ItemsSourceProperty) as ICollectionView;
			if (collectionView != null)
				collectionView.Refresh();
		} // proc CurrentFilterChanged

		public string CurrentFilter
		{
			get => (string)GetValue(CurrentFilterProperty);
			set => SetValue(CurrentFilterProperty, value);
		} // prop CurrentFilter

		static PwSearchListBox()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PwSearchListBox), new FrameworkPropertyMetadata(typeof(PwSearchListBox)));
		} // sctor
	} // class SearchListBox
}
