namespace Workforce.Domain;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Candidate";
    public Guid? DepartmentId { get; set; }
    public bool Active { get; set; } = true;
}

public class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
}

public class Skill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Aliases { get; set; } = "[]";
    public string Description { get; set; } = "";
}

public class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DepartmentId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Open { get; set; } = true;
    public List<Requirement> Requirements { get; set; } = [];
}

public class Requirement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public Guid SkillId { get; set; }
    public int Proficiency { get; set; } = 1;
}

public class ApplicationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public Guid CandidateId { get; set; }
    public string Status { get; set; } = "Submitted";
    public int EvidenceVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public string OriginalName { get; set; } = "";
    public string StorageName { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
    public string Sections { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(180);
}

public class Fact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid? SkillId { get; set; }
    public string Kind { get; set; } = "skill";
    public string Value { get; set; } = "";
    public string SectionId { get; set; } = "";
    public string Quote { get; set; } = "";
    public string Verification { get; set; } = "Unverified";
    public int? Proficiency { get; set; }
    public string Mapping { get; set; } = "Unmapped";
    public string Suggestions { get; set; } = "[]";
}

public class Analysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public int EvidenceVersion { get; set; }
    public bool Stale { get; set; }
    public string Result { get; set; } = "{}";
    public string Metadata { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ProcessingJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public Guid? DocumentId { get; set; }
    public string Kind { get; set; } = "Extract";
    public string Status { get; set; } = "Queued";
    public int Attempts { get; set; }
    public string? Error { get; set; }
    public DateTime AvailableAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Review
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Interview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string Location { get; set; } = "";
    public string Notes { get; set; } = "";
    public Guid ActorId { get; set; }
}

public class Decision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public Guid ActorId { get; set; }
    public string Outcome { get; set; } = "";
    public string Rationale { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class EmployeeSkill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid SkillId { get; set; }
    public int Proficiency { get; set; } = 1;
    public string Evidence { get; set; } = "";
    public string Verification { get; set; } = "Unverified";
    public string DevelopmentNeed { get; set; } = "";
}

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
