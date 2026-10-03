using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Workforce.Application;
using Workforce.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var key =
    builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt__Key required");
if (key.Length < 32)
    throw new InvalidOperationException("JWT key must be at least 32 characters");
if (string.IsNullOrWhiteSpace(builder.Configuration["Internal:Key"]))
    throw new InvalidOperationException("Internal__Key required");
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);
builder.Services.AddDbContext<WorkforceDb>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Database"))
);
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Access>();
builder.Services.AddScoped<SemanticSkills>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddHttpClient<IntelligenceClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Intelligence:Url"]!);
    c.Timeout = TimeSpan.FromMinutes(4);
    c.DefaultRequestHeaders.Add("X-Internal-Key", builder.Configuration["Internal:Key"]);
});
builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = "workforce",
            ValidAudience = "workforce-ui",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ClockSkew = TimeSpan.FromSeconds(10),
        };
        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var id = context
                    .Principal!.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                    ?.Value;
                var db = context.HttpContext.RequestServices.GetRequiredService<WorkforceDb>();
                if (
                    !Guid.TryParse(id, out var uid)
                    || !await db.Users.AnyAsync(x => x.Id == uid && x.Active)
                )
                    context.Fail("Account unavailable");
            },
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy(
        "auth",
        context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 15,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }
            )
    );
});
builder.Services.AddHostedService<ProcessingWorker>();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WorkforceDb>();
    await db.Database.MigrateAsync();
    if (builder.Configuration["Bootstrap:Enabled"] == "true")
    {
        if (await db.Users.AnyAsync(x => x.Role == "HRAdmin"))
            throw new InvalidOperationException("An HR administrator already exists");
        var email =
            builder.Configuration["Bootstrap:Email"]
            ?? throw new InvalidOperationException("Bootstrap email required");
        var password =
            builder.Configuration["Bootstrap:Password"]
            ?? throw new InvalidOperationException("Bootstrap password required");
        if (
            password.Length < 12
            || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)
        )
            throw new InvalidOperationException("Valid email and 12-character password required");
        db.Users.Add(
            new Workforce.Domain.User
            {
                Email = email.ToLowerInvariant(),
                Name = "HR Administrator",
                Role = "HRAdmin",
                PasswordHash = Passwords.Hash(password),
            }
        );
        await db.SaveChangesAsync();
        return;
    }
    await Seed.Run(db, builder.Configuration);
}
app.Use(
    async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Cache-Control"] = "no-store";
        try
        {
            await next();
        }
        catch (DbUpdateException)
        {
            context.Response.StatusCode = 409;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    message = "Record conflict or invalid reference",
                    traceId = context.TraceIdentifier,
                }
            );
        }
        catch (Exception ex)
        {
            app.Logger.LogError(
                "Request {TraceId} failed: {ErrorType}",
                context.TraceIdentifier,
                ex.GetType().Name
            );
            context.Response.StatusCode = ex is HttpRequestException or InvalidOperationException
                ? 503
                : 500;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    message = context.Response.StatusCode == 503
                        ? "Dependent service unavailable. Check processing status and local models."
                        : "Request failed",
                    traceId = context.TraceIdentifier,
                }
            );
        }
    }
);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet(
    "/health",
    async (WorkforceDb db) =>
        await db.Database.CanConnectAsync()
            ? Results.Ok(new { status = "ok" })
            : Results.StatusCode(503)
);
app.Run();
