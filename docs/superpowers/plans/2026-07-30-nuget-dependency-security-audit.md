# NuGet Dependency Security Audit & Remediation Plan

> **For future implementers:** This is an audit + staged remediation plan, not a granular
> task/step implementation plan. Each stage below is independently approvable/executable —
> confirm scope with the user before starting a stage, since Stages 2-4 touch many `.csproj`
> files and Stage 3 includes major-version bumps with real compatibility risk.

**Goal:** Resolve the one real security vulnerability found in the solution's NuGet dependency
graph, and provide a risk-tiered plan for the broader outdated-package backlog surfaced by the
same audit.

**Tech Stack:** .NET 10 solution (`StageFrightCommunity.slnx`), 12 projects (6 `src/`, 6
`tests/`), no central package management (`Directory.Packages.props` does not exist — every
`.csproj` pins its own versions inline).

**Audit method:** `dotnet list StageFrightCommunity.slnx package --vulnerable/--deprecated/--outdated --include-transitive`
against every project, cross-referenced with direct inspection of all 12 `.csproj` files. Run
2026-07-30.

---

## Audit Findings

### 1. Actual security vulnerability (1 found, solution-wide)

| Package | Severity | Advisory | Current | Referencing project | Direct or transitive? |
|---|---|---|---|---|---|
| **AngleSharp** | Moderate | [GHSA-pgww-w46g-26qg](https://github.com/advisories/GHSA-pgww-w46g-26qg) | 1.4.0 | `tests/StageFright.UI.Tests/StageFright.UI.Tests.csproj` | Transitive only, via `bunit` 2.7.2 |

The only vulnerable package across all 12 projects — every other project reported clean. Isolated
to one **test-only** project; no production code path (App/Core/Data/Reports/UI/Plugins.Contracts)
is affected, so there's no shipped-product exposure. It doesn't reach `StageFright.Integration.Tests`
either, even though that project references UI/Reports/TestPlugin, because it only enters the
dependency graph via `bunit`.

### 2. Deprecated package (not a vulnerability, informational)

`xunit` 2.9.3 is flagged "Legacy" (suggested alternative: `xunit.v3`) as a **direct** reference in
all 5 test projects (`Core.Tests`, `Data.Tests`, `Integration.Tests`, `Reports.Tests`,
`UI.Tests`). No CVE attached — a maintainer packaging note, not a security issue. Migrating to
`xunit.v3` is a larger API-surface change and should not be bundled with the security fix.

### 3. General outdated-but-not-vulnerable drift

- Nearly every `Microsoft.Extensions.*` / `Microsoft.AspNetCore.Components.*` /
  `Microsoft.EntityFrameworkCore.*` package solution-wide is pinned at `10.0.9` while `10.0.10` is
  available — patch-level, low risk, touches almost every `.csproj`.
- `QuestPDF` 2026.6.0 → 2026.7.2 (Reports + everything that references/pulls it transitively).
- `SQLitePCLRaw.bundle_e_sqlite3`/`core` 3.0.3 → 3.0.5 (Data, Data.Tests, TestPlugin, App).
- `Microsoft.NET.Test.Sdk` 18.6.0 → 18.8.1 (all 5 test projects, patch-level).
- `NSubstitute` 5.3.0 → **6.0.0** (major version, all 5 test projects, direct reference).
- `Radzen.Blazor` 10.4.9 → **11.1.9** (major version, direct in `StageFright.UI`).
- `Microsoft.Maui.Controls` / `Microsoft.AspNetCore.Components.WebView.Maui` 10.0.71 → 10.0.90
  (App project) — also drags several `Microsoft.WindowsAppSDK.*` transitive packages from the
  `1.8.x` line to `2.x.x`, a **major** jump with real compatibility risk since it's a MAUI
  workload-level dependency.
- `Humanizer.Core` 2.14.1 → 3.0.10 and `Microsoft.CodeAnalysis.*` 3.11.0/5.0.0 → 5.6.0 — all
  transitive via `Microsoft.EntityFrameworkCore.Design` (a dev-time-only tool, `PrivateAssets=all`,
  never shipped) in `StageFright.Data`.

None of the items in this section are flagged as vulnerable or deprecated by NuGet — they're
routine staleness, included because "updates" were in scope of the original ask, not just CVEs.

---

## Remediation Plan (staged by risk)

### Stage 1 — Fix the actual vulnerability (do first, isolated, low risk)

- [ ] Bump `bunit` in `tests/StageFright.UI.Tests/StageFright.UI.Tests.csproj` from `2.7.2` →
      `2.8.6` (latest), which should pull a non-vulnerable `AngleSharp` transitively.
- [ ] If the resolved `AngleSharp` version is still `< 1.6.0` after that bump, add an explicit
      transitive override (`<PackageReference Include="AngleSharp" Version="1.6.0" />`) in that
      same project only.
- [ ] Verify: `dotnet list StageFrightCommunity.slnx package --vulnerable --include-transitive`
      reports zero results.
- [ ] Verify: `dotnet test tests/StageFright.UI.Tests/` green (409 tests currently) — confirms no
      bUnit API breakage from the minor version bump.

### Stage 2 — Safe patch-level bumps (mechanical, low risk, broad)

- [ ] Bump every `10.0.9` → `10.0.10` Microsoft.* direct reference across all 12 `.csproj` files.
- [ ] Bump `QuestPDF` 2026.6.0 → 2026.7.2 in `StageFright.Reports` (re-check QuestPDF's
      Community/Commercial license terms haven't changed for the new version).
- [ ] Bump `SQLitePCLRaw.bundle_e_sqlite3`/`core` → 3.0.5 in Data/Data.Tests/TestPlugin.
- [ ] Bump `Microsoft.NET.Test.Sdk` → 18.8.1 across all 5 test projects.
- [ ] Verify: full `dotnet build` + `dotnet test` from repo root (all 5 test projects, ~1352
      tests), per this repo's CLAUDE.md mandatory build/test verification rule.

### Stage 3 — Higher-risk major-version bumps (evaluate and test individually — none are CVE-flagged, not urgent)

- [ ] `NSubstitute` 5.3.0 → 6.0.0 across all 5 test projects — check changelog for mocking-API
      breaking changes first.
- [ ] `Radzen.Blazor` 10.4.9 → 11.1.9 in `StageFright.UI` — check breaking-changes doc; this is a
      UI-rendering component library used across many pages, so per CLAUDE.md's UI-verification
      rule this needs a manual browser smoke-test after bumping, not just green tests.
- [ ] `Microsoft.Maui.Controls` / `Microsoft.AspNetCore.Components.WebView.Maui` 10.0.71 → 10.0.90
      in `StageFright.App` — the biggest compatibility risk in the audit, since it also moves
      `Microsoft.WindowsAppSDK.*` from the `1.8.x` to `2.x.x` major line; may require a matching
      MAUI workload update (`dotnet workload update`) alongside the package bump. Do this in its
      own isolated change with a full manual app run (`dotnet run --project src/StageFright.App/`)
      before merging.
- [ ] `Humanizer.Core` / `Microsoft.CodeAnalysis.*` transitive bumps via
      `Microsoft.EntityFrameworkCore.Design` need no independent action — they'll move when EF
      Core Design itself is bumped in Stage 2, and since it's a dev-time-only tool
      (`PrivateAssets=all`), there's no shipped-app risk.

### Stage 4 — Backlog, not part of this remediation

- [ ] `xunit` → `xunit.v3` migration across all 5 test projects. Not a security issue; a larger,
      separate migration effort that shouldn't be bundled with the security/patch work above.

### Optional process improvement (not required to fix the vulnerability, worth flagging)

- [ ] Consider adopting a root `Directory.Packages.props` for central package management — 12
      projects currently pin the same packages (e.g. `Microsoft.Extensions.Logging.Abstractions`,
      `QuestPDF`, `Microsoft.EntityFrameworkCore.Sqlite`) independently across `.csproj` files,
      which is exactly how this kind of drift (`10.0.9` vs `10.0.10` inconsistently) happens. This
      would be a separate, larger change and is not needed to resolve the current findings.

---

## Verification Summary

1. Stage 1: `dotnet list StageFrightCommunity.slnx package --vulnerable --include-transitive` →
   zero results; `dotnet test tests/StageFright.UI.Tests/` green.
2. Stage 2: `dotnet build` (solution-wide) + `dotnet test` (all 5 projects) green, per CLAUDE.md.
3. Stage 3: same build/test gate per bump, plus a manual `dotnet run --project src/StageFright.App/`
   smoke-test specifically after the Radzen.Blazor and MAUI/WindowsAppSDK bumps, since those affect
   rendered UI and platform hosting respectively — automated tests alone don't cover visual/runtime
   regressions there.
4. Commit per stage (not all at once), following this repo's CLAUDE.md Git/Commit Workflow, so a
   regression can be bisected to a single stage.
