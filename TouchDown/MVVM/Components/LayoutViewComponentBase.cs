using System;
using TD.MVVM.ViewModels;
using Microsoft.AspNetCore.Components;

namespace TD.MVVM.Components;

public abstract class LayoutViewComponentBase<TViewModel>
    : LayoutComponentBase, IView<TViewModel> where TViewModel : IVM
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