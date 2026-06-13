using TD.Models;

namespace TD.Services;

public interface IAgentProviderRegistry
{
    /// <summary>All registered providers, regardless of availability.</summary>
    IReadOnlyList<IAgentProvider> All { get; }

    /// <summary>Providers that are installed and authenticated on this machine.</summary>
    Task<IReadOnlyList<IAgentProvider>> GetAvailableAsync(CancellationToken ct = default);

    /// <summary>Resolve a specific provider by its ID. Throws if not found.</summary>
    IAgentProvider GetById(string providerId);

    /// <summary>Resolve a provider by ID, or null if not registered.</summary>
    IAgentProvider? TryGetById(string providerId);
}

public class AgentProviderRegistry : IAgentProviderRegistry
{
    private readonly IReadOnlyList<IAgentProvider> _providers;
    private readonly ILogger<AgentProviderRegistry> _logger;

    public AgentProviderRegistry(IEnumerable<IAgentProvider> providers, ILogger<AgentProviderRegistry> logger)
    {
        _providers = providers.ToList();
        _logger = logger;
    }

    public IReadOnlyList<IAgentProvider> All => _providers;

    public async Task<IReadOnlyList<IAgentProvider>> GetAvailableAsync(CancellationToken ct = default)
    {
        var available = new List<IAgentProvider>();
        foreach (var provider in _providers)
        {
            try
            {
                if (await provider.IsAvailableAsync(ct))
                    available.Add(provider);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check availability for provider {ProviderId}", provider.ProviderId);
            }
        }
        return available;
    }

    public IAgentProvider GetById(string providerId) =>
        TryGetById(providerId)
        ?? throw new InvalidOperationException($"No agent provider registered with ID '{providerId}'");

    public IAgentProvider? TryGetById(string providerId) =>
        _providers.FirstOrDefault(p => p.ProviderId.Equals(providerId, StringComparison.OrdinalIgnoreCase));
}
