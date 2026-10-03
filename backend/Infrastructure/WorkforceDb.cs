using Microsoft.EntityFrameworkCore;
using Workforce.Domain;

namespace Workforce.Infrastructure;

public class WorkforceDb(DbContextOptions<WorkforceDb> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Requirement> Requirements => Set<Requirement>();
    public DbSet<ApplicationRecord> Applications => Set<ApplicationRecord>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Fact> Facts => Set<Fact>();
    public DbSet<Analysis> Analyses => Set<Analysis>();
    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Interview> Interviews => Set<Interview>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<EmployeeSkill> EmployeeSkills => Set<EmployeeSkill>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<User>().HasIndex(x => x.Email).IsUnique();
        m.Entity<Department>().HasIndex(x => x.Name).IsUnique();
        m.Entity<Skill>().HasIndex(x => x.Name).IsUnique();
        m.Entity<ApplicationRecord>().Property(x => x.EvidenceVersion).IsConcurrencyToken();
        m.Entity<ApplicationRecord>().HasIndex(x => new { x.JobId, x.CandidateId }).IsUnique();
        m.Entity<EmployeeSkill>().HasIndex(x => new { x.UserId, x.SkillId }).IsUnique();
        m.Entity<Job>()
            .HasMany(x => x.Requirements)
            .WithOne()
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<User>()
            .HasOne<Department>()
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        m.Entity<Job>()
            .HasOne<Department>()
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        m.Entity<Requirement>()
            .HasOne<Skill>()
            .WithMany()
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
        m.Entity<ApplicationRecord>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);
        m.Entity<ApplicationRecord>()
            .HasOne<Job>()
            .WithMany()
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Restrict);
        m.Entity<Document>()
            .HasOne<ApplicationRecord>()
            .WithMany()
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<Fact>()
            .HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<Fact>()
            .HasOne<Skill>()
            .WithMany()
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
        foreach (
            var t in new[]
            {
                typeof(Fact),
                typeof(Analysis),
                typeof(ProcessingJob),
                typeof(Review),
                typeof(Interview),
                typeof(Decision),
            }
        )
            m.Entity(t)
                .HasOne(typeof(ApplicationRecord))
                .WithMany()
                .HasForeignKey("ApplicationId")
                .OnDelete(DeleteBehavior.Cascade);
        m.Entity<EmployeeSkill>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<EmployeeSkill>()
            .HasOne<Skill>()
            .WithMany()
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
        m.Entity<ProcessingJob>().HasIndex(x => new { x.Status, x.AvailableAt });
    }
}
