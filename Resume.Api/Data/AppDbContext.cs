using Microsoft.EntityFrameworkCore;
using Resume.Api.Models;

namespace Resume.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CandidateScore> CandidateScores => Set<CandidateScore>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
    }
}
