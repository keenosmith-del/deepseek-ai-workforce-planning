using System.ComponentModel.DataAnnotations;

namespace Workforce.Application;

public record LoginDto([Required, EmailAddress] string Email, [Required] string Password);

public record RegisterDto(
    [Required, EmailAddress] string Email,
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    [Required, StringLength(128, MinimumLength = 12)] string Password
);

public record DepartmentDto([Required, StringLength(100, MinimumLength = 2)] string Name);

public record RequirementDto(Guid SkillId, [Range(1, 4)] int Proficiency);

public record JobDto(
    Guid DepartmentId,
    [Required, StringLength(150)] string Title,
    [Required, StringLength(6000)] string Description,
    bool Open,
    [Required] List<RequirementDto> Requirements
);

public record SkillDto(
    [Required, StringLength(100)] string Name,
    [Required, StringLength(1000)] string Description,
    [Required] List<string> Aliases
);

public record EmployeeDto(
    [Required, EmailAddress] string Email,
    [Required, StringLength(100)] string Name,
    Guid DepartmentId,
    [Required, StringLength(128, MinimumLength = 12)] string Password
);

public record FactDto(
    int EvidenceVersion,
    Guid? SkillId,
    [Required, StringLength(2000)] string Value,
    [Required] string Verification,
    int? Proficiency,
    [Required, StringLength(2000)] string Note
);

public record ReviewDto([Required, StringLength(2000)] string Note);

public record StatusDto([Required] string Status, [Required, StringLength(2000)] string Rationale);

public record InterviewDto(
    DateTime ScheduledAt,
    [Required, StringLength(500)] string Location,
    [StringLength(4000)] string Notes
);

public record EmployeeSkillDto(
    Guid SkillId,
    [Range(1, 4)] int Proficiency,
    [Required, StringLength(2000)] string Evidence,
    [StringLength(1000)] string DevelopmentNeed
);

public record VerifySkillDto([Required] string Verification);

public record AddFactDto(
    int EvidenceVersion,
    Guid DocumentId,
    [Required] string Kind,
    [Required, StringLength(2000)] string Value,
    Guid? SkillId,
    [Required] string SectionId,
    [Required, StringLength(2000)] string Quote
);
