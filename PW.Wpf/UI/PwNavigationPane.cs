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
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Neo.PerfectWorking.Data;

namespace Neo.PerfectWorking.UI
{
	#region -- class PwContentPage ----------------------------------------------------

	public class PwContentPage : ContentControl
	{
		public static readonly DependencyProperty TitleProperty = PwContentPane.TitleProperty.AddOwner(typeof(PwContentPage));

		private PwNavigationPane navigation = null;

		public PwContentPage()
		{
			//CommandBindings.Add(new CommandBinding(ApplicationCommands.Stop, (sender, e) =>
			//{
			//	Pop();
			//	e.Handled = true;
			//},
			//(sender, e) =>
			//{
			//	e.CanExecute = true;
			//	e.Handled = true;
			//}));
		} // ctor

		public bool Pop()
		{
			var e = new CancelEventArgs(false);
			OnPopping(e);
			if (e.Cancel)
				return false;

			navigation.RemovePage(this);
			return true;
		} // func Pop

		protected virtual void OnPopping(CancelEventArgs e) { }

		internal void Popped()
			=> OnPopped();

		protected virtual void OnPopped() { }

		public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
		public PwNavigationPane Navigation { get => navigation; internal set => navigation = value; }
	} // class PwContentPage

	#endregion

	#region -- class PwDialogPage -----------------------------------------------------

	public class PwDialogPage<T> : PwContentPage
	{
		private readonly TaskCompletionSource<T> result = new TaskCompletionSource<T>();

		protected void Accept(T result)
		{
			this.result.SetResult(result);
			Navigation.RemovePage(this);
		} // proc Accept

		protected void Cancel()
		{
			result.SetCanceled();
			Navigation.RemovePage(this);
		} // proc Cancel

		protected void RaiseException(Exception exception)
		{
			result.SetException(exception);
			Navigation.RemovePage(this);
		} // proc RaiseException

		protected override void OnPopped()
		{
			base.OnPopped();
			if (!IsClosed)
				Cancel();
		} // proc OnPopped

		public bool IsClosed => result.Task.IsCompleted;

		internal Task<T> ResultTask => result.Task;
	} // class PwDialogPage

	#endregion

	#region -- class PwNavigationPane -------------------------------------------------

	/// <summary>Adds stack based page model</summary>
	public class PwNavigationPane : PwContentPane, IPwWindowBackButton
	{
		#region -- CurrentPage property -----------------------------------------------

		private static readonly DependencyPropertyKey currentPagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentPage), typeof(PwContentPage), typeof(PwNavigationPane), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnCurrentPageChanged)));
		private static readonly DependencyPropertyKey currentContentControlPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentContentControl), typeof(ContentControl), typeof(PwNavigationPane), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty CurrentPageProperty = currentPagePropertyKey.DependencyProperty;
		public static readonly DependencyProperty CurrentContentControlProperty = currentContentControlPropertyKey.DependencyProperty;

		private static void OnCurrentPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PwNavigationPane)d).OnCurrentPageChanged((PwContentPage)e.NewValue, (PwContentPage)e.OldValue);

		#endregion

		private readonly EventHandler currentPageTitleChanged;

		public PwNavigationPane()
		{
			currentPageTitleChanged = (sender, e) => UpdateTitle();
		} // ctor

		#region -- Header property ----------------------------------------------------

		private static readonly DependencyPropertyKey hasHeaderPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasHeader), typeof(bool), typeof(PwNavigationPane), new FrameworkPropertyMetadata(true));
		public static readonly DependencyProperty HasHeaderProperty = hasHeaderPropertyKey.DependencyProperty;
		public static readonly DependencyProperty HeaderProperty = HeaderedContentControl.HeaderProperty.AddOwner(typeof(PwNavigationPane), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnHeaderChanged)));
		public static readonly DependencyProperty HeaderTemplateProperty = HeaderedContentControl.HeaderTemplateProperty.AddOwner(typeof(PwNavigationPane));
		public static readonly DependencyProperty HeaderTemplateSelectorProperty = HeaderedContentControl.HeaderTemplateSelectorProperty.AddOwner(typeof(PwNavigationPane));
		public static readonly DependencyProperty HeaderStringFormatProperty = HeaderedContentControl.HeaderStringFormatProperty.AddOwner(typeof(PwNavigationPane));

		private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PwNavigationPane)d).OnHeaderChanged(e.NewValue, e.OldValue);

		protected virtual void OnHeaderChanged(object newValue, object oldValue)
		{
			SetValue(hasHeaderPropertyKey, newValue != null);
			RemoveLogicalChild(oldValue);
			AddLogicalChild(newValue);
		} // proc OnHeaderchanged

		#endregion

		public static readonly DependencyProperty BaseTitleProperty = DependencyProperty.Register(nameof(BaseTitle), typeof(string), typeof(PwContentPage));

		#region -- Current Page handling ----------------------------------------------

		private readonly List<PwContentPage> pages = new List<PwContentPage>();

		protected virtual void OnCurrentPageChanged(PwContentPage newValue, PwContentPage oldValue)
		{
			var pdc = DependencyPropertyDescriptor.FromProperty(PwContentPage.TitleProperty, typeof(PwContentPage));
			if (oldValue != null)
				pdc.RemoveValueChanged(oldValue, currentPageTitleChanged);
			if (newValue != null)
				pdc.AddValueChanged(newValue, currentPageTitleChanged);
			OnNotifyPropertyChanged(nameof(IPwWindowBackButton.CanBack));
		} // proc OnCurrentPageChanged

		protected override void OnContentChanged(object oldContent, object newContent)
		{
			base.OnContentChanged(oldContent, newContent);
			UpdateCurrentPage();
		} // proc OnContentChanged

		private void UpdateCurrentPage()
		{
			CurrentPage = pages.Count > 0
				? pages[pages.Count - 1]
				: null;

			SetValue(currentContentControlPropertyKey, (ContentControl)CurrentPage ?? this);
			UpdateTitle();
		} // proc UpdateCurrentPage

		private void UpdateTitle() 
			=> Title = CurrentPage == null ? BaseTitle : BaseTitle + " / " + CurrentPage.Title;

		private bool TryPushExisting(Predicate<PwContentPage> predicate)
		{
			var idx = pages.FindIndex(predicate);
			if (idx == -1)
				return false;
			else if (idx == pages.Count - 1)
				return true;
			else
			{
				var p = pages[idx];
				pages.RemoveAt(idx);
				pages.Add(p);
				UpdateCurrentPage();
				return true;
			}
		} // func TryPushExisting

		public void Toggle(PwContentPage newPage)
		{
			if (CurrentPage == newPage)
				newPage.Pop();
			else
				Push(newPage);
		} // proc Toggle

		public void Push(PwContentPage newPage)
		{
			if (!TryPushExisting(c => c == newPage))
			{
				newPage.Navigation = this;
				pages.Add(newPage);

				UpdateCurrentPage();
			}
		} // proc Push

		public void Push(Type pageType, Func<PwContentPage> pageFactory = null)
		{
			if (!TryPushExisting(c => c.GetType() == pageType))
				Push(pageFactory?.Invoke() ?? (PwContentPage)Activator.CreateInstance(pageType));
		} // proc Push

		public Task<T> PushAsync<T>(PwDialogPage<T> newPage)
		{
			Push(newPage);
			return newPage.ResultTask;
		} // proc PushModulAsync

		public async Task<(bool, T)> TryPushAsync<T>(PwDialogPage<T> newPage)
		{
			Push(newPage);
			try
			{
				return (true, await newPage.ResultTask);
			}
			catch (TaskCanceledException)
			{
				return (false, default(T));
			}
		} // proc PushModulAsync

		internal void RemovePage(PwContentPage page)
		{
			var idx = pages.IndexOf(page);
			if (idx >= 0 && idx < pages.Count)
			{
				var p = pages[idx];
				pages.RemoveAt(idx);
				p.Popped();

				if (idx == pages.Count)
					UpdateCurrentPage();
			}
		} // proc RemovePage

		void IPwWindowBackButton.GoBack()
			=> CurrentPage?.Pop();

		bool IPwWindowBackButton.CanBack
			=> CurrentPage != null;

		#endregion

		protected override IEnumerator LogicalChildren => UIHelper.GetChildrenEnumerator(base.LogicalChildren, Header);

		public PwContentPage CurrentPage { get => (PwContentPage)GetValue(CurrentPageProperty); private set => SetValue(currentPagePropertyKey, value); }

		public bool HasHeader => (bool)GetValue(HasHeaderProperty);
		public object Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
		public DataTemplate HeaderTemplate { get => (DataTemplate)GetValue(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }
		public DataTemplateSelector HeaderTemplateSelector { get => (DataTemplateSelector)GetValue(HeaderTemplateSelectorProperty); set => SetValue(HeaderTemplateSelectorProperty, value); }
		public string HeaderStringFormat { get => (string)GetValue(HeaderStringFormatProperty); set => SetValue(HeaderStringFormatProperty, value); }

		public string BaseTitle { get => (string)GetValue(BaseTitleProperty); set => SetValue(BaseTitleProperty, value); }

		public ContentControl CurrentContentControl => (ContentControl)GetValue(CurrentContentControlProperty);

		static PwNavigationPane()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PwNavigationPane), new FrameworkPropertyMetadata(typeof(PwNavigationPane)));
		}
	} // class PwNavigationPane

	#endregion
}
