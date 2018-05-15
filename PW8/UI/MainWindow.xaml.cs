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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32;
using Neo.IronLua;
using Neo.PerfectWorking.Data;
using Neo.PerfectWorking.Stuff;
using Neo.PerfectWorking.Win32;
using static Neo.PerfectWorking.Win32.NativeMethods;

namespace Neo.PerfectWorking.UI
{
	public partial class MainWindow : Window
	{
		#region -- class PaneItem -----------------------------------------------------

		public class PaneItem : INotifyPropertyChanged
		{
			public event PropertyChangedEventHandler PropertyChanged;

			private readonly MainWindowModel model;
			private readonly IPwWindowPane pane;

			private readonly PropertyChangedEventHandler panePropertyChanged;
			private readonly EventHandler collectionViewCurrentChanged;

			public PaneItem(MainWindowModel model, IPwWindowPane pane)
			{
				this.model = model;
				this.pane = pane;

				collectionViewCurrentChanged = (sender, e) => OnPropertyChanged(nameof(IsChecked));
				panePropertyChanged = PanePropertyChanged;

				model.Panes.CurrentChanged += collectionViewCurrentChanged;
				var n = (pane as INotifyPropertyChanged);
				if (n != null)
					n.PropertyChanged += panePropertyChanged;
			} // ctor

			public void Detach()
			{
				model.Panes.CurrentChanged -= collectionViewCurrentChanged;
				var n = (pane as INotifyPropertyChanged);
				if (n != null)
					n.PropertyChanged -= panePropertyChanged;
			} // proc Detach

			private void PanePropertyChanged(object sender, PropertyChangedEventArgs e)
			{
				switch (e.PropertyName)
				{
					case nameof(IPwWindowPane.Title):
						OnPropertyChanged(nameof(Title));
						break;
					case nameof(IPwWindowPane.Image):
						OnPropertyChanged(nameof(Image));
						break;
					case nameof(IPwWindowPane.Control):
						OnPropertyChanged(nameof(Control));
						break;
				}
			} // proc PanePropertyChanged

			private void OnPropertyChanged(string propertyName)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

			public string Title => pane.Title ?? "Unknown";
			public object Image => pane.Image;
			public FrameworkElement Control => (FrameworkElement)pane.Control;

			public bool IsChecked
			{
				get => model.Panes.CurrentItem == this;
				set { if (value) model.Panes.MoveCurrentTo(this); }
			} // prop IsChecked

			public IPwWindowPane Pane => pane;
		} // class PaneItem

		#endregion

		#region -- class MainWindowModel ----------------------------------------------

		public class MainWindowModel //: INotifyPropertyChanged
		{
			private readonly MainWindow mainWindow;
			private readonly IPwCollection<IPwWindowPane> panes;

			private readonly ObservableCollection<PaneItem> shadowPanes;

			public MainWindowModel(MainWindow mainWindow, IPwGlobal global)
			{
				this.mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
				this.Global = global ?? throw new ArgumentNullException(nameof(global));
				var globalPackage = (IPwPackage)global;

				Window = global.UserLocal.GetLuaTable("Window");

				// pane registration
				panes = global.RegisterCollection<IPwWindowPane>(globalPackage); // highest
				panes.CollectionChanged += Panes_CollectionChanged;
				shadowPanes = new ObservableCollection<PaneItem>();
				Panes_CollectionChanged(panes, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

				Panes = CollectionViewSource.GetDefaultView(shadowPanes);

				// register standard panels
				global.RegisterObject(globalPackage, "Actions", new ActionsPane(global));
				global.RegisterObject(globalPackage, "MenuHotKey", ((PwGlobal)global).CreateHotKey(
#if DEBUG
					"Ctrl+Alt+Win+P",
#else
					"Win+Alt+P",
#endif
					new Action(((App)Application.Current).ShowMainWindow)
				));

				Panes.MoveCurrentToFirst();
			} // ctor

			private void Panes_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
			{
				PaneItem item;

				PaneItem FindItem(IPwWindowPane pane)
					=> shadowPanes.FirstOrDefault(c => c.Pane == pane);

				switch (e.Action)
				{
					case NotifyCollectionChangedAction.Reset:
						foreach (var c in shadowPanes)
							c.Detach();
						shadowPanes.Clear();

						foreach (var p in panes)
							shadowPanes.Add(new PaneItem(this, p));
						break;
					case NotifyCollectionChangedAction.Add:
						shadowPanes.Add(new PaneItem(this, (IPwWindowPane)e.NewItems[0]));
						break;
					case NotifyCollectionChangedAction.Replace:
						item = FindItem((IPwWindowPane)e.OldItems[0]);
						if (item != null)
						{
							item.Detach();
							var index = shadowPanes.IndexOf(item);
							shadowPanes[index] = new PaneItem(this, (IPwWindowPane)e.NewItems[0]);
						}
						break;
					case NotifyCollectionChangedAction.Remove:
						item = FindItem((IPwWindowPane)e.OldItems[0]);
						if (item != null)
						{
							item.Detach();
							shadowPanes.Remove(item);
						}
						break;
					default:
						throw new NotImplementedException();
				}
			} // event Panes_CollectionChanged
			
			public ICollectionView Panes { get; }
			public LuaTable Window { get; }
			public IPwGlobal Global { get; }
		} // class MainWindowModel

		#endregion

		private HwndSource hwnd;
		private RECT sizingArea;
		private readonly MainWindowModel model;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public MainWindow(IPwGlobal global)
		{
			model = new MainWindowModel(this, global);

			InitializeComponent();

			CommandBindings.AddRange(new CommandBinding[] {
				new CommandBinding(ApplicationCommands.Open,
					(sender, e) => OpenConfiguration(((PwGlobal)global).ConfigurationFile)
				),
				new CommandBinding(ApplicationCommands.Properties,
					(sender, e) =>
					{
						((PwGlobal)global).RefreshConfiguration();
						model.Global.UI.ShowNotification("Konfiguration neu geladen!");
					}
				),
				new CommandBinding(ApplicationCommands.Close,
					(sender, e) => ((App)global.UI).ExitApplication()
				)
			});

			RecalcPosition();

			// add the wnd hook
			hwnd = HwndSource.FromHwnd(new WindowInteropHelper(this).EnsureHandle());
			hwnd.AddHook(WindowProc);

			SetWindowLong(hwnd.Handle, GWL_STYLE, GetWindowLong(hwnd.Handle, GWL_STYLE) & (~(uint)(WS_MAXIMIZEBOX | WS_MINIMIZEBOX)));

			this.DataContext = model;

			Focus();
		} // ctor

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);
			e.Cancel = true;
		} // proc OnClosing

		protected override void OnDeactivated(EventArgs e)
		{
			base.OnDeactivated(e);
			Hide();
		} // proc OnDeactivated

		#endregion

		#region -- WindowProc, Position -----------------------------------------------

		public void RecalcPosition()
		{
			var rcWorkArea = SystemParameters.WorkArea;
			var xBorder = SystemParameters.ResizeFrameVerticalBorderWidth + SystemParameters.BorderWidth + 1;
			var yBorder = SystemParameters.ResizeFrameHorizontalBorderHeight + SystemParameters.BorderWidth + 1;

			Left = rcWorkArea.Right - Width + xBorder;
			Top = rcWorkArea.Bottom - Height + yBorder;

			sizingArea.Left = Convert.ToInt32(rcWorkArea.Width / 8);
			sizingArea.Top = Convert.ToInt32(rcWorkArea.Height / 8);
			sizingArea.Right = Convert.ToInt32(rcWorkArea.Right + xBorder);
			sizingArea.Bottom = Convert.ToInt32(rcWorkArea.Bottom + yBorder);
		} // proc RecalcPosition

		private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			switch (msg)
			{
				case WM_SYSCOMMAND:
					switch (wParam.ToInt32() & 0xFFF0)
					{
						case 0xF010: // SC_MOVE
						case 0xF020: // SC_MINIMIZE
						case 0xF030: // SC_MAXIMIZE
						case 0xF120: // SC_RESTORE
							handled = true;
							return IntPtr.Zero;
					}
					goto default;
				case WM_SIZING:
					{
						var rc = (RECT)Marshal.PtrToStructure(lParam, typeof(RECT));

						handled = false;
						if (rc.Left < sizingArea.Left)
						{
							rc.Left = sizingArea.Left;
							handled |= true;
						}
						if (rc.Top < sizingArea.Top)
						{
							rc.Top = sizingArea.Top;
							handled |= true;
						}
						if (rc.Right != sizingArea.Right)
						{
							rc.Right = sizingArea.Right;
							handled |= true;
						}
						if (rc.Bottom != sizingArea.Bottom)
						{
							rc.Bottom = sizingArea.Bottom;
							handled |= true;
						}
						if (handled)
						{
							Marshal.StructureToPtr(rc, lParam, true);
							return IntPtr.Zero;
						}
						else
							goto default;
					}
				default:
					handled = false;
					return IntPtr.Zero;
			}
		} // proc WindowProc

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
			=> RecalcPosition();

		#endregion

		#region -- OpenConfiguration --------------------------------------------------

		private static string FindVsCode()
		{
			using (var reg = Registry.ClassesRoot.OpenSubKey(@"Applications\Code.exe\shell\open\command", false))
			{
				var vsCode = (string)reg?.GetValue(null, null, RegistryValueOptions.None);
				if (vsCode != null) // parse binary path
				{
					if (vsCode.StartsWith("\""))
					{
						var p = vsCode.IndexOf('"', 1);
						if (p == -1)
							return null;
						return vsCode.Substring(1, p - 1);
					}
					else
					{
						var p = vsCode.IndexOf(' ');
						if (p == -1)
							return vsCode;
						else
							return vsCode.Substring(0, p);
					}
				}
				else
					return null;
			}
		} // func FindVsCode

		private static ProcessStartInfo OpenWithVsCode(string configurationFile)
		{
			// find vscode
			var vsCodeExe = FindVsCode();
			if (vsCodeExe == null)
				return null;

			// get correct file name, because vscode works case sensitive
			var realConfigurationFile = Directory.GetFiles(Path.GetDirectoryName(configurationFile), Path.GetFileName(configurationFile), SearchOption.TopDirectoryOnly).FirstOrDefault();
			if (realConfigurationFile == null)
				return null;

			// build commandline
			return new ProcessStartInfo(
				vsCodeExe,
				'"' + Path.GetDirectoryName(realConfigurationFile) + "\" \"" + realConfigurationFile + '"'
			);
		} // func OpenWithVsCode

		private static ProcessStartInfo OpenWithExplorer(string configurationFile)
		{
			return new ProcessStartInfo(
				Path.Combine(Environment.SystemDirectory, "explorer.exe"),
				'"' + Path.GetDirectoryName(configurationFile) + '"'
			);
		} // proc OpenWithExplorer

		private void OpenConfiguration(string configurationFile)
		{
			var psi = OpenWithVsCode(configurationFile) ?? OpenWithExplorer(configurationFile);
			psi.UseShellExecute = false;

			// run command
			try
			{
				using (var p = Process.Start(psi))
					p.WaitForInputIdle(400);
			}
			catch (Exception e)
			{
				model.Global.UI.ShowException("Konnte Konfiguration nicht öffnen.", e);
			}
		} // proc OpenConfiguration

		#endregion

		static MainWindow()
		{
			HeightProperty.OverrideMetadata(typeof(MainWindow), new FrameworkPropertyMetadata(350.0));
			WidthProperty.OverrideMetadata(typeof(MainWindow), new FrameworkPropertyMetadata(512.0));
		}
	} // class MainWindow
}
