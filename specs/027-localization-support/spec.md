# Feature Specification: Localization Support (Language Resource Files)

**Feature Branch**: `027-localization-support`

**Created**: 2026-08-27

**Status**: Draft

**Input**: User description: "I would like to add localization support to this app. All current text is in Australian english. Include extracting the current hard coded text and labels to language files and updating the ui to use language files. Stop after creating the plan. Do not commit." — plus follow-up: "the app should respect system localisation settings and use that as the default language if the resource file exists; if not, fall back to Australian English."

## Clarifications

### Session 2026-08-27

- Q: For v1, must a language change take effect immediately while the app is running, or is applying only after a restart acceptable? (FR-021) → A: Applies on next launch only; the user is shown a restart notice at the point of change. In-session live switching is out of scope for v1.
- Q: When a non-baseline language/region is selected, how are monetary amounts displayed? (FR-015 / FR-016) → A: Amounts stay presented in Australian dollars ("$" / "AUD"); only the decimal separator, digit grouping and currency-symbol placement follow the selected region. The currency symbol/code itself never changes with the UI language. **Superseded by spec 028:** the currency is no longer hard-coded — it is chosen once at first-run setup (`Settings.CurrencyCode`, ISO 4217, default `AUD`/`"$"`), and `MoneyFormatter` renders that configured symbol/code and minor-unit precision; region still drives only separators, grouping and placement.
- Q: Must a newly added language also be registered in a maintained supported-languages list, or is dropping in its resource set enough? (FR-011 / FR-012 / FR-023) → A: The app auto-discovers the resource sets it ships and derives each language's endonym from its culture metadata; adding a resource set is sufficient, with no code or list change. Test/pseudo locales are excluded from the user-facing selector by naming convention.
- Q: Are non-visible accessibility strings (aria-label, image alt text, title/tooltip attributes) in scope for extraction? (FR-001 / SC-001) → A: Yes — app-authored aria-label, alt and title/tooltip text is extracted to resources and covered by the SC-001 completeness guard, the same as visible text. Genuinely decorative/empty alt="" and aria-hidden content carry no translatable text and are exempt.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - App text comes from a language resource file, not hardcoded (Priority: P1)

Today every visible label, heading, button caption, placeholder, menu name, validation message and status message is written directly into the screen markup and code. This story replaces that with a single, translatable source: all user-facing text for a defined first slice of the app — the navigation shell plus one complete module (Members) — is moved into language resource files, each entry addressed by a stable identifier, and the screens read their text from those files. What an end user sees is unchanged — the same Australian English wording — but the wording now lives in one place a translator or the team can edit without touching code.

**Why this priority**: Nothing else in the feature is possible until the extraction pattern exists and is proven end-to-end on a real slice. It delivers value on its own: a maintainer can change any wording in that slice in one file, and the app is demonstrably ready to accept a second language.

**Independent Test**: Take the navigation shell and the Members module. Confirm no user-facing literal string remains in their screen, code-behind and provider files (all text comes from resource lookups, including the display text of enumeration values such as member status); the app runs and every screen in that slice shows the same wording as before; and editing a value in the resource file changes what the screen displays after a rebuild.

**Acceptance Scenarios**:

1. **Given** the app running in the baseline language, **When** a user opens any screen in the P1 slice, **Then** every label, heading, button, placeholder, menu entry and validation/status message shown is identical to the pre-change wording.
2. **Given** a maintainer changes one text value in the baseline language resource file, **When** the app is rebuilt and reopened, **Then** the corresponding on-screen text reflects the new value with no code change.
3. **Given** a resource key has no entry for the active language, **When** the screen renders, **Then** it shows the baseline (Australian English) value — never a blank, never the raw key — and the gap is recorded where the team can find it.
4. **Given** automated tests for the P1 slice, **When** they run, **Then** they assert text through resource keys / the resource lookup rather than brittle hardcoded string matches, and they pass.

---

### User Story 2 - Every remaining screen, report and system message is localized (Priority: P2)

Extends Story 1's pattern to the whole application: all other modules (Dashboard, Rehearsals, Events/AGM, Finance, Reports, Settings, Setup Wizard), the shared components, the dashboard tiles, the printable/PDF and CSV reports, and the user-facing text of validation and error/exception messages. After this story there is no user-facing hardcoded string left in the app's own code, and a translator has exactly one set of files to work from to produce a complete new language.

**Why this priority**: This is the bulk of the "extract the current hardcoded text and labels" request, but it is mechanical repetition of a pattern that must first be settled and reviewed in Story 1. Splitting it out lets the design-bearing slice merge before the long tail.

**Independent Test**: Run a repository-wide check for user-facing string literals in the app's own screen, code-behind, module-provider, report-provider/renderer and exception/validation code; confirm the only remaining matches are non-user-facing (log messages, format tokens, CSS classes, routes, keys). Open every screen and generate every report; confirm wording is unchanged from today.

**Acceptance Scenarios**:

1. **Given** the full app, **When** any screen, dialog, dashboard tile, menu, settings tab or setup-wizard step is opened, **Then** all visible text originates from a resource lookup.
2. **Given** each printable/PDF and CSV report is generated, **Then** its title, column headers, section labels, subtotal/total labels and fixed annotations come from resource lookups and read identically to today.
3. **Given** an operation raises a validation or domain error that is shown to the user, **Then** the message the user sees comes from a resource lookup; developer-only log text may remain in English.
4. **Given** a screen or report shows an enumeration value (a status, fee/payment type, account type, tax treatment, reconciliation status, or similar), **When** it renders, **Then** the visible text comes from a resource lookup, while any value used for filtering, sorting, comparison or storage is the unchanged culture-invariant enum identity.
5. **Given** the baseline language resource files, **When** they are inspected, **Then** they contain an entry for every key used anywhere in the app — including one per user-facing enumeration member — and no key is defined but unused (allowing for deliberately shared keys).

---

### User Story 3 - The app opens in the right language, and a user can change it (Priority: P3)

On first run, with no choice yet made, the app presents itself in the operating system's display language when it ships a resource set for that language, and in Australian English otherwise — the coordinator is not forced to pick a language before the app is usable. A coordinator can then open Settings (and, on first run, the Setup Wizard) and pick the display language explicitly from the list of languages the app ships with. That explicit choice is saved, always wins over the operating-system language, and is applied so the whole app — screens, menus, messages, and culture-sensitive formatting of dates, numbers and currency — presents in the selected language and region.

**Why this priority**: The extraction (Stories 1–2) is the substance of the request and is valuable even with a single language. Honouring the system language and offering an explicit override is the pay-off, but it depends entirely on the resource infrastructure being in place and only becomes observably different from "always Australian English" once a second resource set exists.

**Independent Test**: With at least one non-baseline language available (a real translation or a test/pseudo language): (a) set the operating system's display language to that language, launch a fresh install that has made no explicit choice, and confirm the app starts in it; (b) set the operating system to a language the app ships no set for and confirm the fresh install starts in Australian English; (c) select a language explicitly in Settings, restart, and confirm the choice persisted and overrode the system language; (d) switch back to Australian English and confirm the original presentation returns.

**Acceptance Scenarios**:

1. **Given** more than one shipped language, **When** the user opens the language selector in Settings, **Then** it lists each shipped language by its own name (endonym) and indicates which one is active.
2. **Given** the user selects a different language and confirms, **When** the app is next launched, **Then** the visible app text and the formatting of dates, numbers and currency present in that language and region; a restart notice is shown at the point of change (FR-021).
3. **Given** the user has made an explicit language choice, **When** the app is closed and reopened, **Then** the app starts in the chosen language regardless of the operating system's display language.
4. **Given** a brand-new install, or an install that predates this feature, where no language has been chosen explicitly, **When** the app starts and the operating system's display language matches a shipped resource set, **Then** the app presents in that system language.
5. **Given** the same no-explicit-choice state, **When** the app starts and the operating system's display language has no matching shipped resource set (or cannot be resolved), **Then** the app presents in Australian English.
6. **Given** a shipped language is missing some keys, **When** the app runs in it, **Then** the missing pieces fall back to Australian English rather than showing blanks or keys.

---

### Edge Cases

- A key referenced by a screen but absent from the baseline resource file must be caught at build time or test time, never shipped as a blank or a raw key.
- Text with runtime values (counts, names, amounts, dates) must use ordered or named placeholders so a translator can reposition them; count-dependent wording ("1 year" vs "2 years") must be expressible per language, not hardcoded by appending "s".
- Very long translated strings must not break layouts — sidebar labels, buttons, table headers, and fixed-width PDF cells in particular.
- Right-to-left languages and non-Latin typography are not addressed by this feature; the key/resource design must not make adding them later impossible.
- User-entered data (member names, organisation name, account names, event titles, notes) is never translated — only app-authored chrome and messages.
- App-authored accessibility text (`aria-label`, image `alt`, `title`/tooltip) is treated as user-facing text and extracted like any visible label; only genuinely decorative or empty accessibility attributes (`alt=""`, `aria-hidden` content) are exempt from the completeness guard.
- Financial math and stored values stay culture-invariant; only display formatting follows the selected region, and only for dates and numbers — monetary amounts keep the Australian-dollar symbol/code ("$" / "AUD") in every language, with just their separators, grouping and symbol placement localised. Changing language must not alter any stored amount or GL balance. **Superseded by spec 028:** the currency is now the organisation's configured `Settings.CurrencyCode` (ISO 4217, default `AUD`), so `MoneyFormatter` renders the chosen symbol/code and minor-unit precision; the culture-invariance of stored amounts and GL balances is unchanged.
- Number and date input parsing must accept the selected culture's format without corrupting stored values.
- An enumeration value that appears both as a user-visible label and as a token for filtering, sorting or persistence (e.g. a member-status report filter, `["Active", "Inactive", "Archived", "All"]`) — only the label is localized; the option/token value stays the culture-invariant enum name, and the two must not be conflated.
- An enum shown to the user has no dedicated key, or a new enum member is added without one — the completeness guard must fail rather than the app rendering the raw member name.
- A report generated after a language change reflects the new language and is internally consistent (no mixed-language output).
- Plugin-contributed text (tiles, settings tabs, menu items, reports from external plugins) is owned by the plugin and is out of scope; the app must not crash or render blank when a plugin supplies only English.
- Changing the language selection while a screen is open or a workflow is mid-way must not disturb unsaved form input: per FR-021 the change only takes effect on the next launch, and the user is shown the restart notice at the point of change rather than the app switching language under them.
- The baseline "Australian English" wording (e.g. "Organisation", "Default July — Australian financial year") is preserved verbatim during extraction — extraction is not a copy-editing pass.
- **System-language default** — "matching resource set" is resolved by exact culture first (e.g. `fr-CA`), then the parent language (`fr`), and only among the resource sets the app actually ships (discovered at runtime per FR-011, test/pseudo locales excluded); anything else is not a match.
- The operating system's display language changes between launches while no explicit selection is stored — on the next launch the app follows the new system language if a matching resource set is shipped, otherwise it stays on Australian English.
- The operating system reports a language for which only a *partial* resource set is shipped — that set still counts as "exists" and is used as the default, with per-key fallback to Australian English.
- The operating system reports a display-language string the platform cannot resolve to a known culture — the app falls back to Australian English without error.
- A `Settings.LanguageCode` value left over from a downgraded install that names a language no longer shipped is treated as "no explicit choice" and re-resolved from the system language / Australian English.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST store all user-facing text authored by the application — labels, headings, button captions, placeholders, menu and tile names, tab names, validation messages, user-visible status/error messages, and app-authored accessibility text (`aria-label`, image `alt` text, and `title`/tooltip attribute values) — in language resource files rather than as literals in markup or code. Genuinely decorative or empty accessibility attributes (`alt=""`, `aria-hidden` content) carry no translatable text and are exempt.
- **FR-002**: Each piece of user-facing text MUST be addressed by a stable, human-readable key that is independent of the wording, so changing the wording does not change the key.
- **FR-003**: The application MUST ship with a complete baseline language set for Australian English containing an entry for every key used anywhere in the application.
- **FR-004**: The baseline extraction MUST preserve the current wording exactly, including Australian spellings and phrasing; it MUST NOT be used as an opportunity to reword copy.
- **FR-005**: Every screen, dialog, shared component, dashboard tile, navigation entry, settings tab and setup-wizard step MUST obtain its user-facing text — including the accessibility strings named in FR-001 — through a resource lookup at render time.
- **FR-006**: Every printable/PDF and CSV report MUST obtain its title, column headers, section labels, subtotal/total labels and fixed annotations through a resource lookup.
- **FR-007**: User-facing validation and domain-error messages MUST be sourced from resource files; purely diagnostic or log-only text MAY remain in English.
- **FR-008**: When a key has no entry for the active language, the system MUST fall back to the Australian English value and MUST NOT display a blank or the raw key.
- **FR-009**: The system MUST record every missing-key fallback (via logging and/or a test-detectable signal) so gaps can be found and filled.
- **FR-010**: Text containing runtime values MUST use ordered or named placeholders so translators can reposition arguments, and count-dependent wording MUST be expressible per language.
- **FR-011**: The system MUST allow a new language to be added by supplying a new resource set only, with no change to screen or business code and no edit to any hand-maintained language list. The set of offered languages MUST be discovered at runtime from the resource sets the application ships, and each language's display name (endonym) MUST be derived from its culture metadata. Test/pseudo locales MUST be excluded from the user-facing selector by naming convention.
- **FR-012**: The application MUST expose a language selector in Settings that lists every shipped language by its own endonym and indicates the active one. The list MUST be populated by the runtime discovery of FR-011, not from a maintained list.
- **FR-013**: The Setup Wizard MUST let a first-run user choose the display language, pre-selecting the default resolved per FR-023.
- **FR-014**: An explicitly selected language MUST be persisted and MUST be re-applied automatically when the application restarts.
- **FR-015**: Applying a language MUST update visible text and the culture-sensitive formatting of dates and numbers to the selected language and region. Monetary amounts MUST remain presented in Australian dollars — the currency symbol/code ("$" / "AUD") MUST NOT change with the UI language; only the decimal separator, digit grouping and symbol placement of those amounts follow the selected region. **Superseded by spec 028 (FR-001…FR-006):** the currency symbol/code is no longer fixed at `$`/`AUD` — it is the organisation's `Settings.CurrencyCode` (ISO 4217, chosen at first-run setup, default `AUD`), rendered with its own minor-unit precision by `MoneyFormatter`; applying a language still changes only separators, grouping and symbol placement, never the configured currency.
- **FR-016**: Changing the language MUST NOT alter any stored value, financial amount or GL balance; only presentation may change.
- **FR-017**: An existing installation with no stored language preference, and a brand-new installation, MUST run in the default language resolved per FR-023; while Australian English is the only shipped language this resolves to Australian English, with presentation identical to the pre-feature behaviour.
- **FR-018**: Automated tests for localized surfaces MUST assert text via resource keys or the resource lookup rather than hardcoded string matches, and the test suite MUST include a guard that fails when a used key has no Australian English entry.
- **FR-019**: The system MUST keep user-entered data (member names, organisation name, account names, event titles, notes, and similar) untranslated.
- **FR-020**: Plugin-contributed text MUST remain the responsibility of the plugin; the host MUST render plugin-supplied text as-provided and MUST NOT fail when a plugin supplies only one language.
- **FR-021**: A newly selected language MUST apply on the next application launch; in-session live language switching is out of scope for v1. The system MUST inform the user with a restart notice at the point of change.
- **FR-022**: Project documentation MUST be updated to describe where resource files live and how to add a language, and any existing `specs/` documentation made stale by the extraction MUST be updated in the same effort.
- **FR-023**: When no language has been explicitly selected, the system MUST detect the operating system's display language at startup and use it as the default when the application ships a matching resource set — matched by exact culture, then by parent language, and only among the resource sets the application actually ships (the runtime-discovered set of FR-011, excluding test/pseudo locales). When there is no match, or the operating-system language cannot be resolved, the system MUST fall back to Australian English. An explicitly selected language MUST always take precedence over the operating-system language.
- **FR-024**: The user-facing display text of enumeration values — member status, fee type, payment method and type, account type, tax treatment, reconciliation status, theme, and any other enum shown to a user, including the labels of enum-derived report filter options — MUST be sourced from resource files, keyed by the enumeration type and member (never `enum.ToString()` or a hardcoded switch at the point of display). The enum name or numeric value used for storage, GL, backups, report filter tokens, sorting and comparison MUST remain culture-invariant. Purely internal enums that are never shown to a user (for example report column alignment, dashboard tile size, the platform-theme probe) are out of scope.

### Key Entities *(include when feature involves data)*

- **Language Resource Set**: the collection of key-to-text entries for one language and region (e.g. Australian English). Key attributes: culture identifier (language + region), coverage (which keys it defines), status (shipped / complete / partial). Relationships: exactly one baseline set (Australian English) that every other set falls back to; zero or more additional sets. A set "exists" for matching purposes (FR-023) whether it is complete or partial.
- **Resource Key**: the stable identifier for one piece of user-facing text. Key attributes: key name, owning area/module, whether it takes runtime arguments, and a description of those arguments. Relationships: referenced by one or more screens, reports or messages; has exactly one entry in the baseline set (mandatory) and an optional entry in each additional set. An enumeration member is addressed like any other key, named for its enumeration type and member (e.g. an `Enum_MemberStatus_Active`-style name).
- **Language Preference**: the user's chosen display language for this installation. Key attributes: selected culture identifier (may be unset), whether it was explicitly chosen or defaulted. Relationships: a singleton held with the other application settings (alongside the existing theme preference) and read at startup. When unset, the effective language is derived at startup from the operating system's display language (if a matching resource set is shipped) or Australian English otherwise; an explicit value always overrides that derivation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of user-facing text authored by the application is served from language resource files — a repository check finds zero user-facing string literals in the app's own screen, component, provider, report and user-facing-message code (excluding logs, routes, keys, format tokens and styling). This includes the display text of user-facing enumeration values: an `enum.ToString()` or a hardcoded status switch reaching a screen or report counts as a violation. It also includes the values of app-authored `aria-label`, `alt` and `title`/tooltip attributes — a hardcoded literal there is a violation — while genuinely decorative/empty `alt=""` and `aria-hidden` content are exempt.
- **SC-002**: With Australian English active, every screen and every report renders wording identical to the pre-feature build, verified across the full screen and report inventory.
- **SC-003**: Adding a complete new language requires adding one resource set and zero code changes — the app discovers it at runtime and lists it in the selector by its endonym automatically — demonstrated end-to-end with a test or pseudo language.
- **SC-004**: A missing translation key never reaches the user: zero occurrences of blank or raw-key text in any language, 100% fall back to Australian English, and every fallback is recorded.
- **SC-005**: Selecting a language and restarting the app results in the app presenting in that language — including date and number formatting, and the regional separators/placement of monetary amounts while those amounts stay in Australian dollars — on 100% of attempts.
- **SC-006**: Changing the display language produces zero changes to stored data, financial amounts or GL balances, verified by comparing persisted state before and after a switch.
- **SC-007**: An existing install upgraded to this version, with no action taken by the user, shows no user-visible difference from the previous version.
- **SC-008**: The full automated test suite passes and includes a guard that fails the run if any used resource key lacks an Australian English entry.
- **SC-009**: A translator can produce a complete new language working only from the resource files and a short written guide, with no access to source code.
- **SC-010**: On a fresh install with no explicit selection, the app starts in the operating system's display language on 100% of attempts when a matching resource set is shipped, and in Australian English on 100% of attempts when it is not (including when the operating-system language cannot be resolved).

## Assumptions

- "Localization support" means the resource infrastructure plus a complete Australian English (`en-AU`) baseline extraction. No additional production-translated language is in scope; a test/pseudo language is used to prove switching and fallback. Which real languages to add later is a business decision outside this feature.
- The list of shipped languages is discovered at runtime from the resource sets present in the build (endonyms taken from each culture's own metadata), not from a hand-maintained catalog, so a new language is genuinely a drop-in resource set (FR-011, resolved in Clarifications, Session 2026-08-27). Test/pseudo locales (e.g. a pseudo-localisation culture) are identifiable by their culture name and are excluded from the user-facing selector and from FR-023 system-language matching, while still usable by automated tests.
- Australian English (`en-AU`) is the ultimate fallback culture and the baseline every resource set falls back to key-by-key. On first run with no explicit selection the effective default is the operating system's display language when the app ships a matching resource set (per FR-023), otherwise `en-AU`. Because `en-AU` is the only shipped language within this feature's scope, the effective first-run default is `en-AU` until more languages are added.
- "Respect system localisation settings" refers to the operating system / device display (UI) language. It only sets the *default* display language; it never overrides an explicit user selection and never changes the culture-invariant rule for stored and serialized values.
- The app is a single-user desktop (MAUI Blazor Hybrid) application; "language" is a per-installation preference, not per-user or per-request.
- The language preference is stored with the other application settings (the singleton settings record), mirroring how the theme preference is stored, and is read once at startup; the operating-system display language is read at the same startup point, and only when no explicit preference is stored.
- FR-021 (resolved in Clarifications, Session 2026-08-27): a language change takes effect on the next application launch and the user is shown a restart notice when they change it. In-session live switching is out of scope for v1, but the design should leave room to add it later without rework.
- Culture-sensitive formatting of dates and numbers follows the selected region, and parsing of user input follows the same region; stored and serialized values (database, backups, GL) remain culture-invariant. Monetary amounts are always Australian dollars: the "$" / "AUD" symbol/code is fixed regardless of the UI language, and only the number's separators, grouping and symbol placement follow the selected region (FR-015, resolved in Clarifications, Session 2026-08-27). **Superseded by spec 028:** the currency is no longer hard-coded — it is chosen once at first-run setup (`Settings.CurrencyCode`, ISO 4217, default `AUD`/`"$"`) and `MoneyFormatter` uses that configured symbol/code and minor-unit precision; separators, grouping and placement still follow the region.
- User-entered content is never translated. Serilog / log output remains English.
- Localizing an enum value covers its *display* text only. The enum name/number used in storage, GL, backups, report filter tokens, sorting and comparison stays culture-invariant (consistent with FR-016). Several user-facing enums (member status, fee/payment/account types, tax treatment, reconciliation status) appear in both screens and reports, so their labels sit in a shared resource so on-screen and printed text match.
- Right-to-left layouts and non-Latin typography tuning are not in scope; the key and resource design must not preclude adding them later.
- Plugin-authored text is out of scope and the plugin contract interfaces are not changed by this feature.
- Report and PDF layouts may get minor width/wrapping tolerance for longer translated strings, but a responsive redesign of reports is out of scope.
- The extraction proceeds module by module: Story 1 (navigation shell + Members) settles the pattern, and the same pattern is then applied uniformly across the remaining surfaces.
