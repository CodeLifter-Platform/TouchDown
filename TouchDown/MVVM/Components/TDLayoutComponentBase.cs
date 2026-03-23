using System;
using TouchDown.MVVM.ViewModels;
using Microsoft.AspNetCore.Components;

namespace TouchDown.MVVM.Components;

public abstract class TDLayoutComponentBase<TViewModel>
    : LayoutComponentBase, IView<TViewModel> where TViewModel : ITDVM
{
    [Inject]
    protected TViewModel? VM { get; set; }

    protected override void OnInitialized()
    {
        // Cause changes to the ViewModel to make Blazor re-render
        VM!.PropertyChanged += (_, _) => InvokeAsync(StateHasChanged);
        base.OnInitialized();
    }

    protected override Task OnInitializedAsync()
        => VM!.OnInitializedAsync();
}