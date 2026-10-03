using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Workforce.Domain;
using Workforce.Infrastructure;

namespace Workforce.Application;

public class ProcessingWorker(
    IServiceScopeFactory scopes,
    IConfiguration config,
    ILogger<ProcessingWorker> log
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WorkforceDb>();
                await db.Database.OpenConnectionAsync(stoppingToken);
                await using var mutex = new NpgsqlCommand(
                    "SELECT pg_try_advisory_lock(398125)",
                    (NpgsqlConnection)db.Database.GetDbConnection()
                );
                if ((bool)(await mutex.ExecuteScalarAsync(stoppingToken))!)
                {
                    try
                    {
                        await Cycle(scope.ServiceProvider, db, stoppingToken);
                    }
                    finally
                    {
                        await using var unlock = new NpgsqlCommand(
                            "SELECT pg_advisory_unlock(398125)",
                            (NpgsqlConnection)db.Database.GetDbConnection()
                        );
                        await unlock.ExecuteScalarAsync(CancellationToken.None);
                    }
                }
                await db.Database.CloseConnectionAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Background processing cycle failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    async Task Cycle(IServiceProvider services, WorkforceDb db, CancellationToken ct)
    {
        // Running jobs recover only after the previous worker's PostgreSQL session lock is released.
        foreach (
            var interrupted in await db
                .ProcessingJobs.Where(x => x.Status == "Running")
                .ToListAsync(ct)
        )
        {
            interrupted.Status = "Queued";
            interrupted.Error = "Worker interrupted; retrying";
        }
        await db.SaveChangesAsync(ct);
        var expired = await db.Documents.Where(x => x.ExpiresAt < DateTime.UtcNow).ToListAsync(ct);
        foreach (var d in expired)
        {
            var a = await db.Applications.FindAsync([d.ApplicationId], ct);
            if (a != null)
                a.EvidenceVersion++;
            db.Analyses.RemoveRange(
                await db.Analyses.Where(x => x.ApplicationId == d.ApplicationId).ToListAsync(ct)
            );
            db.ProcessingJobs.RemoveRange(
                await db.ProcessingJobs.Where(x => x.DocumentId == d.Id).ToListAsync(ct)
            );
            db.Reviews.RemoveRange(await db.Reviews.Where(x => x.ApplicationId == d.ApplicationId).ToListAsync(ct));
            db.Documents.Remove(d);
            db.AuditEvents.Add(
                new AuditEvent { Action = "document.retention.expired", Entity = d.Id.ToString() }
            );
            System.IO.File.Delete(Path.Combine(config["Storage:Path"]!, d.StorageName));
        }
        await db.SaveChangesAsync(ct);
        var job = await db
            .ProcessingJobs.Where(x => x.Status == "Queued" && x.AvailableAt <= DateTime.UtcNow)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (job == null)
            return;
        job.Status = "Running";
        job.StartedAt = DateTime.UtcNow;
        job.Attempts++;
        await db.SaveChangesAsync(ct);
        try
        {
            var ai = services.GetRequiredService<IntelligenceClient>();
            var a = await db.Applications.SingleAsync(x => x.Id == job.ApplicationId, ct);
            var version = a.EvidenceVersion;
            if (job.Kind == "Extract")
            {
                var doc = await db.Documents.SingleAsync(x => x.Id == job.DocumentId, ct);
                var bytes = await System.IO.File.ReadAllBytesAsync(
                    Path.Combine(config["Storage:Path"]!, doc.StorageName),
                    ct
                );
                var parsed = await ai.Parse(bytes, ct);
                var sections = parsed.GetProperty("sections");
                var skills = await db.Skills.ToListAsync(ct);
                var result = await ai.Post(
                    "/extract",
                    new
                    {
                        sections,
                        skills = skills.Select(s => new
                        {
                            id = s.Id,
                            name = s.Name,
                            aliases = JsonSerializer.Deserialize<string[]>(s.Aliases),
                        }),
                    },
                    ct
                );
                // A single atomic save publishes the new evidence and makes previous versions stale.
                doc.Sections = sections.GetRawText();
                db.Facts.RemoveRange(
                    await db.Facts.Where(x => x.DocumentId == doc.Id).ToListAsync(ct)
                );
                foreach (var f in result.GetProperty("facts").EnumerateArray())
                {
                    db.Facts.Add(
                        new Fact
                        {
                            ApplicationId = a.Id,
                            DocumentId = doc.Id,
                            Kind = f.GetProperty("kind").GetString()!,
                            Value = f.GetProperty("value").GetString()!,
                            SectionId = f.GetProperty("section_id").GetString()!,
                            Quote = f.GetProperty("quote").GetString()!,
                            SkillId =
                                f.GetProperty("skill_id").ValueKind == JsonValueKind.Null
                                    ? null
                                    : Guid.Parse(f.GetProperty("skill_id").GetString()!),
                            Mapping = f.GetProperty("mapping").GetString()!,
                        }
                    );
                }
                a.EvidenceVersion++;
                foreach (
                    var old in await db.Analyses.Where(x => x.ApplicationId == a.Id).ToListAsync(ct)
                )
                    old.Stale = true;
                db.ProcessingJobs.Add(new ProcessingJob { ApplicationId = a.Id, Kind = "Analyze" });
            }
            else
            {
                var vacancy = await db
                    .Jobs.Include(x => x.Requirements)
                    .SingleAsync(x => x.Id == a.JobId, ct);
                var skills = await db.Skills.ToListAsync(ct);
                var facts = await db.Facts.Where(x => x.ApplicationId == a.Id).ToListAsync(ct);
                var result = await ai.Post(
                    "/analyze",
                    new
                    {
                        requirements = vacancy.Requirements.Select(r => new
                        {
                            skill_id = r.SkillId,
                            name = skills.Single(s => s.Id == r.SkillId).Name,
                            proficiency = r.Proficiency,
                        }),
                        evidence = facts.Select(f => new
                        {
                            id = f.Id,
                            skill_id = f.SkillId,
                            value = f.Value,
                            quote = f.Quote,
                            section_id = f.DocumentId + ":" + f.SectionId,
                            verification = f.Verification,
                            proficiency = f.Proficiency,
                        }),
                    },
                    ct
                );
                await db.Entry(a).ReloadAsync(ct);
                var analysis = new Analysis
                {
                    ApplicationId = a.Id,
                    EvidenceVersion = version,
                    Stale = a.EvidenceVersion != version,
                    Result = result.GetProperty("result").GetRawText(),
                    Metadata = result.GetProperty("metadata").GetRawText(),
                };
                db.Analyses.Add(analysis);
            }
            job.Status = "Completed";
            job.Error = null;
            db.AuditEvents.Add(
                new AuditEvent
                {
                    Action = "processing." + job.Kind.ToLowerInvariant() + ".completed",
                    Entity = job.Id.ToString(),
                }
            );
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogWarning("Processing {JobId} failed: {ErrorType}", job.Id, ex.GetType().Name);
            db.ChangeTracker.Clear();
            job = await db.ProcessingJobs.SingleAsync(x => x.Id == job.Id, ct);
            job.Status = job.Attempts >= 3 ? "Failed" : "Queued";
            job.AvailableAt = DateTime.UtcNow.AddSeconds(job.Attempts * 10);
            job.Error =
                ex is InvalidOperationException
                    ? ex.Message
                    : "Processing failed: " + ex.GetType().Name;
            db.AuditEvents.Add(
                new AuditEvent { Action = "processing.failed", Entity = job.Id.ToString() }
            );
            await db.SaveChangesAsync(ct);
        }
    }
}
