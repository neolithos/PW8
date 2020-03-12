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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Neo.IronLua;
using Neo.PerfectWorking.Stuff;
using Neo.PerfectWorking.UI;
using Neo.PerfectWorking.UI.Dashboard;
using TecWare.DE.Stuff;

namespace Neo.PerfectWorking.Data
{
	#region -- class PwPackageInitializationException ---------------------------------

	public class PwPackageInitializationException : Exception
	{
		private readonly IPwPackage package;

		public PwPackageInitializationException(IPwPackage package, Exception innerException)
			: base("Package initialization failed.", innerException)
		{
			this.package = package;
		}

		public IPwPackage Package => package;
	} // class PwPackageInitializationException

	#endregion

	#region -- class PwGlobal ---------------------------------------------------------

	/// <summary>DataContext for the main window.</summary>
	internal class PwGlobal : LuaGlobal, IPwGlobal, IPwPackage
	{
		#region -- struct PwPackageVariable -------------------------------------------

		private struct PwPackageVariable
		{
			public PwPackageVariable(IPwPackage package, string variableName)
			{
				Package = package ?? throw new ArgumentNullException(nameof(package));
				VariableName = variableName ?? throw new ArgumentNullException(nameof(variableName));
			} // ctor

			public IPwPackage Package { get; }

			public string VariableName { get; }
		} // struct PwPackageVariable

		#endregion

		#region -- class PwObject -----------------------------------------------------

		private sealed class PwObject : PwObjectId, IPwObject
		{
			private readonly PwGlobal global;

			private object value = null;
			private bool isDisposed = false;

			#region -- Ctor/Dtor ------------------------------------------------------------

			public PwObject(PwGlobal global, IPwPackage package, string name)
				: base(package, name)
			{
				this.global = global ?? throw new ArgumentNullException(nameof(global));
			} // ctor


			public override string ToString()
				=> $"object[{Package.Name}/{Name}]";

			protected override void Dispose(bool disposing)
			{
				if (!isDisposed)
				{
					// remove value
					Value = null;
					// remove from list
					global.ObjectRemove(this);

					// mark es disposed
					isDisposed = true;
				}
			} // proc Dispose

			#endregion

			public override object Value
			{
				get => value;
				set
				{
					if (isDisposed)
						throw new ObjectDisposedException(ToString());

					if (this.value != value)
					{
						var old = this.Value;
						this.value = value;

						if (old == null) // value added
							global.CollectionsAdd(this, value);
						else if (value == null) // value removed
						{
							global.CollectionsRemove(this, old);
						}
						else // value replaced
						{
							global.CollectionsRemove(this, old);
							global.CollectionsAdd(this, value);
						}
					}
				}
			} // prop Value
		} // class PwObject

		#endregion

		public const string ScopeName = "global";

		private readonly App app;
		private readonly IPwObject userRemote;
		private readonly IPwObject userLocal;

		private readonly LuaCompileOptions compileOptions;

		private readonly List<IPwPackage> packages = new List<IPwPackage>(); // list of active packages
		private readonly List<IPwObject> objects = new List<IPwObject>(); // list of all registered objects
		private readonly List<IPwInternalCollection> collections = new List<IPwInternalCollection>(); // list of active collections
		private readonly List<PwPackageVariable> packageVariables = new List<PwPackageVariable>(); // list of all global variables

		private List<IPwPackage> currentInitializedPackages = null;
		private List<PwPackageInitializationException> currentInitializationExceptions = null;
		private IPwPackage currentPackage = null;

		private readonly List<string> resolvePaths = new List<string>();
		private readonly IPwCollection<IPwPackageServiceProvider> serviceProviders;
		private readonly IPwCollection<IPwAutoSaveFile> autoSaveFiles;
		private readonly IPwCollection<ICredentials> credentials;

		private readonly IPwIdleAction autoSaveFilesIdleAction;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PwGlobal(App app, string configurationFile)
			: base(new Lua())
		{
			this.app = app;
			ConfigurationFile = configurationFile;
			compileOptions = new LuaCompileOptions() { DebugEngine = LuaStackTraceDebugger.Default };

			Actions = RegisterCollection<IPwAction>(this);
			WindowHooks = RegisterCollection<PwWindowHook>(this);
			serviceProviders = RegisterCollection<IPwPackageServiceProvider>(this);
			autoSaveFiles = RegisterCollection<IPwAutoSaveFile>(this);
			credentials = RegisterCollection<ICredentials>(this);

			// add resolver paths
			AddResolvePath(Path.GetDirectoryName(typeof(PwGlobal).Assembly.Location));
			AddResolvePath(Path.GetDirectoryName(configurationFile));

			// load options
			userLocal = RegisterObject(this, nameof(IPwGlobal.UserLocal), new PwConfigTable(Path.Combine(app.ApplicationLocalDirectory.FullName, "global.xml")));
			userRemote = RegisterObject(this, nameof(IPwGlobal.UserRemote), new PwConfigTable(Path.Combine(app.ApplicationRemoteDirectory.FullName, "global.xml")));

			// default widgets
			RegisterObject(this, nameof(LogWidget), LogWidget.Factory);
			RegisterObject(this, nameof(TextWidget), TextWidget.Factory);
			RegisterObject(this, nameof(NetworkInterfaceWidget), NetworkInterfaceWidget.Factory);

			RegisterObject(this, "Log", Log.Default);

			autoSaveFilesIdleAction = AddIdleAction(AutoSaveFilesIdle);
		} // ctor

		public void Dispose()
		{
			while (packages.Count > 0)
				RemovePackage(packages[packages.Count - 1]);

			RemoveIdleAction(autoSaveFilesIdleAction);
		} // proc Dispose

		public override string ToString()
			=> "global";

		public override int GetHashCode()
			=> ScopeName.GetHashCode();

		public override bool Equals(object obj)
			=> Equals(obj as IPwPackage);

		public bool Equals(IPwPackage obj)
			=> ReferenceEquals(this, obj) || ScopeName.Equals(obj?.Name);

		#endregion

		#region -- Object Manager -----------------------------------------------------

		private IPwInternalCollection FindCollection(IPwPackage package, Type itemType)
		{
			if (package == null)
				throw new ArgumentNullException(nameof(package));
			if (itemType == null)
				throw new ArgumentNullException(nameof(itemType));

			lock (collections)
				return collections.FirstOrDefault(c => c.Package == package && c.ItemType == itemType);
		} // func FindCollection

		private int FindObjectIndex(IPwPackage package, string name)
			=> objects.IndexOf(new PwObjectId(package, name));

		public IPwObject RegisterObject(IPwPackage package, string name, object value = null)
		{
			lock (collections)
			{
				// remove current object
				var index = FindObjectIndex(package, name);
				if (index >= 0)
					objects[index].Dispose();

				// create new object
				var obj = new PwObject(this, package, name);
				objects.Add(obj);
				obj.Value = value;
				return obj;
			}
		} // proc RegisterObject

		private void ObjectRemove(PwObject obj)
		{
			lock (collections)
				objects.Remove(obj);
		} // proc ObjectRemove

		public IEnumerable<T> EnumerateObjects<T>()
		{
			lock (collections)
			{
				foreach (var c in objects.Select(o => o.Value).OfType<T>())
					yield return c;
			}
		} // func EnumerateObjects

		public IPwCollection<T> RegisterCollection<T>(IPwPackage package)
			where T : class
		{
			lock (collections)
			{
				var collection = FindCollection(package, typeof(T));
				if (collection == null)
				{
					collection = new PwObjectCollection<T>(package);
					collections.Add(collection);

					// add objects
					foreach (var obj in objects)
					{
						if (CheckCollectionAssignable(collection, obj))
							collection.Append(obj, obj.Value);
					}
				}
				return (IPwCollection<T>)collection;
			}
		} // func RegisterCollection

		public bool IsCollectionType(IPwPackage package, Type itemType)
			=> FindCollection(package, itemType) != null;

		public IPwCollection<T> GetCollection<T>(IPwPackage package)
			where T : class
			=> (IPwCollection<T>)FindCollection(package, typeof(T));

		private void CollectionsAdd(IPwObject obj, object value)
		{
			lock (collections)
			{
				foreach (var c in collections)
				{
					if (CheckCollectionAssignable(c, obj))
						c.Append(obj, value);
				}
			}
		} // proc CollectionsAdd

		private void CollectionsRemove(IPwObject obj, object value)
		{
			lock (collections)
			{
				foreach (var c in collections)
					c.Remove(obj, value);
			}
		} // proc CollectionsRemove

		private static bool CheckItemTypeAssignable(Type itemType, IPwObject obj)
			=> obj.Value != null && itemType.IsAssignableFrom(obj.Value.GetType());

		private static bool CheckCollectionAssignable(IPwInternalCollection collection, IPwObject obj)
			=> CheckItemTypeAssignable(collection.ItemType, obj);

		public object GetService(Type serviceType)
		{
			if (serviceType == typeof(IPwShellUI))
				return app;
			else if (serviceType == typeof(Dispatcher))
				return app.Dispatcher;
			else if (serviceType == typeof(IPwGlobal))
				return this;
			else
			{
				lock (serviceProviders.SyncRoot)
				{
					foreach (var c in serviceProviders)
					{
						var r = c.GetPackageService(serviceType);
						if (r != null)
							return r;
					}
				}
				return null;
			}
		} // func GetService

		#endregion

		#region -- Assembly Manager ---------------------------------------------------

		[LuaMember]
		private void AddResolvePath(string path)
		{
			if (String.IsNullOrEmpty(path))
				return;
			else if (path[path.Length - 1] == '\\')
				path = path.Substring(0, path.Length - 1);

			path = Path.GetFullPath(path);
			if (!resolvePaths.Exists(c => String.Compare(c, path, StringComparison.OrdinalIgnoreCase) == 0))
				resolvePaths.Add(path);
		} // proc AddResolvePath

		[LuaMember]
		public string ResolveFile(string fileName, bool throwException = true)
		{
			if (Path.IsPathRooted(fileName))
				return fileName;

			var currentFileInfo = (FileInfo)null;
			foreach (var p in resolvePaths)
			{
				var tmp = new FileInfo(Path.GetFullPath(Path.Combine(p, fileName)));
				if (tmp.Exists && (currentFileInfo == null || currentFileInfo.LastWriteTime < tmp.LastWriteTime))
					currentFileInfo = tmp;
			}

			if (currentFileInfo == null)
			{
				if (throwException)
					throw new ArgumentNullException($"Could not resolve file '{fileName}'.");
				else
					return null;
			}
			else
				return currentFileInfo.FullName;
		} // proc ResolveFile

		private Assembly ResolveAssembly(string assemblyFile)
		{
			var fileName = ResolveFile(assemblyFile);

			return AppDomain.CurrentDomain.GetAssemblies().Where(c => !c.IsDynamic && String.Compare(c.Location, fileName, StringComparison.OrdinalIgnoreCase) == 0).FirstOrDefault()
				?? Assembly.LoadFile(fileName);
		} // func ResolveAssembly

		private Type ResolvePackageType(string packageType)
		{
			var p = packageType.IndexOf(';');
			if (p == -1)
				return Type.GetType(packageType, true);
			else
			{
				var assemblyFile = packageType.Substring(0, p);
				var assembly = ResolveAssembly(assemblyFile);
				if (assembly == null)
					throw new ArgumentException($"Could not load assembly '{assemblyFile}'.");

				var typeName = packageType.Substring(p + 1);
				var type = assembly.GetCustomAttributes<PwPackageAttribute>().Where(c => c.Type.Name == typeName).Select(c => c.Type).FirstOrDefault();
				if (type == null)
					throw new ArgumentException($"Could not find type '{typeName}' in assembly '{assembly.Location}'.");

				return type;
			}
		} // func ResolvePackageType

		#endregion

		#region -- Package Manager ----------------------------------------------------

		private int FindPackageIndex(string name)
			=> packages.FindIndex(c => c.Name == name);

		/// <summary>This functions registers a new package.</summary>
		private IPwPackage RegisterPackage(string packageName, Type packageType, Func<IPwPackage> packageCreation)
		{
			if (currentInitializedPackages == null)
				throw new InvalidOperationException("RegisterPackage is only allowed in the configuration phase.");

			// is the package initialized
			if (currentInitializedPackages.Exists(c => c.Name == packageName))
				throw new ArgumentOutOfRangeException($"{packageName} already registered.");

			// first we only check the name
			IPwPackage initPackage;
			var index = FindPackageIndex(packageName);
			if (index == -1)
				initPackage = packageCreation();
			else
			{
				var oldPackage = packages[index];
				if (oldPackage.GetType() != packageType) // needs the package recreated
				{
					RemovePackage(oldPackage);
					initPackage = packageCreation();
				}
				else // package type is the same, do a dirty refresh
					initPackage = oldPackage;
			}

			if (initPackage == null)
				throw new ArgumentNullException(nameof(initPackage));

			currentInitializedPackages.Add(initPackage);
			return initPackage;
		} // proc RegisterPackage

		private IPwPackage CreatePackageType(Type packageType)
		{
			if (!typeof(IPwPackage).IsAssignableFrom(packageType))
				throw new ArgumentException($"Type '{packageType.Name}' does not implement '{nameof(IPwPackage)}'.");

			return (IPwPackage)Activator.CreateInstance(packageType, this);
		} // func CreatePackageType

		[LuaMember("package")]
		public object InitPackage(string packageName, object initPackageCode)
		{
			if (String.IsNullOrEmpty(packageName))
				throw new ArgumentNullException(nameof(packageName));
			if (currentPackage != null)
				throw new InvalidOperationException($"Nested packages are not allowed ('{packageName}').");

			try
			{
				switch (initPackageCode)
				{
					case string fileName: // load lua based package (package content is a file)
						var c = CompileFile(ResolveFile(fileName));
						currentPackage = RegisterPackage(packageName, typeof(PwLuaPackage), () => new PwLuaPackage(this, packageName));
						c.Run((LuaTable)currentPackage);
						return currentPackage;
					case Delegate function: // load lua based package (package is defined by a function)
						currentPackage = RegisterPackage(packageName, typeof(PwLuaPackage), () => new PwLuaPackage(this, packageName));
						Lua.RtInvoke(function, currentPackage);
						return currentPackage;
					case null: // load assembly based package
						var packageType = ResolvePackageType(packageName);
						return RegisterPackage(packageType.Name, packageType, () => CreatePackageType(packageType));
					default:
						throw new ArgumentOutOfRangeException(nameof(initPackageCode));
				}
			}
			catch (Exception e)
			{
				try
				{
					RemovePackage(currentPackage);
				}
				catch { }

				currentInitializationExceptions.Add(new PwPackageInitializationException(currentPackage, e));
				return null;
			}
			finally
			{
				currentPackage = null;
			}
		}  // proc InitScope

		private void RemovePackage(IPwPackage package)
		{
			lock (collections)
			{
				// remove list objects
				for (var i = collections.Count - 1; i >= 0; i--)
				{
					var c = collections[i];
					if (c.Package == package)
						collections.Remove(c);
					else
						c.RemoveAll(package);
				}

				// remove objects
				for (var i = objects.Count - 1; i >= 0; i--)
				{
					if (objects[i].Package == package)
						objects[i].Dispose();
				}

				// remove variables
				for (var i = packageVariables.Count - 1; i >= 0; i--)
				{
					if (packageVariables[i].Package == package)
					{
						// clear variable
						var variableName = packageVariables[i].VariableName;
						var value = GetMemberValue(variableName, rawGet: true);
						if (value != package) // no double dispose
							FreeObject(value);
						SetMemberValue(variableName, null, rawSet: true);

						// remove variable
						packageVariables.RemoveAt(i);
					}
				}

				// remove scope
				packages.Remove(package);

				// call possible dispose
				FreeObject(package);
			}
		} // proc RemoveScope

		internal static void FreeObject(object obj)
		{
			if (obj is IDisposable t)
				t.Dispose();
		} // proc FreeObject

		#endregion

		#region -- Auto Save Files Manager --------------------------------------------

		private DateTime? GetLastWriteTimeSecure(string fullPath)
		{
			try
			{
				return File.GetLastWriteTime(fullPath);
			}
			catch
			{
				return null;
			}
		} // func GetLastWriteTimeSecure

		private bool AutoSaveFilesIdle(int elapsed)
		{
			if (elapsed > 100)
			{
				lock (autoSaveFiles.SyncRoot)
				{
					foreach (var c in autoSaveFiles)
					{
						if (c.IsModified)
							c.Save();
						else if (c.FileName.Length > 3 && c.FileName.Substring(1, 2) == ":\\")
						{
							var dt = GetLastWriteTimeSecure(c.FileName);
							if (dt.HasValue && dt.Value != c.LastModificationTime)
								c.Reload();
						}
					}
				}
				return false;
			}
			else
				return true;
		} // proc AutoSaveFilesChanged

		#endregion

		#region -- Lua ----------------------------------------------------------------

		private LuaChunk CompileFile(string fileName)
			=> Lua.CompileChunk(fileName, compileOptions);

		private void ShowSyntaxException(LuaParseException e)
			=> UI.ShowException($"Parse Exception {e.FileName}:{e.Line}", e);

		[LuaMember("require")]
		public LuaResult LuaRequire(object modname)
			=> DoChunk(CompileFile(ResolveFile((string)modname)));

		#endregion

		#region -- Idle ---------------------------------------------------------------

		#region -- class FunctionIdleActionImplementation -----------------------------

		private sealed class FunctionIdleActionImplementation : IPwIdleAction
		{
			private readonly Func<int, bool> onIdle;

			public FunctionIdleActionImplementation(Func<int, bool> onIdle)
				=> this.onIdle = onIdle ?? throw new ArgumentNullException("onIdle");

			public bool OnIdle(int elapsed)
				=> onIdle(elapsed);
		} // class FunctionIdleActionImplementation

		#endregion

		[LuaMember]
		public IPwIdleAction AddIdleAction(Func<int, bool> onIdle)
			=> UI.AddIdleAction(new FunctionIdleActionImplementation(onIdle));

		[LuaMember]
		public void RemoveIdleAction(IPwIdleAction idleAction)
			=> UI.RemoveIdleAction(idleAction);

		#endregion

		#region -- Configuration ------------------------------------------------------

		public void RefreshConfiguration()
		{
			void RemoveExceptionPackage(IPwPackage package)
			{
				if (package != null)
					RemovePackage(package);
			} // proc RemoveSemiInitializedObjects

			if (currentInitializedPackages != null)
				throw new InvalidOperationException("Can not call refresh, within an refresh.");

			currentInitializedPackages = new List<IPwPackage> { this };
			currentInitializationExceptions = new List<PwPackageInitializationException>();
			currentPackage = null;

			Log.Default.StartRefreshCongifuration(ConfigurationFile);

			// run init script
			try
			{
				var sw = new Stopwatch();
				var c = CompileFile(ConfigurationFile);
				var compileTime = sw.ElapsedMilliseconds;
				sw.Restart();

				c.Run(this);
				var runTime = sw.ElapsedMilliseconds;
				sw.Restart();


				// clear out of scope variables
				for (var i = packages.Count - 1; i >= 0; i--)
				{
					var idx = currentInitializedPackages.IndexOf(packages[i]);
					if (idx == -1) // package is not initialized -> remove
						RemovePackage(packages[i]);
					else // package is already loaded -> nothing
						currentInitializedPackages.RemoveAt(idx);
				}

				// not touched packages -> add all
				packages.AddRange(currentInitializedPackages);

				var cleanTime = sw.ElapsedMilliseconds;
				
				if (currentInitializationExceptions.Count > 0)
				{
					foreach (var e in currentInitializationExceptions)
						UI.ShowException(e);
				}

				Log.Default.FinishRefreshCongifuration(ConfigurationFile, compileTime, runTime, cleanTime);
			}
			catch (LuaParseException e)
			{
				RemoveExceptionPackage(currentPackage);
				ShowSyntaxException(e);
			}
			catch (Exception e)
			{
				RemoveExceptionPackage(currentPackage);
				UI.ShowException(e);
			}
			finally
			{
				currentInitializedPackages = null;
				currentPackage = null;
			}
		} // proc RefreshConfiguration

		#endregion

		#region -- LuaTable -----------------------------------------------------------

		protected override bool OnNewIndex(object key, object value)
		{
			if (key is string variableName)
			{
				var varIndex = packageVariables.FindIndex(c => c.VariableName == variableName);
				var newVarPackage = value as IPwPackage ?? currentPackage;
				if (varIndex == -1)
				{
					if (newVarPackage == null)
						throw new ArgumentException("Can not create member, no scope defined.");
					packageVariables.Add(new PwPackageVariable(newVarPackage, variableName));
					return false;
				}
				else
				{
					if (newVarPackage == null)
						throw new ArgumentException($"After the initialization the member '{variableName}' is read only.");
					else
					{
						var curVarPackage = packageVariables[varIndex].Package;
						if (curVarPackage == newVarPackage)
							return false; // update the existing variable
						else if (currentInitializedPackages.Contains(curVarPackage)) // this var scope is initialized.
							throw new ArgumentException($"'{variableName}' is registered with package '{packageVariables[varIndex].Package.Name}' and can not registered with '{newVarPackage.Name}'.");
						else // update scope of the variable
						{
							packageVariables[varIndex] = new PwPackageVariable(newVarPackage, variableName);
							return false;
						}
					}
				}
			}
			else
				throw new ArgumentException("Only member are allowed in the global scope.");
		} // func OnNewIndex

		#endregion

		#region -- Lua Global Extensions ----------------------------------------------

		#region -- class FunctionBinding  ---------------------------------------------

		private sealed class FunctionBinding
		{
			private readonly object function;
			private readonly Func<PwAction, Task> executeFunction;

			public FunctionBinding(PwAction button, object function)
			{
				this.function = function;
				this.executeFunction = new Func<PwAction, Task>(
					a => Task.Run(() => ExecuteFunction(a))
				);

				button.Execute = executeFunction;
			} // ctor

			private void ExecuteFunction(PwAction a)
			{
				a.Execute = null;
				try
				{
					Lua.RtInvoke(function, a);
				}
				finally
				{
					a.Execute = executeFunction;
				}
			} // proc ExecuteFunction
		} // class FunctionBinding

		#endregion

		#region -- class PwNoneUIHotKey -----------------------------------------------

		private class PwNoneUIHotKey : IPwHotKey
		{
			public event EventHandler CanExecuteChanged { add => command.CanExecuteChanged += value; remove => command.CanExecuteChanged -= value; }

			private readonly ICommand command;

			public PwNoneUIHotKey(PwKey key, ICommand command)
			{
				if (key == PwKey.None)
					throw new ArgumentException(nameof(key));
				Key = key;

				this.command = command ?? throw new ArgumentNullException(nameof(command));
			} // ctor

			public bool CanExecute(object parameter)
				=> command.CanExecute(parameter);

			public void Execute(object parameter) 
				=> command.Execute(parameter);

			public PwKey Key { get; }
		} // class PwNoneUIHotKey

		#endregion

		#region -- class PwNoneUIHotKey -----------------------------------------------

		private sealed class PwUIHotKey : PwNoneUIHotKey, IPwUIHotKey
		{
			public PwUIHotKey(string title, object image, PwKey key, ICommand command) 
				: base(key, command)
			{
				Title = title ?? throw new ArgumentNullException(nameof(title));
				Image = image;
			} // ctor

			public string Title { get; }
			public object Image { get; }
		} // class PwUIHotKey

		#endregion

		#region -- class PwActionCommand ----------------------------------------------

		private sealed class PwActionCommand : ICommand
		{
			public event EventHandler CanExecuteChanged;

			private readonly object func;
			private readonly object canFunc;

			public PwActionCommand(object func, object canFunc)
			{
				this.func = func ?? throw new ArgumentNullException(nameof(func));
				this.canFunc = canFunc;
			} // ctor

			public void Refresh()
				=> CanExecuteChanged?.Invoke(this, EventArgs.Empty);

			public bool CanExecute(object parameter)
				=> canFunc != null ? Procs.ChangeType<bool>(new LuaResult(Lua.RtInvoke(canFunc, parameter))[0]) : true;

			public void Execute(object parameter)
				=> Lua.RtInvoke(func);

			public static ICommand Create(object command)
				=> command is ICommand c ? c : new PwActionCommand(command, null);
		} // class PwActionCommand

		#endregion

		[LuaMember]
		public ICommand CreateCommand(object func, object canFunc)
			=> new PwActionCommand(func, canFunc);

		[LuaMember]
		public PwWindowHook CreateHook(PwWindowProc hook, params int[] messageFilter)
			=> new PwWindowHook(hook, messageFilter);

		[LuaMember]
		public PwAction CreateAction(object image, string title, string label, object func, string key)
		{
			var button = new PwAction(this, title, label, PwKey.Parse(key, CultureInfo.InvariantCulture), image);

			if (func != null)
				new FunctionBinding(button, func);

			return button;
		} // func CreateAction

		[LuaMember]
		public IPwHotKey CreateHotKey(string key, object command, string title = null, object image = null)
		{
			return String.IsNullOrEmpty(title)
				? new PwNoneUIHotKey(PwKey.Parse(key, CultureInfo.InvariantCulture), PwActionCommand.Create(command))
				: new PwUIHotKey(title, image, PwKey.Parse(key, CultureInfo.InvariantCulture), PwActionCommand.Create(command));
		} // func CreateHotKey

		#endregion

		#region -- Keyboard functions -------------------------------------------------

		[LuaMember]
		public void SendKeyData(string data)
		{
			if (String.IsNullOrEmpty(data))
				return;

			var keyList = new PwKeyList();
			foreach (var c in data)
				keyList.Append(c);

			Log.Default.SendKeyData(data);
			keyList.Send();
		} // proc SendKeyData

		[LuaMember]
		public void SendKeyDown(string key)
		{
			Log.Default.SendKeyDown(key);
			PwKey.Parse(key).SendKey(PwKeySend.Down);
		} // proc SendKeyDown

		[LuaMember]
		public void SendKeyUp(string key)
		{
			Log.Default.SendKeyUp(key);
			PwKey.Parse(key).SendKey(PwKeySend.Up);
		} // proc SendKeyUp
		[LuaMember]
		public void SendKey(string key)
		{
			Log.Default.SendKey(key);
			PwKey.Parse(key).SendKey(PwKeySend.Both);
		} // proc SendKey

		#endregion

		[LuaMember]
		public GlobalWidgetFactory NewDashboard()
			=> new GlobalWidgetFactory(this, app.DashBoardWindow);

		[LuaMember]
		public object ConvertImage(object image)
		{
			if (image is string f)
				image = ResolveFile(f, false);

			if (image == null)
				return null;
			else if (image is ImageSource)
				return image;
			else
			{
				var img = new ImageSourceConverter();
				return img.ConvertFrom(image);
			}
		} // func ConvertImage

		[LuaMember]
		public NetworkCredential GetCredential(string targetName)
		{
			lock (credentials.SyncRoot)
			{
				foreach (var c in credentials)
				{
					var nc = c.GetCredential(new Uri(targetName, UriKind.RelativeOrAbsolute), String.Empty);
					if (nc != null)
					{
						Log.Default.CredentialReturned(targetName, nc.Domain, nc.UserName);
						return nc;
					}
				}
			}

			Log.Default.CredentialNotReturned(targetName);
			return null;
		} // func GetCredential

		protected override void OnPrint(string text)
		{
			Log.Default.LuaPrint(text);
			base.OnPrint(text);
		} // proc OnPrint

		[LuaMember]
		public IPwCollection<IPwAction> Actions { get; }

		[LuaMember]
		public IPwCollection<PwWindowHook> WindowHooks { get; }

		[LuaMember]
		public IPwShellUI UI => app;

		string IPwPackage.Name => ScopeName;

		/// <summary>Returns the configuration file.</summary>
		public string ConfigurationFile { get; }

		LuaTable IPwGlobal.UserLocal => (LuaTable)userLocal.Value;
		LuaTable IPwGlobal.UserRemote => (LuaTable)userRemote.Value;

	} // class PwGlobal

	#endregion
}
