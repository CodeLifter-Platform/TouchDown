using System.Runtime.CompilerServices;
using TD.Areas.Drives.New;
using TD.Models;

namespace TouchDown.Tests.ViewModels;

/// <summary>
/// The huddle: the Head Coach's chat with the Quarterback before the snap. Every turn is
/// persisted as it happens, a typed roll call runs the real one instead of letting the QB
/// invent answers, and the ball cannot be snapped until there is a plan to snap.
/// </summary>
public class HuddleVMTests
{
    private static AgentTeam Team() => new()
    {
        Id = 1,
        Name = "The Playbook",
        Members =
        [
            new AgentMember { Id = 1, Name = "The Quarterback", Role = AgentRole.Leader, Model = ClaudeModel.Opus, SystemPrompt = "You call the plays." },
            new AgentMember { Id = 2, Name = "The Offensive Line", Role = AgentRole.Worker, Model = ClaudeModel.Sonnet },
        ],
    };

    private static AgentSession Session() => new()
    {
        Team = Team(),
        TaskDescription = "Add a login page",
        RepoPath = "/tmp/repo",
        ProviderId = "claude-code",
    };

    [Theory]
    [InlineData("Roll call!")]
    [InlineData("rollcall please")]
    [InlineData("everyone sound off")]
    [InlineData("Can all our agents report their status?")]
    [InlineData("Whole team, introduce yourselves")]
    public void A_request_for_the_whole_team_to_report_is_a_roll_call(string text)
    {
        Assert.True(HuddleVM.LooksLikeRollCall(text));
    }

    [Theory]
    [InlineData("What is the status of the login page?")]
    [InlineData("Everyone agrees the API is the first step")]
    [InlineData("Report back when the build is green")]
    [InlineData("")]
    public void An_ordinary_question_is_not_a_roll_call(string text)
    {
        Assert.False(HuddleVM.LooksLikeRollCall(text));
    }

    [Fact]
    public async Task Opening_the_huddle_persists_the_draft_the_opener_and_the_quarterbacks_reply()
    {
        var service = new ScriptedHuddleService { QbReply = "Here is the plan: build then review." };
        var vm = new HuddleVM(service);
        var session = Session();

        await vm.InitializeWithSession(session);

        Assert.Equal(1, session.Drive.Id);
        Assert.Equal(2, vm.Messages.Count);
        Assert.Equal(("user", "Head Coach", "Add a login page"), (vm.Messages[0].Role, vm.Messages[0].Name, vm.Messages[0].Content));
        Assert.Equal("quarterback", vm.Messages[1].Role);
        Assert.Equal("The Quarterback", vm.Messages[1].Name);
        Assert.Equal("Here is the plan: build then review. ", vm.Messages[1].Content);
        Assert.False(vm.IsStreaming);

        Assert.Equal(["user", "assistant"], service.Turns.Select(t => t.Role));
        Assert.All(service.Turns, t => Assert.Equal(TurnPhase.Huddle, t.Phase));
        Assert.All(service.Turns, t => Assert.Equal(1, t.DriveId));

        // The QB was briefed with its own prompt plus the roster, on the drive's provider.
        var call = Assert.Single(service.QbCalls);
        Assert.Equal("claude-code", call.ProviderId);
        Assert.Contains("You call the plays.", call.SystemPrompt);
        Assert.Contains("The Offensive Line", call.SystemPrompt);
        Assert.EndsWith("[Quarterback]\n", call.Prompt);
    }

    [Fact]
    public async Task A_blank_message_is_ignored()
    {
        var service = new ScriptedHuddleService { QbReply = "plan" };
        var vm = new HuddleVM(service);
        await vm.InitializeWithSession(Session());
        var before = vm.Messages.Count;
        vm.UserInput = "   ";

        await vm.SendMessage();

        Assert.Equal(before, vm.Messages.Count);
        Assert.Single(service.QbCalls);
    }

    [Fact]
    public async Task A_typed_roll_call_asks_every_member_rather_than_the_quarterback()
    {
        var service = new ScriptedHuddleService { QbReply = "ready" };
        var vm = new HuddleVM(service);
        await vm.InitializeWithSession(Session());
        vm.UserInput = "roll call";

        await vm.SendMessage();

        var rollCall = vm.Messages.Where(m => m.Role == "rollcall").ToList();
        Assert.Equal(["The Quarterback", "The Offensive Line"], rollCall.Select(m => m.Name));
        Assert.Equal(3, service.QbCalls.Count);
        Assert.Equal("", vm.UserInput);
    }

    [Fact]
    public async Task The_ball_cannot_be_snapped_until_the_quarterback_has_answered()
    {
        var vm = new HuddleVM(new ScriptedHuddleService { QbReply = "plan" });
        Assert.False(vm.CanSnap);

        await vm.InitializeWithSession(Session());

        Assert.True(vm.CanSnap);
    }

    [Fact]
    public async Task Snapping_hands_the_whole_conversation_and_the_final_playbook_to_the_drive()
    {
        var service = new ScriptedHuddleService { QbReply = "FINAL: build then review", StartedDriveId = "drive-9" };
        var vm = new HuddleVM(service);
        var session = Session();
        await vm.InitializeWithSession(session);

        var id = await vm.SnapTheBall();

        Assert.Equal("drive-9", id);
        Assert.Same(session, service.StartedWith);
        Assert.Contains("===== FINAL PLAYBOOK =====", session.Drive.HuddlePlan);
        Assert.Contains("**Head Coach:**\nAdd a login page", session.Drive.HuddlePlan);
        Assert.EndsWith("FINAL: build then review ", session.Drive.HuddlePlan);
    }

    [Fact]
    public async Task A_snap_that_fails_is_reported_as_a_huddle_error()
    {
        var service = new ScriptedHuddleService { QbReply = "plan", StartFailure = new DrivesNewServiceException("no providers") };
        var vm = new HuddleVM(service);
        await vm.InitializeWithSession(Session());

        var ex = await Assert.ThrowsAsync<HuddleVMException>(() => vm.SnapTheBall());

        Assert.Contains("no providers", ex.InnerException?.Message);
    }

    [Fact]
    public async Task A_quarterback_that_fails_mid_reply_leaves_the_huddle_usable()
    {
        var service = new ScriptedHuddleService { QbReply = "plan", QbFailure = new InvalidOperationException("CLI died") };
        var vm = new HuddleVM(service);

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.InitializeWithSession(Session()));

        Assert.False(vm.IsStreaming);
        Assert.Equal("", vm.StreamingContent);
        Assert.Single(vm.Messages);
    }

    private sealed record QbCall(string? ProviderId, string ModelId, string SystemPrompt, string Prompt);

    private sealed class ScriptedHuddleService : IDrivesNewService
    {
        public string QbReply { get; init; } = "";
        public Exception? QbFailure { get; init; }
        public string StartedDriveId { get; init; } = "drive-1";
        public Exception? StartFailure { get; init; }

        public List<DriveTurn> Turns { get; } = [];
        public List<QbCall> QbCalls { get; } = [];
        public AgentSession? StartedWith { get; private set; }

        public Task<Drive> CreateDraftDriveAsync(AgentSession session) =>
            Task.FromResult(new Drive { Id = 1, DriveId = "draft-1", Status = DriveStatus.Huddle });

        public Task AddTurnAsync(DriveTurn turn)
        {
            Turns.Add(turn);
            return Task.CompletedTask;
        }

        public Task<Drive> StartDriveAsync(AgentSession session)
        {
            StartedWith = session;
            if (StartFailure is not null) throw StartFailure;
            return Task.FromResult(new Drive { Id = 1, DriveId = StartedDriveId });
        }

        public async IAsyncEnumerable<string> StreamQbResponseAsync(
            string? providerId, string modelId, string systemPrompt, string prompt, string? workingDir,
            string? effort = null, [EnumeratorCancellation] CancellationToken ct = default)
        {
            QbCalls.Add(new QbCall(providerId, modelId, systemPrompt, prompt));
            if (QbFailure is not null) throw QbFailure;
            foreach (var word in QbReply.Split(' '))
            {
                yield return word + " ";
                await Task.Yield();
            }
        }

        public IAsyncEnumerable<string> StreamCoordinatorResearchAsync(string? providerId, string modelId, string systemPrompt, string prompt, string? workingDir, string? effort = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<AvailableProvider>> GetAvailableProvidersAsync() => throw new NotSupportedException();
        public Task<List<AgentTeam>> GetAvailableTeamsAsync() => throw new NotSupportedException();
        public Task<AgentTeam> SaveCustomTeamAsync(AgentTeam team) => throw new NotSupportedException();
    }
}
