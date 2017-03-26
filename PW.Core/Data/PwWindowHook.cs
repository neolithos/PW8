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
