using Microsoft.EntityFrameworkCore;

namespace StageFright.TestPlugin;

/// <summary>
/// Minimal DbContext for the TestPlugin. Owns only the TestPlugin_Items table.
/// Table name is prefixed "TestPlugin_" per the plugin data-access convention.
/// </summary>
public class TestPluginDbContext : DbContext
{
    public TestPluginDbContext(DbContextOptions<TestPluginDbContext> options) : base(options) { }

    public DbSet<TestPluginEntity> Items => Set<TestPluginEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestPluginEntity>(e =>
        {
            e.ToTable("TestPlugin_Items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
        });
    }
}
