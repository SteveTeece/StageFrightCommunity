# Quickstart: Validate Dashboard Tile Sizes

Prerequisites: repo built (`dotnet build`), feature implemented per `plan.md` / `tasks.md`.

## 1. Run the app and view the Dashboard

```bash
dotnet run --project src/StageFright.App/
```

Navigate to the Dashboard (default landing page after setup). Confirm:

- Every tile still renders its title, metrics, and (where configured) header action link, matching
  today's appearance for tiles that don't opt into a larger size (SC-004).
- At least one tile configured to a larger size (e.g. Attendance Trend or Cash Flow set to `OneByTwo`
  or `TwoByOne` per `contracts/IDashboardTileProvider.md`) visibly occupies more grid space than a
  `OneByOne` tile like Membership Summary (User Story 1).

## 2. Verify clean packing

- With the default set of core tiles (mixed sizes) on screen, confirm no tiles overlap and no row has
  an avoidable empty gap (SC-001).
- Resize the application window down to a narrow/mobile width. Confirm every tile — including
  `OneByTwo`/`TwoByOne`/`TwoByTwo` tiles — stacks to a single, full-width column with no horizontal
  scrollbar (SC-003, User Story 2 acceptance scenario 2).
- If a plugin tile is present (see `tests/StageFright.TestPlugin`), confirm the "Extensions" section
  packs independently of "Core Metrics" and the two sections remain visually separated (FR-006).

## 3. Run automated tests

```bash
dotnet test tests/StageFright.UI.Tests/ --filter "FullyQualifiedName~DashboardTests"
dotnet test
```

Expected outcome: all `DashboardTests.cs` cases pass, including new cases asserting:

- A provider with no `TileSize` override renders with the default 1x1 CSS class.
- A provider with a `TileSize` override renders with the matching size CSS class.
- Core Metrics / Extensions grouping and existing tile behaviours (click-through, action link,
  loading/error states) are unaffected by size.

The full solution `dotnet test` run must remain green (constitution §11.5 CI gate).
