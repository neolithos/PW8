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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TecWare.DE.Stuff;

namespace Neo.PerfectWorking.OpenVpn
{
	public delegate void OpenVpnStateDelegate(DateTime stamp, OpenVpnState state);
	public delegate void OpenVpnLogLineDelegate(OpenVpnLogLine logLine);
	public delegate void OpenVpnByteCountDelegate(int cid, long inBytes, long outBytes);

	#region -- class OpenVpnNeedPasswordArgs ------------------------------------------

	public sealed class OpenVpnNeedPasswordArgs : EventArgs
	{
		public OpenVpnNeedPasswordArgs(string name, bool needUsername, bool needPassword)
		{
			Name = name;
			NeedUsername = needUsername;
			NeedPassword = needPassword;
		} // ctor

		public bool Handled { get; set; } = false;

		public string UserName { get; set; } = null;
		public string Password { get; set; } = null;

		public string Name { get; }
		public bool NeedUsername { get; }
		public bool NeedPassword { get; }
	} // class OpenVpnNeedPasswordArgs

	#endregion

	public sealed class OpenVpnConnection : IDisposable
	{
		public event EventHandler<EventArgs> Closed;
		public event EventHandler<OpenVpnNeedPasswordArgs> NeedPassword;

		private event OpenVpnStateDelegate stateEvent;
		private event OpenVpnLogLineDelegate logLineEvent;
		private event OpenVpnByteCountDelegate byteCountEvent;
		
		private readonly NetworkStream managementStream = null;
		private readonly StreamReader inputStream = null;
		private readonly StreamWriter outputStream = null;

		private bool isDisposed = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		private OpenVpnConnection(NetworkStream managementStream)
		{
			this.managementStream = managementStream ?? throw new ArgumentNullException(nameof(managementStream));
			inputStream = new StreamReader(managementStream, Encoding.ASCII, false, 4096, true);
			outputStream = new StreamWriter(managementStream, Encoding.ASCII, 4096, false) { NewLine = "\n" };
		} // ctor

		public void Dispose()
		{
			if (isDisposed)
				throw new ObjectDisposedException(nameof(OpenVpnConnection));
			isDisposed = true;

			Closed?.Invoke(this, EventArgs.Empty);

			inputStream?.Dispose();
			outputStream?.Dispose();
			managementStream?.Dispose();
		} // proc Dispose

		#endregion

		public static async Task<OpenVpnConnection> ConnectAsync(int managementPort, string managementPassword, int retry)
		{
			// connect to socket
			retry:
			var managementSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			try
			{
				await managementSocket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, managementPort));
			}
			catch
			{
				managementSocket.Dispose();
				if (--retry <= 0)
					throw;
				else
				{
					await Task.Delay(500);
					goto retry;
				}
			}

			// init streams
			var managementStream = new NetworkStream(managementSocket, true);
			var connection = new OpenVpnConnection(managementStream);
			try
			{
				await connection.SendLineAsync(managementPassword);

				connection.StartProcessInput();
			}
			catch
			{
				connection.Dispose();
				throw;
			}
			return connection;
		} // func ConnectAsync

		#region -- Core Primitives ----------------------------------------------------

		private static readonly DateTime unixBaseStamp = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

		private static DateTime GetDateTime(long unixTimeStamp)
			=> unixBaseStamp.AddSeconds(unixTimeStamp).ToLocalTime();

		private bool TrySplitParameter(string text, out string param1, out string param2)
		{
			var p = text.IndexOf(',');
			if (p == -1)
			{
				param1 = null;
				param2 = null;
				return false;
			}

			param1 = text.Substring(0, p);
			param2 = text.Substring(p + 1);
			return true;
		} // func TrySplitParameter

		private bool TrySplitParameter(string text, out string param1, out string param2, out string param3)
		{
			var p1 = text.IndexOf(',');
			if (p1 == -1)
				goto fail;
			var p2 = text.IndexOf(',', p1 + 1);
			if (p2 == -1)
				goto fail;

			param1 = text.Substring(0, p1);
			param2 = text.Substring(p1 + 1, p2 - p1 - 1);
			param3 = text.Substring(p2);
			return true;

			fail:
			param1 = null;
			param2 = null;
			param3 = null;
			return false;
		} // func TrySplitParameter

		private bool TrySplitParameter(string text, int min, out string[] values)
		{
			values = text.Split(',');
			return min <= values.Length;
		} // func TrySplitParameter

		#endregion

		#region -- Core - Send --------------------------------------------------------

		private TaskCompletionSource<string[]> currentAnswer = null;
		private bool currentAnswerIsMultiLineResult = true;

		private string DebugOutput(string msg)
			=> msg;

		private void ParseAnswerLine(List<string> collect, string msg)
		{
			if (currentAnswerIsMultiLineResult)
			{
				if (msg == "END")
				{
					var lines = collect.ToArray();
					Task.Run(() => currentAnswer.TrySetResult(lines)).Silent();
					collect.Clear();
				}
				else
					collect.Add(msg);
			}
			else
				Task.Run(() => currentAnswer.TrySetResult(new string[] { msg })).Silent();
		} // proc ParseAnswerLine

		private async Task SendLineAsync(string command)
		{
			await outputStream.WriteLineAsync(DebugOutput(command));
			await outputStream.FlushAsync();
		} // proc SendLineAsync

		private string inSendCommand = null;
		private readonly SemaphoreSlim sendCommandLock = new SemaphoreSlim(1,1);

		private async Task<string[]> SendAsync(string command, bool multiLineResult)
		{
			await sendCommandLock.WaitAsync();
			try
			{
				if (inSendCommand != null)
					throw new InvalidOperationException();
				inSendCommand = command;
				try
				{
					currentAnswerIsMultiLineResult = multiLineResult;
					currentAnswer = new TaskCompletionSource<string[]>();
					await SendLineAsync(command);

					var result = await currentAnswer.Task;
					currentAnswer = null;
					return result;
				}
				finally
				{
					inSendCommand = null;
				}
			}
			finally
			{
				sendCommandLock.Release();
			}
		} // proc SendAsync

		private async Task<string> SendAsync(string command)
			=> (await SendAsync(command, true)).FirstOrDefault();

		#endregion

		#region -- Core - Receive -----------------------------------------------------

		private string DebugInput(string msg)
			=> msg;

		private void StartProcessInput()
			=> Task.Run(new Action(ProcessInputAsync().Wait));

		private async Task ProcessInputAsync()
		{
			try
			{
				var collect = new List<string>();
				while (true)
				{
					var msg = DebugInput(await inputStream.ReadLineAsync());
					if (String.IsNullOrEmpty(msg))
						return;
					else if (msg[0] == '>') // from openvpn
					{
						var p = msg.IndexOf(':', 1);
						if (p > 0)
						{
							var src = msg.Substring(1, p - 1).ToUpper();
							var text = msg.Substring(p + 1);
							switch (src)
							{
								case "BYTECOUNT":
									OnProcessByteCount(text);
									break;
								case "BYTECOUNT_CLI":
									OnProcessByteCountCli(text);
									break;
								//case "CLIENT":
								//case "ECHO":
								//case "FATAL":
								//case "HOLD":
								case "INFO":
									OnProcessLogLine(new OpenVpnLogLine(DateTime.Now, OpenVpnLineTyp.Info, text));
									break;
								case "LOG":
									OnProcessLogLine(text);
									break;
								//case "NEED-OK":
								//case "NEED-STR":
								case "PASSWORD":
									await OnProcessPasswordAsync(text);
									break;
								case "STATE":
									OnProcessState(text);
									break;
								default:
									break;
							}
						}
					}
					else // answer
						ParseAnswerLine(collect, msg);
				}
			}
			catch (Exception e)
			{
				Debug.Print(e.ToString());
				Dispose();
			}
		} // func ProcessInputAsync

		#endregion

		#region -- State - management -------------------------------------------------

		private OpenVpnState ConvertVpnState(string state)
		{
			return (state) switch
			{
				"CONNECTING" => OpenVpnState.Connecting,
				"WAIT" => OpenVpnState.Wait,
				"AUTH" => OpenVpnState.Authentifaction,
				"GET_CONFIG" => OpenVpnState.GetConfiguration,
				"ASSIGN_IP" => OpenVpnState.AssignIP,
				"ADD_ROUTES" => OpenVpnState.AddingRoutes,
				"CONNECTED" => OpenVpnState.Connected,
				"RECONNECTING" => OpenVpnState.Reconnecting,
				"EXITING" => OpenVpnState.Exiting,
				_ => OpenVpnState.Unknown,
			};
		} // func ConvertVpnState

		private (DateTime dt, OpenVpnState state) ParseVpnState(string textState, bool parseTime)
		{
			if (TrySplitParameter(textState, 2, out var r))
			{
				// 0 unix timestamp
				var dt = parseTime && Int64.TryParse(r[0], out var unixTime)
					? GetDateTime(unixTime)
					: DateTime.MinValue;

				// 1 state name
				return (dt, ConvertVpnState(r[1]));
			}
			else
				return (DateTime.MinValue, OpenVpnState.Unknown);
		} // func ParseVpnState

		public async Task<IEnumerable<(DateTime Stamp, OpenVpnState State)>> GetStatesAsync()
		{
			return (await SendAsync("state all", true))
				.Select(c => ParseVpnState(c, true));
		} // func GetStatesAsync

		public Task<OpenVpnState> GetStateAsync()
		{
			return SendAsync("state")
			   .ContinueWith(t => ParseVpnState(t.Result, false).state);
		} // proc GetStateAsync

		private void OnProcessState(string textState)
		{
			var r = ParseVpnState(textState, true);
			stateEvent?.Invoke(r.dt, r.state);
		} // proc OnProcessState

		public event OpenVpnStateDelegate StateChanged
		{
			add
			{
				if (value == null)
					throw new ArgumentNullException(nameof(value));

				if (stateEvent == null)
					SendAsync("state on", false).Silent(BackgroundException);

				stateEvent += value;
			}
			remove
			{
				if (value == null)
					return;

				stateEvent -= value;
				if (stateEvent == null)
					SendAsync("state off", false).Silent(BackgroundException);

			}
		} // event StateChanged

		#endregion

		#region -- Log ----------------------------------------------------------------

		public Task ReleaseHoldAsync()
			=> SendLineAsync("hold release");

		private async Task EnableLogAsync()
		{
			foreach (var line in await SendAsync("log on all", true))
				OnProcessLogLine(line);
		} // proc EnableLogAsync

		private void OnProcessLogLine(string text)
		{
			if (TrySplitParameter(text, out var p1, out var level, out var msg)
				&& Int64.TryParse(p1, out var unixTimeStamp))
			{
				OnProcessLogLine(new OpenVpnLogLine(GetDateTime(unixTimeStamp), OpenVpnLogLine.ToType(level[0]), msg));
			}
		} // proc OnProcessLogLine

		private void OnProcessLogLine(OpenVpnLogLine log)
			=> logLineEvent?.Invoke(log);

		public event OpenVpnLogLineDelegate LogLine
		{
			add
			{
				if (value == null)
					throw new ArgumentNullException(nameof(value));

				var activate = logLineEvent == null;
				logLineEvent += value;
				if (activate)
					EnableLogAsync().Silent(BackgroundException);
			}
			remove
			{
				if (value == null)
					return;

				logLineEvent -= value;
				if (logLineEvent == null)
					SendAsync("log off", false).Silent(BackgroundException);

			}
		} // event LogLine

		#endregion

		#region -- ByteCount ----------------------------------------------------------

		private void OnProcessByteCount(string text)
		{
			if (TrySplitParameter(text, out var p1, out var p2)
				&& Int64.TryParse(p1, out var inBytes)
				&& Int64.TryParse(p2, out var outBytes))
				OnProcessByteCount(-1, inBytes, outBytes);
		} // proc OnProcessByteCount

		private void OnProcessByteCountCli(string text)
		{
			if (TrySplitParameter(text, out var p1, out var p2, out var p3)
				&& Int32.TryParse(p1, out var cid)
				&& Int64.TryParse(p2, out var inBytes)
				&& Int64.TryParse(p3, out var outBytes))
				OnProcessByteCount(cid, inBytes, outBytes);
		} // proc OnProcessByteCountCli

		private void OnProcessByteCount(int cid, long inBytes, long outBytes)
			=> byteCountEvent?.Invoke(cid, inBytes, outBytes);

		public event OpenVpnByteCountDelegate ByteCountChanged
		{
			add
			{
				if (value == null)
					throw new ArgumentNullException(nameof(value));

				if (byteCountEvent == null)
					SendAsync("bytecount 1", false).Silent(BackgroundException);

				byteCountEvent += value;
			}
			remove
			{
				if (value == null)
					return;

				byteCountEvent -= value;
				if (byteCountEvent == null)
					SendAsync("bytecount 0", false).Silent(BackgroundException);

			}
		} // event ByteCountChanged

		#endregion

		#region -- Password -----------------------------------------------------------

		private async Task OnProcessPasswordAsync(string text)
		{
			if (text.StartsWith("Need ", StringComparison.OrdinalIgnoreCase))
			{
				var p = text.IndexOf('\'');
				var p2 = p >= 0 ? text.IndexOf('\'', p + 1) : -1;
				if (p2 > 0)
				{
					var name = text.Substring(p + 1, p2 - p - 1);
					var e = new OpenVpnNeedPasswordArgs(name,
						text.IndexOf("username", p2 + 1) > 0,
						text.IndexOf("password", p2 + 1) > 0
					);
					NeedPassword?.Invoke(this, e);

					if (e.Handled)
					{
						if (e.NeedUsername)
							await SendLineAsync($"username \"{name}\" \"{OpenVpnInfo.EscaseString(e.UserName)}\"");
						if (e.NeedPassword)
							await SendLineAsync($"username \"{name}\" \"{OpenVpnInfo.EscaseString(e.Password)}\"");
					}
				}
			}
			
			OnProcessLogLine(new OpenVpnLogLine(DateTime.Now, OpenVpnLineTyp.NoneFatalError, "Unknown password command: " + text));
		} // proc OnProcessPassword

		#endregion

		private void BackgroundException(Exception ex)
		{
			Debug.Print(ex.ToString());
		} // proc BackgroundException
	} // class OpenVpnConnection
}
