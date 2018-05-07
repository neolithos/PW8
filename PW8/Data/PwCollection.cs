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
using System.ComponentModel;
using System.Linq;

namespace Neo.PerfectWorking.Data
{
	#region -- interface IPwInternalCollection  ---------------------------------------

	internal interface IPwInternalCollection
	{
		void Append(IPwObject obj, object value);
		void Remove(IPwObject obj, object value);
		
		void RemoveAll(IPwPackage package);

		IPwPackage Package { get; }
		Type ItemType { get; }
	} // interface IPwInternalCollection

	#endregion

	#region -- class PwObjectId -------------------------------------------------------

	internal class PwObjectId : IPwObject
	{
		private readonly IPwPackage package;
		private readonly string name;

		public PwObjectId(IPwPackage package, string name)
		{
			this.package = package ?? throw new ArgumentNullException(nameof(package));
			this.name = name ?? throw new ArgumentNullException(nameof(name));
		} // ctor

		public override string ToString()
			=> $"objectId[{package.Name}/{name}]";

		public sealed override int GetHashCode()
			=> package.GetHashCode() ^ name.GetHashCode();

		public sealed override bool Equals(object obj)
			=> Equals(obj as IPwObject);

		public bool Equals(IPwObject obj)
			=> ReferenceEquals(this, obj) || (package.Equals(obj?.Package) && name.Equals(obj?.Name));

		protected virtual void Dispose(bool disposing) { }

		public void Dispose()
			=> Dispose(true);

		public IPwPackage Package => package;
		public string Name => name;

		public virtual object Value { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
	} // class PwObjectId

	#endregion
	
	#region -- class PwObjectCollection -----------------------------------------------

	internal class PwObjectCollection<T> : IPwInternalCollection, IPwCollection<T>, IList, IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged
		where T : class
	{
		public event NotifyCollectionChangedEventHandler CollectionChanged;
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly IPwPackage package;

		private readonly object syncRoot = new object();
		private readonly List<IPwObject> objects = new List<IPwObject>();
		
		public PwObjectCollection(IPwPackage package)
		{
			this.package = package;
		} // ctor

		#region -- OnPropertyChanged, OnCollectionChanged -----------------------------
		
		private void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		private void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
			=> CollectionChanged?.Invoke(this, e);

		#endregion

		#region -- Append, Clear, Remove ----------------------------------------------

		void IPwInternalCollection.Append(IPwObject obj, object value)
		{
			int index;
			lock (syncRoot)
			{
				index = objects.IndexOf(obj);
				if (index >= 0)
					return;

				index = objects.Count;
				objects.Add(obj);
			}
			
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value, index));
		} // proc IPwInternalCollection.Append

		void IPwInternalCollection.Remove(IPwObject obj, object value)
		{
			int index;
			lock (syncRoot)
			{
				index = objects.IndexOf(obj);
				if (index >= 0)
					RemoveAt(index, value);
				else
					return;
			}

			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, value, index));
		} // proc IPwInternalCollection.Remove

		void IPwInternalCollection.RemoveAll(IPwPackage package)
		{
			lock (syncRoot)
			{
				for (var i = objects.Count - 1; i >= 0; i--)
				{
					if (objects[i].Package.Equals(package))
						RemoveAt(i, objects[i].Value);
				}
			}

			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		} // proc IPwInternalCollection.RemoveAll

		private void RemoveAt(int index, object value)
			=> objects.RemoveAt(index);
		
		Type IPwInternalCollection.ItemType => typeof(T);
		IPwPackage IPwInternalCollection.Package => package;
		
		public bool Contains(T obj)
			=> IndexOf(obj) >= 0;

		public int IndexOf(T obj)
		{
			lock (syncRoot)
				return objects.FindIndex(c => Object.Equals(c.Value, obj));
		} // func IndexOf

		public int IndexOf(IPwObject obj)
		{
			lock (syncRoot)
				return objects.FindIndex(obj.Equals);
		} // func IndexOf

		public int IndexOf(IPwPackage package, string name)
			=> IndexOf(new PwObjectId(package, name));

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		public IEnumerator<T> GetEnumerator()
			=> (from c in objects select (T)c.Value).GetEnumerator();

		void ICollection.CopyTo(Array array, int index)
		{
			lock (syncRoot)
			{
				var j = index;
				for (var i = 0; i < objects.Count; i++)
				{
					if (objects[i].Value != null)
						array.SetValue(objects[i].Value, j++);
				}
			}
		} // proc ICollection.CopyTo

		bool IList.Contains(object value) => IndexOf(value as T) >= 0;
		int IList.IndexOf(object value) => IndexOf(value as T);

		int IList.Add(object value) => throw new NotSupportedException();
		void IList.Clear() => throw new NotSupportedException();
		void IList.Insert(int index, object value) => throw new NotSupportedException();
		void IList.Remove(object value) => throw new NotSupportedException();
		void IList.RemoveAt(int index) => throw new NotSupportedException();
		
		bool IList.IsReadOnly => true;
		bool IList.IsFixedSize => false;
		bool ICollection.IsSynchronized => true;

		object IList.this[int index] { get { lock (syncRoot) return objects[index].Value; } set => throw new NotSupportedException(); }

		#endregion

		public object SyncRoot => syncRoot;

		public T this[int index] { get { lock (syncRoot) return (T)objects[index].Value; } }

		public T this[IPwPackage package, string name]
			=> this[new PwObjectId(package, name)];

		public T this[IPwObject member]
		{
			get
			{
				lock (syncRoot)
				{
					var idx = IndexOf(member);
					return idx == -1 ? null : (T)objects[idx].Value;
				}
			}
		} // prop this

		public int Count { get { lock (syncRoot) return objects.Count; } }
	} // class PwObjectCollection

	#endregion
}

