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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Win32;
using Neo.PerfectWorking.Data;
using Neo.PerfectWorking.Stuff;
using Neo.PerfectWorking.Win32;
using static Neo.PerfectWorking.Win32.NativeMethods;

namespace Neo.PerfectWorking.UI
{
	/// <summary>Shell for all tasks.</summary>
	public partial class App : Application, IPwShellUI, IPwShellWpf
	{
		#region -- class PwRegisteredHotKey -------------------------------------------

		/// <summary>HotKey registration.</summary>
		private sealed class PwRegisteredHotKey
		{
			private readonly HwndSource hwnd;
			private readonly List<IPwHotKey> hotkeyBinds = new List<IPwHotKey>();

			private readonly PwKey key;
			private readonly int hotKeyId;
			private bool isRegistered = false;

			#region -- Ctor/Dtor ------------------------------------------------------

			public PwRegisteredHotKey(HwndSource hwnd, PwKey key, int hotKeyId)
			{
				this.hwnd = hwnd ?? throw new ArgumentNullException(nameof(mainWindow));
				this.key = key;
				this.hotKeyId = hotKeyId;
			} // ctor

			public void Clear()
				=> UnRegister();

			public void Add(IPwHotKey hotKey)
			{
				if (hotKey.Key != Key)
					throw new ArgumentException(nameof(hotKey));

				if (hotkeyBinds.Count == 0)
				{
					hotkeyBinds.Add(hotKey);
					Register();
				}
				else
				{
					if (hotkeyBinds[0] is IPwUIHotKey)
					{
						if (!(hotKey is IPwUIHotKey))
							throw new ArgumentException("NoneUI hotkey can not registered with UI hotkeys.");
						hotkeyBinds.Add(hotKey);
					}
					else
						throw new ArgumentException("NoneUI hotkey is already registered.", nameof(hotKey));
				}
			} // proc Add

			public bool Remove(IPwHotKey hotKey)
			{
				if (hotkeyBinds.Remove(hotKey))
				{
					if (hotkeyBinds.Count == 0)
						UnRegister();
					return true;
				}
				else
					return false;
			} // proc Remove

			public bool Contains(IPwHotKey hotkey)
				=> hotkeyBinds.Contains(hotkey);

			private void Register()
			{
				// unregister current hotkey
				UnRegister();

				if (!RegisterHotKey(hwnd.Handle, HotKeyId, (uint)Key.Modifiers, Key.VirtualKey))
				{
					var e = new Win32Exception();
					Log.Default.RegisterHotKeyFailed(Key.ToString(), HotKeyId, e.NativeErrorCode);
					if (e.NativeErrorCode != 1409) // Failed to register
						throw e;
				}
				else
				{
					isRegistered = true;
					Log.Default.RegisterHotKeySuccess(Key.ToString(), HotKeyId);
				}
			} // proc Register

			private void UnRegister()
			{
				// unregister
				var r = UnregisterHotKey(hwnd.Handle, HotKeyId);
				if (isRegistered)
					Debug.Print($"Unregister HotKey '{Key}' with Id {HotKeyId}: {(r ? "Successful" : "Failed")}.");
				isRegistered = false;
			} // proc UnRegister

			#endregion

			#region -- Execute --------------------------------------------------------

			public bool Invoke()
			{
				if (hotkeyBinds.Count == 1)
					ExecuteDirect(hotkeyBinds[0]); // todo: Execute Command with event?
				return true;
			} // proc Invoke

			private void ExecuteDirect(ICommand command)
			{
				if (command.CanExecute(null))
					command.Execute(null);
			} // proc ExecuteDirect

			#endregion

			/// <summary>Key that is registered.</summary>
			public PwKey Key => key;
			/// <summary>Hotkey id for registration.</summary>
			public int HotKeyId => hotKeyId;
			/// <summary>IUs this hot key registered.</summary>
			public bool IsRegistered => isRegistered;

			public static int GetHotKeyId(PwKey key)
			{
				//if (hotKeyId < 0 || hotKeyId > 0xBFFF)
				//	throw new ArgumentOutOfRangeException(nameof(hotKeyId), hotKeyId, "HotKeyId must be between 0 and 0xBFFF.");

				// build a unique id (4 bit and 10 bit)
				if (key.VirtualKey > 1023)
					throw new ArgumentOutOfRangeException(nameof(key), "VirtualKey is more than 10bit.");

				return (int)key.Modifiers << 10 | key.VirtualKey;
			} // func GetHotKeyId	
		} // class PwRegisteredHotKey

		#endregion

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

		private IPwCollection<IPwHotKey> hotkeys;
		private readonly Dictionary<int, PwRegisteredHotKey> registeredHotkeys = new Dictionary<int, PwRegisteredHotKey>();
		private IPwCollection<EventSource> eventSources;
		private MenuItem logEventMenuItem;

		#region -- Ctor ---------------------------------------------------------------

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
			
			ApplicationRemoteDirectory = GetDirectory(Environment.SpecialFolder.ApplicationData);
			ApplicationLocalDirectory = GetDirectory(Environment.SpecialFolder.LocalApplicationData);
			if (!ApplicationRemoteDirectory.Exists)
				ApplicationRemoteDirectory.Create();
			if (!ApplicationLocalDirectory.Exists)
				ApplicationLocalDirectory.Create();
		} // ctor

		#endregion

		#region -- Create Native Window -----------------------------------------------

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
				case WM_HOTKEY:
					if(InvokeHotKey(wParam.ToInt32()))
					{
						handled = true;
						return IntPtr.Zero;
					}
					break;
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

		#region -- Idle service -------------------------------------------------------

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
					stopIdle = stopIdle && t.OnIdle(timeSinceRestart);
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

		#region -- Notify Icon --------------------------------------------------------

#pragma warning disable IDE1006 // Naming Styles
		private const int WM_TASKBARNOTIFY = 0x8010;
		private const int WM_HOTKEY = 0x0312;
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
					var iconId = new IntPtr(ProcsPW.IsWin10 ? 103 : 101);
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
			//var iconId = lParam.ToInt32() >> 16;
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
					ShowMainWindow(hwnd);
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

		public void ShowMainWindow()
			=> ShowMainWindow(hwnd.Handle);

		private void ShowMainWindow(IntPtr hwnd)
		{
			dashBoardWindow.BeginHide(true);
			SetForegroundWindow(hwnd);
			mainWindow.Show();
			mainWindow.Activate();
		} // proc ShowMainWindow

		private void RemoveIcon()
		{
			CreateNotifyIconData(out var nid);
			Shell_NotifyIcon(NotifyIconMessage.Delete, ref nid);
		} // proc RemoveIcon

		#endregion

		#region -- OnStartup, OnExit --------------------------------------------------

		protected override void OnStartup(StartupEventArgs e)
		{
			contextMenu = (ContextMenu)Resources["applicationMenu"];

			try
			{
				// parse arguments
				ParseArguments(e.Args);

				if (String.IsNullOrEmpty(configurationFile))
					throw new ArgumentNullException("No configuration file.");

				// set language
				FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.Name)));

				// create global environment
				global = new PwGlobal(this, configurationFile);

				// create windows
				dashBoardWindow = new DashBoardWindow(global);
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

				AssignCommandTarget(contextMenu, mainWindow);

				// create hotkey list
				hotkeys = global.RegisterCollection<IPwHotKey>(global);

				// Events
				logEventMenuItem = contextMenu.FindMenuItem("logMainMenuItem");
				eventSources = global.RegisterCollection<EventSource>(global);
				eventSources.CollectionChanged += EventSources_CollectionChanged;
				EventSources_CollectionChanged(eventSources, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

				// read configuration
				global.RefreshConfiguration();

				// Start idle implementation
				idleTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.ApplicationIdle, (sender, _e) => OnIdle(), Dispatcher);
				InputManager.Current.PreProcessInput += (sender, _e) => RestartIdleTimer(_e);

				// create the event sink, for the win32 integration
				CreateNativeWindow();
				// register icon
				UpdateNotifyIcon();

				// register hotkeys
				hotkeys.CollectionChanged += Hotkeys_CollectionChanged;
				Hotkeys_CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
			catch (Exception ex)
			{
				ShowException(null, ex);
				Shutdown();
			}
		} // proc OnStartup

		private void AssignCommandTarget(ItemsControl items, IInputElement commandTarget)
		{
			if (items is MenuItem mnu)
				mnu.CommandTarget = commandTarget;

			foreach (var m in items.Items.OfType<ItemsControl>())
				AssignCommandTarget(m, commandTarget);
		} // proc AssignCommandTarget

		protected override void OnExit(ExitEventArgs e)
		{
			base.OnExit(e);
			dashBoardWindow?.Close();
			if (hwnd != null)
			{
				RemoveIcon();
				DestroyNativeWindow();
			}
			global?.Dispose();
		} // proc OnExit

		public void ExitApplication()
			=> Shutdown();
		
		#endregion

		#region -- HotKey List --------------------------------------------------------

		private PwRegisteredHotKey AddHotKey(IPwHotKey hotkey)
		{
			// add hot changed
			if (hotkey is INotifyPropertyChanged npc)
				npc.PropertyChanged += Npc_PropertyChanged;

			// check for the key property
			var key = hotkey.Key;
			if (key == PwKey.None)
				return null;

			// ensure id
			var hotKeyId = PwRegisteredHotKey.GetHotKeyId(hotkey.Key);
			if(!registeredHotkeys.TryGetValue(hotKeyId, out var registered))
			{
				registeredHotkeys[hotKeyId] =
					registered = new PwRegisteredHotKey(hwnd, key, hotKeyId);
				
			}
			// register hotkey
			registered.Add(hotkey);
			return registered;
		} // proc AddHotKey

		private void RemoveHotKey(IPwHotKey hotkey)
		{
			// remove hotkey changed
			if (hotkey is INotifyPropertyChanged npc)
				npc.PropertyChanged -= Npc_PropertyChanged;

			// try remove hotkey by key
			var key = hotkey.Key;
			if (key != PwKey.None)
			{
			
				var hotKeyId = PwRegisteredHotKey.GetHotKeyId(key);
				if (registeredHotkeys.TryGetValue(hotKeyId, out var registered)
					&& registered.Remove(hotkey))
					return;
			}

			// try remove on all
			foreach (var c in registeredHotkeys.Values)
			{
				if (c.Remove(hotkey))
					return;
			}
		} // proc RemoveHotKey

		private void Hotkeys_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			var failedMessage = new StringBuilder();

			void CheckResult(PwRegisteredHotKey r)
			{
				if (r != null && !r.IsRegistered)
					failedMessage.Append(String.Format("Failed to register: {0}", r.Key));
			}

			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Reset:
					// clear all hotkeys
					foreach (var c in registeredHotkeys.Values)
						c.Clear();
					registeredHotkeys.Clear();

					// readd all hotkeys
					if (hwnd != null)
					{
						foreach (var c in hotkeys)
							CheckResult(AddHotKey(c));
					}
					break;
				case NotifyCollectionChangedAction.Add:
					foreach (var c in e.NewItems)
						CheckResult(AddHotKey((IPwHotKey)c));
					break;
				case NotifyCollectionChangedAction.Remove:
					foreach (var c in e.OldItems)
						RemoveHotKey((IPwHotKey)c);
					break;
				case NotifyCollectionChangedAction.Replace:
					foreach (var c in e.OldItems)
						RemoveHotKey((IPwHotKey)c);
					foreach (var c in e.NewItems)
						CheckResult(AddHotKey((IPwHotKey)c));
					break;
				default:
					throw new NotImplementedException();
			}

			if (failedMessage.Length > 0)
				MsgBox("HotKey registration failed!" + Environment.NewLine + failedMessage.ToString(), "Warning", "W");
		} // event Hotkeys_CollectionChanged

		private void Npc_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(IPwHotKey.Key))
			{
				RemoveHotKey((IPwHotKey)sender);
				AddHotKey((IPwHotKey)sender);
			}
		} // event  Npc_PropertyChanged

		private bool InvokeHotKey(int hotKeyId)
			=> registeredHotkeys.TryGetValue(hotKeyId, out var registered) ? registered.Invoke() : false;

		#endregion

		#region -- EventSource - Management -------------------------------------------

		private int FindEventSourceItem(EventSource source)
		{
			var i = 0;
			while (i < logEventMenuItem.Items.Count)
			{
				if (logEventMenuItem.Items[i] is Separator)
					break;
				else if (logEventMenuItem.Items[i] is MenuItem m && m.CommandParameter == source)
					return i;
				i++;
			}
			return ~i;
		} // func FindEventSourceItem

		private static string CleanEventLogName(string name)
		{
			const string prefix = "PerfectWorking-";
			const string suffix = "-Log";

			if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				name = name.Substring(prefix.Length);
			if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
				name = name.Substring(0, name.Length - suffix.Length);

			return name;
		} // func CleanEventLogName

		private void AddEventSource(EventSource source)
		{
			var index = FindEventSourceItem(source);
			if (index < 0)
			{
				logEventMenuItem.Items.Insert(~index, new MenuItem
				{
					Header = CleanEventLogName(source.Name),
					Command = AppCommands.OpenEventLog,
					CommandParameter = source,
					CommandTarget = mainWindow
				});
			}
		} // proc AddEventSource

		private void RemoveEventSource(EventSource source)
		{
			var index = FindEventSourceItem(source);
			if (index >= 0)
				logEventMenuItem.Items.RemoveAt(index);
		} // proc RemoveEventSource

		private void ClearEventSources()
		{
			while (logEventMenuItem.Items.Count > 0 && !(logEventMenuItem.Items[0] is Separator))
				logEventMenuItem.Items.RemoveAt(0);
		} // proc ClearEventSources

		private void EventSources_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					foreach (var c in e.NewItems.OfType<EventSource>())
						AddEventSource(c);
					break;
				case NotifyCollectionChangedAction.Remove:
					foreach (var c in e.OldItems.OfType<EventSource>())
						RemoveEventSource(c);
					break;
				case NotifyCollectionChangedAction.Reset:
					ClearEventSources();
					foreach (var c in eventSources)
						AddEventSource(c);
					break;
			}
		} // event EventSources_CollectionChanged

		private static string CompleteEventSourceName(EventSource source)
			=> source.Name + "/Operational";

		public void OpenEventLog(EventSource source)
		{
			var eventViewer = Path.Combine(Environment.SystemDirectory, "eventvwr.exe");
			Process.Start(eventViewer, $"/c:\"{CompleteEventSourceName(source)}\"");
		} // proc OpenEventLog

		private IEnumerable<(string name, string manifestFileName, string manifestResourceFileName)> EnumerateEventSources()
		{
			foreach (var source in global.EnumerateObjects<EventSource>())
			{
				var manifestFileName = global.ResolveFile(source.GetType().Assembly.GetName().Name + "." + source.Name + ".etwManifest.man", false);
				if (manifestFileName == null)
					break;

				var manifestResourceFileName = Path.ChangeExtension(manifestFileName, ".dll");
				yield return (CompleteEventSourceName(source), manifestFileName, manifestResourceFileName);
			}
		} // func EnumerateEventSources

		private string CreateLogScript(bool install)
		{
			var sb = new StringBuilder("@ECHO OFF");
			sb.AppendLine();
			foreach (var (name, manifestFileName, manifestResourceFileName) in EnumerateEventSources())
			{
				sb.AppendLine("ECHO " + name);
				sb.AppendLine($"WEVTUTIL um \"{manifestFileName}\"");
				if (install)
					sb.AppendLine($"WEVTUTIL im \"{manifestFileName}\" /rf:\"{manifestResourceFileName}\" /mf:\"{manifestResourceFileName}\"");
			}
			sb.AppendLine("PAUSE");
			return sb.ToString();
		} // func CreateLogScript

		private static void RunBatchScriptUAC(string script)
		{
			// write script
			var tempBatch = Path.GetTempPath() + Guid.NewGuid().ToString("N") + ".bat";
			File.WriteAllText(tempBatch, script + Environment.NewLine + "DEL \"" + tempBatch + "\"");

			// run script
			var psi = new ProcessStartInfo
			{
				FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
				Arguments = "/c \"" + tempBatch + "\"",
				Verb = "runas"
			};
			Process.Start(psi);
		} // proc RunBatchScriptUAC

		public void InstallEventLogs()
			=> RunBatchScriptUAC(CreateLogScript(true));

		public void DeinstallEventLogs()
			=> RunBatchScriptUAC(CreateLogScript(false));

		public void ExportEventLogs()
		{
			var dlg = new SaveFileDialog
			{
				Title = "Exportiere Log-Dateien",
				DefaultExt = "zip",
				Filter = "Zip-Dateien (*.zip)|*.zip"
			};
			if (dlg.ShowDialog() == true)
			{
				var exportedLogs = new List<(string, string)>();
				try
				{
					foreach (var source in global.EnumerateObjects<EventSource>())
					{
						var tempExportFileName = Path.GetTempFileName();
						exportedLogs.Add((tempExportFileName, source.Name + ".evtx"));
						File.Delete(tempExportFileName);
						EventLogSession.GlobalSession.ExportLog(CompleteEventSourceName(source), PathType.LogName, null, tempExportFileName);
					}

					using (var dst = new FileStream(dlg.FileName, FileMode.Create))
					using (var zip = new ZipArchive(dst, ZipArchiveMode.Create))
					{
						foreach (var cur in exportedLogs)
						{
							var entry = zip.CreateEntry(Path.GetFileName(cur.Item2));
							using (var dst2 = entry.Open())
							using (var src2 = new FileStream(cur.Item1, FileMode.Open))
								src2.CopyTo(dst2);
						}
					}

					global.UI.ShowNotification("Logs exportiert.");
				}
				catch(Exception e)
				{
					global.UI.ShowException(e);
				}
				finally
				{
					foreach (var cur in exportedLogs)
					{
						try { File.Delete(cur.Item1); }
						catch { }
					}
				}
			}
		} // proc ExportEventLogs

		#endregion

		#region -- Invoke -------------------------------------------------------------

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

		#region -- ShowException ------------------------------------------------------

		private void ShowException(string text, Exception e)
		{
			var sb = new StringBuilder();
			if (!String.IsNullOrEmpty(text))
				sb.AppendLine(text).AppendLine();

			sb.Append(e.ToString());

			Log.Default.Exception(text, e.ToString());

			MessageBox.Show(sb.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
		} // proc ShowException

		public async Task ShowExceptionAsync(string text, Exception e)
			=> await Dispatcher.InvokeAsync(() => ShowException(text, e));

		void IPwShellUI.ShowException(string text, Exception e)
		{
			if (Dispatcher.CheckAccess())
			{
				using (mainWindow.StayOpenBegin())
					ShowException(text, e);
			}
			else
				ShowExceptionAsync(text, e).Wait();
		} // proc IPwShellUI.ShowException

		#endregion

		#region -- MsgBox -------------------------------------------------------------

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
				case MessageBoxResult.None:
				default:
					return null;
			}
		} // func GetMsgBoxResultFromResult

		private string MsgBox(string text, string caption, object icon, object buttons = null, object result = null)
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
			{
				using (mainWindow.StayOpenBegin())
					return MsgBox(text, caption, icon, buttons, result);
			}
			else
				return MsgBoxAsync(text, caption, icon, buttons, result).Result;
		} // func IPwShellUI.MsgBox

		#endregion

		#region -- ShowNotification ---------------------------------------------------

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
			+ " (Debug)"
#endif
			;

		public DirectoryInfo ApplicationRemoteDirectory { get; }

		public DirectoryInfo ApplicationLocalDirectory { get; }

		internal DashBoardWindow DashBoardWindow => dashBoardWindow;

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
	} // class App

	public static class AppCommands
	{
		public static readonly RoutedUICommand OpenEventLog = new RoutedUICommand("Open", "OpenEventLog", typeof(AppCommands));
		public static readonly RoutedUICommand InstallEventLogs = new RoutedUICommand("Install", "InstallEventLog", typeof(AppCommands));
		public static readonly RoutedUICommand DeinstallEventLogs = new RoutedUICommand("Deinstall", "UninstallEventLog", typeof(AppCommands));
		public static readonly RoutedUICommand ExportEventLogs = new RoutedUICommand("Export", "ExportEventLog", typeof(AppCommands));
	} // class AppCommands
}
