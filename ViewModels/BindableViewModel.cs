using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ImageViewer.ViewModels
{
    public abstract class BindableViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void Changed(string name) { var handler = PropertyChanged; if (handler != null) handler(this, new PropertyChangedEventArgs(name)); }
    }
}
