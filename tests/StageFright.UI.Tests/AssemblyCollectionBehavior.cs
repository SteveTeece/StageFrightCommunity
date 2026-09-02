// Spec 029's CultureProvider.Switch mutates process-wide static CultureInfo state
// (CurrentCulture/CurrentUICulture/DefaultThreadCurrentCulture/DefaultThreadCurrentUICulture —
// see CultureProvider.razor.cs), by design: it mirrors exactly what MauiProgram.RunStartupSequence
// already does once at startup, so background work (report rendering, etc.) also picks up a
// switched language. Because virtually every test in this assembly renders through
// LocalizedTestContext and asserts against IStringLocalizer output resolved from that same
// ambient culture, a test that exercises Switch (CultureProviderTests, FirstRunLanguageScreenTests)
// races non-deterministically against whatever other test collections xUnit happens to run
// concurrently — reproduced across multiple full runs, each time corrupting a different,
// unrelated test's expected English/French text. Disabling cross-collection parallelism for this
// one assembly (xUnit's standard answer for "some tests share global mutable state") eliminates
// the race outright; per-class [Collection] tagging (see MoneyFormatterStateCollection in
// StageFright.Integration.Tests for that narrower pattern) isn't viable here since the "readers"
// are effectively the whole suite, not a handful of files.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
