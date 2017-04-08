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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo.PerfectWorking.Data
{
	#region -- interface IPwIdleAction --------------------------------------------------

	public interface IPwIdleAction
	{
		/// <summary>Gets called on application idle start.</summary>
		/// <param name="elapsed">ms elapsed since the idle started</param>
		/// <returns><c>false</c>, if there are more idles needed.</returns>
		bool OnIdle(int elapsed);
	} // interface IPwIdleAction

	#endregion

	#region -- interface IPwShellUI -----------------------------------------------------

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
		/// <summary></summary>
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

		DirectoryInfo ApplicationRemoteDirectory { get; }
		DirectoryInfo ApplicationLocalDirectory { get; }
	} // interface IPwShellUI

	#endregion

	#region -- interface IPwWindowPane --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPwWindowPane
	{
		/// <summary>Title of the pane</summary>
		string Title { get; }
		/// <summary>Control of the pane (FrameworkControl).</summary>
		object Control { get; }
		/// <summary>Returns the image of the panel.</summary>
		object Image { get; }
	} // interface IPwWindowPane

	#endregion

	public static class UIHelper
	{
		public static void ShowException(this IPwShellUI ui, Exception e)
			=> ui.ShowException(e?.Message, e);

		public static Task ShowExceptionAsync(this IPwShellUI ui, Exception e)
			=> ShowExceptionAsync(ui, e?.Message, e);

		public static Task ShowExceptionAsync(this IPwShellUI ui, string text, Exception e)
			=> ui.InvokeAsync(() => ui.ShowException(text, e));
	} // class ShellHelper
}
