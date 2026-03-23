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
        });

        // Seed the default Playbook team
        var team = new { Id = 1, Name = "The Playbook", Description = "The default TD agent team.", IsDefault = true };
        modelBuilder.Entity<AgentTeam>().HasData(team);

        modelBuilder.Entity<AgentMember>().HasData(
            new { Id = 1, Name = "The Quarterback", Role = AgentRole.Leader, Model = ClaudeModel.Opus, SystemPrompt = "You are the Quarterback — the team leader. You read the task, create a structured plan, delegate assignments to your team, and coordinate the drive to completion.", AgentTeamId = 1 },
            new { Id = 2, Name = "Left Guard", Role = AgentRole.Worker, Model = ClaudeModel.Sonnet, SystemPrompt = "You are the Left Guard — a core implementer. You receive your assignment from the Quarterback and execute it with precision.", AgentTeamId = 1 },
            new { Id = 3, Name = "Right Guard", Role = AgentRole.Worker, Model = ClaudeModel.Sonnet, SystemPrompt = "You are the Right Guard — a parallel implementer. You work alongside the Left Guard on your assigned portion.", AgentTeamId = 1 },
            new { Id = 4, Name = "The Safety", Role = AgentRole.Validator, Model = ClaudeModel.Sonnet, SystemPrompt = "You are the Safety — the code reviewer. You review all output from the Guards before it merges.", AgentTeamId = 1 },
            new { Id = 5, Name = "The Scout", Role = AgentRole.Tester, Model = ClaudeModel.Haiku, SystemPrompt = "You are the Scout — fast and lightweight. You write and run tests concurrently with implementation.", AgentTeamId = 1 },
            new { Id = 6, Name = "Special Teams", Role = AgentRole.DevOps, Model = ClaudeModel.Haiku, SystemPrompt = "You are Special Teams — handling CI/CD, infrastructure, and build pipeline work.", AgentTeamId = 1 }
        );

        modelBuilder.Entity<CommunicationRule>().HasData(
            new { Id = 1, Style = CommStyle.LeaderGated, Description = "QB calls plays, Guards run parallel, Safety reviews before merge, Scout runs concurrently.", AgentTeamId = 1 }
        );
    }
}
