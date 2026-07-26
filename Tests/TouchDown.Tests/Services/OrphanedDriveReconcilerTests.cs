using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TD.Models;
using TD.Services;
using TouchDown.Tests.TestSupport;

namespace TouchDown.Tests.Services;

/// <summary>
/// Drive execution is an in-process background task, so a restart orphans anything
/// still InProgress. Without reconciliation those drives show a live spinner for work
/// that already died, until the 30-minute stale sweep eventually notices.
/// </summary>
public class OrphanedDriveReconcilerTests
{
    private static OrphanedDriveReconciler CreateReconciler(TestDb db) =>
        new(db, NullLogger<OrphanedDriveReconciler>.Instance);

    private static Drive NewDrive(DriveStatus status, string task = "do the thing") => new()
    {
        Status = status,
        TaskDescription = task,
        AgentTeamId = 1,
    };

    [Fact]
    public async Task ReconcileAsync_MarksInProgressDrivesAsTurnover()
    {
        using var db = TestDb.Create();
        await using (var ctx = db.CreateDbContext())
        {
            ctx.Drives.Add(NewDrive(DriveStatus.InProgress));
            await ctx.SaveChangesAsync();
        }

        var count = await CreateReconciler(db).ReconcileAsync();

        Assert.Equal(1, count);
        await using var verify = db.CreateDbContext();
        var drive = await verify.Drives.SingleAsync();
        Assert.Equal(DriveStatus.Turnover, drive.Status);
        Assert.NotNull(drive.CompletedAt);
    }

    [Fact]
    public async Task ReconcileAsync_WritesAnExplanatoryLog()
    {
        using var db = TestDb.Create();
        await using (var ctx = db.CreateDbContext())
        {
            ctx.Drives.Add(NewDrive(DriveStatus.InProgress));
            await ctx.SaveChangesAsync();
        }

        await CreateReconciler(db).ReconcileAsync();

        await using var verify = db.CreateDbContext();
        var log = await verify.DriveLogs.SingleAsync();
        Assert.Equal("System", log.AgentName);
        Assert.Equal(TD.Models.LogLevel.Warning, log.Level);
        Assert.Contains("restarted", log.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DriveStatus.Touchdown)]
    [InlineData(DriveStatus.Turnover)]
    [InlineData(DriveStatus.Cancelled)]
    [InlineData(DriveStatus.Pending)]
    [InlineData(DriveStatus.Huddle)]
    public async Task ReconcileAsync_LeavesOtherStatusesAlone(DriveStatus status)
    {
        // A Huddle drive in particular is a draft the user may still be working on.
        using var db = TestDb.Create();
        await using (var ctx = db.CreateDbContext())
        {
            ctx.Drives.Add(NewDrive(status));
            await ctx.SaveChangesAsync();
        }

        var count = await CreateReconciler(db).ReconcileAsync();

        Assert.Equal(0, count);
        await using var verify = db.CreateDbContext();
        Assert.Equal(status, (await verify.Drives.SingleAsync()).Status);
        Assert.Empty(verify.DriveLogs);
    }

    [Fact]
    public async Task ReconcileAsync_ReconcilesEveryOrphanedDrive()
    {
        using var db = TestDb.Create();
        await using (var ctx = db.CreateDbContext())
        {
            ctx.Drives.AddRange(
                NewDrive(DriveStatus.InProgress, "one"),
                NewDrive(DriveStatus.InProgress, "two"),
                NewDrive(DriveStatus.Touchdown, "already done"));
            await ctx.SaveChangesAsync();
        }

        var count = await CreateReconciler(db).ReconcileAsync();

        Assert.Equal(2, count);
        await using var verify = db.CreateDbContext();
        Assert.Equal(0, await verify.Drives.CountAsync(d => d.Status == DriveStatus.InProgress));
        Assert.Equal(1, await verify.Drives.CountAsync(d => d.Status == DriveStatus.Touchdown));
    }

    [Fact]
    public async Task ReconcileAsync_EmptyDatabase_IsANoOp()
    {
        using var db = TestDb.Create();

        var count = await CreateReconciler(db).ReconcileAsync();

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ReconcileAsync_IsIdempotent()
    {
        using var db = TestDb.Create();
        await using (var ctx = db.CreateDbContext())
        {
            ctx.Drives.Add(NewDrive(DriveStatus.InProgress));
            await ctx.SaveChangesAsync();
        }
        var reconciler = CreateReconciler(db);

        var first = await reconciler.ReconcileAsync();
        var second = await reconciler.ReconcileAsync();

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        await using var verify = db.CreateDbContext();
        Assert.Equal(1, await verify.DriveLogs.CountAsync());
    }
}
