# Localization: where the text lives and how to add a language

This is the guide referenced by **FR-022 / SC-009** of `specs/027-localization-support`. It is
written so a translator can produce a complete new language working **only from the resource
files and this page**, with no access to source code, and so a maintainer knows where every
user-facing string lives and how the pieces fit.

---

## 1. The one-minute version

* Every piece of app-authored user-facing text is a `name` → `value` entry in a `.resx` file.
* The **neutral** file (no culture in the name, e.g. `MembersResource.resx`) **is** the
  Australian English (`en-AU`) baseline. It is the ultimate fallback for every other language.
* To add a language you copy each neutral `.resx` to `<Marker>.<culture>.resx`
  (e.g. `MembersResource.fr-FR.resx`), translate the `<value>` of each entry, and rebuild.
  **No code changes, no list to edit.** The app discovers the new set at runtime and offers it
  in Settings → General and in the first-run Setup Wizard, listed by the language's own name.
* Anything you don't translate falls back, key by key, to the Australian English value — never a
  blank, never a raw key.

---

## 2. Where the resource files live

Text is grouped into a dozen **area** files (not one file per screen). Each area has a marker
class `<Name>Resource.cs` and, beside it, the neutral `<Name>Resource.resx` plus any
`<Name>Resource.<culture>.resx` translations.

| Area file | Project / folder | Owns |
|---|---|---|
| `SharedResource` | `src/StageFright.UI/Resources/Strings/` | Cross-cutting words used everywhere — Save, Cancel, Close, Yes, No, "Loading…", "Actions", "Status". |
| `DashboardResource` | `src/StageFright.UI/Resources/Strings/` | The Dashboard page and every dashboard tile caption. |
| `MembersResource` | `src/StageFright.UI/Resources/Strings/` | All Members screens and components. |
| `RehearsalsResource` | `src/StageFright.UI/Resources/Strings/` | All Rehearsals screens and the attendance grid. |
| `EventsResource` | `src/StageFright.UI/Resources/Strings/` | Events and AGM screens. |
| `FinanceResource` | `src/StageFright.UI/Resources/Strings/` | All Finance screens. |
| `SettingsResource` | `src/StageFright.UI/Resources/Strings/` | The Settings page and its tabs, including the language picker. |
| `SetupResource` | `src/StageFright.UI/Resources/Strings/` | The first-run Setup Wizard steps, including the language step. |
| `NavigationResource` | `src/StageFright.Core/Modules/Localization/Resources/` | Navigation-bar item titles and short labels, and the app shell chrome. |
| `ValidationResource` | `src/StageFright.Core/Modules/Localization/Resources/` | User-facing validation and domain-error message text. |
| `EnumsResource` | `src/StageFright.Core/Modules/Localization/Resources/` | The display text of every user-facing enumeration value (see §4.4). Shared so a status reads the same on a screen and in a printed report. |
| `ReportsResource` | `src/StageFright.Reports/Resources/` | PDF/CSV report titles, column headers, section labels, subtotal/total labels and fixed annotations. |

Each owning project sets `<NeutralLanguage>en-AU</NeutralLanguage>` in its `.csproj`, which is
why the neutral file is the `en-AU` baseline.

The Core localization plumbing (not text — you never edit these as a translator) lives in
`src/StageFright.Core/Localization/`: `ILocalizer` / `Localizer` (the facade call sites use),
`MissingKeyLoggingLocalizerFactory` (logs every fallback), `EnumLocalizationExtensions`
(`LocalizeEnum()`), and `MoneyFormatter` (fixed-AUD money display). The runtime
language catalog and startup resolution live in `src/StageFright.Core/Modules/Localization/`
(`SupportedLanguagesCatalog`, `LanguageProvider`, `SupportedLanguage`).

---

## 3. How to add a language

### 3.1 Steps

1. Pick the **culture code** — a standard .NET culture name: language, or language-region.
   Examples: `fr` (French), `fr-CA` (Canadian French), `mi-NZ` (New Zealand Māori),
   `de-DE` (German). Region-specific is preferred when the number/date formatting differs.
2. For **every** neutral `.resx` in the table above, make a copy in the **same folder** named
   `<Marker>.<culture>.resx`. For French that is `SharedResource.fr-FR.resx`,
   `MembersResource.fr-FR.resx`, … `ReportsResource.fr-FR.resx` — twelve files.
3. In each copy, translate the **`<value>`** of every `<data name="…">` entry. Do **not** change
   the `name`, and keep the `<comment>` (it documents the placeholders — see §4.2).
4. Rebuild the solution (`dotnet build`). The build compiles each `.resx` into a satellite
   assembly next to the main one.
5. Launch the app. The new language now appears in **Settings → General → Display language** and
   in the **Setup Wizard language step**, listed by its own endonym (its
   `CultureInfo.NativeName`, e.g. *Français (France)*). Selecting it and restarting presents the
   whole app — text, dates, numbers, and the separators/placement of money amounts — in that
   language and region.

That is the whole process. There is no supported-languages list, no registration call, and no
screen or business-logic code to touch (**SC-003**). A maintainer's only involvement is code
review of the `.resx` files in the pull request.

### 3.2 Partial languages are fine

A `<culture>.resx` set does **not** have to be complete to ship. Any key you leave out (or leave
as the English value) resolves — for that one key — to the Australian English baseline, and the
fallback is written to the log as a `Warning` (`Missing localization key …`) so gaps are easy to
find and fill later (**FR-008 / FR-009 / SC-004**). A partial set still counts as "the app ships
this language" for the purpose of matching the operating-system language on first run.

### 3.3 What a *complete* language means — checklist

* [ ] All twelve `<Marker>.<culture>.resx` files exist, in the correct folders.
* [ ] Every `<data name>` from the neutral file is present and translated.
* [ ] No key was renamed or removed; no key exists that the neutral file doesn't have.
* [ ] Every `{Named}` placeholder from the neutral value appears in the translation (order may
      differ — that is the point).
* [ ] Every plural pair (`…_One` / `…_Other`) is translated as a pair, with the count wording
      correct for the language.
* [ ] Enum values (`Enum_*` keys in `EnumsResource`) are all translated.
* [ ] The app runs in the language with **no** `Missing localization key` warnings in the log.

---

## 4. The rules a resource key obeys

Full contract: `specs/027-localization-support/contracts/resource-key-catalog.md`. The parts a
translator needs:

### 4.1 Key shape — `Area_Context_Meaning`

`PascalCase` segments joined by `_`, e.g. `Members_List_Title`, `Shared_Action_Save`,
`Validation_Member_FirstNameRequired`, `Reports_MemberList_ColumnJoinDate`. The key is
**wording-independent** — it never changes when the copy changes, so you translate against the
key, not against a matching English phrase. Role suffixes you will see:
`…Label`, `…Title`, `…Heading`, `…Placeholder`, `…HelpText`, `…Button`, `…Error`, `…Success`,
`…Column<Name>`, `…Required`, and the accessibility suffixes `…AriaLabel`, `…Alt`, `…Tooltip`
(screen-reader / tooltip text — translate these exactly like visible labels).

### 4.2 Placeholders — named, never bare numbers

Runtime values use **named** tokens: `{OrganisationName}`, `{Count}`, `{MemberName}`. Each token
is described in the entry's `<comment>`. You may **reorder** tokens to suit the language; you may
**not** drop one or invent a new one. Example — neutral:
`"{MemberName} joined on {JoinDate}."` → a valid French translation may read
`"{JoinDate} : arrivée de {MemberName}."`

### 4.3 Plurals — two keys, `_One` and `_Other`

Count-dependent wording is a pair of keys, e.g.
`Settings_General_AuditRetentionYears_One` = `"{Count} year"`,
`Settings_General_AuditRetentionYears_Other` = `"{Count} years"`.
Translate **both** halves. If the target language has more plural categories than English
handles here, put the most common form in `_Other`; the pairing guard only requires both halves
to exist and to use the same tokens.

### 4.4 Enumeration values — `Enum_<Type>_<Member>`

Every user-facing enum member is one key in `EnumsResource`, e.g. `Enum_MemberStatus_Active`,
`Enum_FeeType_Annual`, `Enum_TaxCode_Standard`, `Enum_Theme_Dark`. Translate the **`<value>`**
(the label a user sees). The `<Member>` part of the **key** and the value stored in the database,
used in GL, backups, sorting, and report-filter option *values* are the culture-invariant enum
identity and are never touched.

---

## 5. What is **not** translated

| Not translated | Why |
|---|---|
| User-entered data — member names, the organisation name, account names, event titles, notes. | It is the user's content, not app chrome (**FR-019**). |
| Log / diagnostic text (Serilog output). | Developer-facing; stays English (**FR-007**). |
| The currency symbol / code. | Money is always Australian dollars: `"$"` / `"AUD"` is **fixed** in every language. Only the decimal separator, digit grouping and symbol placement follow the region — handled by `MoneyFormatter`, not a resource string (**FR-015 / FR-016**). Never introduce a `.resx` entry for a currency symbol. |
| Enum storage tokens, report-filter option **values**, routes (`/members`), CSS classes, format tokens (`"N2"`, `"dd/MM/yyyy"`), `data-*` attributes, resource keys themselves. | Culture-invariant identities, not display text. |
| Decorative / empty accessibility attributes — `alt=""`, content inside `aria-hidden`. | They carry no translatable text (**FR-001**). |
| DataAnnotations `[Required(ErrorMessage = "…")]` / `ValidationResult` strings. | See §7 — a small, documented, permanent carve-out. |

---

## 6. The `qps-ploc` pseudo-locale (test only)

Beside each neutral file there is a `<Marker>.qps-ploc.resx`. This is a **pseudo-localised**
set — the English value bracketed and accented, e.g. `Save` → `⟦Šà133⟧` — generated by
`scripts/generate-pseudo-locale.py`, with about three keys deliberately omitted to exercise
per-key fallback. It exists to let the automated tests prove that switching languages and
falling back both work.

`qps-ploc` is **not a shipped language**: it is test-fixture content, and the runtime catalog
excludes any culture whose name starts with `qps-`, so it never appears in the Settings or Setup
pickers and never matches the operating-system language. Do not translate it by hand; if the
neutral files change, re-run the script.

---

## 7. Known carve-out — validation attribute messages

Twelve validation messages are declared as .NET **DataAnnotations attribute arguments**
(`[Required(ErrorMessage = "Organisation name is required.")]` and similar, in a handful of
Setup/Settings/Reconciliation form models). An attribute argument must be a compile-time
constant, so it cannot call the runtime resource lookup that every other string uses, and the
strongly-typed-`.resx`-designer alternative was rejected during design (it bypasses the
missing-key logging — research Decision 1). These messages therefore stay Australian English in
every language. This is a deliberate, final decision, not an oversight; the residual-literal
guard (`Us2LocalizationGuardTests`) explicitly exempts them and nothing else.

---

## 8. How it is enforced (for maintainers)

`tests/StageFright.Localization.Tests` fails the build if:

* a key is referenced in code with no neutral (`en-AU`) entry — **baseline completeness**;
* a `<culture>.resx` defines a key the neutral file doesn't — **no orphan keys**;
* a translation's `{Named}` token set differs from the neutral entry's — **placeholder parity**;
* a plural key is missing its `_One` / `_Other` partner — **plural pairing**;
* a user-facing enum member has no `Enum_<Type>_<Member>` key, or a screen renders an enum with
  `.ToString()` — **enum coverage / no raw enum display**;
* a user-facing literal remains anywhere on the app surface (all of `StageFright.UI` plus every
  user-facing exception message in `StageFright.Core`) — **residual-literal scan**, repo-wide
  since T060, with the §7 carve-out the only exception;
* a money amount is formatted with `"C"` / `{0:C}` / `FormatString="{0:C}"` — **no culture
  currency symbol**; use `MoneyFormatter`;
* a deliberately-omitted `qps-ploc` key does not fall back to `en-AU` with a logged `Warning` —
  **missing-key logging**.

---

## 9. Startup language resolution and the restart notice

At startup `LanguageProvider` resolves the display culture in this order (**FR-023 / SC-010**):

1. an **explicit** choice saved in `Settings.LanguageCode` that names a shipped language — always
   wins;
2. otherwise the **operating-system display language**, if the app ships a matching set — matched
   by exact culture first (`fr-CA`), then by parent language (`fr`), `qps-*` excluded;
3. otherwise **Australian English**.

`Settings.LanguageCode` is a nullable column (added by the `AddLanguageCodeToSettings`
migration). It stays `null` until the user picks a language explicitly, so an untouched install
keeps following the system language across OS changes, and an upgraded install behaves exactly as
before. A `LanguageCode` naming a language no longer shipped is treated as "no explicit choice"
and re-resolved.

Changing the language in Settings or the Setup Wizard **persists immediately** and shows an
inline "restart to finish switching" notice. The running app is not re-rendered in the new
language — the change takes effect on the next launch. In-session live switching is out of scope
for v1; the `CultureProvider` cascading component in `src/StageFright.UI/Layout/` is the seam a
later story would use to add it without touching call sites (**FR-021**).
