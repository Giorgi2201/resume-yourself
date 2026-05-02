using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Resume.Api.Models;

namespace Resume.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CandidateScore> CandidateScores => Set<CandidateScore>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CandidateScore>()
            .HasOne(s => s.Candidate)
            .WithMany(c => c.Scores)
            .HasForeignKey(s => s.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CandidateScore>()
            .HasOne(s => s.Job)
            .WithMany(j => j.Scores)
            .HasForeignKey(s => s.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Feedback>()
            .HasOne(f => f.Candidate)
            .WithMany(c => c.Feedbacks)
            .HasForeignKey(f => f.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Feedback>()
            .HasOne(f => f.Job)
            .WithMany(j => j.Feedbacks)
            .HasForeignKey(f => f.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CandidateScore>()
            .HasIndex(s => new { s.JobId, s.Rank });

        modelBuilder.Entity<CandidateScore>()
            .HasIndex(s => new { s.JobId, s.CandidateId })
            .IsUnique();

        modelBuilder.Entity<Feedback>()
            .HasIndex(f => new { f.CandidateId, f.JobId })
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(t => t.TokenHash)
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
            .HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
