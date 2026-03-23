using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace TD.MVVM.ViewModels;

public abstract partial class RecipientVM : ObservableRecipient, IVM
{
    public virtual async Task OnInitializedAsync()
    {
        await Loaded().ConfigureAwait(true);
    }

    protected virtual void NotifyStateChanged() => OnPropertyChanged((string?)null);

    [RelayCommand]
    public virtual async Task Loaded()
        => await Task.CompletedTask.ConfigureAwait(false);
}

public abstract partial class RecipientVM<TMessage> : RecipientVM, IRecipient<TMessage>
    where TMessage : class
{
    public abstract void Receive(TMessage message);
}

