using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using MudBlazor;
using TD.Hubs;
using TD.Models;
using TD.Services;

namespace TouchDown.Tests.TestSupport;

// Shared fakes for the whole test project. A fake here is an in-memory implementation of a
// seam the app already has; tests assert on outcomes (what was persisted, what the client
// received), never on which calls were made.

/// <summary>
/// A scripted <see cref="IAgentProvider"/>. By default any call fails the test: pass a
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

    /// <summary>When set, the terminal chunk of every stream reports an error with this text.</summary>
    public string? ErrorResult { get; init; }

    /// <summary>When set, streaming throws this instead of producing output (a crashed CLI).</summary>
    public Exception? ThrowOnStream { get; init; }

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

        if (ThrowOnStream is not null)
            throw ThrowOnStream;

        if (ErrorResult is not null)
        {
            yield return new AgentStreamChunk { IsComplete = true, IsError = true, Result = ErrorResult };
            yield break;
        }

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

/// <summary>
/// A provider whose stream never ends until cancelled. Used to hold a drive in progress so
/// cancellation can be exercised deterministically.
/// </summary>
public sealed class HangingAgentProvider : IAgentProvider
{
    public string ProviderId => "hanging";
    public string DisplayName => "Hanging Provider";
    public IReadOnlyList<AgentModel> AvailableModels { get; } =
        [new AgentModel { ModelId = "hang-1", DisplayName = "Hang" }];

    /// <summary>Completes once the first stream has started, so a test knows the drive is mid-play.</summary>
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task<AgentResponse> RunAsync(AgentContext context, CancellationToken ct = default) =>
        throw new InvalidOperationException("Planning should not reach the model in this test.");

    public async IAsyncEnumerable<AgentStreamChunk> StreamAsync(
        AgentContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        Started.TrySetResult();
        yield return new AgentStreamChunk { TextDelta = "working...\n" };
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }
}

/// <summary>Preferences held in memory, swappable mid-test to simulate a consent change.</summary>
public sealed class StubPreferences : IUserPreferencesService
{
    public UserPreferences Current { get; set; } = new();
    public int Saves { get; private set; }

    public Task SaveAsync(CancellationToken ct = default)
    {
        Saves++;
        return Task.CompletedTask;
    }
}

/// <summary>Preferences that cannot be read: the failure path for everything that depends on them.</summary>
public sealed class ThrowingPreferences : IUserPreferencesService
{
    public UserPreferences Current => throw new InvalidOperationException("preferences unavailable");
    public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>
/// An <see cref="ISnackbar"/> that records what the user would have been shown, so VM tests
/// can assert on the message and severity rather than on a MudBlazor render.
/// </summary>
public sealed class FakeSnackbar : ISnackbar
{
    public List<(string Message, Severity Severity)> Shown { get; } = [];

    public IEnumerable<Snackbar> ShownSnackbars => [];
    public SnackbarConfiguration Configuration { get; } = new();
    public event Action? OnSnackbarsUpdated { add { } remove { } }

    public Snackbar? Add(string message, Severity severity = Severity.Normal, Action<SnackbarOptions>? configure = null, string? key = "")
    {
        Shown.Add((message, severity));
        return null;
    }

    public Snackbar? Add(MarkupString message, Severity severity = Severity.Normal, Action<SnackbarOptions>? configure = null, string? key = "")
    {
        Shown.Add((message.Value, severity));
        return null;
    }

    public Snackbar? Add(RenderFragment message, Severity severity = Severity.Normal, Action<SnackbarOptions>? configure = null, string? key = "")
    {
        Shown.Add(("<fragment>", severity));
        return null;
    }

    public Snackbar? Add<T>(Dictionary<string, object>? componentParameters = null, Severity severity = Severity.Normal, Action<SnackbarOptions>? configure = null, string? key = "")
        where T : IComponent
    {
        Shown.Add((typeof(T).Name, severity));
        return null;
    }

    public void Clear() => Shown.Clear();
    public void Remove(Snackbar snackbar) { }
    public void RemoveByKey(string key) { }
    public void Dispose() { }
}

/// <summary>
/// An <see cref="IHubContext{AgentHub}"/> that records every message sent to every group,
/// so orchestrator tests can assert on what a monitoring client would have received without
/// hosting SignalR. The wire-level contract itself is covered by the hosted hub tests.
/// </summary>
public sealed class RecordingHubContext : IHubContext<AgentHub>
{
    public RecordingHubContext()
    {
        Clients = new RecordingHubClients(this);
    }

    /// <summary>(group, method, payload) in send order.</summary>
    public List<(string Group, string Method, object? Payload)> Sent { get; } = [];

    public IHubClients Clients { get; }
    public IGroupManager Groups { get; } = new NoOpGroupManager();

    public IEnumerable<object?> PayloadsFor(string group, string method) =>
        Sent.Where(s => s.Group == group && s.Method == method).Select(s => s.Payload);

    /// <summary>Reads a property off an anonymous payload by name.</summary>
    public static object? Prop(object? payload, string name) =>
        payload?.GetType().GetProperty(name)?.GetValue(payload);

    private sealed class RecordingHubClients(RecordingHubContext owner) : IHubClients
    {
        public IClientProxy All => new Proxy(owner, "*");
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => All;
        public ISingleClientProxy Client(string connectionId) => new Proxy(owner, connectionId);
        IClientProxy IHubClients<IClientProxy>.Client(string connectionId) => Client(connectionId);
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => All;
        public IClientProxy Group(string groupName) => new Proxy(owner, groupName);
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Group(groupName);
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => All;
        public IClientProxy User(string userId) => new Proxy(owner, userId);
        public IClientProxy Users(IReadOnlyList<string> userIds) => All;
    }

    private sealed class Proxy(RecordingHubContext owner, string group) : ISingleClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            lock (owner.Sent)
                owner.Sent.Add((group, method, args.Length > 0 ? args[0] : null));
            return Task.CompletedTask;
        }

        public Task<T> InvokeCoreAsync<T>(string method, object?[] args, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The hub never invokes clients.");
    }

    private sealed class NoOpGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

/// <summary>A git service whose every operation fails: the workspace-setup failure path.</summary>
public sealed class ThrowingGitWorktreeService : IGitWorktreeService
{
    private static InvalidOperationException Boom() => new("git is unavailable in this test");

    public Task<string> CreateWorktreeAsync(string repoPath, string branchName, CancellationToken ct = default) => throw Boom();
    public Task RemoveWorktreeAsync(string worktreePath, CancellationToken ct = default) => throw Boom();
    public Task<List<string>> ListBranchesAsync(string repoPath, CancellationToken ct = default) => throw Boom();
    public Task<string> GetCurrentBranchAsync(string repoPath, CancellationToken ct = default) => throw Boom();
    public Task<string> CloneRepoAsync(string repoUrl, string targetPath, CancellationToken ct = default) => throw Boom();
    public Task<GitStatusResult> GetStatusAsync(string workingDir, CancellationToken ct = default) => throw Boom();
    public Task<GitDiffSummary> GetDiffSummaryAsync(string workingDir, CancellationToken ct = default) => throw Boom();
    public Task StageAllAsync(string workingDir, CancellationToken ct = default) => throw Boom();
    public Task<string> CommitAsync(string workingDir, string message, CancellationToken ct = default) => throw Boom();
    public Task PushAsync(string workingDir, string? remoteBranch = null, CancellationToken ct = default) => throw Boom();
    public Task<string> GetRemoteUrlAsync(string workingDir, CancellationToken ct = default) => throw Boom();
    public Task<string> InitRepoAsync(string path, CancellationToken ct = default) => throw Boom();
}

/// <summary>
/// A telemetry service that records what it was told and never throws, so orchestrator
/// tests can assert on the reported outcome (status and turnover reason) of a drive.
/// </summary>
public sealed class RecordingTelemetry : TD.Services.Telemetry.ITelemetryService
{
    public List<TD.Services.Telemetry.DriveResult> Outcomes { get; } = [];
    public List<(string Name, Dictionary<string, object>? Props)> Events { get; } = [];

    public bool IsConsentGranted => true;

    public Task TrackEventAsync(string name, Dictionary<string, object>? props = null)
    {
        lock (Events) Events.Add((name, props));
        return Task.CompletedTask;
    }

    public Task TrackErrorAsync(Exception ex, string component, Dictionary<string, object>? ctx = null) => Task.CompletedTask;

    public Task TrackDriveOutcomeAsync(TD.Services.Telemetry.DriveResult result)
    {
        lock (Outcomes) Outcomes.Add(result);
        return Task.CompletedTask;
    }

    public Task TrackTimingAsync(string operation, TimeSpan duration, Dictionary<string, object>? ctx = null) => Task.CompletedTask;
    public TD.Services.Telemetry.ITelemetryScope StartDriveScope(string driveId, Dictionary<string, object>? attributes = null) => Scope.Instance;
    public TD.Services.Telemetry.ITelemetryScope StartPlayScope(string playName, Dictionary<string, object>? attributes = null) => Scope.Instance;
    public TD.Services.Telemetry.ITelemetryScope StartAgentScope(string agentName, Dictionary<string, object>? attributes = null) => Scope.Instance;

    private sealed class Scope : TD.Services.Telemetry.ITelemetryScope
    {
        public static readonly Scope Instance = new();
        public void SetAttribute(string key, object? value) { }
        public void AddEvent(string name, Dictionary<string, object>? attributes = null) { }
        public void SetError(Exception ex) { }
        public void SetError(string description) { }
        public void Dispose() { }
    }
}
