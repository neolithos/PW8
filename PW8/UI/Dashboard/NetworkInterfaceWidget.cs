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
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Neo.PerfectWorking.Data;

namespace Neo.PerfectWorking.UI
{
	internal sealed class NetworkInterfaceWidget : Control, IPwIdleAction
	{
		#region -- class NetworkTrafficGraph ------------------------------------------

		private sealed class NetworkTrafficGraph
		{
			#region -- class DownSpeed ------------------------------------------------

			private sealed class DownSpeed : IPwSparkLineSource
			{
				private readonly NetworkTrafficGraph graph;

				public DownSpeed(NetworkTrafficGraph graph)
					=> this.graph = graph ?? throw new ArgumentNullException(nameof(graph));

				public double GetPoint(int index, double scaleY)
					=> graph.GetScaledPoint(graph.totalSpeed, index, scaleY);

				public event EventHandler Changed
				{
					add { graph.GraphChanged += value; }
					remove { graph.GraphChanged -= value; }
				} // event Changed

				public int Count => graph.totalSpeed.Length;
			} // class DownSpeed

			#endregion

			#region -- class DownSpeed ------------------------------------------------

			private sealed class UpSpeed : IPwSparkLineSource
			{
				private readonly NetworkTrafficGraph graph;

				public UpSpeed(NetworkTrafficGraph graph)
					=> this.graph = graph ?? throw new ArgumentNullException(nameof(graph));

				public double GetPoint(int index, double scaleY)
					=> graph.GetScaledPoint(graph.uploadSpeed, index, scaleY);

				public event EventHandler Changed
				{
					add { graph.GraphChanged += value; }
					remove { graph.GraphChanged -= value; }
				} // event Changed

				public int Count => graph.uploadSpeed.Length;
			} // class UpSpeed

			#endregion

			public event EventHandler GraphChanged;

			private readonly NetworkInterface networkInterface;

			private int lastAdded = 0;
			private long currentMax = 0;
			private long discreteMax = 1024;
			private readonly long[] totalSpeed = new long[60]; // speed in byte/sec
			private readonly long[] uploadSpeed = new long[60]; // speed in byte/sec

			private long lastTotalBytes = -1L;
			private long lastUpBytes = -1L;
			private int lastTick = -1;
			private int lastDiffRest = 0;

			private readonly IPwSparkLineSource totalLine;
			private readonly IPwSparkLineSource uploadLine;

			public NetworkTrafficGraph(NetworkInterface networkInterface)
			{
				this.networkInterface = networkInterface ?? throw new ArgumentNullException(nameof(networkInterface));

				totalLine = new DownSpeed(this);
				uploadLine = new UpSpeed(this);
			} // ctor

			private void Append(long totalBytesPerSec, long upBytesPerSec)
			{
				lastAdded += 1;
				if (lastAdded >= totalSpeed.Length)
					lastAdded = 0;

				// update array
				var deletedTotalValue = totalSpeed[lastAdded];
				totalSpeed[lastAdded] = totalBytesPerSec;
				uploadSpeed[lastAdded] = upBytesPerSec;

				// check max value
				var recalcMax = deletedTotalValue == currentMax;
				if (recalcMax)
				{
					currentMax = 0;
					for (var i = 0; i < totalSpeed.Length; i++)
					{
						if (totalSpeed[i] > currentMax)
							SetCurrentMax(totalSpeed[i]);
					}
				}
				else // check for new max value
				{
					if (totalBytesPerSec > currentMax)
						SetCurrentMax(totalBytesPerSec);
				}
			} // proc Append

			private void SetCurrentMax(long bytesPerSec)
			{
				long CalcDiscrete(long dim)
				{
					var v = Math.DivRem(currentMax, dim, out var r);
					if (r > 0)
						v++;

					var t = v < 10 ? 10 : 100;
					v = Math.DivRem(v, t, out r);
					if (r > 0)
						v++;

					return v * t * dim;
				} // CalcDiscrete

				currentMax = bytesPerSec;
				if (currentMax < 1024)
					discreteMax = 1024; // 1KB
				else if (currentMax < 1 << 20) // 1MB
					discreteMax = CalcDiscrete(1 << 10);
				else // GB
					discreteMax = CalcDiscrete(1 << 20);
			} // proc SetCurrentMax

			private double GetScaledPoint(long[] bytesPerSecArray, int index, double scaleY)
			{
				var realIndex = lastAdded + index + 1;
				var count = totalSpeed.Length;
				while (realIndex >= count)
					realIndex -= count;
				return bytesPerSecArray[realIndex] * scaleY / discreteMax;
			} // func GetScaledPoint

			public void Update()
			{
				var stat = networkInterface.GetIPStatistics();
				if (lastTotalBytes == -1L)
				{
					lastTotalBytes = stat.BytesReceived;
					lastUpBytes = stat.BytesSent;
					lastTick = Environment.TickCount;
				}
				else
				{
					var newUpBytes = stat.BytesSent;
					var newTotalBytes = stat.BytesReceived + newUpBytes;
					var newTick = Environment.TickCount;
					
					var diff = unchecked(newTick - lastTick); // time between measures

					var totalBytesPerSec = (newTotalBytes - lastTotalBytes) * 1000 / diff;
					var upBytesPerSec = (newUpBytes - lastUpBytes) * 1000 / diff;

					var count = Math.DivRem(lastDiffRest + diff, 1000, out var newRest);
					lastDiffRest = newRest;

					while (count-- > 0)
						Append(totalBytesPerSec, upBytesPerSec);
					GraphChanged?.Invoke(this, EventArgs.Empty);

					lastTotalBytes = newTotalBytes;
					lastUpBytes = newUpBytes;
					lastTick = newTick;
				}
			} // proc Update

			public long LastTotalSpeed => totalSpeed[lastAdded];
			public long LastDownloadSpeed => LastTotalSpeed - LastUploadSpeed;
			public long LastUploadSpeed => uploadSpeed[lastAdded];
			public long DiscreteMax => discreteMax;

			public IPwSparkLineSource TotalLine => totalLine;
			public IPwSparkLineSource UploadLine => uploadLine;
		} // class NetworkTrafficGraph

		#endregion

		private int networkInterfaceIndex = -1;
		private NetworkTrafficGraph networkSpeed = null;
		private int lastElapsed = 0;

		public NetworkInterfaceWidget(IPwWidgetWindow window)
		{
			if (window == null)
				throw new ArgumentNullException(nameof(window));

			// update colors
			ForegroundMiddle = new SolidColorBrush(UIHelper.GetMixedColor(window.ForegroundColor, window.BackgroundColor, 0.5f));
			TotalSpeedLineColor = UIHelper.GetMixedColor(window.BackgroundColor, Colors.Green, 0.5f);
			UploadSpeedLineColor = UIHelper.GetMixedColor(window.BackgroundColor, Colors.Red, 0.5f);

			window.Global.UI.AddIdleAction(this);
		} // ctor

		private void ClearProperties()
		{
			networkSpeed = null;
			TotalSpeedLine = null;
			UploadSpeedLine = null;
			CurrentSpeed = 0;
		} // proc ClearProperties

		bool IPwIdleAction.OnIdle(int elapsed)
		{
			if (lastElapsed > elapsed)
				lastElapsed = 0;

			if (unchecked(elapsed - lastElapsed) > 900)
			{
				lastElapsed = elapsed;
				UpdateProperties();
			}
			return true;
		} // proc OnIdle

		private NetworkInterface GetCurrentNetworkInterface()
			=> GetNetworkInterface(ref networkInterfaceIndex, InterfaceName);

		private void UpdateProperties()
		{
			var networkInterface = GetCurrentNetworkInterface();
			if (networkInterface != null && networkInterface.OperationalStatus == OperationalStatus.Up)
			{
				var props = networkInterface.GetIPProperties();

				var ipV4 = props.UnicastAddresses.FirstOrDefault(c => c.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
				var ipV6 = props.UnicastAddresses.FirstOrDefault(c => c.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);
				UnicastAddress4 = ipV4?.Address.ToString();
				UnicastAddress6 = ipV6?.Address.ToString();
				Description = IpLookup?.Invoke(ipV4?.Address ?? ipV6?.Address) ?? networkInterface.Description;

				if (networkSpeed == null)
				{
					networkSpeed = new NetworkTrafficGraph(networkInterface);
					TotalSpeedLine = networkSpeed.TotalLine;
					UploadSpeedLine = networkSpeed.UploadLine;
				}

				networkSpeed.Update();

				NetworkState = "Up";
				NetworkSpeed = networkInterface.Speed;
				CurrentSpeed = networkSpeed.DiscreteMax;
				CurrentDownSpeed = networkSpeed.LastDownloadSpeed;
				CurrentUpSpeed = networkSpeed.LastUploadSpeed;
			}
			else
			{
				if (networkInterface == null)
					NetworkState = "missing";
				else
					NetworkState = networkInterface.OperationalStatus.ToString();

				ClearProperties();
			}
		} // proc UpdateProperties

		#region -- InterfaceName - Property -------------------------------------------

		public static readonly DependencyProperty InterfaceNameProperty = DependencyProperty.Register(nameof(InterfaceName), typeof(string), typeof(NetworkInterfaceWidget), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnInterfaceNameChanged)));
		
		private static void OnInterfaceNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((NetworkInterfaceWidget)d).UpdateProperties();

		public string InterfaceName { get => (string)GetValue(InterfaceNameProperty); set => SetValue(InterfaceNameProperty, value); }

		#endregion

		#region -- NetworkSpeed - Property --------------------------------------------

		private static readonly DependencyPropertyKey networkSpeedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(NetworkSpeed), typeof(long), typeof(NetworkInterfaceWidget), new FrameworkPropertyMetadata(0L));
		public static readonly DependencyProperty NetworkSpeedProperty = networkSpeedPropertyKey.DependencyProperty;

		public long NetworkSpeed { get => (long)GetValue(NetworkSpeedProperty); private set => SetValue(networkSpeedPropertyKey, value); }

		#endregion

		#region -- CurrentSpeed - Property --------------------------------------------

		private static readonly DependencyPropertyKey currentSpeedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentSpeed), typeof(long), typeof(NetworkInterfaceWidget), new FrameworkPropertyMetadata(0L));
		public static readonly DependencyProperty CurrentSpeedProperty = currentSpeedPropertyKey.DependencyProperty;

		public long CurrentSpeed { get => (long)GetValue(CurrentSpeedProperty); private set => SetValue(currentSpeedPropertyKey, value); }

		#endregion

		#region -- CurrentDownSpeed - Property ----------------------------------------

		private static readonly DependencyPropertyKey currentDownSpeedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentDownSpeed), typeof(long), typeof(NetworkInterfaceWidget), new FrameworkPropertyMetadata(0L));
		public static readonly DependencyProperty CurrentDownSpeedProperty = currentDownSpeedPropertyKey.DependencyProperty;

		public long CurrentDownSpeed { get => (long)GetValue(CurrentDownSpeedProperty); private set => SetValue(currentDownSpeedPropertyKey, value); }

		#endregion

		#region -- CurrentUpSpeed - Property ------------------------------------------

		private static readonly DependencyPropertyKey currentUpSpeedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentUpSpeed), typeof(long), typeof(NetworkInterfaceWidget), new FrameworkPropertyMetadata(0L));
		public static readonly DependencyProperty CurrentUpSpeedProperty = currentUpSpeedPropertyKey.DependencyProperty;

		public long CurrentUpSpeed { get => (long)GetValue(CurrentUpSpeedProperty); private set => SetValue(currentUpSpeedPropertyKey, value); }

		#endregion

		#region -- UnicastAddress - Property ------------------------------------------

		private static readonly DependencyPropertyKey unicastAddress4PropertyKey = DependencyProperty.RegisterReadOnly(nameof(UnicastAddress4), typeof(string), typeof(NetworkInterfaceWidget), new FrameworkPropertyMetadata(null));
		private static readonly DependencyPropertyKey unicastAddress6PropertyKey = DependencyProperty.RegisterReadOnly(nameof(UnicastAddress6), typeof(string), typeof(NetworkInterfaceWidget), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty UnicastAddress4Property = unicastAddress4PropertyKey.DependencyProperty;
		public static readonly DependencyProperty UnicastAddress6Property = unicastAddress6PropertyKey.DependencyProperty;

		public string UnicastAddress4 { get => (string)GetValue(UnicastAddress4Property); private set => SetValue(unicastAddress4PropertyKey, value); }
		public string UnicastAddress6 { get => (string)GetValue(UnicastAddress6Property); private set => SetValue(unicastAddress6PropertyKey, value); }

		#endregion

		#region -- Description - Property ---------------------------------------------

		private static readonly DependencyPropertyKey descriptionPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Description), typeof(string), typeof(NetworkInterfaceWidget), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty DescriptionProperty = descriptionPropertyKey.DependencyProperty;

		public string Description { get => (string)GetValue(DescriptionProperty); private set => SetValue(descriptionPropertyKey, value); }

		#endregion

		#region -- NetworkState - Property --------------------------------------------

		private static readonly DependencyPropertyKey networkStatePropertyKey = DependencyProperty.RegisterReadOnly(nameof(NetworkState), typeof(string), typeof(NetworkInterfaceWidget), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty NetworkStateProperty = networkStatePropertyKey.DependencyProperty;

		public string NetworkState { get => (string)GetValue(NetworkStateProperty); private set => SetValue(networkStatePropertyKey, value); }

		#endregion
		
		#region -- DiagramWidth - Property --------------------------------------------

		public static readonly DependencyProperty DiagramWidthProperty = DependencyProperty.Register(nameof(DiagramWidth), typeof(GridLength), typeof(NetworkInterfaceWidget), new FrameworkPropertyMetadata(new GridLength(1.0, GridUnitType.Star)));

		public GridLength DiagramWidth { get => (GridLength)GetValue(DiagramWidthProperty); set => SetValue(DiagramWidthProperty, value); }

		#endregion

		#region -- TotalSpeedLine - Property ------------------------------------------

		private static readonly DependencyPropertyKey totalSpeedLinePropertyKey = DependencyProperty.RegisterReadOnly(nameof(TotalSpeedLine), typeof(IPwSparkLineSource), typeof(NetworkInterfaceWidget), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty TotalSpeedLineProperty = totalSpeedLinePropertyKey.DependencyProperty;

		public IPwSparkLineSource TotalSpeedLine { get => (IPwSparkLineSource)GetValue(TotalSpeedLineProperty); private set => SetValue(totalSpeedLinePropertyKey, value); }

		#endregion

		#region -- UploadSpeedLine - Property -----------------------------------------

		private static readonly DependencyPropertyKey uploadSpeedLinePropertyKey = DependencyProperty.RegisterReadOnly(nameof(UploadSpeedLine), typeof(IPwSparkLineSource), typeof(NetworkInterfaceWidget), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty UploadSpeedLineProperty = uploadSpeedLinePropertyKey.DependencyProperty;

		public IPwSparkLineSource UploadSpeedLine { get => (IPwSparkLineSource)GetValue(UploadSpeedLineProperty); private set => SetValue(uploadSpeedLinePropertyKey, value); }

		#endregion

		#region -- TotalSpeedLineColor - Property -------------------------------------

		public static readonly DependencyProperty TotalSpeedLineColorProperty = DependencyProperty.Register(nameof(TotalSpeedLineColor), typeof(Color), typeof(NetworkInterfaceWidget), new FrameworkPropertyMetadata(Colors.Green));

		public Color TotalSpeedLineColor { get => (Color)GetValue(TotalSpeedLineColorProperty); set => SetValue(TotalSpeedLineColorProperty, value); }

		#endregion

		#region -- UploadSpeedLineColor - Property ------------------------------------

		public static readonly DependencyProperty UploadSpeedLineColorProperty = DependencyProperty.Register(nameof(UploadSpeedLineColor), typeof(Color), typeof(NetworkInterfaceWidget), new FrameworkPropertyMetadata(Colors.Red));

		public Color UploadSpeedLineColor { get => (Color)GetValue(UploadSpeedLineColorProperty); set => SetValue(UploadSpeedLineColorProperty, value); }

		#endregion

		#region -- ForegroundMiddle - Property ----------------------------------------

		public static readonly DependencyProperty ForegroundMiddleProperty = DependencyProperty.Register(nameof(ForegroundMiddle), typeof(Brush), typeof(NetworkInterfaceWidget), new FrameworkPropertyMetadata(null));

		public Brush ForegroundMiddle { get => (Brush)GetValue(ForegroundMiddleProperty); set => SetValue(ForegroundMiddleProperty, value); }

		#endregion

		public Func<IPAddress, string> IpLookup { get; set; } = null;

		static NetworkInterfaceWidget()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(NetworkInterfaceWidget), new FrameworkPropertyMetadata(typeof(NetworkInterfaceWidget)));
		} // sctor

		#region -- class NetworkStateConverter ----------------------------------------

		private sealed class NetworkStateConverter : IMultiValueConverter
		{
			public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
			{
				if(values[0] is string state && values[1] is long speed)
				{
					if (state == "Up")
					{
						if (speed < 1000)
							return $"{speed} Bits/s";
						else if (speed < 1000000)
							return $"{speed / 1000} KBits/s";
						else if (speed < 1000000000)
							return $"{speed / 1000000} MBits/s";
						else
							return $"{speed / 1000000000} GBits/s";
					}
					else
						return state;
				}
				return "?";
			} // func Convert

			public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
				=> throw new NotSupportedException();
		} // class NetworkStateConverter

		#endregion

		#region -- class NetworkSpeedConverter ----------------------------------------

		private sealed class NetworkSpeedConverter : IValueConverter
		{
			public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				if (value is long l)
				{
					if (l < 1024)
						return $"{l} Byte/s";
					else if (l < 1 << 20)
						return $"{l / 1024.0:N1} KiB/s";
					else 
						return $"{l / 1048576.0:N1} MiB/s";
				}
				return "?";
			} // func Convert

			public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
				=> throw new NotSupportedException();
		} // class NetworkSpeedConverter

		#endregion

		private static NetworkInterface[] networkInterfaces = null;
		private static int networkInterfaceTick = 0;

		private static NetworkInterface GetNetworkInterface(ref int networkInterfaceIndex, string interfaceName)
		{
			// get interfaces
			if (networkInterfaces == null || unchecked(Environment.TickCount - networkInterfaceTick) > 5000)
			{
				networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
				networkInterfaceTick = Environment.TickCount;
			}

			// find interface
			if (networkInterfaceIndex >= 0 && networkInterfaceIndex < networkInterfaces.Length
				&& String.Compare(networkInterfaces[networkInterfaceIndex].Name, interfaceName, StringComparison.OrdinalIgnoreCase) == 0)
				return networkInterfaces[networkInterfaceIndex];
			else
			{
				networkInterfaceIndex = Array.FindIndex(networkInterfaces, c => String.Compare(c.Name, interfaceName, StringComparison.OrdinalIgnoreCase) == 0);
				return networkInterfaceIndex >= 0 ? networkInterfaces[networkInterfaceIndex] : null;
			}
		} // func GetNetworkInterface

		public static IMultiValueConverter NetworkStateConvert { get; } = new NetworkStateConverter();
		public static IValueConverter NetworkSpeedConvert { get; } = new NetworkSpeedConverter();

		public static IPwWidgetFactory Factory { get; } = new PwWidgetFactory<NetworkInterfaceWidget>();
	} // class NetworkInterfaceWidget
}
