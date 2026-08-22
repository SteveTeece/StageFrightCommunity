# Phase 0 Research: AGM Workflow

**Feature**: [spec.md](./spec.md) | **Date**: 2026-07-31

Each entry: **Decision** (what we chose) / **Rationale** (why) / **Alternatives considered** (what else, and why not). Findings below come from a four-area parallel codebase investigation (Events module, Committee/Members domain, Settings & Setup wizard, Reports/grid/testing conventions).

---

## D1 — Committee terms auto-supersede through chronological open/close, not an explicit "supersede" write

**Decision**: Every saved AGM creates exactly one new `CommitteeTerm` row (`StartDate` = that AGM's date). Creating a new term automatically closes whatever term was previously open (sets its `EndDate` = the new AGM's date). "Current committee" is simply "the position records belonging to whichever `CommitteeTerm` has `EndDate == null`." No write ever touches an older AGM's own stored records.

**Rationale**: This mechanically satisfies the clarified rerun edge case (spec.md Edge Cases, and FR-010/FR-025) without any bespoke "supersede" operation: if AGM #1 starts Term‑A and a rerun AGM #2 is saved two days later, AGM #2 simply starts Term‑B, which closes Term‑A in the same stroke. Term‑A's position records are never edited or deleted — they stay exactly as AGM #1 saved them, inspectable forever via AGM #1's own read-only detail view (FR-016). "Current committee" trivially becomes Term‑B's records because Term‑A is no longer open. This also matches User Story 3's own definition of a term ("runs from one AGM to the next") literally — even a two-day term between a failed-quorum meeting and its rerun is still, by that definition, a term.

**Alternatives considered**: Model "supersede" as an explicit rewrite/soft-delete of the earlier AGM's position records tied to one shared `CommitteeTerm` row. Rejected — it directly violates the clarified answer (prior AGM's records must be preserved unmodified) and requires an extra "is this the current source AGM" flag that the open/closed term state already provides for free.

---

## D2 — Extend `CommitteeMembership` in place (rename to `CommitteePositionRecord`), carrying legacy and new shapes side by side

**Decision**: Rename `CommitteeMembership` → `CommitteePositionRecord` (same table via migration rename) and add nullable fields: `CommitteeTermId` (FK, nullable), `OfficeHolderTypeId` (FK, nullable — null means general committee member), `StartDate` (nullable), `EndDate` (nullable). The existing `Year` (int) and `Position` (string) fields are kept but become nullable/legacy-only — populated on historical rows created before this feature, and left `null` on every row this feature creates. A row's era is determined by whether `CommitteeTermId` is set.

**Rationale**: The spec's Key Entities section explicitly calls this "Committee Position Record *(existing — reused, extended)*", and its Assumptions/Edge Cases explicitly forbid retroactively re-dating historical calendar-year records. Carrying both shapes on one entity is the minimal-migration path that satisfies both constraints without fabricating history.

**Alternatives considered**: Synthesize a `CommitteeTerm` per historical `Year` value and backfill `CommitteeTermId` for every legacy row. Rejected — there was no AGM entity (and often no exact AGM date) before this feature, so any synthesized term boundary would be fabricated, directly contradicting the spec's explicit "not retroactively re-dated" assumption.

**Schema impact confirmed by research**: the current `(MemberId, Year)` filtered-unique index on `CommitteeMembershipConfiguration` must be dropped — it enforces the wrong invariant for the new model (see D9 for the replacement indexes).

---

## D3 — Remove the old manual committee-reset mechanism entirely (FR-018)

**Decision**: Delete `CommitteeAnnualResetService` and `ICommitteeAnnualResetService` outright (both `CheckAgmBannerAsync` and `ResetAsync`), their DI registration, the banner `<div>` + "Reset now" button + "Reset Committee for New Year" button in `GeneralSettingsTab.razor`, the corresponding fields/handlers in `GeneralSettingsTab.razor.cs` (`_agmBanner`, `HandleResetCommitteeAsync`, the `OnInitializedAsync` banner-check block), the now-obsolete `SoftDeleteCurrentYearAsync` methods on `ICommitteeMembershipRepository`/`ICommitteeService`, the `Settings.LastCommitteeResetYear` field (plus its `SettingsBackupDto`/`BackupService` mapping and a migration `DropColumn`), and the `V13_CommitteeResetAgmBannerTests.cs` integration test file (replaced by the new AGM workflow's own integration coverage).

**Rationale**: research confirmed `CommitteeAnnualResetService.CheckAgmBannerAsync`/`ResetAsync` is a purely calendar-year, manual-click mechanism that FR-009/FR-010's atomic AGM-save behavior makes redundant (clarified Q1, Option A). Research also confirmed `CommitteeRenewalMonth` is *not* actually read by any of this timing logic today (`CheckAgmBannerAsync` gates only on `LastCommitteeResetYear` + `IEventRepository.AgmExistsInYearAsync`/`GetMostRecentPastAsync`), so removing the banner and repurposing `CommitteeRenewalMonth` (D7) are independent, non-entangled changes.

**Alternatives considered**: Keep the banner but repoint it at the new `AnnualGeneralMeeting` entity instead of the generic `Event`. Rejected by the clarification itself (Option A) — once AGM save is atomic and self-contained, a separate manual "go reset now" step has nothing left to do.

---

## D4 — FR-003 (stop offering "Annual General Meeting" as a generic event type): drop it from the default seed, filter it defensively for upgrades, rewrite the dev seeder outright

**Decision**: `EventType` is a DB-seeded table row (`EventTypeService.GetDefaultEventTypeNames()` includes `"Annual General Meeting"` as a seeded, `IsSystemDefault` row), not an enum. Two parts:
1. Remove `"Annual General Meeting"` from `GetDefaultEventTypeNames()`'s hardcoded array — a brand-new install never gets this `EventType` row at all, since AGMs are their own dedicated record type from day one.
2. For an existing install upgrading into this feature (where the row, and possibly historical `Event` rows referencing it, already exist), FR-003's "preserving any existing events historically tagged with that type" is satisfied by leaving that row and its FK'd `Event` rows completely untouched — the "create new generic event" dropdown filters it out by name (`GetSelectableForNewEventsAsync()`), which is a no-op for fresh installs (nothing to filter) and a real filter for upgrading installs (row still exists, just hidden from new-event creation).

**Repo-local seed/test data is explicitly out of scope for "preservation"**: this repo's own `TestData/stagefright.db` and `src/StageFright.App/Seeding/DebugDataSeeder.cs` are dev/test fixtures, not real customer history — confirmed by the user, who noted the seeded EventType row "can be ignored" and seed data "can be removed and regenerated to comply with the new spec." Concretely, `DebugDataSeeder.SeedAgmAsync` (currently: `_eventService.ScheduleAsync` with the AGM `EventType` + `RecordParticipationAsync`) and `SeedCommitteeAsync` (currently: `CommitteeService.AddOrUpdateAsync(memberId, year, position)` per member/year) are rewritten outright to seed through the new `IAgmService.RecordAsync` and the new office-holder-type/committee-term model. No migration or backward-compat path is needed for the seeder itself — it's simply regenerated to match the new spec, same as any other test fixture.

**Rationale**: No entity/migration change is needed for real historical data; existing (real) events keep their type reference exactly as before. The seed data, in contrast, is disposable fixture data local to this repo and should be rewritten fresh rather than laboriously preserved or migrated.

---

## D5 — The AGM attendance grid needs a new, non-paged, independently-scrolling pattern (FR-005)

**Decision**: Build the AGM attendance grid with `AllowPaging="false"` on `RadzenDataGrid`, wrapped in a container styled with the same `flex:1; min-height:0; overflow-y:auto` recipe already used by `.shell-content`/`.sidebar-list` in `src/StageFright.App/wwwroot/app.css` (lines ~155–160, ~218–228) to make the nav sidebar scroll independently of the shell frame. Because this is genuinely component-specific and has no existing global-stylesheet equivalent, it's the one part of this feature that warrants a `.razor.css` file per the constitution's CSS-isolation carve-out (§4.7.2).

**Rationale**: research grepped every `RadzenDataGrid` in the codebase (14 usages) and found zero instances of `AllowPaging="false"` or scroll-height styling anywhere — every existing grid (`MemberList`, `AttendanceGrid`, `ParticipationGrid`, etc.) is paged at `PageSize="15"`. This is a new pattern for the codebase, not a copy of an existing one.

---

## D6 — New "Committee" Settings tab as a 5th hardcoded core tab, not a plugin `ISettingsTabProvider`

**Decision**: Add the Committee configuration tab (office-holder titles + seat-count target, FR-012–014) as a 5th hardcoded `<Tab>` in `SettingsPage.razor`/`.razor.cs`, exactly matching the General/GST/EventTypes/Backup pattern: its own `CommitteeShown` flag, lazy `@if (CommitteeShown) { <ErrorBoundary>...</ErrorBoundary> }` instantiation, and an `OnClick` (not `OnShown`) handler.

**Rationale**: research found that despite `ISettingsTabProvider`'s doc comment claiming core tabs are registered through it, **none of the four real core tabs actually are** — `MauiProgram.cs` has zero `ISettingsTabProvider` registrations; that interface is exclusively the *plugin* extension path (`SettingsPage.razor.cs` injects `IEnumerable<ISettingsTabProvider>` only for `PluginTabs`, rendered via `DynamicComponent` with `OnShown`). CLAUDE.md's Known Gotchas explicitly warns that Settings tabs need the `OnClick`/lazy-render fix to avoid concurrent-DbContext/`OnShown` callback failures in the MAUI WebView — the plugin path does not have this fix. Building the Committee tab as a genuine `ISettingsTabProvider` plugin would silently reintroduce that exact gotcha.

**Alternatives considered**: Register via `ISettingsTabProvider`. Rejected for the reason above unless `SettingsPage.razor` is also patched to special-case it — at which point it's simpler to just add it as a 5th hardcoded tab like its siblings.

**Reference for the list-management UI** (add/rename/reorder/archive titles): `EventTypesTab.razor`/`.razor.cs` — separate active/archived lists reloaded after every mutation, a small `EditForm`+`DataAnnotationsValidator` add-form, a paged `RadzenDataGrid<T>` with an Actions-column `Template`, and an `IsSystemDefault`-style flag for rows that can't be archived (directly reusable for the built-in President/Secretary/Treasurer rows, FR-013).

**Cross-tab save-merge reminder**: every Settings tab re-fetches current `Settings` before saving and copies in the fields it doesn't own (confirmed pattern in `GeneralSettingsTab`/`GstSettingsTab`). The new Committee tab's fields (the repurposed AGM-month field, D7) must be added to every other tab's merge-preserve list, and the Committee tab itself must merge-preserve every field it doesn't own.

---

## D7 — Repurpose `Settings.CommitteeRenewalMonth` as the "AGM month" setting (FR-022/FR-030)

**Decision**: Reuse the existing `CommitteeRenewalMonth` int field (1–12, default 1) as the AGM month, rather than adding a second field. Update its doc comment and the `GeneralSettingsTab`/setup-wizard month-name dropdown to describe it as "the month the AGM is normally held." `SetupService.cs` currently hardcodes `CommitteeRenewalMonth = 1` at first-run init — thread it from the new wizard step's request field instead (mirroring how GST fields already flow from `SetupRequest` into `SetupService.InitializeAsync`).

**Rationale**: research confirmed this field is not read by any timing/scheduling logic today — it is a pure CRUD/backup/UI round-trip with no algorithm attached — so repurposing it is a zero-risk rename-in-place, not a migration of live behavior. This matches the spec's own Assumption ("reuses/replaces the existing committee-renewal-month configuration rather than adding a second, separate setting").

---

## D8 — New entities need explicit backup/restore support; don't assume it's automatic

**Decision**: For each new entity (`AnnualGeneralMeeting`, `AgmAttendanceRecord`, `CommitteeOfficeHolderType`, `CommitteeTerm`, extended `CommitteePositionRecord`), add a matching `*BackupDto.cs` in `src/StageFright.Core/Modules/Settings/Backup/`, extend `BackupEnvelope`/`BackupSnapshot` with the new collections, add mapper methods in `BackupService`, and add `EntityCounts` keys checked by `ValidateCompleteness()`.

**Rationale**: research found the existing `BackupService`/`BackupRepository` full-snapshot mechanism is genuinely comprehensive for entities it knows about (it always exports/imports the *whole* database in one `.sfbak` file), but `SettingsBackupDto` itself is already missing several `Settings` fields (`Abn`, `FinancialYearStartMonth`, GST fields, `ShowParticipationGraphs`) — proof that new fields/entities are **not** automatically covered and must be wired in explicitly per the existing `MemberBackupDto`-style pattern (flat `[ProtoContract]`/`[ProtoMember(n)]` class, sequential member numbers).

---

## D9 — Uniqueness invariants enforced via filtered indexes (defense in depth) + transaction-time validation

**Decision**:
- `unique index (CommitteeTermId, OfficeHolderTypeId) WHERE EndDate IS NULL AND OfficeHolderTypeId IS NOT NULL` — enforces "one holder per office-holder title at a time per term" (built-in and custom titles alike, per the clarified single-holder rule).
- `unique index (CommitteeTermId, MemberId) WHERE EndDate IS NULL` — enforces "a member can't hold more than one open position/seat in the same term" (FR-008), including general-committee slots.
- FR-008's "within the same AGM" check (all selections on one AGM-save must be mutually exclusive) is validated in application code *inside* the `ExecuteInTransactionAsync` lambda, before any writes, since it needs the full set of that save's assignments together — the DB indexes above are the durable backstop, not the primary UX validation path (which should reject with a `ValidationException` before the transaction even opens, mirroring `PaymentService.RecordAsync`'s "validate simple invariants before entering the transaction" pattern).

**Rationale**: matches the existing `CommitteeMembershipConfiguration`'s filtered-unique-index convention (`HasFilter("[IsDeleted] = 0")` — bracketed column names even on SQLite is the copied convention) and the constitution's "exhaustive test-path coverage" expectation of a hard backstop, not just service-layer validation.

---

## D10 — No new custom exception types needed

**Decision**: Reuse existing exception types — `ValidationException` for FR-008 one-slot-per-member violations, `EntityNotFoundException` for a missing AGM/member/office-holder-type lookup, `DataIntegrityException` for lifecycle violations (special election attempted on an already-closed term — the closest existing analogue is `ReconciliationException`'s "can't edit a finalized reconciliation," but the invariant fits `DataIntegrityException` without needing a dedicated new type).

**Rationale**: research enumerated all 10 existing custom exceptions in `src/StageFright.Core/Exceptions/`; none reference AGM/committee-term concepts, but each candidate scenario maps cleanly onto an existing type without forcing a fit.

---

## D11 — Special election has no dedicated entity

**Decision**: A special election (User Story 4) is a service *operation*, not a new stored entity. It: (1) validates the target `CommitteeTerm.EndDate IS NULL` (else `DataIntegrityException`), (2) finds the currently-open `CommitteePositionRecord` for the departing holder, sets its `EndDate` to the replacement date, (3) creates a new `CommitteePositionRecord` (same `CommitteeTermId`/`OfficeHolderTypeId`, `StartDate` = replacement date, `EndDate` = null) for the incoming member, (4) re-validates the FR-008-style one-slot-per-member rule for the incoming member, all inside one `ExecuteInTransactionAsync` call, with two audit log entries.

**Rationale**: the spec's Key Entities section lists exactly five entities (Annual General Meeting, AGM Attendance Record, Committee Office-Holder Type, Committee Position Record, Committee Term) — no "Special Election" entity — and the constitution favors simplicity over speculative structure. Everything FR-026/027/028 requires is expressible as start/end dates on `CommitteePositionRecord` rows the operation creates/closes.

---

## D12 — Test conventions to follow (the codebase's actual practice, not CLAUDE.md's literal naming)

**Decision**: Follow the *actual* patterns research found, which diverge slightly from CLAUDE.md's literal `Should_[Behavior]_When_[Condition]`/`_Integration`-suffix wording:
- Unit tests (`tests/StageFright.Core.Tests/Modules/Members/` or a new `Events/` subfolder): behavior-named methods in the `PaymentServiceTests.cs` style (e.g. `RecordAsync_CreatesPersistableAgmRecord`, `RecordAsync_ThrowsValidation_WhenMemberAssignedTwoPositions`), NSubstitute mocks for every repo/unit-of-work dependency, with `_unitOfWork.ExecuteInTransactionAsync` stubbed to invoke its delegate directly.
- bUnit tests (`tests/StageFright.UI.Tests/Pages/Events/`): follow `ParticipationGridTests.cs`'s pattern — `RadzenGridTestContext` base, DI-registered NSubstitute services, AngleSharp DOM queries.
- Integration tests (`tests/StageFright.Integration.Tests/Scenarios/`): add the next sequential scenario file, `V18_AgmWorkflowTests.cs` (confirmed `V17_BankDepositTests.cs` is the current latest), using the existing `Data Source=:memory:` + real `Database.MigrateAsync()` + hand-built repositories/services pattern (no DI container, no `UseInMemoryDatabase`).

**Rationale**: matching actual codebase precedent produces more reviewable, consistent code than mechanically applying the constitution's literal naming template where it has already drifted in practice.
