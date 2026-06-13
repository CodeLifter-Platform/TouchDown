namespace TD.Models;

/// <summary>
/// Machine-local user preferences persisted as JSON on disk.
/// NOT stored in the database — preferences are per app installation.
/// </summary>
public class UserPreferences
{
    // ── Telemetry consent ────────────────────────────────────────────────────
    /// <summary>Whether the user has granted consent to anonymous telemetry.</summary>
    public bool TelemetryConsented { get; set; }

    /// <summary>True once the user has clicked Accept or Decline at least once.</summary>
    public bool HasRespondedToTelemetryConsent { get; set; }

    /// <summary>When the user last responded to the consent prompt.</summary>
    public DateTimeOffset? ConsentTimestamp { get; set; }

    /// <summary>
    /// The policy version the user last responded to.
    /// Used to re-present the modal when the policy materially changes.
    /// Defaults to "1.0" so new installs start fresh.
    /// </summary>
    public string ConsentVersion { get; set; } = "1.0";
}

