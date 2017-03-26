using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo.PerfectWorking.Data
{
	public class ObservableObject : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		public virtual void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		protected void SetProperty<T>(string propertyName, ref T value, T newValue)
		{
			if (!Object.Equals(value, newValue))
			{
				value = newValue;
				OnPropertyChanged(propertyName);
			}
		} // proc SetProperty
	} // class ObservableObject
}
