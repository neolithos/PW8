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

namespace Neo.PerfectWorking.Data
{
	#region -- class PwWindowHook -------------------------------------------------------

	public delegate IntPtr? PwWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

	public sealed class PwWindowHook
	{
		private readonly int[] messageFilter;
		private readonly PwWindowProc wndProc;

		public PwWindowHook(PwWindowProc wndProc, params int[] messageFilter) 
		{
			if (messageFilter == null || messageFilter.Length == 0)
				throw new ArgumentNullException(nameof(messageFilter));
			this.messageFilter = messageFilter;
			this.wndProc = wndProc ?? throw new ArgumentNullException(nameof(wndProc));
		} // ctor

		public bool IsHookMessage(int msg)
			=> Array.IndexOf(messageFilter, msg) >= 0;

		public PwWindowProc WndProc => wndProc;
	} // class PwWindowHook

	#endregion
}
