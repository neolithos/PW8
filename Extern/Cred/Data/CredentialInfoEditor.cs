using Neo.PerfectWorking.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security;
using System.Windows.Input;
using System.Windows.Controls;
using Neo.PerfectWorking.UI;

namespace Neo.PerfectWorking.Cred.Data
{
	#region -- class NewCredentialInfo --------------------------------------------------

	internal sealed class NewCredentialInfo : ObservableObject, ICredentialInfo
	{
		private readonly SimpleCommand appendCommand;

		private ICredentialProvider provider;
		private string targetName;
		private string userName;
		private string comment;
		private readonly PwPasswordBoxController password = new PwPasswordBoxController();

		public NewCredentialInfo(IPwShellUI ui)
		{
			Clear();

			this.appendCommand = new SimpleCommand(
				p =>
				{
					if (appendCommand.CanExecute(p))
					{
						try
						{
							provider.Append(this);
							Clear();
						}
						catch (Exception e)
						{
							ui.ShowException(e); // show error tooltip
						}
					}
				},
				p => Provider != null && !String.IsNullOrEmpty(TargetName) && !String.IsNullOrEmpty(UserName)
			);
		} // ctor

		public override void OnPropertyChanged(string propertyName)
		{
			base.OnPropertyChanged(propertyName);
			appendCommand?.Refresh();
		} // proc OnPropertyChanged

		public void Clear()
		{
			TargetName = String.Empty;
			UserName = String.Empty;
			Comment = String.Empty;
			password.Clear();
		} // proc Clear

		public void SetPassword(SecureString password) 
			=> throw new NotSupportedException();

		public SecureString GetPassword()
			=> password.GetPassword();

		public ICredentialProvider Provider { get => provider; set => SetProperty(nameof(Provider), ref provider, value); }
		public string TargetName { get => targetName; set => SetProperty(nameof(TargetName), ref targetName, value); }
		public string UserName { get => userName; set => SetProperty(nameof(UserName), ref userName, value); }
		public string Comment { get => comment; set => SetProperty(nameof(Comment), ref comment, value); }
		public PwPasswordBoxController Password => password;

		public ICommand AppendCommand => appendCommand;

		DateTime ICredentialInfo.LastWritten => DateTime.Now;
		object ICredentialInfo.Image => null;
	} // class NewCredentialInfo

	#endregion

	#region -- class ChangeCredentialInfo -----------------------------------------------

	internal sealed class ChangeCredentialInfo : ObservableObject
	{
		private ICredentialInfo currentInfo = null;

		private readonly SimpleCommand changeCommand;
		private string userName = String.Empty;
		private readonly PwPasswordBoxController password = new PwPasswordBoxController();
		private string comment = String.Empty;
		private bool isModified = false;

		public ChangeCredentialInfo()
		{
			changeCommand = new SimpleCommand(
				p => Update(),
				p => currentInfo != null && isModified
			);

			password.PasswordChanged += (sender, e) => SetModified();
		} // ctor

		public override void OnPropertyChanged(string propertyName)
		{
			base.OnPropertyChanged(propertyName);
			SetModified();
		} // proc OnPropertyChanged

		private void SetModified()
		{
			if (!isModified)
			{
				isModified = true;
				changeCommand.Refresh();
			}
		} // proc SetModified

		private void ResetModified()
		{
			if (isModified)
			{
				isModified = false;
				changeCommand.Refresh();
			}
		} // proc ResetModified

		public void SetCurrent(ICredentialInfo currentInfo)
		{
			if (this.currentInfo != currentInfo)
			{
				this.currentInfo = currentInfo;

				UserName = currentInfo.UserName;
				password.SetPassword(currentInfo.GetPassword());
				Comment = currentInfo.Comment;

				ResetModified();
			}
		} // proc SetCurrent

		private void Update()
		{
			currentInfo.UserName = userName;
			currentInfo.SetPassword(password.GetPassword());
			currentInfo.Comment = comment;

			ResetModified();
		} // proc Update

		public string UserName { get => userName; set => SetProperty(nameof(UserName), ref userName, value); }
		public PwPasswordBoxController Password => password;
		public string Comment { get => comment; set => SetProperty(nameof(Comment), ref comment, value); }
		public ICommand ChangeCommand => changeCommand;
	} // class ChangeCredentialInfo

	#endregion
}
