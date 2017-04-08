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
