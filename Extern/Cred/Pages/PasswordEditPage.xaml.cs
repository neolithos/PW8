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
using System.ComponentModel;
using System.Security;
using System.Windows.Data;
using System.Windows.Input;
using Neo.PerfectWorking.Data;
using Neo.PerfectWorking.UI;
using TecWare.DE.Data;

namespace Neo.PerfectWorking.Cred.Pages
{
	/// <summary>
	/// Interaction logic for EditPasswordPage.xaml
	/// </summary>
	public partial class PasswordEditPage : PwContentPage
	{
		#region -- class PasswordEditModel --------------------------------------------

		private sealed class PasswordEditModel : ObservableObject, ICredentialInfo
		{
			private ICredentialInfo currentInfo;

			private ICredentialProvider provider;
			private string targetName;
			private readonly PwPasswordBoxController password = new PwPasswordBoxController();
			private string userName;
			private string comment;
			private bool isModified = false;

			public PasswordEditModel(PasswordEditPage page, ICredentialInfo currentInfo)
			{
				this.currentInfo = currentInfo;

				password.PasswordChanged += (sender, e) => SetModified();

				if(currentInfo == null)
				{
					provider = page.Providers.CurrentItem as ICredentialProvider;
					targetName = String.Empty;
					password.Clear();
					userName = String.Empty;
					comment = String.Empty;
				}
				else
				{
					provider = currentInfo.Provider;
					targetName = currentInfo.TargetName;
					password.SetPassword(currentInfo.GetPassword());
					userName = currentInfo.UserName;
					comment = currentInfo.Comment;
				}
				ResetModified();
			} // ctor

			protected override void OnPropertyChanged(string propertyName)
			{
				base.OnPropertyChanged(propertyName);
				if (propertyName != nameof(IsModified)
					&& propertyName != nameof(IsNew))
					SetModified();
			} // proc OnPropertyChanged

			private void SetModified()
				=> Set(ref isModified, true, nameof(IsModified));

			private void ResetModified()
				=> Set(ref isModified, false, nameof(IsModified));

			void ICredentialInfo.SetPassword(SecureString password)
				=> throw new NotSupportedException();

			SecureString ICredentialInfo.GetPassword()
				=> password.GetPassword();

			public bool Validate(out string msg)
			{
				if (provider == null)
				{
					msg = "Provider fehlt.";
					return false;
				}
				else if (String.IsNullOrEmpty(targetName))
				{
					msg = "Ziel fehlt.";
					return false;
				}
				else if (String.IsNullOrEmpty(UserName))
				{
					msg = "Nutzer fehlt.";
					return false;
				}
				else
				{
					msg = null;
					return true;
				}
			} // func Validate

			public void Save()
			{
				if (currentInfo == null) // create a new one
				{
					currentInfo = provider.Append(this);
					OnPropertyChanged(nameof(IsNew));
				}
				else if (currentInfo.TargetName != targetName) // remove and create it with a new target
				{
					provider.Remove(currentInfo.TargetName);
					currentInfo = provider.Append(this);
					OnPropertyChanged(nameof(IsNew));
				}
				else // update current info
				{
					currentInfo.UserName = userName;
					currentInfo.SetPassword(password.GetPassword());
					currentInfo.Comment = comment;
				}
				ResetModified();
			} // proc Save

			public ICredentialProvider Provider { get => provider; set => Set(ref provider, value, nameof(Provider)); }
			public string TargetName { get => targetName; set => Set(ref targetName, value, nameof(TargetName)); }
			public string UserName { get => userName; set => Set(ref userName, value, nameof(UserName)); }
			public PwPasswordBoxController Password => password;
			public string Comment { get => comment; set => Set(ref comment, value, nameof(Comment)); }

			public ICollectionView Providers => null;

			public bool IsNew => currentInfo == null;
			public bool IsModified => isModified;

			public ICredentialInfo CurrentInfo => currentInfo;

			DateTime ICredentialInfo.LastWritten => DateTime.Now;
			object ICredentialInfo.Image => null;
		} // class PasswordEditModel

		#endregion

		private readonly CredPackagePane credPane;
		private readonly IPwShellUI ui;
		private PasswordEditModel currentContext = null;

		private readonly CollectionViewSource credentialProviderCollectionView;

		public PasswordEditPage(CredPackagePane credPane)
		{
			this.credPane = credPane;

			ui = credPane.Package.Global.UI;

			credentialProviderCollectionView = new CollectionViewSource() { Source = credPane.Package.CredentialProviders };
			credentialProviderCollectionView.Filter += (sender, e) => e.Accepted = e.Item is ICredentialProvider cp && !cp.IsReadOnly;
			credentialProviderCollectionView.SortDescriptions.Add(new SortDescription(nameof(ICredentialProvider.Name), ListSortDirection.Ascending));

			InitializeComponent();

			New();
		} // ctor

		public void New()
		{
			DataContext = currentContext = new PasswordEditModel(this, null);
		} // proc New

		public void Load(ICredentialInfo credentialInfo)
		{
			DataContext = currentContext = new PasswordEditModel(this, credentialInfo);
		} // proc Load

		private void SaveExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			if (!currentContext.Validate(out var msg))
				ui.MsgBox(msg, icon: "i");
			else
			{
				currentContext.Save();
				CommandManager.InvalidateRequerySuggested();
			}
			e.Handled = true;
		} // event SaveExecuted

		public void SaveCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = currentContext?.IsModified ?? false;
			e.Handled = true;
		} // event SaveCanExecute

		private void CloseExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			Pop();
			e.Handled = true;
		} // event CloseExecuted

		public ICollectionView Providers => credentialProviderCollectionView.View;
		public ICredentialInfo CurrentCredential => currentContext?.CurrentInfo;
	} // class PasswordEditPage
}
