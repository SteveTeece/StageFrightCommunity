namespace StageFright.Integration.Tests;

/// <summary>
/// Serialises every integration test that reads or mutates the process-wide
/// <see cref="StageFright.Core.Localization.MoneyFormatter"/> currency (spec 028). Because
/// <c>MoneyFormatter.Configure</c> sets static state, a test that configures a non-AUD
/// currency must not run in parallel with one asserting the default AUD output. Classes in
/// this collection run one at a time and never alongside another collection.
/// </summary>
[CollectionDefinition("MoneyFormatterState", DisableParallelization = true)]
public sealed class MoneyFormatterStateCollection;
