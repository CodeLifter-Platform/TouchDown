using Microsoft.EntityFrameworkCore;
using TD.Models;

namespace TD.Data;

public class TDDbContext : DbContext
{
    public TDDbContext(DbContextOptions<TDDbContext> options) : base(options) { }

    public DbSet<AgentTeam> AgentTeams => Set<AgentTeam>();
    public DbSet<AgentMember> AgentMembers => Set<AgentMember>();
    public DbSet<CommunicationRule> CommunicationRules => Set<CommunicationRule>();
    public DbSet<Drive> Drives => Set<Drive>();
    public DbSet<Play> Plays => Set<Play>();
    public DbSet<DriveLog> DriveLogs => Set<DriveLog>();
    public DbSet<DriveTurn> DriveTurns => Set<DriveTurn>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AgentTeam>(entity =>
        {
            entity.HasMany(t => t.Members)
                .WithOne(m => m.AgentTeam)
                .HasForeignKey(m => m.AgentTeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(t => t.CommunicationRules)
                .WithOne(r => r.AgentTeam)
                .HasForeignKey(r => r.AgentTeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Drive>(entity =>
        {
            entity.HasIndex(d => d.DriveId).IsUnique();

            entity.HasMany(d => d.Plays)
                .WithOne(p => p.Drive)
                .HasForeignKey(p => p.DriveId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(d => d.Logs)
                .WithOne(l => l.Drive)
                .HasForeignKey(l => l.DriveId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(d => d.Turns)
                .WithOne(t => t.Drive)
                .HasForeignKey(t => t.DriveId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DriveTurn>(entity =>
        {
            entity.HasIndex(t => new { t.DriveId, t.Timestamp });
        });

        // Seed the default Playbook team from the single canonical definition, so the seeded
        // rows and AgentTeam.CreateThePlaybook() cannot drift apart (they previously had).
        modelBuilder.Entity<AgentTeam>().HasData(new
        {
            Id = PlaybookSeed.TeamId,
            Name = PlaybookSeed.TeamName,
            Description = PlaybookSeed.TeamDescription,
            IsDefault = true
        });

        modelBuilder.Entity<AgentMember>().HasData(
            PlaybookSeed.Members.Select(m => new
            {
                m.Id,
                m.Name,
                m.Role,
                m.Model,
                m.Effort,
                m.MaxInstances,
                m.SystemPrompt,
                AgentTeamId = PlaybookSeed.TeamId
            }).ToArray()
        );

        modelBuilder.Entity<CommunicationRule>().HasData(new
        {
            Id = PlaybookSeed.CommunicationRuleId,
            Style = PlaybookSeed.CommunicationStyle,
            Description = PlaybookSeed.CommunicationRuleDescription,
            AgentTeamId = PlaybookSeed.TeamId
        });
    }
}
