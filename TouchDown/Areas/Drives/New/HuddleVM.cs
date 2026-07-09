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
    string StreamingRole { get; }
    AgentSession? Session { get; }
    bool CanSnap { get; }
    bool HasResearcher { get; }
    string ResearcherName { get; }
    Task InitializeWithSession(AgentSession session);
    Task SendMessage();
    Task EnlistResearcher();
    Task RollCall();
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

    /// <summary>Who is currently streaming — "Quarterback" or the researcher's name.</summary>
    [ObservableProperty]
    private string _streamingRole = "Quarterback";

    public AgentSession? Session { get; set; }

    private AgentMember? Researcher => Session?.Team?.Members.FirstOrDefault(m => m.Role == AgentRole.Researcher);

    /// <summary>True when the team has a researcher the coach can enlist for web research.</summary>
    public bool HasResearcher => Researcher != null;

    /// <summary>Display name of the team's researcher (e.g. "The Scout"), for labels.</summary>
    public string ResearcherName => Researcher?.Name ?? "Scout";

    public async Task InitializeWithSession(AgentSession session)
    {
        _log.Debug("Initializing huddle with session");
        Session = session;
        var leader = session.Team?.GetLeader();
        if (leader != null)
        {
            // Persist a draft Drive up front so each huddle turn can be stored as it happens.
            session.Drive = await _service.CreateDraftDriveAsync(session);

            var opener = session.TaskDescription ?? "";
            Messages = [new HuddleMessage { Role = "user", Name = "Head Coach", Content = opener }];
            await PersistHuddleTurnAsync("user", "Head Coach", opener);
            await GetQbResponse();
        }
    }

    [RelayCommand]
    public async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(UserInput)) return;
        _log.Debug("Sending user message in huddle");
        var content = UserInput;
        Messages = [..Messages, new HuddleMessage { Role = "user", Name = "Head Coach", Content = content }];
        UserInput = "";
        await PersistHuddleTurnAsync("user", "Head Coach", content);

        // A typed "everyone report" runs the real roll call instead of letting the QB fabricate one.
        if (LooksLikeRollCall(content))
            await RunRollCallAsync();
        else
            await GetQbResponse();
    }

    [RelayCommand]
    public async Task EnlistResearcher()
    {
        if (string.IsNullOrWhiteSpace(UserInput)) return;
        var researcher = Researcher;
        if (researcher == null) return;

        _log.Information("Enlisting {Researcher} for research", researcher.Name);
        var question = UserInput;
        Messages = [..Messages, new HuddleMessage { Role = "user", Name = "Head Coach", Content = question }];
        UserInput = "";
        await PersistHuddleTurnAsync("user", "Head Coach", question);

        IsStreaming = true;
        StreamingRole = researcher.Name;
        StreamingContent = "";
        _cts = new CancellationTokenSource();
        try
        {
            var systemPrompt = (string.IsNullOrWhiteSpace(researcher.SystemPrompt)
                ? AgentDefaults.ScoutSystemPrompt
                : researcher.SystemPrompt)
                + "\n\n" + (Session?.Team?.BuildRosterPrompt() ?? "");

            var conversation = string.Join("\n\n", Messages.Select(m => $"[{Speaker(m)}]\n{m.Content}"));
            var prompt = conversation +
                $"\n\n[{researcher.Name}]\nResearch the Head Coach's latest request on the web and report concise, sourced findings the team can act on.\n";

            var model = researcher.Model.ToModelId();
            var effort = model.Contains("haiku", StringComparison.OrdinalIgnoreCase)
                ? null
                : researcher.Effort.ToCliValue();

            await foreach (var chunk in _service.StreamCoordinatorResearchAsync(
                model, systemPrompt, prompt, Session?.RepoPath, effort, _cts.Token))
            {
                StreamingContent += chunk;
            }

            var findings = StreamingContent;
            Messages = [..Messages, new HuddleMessage { Role = "coordinator", Name = researcher.Name, Content = findings }];
            await PersistHuddleTurnAsync("coordinator", researcher.Name, findings);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.Error(ex, "Researcher run failed");
            Messages = [..Messages, new HuddleMessage { Role = "coordinator", Name = researcher.Name, Content = $"⚠️ Research failed: {ex.Message}" }];
        }
        finally
        {
            IsStreaming = false;
            StreamingRole = "Quarterback";
            StreamingContent = "";
        }
    }

    private string RoleLabel(string role) => role switch
    {
        "user" => "Head Coach",
        "coordinator" => ResearcherName,
        _ => "Quarterback"
    };

    private string Speaker(HuddleMessage m) => m.Name ?? RoleLabel(m.Role);

    /// <summary>Heuristic: does a typed Head-Coach message ask the whole team to report?</summary>
    private static bool LooksLikeRollCall(string text)
    {
        var t = text.ToLowerInvariant();
        if (t.Contains("roll call") || t.Contains("rollcall") || t.Contains("sound off")) return true;
        var teamWide = t.Contains("all our agents") || t.Contains("all of our agents") || t.Contains("all agents")
            || t.Contains("every agent") || t.Contains("each agent") || t.Contains("everyone") || t.Contains("everybody")
            || t.Contains("whole team") || t.Contains("all players") || t.Contains("entire team");
        var report = t.Contains("report") || t.Contains("status") || t.Contains("their name") || t.Contains("your name")
            || t.Contains("introduce") || t.Contains("check in") || t.Contains("checkin");
        return teamWide && report;
    }

    [RelayCommand]
    public async Task RollCall()
    {
        const string ask = "Roll call — everyone state your name, position, and status.";
        Messages = [..Messages, new HuddleMessage { Role = "user", Name = "Head Coach", Content = ask }];
        await PersistHuddleTurnAsync("user", "Head Coach", ask);
        await RunRollCallAsync();
    }

    private async Task RunRollCallAsync()
    {
        if (Session?.Team?.Members is not { Count: > 0 } members) return;
        _log.Information("Running huddle roll call across {Count} agents", members.Count);

        var roster = Session!.Team!.BuildRosterPrompt();

        IsStreaming = true;
        _cts = new CancellationTokenSource();
        try
        {
            foreach (var member in members.OrderBy(m => m.Role))
            {
                if (_cts.Token.IsCancellationRequested) break;

                StreamingRole = member.Name;
                StreamingContent = "";

                var systemPrompt = (string.IsNullOrWhiteSpace(member.SystemPrompt) ? "" : member.SystemPrompt)
                    + "\n\n" + roster;
                var prompt =
                    "Roll call from the Head Coach. Reply in 1–2 short lines only: your name, your position/role " +
                    "on this team, and your current readiness status. Stay in character as your role; do not mention " +
                    "or invent any agents outside the roster above.";
                var model = member.Model.ToModelId();
                var effort = model.Contains("haiku", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : member.Effort.ToCliValue();

                try
                {
                    await foreach (var chunk in _service.StreamQbResponseAsync(
                        model, systemPrompt, prompt, Session?.RepoPath, effort, _cts.Token))
                    {
                        StreamingContent += chunk;
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _log.Error(ex, "Roll call failed for {Member}", member.Name);
                    StreamingContent = $"⚠️ {ex.Message}";
                }

                Messages = [..Messages, new HuddleMessage { Role = "rollcall", Name = member.Name, Content = StreamingContent }];
                await PersistHuddleTurnAsync("rollcall", member.Name, StreamingContent);
            }
        }
        finally
        {
            IsStreaming = false;
            StreamingRole = "Quarterback";
            StreamingContent = "";
        }
    }

    [RelayCommand]
    public async Task<string?> SnapTheBall()
    {
        if (Session == null) return null;
        _log.Information("Snapping the ball from huddle");
        try
        {
            // Store the last QB message as the primary plan (should contain the final playbook),
            // with the full conversation as context
            var lastQbMessage = Messages.LastOrDefault(m => m.Role == "quarterback")?.Content ?? "";
            var fullConversation = string.Join("\n\n---\n\n",
                Messages.Select(m => $"**{Speaker(m)}:**\n{m.Content}"));

            Session.Drive.HuddlePlan = $"{fullConversation}\n\n===== FINAL PLAYBOOK =====\n\n{lastQbMessage}";
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
        StreamingRole = leader.Name;
        StreamingContent = "";
        _cts = new CancellationTokenSource();

        try
        {
            // The QB's definition is the leader's editable SystemPrompt (Teams page).
            // Fall back to the shared default only if a team somehow has none.
            var systemPrompt = (string.IsNullOrWhiteSpace(leader.SystemPrompt)
                ? AgentDefaults.QuarterbackSystemPrompt
                : leader.SystemPrompt)
                + "\n\n" + (Session?.Team?.BuildRosterPrompt() ?? "");

            // Build conversation as a threaded prompt so the QB has full context (incl. any research / roll call).
            var conversationParts = Messages.Select(msg => $"[{Speaker(msg)}]\n{msg.Content}");
            var prompt = string.Join("\n\n", conversationParts) + "\n\n[Quarterback]\n";

            // The huddle QB runs on Claude. Honor the drive's primary model when the provider is Claude;
            // a Codex drive still plans the huddle with the team's Claude leader model.
            var qbModel = Session?.ProviderId == "claude-code" && !string.IsNullOrEmpty(Session?.ModelId)
                ? Session!.ModelId!
                : leader.Model.ToModelId();
            // The --effort flag isn't supported on Haiku.
            var effort = qbModel.Contains("haiku", StringComparison.OrdinalIgnoreCase)
                ? null
                : (Session?.Effort ?? AgentEffort.High).ToCliValue();

            await foreach (var chunk in _service.StreamQbResponseAsync(
                qbModel,
                systemPrompt,
                prompt, Session?.RepoPath, effort, _cts.Token))
            {
                StreamingContent += chunk;
            }

            var qbContent = StreamingContent;
            Messages = [..Messages, new HuddleMessage { Role = "quarterback", Name = leader.Name, Content = qbContent }];
            await PersistHuddleTurnAsync("assistant", leader.Name, qbContent);
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsStreaming = false;
            StreamingContent = "";
        }
    }

    private async Task PersistHuddleTurnAsync(string role, string agentName, string content)
    {
        if (Session?.Drive is not { Id: > 0 } drive) return;
        await _service.AddTurnAsync(new DriveTurn
        {
            DriveId = drive.Id,
            Phase = TurnPhase.Huddle,
            Role = role,
            AgentName = agentName,
            Content = content
        });
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
