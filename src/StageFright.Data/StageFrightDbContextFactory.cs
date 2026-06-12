using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StageFright.Data;

/// <summary>Design-time factory used by EF Core tooling (dotnet ef migrations).</summary>
public class StageFrightDbContextFactory : IDesignTimeDbContextFactory<StageFrightDbContext>
{
    public StageFrightDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=design_time.db")
            .Options;
        return new StageFrightDbContext(options);
    }
}
