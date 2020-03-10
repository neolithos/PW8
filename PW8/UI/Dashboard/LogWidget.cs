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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Neo.PerfectWorking.Data;
using TecWare.DE.Stuff;

namespace Neo.PerfectWorking.UI
{
	#region -- class LogItem ----------------------------------------------------------

	internal sealed class LogItem
	{
		public LogItem(EventLevel level, DateTime stamp, string text)
		{
			Level = level;
			Stamp = stamp;
			Text = text;
		} // ctor

		public EventLevel Level { get; }
		public DateTime Stamp { get; }
		public string Text { get; }
	} // class LogItem

	#endregion

	#region -- class LogItemArray -----------------------------------------------------

	internal sealed class LogItemArray : IEnumerable<LogItem>, INotifyCollectionChanged
	{
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private LogItem[] items = new LogItem[10];
		private int top = 0; // show the top item

		#region -- List management ----------------------------------------------------

		public void Append(EventLevel level, DateTime stamp, string text)
		{
			items[top] = new LogItem(level, stamp, text);
			top = GetNext(top);
			OnCollectionReset();
		} // proc Append

		private void Resize(int newCount)
		{
			if (items.Length == newCount)
				return;

			var newItems = new LogItem[newCount];

			var j = 0;
			foreach (var c in this)
			{
				newItems[j] = c;
				j = j >= newItems.Length-1 ? 0 : j + 1;
			}

			top = j;
			items = newItems;

			OnCollectionReset();
		} // proc Resize

		private int GetNext(int cur)
		{
			cur++;
			if (cur >= items.Length)
				cur = 0;
			return cur;
		} // func GetNext

		public IEnumerator<LogItem> GetEnumerator()
		{
			var i = top;
			while (true)
			{
				var c = items[i];
				if (c != null)
					yield return c;
				i = i >= items.Length-1 ? 0 : i + 1;
				if (i == top)
					break;
			}
		} // func GetEnumerator

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		private void OnCollectionReset()
			=> CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		#endregion

		public int Capacity
		{
			get => items.Length;
			set => Resize(value);
		}
	} // class LogItemArray

	#endregion

	internal sealed class LogWidget : ItemsControl
	{
		private readonly IPwCollection<EventSource> sources;
		private readonly EventListener listener;

		private readonly LogItemArray items = new LogItemArray();

		public LogWidget(IPwGlobal global)
		{
			ItemsSource = items;
			OnLogLinesChanged(items.Capacity);

			sources = global.RegisterCollection<EventSource>((IPwPackage)global);
			listener = new EventListener();
			listener.EventWritten += Listener_EventWritten;
			sources.CollectionChanged += Sources_CollectionChanged;

			ResetCollections();
		} // ctor

		private void ResetCollections()
		{
			foreach (var c in sources)
				listener.EnableEvents(c, EventLevel.Verbose);
		} // proc ResetCollections

		private void Sources_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					listener.EnableEvents((EventSource)e.NewItems[0], EventLevel.Verbose);
					break;
				case NotifyCollectionChangedAction.Remove:
					listener.DisableEvents((EventSource)e.NewItems[0]);
					break;
				case NotifyCollectionChangedAction.Reset:
					ResetCollections();
					break;
			}
		} // event Sources_CollectionChanged

		private static string CreateMessage(EventWrittenEventArgs e)
			=> String.Format(e.Message, e.Payload.ToArray()).GetFirstLine();

		private void Listener_EventWritten(object sender, EventWrittenEventArgs e)
		{
			if (e.Channel == EventChannel.Operational)
			{
				Dispatcher.BeginInvoke(new Action<EventLevel, DateTime, string>(items.Append),
				  DispatcherPriority.Normal,
				  e.Level, DateTime.Now, CreateMessage(e)
			  );
			}
		} // event Listener_EventWritten

		#region -- LogLines - Properties ----------------------------------------------

		public static readonly DependencyProperty LogLinesProperty = DependencyProperty.Register(nameof(LogLines), typeof(int), typeof(LogWidget), new FrameworkPropertyMetadata(10, new PropertyChangedCallback(OnLogLinesChanged), new CoerceValueCallback(CoerceLogLinesCallback)));

		private static object CoerceLogLinesCallback(DependencyObject d, object baseValue)
			=> !(baseValue is int t) || t < 3 ? 3 : t;

		private static void OnLogLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((LogWidget)d).OnLogLinesChanged((int)e.NewValue);

		private void OnLogLinesChanged(int newValue)
		{
			items.Capacity = newValue;

			Height = 20 * items.Capacity;
		} // proc OnLogLinesChanged

		public int LogLines { get => (int)GetValue(LogLinesProperty); set => SetValue(LogLinesProperty, value); }
		
		#endregion

		static LogWidget()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(LogWidget), new FrameworkPropertyMetadata(typeof(LogWidget)));
		} // sctor

		public static IWidgetFactory Factory { get; } = new WidgetFactory<LogWidget>();
	} // class LogWidget
}
