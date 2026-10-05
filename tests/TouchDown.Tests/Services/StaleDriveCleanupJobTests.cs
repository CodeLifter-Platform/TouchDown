using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TD.Models;
using TD.Services;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.Services;

/// <summary>
/// The recurring sweep that gives up on a drive nothing has touched for half an hour. The
/// clock is injected so the boundary is exact rather than "sleep and hope".
/// </summary>
public class StaleDriveCleanupJobTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private static StaleDriveCleanupJob Job(TestDb db) =>
        new(db, NullLogger<StaleDriveCleanupJob>.Instance, new FakeTimeProvider(new DateTimeOffset(Now)));

    private static async Task<int> SeedAsync(TestDb db, DriveStatus status, TimeSpan age)
    {
        await using var ctx = db.CreateDbContext();
        var drive = new Drive { TaskDescription = "t", AgentTeamId = 1, Status = status, CreatedAt = Now - age };
        ctx.Drives.Add(drive);
        await ctx.SaveChangesAsync();
        return drive.Id;
    }

    [Fact]
    public async Task A_drive_in_progress_past_the_timeout_is_turned_over_with_an_explanation()
    {
        using var db = TestDb.Create();
        var id = await SeedAsync(db, DriveStatus.InProgress, StaleDriveCleanupJob.Timeout + TimeSpan.FromMinutes(1));

        await Job(db).ExecuteAsync();

        await using var verify = db.CreateDbContext();
        var drive = await verify.Drives.SingleAsync(d => d.Id == id);
        Assert.Equal(DriveStatus.Turnover, drive.Status);
        Assert.Equal(Now, drive.CompletedAt);
        var log = await verify.DriveLogs.SingleAsync(l => l.DriveId == id);
        Assert.Equal(TD.Models.LogLevel.Warning, log.Level);
        Assert.Contains("timed out", log.Message);
    }

    [Fact]
    public async Task A_drive_in_progress_inside_the_timeout_is_left_running()
    {
        using var db = TestDb.Create();
        var id = await SeedAsync(db, DriveStatus.InProgress, StaleDriveCleanupJob.Timeout - TimeSpan.FromMinutes(1));

        await Job(db).ExecuteAsync();

        await using var verify = db.CreateDbContext();
        Assert.Equal(DriveStatus.InProgress, (await verify.Drives.SingleAsync(d => d.Id == id)).Status);
        Assert.Empty(verify.DriveLogs);
    }

    [Fact]
    public async Task Finished_and_draft_drives_are_never_touched_however_old()
    {
        using var db = TestDb.Create();
        var touchdown = await SeedAsync(db, DriveStatus.Touchdown, TimeSpan.FromDays(3));
        var huddle = await SeedAsync(db, DriveStatus.Huddle, TimeSpan.FromDays(3));
        var cancelled = await SeedAsync(db, DriveStatus.Cancelled, TimeSpan.FromDays(3));

        await Job(db).ExecuteAsync();

        await using var verify = db.CreateDbContext();
        Assert.Equal(DriveStatus.Touchdown, (await verify.Drives.SingleAsync(d => d.Id == touchdown)).Status);
        Assert.Equal(DriveStatus.Huddle, (await verify.Drives.SingleAsync(d => d.Id == huddle)).Status);
        Assert.Equal(DriveStatus.Cancelled, (await verify.Drives.SingleAsync(d => d.Id == cancelled)).Status);
        Assert.Empty(verify.DriveLogs);
    }
}
