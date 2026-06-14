namespace StageFright.TestPlugin;

/// <summary>
/// Minimal entity owned by the TestPlugin. Used to prove the plugin data-access pattern:
/// a plugin DbContext manages its own table in the shared SQLite database.
/// </summary>
public class TestPluginEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
