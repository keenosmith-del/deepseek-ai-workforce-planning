using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Workforce.Domain;
using Workforce.Infrastructure;

namespace Workforce.Application;

public class Access(WorkforceDb db, IHttpContextAccessor http)
{
    public Guid Actor =>
        Guid.Parse(http.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public bool Staff =>
        http.HttpContext!.User.IsInRole("HRAdmin") || http.HttpContext.User.IsInRole("Recruiter");

    public async Task<ApplicationRecord?> Application(Guid id)
    {
        var a = await db.Applications.FindAsync(id);
        return a != null && (Staff || a.CandidateId == Actor) ? a : null;
    }

    public void Audit(string action, object entity) =>
        db.AuditEvents.Add(
            new AuditEvent
            {
                ActorId = Actor,
                Action = action,
                Entity = entity.ToString()!,
            }
        );

    public async Task Invalidate(ApplicationRecord a)
    {
        a.EvidenceVersion++;
        foreach (
            var analysis in await db
                .Analyses.Where(x => x.ApplicationId == a.Id && !x.Stale)
                .ToListAsync()
        )
            analysis.Stale = true;
    }
}

public class IntelligenceClient(HttpClient client)
{
    public async Task<JsonElement> Post(string path, object body, CancellationToken ct = default)
    {
        using var r = await client.PostAsJsonAsync(path, body, ct);
        return await Read(r, ct);
    }

    public async Task<JsonElement> Parse(byte[] data, CancellationToken ct)
    {
        using var content = new ByteArrayContent(data);
        using var r = await client.PostAsync("/parse", content, ct);
        return await Read(r, ct);
    }

    public async Task<JsonElement> Models()
    {
        using var r = await client.GetAsync("/models");
        return await Read(r, default);
    }

    static async Task<JsonElement> Read(HttpResponseMessage r, CancellationToken ct)
    {
        if (!r.IsSuccessStatusCode)
        {
            var text = await r.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Intelligence service {(int)r.StatusCode}: {text[..Math.Min(text.Length, 400)]}"
            );
        }
        return (await r.Content.ReadFromJsonAsync<JsonElement>(ct));
    }
}

public class SemanticSkills(WorkforceDb db, IntelligenceClient ai)
{
    static string Vector(JsonElement v) =>
        "["
        + string.Join(
            ",",
            v.EnumerateArray()
                .Select(x =>
                    x.GetDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                )
        )
        + "]";

    public async Task<int> Refresh()
    {
        var skills = await db.Skills.OrderBy(x => x.Name).ToListAsync();
        if (skills.Count == 0)
            return 0;
        var result = await ai.Post(
            "/embed",
            new { texts = skills.Select(x => x.Name + ": " + x.Description).ToArray() }
        );
        var model = result.GetProperty("model").GetString()!;
        var vectors = result.GetProperty("vectors").EnumerateArray().ToArray();
        await db.Database.OpenConnectionAsync();
        try
        {
            for (int i = 0; i < skills.Count; i++)
            {
                await using var cmd = new NpgsqlCommand(
                    "INSERT INTO skill_embeddings (skill_id,model,embedding) VALUES (@id,@model,CAST(@v AS vector)) ON CONFLICT (skill_id) DO UPDATE SET model=@model,embedding=CAST(@v AS vector)",
                    (NpgsqlConnection)db.Database.GetDbConnection()
                );
                cmd.Parameters.AddWithValue("id", skills[i].Id);
                cmd.Parameters.AddWithValue("model", model);
                cmd.Parameters.AddWithValue("v", Vector(vectors[i]));
                await cmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
        return skills.Count;
    }

    public async Task<object[]> Search(string query)
    {
        var result = await ai.Post("/embed", new { texts = new[] { query } });
        var vector = Vector(result.GetProperty("vectors")[0]);
        var model = result.GetProperty("model").GetString()!;
        var results = new List<object>();
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT s.\"Id\",s.\"Name\", 1-(e.embedding <=> CAST(@v AS vector)) AS similarity FROM skill_embeddings e JOIN \"Skills\" s ON s.\"Id\"=e.skill_id WHERE e.model=@model ORDER BY e.embedding <=> CAST(@v AS vector) LIMIT 5",
                (NpgsqlConnection)db.Database.GetDbConnection()
            );
            cmd.Parameters.AddWithValue("v", vector);
            cmd.Parameters.AddWithValue("model", model);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                results.Add(
                    new
                    {
                        skillId = reader.GetGuid(0),
                        name = reader.GetString(1),
                        similarity = reader.GetDouble(2),
                        verification = "Ambiguous",
                        requiresHumanConfirmation = true,
                    }
                );
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
        return results.ToArray();
    }
}
