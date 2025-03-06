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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Neo.IronLua;
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

	internal class ObjectMock : IPwObject
	{
		private readonly IPwPackage package;
		private readonly string name;
		private object value;

		public ObjectMock(IPwPackage package, string name, object value)
		{
			this.package = package;
			this.name = name;
			this.value = value;
		}

		public void Dispose()
		{
			if (value is IDisposable d)
				d.Dispose();
		}

		public IPwPackage Package => package;
		public string Name => name;
		public object Value { get => value; set => this.value = value; }

		public bool Equals(IPwObject other)
		{
			throw new NotImplementedException();
		}
	}

	internal class CollectionMock<T> : List<T>, IPwCollection<T>
		where T : class
	{
		public CollectionMock(IPwPackage package)
		{
		}

		public T this[IPwObject index] => throw new NotImplementedException();

		public T this[IPwPackage package, string name] => throw new NotImplementedException();

		public object SyncRoot => throw new NotImplementedException();

		public event NotifyCollectionChangedEventHandler CollectionChanged;
		public event PropertyChangedEventHandler PropertyChanged;
	}

	internal class GlobalMock : IPwGlobal
	{
		private readonly static ShellUIMock shellUIMock = new ShellUIMock();

		public IPwCollection<T> RegisterCollection<T>(IPwPackage package)
			where T : class
			=> new CollectionMock<T>(package);

		public IPwObject RegisterObject(IPwPackage package, string name, object value = null)
			=> new ObjectMock(package, name, value);

		public IPwShellUI UI => shellUIMock;

		public object ConvertImage(object image) => throw new NotImplementedException();
		public IPwCollection<T> GetCollection<T>(IPwPackage package) where T : class => throw new NotImplementedException();
		public NetworkCredential GetCredential(string targetName) => throw new NotImplementedException();
		public object GetService(Type serviceType) => throw new NotImplementedException();
		public bool IsCollectionType(IPwPackage package, Type type) => throw new NotImplementedException();

		public string ResolveFile(string fileName, bool throwException = true) => throw new NotImplementedException();
		public IEnumerable<T> EnumerateObjects<T>() => throw new NotImplementedException();

		public static GlobalMock Instance { get; } = new GlobalMock();

		public LuaTable UserLocal => throw new NotImplementedException();
		public LuaTable UserRemote => throw new NotImplementedException();
	}
}
