using System;
using TouchDown.MVVM.ViewModels;

namespace TouchDown.MVVM.Components;

// differentiate View (Page) from ViewModel for MvvmNavigationManager auto-detection
public interface IView<out TViewModel> : IView
    where TViewModel : ITDVM
{
    // Skip
}

public interface IView
{
    // Skip
}
