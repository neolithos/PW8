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
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Neo.PerfectWorking.Stuff;

namespace Neo.PerfectWorking.UI
{
	#region -- class PwPasswordBoxController --------------------------------------------

	public class PwPasswordBoxController
	{
		public event EventHandler PasswordChanged;

		private PwPasswordBox passwordBox = null;
		private SecureString passwordText = null;
		private readonly RoutedEventHandler passwordBoxChanged;
		
		public PwPasswordBoxController()
		{
			passwordBoxChanged = (sender, e) => PasswordChanged?.Invoke(this, EventArgs.Empty);
		} // ctor

		internal void Attach(PwPasswordBox passwordBox)
		{
			if (this.passwordBox != passwordBox)
			{
				Detach();
				this.passwordBox = passwordBox;
				this.passwordBox.SetPassword(passwordText);
				this.passwordBox.PasswordChanged += passwordBoxChanged;
			}
		} // proc Attach

		internal void Detach()
		{
			if (passwordBox != null)
			{
				passwordText = passwordBox.GetPassword();
				passwordBox.PasswordChanged -= passwordBoxChanged;
			}
			passwordBox = null;
		} // proc Detach

		public void SetPassword(SecureString password)
		{
			if (passwordBox == null)
				passwordText = password;
			else
				passwordBox.SetPassword(password);
		} // proc SetPassword

		public SecureString GetPassword()
				=> passwordBox == null
					? passwordText
					: passwordBox.GetPassword();

		public void Clear()
		{
			passwordBox?.Clear();
			passwordText = null;
		} // proc Clear
	} // class PwPasswordBoxController

	#endregion

	#region -- class PwPasswordBox ------------------------------------------------------

	public sealed class PwPasswordBox : Control
	{
		public static readonly DependencyProperty ControllerProperty = DependencyProperty.Register(nameof(Controller), typeof(PwPasswordBoxController), typeof(PwPasswordBox), new FrameworkPropertyMetadata(OnControllerChangedCallback));
		public static readonly DependencyProperty MaxLengthProperty = TextBox.MaxLengthProperty.AddOwner(typeof(PwPasswordBox));
		private static readonly DependencyPropertyKey hasPasswordPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasPassword), typeof(bool), typeof(PwPasswordBox), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty HasPasswordProperty = hasPasswordPropertyKey.DependencyProperty;
		public static readonly DependencyProperty AllowCopyPasswordProperty = DependencyProperty.Register(nameof(AllowCopyPassword), typeof(bool), typeof(PwPasswordBox), new FrameworkPropertyMetadata(false, new PropertyChangedCallback(OnAllowCopyPasswordChanged)));
		public static readonly DependencyProperty IsReadOnlyProperty = TextBoxBase.IsReadOnlyProperty.AddOwner(typeof(PwPasswordBox));

		public static readonly RoutedEvent PasswordChangedEvent = PasswordBox.PasswordChangedEvent.AddOwner(typeof(PwPasswordBox));

		// controls
		private PasswordBox passwordBox;
		private TextBlock visibleBox;

		// password store
		private SecureString password = null;
		private bool passwordChanged = false;
		private bool inPasswordChange = false;

		private RoutedEventHandler passwordChangedEventHandler;
		private CommandBinding copyCommandBinding;
		private DependencyPropertyChangedEventHandler isVisibleChangedEventHandler;

		public PwPasswordBox()
		{
			passwordChangedEventHandler = (sender, e) => { OnPasswordChanged(); e.Handled = true; };
			isVisibleChangedEventHandler = (sender, e) => OnVisibleBoxChanged((bool)e.OldValue, (bool)e.NewValue);

			copyCommandBinding = new CommandBinding(ApplicationCommands.Copy,
				(sender, e) => { CopyPassword(); e.Handled = true; },
				(sender, e) => { e.CanExecute = HasPassword; e.Handled = true; }
			);
		} // ctor

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();
			DetachEvents();

			passwordBox = (PasswordBox)GetTemplateChild("PART_PasswordBox");
			visibleBox = (TextBlock)GetTemplateChild("PART_VisibleBox");

			AttachEvents();

			UpdatePasswordContent();
		} // proc OnApplyTemplate

		private void AttachEvents()
		{
			if (visibleBox != null)
				visibleBox.IsVisibleChanged += isVisibleChangedEventHandler;
			if (passwordBox != null)
			{
				if (AllowCopyPassword)
					passwordBox.CommandBindings.Add(copyCommandBinding);
				passwordBox.PasswordChanged += passwordChangedEventHandler;
			}
		} // proc AttachEvents

		private void DetachEvents()
		{
			if (visibleBox != null)
				visibleBox.IsVisibleChanged -= isVisibleChangedEventHandler;
			if (passwordBox != null)
			{
				passwordBox.CommandBindings.Remove(copyCommandBinding);
				passwordBox.PasswordChanged -= passwordChangedEventHandler;
			}
		} // proc DetachEvents


		private string GetPasswordString()
		{
			using (var tmp = (passwordBox?.SecurePassword ?? password))
			{
				if (tmp != null && tmp.Length > 0)
				{
					using (var passwordPtr = new InteropSecurePassword(tmp))
						return Marshal.PtrToStringUni(passwordPtr, tmp.Length);
				}
				else
					return String.Empty;
			}
		} // proc GetPasswordString

		private void OnVisibleBoxChanged(bool oldValue, bool newValue)
		{
			if (newValue) // unpack password and set i
				visibleBox.Text = GetPasswordString();
			else // clear password
				visibleBox.Text = null;
		} // proc OnVisibleBoxChanged

		private void OnPasswordChanged()
		{
			if (!inPasswordChange)
			{
				using (var t = passwordBox.SecurePassword)
					SetValue(hasPasswordPropertyKey, t != null && t.Length > 0);
				passwordChanged = true;
			}

			// raise password changed
			RaiseEvent(new RoutedEventArgs(PasswordChangedEvent, this));
		} // proc OnPasswordChanged

		private void UpdatePasswordContent()
		{
			inPasswordChange = true;
			try
			{
				if (password == null || password.Length == 0)
					passwordBox.Password = String.Empty;
				else // update the password (use internal method, not password property)
					setSecurePasswordMethodInfo.Invoke(passwordBox, new object[] { password.Copy() });
			}
			finally
			{
				inPasswordChange = false;
			}
		} // proc UpdatePasswordContent

		public void Clear()
		{
			// clear current password
			password?.Dispose();
			password = null;
			SetValue(hasPasswordPropertyKey, false);
			passwordChanged = false;

			// if attached
			if (passwordBox != null)
				passwordBox.Password = String.Empty;
		} // proc Clear

		public void CopyPassword()
		{
			if (HasPassword)
			{
				try
				{
					Clipboard.SetText(GetPasswordString());
				}
				catch
				{
					MessageBox.Show("Password copy failed.");
				}
			} 
		} // proc CopyPassword

		public void SetPassword(SecureString password)
		{
			this.password = password?.Copy();
			SetValue(hasPasswordPropertyKey, password != null && password.Length > 0);
			passwordChanged = false;

			if (passwordBox != null)
				UpdatePasswordContent();
		} // proc SetPassword

		public SecureString GetPassword()
		{
			if (passwordChanged)
			{
				password = passwordBox.SecurePassword;
				passwordChanged = false;
			}

			return password?.Copy();
		} // func GetPassword

		private void OnControllerChanged(PwPasswordBoxController oldValue, PwPasswordBoxController newValue)
		{
			oldValue?.Detach();
			newValue?.Attach(this);
		} // proc OnControllerChanged

		private static void OnControllerChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PwPasswordBox)d).OnControllerChanged((PwPasswordBoxController)e.OldValue, (PwPasswordBoxController)e.NewValue);

		private void OnAllowCopyPasswordChanged(bool oldValue, bool newValue)
		{
			if (passwordBox != null)
			{
				if (newValue)
					passwordBox.CommandBindings.Add(copyCommandBinding);
				else
					passwordBox.CommandBindings.Remove(copyCommandBinding);
			}
		} // prop OnAllowCopyPasswordChanged

		private static void OnAllowCopyPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PwPasswordBox)d).OnAllowCopyPasswordChanged((bool)e.OldValue, (bool)e.NewValue);

		public PwPasswordBoxController Controller
		{
			get => (PwPasswordBoxController)GetValue(ControllerProperty);
			set => SetValue(ControllerProperty, value);
		} // prop Controller

		public bool AllowCopyPassword
		{
			get => (bool)GetValue(AllowCopyPasswordProperty);
			set => SetValue(AllowCopyPasswordProperty, value);
		} // prop AllowCopyPassword

		public bool IsReadOnly
		{
			get => (bool)GetValue(IsReadOnlyProperty);
			set => SetValue(IsReadOnlyProperty, value);
		} // prop IsReadOnly

		public bool HasPassword => (bool)GetValue(HasPasswordProperty);

		public event RoutedEventHandler PasswordChanged
		{
			add => AddHandler(PasswordChangedEvent, value);
			remove => RemoveHandler(PasswordChangedEvent, value);
		} // event PasswordChanged

		private static MethodInfo setSecurePasswordMethodInfo;

		static PwPasswordBox()
		{
			var type = typeof(PasswordBox);
			setSecurePasswordMethodInfo = type.GetMethod("SetSecurePassword", BindingFlags.NonPublic | BindingFlags.Instance);
			if (setSecurePasswordMethodInfo == null)
				throw new ArgumentNullException(nameof(setSecurePasswordMethodInfo));

			DefaultStyleKeyProperty.OverrideMetadata(typeof(PwPasswordBox), new FrameworkPropertyMetadata(typeof(PwPasswordBox)));
		} // sctor
	} // class PwPasswordBox

	#endregion
}
