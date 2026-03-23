using System;
using TD.MVVM.ViewModels;

namespace TD.MVVM.Components;

// differentiate View (Page) from ViewModel for MvvmNavigationManager auto-detection
public interface IView<out TViewModel> : IView
    where TViewModel : IVM
{
    // Skip
}

public interface IView
{
    // Skip
}
