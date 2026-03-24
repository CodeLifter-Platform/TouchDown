using TD.Models;

namespace TD.Services;

/// <summary>
/// First-class abstraction for any LLM backend that can power TouchDown agents.
/// Future providers (Gemini, Codex, Cursor…) implement this interface and slot in
/// without touching orchestration logic.
/// </summary>
public interface IAgentProvider
{
    /// <summary>Stable, lowercase identifier e.g. "claude-code".</summary>
    string ProviderId { get; }

    /// <summary>Human-readable name shown in the UI.</summary>
    string DisplayName { get; }

    /// <summary>All models this provider supports, in preference order.</summary>
    IReadOnlyList<AgentModel> AvailableModels { get; }

    /// <summary>Run an agent and return the aggregated response.</summary>
    Task<AgentResponse> RunAsync(AgentContext context, CancellationToken ct = default);

    /// <summary>Stream an agent run, yielding chunks as they arrive.</summary>
    IAsyncEnumerable<AgentStreamChunk> StreamAsync(AgentContext context, CancellationToken ct = default);

    /// <summary>Returns true when the provider is installed and authenticated.</summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}

// ──────────────────────────────────────────────────────────────
// DI helpers
// ──────────────────────────────────────────────────────────────

public static class AgentProviderServiceCollectionExtensions
{
    /// <summary>
    /// Registers a concrete <typeparamref name="T"/> as both itself and
    /// as an <see cref="IAgentProvider"/> singleton.
    /// Usage: builder.Services.AddAgentProvider&lt;ClaudeCodeProvider&gt;()
    /// </summary>
    public static IServiceCollection AddAgentProvider<T>(this IServiceCollection services)
        where T : class, IAgentProvider
    {
        services.AddSingleton<T>();
        services.AddSingleton<IAgentProvider>(sp => sp.GetRequiredService<T>());
        return services;
    }
}

