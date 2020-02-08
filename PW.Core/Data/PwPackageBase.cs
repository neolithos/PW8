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
using System.Net;
using Neo.IronLua;

namespace Neo.PerfectWorking.Data
{
	#region -- interface IPwObject ----------------------------------------------------

	/// <summary>Reference to a global registration.</summary>
	public interface IPwObject : IEquatable<IPwObject>, IDisposable
	{
		/// <summary>Owner of the object.</summary>
		IPwPackage Package { get; }
		/// <summary>Name of the object.</summary>
		string Name { get; }
		/// <summary>Type of the value</summary>
		object Value { get; set; }
	} // interface IPwObject

	#endregion

	#region -- interface IPwPackage ---------------------------------------------------

	/// <summary>Contains a implementation for the application.</summary>
	public interface IPwPackage
	{
		string Name { get; }
	} // interface IPwPackage

	#endregion

	#region -- interface IPwPackageServiceProvider ------------------------------------

	public interface IPwPackageServiceProvider
	{
		object GetPackageService(Type serviceType);
	} // interface IPwPackageServiceProvider

	#endregion

	#region -- interface IPwCollection ------------------------------------------------

	public interface IPwCollection<T> : IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged
		where T : class
	{
		T this[IPwObject index] { get; }
		T this[IPwPackage package, string name] { get; }

		object SyncRoot { get; }
	} // interface IPwCollection

	#endregion

	#region -- interface IPwGlobal ----------------------------------------------------

	/// <summary>Combines all implementations to one application.</summary>
	public interface IPwGlobal : IServiceProvider
	{
		/// <summary>Registers the value as global member.</summary>
		/// <param name="package"></param>
		/// <param name="name"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		IPwObject RegisterObject(IPwPackage package, string name, object value = null);

		/// <summary>A collection that collects local objects from different moduls.</summary>
		/// <typeparam name="T">Base type of the objects.</typeparam>
		/// <param name="package"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		IPwCollection<T> RegisterCollection<T>(IPwPackage package)
			where T : class;

		/// <summary>Gets the pointer to an collection.</summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		IPwCollection<T> GetCollection<T>()
			where T : class;

		/// <summary>Is the type assigned to an collection.</summary>
		/// <param name="type"></param>
		/// <returns></returns>
		bool IsCollectionType(Type type);

		/// <summary></summary>
		/// <param name="fileName"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		string ResolveFile(string fileName, bool throwException = true);
		/// <summary></summary>
		/// <param name="image"></param>
		/// <returns></returns>
		object ConvertImage(object image);

		/// <summary></summary>
		/// <param name="targetName"></param>
		/// <returns></returns>
		NetworkCredential GetCredential(string targetName);

		/// <summary>Access the shell implementation.</summary>
		IPwShellUI UI { get; }

		/// <summary></summary>
		LuaTable UserLocal { get; }
		/// <summary></summary>
		LuaTable UserRemote { get; }
	} // interface IPwGlobal

	#endregion

	#region -- class PwPackageAttribute -----------------------------------------------

	/// <summary>Marks a typ as an package</summary>
	[AttributeUsage(AttributeTargets.Assembly)]
	public class PwPackageAttribute : Attribute
	{
		private readonly Type type;

		public PwPackageAttribute(Type type)
		{
			this.type = type;
		} // ctor

		public Type Type => type;
	} // class PwPackageAttribute

	#endregion

	#region -- class PwPackageBase ----------------------------------------------------

	/// <summary>Scope implementation for a .net scope.</summary>
	public abstract class PwPackageBase : IPwPackage
	{
		private readonly IPwGlobal global;
		private readonly string packageName;

		#region -- Ctor/Dtor ----------------------------------------------------------

		protected PwPackageBase(IPwGlobal global, string packageName)
		{
			this.global = global ?? throw new ArgumentNullException(nameof(packageName));
			this.packageName = packageName ?? throw new ArgumentNullException(nameof(packageName));
		} // ctor

		public override string ToString()
			=> $"modul[{packageName}]";

		#endregion

		/// <summary>Name of the current modul.</summary>
		public string Name => packageName;
		/// <summary>Access to the global service.</summary>
		public IPwGlobal Global => global;
	} // class PwPackageBase

	#endregion

	#region -- class ServiceProviderHelper --------------------------------------------

	public static class ServiceProviderHelper
	{
		public static T GetService<T>(this IServiceProvider sp, bool throwException = false)
			where T : class
			=> GetService<T>(sp, typeof(T), throwException);

		public static T GetService<T>(this IServiceProvider sp, Type serviceType, bool throwException = false)
			where T : class
		{
			var obj = sp.GetService(typeof(T));
			if (obj == null && throwException)
				throw new ArgumentException($"Requested service '{typeof(T).Name}' not found.");
			return obj as T;
		} // func GetService
	} // class ServiceProviderHelper

	#endregion
}
