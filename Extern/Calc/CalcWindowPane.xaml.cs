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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Neo.IronLua;
using Neo.PerfectWorking.Data;
using Neo.PerfectWorking.UI;

namespace Neo.PerfectWorking.Calc
{
	#region -- class CalcWindowPane ---------------------------------------------------

	public partial class CalcWindowPane : PwContentPane
	{
		#region -- class VariableView ---------------------------------------------------

		private sealed class VariableView : IComparable<VariableView>, INotifyPropertyChanged
		{
			public event PropertyChangedEventHandler PropertyChanged;

			private readonly LuaTable table;
			private readonly string name;

			public VariableView(LuaTable table, string name)
			{
				this.table = table;
				this.name = name;
			} // ctor

			public override int GetHashCode()
				=> name.GetHashCode();

			public override bool Equals(object obj)
				=> obj is VariableView v ? CompareTo(v) == 0 : false;

			public int CompareTo(VariableView other)
				=> String.Compare(this.name, other.name, StringComparison.OrdinalIgnoreCase);

			public void RefreshValue()
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));

			public string Name => name;
			public object Value => table.GetMemberValue(name, rawGet: true);
		} // class VariableView

		#endregion

		#region -- class VariableCollection ---------------------------------------------

		private sealed class VariableCollection : IList, IEnumerable<VariableView>, INotifyCollectionChanged
		{
			public event NotifyCollectionChangedEventHandler CollectionChanged;

			private readonly LuaTable table;
			private readonly List<VariableView> variables = new List<VariableView>();

			public VariableCollection(LuaTable table)
			{
				this.table = table;

				table.PropertyChanged += (sender, e) => CheckProperty(e.PropertyName);
			} // ctor

			private void CheckProperty(string propertyName)
			{
				var v = new VariableView(table, propertyName);
				var index = variables.BinarySearch(v);
				if (index >= 0) // variable exists
				{
					v = variables[index];
					if (table.GetMemberValue(propertyName, rawGet: true) == null) // remove because it is zero
					{
						variables.RemoveAt(index);
						CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, v, index));

					}
					else
						v.RefreshValue();
				}
				else // add variable and reset list
				{
					index = ~index;
					variables.Insert(index, v);
					CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, v, index));
				}
			} // func CheckProperty

			void ICollection.CopyTo(Array array, int index)
				=> ((IList)variables).CopyTo(array, index);

			bool IList.Contains(object value)
				=> value is VariableView v ? variables.Contains(v) : false;

			int IList.IndexOf(object value)
				=> value is VariableView v ? variables.IndexOf(v) : -1;

			public IEnumerator<VariableView> GetEnumerator()
				=> variables.GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			int IList.Add(object value) => throw new NotSupportedException();
			void IList.Clear() => throw new NotSupportedException();

			void IList.Insert(int index, object value) => throw new NotSupportedException();
			void IList.Remove(object value) => throw new NotSupportedException();
			void IList.RemoveAt(int index) => throw new NotSupportedException();

			bool IList.IsReadOnly => true;
			bool IList.IsFixedSize => false;
			object ICollection.SyncRoot => null;
			bool ICollection.IsSynchronized => false;

			public int Count => variables.Count;

			public object this[int index] { get => variables[index]; set => throw new NotSupportedException(); }
		} // class VariableCollection

		#endregion

		#region -- class FormularView ---------------------------------------------------

		private sealed class FormularView : INotifyPropertyChanged
		{
			public event PropertyChangedEventHandler PropertyChanged;

			private readonly Formular formular;
			private object value;

			public FormularView(Formular formular)
			{
				this.formular = formular;
			} // ctor

			public void Calculate()
			{
				value = formular.GetResult();
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
			} // proc Calculate

			public string Formular => formular.Value;
			public object Value => value;
		} // class FormularView

		#endregion

		public static readonly RoutedCommand ExecuteFormularCommand = new RoutedCommand("Execute", typeof(CalcWindowPane));

		public static readonly DependencyProperty CurrentFormularTextProperty = DependencyProperty.Register(nameof(CurrentFormularText), typeof(string), typeof(CalcWindowPane));
		private static readonly DependencyPropertyKey currentAnsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentAns), typeof(object), typeof(CalcWindowPane), new FrameworkPropertyMetadata());
		private static readonly DependencyPropertyKey variableCollectionPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Variables), typeof(VariableCollection), typeof(CalcWindowPane), new FrameworkPropertyMetadata());

		private readonly CalcPackage package;
		private readonly IPwShellUI ui; 
		private readonly FormularEnvironment currentEnvironment;
		private readonly ObservableCollection<FormularView> formulars = new ObservableCollection<FormularView>();
		private readonly ICollectionView formularsView;

		public CalcWindowPane(CalcPackage package)
		{
			this.package = package ?? throw new ArgumentNullException(nameof(package));
			ui = package.Global.UI;

			InitializeComponent();

			currentEnvironment = package.CreateNewEnvironment();
			SetValue(variableCollectionPropertyKey, new VariableCollection(currentEnvironment));

			var source = new CollectionViewSource { Source = formulars };
			formularsView = source.View;

			CommandBindings.Add(
				new CommandBinding(ExecuteFormularCommand,
					(sender, e) =>
					{
						ProcessFormular(true);
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

		private void FormularListDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (e.Source is FrameworkElement fe && fe.DataContext is FormularView fv)
			{
				CurrentFormularText = fv.Formular;
				ProcessFormular(false);
			}
		} // event FormularListDoubleClick

		private void ProcessFormular(bool add)
		{
			try
			{
				var formular = new Formular(currentEnvironment, CurrentFormularText);

				// create view
				var formularView = new FormularView(formular);

				// calculate result
				formularView.Calculate();
				SetValue(currentAnsPropertyKey, formularView.Value);

				// add to history
				if (add)
				{
					formulars.Add(formularView);
					formularsView.MoveCurrentToLast();
					formularList.ScrollIntoView(formularsView.CurrentItem);
				}
			}
			catch (FormularException e)
			{
				formularText.CaretIndex = e.Position;
				ui.ShowNotification(e.Message);
			}
			catch (Exception e)
			{
				ui.ShowException(e);
			}
		} // proc ProcessFormular

		public string CurrentFormularText { get => (string)GetValue(CurrentFormularTextProperty); set => SetValue(CurrentFormularTextProperty, value); }
		public object CurrentAns => GetValue(currentAnsPropertyKey.DependencyProperty);
		public IList Variables => (IList)GetValue(variableCollectionPropertyKey.DependencyProperty);
		public ICollectionView Formulars => formularsView;
	} // class CalcWindowPane

	#endregion

	#region -- class AnsConverter -----------------------------------------------------

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

				return Base switch
				{
					2 => "0b" + System.Convert.ToString(System.Convert.ToInt64(value), 2),
					8 => "0o" + System.Convert.ToString(System.Convert.ToInt64(value), 8),
					10 => value.ToString(),
					16 => "0x" + System.Convert.ToString(System.Convert.ToInt64(value), 16),
					_ => "<error>",
				};
			}
		} // func Convert

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();

		public int Base { get; set; } = 10;
	} // class AnsConverter

	#endregion
}
