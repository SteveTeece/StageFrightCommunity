# Dark Mode First-Run Default — Handoff Status (2026-07-22)

Work in progress for GitHub issue #248 ("Default to Dark Mode"). This file is a resume point — read it before continuing so no work is repeated or lost.

## Where things stand

- **Design spec (approved, committed):** `docs/superpowers/specs/2026-07-22-dark-mode-first-run-default-design.md`
- **Implementation plan (committed):** `docs/superpowers/plans/2026-07-22-dark-mode-first-run-default.md` — 5 tasks. Execution mode chosen: Subagent-Driven Development (`superpowers:subagent-driven-development`).
- **Worktree:** `.claude/worktrees/dark-mode-default`, branch `worktree-dark-mode-default`, branched from local `dev` HEAD `a349e05` (includes the spec + plan commits, which were not yet pushed to `origin/dev` as of this session).
- **Progress ledger:** `.claude/worktrees/dark-mode-default/.superpowers/sdd/progress.md` (gitignored scratch — this file is the durable, committed backup of the same information).

## Task 1 — IN PROGRESS, uncommitted

All 5 files from the plan's Task 1 have been hand-written directly into the worktree (see "Incident" below for why), but **not yet verified with `dotnet build`/`dotnet test`, and not yet committed.**

Current `git status --short` in the worktree:
```
 M src/StageFright.UI/Layout/ThemeProvider.razor.cs
 M tests/StageFright.UI.Tests/Layout/ShellLayoutTests.cs
 M tests/StageFright.UI.Tests/Layout/ThemeProviderTests.cs
?? src/StageFright.Core/Contracts/IDeviceThemePreferenceProvider.cs
?? src/StageFright.Core/Enums/PlatformThemePreference.cs
```

All 5 files match the plan's Task 1 code exactly (see the plan file, Task 1 section, Steps 1/2/5/6 — the ThemeProviderTests.cs full-file replacement is Step 3).

### Next steps to finish Task 1

1. `cd .claude/worktrees/dark-mode-default` (or resume a session already there)
2. `dotnet build` — expect 0 errors (only the 5 pre-existing warnings unrelated to this change: NU1902 AngleSharp, CS8765 AbnInput nullability, 2x WINAPPSDKGENERATEPROJECTPRIFILE PRI249 — these existed before this work started, confirmed via the clean baseline run).
3. `dotnet test tests/StageFright.UI.Tests/ --filter "FullyQualifiedName~ThemeProviderTests|FullyQualifiedName~ShellLayoutTests"` — expect all passing.
4. `dotnet test` (full suite) — expect all 5 projects green (baseline before this work: 1202/1202 passing across Core.Tests 442, Reports.Tests 96, Data.Tests 130, UI.Tests 371, Integration.Tests 163).
5. Commit:
   ```
   git add src/StageFright.Core/Enums/PlatformThemePreference.cs src/StageFright.Core/Contracts/IDeviceThemePreferenceProvider.cs src/StageFright.UI/Layout/ThemeProvider.razor.cs tests/StageFright.UI.Tests/Layout/ThemeProviderTests.cs tests/StageFright.UI.Tests/Layout/ShellLayoutTests.cs
   git commit -m "Add device theme preference abstraction; ThemeProvider falls back to OS/Dark (#248)"
   ```
6. Append to `.superpowers/sdd/progress.md`: `Task 1: complete (commits <base7>..<head7>, review clean)` — but note no task-reviewer subagent has reviewed this task yet (see Incident below); dispatch one per the skill before marking it fully done, or review it yourself against the plan's Task 1 acceptance criteria if continuing solo.

## Tasks 2–4 — NOT STARTED

Follow the plan file exactly:
- **Task 2:** `SetupRequest.Theme` field + `SetupService.InitializeAsync` persists `request.Theme` (no more hardcoded `Theme.Light`) + test updates in `SetupServiceTests.cs`, `V1_FirstRunSetupTests.cs`, `V10_ThemeTests.cs`.
- **Task 3:** Theme toggle switch added directly to the Setup Wizard UI (`SetupWizard.razor` + `.razor.cs`) + new `SetupWizardThemeTests.cs`. **Note:** this task exists because GitHub issue #248 explicitly asks for a wizard-level toggle, which was NOT in the original user request or the first approved design — this was caught and confirmed with the user via AskUserQuestion mid-session before the plan was written. Don't drop it.
- **Task 4:** `MauiDeviceThemePreferenceProvider` in `StageFright.App` (uses `Application.Current.RequestedTheme` / `AppTheme` — confirmed available via MAUI's implicit global usings, no extra `using` needed) + DI registration in `MauiProgram.RegisterCoreServices`.
- **Task 5:** Full `dotnet build` + `dotnet test`, grep for stray `Theme.Light` default assumptions, then `gh issue close 248` with a summary comment — only after everything is green.

After Task 5: dispatch the final whole-branch code reviewer (`superpowers:requesting-code-review`'s `code-reviewer.md` template) against the full diff from the worktree's branch point, then use `superpowers:finishing-a-development-branch` to merge/PR. Remember: PRs in this repo must target `dev`, not `master` (per this user's standing memory).

## Incident: subagent isolation mistake (resolved)

The first Task 1 dispatch used `Agent(..., isolation: "worktree")`. That parameter creates a **separate, new** isolated worktree per agent — it does not mean "work in the shared worktree I already made." The subagent got stuck trying to also `EnterWorktree` into the shared `dark-mode-default` path from inside its own separate worktree and hit a hard block on all Bash/PowerShell/git commands, reporting BLOCKED. No commit resulted.

Recovery already done:
- Verified the stray worktree's file changes matched the plan exactly (by reading them), then discarded them rather than trying to merge/cherry-pick.
- Removed the stray worktree and its branch: `git worktree remove --force .claude/worktrees/agent-a25e0f71e0e51da6c` + `git branch -D worktree-agent-a25e0f71e0e51da6c` (run from the main repo checkout).
- Re-implemented Task 1's 5 files directly (Read/Write/Edit tools, no subagent) in the correct worktree.

**Going forward: do NOT pass `isolation: "worktree"` when dispatching implementer subagents for this plan.** The subagent-driven-development skill's design already assumes one shared worktree (the one this session is in) — subagents dispatched without an `isolation` parameter operate in that same working directory, which is what's wanted.

## Loose end: `.claude/settings.json` change in the MAIN repo checkout (not the worktree)

Before creating the worktree, `worktree.baseRef: "head"` was added to `.claude/settings.json` **in the main repo checkout** (`C:\Users\sgtee\source\repos\StageFrightCommunity`, not the worktree) so `EnterWorktree` would branch from local `dev` HEAD instead of `origin/master` (this repo's default branch is `master`, but PRs target `dev`, and local `dev` had 3 unpushed commits — including the spec/plan for this very feature — that would otherwise have been missed). This change is **still uncommitted** in the main checkout as of this handoff (`git status --short .claude/settings.json` there shows ` M .claude/settings.json`). It was an explicit, user-approved choice (see conversation: "Base it on current local dev (Recommended)").

Decide whether to commit it (makes `head`-based worktrees the standing default for this repo — reasonable given the PRs-target-dev convention) or revert it (if it should only have applied to this one worktree creation). Not yet resolved either way.

## Key file locations for anyone resuming

- Plan: `docs/superpowers/plans/2026-07-22-dark-mode-first-run-default.md`
- Design spec: `docs/superpowers/specs/2026-07-22-dark-mode-first-run-default-design.md`
- This status file: `docs/superpowers/status/2026-07-22-dark-mode-default-handoff.md`
- Progress ledger (scratch, gitignored): `.claude/worktrees/dark-mode-default/.superpowers/sdd/progress.md`
- Task 1 brief (scratch, gitignored): `.claude/worktrees/dark-mode-default/.superpowers/sdd/task-1-brief.md`
