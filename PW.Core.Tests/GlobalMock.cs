using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Neo.PerfectWorking.Data;

namespace PW.Core.Tests
{
	internal class ShellUIMock : IPwShellUI
	{
		public DirectoryInfo ApplicationRemoteDirectory => throw new NotImplementedException();

		public DirectoryInfo ApplicationLocalDirectory => throw new NotImplementedException();

		public IPwIdleAction AddIdleAction(IPwIdleAction idleAction)
			=> idleAction;

		public void RemoveIdleAction(IPwIdleAction idleAction)
		{
		}

		public void BeginInvoke(Action action) => throw new NotImplementedException();
		public Task InvokeAsync(Action action) => throw new NotImplementedException();
		public Task<T> InvokeAsync<T>(Func<T> action) => throw new NotImplementedException();
		public string MsgBox(string text, string caption = null, object icon = null, object buttons = null, object result = null) => throw new NotImplementedException();
		public Task<string> MsgBoxAsync(string text, string caption = null, object icon = null, object buttons = null, object result = null) => throw new NotImplementedException();
		public void ShowException(string text, Exception e) => throw new NotImplementedException();
		public void ShowNotification(object message, object image = null) => throw new NotImplementedException();
	}

	internal class GlobalMock : IPwGlobal
	{
		private readonly static ShellUIMock shellUIMock = new ShellUIMock(); 

		public IPwShellUI UI => shellUIMock;

		public object ConvertImage(object image) => throw new NotImplementedException();
		public IPwCollection<T> GetCollection<T>() where T : class => throw new NotImplementedException();
		public NetworkCredential GetCredential(string targetName, string serviceName) => throw new NotImplementedException();
		public object GetService(Type serviceType) => throw new NotImplementedException();
		public bool IsCollectionType(Type type) => throw new NotImplementedException();
		public IPwCollection<T> RegisterCollection<T>(IPwPackage package) where T : class => throw new NotImplementedException();
		public IPwObject RegisterObject(IPwPackage package, string name, object value = null) => throw new NotImplementedException();
		public string ResolveFile(string fileName, bool throwException = true) => throw new NotImplementedException();

		public static GlobalMock Instance { get; } = new GlobalMock();
	}
}
