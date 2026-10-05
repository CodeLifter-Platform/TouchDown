using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TD.Hubs;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.Integration;

/// <summary>
/// The orchestrator publishes anonymous objects through <see cref="IHubContext{AgentHub}"/>
/// and the monitor page reads them back as <see cref="JsonElement"/>s by property name, with
/// case-sensitive <c>TryGetProperty("AgentName")</c> lookups. Nothing in the type system ties
/// the two together, so this hosts the real hub, publishes each message the orchestrator
/// sends, and reads it the way the page does.
///
/// Regression: the default SignalR JSON protocol camel-cases property names, so every one of
/// these lookups missed. Logs showed up as "System" with an empty message, every agent card
/// stayed blank, and a finished drive was reported as a Turnover because the status read as
/// "". The hub protocol now keeps the server's property names.
/// </summary>
public class AgentHubWireContractTests : IAsyncLifetime
{
    private const string DriveId = "wire-drive-1";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly HostedApp _app = new();
    private HubConnection _client = null!;
    private IHubContext<AgentHub> _hub = null!;

    public async Task InitializeAsync()
    {
        _client = await _app.ConnectToDriveAsync(DriveId);
        _hub = _app.Services.GetRequiredService<IHubContext<AgentHub>>();
    }

    public async Task DisposeAsync()
    {
        await _client.DisposeAsync();
        _app.Dispose();
    }

    /// <summary>Publishes to the drive's group and returns what the client received.</summary>
    private async Task<JsonElement> RoundTrip(string method, object payload)
    {
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = _client.On<JsonElement>(method, el => received.TrySetResult(el));

        await _hub.Clients.Group(DriveId).SendAsync(method, payload);

        return await received.Task.WaitAsync(Timeout);
    }

    /// <summary>Reads a property exactly as the monitor page does: by name, case-sensitively.</summary>
    private static JsonElement Property(JsonElement el, string name)
    {
        Assert.True(el.TryGetProperty(name, out var value),
            $"The client should see a property named '{name}' (exact case) on {el.GetRawText()}");
        return value;
    }

    [Fact]
    public async Task A_log_line_arrives_with_the_names_the_monitor_page_reads()
    {
        var received = await RoundTrip("ReceiveLog", new
        {
            Timestamp = DateTime.UtcNow,
            AgentName = "The Offensive Line #1",
            Message = "Starting: add the login page",
            Level = "Info"
        });

        Assert.Equal("The Offensive Line #1", Property(received, "AgentName").GetString());
        Assert.Equal("Starting: add the login page", Property(received, "Message").GetString());
        Assert.Equal("Info", Property(received, "Level").GetString());
        Assert.Equal(JsonValueKind.String, Property(received, "Timestamp").ValueKind);
    }

    [Fact]
    public async Task An_agent_status_update_carries_name_status_and_progress()
    {
        var received = await RoundTrip("AgentStatusUpdate", new
        {
            AgentName = "The Safety",
            Status = "Running",
            ProgressPercent = 0
        });

        Assert.Equal("The Safety", Property(received, "AgentName").GetString());
        Assert.Equal("Running", Property(received, "Status").GetString());
        Assert.Equal(0, Property(received, "ProgressPercent").GetInt32());
    }

    [Fact]
    public async Task A_completed_drive_reports_its_status_so_a_touchdown_is_not_shown_as_a_turnover()
    {
        // MarkDriveCompleted maps anything other than "Touchdown" to Turnover, so a missing
        // Status property turns every success into a failure on screen.
        var received = await RoundTrip("DriveCompleted", new { DriveId, Status = "Touchdown" });

        Assert.Equal("Touchdown", Property(received, "Status").GetString());
        Assert.Equal(DriveId, Property(received, "DriveId").GetString());
    }

    [Fact]
    public async Task A_phase_change_names_the_phase()
    {
        var received = await RoundTrip("DrivePhaseChanged", new { DriveId, Phase = "Executing Wave 1 of 2" });

        Assert.Equal("Executing Wave 1 of 2", Property(received, "Phase").GetString());
    }

    [Fact]
    public async Task The_play_board_arrives_as_a_list_with_each_plays_fields()
    {
        var received = await RoundTrip("PlaysReady", new
        {
            DriveId,
            Plays = new[]
            {
                new { Id = 7, AgentName = "The Offensive Line #1", Description = "build it", Status = "Pending", OrderIndex = 0 },
                new { Id = 8, AgentName = "The Safety", Description = "review it", Status = "Pending", OrderIndex = 1 },
            }
        });

        var plays = Property(received, "Plays");
        Assert.Equal(JsonValueKind.Array, plays.ValueKind);
        var first = plays.EnumerateArray().First();
        Assert.Equal(7, Property(first, "Id").GetInt32());
        Assert.Equal("The Offensive Line #1", Property(first, "AgentName").GetString());
        Assert.Equal("build it", Property(first, "Description").GetString());
        Assert.Equal("Pending", Property(first, "Status").GetString());
        Assert.Equal(0, Property(first, "OrderIndex").GetInt32());
    }

    [Fact]
    public async Task A_play_status_update_carries_the_timestamps_as_parseable_dates()
    {
        var started = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var received = await RoundTrip("PlayStatusUpdate", new
        {
            PlayId = 7,
            AgentName = "The Offensive Line #1",
            Status = "Completed",
            StartedAt = started,
            CompletedAt = (DateTime?)null
        });

        Assert.Equal(7, Property(received, "PlayId").GetInt32());
        Assert.Equal("Completed", Property(received, "Status").GetString());
        Assert.True(Property(received, "StartedAt").TryGetDateTime(out var parsed));
        Assert.Equal(started, parsed.ToUniversalTime());
        Assert.Equal(JsonValueKind.Null, Property(received, "CompletedAt").ValueKind);
    }

    [Fact]
    public async Task A_client_watching_another_drive_does_not_receive_this_drives_messages()
    {
        await using var other = await _app.ConnectToDriveAsync("some-other-drive");
        var leaked = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var leakHandler = other.On<JsonElement>("ReceiveLog", el => leaked.TrySetResult(el));

        // Delivered to this drive's client, which proves the message went out at all.
        var received = await RoundTrip("ReceiveLog", new { AgentName = "System", Message = "hello", Level = "Info" });
        Assert.Equal("hello", Property(received, "Message").GetString());

        var completed = await Task.WhenAny(leaked.Task, Task.Delay(500));
        Assert.NotSame(leaked.Task, completed);
    }
}
