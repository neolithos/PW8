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
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Neo.PerfectWorking.Data;
using Neo.PerfectWorking.Win32;
using static Neo.PerfectWorking.Win32.NativeMethods;

namespace Neo.PerfectWorking.UI
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application, IPwShellUI, IPwShellWpf
	{
		private HwndSource hwnd = null;
		private IntPtr notificationIcon = IntPtr.Zero;

		private PwGlobal global = null;

		private DashBoardWindow dashBoardWindow;
		private MainWindow mainWindow;
		private NotificationWindow notificationWindow;
		private ContextMenu contextMenu;

		private int restartTime = -1;
		private DispatcherTimer idleTimer;
		private readonly List<WeakReference<IPwIdleAction>> idleActions = new List<WeakReference<IPwIdleAction>>();

		private readonly DirectoryInfo applicationRemoteDirectory;
		private readonly DirectoryInfo applicationLocalDirectory;

		#region -- Ctor -----------------------------------------------------------------

		public App()
		{
			// create the directories
			DirectoryInfo GetDirectory(Environment.SpecialFolder folder)
				=> new DirectoryInfo(Path.GetFullPath(Path.Combine(Environment.GetFolderPath(folder), "Perfect Working",
#if DEBUG
				"8.0dbg"
#else
				"8.0"
#endif
				)));
			applicationRemoteDirectory = GetDirectory(Environment.SpecialFolder.ApplicationData);
			applicationLocalDirectory = GetDirectory(Environment.SpecialFolder.LocalApplicationData);
			if (!applicationRemoteDirectory.Exists)
				applicationRemoteDirectory.Create();
			if (!applicationLocalDirectory.Exists)
				applicationLocalDirectory.Create();
		} // ctor

		#endregion

		#region -- Create Native Window -------------------------------------------------

		private uint wmTaskbarCreated = 0;

		private void CreateNativeWindow()
		{
			hwnd = new HwndSource(
				0,
				unchecked((int)(WS_POPUP)),
				0,
				0, 0, 0, 0,
				"Perfect Working Sink",
				IntPtr.Zero,
				false
			);
			hwnd.AddHook(NativeWndProc);

			wmTaskbarCreated = RegisterWindowMessage("TaskbarCreated");
		} // proc CreateNativeWindow

		private IntPtr NativeWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			switch (msg)
			{
				case WM_TASKBARNOTIFY:
					return WmTaskbarNotify(hwnd, msg, wParam, lParam, ref handled);
				default:
					if (msg == wmTaskbarCreated)
					{
						UpdateNotifyIcon(true);
					}
					break;
			}
			handled = false;
			return IntPtr.Zero;
		} // func NativeWndProc

		private void DestroyNativeWindow()
		{
			if (hwnd != null)
			{
				hwnd.Dispose();
				hwnd = null;
			}
		} // proc DestroyNativeWindow

		#endregion

		#region -- Idle service ---------------------------------------------------------

		private int IndexOfIdleAction(IPwIdleAction idleAction)
		{
			for (var i = 0; i < idleActions.Count; i++)
			{
				if (idleActions[i].TryGetTarget(out var t) && t == idleAction)
					return i;
			}
			return -1;
		} // func IndexOfIdleAction

		public IPwIdleAction AddIdleAction(IPwIdleAction idleAction)
		{
			if (IndexOfIdleAction(idleAction) == -1)
				idleActions.Add(new WeakReference<IPwIdleAction>(idleAction));
			return idleAction;
		} // proc AddIdleAction

		public void RemoveIdleAction(IPwIdleAction idleAction)
		{
			if (idleAction == null)
				return;

			var i = IndexOfIdleAction(idleAction);
			if (i >= 0)
				idleActions.RemoveAt(i);
		} // proc RemoveIdleAction

		private void OnIdle()
		{
			var stopIdle = true;
			var timeSinceRestart = unchecked(Environment.TickCount - restartTime);
			for (var i = idleActions.Count - 1; i >= 0; i--)
			{
				if (idleActions[i].TryGetTarget(out var t))
					stopIdle = stopIdle && !t.OnIdle(timeSinceRestart);
				else
					idleActions.RemoveAt(i);
			}

			// increase the steps
			if (stopIdle)
				idleTimer.Stop();
			else
				idleTimer.Interval = TimeSpan.FromMilliseconds(100);
		} // proc OnIdle

		private void RestartIdleTimer(PreProcessInputEventArgs e)
		{
			if (idleActions.Count > 0)
			{
				var inputEvent = e.StagingItem.Input;
				if (inputEvent is MouseEventArgs ||
					inputEvent is KeyboardEventArgs)
				{
					restartTime = Environment.TickCount;
					idleTimer.Stop();
					idleTimer.Start();
				}
			}
		} // proc RestartIdleTimer

		#endregion

		#region -- Notify Icon ----------------------------------------------------------

#pragma warning disable IDE1006 // Naming Styles
		private const int WM_TASKBARNOTIFY = 0x8010;
#pragma warning restore IDE1006 // Naming Styles
		private static readonly Guid guidTaskbar =
#if DEBUG
			new Guid("5f960f02-59e7-4af2-a355-ccc68150fa52");
#else
			new Guid("2eba4923-20d7-426c-a022-e7e65ba7b6c0");
#endif

		private bool notifyIconRegistered = false;

		private void CreateNotifyIconData(out NotifyIconData nid)
		{
			nid = new NotifyIconData()
			{
				cbSize = (uint)Marshal.SizeOf(typeof(NotifyIconData)),
				hWnd = hwnd.Handle,
				uID = 1,
				guidItem = guidTaskbar,
				uFlags = NotifyIconFlags.Guid
			};
		} // proc CreateNotifyIconData

		private void UpdateNotifyIcon(bool force = false)
		{

			CreateNotifyIconData(out var nid);

			if (!notifyIconRegistered || force)
			{
				nid.uFlags |= NotifyIconFlags.Icon | NotifyIconFlags.Message | NotifyIconFlags.Tip;

				if (notificationIcon == IntPtr.Zero)
				{
#if DEBUG
					var iconId = new IntPtr(102);
#else
					var iconId = new IntPtr(Procs.IsWin10 ? 103 : 101);
#endif
					if (LoadIconMetric(GetModuleHandle(null), iconId, 0, out notificationIcon) != 0)
						throw new Win32Exception();
				}

				nid.hIcon = notificationIcon;
				nid.uCallbackMessage = WM_TASKBARNOTIFY;
				nid.szTip = Title;

				if (!Shell_NotifyIcon(NotifyIconMessage.Add, ref nid))
				{
					if (!Shell_NotifyIcon(NotifyIconMessage.Delete, ref nid))
						throw new Win32Exception();
					if (!Shell_NotifyIcon(NotifyIconMessage.Add, ref nid))
						throw new Win32Exception();
				}

				nid.uFlags = NotifyIconFlags.Guid;
				nid.uTimeoutOrVersion = 4;
				if (!Shell_NotifyIcon(NotifyIconMessage.SetVersion, ref nid))
					throw new Win32Exception();

				notifyIconRegistered = true;
			}
		} // proc UpdateNotifyIcon

		private IntPtr WmTaskbarNotify(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			var iconMsg = lParam.ToInt32() & 0xFFFF;
			var iconId = lParam.ToInt32() >> 16;
			var x = wParam.ToInt32() & 0xFFFF;
			var y = wParam.ToInt32() >> 16;

			switch (iconMsg)
			{
				case WM_CONTEXTMENU:
				case WM_RBUTTONUP:

					dashBoardWindow.BeginHide(true);

					var pt = this.hwnd.CompositionTarget.TransformFromDevice.Transform(new Point(x, y));
					contextMenu.HorizontalOffset = pt.X;
					contextMenu.VerticalOffset = pt.Y;
					contextMenu.IsOpen = true;

					SetForegroundWindow(hwnd);

					goto case 0;

				case NIM_SELECT:
				case NIM_KEYSELECT:
					dashBoardWindow.BeginHide(true);
					SetForegroundWindow(hwnd);
					mainWindow.Show();
					mainWindow.Activate();
					goto case 0;

				case WM_MOUSEMOVE:
					if (!contextMenu.IsOpen && !mainWindow.IsVisible)
						dashBoardWindow.BeginShow(x, y);
					goto case 0;

				case NIM_POPUPOPEN:
				case NIM_POPUPCLOSE:
				case 0:
					handled = true;
					return IntPtr.Zero;

				default:
					handled = false;
					return IntPtr.Zero;
			}

		} // func WmTaskbarNotify

		private void RemoveIcon()
		{
			CreateNotifyIconData(out var nid);
			Shell_NotifyIcon(NotifyIconMessage.Delete, ref nid);
		} // proc RemoveIcon

		#endregion

		#region -- OnStartup, OnExit ----------------------------------------------------

		protected override void OnStartup(StartupEventArgs e)
		{
			contextMenu = (ContextMenu)Resources["applicationMenu"];

			try
			{
				// parse arguments
				ParseArguments(e.Args);

				if (String.IsNullOrEmpty(configurationFile))
					throw new ArgumentNullException("No configuration file.");

				global = new PwGlobal(this, configurationFile);

				// create windows
				dashBoardWindow = new DashBoardWindow();
				mainWindow = new MainWindow(global);
				notificationWindow = new NotificationWindow();
				
				SystemParameters.StaticPropertyChanged += (sender, e2) =>
				{
					switch (e2.PropertyName)
					{
						case nameof(SystemParameters.WorkArea):
							dashBoardWindow.RecalcPosition();
							mainWindow.RecalcPosition();
							notificationWindow.RecalcPosition();
							break;
					}
				};

				// read configuration
				global.RefreshConfiguration();

				// Start idle implementation
				this.idleTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.ApplicationIdle, (sender, _e) => OnIdle(), Dispatcher);
				InputManager.Current.PreProcessInput += (sender, _e) => RestartIdleTimer(_e);

				// create the event sink, for the win32 integration
				CreateNativeWindow();
				// register icon
				UpdateNotifyIcon();
			}
			catch (Exception ex)
			{
				ShowException(null, ex);
			}
		} // proc OnStartup

		protected override void OnExit(ExitEventArgs e)
		{
			base.OnExit(e);
			dashBoardWindow.Close();
			RemoveIcon();
			DestroyNativeWindow();
			global.Dispose();
		} // proc OnExit

		private void ApplicationClose(object sender, RoutedEventArgs e)
			=> ExitApplication();

		public void ExitApplication()
		{
			Shutdown();
		} // proc ExitApplication

		#endregion

		#region -- Invoke ---------------------------------------------------------------

		public Task InvokeAsync(Action action)
		{
			if (Dispatcher.CheckAccess())
			{
				action();
				return Task.CompletedTask;
			}
			else
				return Dispatcher.InvokeAsync(action).Task;
		} //  proc InvokeAsync

		public Task<T> InvokeAsync<T>(Func<T> action)
		{
			if (Dispatcher.CheckAccess())
				return Task.FromResult(action());
			else
				return Dispatcher.InvokeAsync(action).Task;
		} //  proc InvokeAsync

		public void BeginInvoke(Action action)
			=> Dispatcher.InvokeAsync(action);

		#endregion

		#region -- ShowException --------------------------------------------------------

		private void ShowException(string text, Exception e)
			=> MessageBox.Show(text ?? e?.ToString() ?? "Unknown error.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

		public async Task ShowExceptionAsync(string text, Exception e)
			=> await Dispatcher.InvokeAsync(() => ShowException(text, e));

		void IPwShellUI.ShowException(string text, Exception e)
		{
			if (Dispatcher.CheckAccess())
				ShowException(text, e);
			else
				ShowExceptionAsync(text, e).Wait();
		} // proc IPwShellUI.ShowException

		#endregion

		#region -- MsgBox ---------------------------------------------------------------

		private static MessageBoxImage GetMsgBoxImage(object icon)
		{
			if (icon is MessageBoxImage wpfImage)
				return wpfImage;
			else if (icon is string img)
			{
				switch (img)
				{
					case "i":
					case "info":
						return MessageBoxImage.Information;
					case "e":
					case "error":
						return MessageBoxImage.Error;
					case "w":
					case "warn":
						return MessageBoxImage.Warning;
					case "a":
					case "q":
					case "ask":
						return MessageBoxImage.Question;
					default:
						return MessageBoxImage.Information;
				}
			}
			else
				return MessageBoxImage.Information;
		} // func GetMsgBoxImage

		private static MessageBoxButton GetMsgBoxButtons(object buttons, MessageBoxImage image)
		{
			if (buttons is MessageBoxButton wpfButton)
				return wpfButton;
			else if (buttons is string btn)
			{
				switch (btn)
				{
					case "ok":
						return MessageBoxButton.OK;
					case "oc":
					case "okcancel":
						return MessageBoxButton.OKCancel;
					case "yn":
					case "yesno":
						return MessageBoxButton.YesNo;
					case "ync":
					case "yesnocancel":
						return MessageBoxButton.YesNoCancel;
					default:
						return GetMsgBoxButtons(null, image);
				}
			}
			else
				return image == MessageBoxImage.Question ? MessageBoxButton.YesNo : MessageBoxButton.OK;
		} // func GetMsgBoxButtons

		private static string GetMsgBoxCaptionFromImage(MessageBoxImage icon)
		{
			switch (icon)
			{
				case MessageBoxImage.Error:
					return "Fehler";
				case MessageBoxImage.Question:
					return "Frage";
				case MessageBoxImage.Warning:
					return "Warnung";
				case MessageBoxImage.Information:
				default:
					return "Information";
			}
		} // func GetMsgBoxCaptionFromImage

		private static MessageBoxResult GetMsgBoxResultFromObject(object result)
		{
			if (result is MessageBoxResult wpfResult)
				return wpfResult;
			else if (result is string r)
			{
				switch (r)
				{
					case "o":
					case "ok":
						return MessageBoxResult.OK;
					case "y":
					case "yes":
						return MessageBoxResult.Yes;
					case "n":
					case "no":
						return MessageBoxResult.No;
					case "c":
					case "cancel":
						return MessageBoxResult.Cancel;
					default:
						return MessageBoxResult.None;
				}
			}
			else if (result is int i)
			{
				if (i < 0)
					return MessageBoxResult.Cancel;
				else if (i > 0)
					return MessageBoxResult.OK;
				else
					return MessageBoxResult.Yes;
			}
			else
				return MessageBoxResult.None;
		} // func GetMsgBoxResultFromObject

		private static string GetMsgBoxResultFromResult(MessageBoxResult result)
		{
			switch (result)
			{
				case MessageBoxResult.Yes:
					return "y";
				case MessageBoxResult.No:
					return "n";
				case MessageBoxResult.Cancel:
					return "c";
				case MessageBoxResult.OK:
					return "o";
				default:
					return null;
			}
		} // func GetMsgBoxResultFromResult

		private string MsgBox(string text, string caption, object icon, object buttons, object result)
		{
			var image = GetMsgBoxImage(icon);
			var button = GetMsgBoxButtons(buttons, image);
			return GetMsgBoxResultFromResult(MessageBox.Show(text, caption ?? GetMsgBoxCaptionFromImage(image), button, image, GetMsgBoxResultFromObject(button)));
		} // func MsgBox

		public async Task<string> MsgBoxAsync(string text, string caption, object icon, object buttons, object result)
			=> await Dispatcher.InvokeAsync(() => MsgBox(text, caption, icon, buttons, result));

		string IPwShellUI.MsgBox(string text, string caption, object icon, object buttons, object result)
		{
			if (Dispatcher.CheckAccess())
				return MsgBox(text, caption, icon, buttons, result);
			else
				return MsgBoxAsync(text, caption, icon, buttons, result).Result;
		} // func IPwShellUI.MsgBox

		#endregion

		#region -- ShowNotification -----------------------------------------------------

		public void ShowNotification(object message, object image = null)
		{
			if (Dispatcher.CheckAccess())
				notificationWindow.Show(message, image);
			else
				Dispatcher.BeginInvoke(new Action<object, object>(notificationWindow.Show), message, image);
		} // func ShowNotification

		#endregion

		public static string Title => "Perfect Working 8"
#if DEBUG
			+ " (Debug)";
#endif

		public DirectoryInfo ApplicationRemoteDirectory => applicationRemoteDirectory;
		public DirectoryInfo ApplicationLocalDirectory => applicationLocalDirectory;

		// -- Static part -----------------------------------------------------

		private const string configurationFileArgument = "--config:";

		private static string configurationFile = String.Empty;

		private static void ParseArguments(IEnumerable<string> args)
		{
			foreach (var a in args)
			{
				if (a.StartsWith(configurationFileArgument, StringComparison.OrdinalIgnoreCase))
					configurationFile = a.Substring(configurationFileArgument.Length);
			}
		} // proc ParseArguments
	}
}
