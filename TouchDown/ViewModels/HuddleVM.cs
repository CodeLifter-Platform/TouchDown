using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TD.Models;
using TD.MVVM.ViewModels;
using TD.Services;

namespace TD.ViewModels;

public partial class HuddleVM : VM
{
    private readonly IClaudeStreamingService _claude;
    private readonly IAgentOrchestrationService _orchestration;
    private CancellationTokenSource? _cts;

    public HuddleVM(IClaudeStreamingService claude, IAgentOrchestrationService orchestration)
    {
        _claude = claude;
        _orchestration = orchestration;
    }

    [ObservableProperty]
    private List<HuddleMessage> _messages = [];

    [ObservableProperty]
    private string _userInput = "";

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private string _streamingContent = "";

    public AgentSession? Session { get; set; }

    public async Task InitializeWithSession(AgentSession session)
    {
        Session = session;
        var leader = session.Team?.GetLeader();
        if (leader != null)
        {
            Messages = [new HuddleMessage { Role = "user", Content = $"Here's the play: {session.TaskDescription}" }];
            await GetQbResponse();
        }
    }

    [RelayCommand]
    public async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(UserInput)) return;
        Messages = [..Messages, new HuddleMessage { Role = "user", Content = UserInput }];
        UserInput = "";
        await GetQbResponse();
    }

    [RelayCommand]
    public async Task<string?> SnapTheBall()
    {
        if (Session == null) return null;
        Session.Drive.HuddlePlan = string.Join("\n---\n",
            Messages.Where(m => m.Role == "quarterback").Select(m => m.Content));
        var drive = await _orchestration.StartDriveAsync(Session);
        return drive.DriveId;
    }

    public bool CanSnap => !IsStreaming && Messages.Count >= 2;

    private async Task GetQbResponse()
    {
        var leader = Session?.Team?.GetLeader();
        if (leader == null) return;

        IsStreaming = true;
        StreamingContent = "";
        _cts = new CancellationTokenSource();

        try
        {
            var context = string.Join("\n", Messages.Select(m => $"{m.Role}: {m.Content}"));
            var prompt = $"Review this conversation and provide your analysis, plan, and task breakdown:\n\n{context}";

            await foreach (var chunk in _claude.StreamResponseAsync(
                leader.Model.ToModelId(),
                leader.SystemPrompt ?? "You are the Quarterback, the team leader.",
                prompt, Session?.RepoPath, _cts.Token))
            {
                StreamingContent += chunk;
            }

            Messages = [..Messages, new HuddleMessage { Role = "quarterback", Content = StreamingContent }];
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsStreaming = false;
            StreamingContent = "";
        }
    }

    public async Task CancelStreaming()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = null;
        }
    }
}
