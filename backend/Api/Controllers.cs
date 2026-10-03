using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Workforce.Application;
using Workforce.Domain;
using Workforce.Infrastructure;

namespace Workforce.Api;

[ApiController, Route("api/auth")]
public class AuthController(WorkforceDb db, TokenService tokens) : ControllerBase
{
    [HttpPost("login"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var u = await db.Users.SingleOrDefaultAsync(x => x.Email == dto.Email.ToLowerInvariant());
        if (u == null || !u.Active || !Passwords.Verify(dto.Password, u.PasswordHash))
            return Unauthorized(new { message = "Invalid credentials" });
        return Ok(
            new
            {
                token = tokens.Issue(u),
                user = new
                {
                    u.Id,
                    u.Name,
                    u.Email,
                    u.Role,
                },
            }
        );
    }

    [HttpPost("register"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var email = dto.Email.ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email))
            return Conflict(new { message = "Email already registered" });
        var u = new User
        {
            Email = email,
            Name = dto.Name,
            PasswordHash = Passwords.Hash(dto.Password),
        };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return Ok(
            new
            {
                token = tokens.Issue(u),
                user = new
                {
                    u.Id,
                    u.Name,
                    u.Email,
                    u.Role,
                },
            }
        );
    }

    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Me([FromServices] Access access)
    {
        var u = await db.Users.FindAsync(access.Actor);
        return Ok(
            new
            {
                u!.Id,
                u.Name,
                u.Email,
                u.Role,
                u.DepartmentId,
            }
        );
    }
}

[ApiController, Route("api"), Authorize]
public class AdminController(
    WorkforceDb db,
    Access access,
    SemanticSkills semantic,
    IntelligenceClient ai
) : ControllerBase
{
    [HttpGet("departments")]
    public async Task<object> Departments() =>
        await db.Departments.OrderBy(x => x.Name).ToListAsync();

    [HttpPost("departments"), Authorize(Roles = "HRAdmin")]
    public async Task<IActionResult> Department(DepartmentDto dto)
    {
        if (await db.Departments.AnyAsync(x => x.Name == dto.Name))
            return Conflict(new { message = "Department exists" });
        var d = new Department { Name = dto.Name };
        db.Departments.Add(d);
        access.Audit("department.created", d.Id);
        await db.SaveChangesAsync();
        return Ok(d);
    }

    [HttpPut("departments/{id:guid}"), Authorize(Roles = "HRAdmin")]
    public async Task<IActionResult> DepartmentEdit(Guid id, DepartmentDto dto)
    {
        var d = await db.Departments.FindAsync(id);
        if (d == null)
            return NotFound();
        d.Name = dto.Name;
        access.Audit("department.updated", id);
        await db.SaveChangesAsync();
        return Ok(d);
    }

    [HttpGet("skills")]
    public async Task<object> Skills() => await db.Skills.OrderBy(x => x.Name).ToListAsync();

    [HttpPost("skills"), Authorize(Roles = "HRAdmin")]
    public async Task<IActionResult> Skill(SkillDto dto)
    {
        if (
            dto.Aliases.Count > 30
            || dto.Aliases.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 100)
        )
            return BadRequest(new { message = "Invalid aliases" });
        if (await db.Skills.AnyAsync(x => x.Name == dto.Name))
            return Conflict(new { message = "Skill exists" });
        var s = new Skill
        {
            Name = dto.Name,
            Description = dto.Description,
            Aliases = JsonSerializer.Serialize(dto.Aliases),
        };
        db.Skills.Add(s);
        access.Audit("skill.created", s.Id);
        await db.SaveChangesAsync();
        return Ok(s);
    }

    [HttpPost("skills/embeddings"), Authorize(Roles = "HRAdmin")]
    public async Task<object> Embeddings()
    {
        var count = await semantic.Refresh();
        access.Audit("taxonomy.embedded", count);
        await db.SaveChangesAsync();
        return new { count };
    }

    [HttpGet("skills/search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length > 1000)
            return BadRequest();
        return Ok(await semantic.Search(q));
    }

    [HttpGet("models"), Authorize(Roles = "HRAdmin,Recruiter")]
    public async Task<object> Models() => await ai.Models();

    [HttpGet("employees"), Authorize(Roles = "HRAdmin")]
    public async Task<object> Employees() =>
        await db
            .Users.Where(x => x.Role == "Employee")
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                x.DepartmentId,
                x.Active,
            })
            .ToListAsync();

    [HttpPost("employees"), Authorize(Roles = "HRAdmin")]
    public async Task<IActionResult> Employee(EmployeeDto dto)
    {
        if (!await db.Departments.AnyAsync(x => x.Id == dto.DepartmentId))
            return BadRequest(new { message = "Unknown department" });
        if (await db.Users.AnyAsync(x => x.Email == dto.Email.ToLowerInvariant()))
            return Conflict();
        var u = new User
        {
            Email = dto.Email.ToLowerInvariant(),
            Name = dto.Name,
            Role = "Employee",
            DepartmentId = dto.DepartmentId,
            PasswordHash = Passwords.Hash(dto.Password),
        };
        db.Users.Add(u);
        access.Audit("employee.created", u.Id);
        await db.SaveChangesAsync();
        return Ok(
            new
            {
                u.Id,
                u.Name,
                u.Email,
                u.DepartmentId,
            }
        );
    }

    [HttpPut("employees/{id:guid}"), Authorize(Roles = "HRAdmin")]
    public async Task<IActionResult> EmployeeEdit(Guid id, DepartmentAssignment dto)
    {
        var u = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.Role == "Employee");
        if (u == null)
            return NotFound();
        if (!await db.Departments.AnyAsync(x => x.Id == dto.DepartmentId))
            return BadRequest();
        u.DepartmentId = dto.DepartmentId;
        u.Active = dto.Active;
        access.Audit("employee.updated", id);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("audit"), Authorize(Roles = "HRAdmin")]
    public async Task<object> Audit() =>
        await db.AuditEvents.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync();

    [HttpGet("dashboard"), Authorize(Roles = "HRAdmin")]
    public async Task<object> Dashboard()
    {
        var employees = await db.Users.Where(x => x.Role == "Employee" && x.Active).ToListAsync();
        var inventory = await db.EmployeeSkills.ToListAsync();
        var jobs = await db.Jobs.Include(x => x.Requirements).Where(x => x.Open).ToListAsync();
        var departments = await db.Departments.ToListAsync();
        var skills = await db.Skills.ToListAsync();
        return new
        {
            departments = departments.Select(d => new
            {
                d.Id,
                d.Name,
                employees = employees.Count(e => e.DepartmentId == d.Id),
                vacancies = jobs.Count(j => j.DepartmentId == d.Id),
            }),
            coverage = skills.Select(s => new
            {
                s.Id,
                s.Name,
                documentedEmployees = inventory.Count(e =>
                    e.SkillId == s.Id
                    && e.Verification == "Documented"
                    && employees.Any(u => u.Id == e.UserId)
                ),
                unverifiedEmployees = inventory.Count(e =>
                    e.SkillId == s.Id
                    && e.Verification != "Documented"
                    && employees.Any(u => u.Id == e.UserId)
                ),
                vacanciesRequiring = jobs.Count(j => j.Requirements.Any(r => r.SkillId == s.Id)),
                departmentsWithoutDocumentedCoverage = departments.Count(d =>
                    jobs.Any(j =>
                        j.DepartmentId == d.Id && j.Requirements.Any(r => r.SkillId == s.Id)
                    )
                    && !inventory.Any(e =>
                        e.SkillId == s.Id
                        && e.Verification == "Documented"
                        && employees.Any(u => u.Id == e.UserId && u.DepartmentId == d.Id)
                    )
                ),
            }),
            development = inventory
                .Where(e => e.DevelopmentNeed != "" && employees.Any(u => u.Id == e.UserId))
                .GroupBy(e => e.SkillId)
                .Select(g => new { skillId = g.Key, count = g.Count() }),
            applicationStates = await db
                .Applications.GroupBy(x => x.Status)
                .Select(g => new { status = g.Key, count = g.Count() })
                .ToListAsync(),
        };
    }
}

public record DepartmentAssignment(Guid DepartmentId, bool Active);

[ApiController, Route("api/jobs"), Authorize]
public class JobsController(WorkforceDb db, Access access) : ControllerBase
{
    [HttpGet]
    public async Task<object> List() =>
        await db
            .Jobs.Include(x => x.Requirements)
            .Where(x => x.Open || access.Staff)
            .OrderBy(x => x.Title)
            .ToListAsync();

    async Task<string?> Validate(JobDto dto)
    {
        if (
            dto.Requirements.Count is < 1 or > 30
            || dto.Requirements.Select(x => x.SkillId).Distinct().Count() != dto.Requirements.Count
            || dto.Requirements.Any(x => x.Proficiency is < 1 or > 4)
        )
            return "1–30 distinct requirements with proficiency 1–4 required";
        if (!await db.Departments.AnyAsync(x => x.Id == dto.DepartmentId))
            return "Unknown department";
        if (
            await db.Skills.CountAsync(x => dto.Requirements.Select(r => r.SkillId).Contains(x.Id))
            != dto.Requirements.Count
        )
            return "Unknown skills";
        return null;
    }

    [HttpPost, Authorize(Roles = "HRAdmin,Recruiter")]
    public async Task<IActionResult> Create(JobDto dto)
    {
        var error = await Validate(dto);
        if (error != null)
            return BadRequest(new { message = error });
        var j = new Job
        {
            DepartmentId = dto.DepartmentId,
            Title = dto.Title,
            Description = dto.Description,
            Open = dto.Open,
            Requirements = dto
                .Requirements.Select(r => new Requirement
                {
                    SkillId = r.SkillId,
                    Proficiency = r.Proficiency,
                })
                .ToList(),
        };
        db.Jobs.Add(j);
        access.Audit("job.created", j.Id);
        await db.SaveChangesAsync();
        return Ok(j);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = "HRAdmin,Recruiter")]
    public async Task<IActionResult> Edit(Guid id, JobDto dto)
    {
        var error = await Validate(dto);
        if (error != null)
            return BadRequest(new { message = error });
        var j = await db.Jobs.Include(x => x.Requirements).SingleOrDefaultAsync(x => x.Id == id);
        if (j == null)
            return NotFound();
        j.Title = dto.Title;
        j.Description = dto.Description;
        j.DepartmentId = dto.DepartmentId;
        j.Open = dto.Open;
        db.Requirements.RemoveRange(j.Requirements);
        j.Requirements = dto
            .Requirements.Select(r => new Requirement
            {
                SkillId = r.SkillId,
                Proficiency = r.Proficiency,
            })
            .ToList();
        foreach (var a in await db.Applications.Where(x => x.JobId == id).ToListAsync())
            await access.Invalidate(a);
        access.Audit("job.updated", id);
        await db.SaveChangesAsync();
        return Ok(j);
    }

    [HttpPost("{id:guid}/apply"), Authorize(Roles = "Candidate")]
    public async Task<IActionResult> Apply(Guid id)
    {
        if (!await db.Jobs.AnyAsync(x => x.Id == id && x.Open))
            return NotFound();
        if (await db.Applications.AnyAsync(x => x.JobId == id && x.CandidateId == access.Actor))
            return Conflict(new { message = "Already applied" });
        var a = new ApplicationRecord { JobId = id, CandidateId = access.Actor };
        db.Applications.Add(a);
        access.Audit("application.submitted", a.Id);
        await db.SaveChangesAsync();
        return Ok(a);
    }
}

[ApiController, Route("api/applications"), Authorize]
public class ApplicationsController(WorkforceDb db, Access access, IConfiguration config)
    : ControllerBase
{
    string Root => config["Storage:Path"] ?? "/data/documents";

    [HttpGet]
    public async Task<object> List() =>
        await (
            from a in db.Applications
            join j in db.Jobs on a.JobId equals j.Id
            join u in db.Users on a.CandidateId equals u.Id
            where access.Staff || a.CandidateId == access.Actor
            orderby a.CreatedAt descending
            select new
            {
                a.Id,
                a.JobId,
                a.Status,
                a.EvidenceVersion,
                a.CreatedAt,
                jobTitle = j.Title,
                candidateName = u.Name,
            }
        ).ToListAsync();

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var a = await access.Application(id);
        if (a == null)
            return NotFound();
        var staff = access.Staff;
        return Ok(
            new
            {
                application = a,
                job = await db.Jobs.Include(x => x.Requirements).SingleAsync(x => x.Id == a.JobId),
                documents = await db
                    .Documents.Where(x => x.ApplicationId == id)
                    .Select(x => new
                    {
                        x.Id,
                        x.OriginalName,
                        x.Sections,
                        x.CreatedAt,
                        x.ExpiresAt,
                    })
                    .ToListAsync(),
                facts = await db.Facts.Where(x => x.ApplicationId == id).ToListAsync(),
                analyses = staff
                    ? await db
                        .Analyses.Where(x => x.ApplicationId == id)
                        .OrderByDescending(x => x.CreatedAt)
                        .ToListAsync()
                    : [],
                processing = await db
                    .ProcessingJobs.Where(x => x.ApplicationId == id)
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync(),
                reviews = staff
                    ? await db
                        .Reviews.Where(x => x.ApplicationId == id)
                        .OrderByDescending(x => x.CreatedAt)
                        .ToListAsync()
                    : [],
                interviews = await db
                    .Interviews.Where(x => x.ApplicationId == id)
                    .Select(x => new
                    {
                        x.Id,
                        x.ScheduledAt,
                        x.Location,
                        notes = staff ? x.Notes : null,
                    })
                    .ToListAsync(),
                decisions = staff
                    ? await db.Decisions.Where(x => x.ApplicationId == id).ToListAsync()
                    : [],
            }
        );
    }

    [
        HttpPost("{id:guid}/documents"),
        Authorize(Roles = "Candidate,Recruiter,HRAdmin"),
        RequestSizeLimit(6 * 1024 * 1024)
    ]
    public async Task<IActionResult> Upload(Guid id, IFormFile file)
    {
        var a = await access.Application(id);
        if (a == null)
            return NotFound();
        if (file.Length is < 1 or > 5 * 1024 * 1024)
            return BadRequest(new { message = "File must be 1 byte–5 MiB" });
        if (await db.Documents.CountAsync(x => x.ApplicationId == id) >= 5)
            return BadRequest(new { message = "Maximum five documents" });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".pdf" && ext != ".docx")
            return BadRequest(new { message = "PDF or DOCX required" });
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();
        if (
            (ext == ".pdf" && !bytes.AsSpan().StartsWith("%PDF-"u8))
            || (ext == ".docx" && !bytes.AsSpan().StartsWith("PK"u8))
        )
            return BadRequest(new { message = "File signature mismatch" });
        Directory.CreateDirectory(Root);
        var doc = new Document
        {
            ApplicationId = id,
            OriginalName = Path.GetFileName(file.FileName)[
                ..Math.Min(Path.GetFileName(file.FileName).Length, 150)
            ],
            StorageName = Guid.NewGuid().ToString("N") + ext,
            Size = bytes.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
        };
        await System.IO.File.WriteAllBytesAsync(Path.Combine(Root, doc.StorageName), bytes);
        db.Documents.Add(doc);
        db.ProcessingJobs.Add(new ProcessingJob { ApplicationId = id, DocumentId = doc.Id });
        access.Audit("document.uploaded", doc.Id);
        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            System.IO.File.Delete(Path.Combine(Root, doc.StorageName));
            throw;
        }
        return Accepted(new { doc.Id });
    }

    [HttpGet("{id:guid}/documents/{docId:guid}/download")]
    public async Task<IActionResult> Download(Guid id, Guid docId)
    {
        if (await access.Application(id) == null)
            return NotFound();
        var doc = await db.Documents.SingleOrDefaultAsync(x =>
            x.Id == docId && x.ApplicationId == id
        );
        if (doc == null)
            return NotFound();
        access.Audit("document.read", docId);
        await db.SaveChangesAsync();
        return PhysicalFile(
            Path.GetFullPath(Path.Combine(Root, doc.StorageName)),
            doc.StorageName.EndsWith(".pdf")
                ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            doc.OriginalName
        );
    }

    [HttpDelete("{id:guid}/documents/{docId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id, Guid docId)
    {
        var a = await access.Application(id);
        if (a == null)
            return NotFound();
        var d = await db.Documents.SingleOrDefaultAsync(x =>
            x.Id == docId && x.ApplicationId == id
        );
        if (d == null)
            return NotFound();
        if (await db.ProcessingJobs.AnyAsync(x => x.ApplicationId == id && x.Status == "Running"))
            return Conflict(new { message = "Wait for processing before deletion" });
        db.ProcessingJobs.RemoveRange(
            await db.ProcessingJobs.Where(x => x.DocumentId == docId).ToListAsync()
        );
        db.Documents.Remove(d);
        db.Reviews.RemoveRange(await db.Reviews.Where(x => x.ApplicationId == id).ToListAsync());
        db.Analyses.RemoveRange(await db.Analyses.Where(x => x.ApplicationId == id).ToListAsync());
        await access.Invalidate(a);
        access.Audit("document.deleted", docId);
        await db.SaveChangesAsync();
        System.IO.File.Delete(Path.Combine(Root, d.StorageName));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var a = await access.Application(id);
        if (a == null)
            return NotFound();
        if (await db.ProcessingJobs.AnyAsync(x => x.ApplicationId == id && x.Status == "Running"))
            return Conflict(new { message = "Wait for processing before deletion" });
        var files = await db
            .Documents.Where(x => x.ApplicationId == id)
            .Select(x => x.StorageName)
            .ToListAsync();
        db.Applications.Remove(a);
        access.Audit("application.deleted", id);
        await db.SaveChangesAsync();
        foreach (var f in files)
            System.IO.File.Delete(Path.Combine(Root, f));
        return NoContent();
    }

    [HttpPut("{id:guid}/facts/{factId:guid}"), Authorize(Roles = "HRAdmin,Recruiter")]
    public async Task<IActionResult> Correct(Guid id, Guid factId, FactDto dto)
    {
        var a = await access.Application(id);
        if (a == null)
            return NotFound();
        if (a.EvidenceVersion != dto.EvidenceVersion)
            return Conflict(new { message = "Evidence changed; reload before editing" });
        var f = await db.Facts.SingleOrDefaultAsync(x => x.Id == factId && x.ApplicationId == id);
        if (f == null)
            return NotFound();
        if (
            !new[] { "Documented", "Unverified", "Ambiguous", "Rejected" }.Contains(
                dto.Verification
            ) || dto.Proficiency is < 1 or > 4
        )
            return BadRequest();
        if (dto.SkillId != null && !await db.Skills.AnyAsync(x => x.Id == dto.SkillId))
            return BadRequest();
        var before = JsonSerializer.Serialize(f);
        f.SkillId = dto.SkillId;
        f.Value = dto.Value;
        f.Verification = dto.Verification;
        f.Proficiency = dto.Proficiency;
        f.Mapping = "Human";
        await access.Invalidate(a);
        db.Reviews.Add(
            new Review
            {
                ApplicationId = id,
                ActorId = access.Actor,
                Action = "fact.corrected",
                Note = dto.Note + "\nBefore: " + before + "\nAfter: " + JsonSerializer.Serialize(f),
            }
        );
        access.Audit("fact.corrected", factId);
        await db.SaveChangesAsync();
        return Ok(new { a.EvidenceVersion });
    }

    [HttpPost("{id:guid}/facts"), Authorize(Roles = "HRAdmin,Recruiter")]
    public async Task<IActionResult> AddFact(Guid id, AddFactDto dto)
    {
        var a = await access.Application(id);
        if (a == null)
            return NotFound();
        if (a.EvidenceVersion != dto.EvidenceVersion)
            return Conflict(new { message = "Evidence changed; reload" });
        var d = await db.Documents.SingleOrDefaultAsync(x =>
            x.Id == dto.DocumentId && x.ApplicationId == id
        );
        if (d == null)
            return NotFound();
        var section = JsonSerializer
            .Deserialize<JsonElement[]>(d.Sections)!
            .FirstOrDefault(x => x.GetProperty("id").GetString() == dto.SectionId);
        if (
            section.ValueKind == JsonValueKind.Undefined
            || !section
                .GetProperty("text")
                .GetString()!
                .Contains(dto.Quote, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(dto.Quote)
        )
            return BadRequest(new { message = "Quote must appear exactly in the source section" });
        if (!new[] { "skill", "education", "experience", "technology" }.Contains(dto.Kind))
            return BadRequest();
        if (dto.SkillId != null && !await db.Skills.AnyAsync(x => x.Id == dto.SkillId))
            return BadRequest();
        var f = new Fact
        {
            ApplicationId = id,
            DocumentId = dto.DocumentId,
            Kind = dto.Kind,
            Value = dto.Value,
            SkillId = dto.SkillId,
            SectionId = dto.SectionId,
            Quote = dto.Quote,
            Mapping = "Human",
        };
        db.Facts.Add(f);
        await access.Invalidate(a);
        db.Reviews.Add(
            new Review
            {
                ApplicationId = id,
                ActorId = access.Actor,
                Action = "fact.added",
                Note = "Human transcribed source evidence",
            }
        );
        access.Audit("fact.added", f.Id);
        await db.SaveChangesAsync();
        return Ok(f);
    }

    [HttpPost("{id:guid}/analysis"), Authorize(Roles = "HRAdmin,Recruiter")]
    public async Task<IActionResult> Analyze(Guid id)
    {
        var a = await access.Application(id);
        if (a == null)
            return NotFound();
        if (
            await db.ProcessingJobs.AnyAsync(x =>
                x.ApplicationId == id && (x.Status == "Queued" || x.Status == "Running")
            )
        )
            return Conflict(new { message = "Processing is already pending" });
        db.ProcessingJobs.Add(new ProcessingJob { ApplicationId = id, Kind = "Analyze" });
        access.Audit("analysis.requested", id);
        await db.SaveChangesAsync();
        return Accepted();
    }

    [HttpPost("{id:guid}/processing/{jobId:guid}/retry"), Authorize(Roles = "HRAdmin,Recruiter")]
    public async Task<IActionResult> Retry(Guid id, Guid jobId)
    {
        if (await access.Application(id) == null)
            return NotFound();
        var j = await db.ProcessingJobs.SingleOrDefaultAsync(x =>
            x.Id == jobId && x.ApplicationId == id && x.Status == "Failed"
        );
        if (j == null)
            return NotFound();
        j.Status = "Queued";
        j.Attempts = 0;
        j.Error = null;
        j.AvailableAt = DateTime.UtcNow;
        access.Audit("processing.retried", jobId);
        await db.SaveChangesAsync();
        return Accepted();
    }

    [HttpPost("{id:guid}/reviews"), Authorize(Roles = "HRAdmin,Recruiter")]
    public async Task<IActionResult> Review(Guid id, ReviewDto dto)
    {
        if (await access.Application(id) == null)
            return NotFound();
        db.Reviews.Add(
            new Review
            {
                ApplicationId = id,
                ActorId = access.Actor,
                Action = "review.recorded",
                Note = dto.Note,
            }
        );
        access.Audit("review.recorded", id);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{id:guid}/interviews"), Authorize(Roles = "HRAdmin,Recruiter")]
    public async Task<IActionResult> Schedule(Guid id, InterviewDto dto)
    {
        if (await access.Application(id) == null)
            return NotFound();
        if (
            dto.ScheduledAt.Kind == DateTimeKind.Unspecified
            || dto.ScheduledAt.ToUniversalTime() < DateTime.UtcNow
        )
            return BadRequest(new { message = "Future UTC date required" });
        var i = new Interview
        {
            ApplicationId = id,
            ActorId = access.Actor,
            ScheduledAt = dto.ScheduledAt.ToUniversalTime(),
            Location = dto.Location,
            Notes = dto.Notes,
        };
        db.Interviews.Add(i);
        access.Audit("interview.scheduled", id);
        await db.SaveChangesAsync();
        return Ok(i);
    }

    [HttpPut("{id:guid}/interviews/{interviewId:guid}"), Authorize(Roles = "HRAdmin,Recruiter")]
    public async Task<IActionResult> InterviewEdit(Guid id, Guid interviewId, InterviewDto dto)
    {
        if (await access.Application(id) == null)
            return NotFound();
        var i = await db.Interviews.SingleOrDefaultAsync(x =>
            x.Id == interviewId && x.ApplicationId == id
        );
        if (i == null)
            return NotFound();
        if (dto.ScheduledAt.Kind == DateTimeKind.Unspecified)
            return BadRequest();
        i.Notes = dto.Notes;
        i.Location = dto.Location;
        i.ScheduledAt = dto.ScheduledAt.ToUniversalTime();
        access.Audit("interview.updated", id);
        await db.SaveChangesAsync();
        return Ok(i);
    }

    [HttpPut("{id:guid}/status"), Authorize(Roles = "HRAdmin,Recruiter")]
    public async Task<IActionResult> Status(Guid id, StatusDto dto)
    {
        var a = await access.Application(id);
        if (a == null)
            return NotFound();
        var transitions = new Dictionary<string, string[]>
        {
            { "Submitted", ["UnderReview", "Withdrawn"] },
            { "UnderReview", ["Interview", "Rejected", "Withdrawn"] },
            { "Interview", ["Offered", "Rejected", "UnderReview", "Withdrawn"] },
            { "Offered", ["Hired", "Rejected", "Withdrawn"] },
        };
        if (!transitions.TryGetValue(a.Status, out var next) || !next.Contains(dto.Status))
            return BadRequest(new { message = "Invalid state transition" });
        a.Status = dto.Status;
        if (dto.Status == "Hired") {
            var vacancy = await db.Jobs.SingleAsync(x => x.Id == a.JobId);
            vacancy.Open = false;
            access.Audit("job.filled.manual", vacancy.Id);
        }
        db.Decisions.Add(
            new Decision
            {
                ApplicationId = id,
                ActorId = access.Actor,
                Outcome = dto.Status,
                Rationale = dto.Rationale,
            }
        );
        access.Audit("application.status.manual", id);
        await db.SaveChangesAsync();
        return Ok(a);
    }
}

[ApiController, Route("api/profile"), Authorize(Roles = "Employee,HRAdmin")]
public class ProfileController(WorkforceDb db, Access access) : ControllerBase
{
    [HttpGet]
    public async Task<object> Mine() =>
        await db.EmployeeSkills.Where(x => x.UserId == access.Actor).ToListAsync();

    [HttpPut("skills")]
    public async Task<IActionResult> Save(EmployeeSkillDto dto)
    {
        if (!await db.Skills.AnyAsync(x => x.Id == dto.SkillId))
            return BadRequest();
        var e = await db.EmployeeSkills.SingleOrDefaultAsync(x =>
            x.UserId == access.Actor && x.SkillId == dto.SkillId
        );
        if (e == null)
        {
            e = new EmployeeSkill { UserId = access.Actor, SkillId = dto.SkillId };
            db.EmployeeSkills.Add(e);
        }
        e.Proficiency = dto.Proficiency;
        e.Evidence = dto.Evidence;
        e.DevelopmentNeed = dto.DevelopmentNeed;
        e.Verification = "Unverified";
        access.Audit("employee.skill.updated", e.Id);
        await db.SaveChangesAsync();
        return Ok(e);
    }

    [HttpDelete("skills/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var e = await db.EmployeeSkills.SingleOrDefaultAsync(x =>
            x.Id == id && x.UserId == access.Actor
        );
        if (e == null)
            return NotFound();
        db.EmployeeSkills.Remove(e);
        access.Audit("employee.skill.deleted", id);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("employees/{id:guid}"), Authorize(Roles = "HRAdmin")]
    public async Task<object> Inventory(Guid id) =>
        await db.EmployeeSkills.Where(x => x.UserId == id).ToListAsync();

    [HttpPut("employees/skills/{id:guid}/verification"), Authorize(Roles = "HRAdmin")]
    public async Task<IActionResult> Verify(Guid id, VerifySkillDto dto)
    {
        if (!new[] { "Documented", "Unverified", "Ambiguous" }.Contains(dto.Verification))
            return BadRequest();
        var e = await db.EmployeeSkills.FindAsync(id);
        if (e == null)
            return NotFound();
        e.Verification = dto.Verification;
        access.Audit("employee.skill.reviewed", id);
        await db.SaveChangesAsync();
        return Ok(e);
    }
}
