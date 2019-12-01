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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Neo.PerfectWorking.Cred.Pages;
using Neo.PerfectWorking.UI;

namespace Neo.PerfectWorking.Cred
{
	public partial class CredPackagePane : PwNavigationPane
	{
		public static RoutedUICommand CopyGeneratedPasswordCommand = new RoutedUICommand();
		public static RoutedUICommand GeneratePasswordCommand = new RoutedUICommand();

		#region -- class CredPackageModel -----------------------------------------------

		private sealed class CredPackageModel : IList, INotifyCollectionChanged
		{
			public event NotifyCollectionChangedEventHandler CollectionChanged;

			private readonly CredPackage package;
			private readonly NotifyCollectionChangedEventHandler credentialProviderChanged;
			private readonly List<ICredentialInfo> credentials = new List<ICredentialInfo>();

			private readonly CollectionViewSource credentialInfoCollectionView;

			public CredPackageModel(CredPackage package)
			{
				this.package = package;
				this.credentialProviderChanged = CredentialProvider_CollectionChanged;
			
				// connect
				package.CredentialProviders.CollectionChanged += CredentialProviders_CollectionChanged;
				CredentialProviders_CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

				credentialInfoCollectionView = new CollectionViewSource() { Source = this };
				credentialInfoCollectionView.SortDescriptions.Add(new SortDescription(nameof(ICredentialInfo.TargetName), ListSortDirection.Ascending));
			} // ctor

			#region -- Credential Provider Sync -----------------------------------------

			private void CredentialProviders_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
			{
				void AddNotifier()
				{
					for (var i = 0; i < e.NewItems.Count; i++)
					{
						var c = e.NewItems[i];
						if (c is ICredentialProvider cp)
						{
							cp.CollectionChanged += credentialProviderChanged;
							ResetCredentialInfo(cp);
						}
					}
				} // proc AddNotifier
				void RemoveNotifier()
				{
					for (var i = 0; i < e.OldItems.Count; i++)
					{
						var c = e.OldItems[i];
						if (c is ICredentialProvider cp)
						{
							cp.CollectionChanged -= credentialProviderChanged;
							ClearCredentialInfo(cp);
						}
					}
				} // proc RemoveNotifier

				switch (e.Action)
				{
					case NotifyCollectionChangedAction.Add:
						AddNotifier();
						break;
					case NotifyCollectionChangedAction.Remove:
						RemoveNotifier();
						break;
					case NotifyCollectionChangedAction.Replace:
						RemoveNotifier();
						AddNotifier();
						break;
					case NotifyCollectionChangedAction.Reset:
						ClearCredentialInfos();
						ResetCredentialInfos();
						break;
				}
			} // proc CredentialProviders_CollectionChanged

			private void CredentialProvider_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
			{
				void AddCredentialInfo()
				{
					for (var i = 0; i < e.NewItems.Count; i++)
					{
						var c = e.NewItems[i];
						if (c is ICredentialInfo ci)
							InternalAdd(ci);
					}
				} // proc AddNotifier
				void RemoveCredentialInfo()
				{
					for (var i = 0; i < e.OldItems.Count; i++)
					{
						var c = e.OldItems[i];
						if (c is ICredentialInfo cp)
							InternalRemove(cp);
					}
				} // proc RemoveNotifier

				switch (e.Action)
				{
					case NotifyCollectionChangedAction.Add:
						AddCredentialInfo();
						break;
					case NotifyCollectionChangedAction.Remove:
						RemoveCredentialInfo();
						break;
					case NotifyCollectionChangedAction.Replace:
						RemoveCredentialInfo();
						AddCredentialInfo();
						break;
					case NotifyCollectionChangedAction.Reset:
						ResetCredentialInfo((ICredentialProvider)sender);
						break;
				}
			} // proc CredentialProvider_CollectionChanged

			private void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
				=> CollectionChanged?.Invoke(this, e);

			private void InternalAdd(ICredentialInfo info)
			{
				if (credentials.IndexOf(info) >= 0)
					return;

				var idx = credentials.Count;
				credentials.Add(info);
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, info, idx));
			} // proc InternalAdd

			private void InternalRemove(ICredentialInfo info)
			{
				var idx = credentials.IndexOf(info);
				if (idx >= 0)
				{
					credentials.RemoveAt(idx);
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, info, idx));
				}
			} // proc InternalRemove

			private void ClearCredentialInfos()
			{
				credentials.Clear();
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			} // proc ClearCredentialInfo

			private void ClearCredentialInfo(ICredentialProvider provider)
			{
				for (var i = credentials.Count - 1; i >= 0; i--)
				{
					if (credentials[i].Provider == provider)
					{
						var old = credentials[i];
						credentials.RemoveAt(i);
						OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, old, i));
					}
				}
			} // proc ClearCredentialInfo

			private void ResetCredentialInfos()
			{
				credentials.Clear();
				foreach (var cp in package.CredentialProviders)
					credentials.AddRange(cp);

				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			} // proc ReseetCredentialInfos

			private void ResetCredentialInfo(ICredentialProvider provider)
			{
				ClearCredentialInfo(provider);
				foreach (var c in provider)
				{
					var i = credentials.Count;
					credentials.Add(c);
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, c, i));
				}
			} // proc ResetCredentialInfo

			#endregion

			#region -- IList implementation ---------------------------------------------

			public bool Contains(object value)
				=> credentials.Contains(value as ICredentialInfo);

			public void CopyTo(Array array, int index)
				=> ((IList)credentials).CopyTo(array, index);

			public IEnumerator GetEnumerator()
				=> credentials.GetEnumerator();

			public int IndexOf(object value)
				=> credentials.IndexOf(value as ICredentialInfo);

			int IList.Add(object value) => throw new NotSupportedException();
			void IList.Insert(int index, object value) => throw new NotSupportedException();
			void IList.Clear() => throw new NotSupportedException();
			void IList.Remove(object value) => throw new NotSupportedException();
			void IList.RemoveAt(int index) => throw new NotSupportedException();

			public bool IsReadOnly => true;
			public bool IsFixedSize => false;
			public object SyncRoot => null;
			public bool IsSynchronized => false;

			public int Count => credentials.Count;

			public object this[int index] { get => credentials[index]; set => throw new NotSupportedException(); }

			#endregion

			public ICollectionView Credentials => credentialInfoCollectionView.View;
		} // class CredPackageModel

		#endregion

		private readonly CredPackage package;
		private readonly CredPackageModel model;

		private readonly PasswordGeneratorPage passwordGeneratorPage;
		private readonly PasswordEditPage passwordEditPage;

		public CredPackagePane(CredPackage package)
		{
			this.package = package;
			this.model = new CredPackageModel(package);

			passwordGeneratorPage = new PasswordGeneratorPage(package);

			passwordEditPage = new PasswordEditPage(this);

			InitializeComponent();

			this.DataContext = model;

			// set filter command
			model.Credentials.Filter = OnFilter;
		} // ctor

		private bool OnFilter(object item)
		{
			if (item is ICredentialInfo c)
			{
				var currentFilter = credentialListBox.CurrentFilter;
				return (c.TargetName != null && c.TargetName.IndexOf(currentFilter, StringComparison.OrdinalIgnoreCase) >= 0)
					|| (c.UserName != null && c.UserName.IndexOf(currentFilter, StringComparison.OrdinalIgnoreCase) >= 0)
					|| (c.Comment != null && c.Comment.IndexOf(currentFilter, StringComparison.OrdinalIgnoreCase) >= 0);
			}
			else
				return false;
		} // func OnFilter
		
		private void RemoveCredentialInfo(object sender, RoutedEventArgs e)
		{
			var source = (FrameworkElement)e.Source;
			var credInfo = (ICredentialInfo)source.DataContext;
			((Popup)FindResource("removePopup")).IsOpen = false;
			credInfo.Provider.Remove(credInfo.TargetName);
			e.Handled = true;
		} // event RemoveCredentialInfo

		private void PushNewPasswordClick(object sender, RoutedEventArgs e)
		{
			if (CurrentPage == passwordEditPage)
			{
				if (passwordEditPage.CurrentCredential == null)
					passwordEditPage.Pop();
				else
					passwordEditPage.New();
			}
			else
			{
				if (passwordEditPage.CurrentCredential != null)
					passwordEditPage.New();
				Push(passwordEditPage);
			}
		} // event PushNewPasswordClick

		private void PushEditPasswordClick(object sender, RoutedEventArgs e)
		{
			var source = (FrameworkElement)e.Source;
			var credInfo = (ICredentialInfo)source.DataContext;
			if (passwordEditPage == CurrentPage)
				return;

			if (passwordEditPage.CurrentCredential != credInfo)
				passwordEditPage.Load(credInfo);
			Push(passwordEditPage);
		} // event PushEditPasswordClick

		private void credentialItemDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
				PushEditPasswordClick(sender, e);
		} // proc credentialItemDoubleClick

		private void PushPasswordGeneratorClick(object sender, RoutedEventArgs e)
			=> Toggle(passwordGeneratorPage);

		public CredPackage Package => package;
	} // class CredPackagePane
}
