using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using TD.Models;
using TD.MVVM.ViewModels;

namespace TD.Areas.Drives.New;

public interface IHuddleVM
{
    List<HuddleMessage> Messages { get; }
    string UserInput { get; set; }
    bool IsStreaming { get; }
    string StreamingContent { get; }
    AgentSession? Session { get; }
    bool CanSnap { get; }
    Task InitializeWithSession(AgentSession session);
    Task SendMessage();
    Task<string?> SnapTheBall();
    Task CancelStreaming();
}

public class HuddleVMException : Exception
{
    public HuddleVMException() { }
    public HuddleVMException(string message) : base(message) { }
    public HuddleVMException(string message, Exception innerException) : base(message, innerException) { }
}

public partial class HuddleVM : VM, IHuddleVM
{
    private readonly IDrivesNewService _service;
    private CancellationTokenSource? _cts;
    private readonly Serilog.ILogger _log = Log.ForContext<HuddleVM>();

    public HuddleVM(IDrivesNewService service)
    {
        _service = service;
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
        _log.Debug("Initializing huddle with session");
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
        _log.Debug("Sending user message in huddle");
        Messages = [..Messages, new HuddleMessage { Role = "user", Content = UserInput }];
        UserInput = "";
        await GetQbResponse();
    }

    [RelayCommand]
    public async Task<string?> SnapTheBall()
    {
        if (Session == null) return null;
        _log.Information("Snapping the ball from huddle");
        try
        {
            Session.Drive.HuddlePlan = string.Join("\n---\n",
                Messages.Where(m => m.Role == "quarterback").Select(m => m.Content));
            var drive = await _service.StartDriveAsync(Session);
            return drive.DriveId;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to snap the ball");
            throw new HuddleVMException("Failed to snap the ball", ex);
        }
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

            await foreach (var chunk in _service.StreamQbResponseAsync(
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
