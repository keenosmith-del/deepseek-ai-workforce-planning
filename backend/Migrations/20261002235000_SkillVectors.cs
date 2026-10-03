using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Workforce.Infrastructure;

namespace Workforce.Migrations;

[DbContext(typeof(WorkforceDb))]
[Migration("20261002235000_SkillVectors")]
public class SkillVectors : Migration
{
    protected override void Up(MigrationBuilder m) =>
        m.Sql(
            """
            CREATE EXTENSION IF NOT EXISTS vector;
            CREATE TABLE skill_embeddings (skill_id uuid PRIMARY KEY REFERENCES "Skills"("Id") ON DELETE CASCADE, model text NOT NULL, embedding vector(768) NOT NULL);
            CREATE INDEX skill_embeddings_cosine ON skill_embeddings USING hnsw (embedding vector_cosine_ops);
            """
        );

    protected override void Down(MigrationBuilder m) =>
        m.Sql("DROP TABLE IF EXISTS skill_embeddings;");
}
