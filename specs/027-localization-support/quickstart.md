# Quickstart / Validation Guide: Localization Support

**Feature**: `027-localization-support` | **Date**: 2026-08-27

A run-and-check guide that proves the feature end-to-end. It does **not** contain implementation
code — see [contracts/localization-contracts.md](./contracts/localization-contracts.md),
[contracts/resource-key-catalog.md](./contracts/resource-key-catalog.md) and
[data-model.md](./data-model.md) for the shapes and rules, and `tasks.md` for the work items.

Scenarios are grouped by user story so each can be validated as its slice lands.

---

## Prerequisites

- .NET SDK per `global.json`; `dotnet restore` clean.
- `Microsoft.Extensions.Localization` present in `Directory.Packages.props` and referenced by
  `StageFright.Core` / `StageFright.Reports` / `StageFright.UI` (see research.md "New dependency").
- New test project `tests/StageFright.Localization.Tests` in `StageFrightCommunity.slnx`.
- The `qps-ploc` pseudo-locale `.resx` files exist as **test-fixture content only** (not shipped
  satellites) and deliberately omit ~3 keys.

## Build & full check (run after every slice)

```bash
dotnet restore
dotnet build
dotnet test                       # includes StageFright.Localization.Tests
```

Expected: build clean (judge warnings from a full rebuild — see CLAUDE.md); all tests green,
including the guard suite below.

---

## US1 — extraction pattern proven on the nav shell + Members

| # | Steps | Expected outcome |
|---|-------|------------------|
| 1.1 | Run `StageFright.Localization.Tests` **baseline completeness** + **residual-literal** guards scoped to the US1 file list ([resource-key-catalog.md §4](./contracts/resource-key-catalog.md)). | Pass. No user-facing literal — element text, **`aria-label` / `alt` / `title` values** (FR-001), `Text`/`Label`/`Placeholder`/`_errorMessage` assignments — remains in the shell + Members files. Decorative `alt=""` / `aria-hidden` are allowed. |
| 1.2 | Run the app (`dotnet run --project src/StageFright.App/`) in the default culture; open every Members screen + the nav shell. | Wording is byte-identical to the pre-feature build (SC-002), including Australian spellings ("Organisation"). |
| 1.3 | Edit one value in the neutral `MembersResource.resx`; rebuild; reopen. | The on-screen text changes with no code change (US1 AS-2). |
| 1.4 | Run the **enum-coverage** + **no-raw-enum-display** guards for `MemberStatus` / `Theme`. | Pass. `member.Status` renders via `LocalizeEnum()`; no `.ToString()` on an enum at a display site. |
| 1.5 | Run the **no-`"C"`-currency-format** guard over the US1 files; view a Members balance. | Pass. Amount rendered through `MoneyFormatter` (FR-015), not `.ToString("C")` / `{0:C}`. Symbol is `"$"`. |
| 1.6 | Run US1 bUnit component tests. | Assertions reference keys / the localizer, not hardcoded English (FR-018); green. |

## US2 — every remaining screen, report and message

| # | Steps | Expected outcome |
|---|-------|------------------|
| 2.1 | Run the completeness + residual-literal + enum-coverage + no-`"C"`-currency guards scoped to the full US2 file list. | Pass. Only non-user-facing literals remain (logs, routes, CSS, format tokens, `<option value>` enum tokens, keys). `aria-label` / `alt` / `title` text is all keyed. |
| 2.2 | Generate **every** report (all 10 `IReportProvider` reports + the print-only sheets) as PDF and CSV. | Titles, column headers, section/subtotal/total labels, fixed annotations all come from `ReportsResource`; wording identical to today (SC-002). No mixed-language output. |
| 2.3 | Trigger a user-facing validation / domain error in each module. | The message shown comes from `ValidationResource`; Serilog log text may stay English (FR-007). |
| 2.4 | Inspect the neutral `.resx` set. | One entry per referenced key, including one per user-facing enum member; no defined-but-unused key except deliberately shared ones (US2 AS-5 / SC-008). |
| 2.5 | Load `StageFright.TestPlugin` under a non-`en` culture. | Plugin's English strings render as-provided; no crash, no blank (FR-020). |

## US3 — right language on open, user can change it

Uses the `qps-ploc` pseudo-locale (test fixtures) as the "second language".

| # | Steps | Expected outcome |
|---|-------|------------------|
| 3.1 | Fresh install, `Settings.LanguageCode = null`, OS display language = a culture the app ships **no** set for. Launch. | App presents in Australian English (SC-010, US3 AS-5). |
| 3.2 | Same, but with a `qps-ploc` set discoverable and OS language mapped to it in a test. Launch. | App presents in the pseudo-locale (US3 AS-4 / SC-010). *In production `qps-*` is filtered from matching, so this is a test-only assertion of the ladder.* |
| 3.3 | Open the Settings language selector. | Lists each **runtime-discovered** shipped language by its own `CultureInfo.NativeName` endonym, active one indicated (FR-012). `qps-ploc` does **not** appear (FR-011). |
| 3.4 | Select a different language, confirm. | `Settings.LanguageCode` is persisted immediately; an inline **restart notice** is shown; the running app does **not** re-render in the new language (FR-021 — in-session switch out of scope for v1). Unsaved form input elsewhere is untouched. |
| 3.5 | Close and reopen the app. | Starts in the chosen language; the explicit choice overrides the OS display language (US3 AS-3 / SC-005). |
| 3.6 | With the pseudo-locale active (missing ~3 keys), exercise a screen using an omitted key. | Falls back to the Australian English value — never blank, never the raw key — and a `Warning` is logged (FR-008 / FR-009 / SC-004). |
| 3.7 | Under a non-`en` culture (comma decimal separator), view and enter a monetary amount. | Display shows `"$"` + that culture's separators/grouping (e.g. `$1 234,50`) — **not** `€` or another currency symbol (FR-015). An entered `1234,50` round-trips to the same stored `decimal`. |
| 3.8 | Snapshot the DB + computed member/GL balances; switch `LanguageCode`; re-resolve; re-snapshot. | Byte-identical stored state and GL balances (FR-016 / SC-006). |
| 3.9 | Set language back to Australian English; restart. | Original presentation returns exactly (US3 AS / SC-007). |
| 3.10 | `Settings.LanguageCode` holds a code for a language no longer shipped; launch. | Treated as "no explicit choice" — re-resolved from OS language / `en-AU`, no error (spec Edge Cases). |

---

## Guard suite reference (`StageFright.Localization.Tests`)

All are ordinary `dotnet test` tests; a failure blocks merge. Full behaviour table in
[resource-key-catalog.md §3](./contracts/resource-key-catalog.md):

- Baseline completeness · No orphan satellite keys · Placeholder parity · Plural pairing
- Enum coverage · No raw enum display
- Residual-literal scan (incl. `aria-label` / `alt` / `title`; decorative `alt=""` / `aria-hidden` exempt) — scoped per phase
- **No culture currency symbol** (`"C"` / `{0:C}` / `FormatString="{0:C}"` at a display site ⇒ fail)
- Missing-key logging

## Definition of done for the feature

- All guards green; `dotnet build` + full `dotnet test` green.
- Every screen and report renders identical wording under `en-AU` (SC-002 / SC-007).
- Adding a language = drop in one `.resx` set → discovered and offered automatically, zero code change (SC-003).
- `/docs` describes where resource files live and how to add a language; stale `specs/**` docs refreshed (FR-022).
