namespace StageFright.Localization.Tests.Scanning;

/// <summary>
/// One resource-key usage found in source: the key text, the file it was found in, and the
/// 1-based line number. Produced by <see cref="LocalizerKeyUsageScanner"/> and consumed by the
/// completeness / residual-literal guard tests (Phase 3+).
/// </summary>
public sealed record LocalizerKeyUsage(string Key, string FilePath, int LineNumber);
