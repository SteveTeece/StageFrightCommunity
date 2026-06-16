namespace StageFright.Core.Contracts;

/// <summary>
/// Populates the database with realistic test data after the first-run setup wizard completes.
/// Only registered and called in DEBUG builds. No-op if data already exists.
/// </summary>
public interface IDebugDataSeeder
{
    Task SeedAsync(IProgress<string>? progress = null, CancellationToken ct = default);
}
