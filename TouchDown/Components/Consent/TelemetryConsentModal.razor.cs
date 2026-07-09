using Microsoft.AspNetCore.Components;
using TD.Services;
using TD.Services.Telemetry;

namespace TD.Components.Consent;

public partial class TelemetryConsentModal : ComponentBase
{
    /// <summary>
    /// The current policy version constant. Bump this string when the privacy policy
    /// materially changes so users who previously responded are re-prompted.
    /// </summary>
    public const string TelemetryPolicyVersion = "1.0";

    [Inject] private IUserPreferencesService Prefs { get; set; } = default!;

    /// <summary>Fires after the user accepts or declines, so the host can hide the modal.</summary>
    [Parameter] public EventCallback OnResolved { get; set; }

    private bool _saving;
    private bool _isPolicyChange;

    /// <summary>
    /// True when the modal should be rendered:
    ///   - User has never responded, OR
    ///   - User previously responded but the policy version has changed.
    /// </summary>
    public bool IsVisible =>
        !Prefs.Current.HasRespondedToTelemetryConsent ||
        Prefs.Current.ConsentVersion != TelemetryPolicyVersion;

    protected override void OnInitialized()
    {
        // Detect if this is a policy-change re-prompt vs a first-time prompt
        _isPolicyChange = Prefs.Current.HasRespondedToTelemetryConsent &&
                          Prefs.Current.ConsentVersion != TelemetryPolicyVersion;
    }

    private async Task AcceptAsync()
    {
        _saving = true;
        Prefs.Current.TelemetryConsented = true;
        Prefs.Current.HasRespondedToTelemetryConsent = true;
        Prefs.Current.ConsentTimestamp = DateTimeOffset.UtcNow;
        Prefs.Current.ConsentVersion = TelemetryPolicyVersion;
        await Prefs.SaveAsync();
        _saving = false;
        await OnResolved.InvokeAsync();
    }

    private async Task DeclineAsync()
    {
        _saving = true;
        Prefs.Current.TelemetryConsented = false;
        Prefs.Current.HasRespondedToTelemetryConsent = true;
        Prefs.Current.ConsentTimestamp = DateTimeOffset.UtcNow;
        Prefs.Current.ConsentVersion = TelemetryPolicyVersion;
        await Prefs.SaveAsync();
        _saving = false;
        await OnResolved.InvokeAsync();
    }
}

