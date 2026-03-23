using System;
using System.ComponentModel;

namespace TouchDown.MVVM.ViewModels;

public interface ITDVM : INotifyPropertyChanged
{
    Task OnInitializedAsync();
    Task Loaded();
}

