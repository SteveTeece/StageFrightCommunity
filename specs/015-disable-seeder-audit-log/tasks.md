# Tasks: Skip Audit Trail Logging During Debug Data Seeding

Classified **simple** — see [spec.md § Approach](./spec.md#approach) for the plan these tasks implement.

- [x] **T001** [P] Create `AuditTrailSuppressionScope` ambient scope (`AsyncLocal<bool>`-backed, `Begin()` returns `IDisposable`, exposes `IsSuppressed`) + `src/StageFright.Core/Modules/AuditTrail/AuditTrailSuppressionScope.cs`
- [x] **T002** Update `AuditTrailService.LogAsync` to return immediately without calling the repository when `AuditTrailSuppressionScope.IsSuppressed` is true (unchanged otherwise) + `src/StageFright.Core/Modules/AuditTrail/AuditTrailService.cs`
- [x] **T003** [P] Wrap the body of `DebugDataSeeder.SeedAsync` in `using var _ = AuditTrailSuppressionScope.Begin();` so the whole seeding run is covered and lifts even if a step throws + `src/StageFright.App/Seeding/DebugDataSeeder.cs`
- [x] **T004** [P] Add unit tests for `AuditTrailSuppressionScope`: begin/dispose toggles `IsSuppressed`, restores to false even when an exception is thrown inside the scope, and flows across an `await` + `tests/StageFright.Core.Tests/Modules/AuditTrail/AuditTrailSuppressionScopeTests.cs` (new)
- [x] **T005** Add unit tests for `AuditTrailService.LogAsync` suppression: no repository call while suppressed; unchanged (still logs) when not suppressed + `tests/StageFright.Core.Tests/Modules/AuditTrail/AuditTrailServiceTests.cs`
- [x] **T006** Run `dotnet build` and the full `dotnet test` suite; fix any regressions and report results

## Dependencies

- T002 depends on T001 (needs the scope to check).
- T003 depends on T001 (needs the scope to wrap the seeder call).
- T004 depends on T001 (tests the scope directly).
- T005 depends on T002 (tests the suppression behavior in `LogAsync`).
- T006 depends on T001–T005.

`[P]` tasks (T001, T003, T004) can proceed in parallel once their own dependencies are satisfied.
