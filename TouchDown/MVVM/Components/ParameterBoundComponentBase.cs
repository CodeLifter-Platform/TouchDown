using System;
using TD.MVVM.ViewModels;
using Microsoft.AspNetCore.Components;

namespace TD.MVVM.Components
{
	public class ParameterBoundComponentBase<TViewModel> : ComponentBase, IView<TViewModel> where TViewModel : IVM
    {
        [Parameter]
        public TViewModel VM { get; set; } = default!;

        protected override void OnInitialized()
        {
            // Cause changes to the ViewModel to make Blazor re-render
            VM!.PropertyChanged += (_, _) => InvokeAsync(StateHasChanged);
            base.OnInitialized();
        }

        protected override Task OnInitializedAsync()
            => VM!.OnInitializedAsync();
    }
}

