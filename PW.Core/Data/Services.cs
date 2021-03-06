﻿#region -- copyright --
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
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Neo.PerfectWorking.Stuff;

namespace Neo.PerfectWorking.Data
{
	#region -- interface IPwIdleAction ------------------------------------------------

	public interface IPwIdleAction
	{
		/// <summary>Gets called on application idle start.</summary>
		/// <param name="elapsed">ms elapsed since the idle started</param>
		/// <returns><c>false</c>, if there are more idles needed.</returns>
		bool OnIdle(int elapsed);
	} // interface IPwIdleAction

	#endregion

	#region -- interface IPwShellUI ---------------------------------------------------

	public interface IPwShellUI
	{
		/// <summary>Adds a idle action to the UI.</summary>
		/// <param name="idleAction"></param>
		/// <returns></returns>
		IPwIdleAction AddIdleAction(IPwIdleAction idleAction);
		/// <summary>Removes the idle action.</summary>
		/// <param name="idleAction"></param>
		void RemoveIdleAction(IPwIdleAction idleAction);

		/// <summary>Execute the delegate in the ui-thread.</summary>
		/// <param name="action"></param>
		/// <returns></returns>
		Task InvokeAsync(Action action);
		/// <summary>Execute the delegate in the ui-thread.</summary>
		/// <param name="action"></param>
		/// <returns></returns>
		Task<T> InvokeAsync<T>(Func<T> action);
		/// <summary>Post the delegate to the ui-thread.</summary>
		/// <param name="action"></param>
		void BeginInvoke(Action action);

		/// <summary>Show the exception.</summary>
		/// <param name="text"></param>
		/// <param name="e"></param>
		void ShowException(string text, Exception e);
		/// <summary>Show a messagebox.</summary>
		/// <param name="text"></param>
		/// <param name="caption"></param>
		/// <param name="icon"></param>
		/// <param name="buttons"></param>
		/// <param name="result"></param>
		/// <returns>y,n,o,c</returns>
		string MsgBox(string text, string caption = null, object icon = null, object buttons = null, object result = null);
		/// <summary>Schow a messagebox in the main thread.</summary>
		/// <param name="text"></param>
		/// <param name="caption"></param>
		/// <param name="icon"></param>
		/// <param name="buttons"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		Task<string> MsgBoxAsync(string text, string caption = null, object icon = null, object buttons = null, object result = null);
		/// <summary></summary>
		/// <param name="message"></param>
		/// <param name="image"></param>
		void ShowNotification(object message, object image = null);

		/// <summary>Remote user directory.</summary>
		DirectoryInfo ApplicationRemoteDirectory { get; }
		/// <summary>Local user directory.</summary>
		DirectoryInfo ApplicationLocalDirectory { get; }
	} // interface IPwShellUI

	#endregion

	#region -- interface IPwWindowPane ------------------------------------------------

	public interface IPwWindowPane
	{
		/// <summary>Title of the pane</summary>
		string Title { get; }
		/// <summary>Control of the pane (FrameworkControl).</summary>
		object Control { get; }
		/// <summary>Returns the image of the panel.</summary>
		object Image { get; }
		/// <summary></summary>
		bool IsEnabled { get; }
	} // interface IPwWindowPane

	#endregion

	#region -- interface IPwWindowBackButton ------------------------------------------

	/// <summary>Support for the Back button</summary>
	public interface IPwWindowBackButton
	{
		/// <summary></summary>
		void GoBack();
		bool CanBack { get; }
	} // interface IPwWindowBackButton

	#endregion

	#region -- interface IPwHotKey ----------------------------------------------------

	/// <summary>Hot key</summary>
	public interface IPwHotKey : ICommand
	{
		/// <summary>Hotkey definition.</summary>
		PwKey Key { get; }
	} // interface IPwRegisteredHotKey

	#endregion

	#region -- interface IPwUIHotKey --------------------------------------------------

	/// <summary></summary>
	public interface IPwUIHotKey : IPwHotKey
	{
		/// <summary>Title of the command to execute</summary>
		string Title { get; }
		/// <summary>Image of the command.</summary>
		object Image { get; }
	} // interface IPwUIHotKey

	#endregion

	#region -- interface IPwContextMenu -----------------------------------------------

	/// <summary></summary>
	public interface IPwContextMenu : ICommand
	{
		string Title { get; }
		object Image { get; }

		IEnumerable<IPwContextMenu> Menu { get; }
	} // interface IPwContextMenu

	#endregion

	#region -- interface IPwContextMenu2 ----------------------------------------------

	/// <summary></summary>
	public interface IPwContextMenu2 : IPwContextMenu
	{
		bool IsCheckable { get; }
		bool IsChecked { get; }
	} // interface IPwContextMenu2

	#endregion

	public static class UIHelper
	{
		#region -- class GenericContextMenu -------------------------------------------

		private sealed class GenericContextMenu : IPwContextMenu
		{
			public event EventHandler CanExecuteChanged
			{
				add
				{
					if (command != null)
						command.CanExecuteChanged += value;
				}
				remove
				{
					if (command != null)
						command.CanExecuteChanged -= value;
				}
			} // event CanExecuteChanged

			private readonly IPwContextMenu[] menu;
			private readonly string title;
			private readonly object image;
			private readonly ICommand command;

			public GenericContextMenu(string title, object image, ICommand command, IPwContextMenu[] menu)
			{
				this.title = title;
				this.image = image;
				this.command = command;
				this.menu = menu;
			}

			public void Execute(object parameter)
				=> command?.Execute(parameter);

			public bool CanExecute(object parameter)
				=> command?.CanExecute(parameter) ?? false;

			public string Title => title;
			public object Image => image;

			public IEnumerable<IPwContextMenu> Menu => menu;
		} // class GenericContextMenu

		#endregion

		public static IPwContextMenu CreateMenu(string title, object image, ICommand command, params IPwContextMenu[] menu)
			=> new GenericContextMenu(title, image, command, menu);

		public static void ShowException(this IPwShellUI ui, Exception e)
			=> ui.ShowException(e?.Message, e);

		public static Task ShowExceptionAsync(this IPwShellUI ui, Exception e)
			=> ShowExceptionAsync(ui, e?.Message, e);

		public static Task ShowExceptionAsync(this IPwShellUI ui, string text, Exception e)
			=> ui.InvokeAsync(() => ui.ShowException(text, e));
	} // class ShellHelper
}
