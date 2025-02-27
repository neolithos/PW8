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
using Neo.PerfectWorking.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using TecWare.DE.Stuff;

namespace Neo.PerfectWorking.Cred.Provider
{
	#region -- class FileCredentialProviderBase ---------------------------------------

	internal abstract class FileCredentialProviderBase : ICredentialProvider, IPwAutoSaveFile
	{
		#region -- class CredentialItemBase -------------------------------------------

		protected abstract class CredentialItemBase : ICredentialInfo, IXmlCredentialItem
		{
			protected enum PropertyName
			{
				UserName = 0,
				Comment,
				LastWritten,
				EncryptedPassword,
				IsVisible
			} // enum PropertyName

			public event PropertyChangedEventHandler PropertyChanged;

			private readonly FileCredentialProviderBase provider;
			private bool isTouched = true;

			private readonly bool[] propertyChanged = new bool[] { false, false, false, false, false };

			public CredentialItemBase(FileCredentialProviderBase provider)
			{
				this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
			} // ctor

			public bool GetTouched()
			{
				var s = isTouched;
				isTouched = false;
				return s;
			} // func GetTouched

			protected abstract bool UpdateCore(IXmlCredentialItem other);

			public bool Update(IXmlCredentialItem other)
			{
				isTouched = true;
				return UpdateCore(other);
			} // func Update

			protected abstract bool RemoveCore();

			public bool Remove()
				=> RemoveCore();

			public void NotifyPropertyChanged()
			{
				for (var i = 0; i < propertyChanged.Length; i++)
				{
					if (propertyChanged[i])
						OnPropertyChanged((PropertyName)i, false);
					propertyChanged[i] = false;
				}
			} // proc NotifyPropertyChanged

			protected void OnPropertyChanged(PropertyName property, bool late)
			{
				if (late)
					propertyChanged[(int)property] = true;
				else
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property.ToString()));
			} // proc OnPropertyChanged

			protected void OnPropertyChanged(XmlCredentialProperty props, bool late)
			{
				if ((props & XmlCredentialProperty.UserName) != 0)
					OnPropertyChanged(PropertyName.UserName, late);
				if ((props & XmlCredentialProperty.Comment) != 0)
					OnPropertyChanged(PropertyName.Comment, late);
				if ((props & XmlCredentialProperty.LastWritten) != 0)
					OnPropertyChanged(PropertyName.LastWritten, late);
			} // proc OnPropertyChanged

			public ICredentialProvider Provider => provider;
			
			public abstract object Image { get; }
			public abstract string TargetName { get; }
			public abstract string UserName { get; set; }
			public abstract string Comment { get; set; }
			public abstract DateTime LastWritten { get; }

			public SecureString GetPassword()
				=> provider.package.DecryptPassword(EncryptedPassword, provider.protector);

			public void SetPassword(SecureString password)
				=> EncryptedPassword = provider.package.EncryptPassword(password, provider.protector);

			public abstract object EncryptedPassword { get; set;  }

			public abstract bool IsVisible { get; }
		} // class CredentialItemBase

		#endregion

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly CredPackage package;
		private readonly ICredentialProtector protector;
		private readonly string fileName;
		private readonly string shadowFileName; // Local copy for network files

		private DateTime lastModification;
		private readonly List<CredentialItemBase> items = new List<CredentialItemBase>();
		private bool isLoading;

		#region -- Ctor/Dtor ----------------------------------------------------------

		protected FileCredentialProviderBase(CredPackage package, string fileName, ICredentialProtector protector, string shadowFileName)
		{
			this.package = package ?? throw new ArgumentNullException(nameof(package));
			this.fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
			this.protector = protector ?? throw new ArgumentNullException(nameof(protector));
			this.shadowFileName = shadowFileName;

			lastModification = DateTime.MinValue;

			// Start load
			Load(true);
		} // ctor

		#endregion

		#region -- ICredentialProvider - members --------------------------------------

		protected void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
			=> CollectionChanged?.Invoke(this, e);

		protected abstract CredentialItemBase CreateNew(ICredentialInfo item);

		protected virtual void OnItemAdded(CredentialItemBase item)
			=> OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new ICredentialInfo[] { item }));

		protected virtual void OnItemRemoved(CredentialItemBase item)
			=> OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, new ICredentialInfo[] { item }));

		ICredentialInfo ICredentialProvider.Append(ICredentialInfo newItem)
		{
			var item = Find(newItem.TargetName);
			if (item == null)
			{
				item = CreateNew(newItem);
				items.Add(item);
				OnItemAdded(item);
				
				return item;
			}
			else if (newItem is IXmlCredentialItem x)
			{
				item.Update(x);
				return item;
			}
			else
				throw new ArgumentException();
		} // proc Append

		bool ICredentialProvider.Remove(string targetName)
		{
			var item = Find(targetName);
			if (item == null)
				return false;

			if (item.Remove())
			{
				OnItemRemoved(item);
				return true;
			}
			else
				return false;
		} // func Remove

		public NetworkCredential GetCredential(Uri uri, string authType)
			=> CredPackage.FindCredentials(Name, uri, this);

		public IEnumerator<ICredentialInfo> GetEnumerator()
			=> items.Where(c => c.IsVisible).GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		#endregion

		#region -- IPwAutoSaveFile - members ------------------------------------------

		#region -- Update Shadow File -------------------------------------------------

		private string CopyFileSafe(FileInfo src, FileInfo dst)
		{
			var tries = 0;
			var lastException = (string)null;
			while (tries < 3)
			{
				try
				{
					src.CopyTo(dst.FullName, true);
					dst.LastWriteTimeUtc = src.LastWriteTimeUtc;
					return null;
				}
				catch (UnauthorizedAccessException e)
				{
					return e.Message;
				}
				catch (IOException e)
				{
					lastException = e.Message;
				}
				tries++;
				Thread.Sleep(tries * 1000);
			}
			return lastException;
		} // proc CopyFile

		private async Task<bool> CopyFileAsync(FileInfo src, FileInfo dst)
		{
			Log.Default.SourceFileCopy(Name, src.FullName);
			if (dst.Exists)
				dst.Attributes = dst.Attributes & ~FileAttributes.ReadOnly;
			try
			{
				var r = await Task.Run(() => CopyFileSafe(src, dst));
				if (r == null)
					return true;
				else
				{
					Log.Default.SourceFileCopyFailed(Name, src.FullName);
					return false;
				}
			}
			finally
			{
				dst.Attributes = dst.Attributes | FileAttributes.ReadOnly;
			}
		} // proc CopyFileSync

		private async Task<bool> UpdateShadowFileAsync()
		{
			if (shadowFileName == null)
				return false;

			var shadowFileInfo = new FileInfo(shadowFileName);
			var sourceFileInfo = new FileInfo(fileName);
			if (sourceFileInfo.Exists)
			{
				if (!shadowFileInfo.Exists || sourceFileInfo.LastWriteTimeUtc > shadowFileInfo.LastWriteTimeUtc)
					return await CopyFileAsync(sourceFileInfo, shadowFileInfo);
			}
			else
				Log.Default.SourceFileIsOffline(Name, sourceFileInfo.FullName);

			return false;
		} // func PullShadowFileAsync

		#endregion

		#region -- Load ---------------------------------------------------------------

		protected virtual DateTime GetLastWriteTimeSafe()
		{
			try
			{
				return File.GetLastWriteTime(shadowFileName ?? fileName);
			}
			catch (IOException)
			{
				return DateTime.MinValue;
			}
		} // func GetLastWriteTimeSafe

		protected bool IsNewDataOnDisk()
			=> lastModification < GetLastWriteTimeSafe();

		protected virtual IEnumerable<IXmlCredentialItem> LoadItemsCore()
			=> XmlCredentialItem.Load(shadowFileName ?? fileName);

		protected abstract CredentialItemBase CreateItem(IXmlCredentialItem item);

		private (CredentialItemBase[] added, CredentialItemBase[] changed, CredentialItemBase[] removed, DateTime lastModification) UpdateItems()
		{
			var itemsAdded = new List<CredentialItemBase>();
			var itemsChanged = new List<CredentialItemBase>();
			var itemsRemoved = new List<CredentialItemBase>();

			// update items
			var lm = GetLastWriteTimeSafe();
			foreach (var cur in LoadItemsCore())
			{
				var item = Find(cur.TargetName);
				if (item == null) // new item
					itemsAdded.Add(CreateItem(cur));
				else
				{
					var isVisibleOld = item.IsVisible;
					if (item.Update(cur)) // update current item
						(isVisibleOld ? itemsChanged : itemsAdded).Add(item);
				}
			}

			// collect removed items
			foreach (var cur in items)
			{
				if (!cur.GetTouched())
					itemsRemoved.Add(cur);
			}

			return (itemsAdded.ToArray(), itemsChanged.ToArray(), itemsRemoved.ToArray(), lm);
		} // proc UpdateItems

		protected async Task LoadAsync(bool force)
		{
			// try sync shadow file if exists
			var shadowFileSynced = await UpdateShadowFileAsync();

			// load information
			if (force || shadowFileSynced || IsNewDataOnDisk())
			{
				if (isLoading)
					return;

				isLoading = true;
				try
				{
					var (addedItems, changedItems, removedItems, lm) = await Task.Run(() => UpdateItems());

					// remove items
					foreach (var cur in removedItems)
						items.Remove(cur);
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItems));

					// add items
					foreach (var cur in addedItems)
						items.Add(cur);
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, addedItems));

					// notify items
					foreach (var cur in changedItems)
						cur.NotifyPropertyChanged();

					lastModification = lm;
				}
				finally
				{
					isLoading = false;
				}
			}
		} // proc LoadAsync

		private void LoadFailed(Exception exception)
			=> Log.FileProviderLoadFailed(Name, exception);

		private void Load(bool force)
			=> LoadAsync(force).Spawn(LoadFailed);

		#endregion

		protected abstract void Save(bool force);

		void IPwAutoSaveFile.Reload()
			=> Load(false);

		void IPwAutoSaveFile.Save(bool force)
			=> Save(force);

		string IPwAutoSaveFile.FileName => fileName;

		#endregion

		protected CredentialItemBase Find(string targetName)
			=> items.FirstOrDefault(c => String.Compare(c.TargetName, targetName, StringComparison.OrdinalIgnoreCase) == 0);

		public CredPackage Package => package;
		public ICredentialProtector Protector => protector;

		public string Name => Path.GetFileNameWithoutExtension(fileName);
		public abstract bool IsReadOnly { get; }
		public abstract bool IsModified { get; }
		public DateTime LastModificationTime => lastModification;

		protected string FileName => fileName;
		protected string ShadowFileName => shadowFileName;
	} // class FileCredentialProviderBase

	#endregion

	#region -- class FileReadOnlyCredentialProvider -----------------------------------

	internal sealed class FileReadOnlyCredentialProvider : FileCredentialProviderBase
	{
		#region -- class CredentialItem -----------------------------------------------

		private sealed class CredentialItem : CredentialItemBase
		{
			private IXmlCredentialItem item;

			#region -- Ctor/Dtor ------------------------------------------------------

			public CredentialItem(FileReadOnlyCredentialProvider provider, IXmlCredentialItem item)
				: base(provider)
			{
				this.item = item ?? throw new ArgumentNullException(nameof(item));
			} // ctor

			#endregion

			#region -- Update/Remove --------------------------------------------------

			protected override bool UpdateCore(IXmlCredentialItem other)
			{
				if (other == null)
					throw new ArgumentNullException(nameof(other));

				var props = XmlCredentialItem.Compare(item, other, false);
				if (props == XmlCredentialProperty.None)
					return false;

				item = other;

				OnPropertyChanged(props, true);

				return true;
			} // func Update

			protected override bool RemoveCore()
				=> false;

			#endregion

			public override object Image => providerImage;
			public override string TargetName => item.TargetName;

			public override string UserName { get => item.UserName; set => throw GetReadOnlyException(); }
			public override string Comment { get => item.Comment; set => throw GetReadOnlyException(); }
			public override object EncryptedPassword { get => item.EncryptedPassword; set => throw GetReadOnlyException(); }
			public override DateTime LastWritten => item.LastWritten;

			public override bool IsVisible => true;
		} // class CredentialItem

		#endregion

		#region -- Ctor ---------------------------------------------------------------

		public FileReadOnlyCredentialProvider(CredPackage package, string fileName, ICredentialProtector protector, string shadowFileName)
			: base(package, fileName, protector, shadowFileName)
		{
		} // ctor

		private static Exception GetReadOnlyException()
			=> new InvalidOperationException("This credential provider is readonly.");

		#endregion

		#region -- Load/Save ----------------------------------------------------------

		protected override CredentialItemBase CreateItem(IXmlCredentialItem item)
			=> new CredentialItem(this, item);

		protected override CredentialItemBase CreateNew(ICredentialInfo item)
			=> throw GetReadOnlyException();

		protected override void OnItemAdded(CredentialItemBase item)
			=> throw GetReadOnlyException();

		protected override void OnItemRemoved(CredentialItemBase item)
			=> throw GetReadOnlyException();

		protected override void Save(bool force)
			=> throw GetReadOnlyException();

		#endregion

		public override bool IsReadOnly => true;
		public override bool IsModified => false;

		// -- Static ------------------------------------------------------

		private static readonly DrawingImage providerImage;

		static FileReadOnlyCredentialProvider()
		{
			providerImage = new DrawingImage(
				new GeometryDrawing(
					new SolidColorBrush(Colors.DarkRed), null, Geometry.Parse("M7 14C5.9 14 5 13.1 5 12S5.9 10 7 10 9 10.9 9 12 8.1 14 7 14M12.6 10C11.8 7.7 9.6 6 7 6C3.7 6 1 8.7 1 12S3.7 18 7 18C9.6 18 11.8 16.3 12.6 14H16V18H20V14H23V10H12.6Z")
				)
			);
			providerImage.Freeze();
		} // sctor
	} // class FileReadOnlyCredentialProvider

	#endregion

	#region -- class FileCredentialProvider -------------------------------------------

	internal sealed class FileCredentialProvider : FileCredentialProviderBase
	{
		#region -- enum CredentialItemState -------------------------------------------

		private enum CredentialItemState
		{
			Original,
			New,
			Modified,
			Deleted
		} // enum CredentialItemState

		#endregion

		#region -- class CredentialItem -----------------------------------------------

		private sealed class CredentialItem : CredentialItemBase
		{
			private delegate T GetOriginalValueDelegate<T>(IXmlCredentialItem item);

			private IXmlCredentialItem original;
			private CredentialItemState state;

			// modified values
			private string newTargetName = null;
			private string userName = null;
			private string comment = null;
			private DateTime lastWritten = DateTime.MinValue;
			private object encryptedPassword = null;

			#region -- Ctor/Dtor ------------------------------------------------------

			public CredentialItem(FileCredentialProvider provider, IXmlCredentialItem item)
				: base(provider)
			{
				original = item;
				state = CredentialItemState.Original;
			} // ctor

			public CredentialItem(FileCredentialProvider provider, ICredentialInfo item)
				: base(provider)
			{
				original = null;
				state = CredentialItemState.New;

				newTargetName = item.TargetName ?? throw new ArgumentNullException(nameof(TargetName));
				userName = item.UserName;
				comment = item.Comment;
				lastWritten = item.LastWritten;
				encryptedPassword = provider.Package.EncryptPassword(item.GetPassword(), provider.Protector);
			} // ctor

			#endregion

			#region -- Update/Remove --------------------------------------------------

			protected override bool UpdateCore(IXmlCredentialItem item)
			{
				if (state == CredentialItemState.Original) // compare and update
				{
					var props = XmlCredentialItem.Compare(original, item, false);
					if (props == XmlCredentialProperty.None)
						return false;

					original = item;
					OnPropertyChanged(props, true);
					return true;
				}
				else if (state == CredentialItemState.Modified
					|| state == CredentialItemState.New) // compare cache
				{
					var props = XmlCredentialItem.Compare(this, item, false);
					if (props == XmlCredentialProperty.None)
					{
						SetState(CredentialItemState.Original);
						return false;
					}

					original = item;
					state = CredentialItemState.Modified;

					return false;
				}
				else if (state == CredentialItemState.Deleted)
				{
					if (item.LastWritten > lastWritten)
					{
						original = item;
						SetState(CredentialItemState.Original);
						OnPropertyChanged(XmlCredentialProperty.UserName | XmlCredentialProperty.Comment | XmlCredentialProperty.Password | XmlCredentialProperty.LastWritten, true);
						return true;
					}
					else // stay deleted
						return false;
				}
				else
					throw new InvalidOperationException();
			} // func UpdateCore

			protected override bool RemoveCore()
			{ 
				SetState(CredentialItemState.Deleted);
				return true;
			} // proc SetState

			#endregion

			#region -- SetState -------------------------------------------------------

			private void SetState(CredentialItemState state)
			{
				if (this.state != state)
				{
					this.state = state;

					switch (state)
					{
						case CredentialItemState.Original:
							if (original == null)
								throw new InvalidOperationException();
							userName = null;
							comment = null;
							lastWritten = DateTime.MinValue;
							encryptedPassword = null;
							break;

						case CredentialItemState.New:
							if (original != null)
								throw new InvalidOperationException();
							break;

						case CredentialItemState.Modified:
							if (original == null)
								throw new InvalidOperationException();

							userName = original.UserName;
							comment = original.Comment;
							lastWritten = original.LastWritten;
							encryptedPassword = original.EncryptedPassword;
							break;

						case CredentialItemState.Deleted:
							userName = null;
							comment = null;
							lastWritten = DateTime.MinValue;
							encryptedPassword = null;
							break;
					}
					OnPropertyChanged(PropertyName.IsVisible, false);
				}
			} // proc SetState

			#endregion

			#region -- Properties -----------------------------------------------------

			private T GetValue<T>(ref T localValue, GetOriginalValueDelegate<T> originalValue)
			{
				return state == CredentialItemState.New || state == CredentialItemState.Modified
					? localValue
					: originalValue(original);
			} // func GetValue

			private void SetValueCore<T>(ref T localValue, T value, PropertyName propertyName)
			{
				localValue = value;
				OnPropertyChanged(propertyName, false);
				((FileCredentialProvider)Provider).SetModified();
			} // proc SetValueCore

			private static bool EqualsProperty(PropertyName propertyName, object a, object b)
				=> propertyName == PropertyName.EncryptedPassword ? Cred.Provider.Protector.EqualValue(a, b) : Equals(a, b);

			private void SetValue<T>(ref T localValue, GetOriginalValueDelegate<T> originalValue, T value, PropertyName propertyName)
			{
				if (state == CredentialItemState.New)
				{
					if (EqualsProperty(propertyName, localValue, value))
						return;

					// update local copy
					SetValueCore(ref localValue, value, propertyName);
				}
				else if (state == CredentialItemState.Original)
				{
					if (EqualsProperty(propertyName, originalValue(original), value))
						return;

					// switch state to copy
					SetState(CredentialItemState.Modified);
					// update local copy
					SetValueCore(ref localValue, value, propertyName);
				}
				else if (state == CredentialItemState.Modified)
				{
					if (EqualsProperty(propertyName, localValue, value))
						return;

					// update local copy
					localValue = value;

					// switch state back to original
					if (EqualsProperty(PropertyName.UserName, original.UserName, userName)
						&& EqualsProperty(PropertyName.Comment, original.Comment, comment)
						&& EqualsProperty(PropertyName.LastWritten, original.LastWritten, lastWritten)
						&& EqualsProperty(PropertyName.EncryptedPassword, original.EncryptedPassword, encryptedPassword))
					{
						SetState(CredentialItemState.Original);
					}

					OnPropertyChanged(propertyName, false);
				}
				else
					throw new InvalidOperationException();
			} // proc SetValue

			private static string GetUserName(IXmlCredentialItem original)
				=> original.UserName;

			private static string GetComment(IXmlCredentialItem original)
				=> original.Comment;

			private static DateTime GetLastWritten(IXmlCredentialItem original)
				=> original.LastWritten;

			private static object GetEncryptedPassword(IXmlCredentialItem original)
				=> original.EncryptedPassword;

			private void SetLastWritten(DateTime value)
				=> SetValue(ref lastWritten, GetLastWritten, value, PropertyName.LastWritten);

			public override string TargetName => state == CredentialItemState.New ? newTargetName : original.TargetName;
			public override object Image => providerImage;

			public override string UserName
			{
				get => GetValue(ref userName, GetUserName);
				set => SetValue(ref userName, GetUserName, value, PropertyName.UserName);
			} // prop UserName

			public override string Comment
			{
				get => GetValue(ref comment, GetComment);
				set => SetValue(ref comment, GetComment, value, PropertyName.Comment);
			} // prop Comment

			public override DateTime LastWritten => GetValue(ref lastWritten, GetLastWritten);

			public override object EncryptedPassword
			{
				get => GetValue(ref encryptedPassword, GetEncryptedPassword);
				set => SetValue(ref encryptedPassword, GetEncryptedPassword, value, PropertyName.EncryptedPassword);
			} // prop EncryptedPassword

			#endregion

			public override bool IsVisible => state != CredentialItemState.Deleted;
		} // class CredentialItem

		#endregion

		private readonly string changeFileName;
		private bool isModified = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public FileCredentialProvider(CredPackage package, string fileName, ICredentialProtector protector, string shadowFileName)
			: base(package, fileName, protector, shadowFileName)
		{
			if (shadowFileName == null)
				changeFileName = null;
			else
			{
				var i = shadowFileName.LastIndexOf("-shadow");
				changeFileName = shadowFileName.Substring(0, i) + "-changes" + shadowFileName.Substring(i + 7);
			}
		} // ctor

		#endregion

		#region -- Load/Save ----------------------------------------------------------

		private void SetModified()
			=> isModified = true;

		protected override DateTime GetLastWriteTimeSafe()
		{
			return base.GetLastWriteTimeSafe();
		}

		protected override IEnumerable<IXmlCredentialItem> LoadItemsCore()
		{
			return base.LoadItemsCore();
		}

		protected override CredentialItemBase CreateItem(IXmlCredentialItem item)
			=> new CredentialItem(this, item);

		protected override CredentialItemBase CreateNew(ICredentialInfo item)
			=> new CredentialItem(this, item);

		protected override void OnItemAdded(CredentialItemBase item)
		{
			SetModified();
			base.OnItemAdded(item);
		} // proc OnItemAdded

		protected override void OnItemRemoved(CredentialItemBase item)
		{
			SetModified();
			base.OnItemRemoved(item);
		} // proc OnItemRemoved

		private void SaveChangeMode()
		{
		} // proc SaveChangeMode

		private void SaveDirectMode()
		{
			XmlCredentialItem.Save(FileName, this.Cast<IXmlCredentialItem>());
			await LoadAsync(true);
			isModified = false;
		} // proc SaveDirectMode

		protected override void Save(bool force)
		{
			if (changeFileName != null)
				SaveChangeMode();
			else
				SaveDirectMode();
		} // proc Save

		private void LogSaveFailed(Exception exception)
			=> Log.FileProviderSaveFailed(Name, exception);

		#endregion

		public override bool IsReadOnly => false;
		public override bool IsModified => isModified;

		// -- Static ------------------------------------------------------

		private static readonly DrawingImage providerImage;

		static FileCredentialProvider()
		{
			providerImage = new DrawingImage(
				new GeometryDrawing(
					new SolidColorBrush(Colors.DarkGreen), null, Geometry.Parse("M7 14C5.9 14 5 13.1 5 12S5.9 10 7 10 9 10.9 9 12 8.1 14 7 14M12.6 10C11.8 7.7 9.6 6 7 6C3.7 6 1 8.7 1 12S3.7 18 7 18C9.6 18 11.8 16.3 12.6 14H16V18H20V14H23V10H12.6Z")
				)
			);
			providerImage.Freeze();
		} // sctor
	}

	#endregion
}
