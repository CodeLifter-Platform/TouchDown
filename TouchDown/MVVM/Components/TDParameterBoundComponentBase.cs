using System;
using TouchDown.MVVM.ViewModels;
using Microsoft.AspNetCore.Components;

namespace TouchDown.MVVM.Components
{
	public class TDParameterBoundComponentBase<TViewModel> : ComponentBase, IView<TViewModel> where TViewModel : ITDVM
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

