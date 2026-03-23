using System;
using System.ComponentModel;

namespace TD.MVVM.ViewModels;

public interface IVM : INotifyPropertyChanged
{
    Task OnInitializedAsync();
    Task Loaded();
}

