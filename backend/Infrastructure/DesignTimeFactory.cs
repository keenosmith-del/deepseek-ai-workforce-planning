using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Workforce.Infrastructure;

public class DesignTimeFactory : IDesignTimeDbContextFactory<WorkforceDb>
{
    public WorkforceDb CreateDbContext(string[] args) =>
        new(
            new DbContextOptionsBuilder<WorkforceDb>()
                .UseNpgsql(
                    "Host=localhost;Database=workforce;Username=workforce;Password=design-time-only"
                )
                .Options
        );
}
