using TD.Models;
using TD.Services;

namespace TouchDown.Tests.TestSupport;

/// <summary>
/// A scripted <see cref="IAgentProvider"/>. By default any call fails the test — pass a
/// response only where the code under test is expected to reach the model.
/// </summary>
public sealed class FakeAgentProvider : IAgentProvider
{
    private readonly string? _response;

    public FakeAgentProvider(string? response = null, string providerId = "fake")
    {
        _response = response;
        ProviderId = providerId;
    }

    public string ProviderId { get; }
    public string DisplayName => "Fake Provider";
    public IReadOnlyList<AgentModel> AvailableModels { get; } =
        [new AgentModel { ModelId = "fake-model-1", DisplayName = "Fake Model" }];

    public bool IsAvailable { get; set; } = true;

    /// <summary>Every context this provider was asked to run, in order.</summary>
    public List<AgentContext> Calls { get; } = [];

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(IsAvailable);

    public Task<AgentResponse> RunAsync(AgentContext context, CancellationToken ct = default)
    {
        Calls.Add(context);

        if (_response is null)
            throw new InvalidOperationException(
                "The provider was called, but this test expected the result to be resolved without a model call.");

        return Task.FromResult(new AgentResponse { FullText = _response });
    }

    public async IAsyncEnumerable<AgentStreamChunk> StreamAsync(
        AgentContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        Calls.Add(context);

        if (_response is null)
            throw new InvalidOperationException("The provider was streamed from unexpectedly.");

        foreach (var word in _response.Split(' '))
        {
            ct.ThrowIfCancellationRequested();
            yield return new AgentStreamChunk { TextDelta = word + " " };
            await Task.Yield();
        }

        yield return new AgentStreamChunk { IsComplete = true, Result = _response, CostUsd = 0.01 };
    }
}
