<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan:
[specs/001-initial-mvp/plan.md](specs/001-initial-mvp/plan.md)

Active feature: `001-initial-mvp` — StageFright Community MVP desktop app.
Stack: C# 14 / .NET 10.0, .NET MAUI Blazor Hybrid (single BlazorWebView, Blazor-controlled navigation), EF Core + SQLite (centralized DAL), Radzen.Blazor + Bootstrap 5.3, Serilog + OpenTelemetry, protobuf-net (backup), QuestPDF (PDF reports), CsvHelper (CSV export), xUnit + bUnit + NSubstitute (tests).
Design artifacts: [research.md](specs/001-initial-mvp/research.md), [data-model.md](specs/001-initial-mvp/data-model.md), [contracts/](specs/001-initial-mvp/contracts/), [quickstart.md](specs/001-initial-mvp/quickstart.md).
Governance: `.specify/memory/constitution.md` (v2.3.0) — one class per file, soft-delete pattern (financial records exempt and immutable), custom exceptions at boundaries, no custom JavaScript, exhaustive code-path test coverage.
<!-- SPECKIT END -->
