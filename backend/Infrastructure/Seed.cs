using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Workforce.Application;
using Workforce.Domain;

namespace Workforce.Infrastructure;

public static class Seed
{
    public static async Task Run(WorkforceDb db, IConfiguration c)
    {
        if (await db.Users.AnyAsync())
            return;
        if (c["Seed:Enabled"] != "true")
            return;
        var password =
            c["Seed:Password"] ?? throw new InvalidOperationException("Seed password required");
        var engineering = new Department { Name = "Engineering" };
        var operations = new Department { Name = "Operations" };
        db.Departments.AddRange(engineering, operations);
        var skills = new[]
        {
            new Skill
            {
                Name = "C#",
                Aliases = "[\"csharp\",\"c sharp\"]",
                Description = "C# programming and .NET development",
            },
            new Skill
            {
                Name = "Python",
                Aliases = "[\"python3\"]",
                Description = "Python programming and data processing",
            },
            new Skill
            {
                Name = "PostgreSQL",
                Aliases = "[\"postgres\",\"psql\"]",
                Description = "PostgreSQL relational database administration and SQL",
            },
            new Skill
            {
                Name = "React",
                Aliases = "[\"reactjs\",\"react.js\"]",
                Description = "React user interface development",
            },
            new Skill
            {
                Name = "Docker",
                Aliases = "[\"containerization\"]",
                Description = "Docker containers and deployment",
            },
            new Skill
            {
                Name = "TypeScript",
                Aliases = "[\"typescript\"]",
                Description = "TypeScript typed JavaScript development",
            },
        };
        db.Skills.AddRange(skills);
        var users = new[]
        {
            new User
            {
                Email = "hr@demo.test",
                Name = "Avery Admin (fictional)",
                Role = "HRAdmin",
            },
            new User
            {
                Email = "recruiter@demo.test",
                Name = "Morgan Recruiter (fictional)",
                Role = "Recruiter",
            },
            new User
            {
                Email = "candidate@demo.test",
                Name = "Taylor Candidate (fictional)",
                Role = "Candidate",
            },
            new User
            {
                Email = "employee@demo.test",
                Name = "Jordan Employee (fictional)",
                Role = "Employee",
                DepartmentId = engineering.Id,
            },
        };
        foreach (var u in users)
            u.PasswordHash = Passwords.Hash(password);
        db.Users.AddRange(users);
        var job = new Job
        {
            DepartmentId = engineering.Id,
            Title = "Platform engineer",
            Description = "Build APIs and reliable data services using C#, PostgreSQL and Docker.",
            Requirements =
            [
                new Requirement { SkillId = skills[0].Id, Proficiency = 2 },
                new Requirement { SkillId = skills[2].Id, Proficiency = 2 },
                new Requirement { SkillId = skills[4].Id, Proficiency = 2 },
            ],
        };
        db.Jobs.Add(job);
        db.EmployeeSkills.Add(
            new EmployeeSkill
            {
                UserId = users[3].Id,
                SkillId = skills[1].Id,
                Proficiency = 2,
                Evidence = "Fictional internal Python automation workshop completion",
                Verification = "Documented",
                DevelopmentNeed = "Learn PostgreSQL query tuning",
            }
        );
        await db.SaveChangesAsync();
    }
}
