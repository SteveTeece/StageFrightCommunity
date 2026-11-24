# Phase 1 Contract: External Organisations

This file lists the surfaces that callers and tests code against: routes, element ids, service and repository signatures, request and result shapes, finance-screen changes, backup members and localization keys. The feature adds no HTTP API and no CLI.

The spec has no Verbatim Constraints section. The user-visible English (en-AU) strings below come straight from the spec's acceptance scenarios and requirements: "Organisations", "Add organisation", "show archived", "Total paid", "Total received", "Organisation". Existing identifiers (`/members`, `#payee`, `RecordExpenseRequest`, `JournalEntryType.ExpensePayment`, `BackupSchema.CurrentSchemaVersion` and the rest) are copied from the current code unchanged. This feature renames no existing test-facing identifier.

---

## 1. Routes

| Route | Component | Notes |
|---|---|---|
| `/organisations` | `Pages/Organisations/OrganisationList.razor` | Top-level menu target (FR-001). |
| `/organisations/new` | `Pages/Organisations/OrganisationForm.razor` | Create mode. Saving goes to `/organisations/{Id}`. |
| `/organisations/{Id:guid}/edit` | `Pages/Organisations/OrganisationForm.razor` | Edit mode. Saving goes to `/organisations/{Id}`. Cancel goes back to the Details tab. |
| `/organisations/{Id:guid}` | `Pages/Organisations/OrganisationDetail.razor` | Details tab (§3.4). |
| `/organisations/{Id:guid}/contacts` | `Pages/Organisations/OrganisationContacts.razor` | Contacts tab (§3.5). Hosts `ContactEditor`. |
| `/organisations/{Id:guid}/history` | `Pages/Organisations/OrganisationHistoryPage.razor` | Financial history tab (§3.7). |
| `/finance` tab `record-expense` | `Pages/Finance/ExpensePaymentPage.razor` (also `@page "/finance/expenses"`) | Gains the organisation picker (§5). |
| `/finance` tab `record-income` | `Pages/Finance/RecordIncome.razor` | Gains the organisation picker (§5). |

Every new `.razor` has a paired `.razor.cs`, with no `@code` block.

The spec's "detail screen" is these three tabs, joined by the shared `OrganisationTabs` strip (§3.3). Each tab is its own page, so each user story owns its own file: US1 the Details tab, US2 the Contacts tab, US3 the History tab. Each tab loads the organisation itself through `IExternalOrganisationService.GetByIdAsync`. Each works for active and archived organisations, and each shows `L["Organisations_Detail_NotFound"]` for an unknown id, not an exception. Each renders the organisation's name as its `h1`, plus the `L["Organisations_Detail_ArchivedBadge"]` badge when archived, above the strip.

## 2. Navigation

`StageFright.Core/Modules/Organisations/OrganisationsMenuItemProvider.cs` implements `IMenuItemProvider`:

| Member | Value |
|---|---|
| `ModuleName` | `"Organisations"` |
| `DisplayOrder` | `2` |
| `GetMenuItems()` | one item: `Title = L["Nav_Organisations_Title"]`, `Route = "/organisations"`, `ShortLabel = L["Nav_Organisations_ShortLabel"]`, `DisplayOrder = 0`, no children |

Other providers' `DisplayOrder` after this change: Dashboard `0`, Members `1` (unchanged), **Organisations `2`**, Rehearsals `3`, Events `4`, Finance `5`, Reports `6`, Settings `999` (unchanged).

## 3. Screen contracts (test-facing ids)

Ids follow the existing camelCase convention (`#payee`, `#expenseDate`, `showInactiveSwitch`).

### 3.1 `OrganisationList` (`/organisations`)

| Element | Contract |
|---|---|
| `h1` | `L["Organisations_List_Heading"]` ("Organisations") |
| `#addOrganisationButton` | Opens `/organisations/new`. Label `L["Organisations_List_AddButton"]` ("Add organisation"). |
| `#organisationSearch` | `<input type="search">`, `@bind:event="oninput"`. Filters the rows on the page to names that contain the trimmed term, compared with `StringComparison.OrdinalIgnoreCase`. A blank term shows every row (FR-007). |
| `RadzenSwitch Name="showArchivedSwitch"` | Label `L["Organisations_List_ShowArchivedLabel"]` ("Show archived"). Off: active organisations only. On: active plus archived, and archived rows show the `L["Organisations_List_ArchivedBadge"]` badge. Uses `@bind-Value` and `Change`, per the toggle standard. |
| Grid | `RadzenDataGrid<ExternalOrganisation>` with `AllowSorting="true" AllowPaging="true" PageSize="15" class="rz-shadow-0"`. Columns: Name (`Organisations_List_ColName`), Email (`Organisations_List_ColEmail`), Contacts (`Organisations_List_ColContacts`, value `Contacts.Count`). Default sort is Name ascending. A row click opens `/organisations/{Id}`. |
| Empty register | `L["Organisations_List_Empty"]` plus the add button. When rows exist but none matches the search: `L["Organisations_List_NoMatch"]`. |

### 3.2 `OrganisationForm` (`/organisations/new`, `/organisations/{Id:guid}/edit`)

| Element | Binds to | Contract |
|---|---|---|
| `#organisationName` | `SaveExternalOrganisationRequest.Name` | `InputText`, required (marked `*`). |
| `#organisationAddress` | `.Address` | `InputTextArea`, `rows="4"`, multi-line. |
| `#organisationEmail` | `.Email` | `InputText type="email"`. |
| `#saveOrganisationButton` | — | Submits the form. |
| `#cancelOrganisationButton` | — | Leaves without saving. |
| `#organisationFormError` | — | `alert alert-danger`. Shows a service `ValidationException` message, such as the duplicate-name refusal (US1 scenario 5). |

Field-level messages (FR-004) follow the `MemberForm` pattern. Before calling the service, the page fills a `Dictionary<string, string> _errors` keyed by request property name (`"Name"`, `"Address"`, `"Email"`) from `ExternalOrganisationValidationService.GetErrors(request)` (§4.4). Each offending input gets `is-invalid` and a following `<div class="invalid-feedback">` holding the message. The service is not called while `_errors` is non-empty.

### 3.3 `OrganisationTabs` (shared strip)

| Parameter | Type | Contract |
|---|---|---|
| `Id` | `Guid` | Required. The organisation the links point at. |
| `Active` | `string` | Required. `"details"`, `"contacts"` or `"history"`. |

| Element | Contract |
|---|---|
| `<nav aria-label>` | `L["Organisations_Tabs_AriaLabel"]`. Holds a Bootstrap `nav nav-tabs` list. |
| `#organisationTabDetails` | Links to `/organisations/{Id}`. Label `L["Organisations_Tabs_Details"]`. |
| `#organisationTabContacts` | Links to `/organisations/{Id}/contacts`. Label `L["Organisations_Tabs_Contacts"]`. |
| `#organisationTabHistory` | Links to `/organisations/{Id}/history`. Label `L["Organisations_Tabs_History"]`. |

The link matching `Active` gets the `active` class and `aria-current="page"`. The strip loads no data.

### 3.4 `OrganisationDetail` (`/organisations/{Id:guid}`, Details tab)

| Element | Contract |
|---|---|
| `OrganisationTabs` | `Active="details"`. |
| Details card | Shows address (line breaks preserved) and email. |
| `#editOrganisationButton` | Opens `/organisations/{Id}/edit`. Shown only while active. |
| `#archiveOrganisationButton` | Shown while active. Calls `IExternalOrganisationService.ArchiveAsync`. |
| `#restoreOrganisationButton` | Shown while archived. Calls `RestoreAsync`. A `ValidationException` (name taken) shows in `#organisationDetailError`. |
| `#organisationDetailError` | `alert alert-danger` for service refusals. |

### 3.5 `OrganisationContacts` (`/organisations/{Id:guid}/contacts`, Contacts tab)

| Element | Contract |
|---|---|
| `OrganisationTabs` | `Active="contacts"`. |
| Contacts grid | `RadzenDataGrid<ExternalOrganisationContact>` over the organisation's `Contacts`, Members-grid attributes, `PageSize="15"`. Columns: Name, Role, Phone, Mobile, Email (`Organisations_Contacts_Col*`). Empty state: `L["Organisations_Contacts_Empty"]`. |
| `#addContactButton` | Opens an empty `ContactEditor`. Shown only while the organisation is active. |
| Per-row `#editContact-{ContactId}` / `#removeContact-{ContactId}` | Edit loads that contact into `ContactEditor`. Remove calls `IExternalOrganisationContactService.RemoveAsync`, then reloads. Both are shown only while the organisation is active. |
| `#organisationContactsError` | `alert alert-danger` for a refused remove. |

### 3.6 `ContactEditor` (inline on `OrganisationContacts`)

| Parameter | Type | Contract |
|---|---|---|
| `OrganisationId` | `Guid` | Required. |
| `Contact` | `ExternalOrganisationContact?` | Null means add, non-null means edit. |
| `OnSaved` | `EventCallback` | Raised after a successful add or update. The parent reloads the organisation. |
| `OnCancelled` | `EventCallback` | Raised by `#cancelContactButton`. |

| Element | Binds to |
|---|---|
| `#contactName` (required) | `SaveExternalOrganisationContactRequest.Name` |
| `#contactRole` | `.Role` |
| `#contactPhone` | `.Phone` (free text) |
| `#contactMobile` | `.Mobile` (free text) |
| `#contactEmail` | `.Email` (format-checked) |
| `#saveContactButton` | Submits. |
| `#contactEditorError` | `alert alert-danger` for service refusals. |

Field-level messages use the same `_errors` pattern as §3.2, keyed `"Name"`, `"Role"`, `"Phone"`, `"Mobile"`, `"Email"`.

### 3.7 `OrganisationHistoryPage` (`/organisations/{Id:guid}/history`, History tab)

The page loads the organisation through `IExternalOrganisationService.GetByIdAsync`, then the history through `IOrganisationHistoryService.GetHistoryAsync` (§4.9).

| Element | Contract |
|---|---|
| `OrganisationTabs` | `Active="history"`. |
| History grid | `RadzenDataGrid<OrganisationHistoryEntry>`, Members-grid attributes, `PageSize="15"`. Columns: Date, Description, Account, Amount, with Amount in a `<Template>` calling `MoneyFormatter.Format(...)` (FR-013). Rows arrive newest first. Empty state: `L["Organisations_History_Empty"]`. |
| `#totalPaid` | Label `L["Organisations_History_TotalPaid"]` ("Total paid"), value `MoneyFormatter.Format(history.TotalPaid)`. |
| `#totalReceived` | Label `L["Organisations_History_TotalReceived"]` ("Total received"), value `MoneyFormatter.Format(history.TotalReceived)`. |
| `#organisationHistoryError` | `alert alert-danger` showing `L["Organisations_Error_Unexpected"]` when loading the organisation or its history fails. The totals and grid are not shown. |

## 4. Core types and services

### 4.1 Entities (detailed in data-model.md)

`StageFright.Core/Entities/ExternalOrganisation.cs`: `Id`, `Name`, `Address?`, `Email?`, `IsDeleted`, `DeletedAt?`, `DeletedBy?`, `CreatedAt`, `UpdatedAt`, `List<ExternalOrganisationContact> Contacts`, `List<JournalEntry> JournalEntries`.

`StageFright.Core/Entities/ExternalOrganisationContact.cs`: `Id`, `ExternalOrganisationId`, `Name`, `Role?`, `Phone?`, `Mobile?`, `Email?`, `IsDeleted`, `DeletedAt?`, `DeletedBy?`, `CreatedAt`, `UpdatedAt`, `ExternalOrganisation ExternalOrganisation`.

`JournalEntry` (changed): adds `Guid? ExternalOrganisationId` and `ExternalOrganisation? ExternalOrganisation`. The value is set once, when the entry is created. No code path writes it afterwards.

### 4.2 Requests and results (`StageFright.Core/Modules/Organisations/`)

```csharp
public class SaveExternalOrganisationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Email { get; set; }
}

public class SaveExternalOrganisationContactRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
}

public sealed record OrganisationHistoryEntry(
    Guid JournalEntryId,
    DateTime Date,
    string? Description,
    string AccountName,
    JournalEntryType Type,
    decimal Amount);

public sealed record OrganisationHistory(
    IReadOnlyList<OrganisationHistoryEntry> Entries,
    decimal TotalPaid,
    decimal TotalReceived);
```

Every string the services store is trimmed, and a blank optional field is stored as `null`.

History mapping (FR-011, FR-012):

- `Amount` = Σ `Transaction.DebitAmount` over the entry's lines, which counts the bank-side movement once.
- `AccountName` = the `Account.Name` of the first line whose account is neither `IsBankAccount` nor `IsSystem` (the expense or income account the user chose).
- `Description` = `JournalEntry.Description`, which still carries the payee as it does today.
- `TotalPaid` = Σ `Amount` where `Type == JournalEntryType.ExpensePayment`. `TotalReceived` = Σ `Amount` where `Type == JournalEntryType.Income`.
- `Entries` are ordered by `Date` descending, then `CreatedAt` descending.

### 4.3 Validation rules

| Field | Rule | Message key (`ValidationResource`) |
|---|---|---|
| Organisation `Name` | required | `Validation_Organisation_NameRequired` |
| Organisation `Name` | ≤ 200 characters | `Validation_Organisation_NameMaxLength` |
| Organisation `Name` | not used by another active organisation (trimmed, `OrdinalIgnoreCase`) | `Validation_Organisation_NameDuplicate` |
| Organisation `Address` | ≤ 1000 characters | `Validation_Organisation_AddressMaxLength` |
| Organisation `Email` | ≤ 254 characters | `Validation_Organisation_EmailMaxLength` |
| Organisation `Email` | when present, matches `^[^@\s]+@[^@\s]+\.[^@\s]+$` (the `MemberValidationService` pattern) | `Validation_Organisation_EmailInvalid` |
| Contact `Name` | required | `Validation_OrganisationContact_NameRequired` |
| Contact `Name` | ≤ 200 characters | `Validation_OrganisationContact_NameMaxLength` |
| Contact `Role` | ≤ 100 characters | `Validation_OrganisationContact_RoleMaxLength` |
| Contact `Phone`, `Mobile` | ≤ 50 characters each, free text | `Validation_OrganisationContact_PhoneMaxLength`, `Validation_OrganisationContact_MobileMaxLength` |
| Contact `Email` | ≤ 254 characters, and the same format rule as above when present | `Validation_OrganisationContact_EmailMaxLength`, `Validation_OrganisationContact_EmailInvalid` |
| Any write to an archived organisation or its contacts | refused | `Validation_Organisation_Archived` |
| Finance posting with an archived organisation | refused | `Validation_Organisation_ArchivedNotSelectable` |

Length limits match the EF configuration (data-model.md), so the database never refuses what validation allowed.

### 4.4 `ExternalOrganisationValidationService` (`Modules/Organisations/`, new)

This is the one owner of the format and length rules above. Both the form pages (§3.2, §3.6) and the services use it, so the rule text exists in one place. It mirrors `MemberValidationService` and adds the per-field shape FR-004 needs.

```csharp
public partial class ExternalOrganisationValidationService
{
    public ExternalOrganisationValidationService(IStringLocalizer<ValidationResource> localizer);

    // Keyed by request property name; empty when valid. Excludes the duplicate-name rule (needs the repository).
    public IReadOnlyDictionary<string, string> GetErrors(SaveExternalOrganisationRequest request);
    public IReadOnlyDictionary<string, string> GetErrors(SaveExternalOrganisationContactRequest request);
}
```

The services call `GetErrors` and, when it returns errors, throw a `ValidationException` carrying the first message.

### 4.5 `IExternalOrganisationService` (`StageFright.Core/Contracts/`)

```csharp
public interface IExternalOrganisationService
{
    /// Active organisations ordered by Name, each with its non-removed Contacts loaded.
    Task<IReadOnlyList<ExternalOrganisation>> GetActiveAsync(CancellationToken ct = default);

    /// Archived organisations ordered by Name, each with its non-removed Contacts loaded.
    Task<IReadOnlyList<ExternalOrganisation>> GetArchivedAsync(CancellationToken ct = default);

    /// Active or archived organisation with its non-removed Contacts, or null when the id is unknown.
    Task<ExternalOrganisation?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// Throws ValidationException on a rule failure (§4.3) or a duplicate active name. Audits Create.
    Task<ExternalOrganisation> CreateAsync(SaveExternalOrganisationRequest request, CancellationToken ct = default);

    /// Throws EntityNotFoundException for an unknown id, ValidationException on a rule failure,
    /// a duplicate active name (excluding itself), or an archived organisation. Audits Update.
    Task UpdateAsync(Guid id, SaveExternalOrganisationRequest request, CancellationToken ct = default);

    /// Soft delete. Contacts and linked journal entries are untouched. Audits Delete.
    Task ArchiveAsync(Guid id, CancellationToken ct = default);

    /// Throws ValidationException (Validation_Organisation_NameDuplicate) when an active
    /// organisation now uses the name. Audits Restore.
    Task RestoreAsync(Guid id, CancellationToken ct = default);
}
```

The financial history is not on this service. It lives on `IOrganisationHistoryService` (§4.9), so US3 owns it without editing US1's service.

### 4.6 `IExternalOrganisationContactService` (`StageFright.Core/Contracts/`)

```csharp
public interface IExternalOrganisationContactService
{
    /// Throws EntityNotFoundException for an unknown organisation, ValidationException on a rule
    /// failure or an archived organisation. Audits Create.
    Task<ExternalOrganisationContact> AddAsync(Guid organisationId, SaveExternalOrganisationContactRequest request, CancellationToken ct = default);

    /// Throws EntityNotFoundException for an unknown or removed contact, ValidationException on a
    /// rule failure or an archived organisation. Audits Update.
    Task UpdateAsync(Guid contactId, SaveExternalOrganisationContactRequest request, CancellationToken ct = default);

    /// Soft delete (FR-009). Throws EntityNotFoundException for an unknown or removed contact,
    /// ValidationException for an archived organisation. Audits Delete.
    Task RemoveAsync(Guid contactId, CancellationToken ct = default);
}
```

### 4.7 Audit trail (FR-014)

Each call goes through the existing `IAuditTrailService.LogAsync(entityType, entityId, action, ...)`:

| Operation | `entityType` | `action` |
|---|---|---|
| Create, update, archive, restore an organisation | `nameof(ExternalOrganisation)` | `AuditAction.Create` / `Update` / `Delete` / `Restore` |
| Add, edit, remove a contact | `nameof(ExternalOrganisationContact)` | `AuditAction.Create` / `Update` / `Delete` |

### 4.8 Repository contracts (`StageFright.Core/Contracts/`)

```csharp
public interface IExternalOrganisationRepository : ISoftDeletableRepository<ExternalOrganisation>
{
    /// Ignores the soft-delete filter on the organisation; Contacts holds only non-removed contacts.
    /// Null when the id is unknown.
    Task<ExternalOrganisation?> GetByIdIncludingArchivedAsync(Guid id, CancellationToken ct = default);
}

public interface IExternalOrganisationContactRepository : ISoftDeletableRepository<ExternalOrganisationContact>
{
}

// IJournalEntryRepository (changed): one new member
/// Entries linked to the organisation, with Transactions and each line's Account loaded,
/// ordered by Date then CreatedAt descending. Never filtered by the organisation's archived state.
Task<IReadOnlyList<JournalEntry>> GetByExternalOrganisationAsync(Guid externalOrganisationId, CancellationToken ct = default);
```

`GetAllAsync` and `GetArchivedAsync` on `IExternalOrganisationRepository` load `Contacts` filtered to non-removed contacts, including for archived organisations. Implementations inherit `DataAccessException` translation from `BaseRepository` / `SoftDeletableBaseRepository`.

### 4.9 `IOrganisationHistoryService` (`StageFright.Core/Contracts/`)

```csharp
public interface IOrganisationHistoryService
{
    /// Linked expense and income entries with totals, mapped per §4.2. Works for archived
    /// organisations. An unknown id has no linked entries, so it returns an empty history.
    Task<OrganisationHistory> GetHistoryAsync(Guid organisationId, CancellationToken ct = default);
}
```

`Modules/Organisations/OrganisationHistoryService.cs` implements it on top of `IJournalEntryRepository.GetByExternalOrganisationAsync` alone. It never checks that the organisation exists: the History tab does that through `IExternalOrganisationService.GetByIdAsync` before it asks for the history.

## 5. Finance screen and service changes

### 5.1 Requests

| Type | New member |
|---|---|
| `RecordExpenseRequest` | `public Guid? ExternalOrganisationId { get; set; }`: optional organisation paid. |
| `RecordIncomeRequest` | `public Guid? ExternalOrganisationId { get; set; }`: optional organisation the income came from. |
| `ExpensePaymentModel` (UI) | `public Guid? ExternalOrganisationId { get; set; }` |
| `RecordIncomeModel` (UI) | `public Guid? ExternalOrganisationId { get; set; }` |

### 5.2 `ExpensePaymentService.RecordExpenseAsync` / `IncomeEntryService.RecordIncomeAsync`

- When `ExternalOrganisationId` is null, the service behaves exactly as it does today: the same journal entry, lines, descriptions and audit entry (SC-004).
- When it is set, the service calls `IExternalOrganisationRepository.GetByIdIncludingArchivedAsync` before the transaction opens, next to the existing account checks. Null raises `EntityNotFoundException(nameof(ExternalOrganisation), id, nameof(RecordExpenseAsync))` (or `nameof(RecordIncomeAsync)`). An archived organisation raises `ValidationException` (`Validation_Organisation_ArchivedNotSelectable`).
- The new `JournalEntry` is created with `ExternalOrganisationId` set, inside the existing `ExecuteInTransactionAsync`. Amounts, accounts, tax lines and the closed-period guard are unchanged.
- Both services take `IExternalOrganisationRepository` by constructor injection. Finance never references `ExternalOrganisationService`.
- `IExpensePaymentService` and `IIncomeEntryService` signatures are unchanged.

### 5.3 Picker markup

| Screen | Element | Contract |
|---|---|---|
| Expense payment | `#expenseOrganisation` | `InputSelect` bound to `_form.ExternalOrganisationId`, placed above `#payee`. The first option is `L["Finance_Organisation_None"]` with a null value, followed by `IExternalOrganisationService.GetActiveAsync()` by name. Label `L["Finance_Organisation_Label"]` ("Organisation"). |
| Record income | `#incomeOrganisation` | The same, placed above `#description`. |

Payee fill (FR-010, US3 scenarios 6 and 7), on the expense screen only. When `ExternalOrganisationId` changes to a non-null value and `string.IsNullOrWhiteSpace(_form.Payee)`, set `_form.Payee` to that organisation's `Name`. A payee that is not blank is never overwritten. Clearing or changing the organisation never clears or rewrites a payee. `#payee` stays editable.

After a successful save, both screens reset `ExternalOrganisationId` to null along with the rest of the form. Bank deposit, general journal, member fee and member payment screens get no picker.

## 6. Backup (schema 1.3.0)

| Item | Change |
|---|---|
| `BackupSchema.CurrentSchemaVersion` | `"1.2.0"` → `"1.3.0"` |
| `BackupService` | Writes `SchemaVersion = BackupSchema.CurrentSchemaVersion`, replacing the `"1.2.0"` literal. |
| `BackupEnvelope` | `[ProtoMember(27)] public List<ExternalOrganisationBackupDto>? ExternalOrganisations { get; set; }` and `[ProtoMember(28)] public List<ExternalOrganisationContactBackupDto>? ExternalOrganisationContacts { get; set; }`. Both are added in 1.3.0 and are null in older files, normalised with `??= []`. |
| `EntityCounts` | New keys `"ExternalOrganisations"` and `"ExternalOrganisationContacts"`. |
| `JournalEntryBackupDto` | `[ProtoMember(6)] public Guid? ExternalOrganisationId { get; set; }` |
| `ExternalOrganisationBackupDto` | `[ProtoContract]`, members 1–9: `Id`, `Name`, `Address`, `Email`, `IsDeleted`, `DeletedAt`, `DeletedBy`, `CreatedAt`, `UpdatedAt`. |
| `ExternalOrganisationContactBackupDto` | `[ProtoContract]`, members 1–12: `Id`, `ExternalOrganisationId`, `Name`, `Role`, `Phone`, `Mobile`, `Email`, `IsDeleted`, `DeletedAt`, `DeletedBy`, `CreatedAt`, `UpdatedAt`. |
| `BackupSnapshot` | `IReadOnlyList<ExternalOrganisation> ExternalOrganisations` and `IReadOnlyList<ExternalOrganisationContact> ExternalOrganisationContacts`, each defaulting to `Array.Empty<>()`. |
| `BackupRepository` | Reads both sets with `IgnoreQueryFilters()`, so archived organisations and removed contacts are included. On restore it upserts organisations, then contacts, **before** `JournalEntries`. |

A 1.2.0 file restores with an empty register and every journal entry unlinked (FR-015). A 1.3.0 file is refused by a 1.2.0 app through the existing `BackupSchema.IsRestorable`.

## 7. Localization keys

All keys are added to the neutral `.resx` (en-AU) and translated into `de-DE`, `en-US`, `es-ES`, `fr-FR`, `it-IT`, `ja-JP` and `pl-PL` in the same change. `qps-ploc` is regenerated by `scripts/generate-pseudo-locale.py`.

**`NavigationResource`**: `Nav_Organisations_Title` ("Organisations"), `Nav_Organisations_ShortLabel`.

**`ValidationResource`**: every key in the §4.3 table.

**`FinanceResource`**: `Finance_Organisation_Label` ("Organisation"), `Finance_Organisation_None`.

**`OrganisationsResource`** (new area, `src/StageFright.UI/Resources/Strings/OrganisationsResource.cs` plus 9 `.resx` files; added to the `Us1`/`Us2` guard dictionaries):

| Group | Keys |
|---|---|
| List | `Organisations_List_PageTitle`, `Organisations_List_Heading` ("Organisations"), `Organisations_List_AddButton` ("Add organisation"), `Organisations_List_SearchPlaceholder`, `Organisations_List_SearchAriaLabel`, `Organisations_List_ShowArchivedLabel` ("Show archived"), `Organisations_List_ArchivedBadge`, `Organisations_List_ColName`, `Organisations_List_ColEmail`, `Organisations_List_ColContacts`, `Organisations_List_Empty`, `Organisations_List_NoMatch`, `Organisations_List_LoadingText` |
| Form | `Organisations_Form_PageTitleNew`, `Organisations_Form_PageTitleEdit`, `Organisations_Form_NameLabel`, `Organisations_Form_AddressLabel`, `Organisations_Form_EmailLabel`, `Organisations_Form_EmailHelp`, `Organisations_Form_SaveButton`, `Organisations_Form_CancelButton` |
| Tabs | `Organisations_Tabs_AriaLabel`, `Organisations_Tabs_Details`, `Organisations_Tabs_Contacts`, `Organisations_Tabs_History` |
| Detail | `Organisations_Detail_PageTitle`, `Organisations_Detail_NotFound`, `Organisations_Detail_EditButton`, `Organisations_Detail_ArchiveButton`, `Organisations_Detail_RestoreButton`, `Organisations_Detail_ArchivedBadge`, `Organisations_Detail_AddressLabel`, `Organisations_Detail_EmailLabel` |
| Contacts | `Organisations_Contacts_PageTitle`, `Organisations_Contacts_Heading`, `Organisations_Contacts_AddButton`, `Organisations_Contacts_ColName`, `Organisations_Contacts_ColRole`, `Organisations_Contacts_ColPhone`, `Organisations_Contacts_ColMobile`, `Organisations_Contacts_ColEmail`, `Organisations_Contacts_EditButton`, `Organisations_Contacts_RemoveButton`, `Organisations_Contacts_SaveButton`, `Organisations_Contacts_CancelButton`, `Organisations_Contacts_Empty` |
| History | `Organisations_History_PageTitle`, `Organisations_History_Heading`, `Organisations_History_ColDate`, `Organisations_History_ColDescription`, `Organisations_History_ColAccount`, `Organisations_History_ColAmount`, `Organisations_History_TotalPaid` ("Total paid"), `Organisations_History_TotalReceived` ("Total received"), `Organisations_History_Empty` |
| Shared | `Organisations_Error_Unexpected` |

## 8. DI registration (`MauiProgram`)

`IExternalOrganisationRepository` → `ExternalOrganisationRepository`, `IExternalOrganisationContactRepository` → `ExternalOrganisationContactRepository`, `IExternalOrganisationService` → `ExternalOrganisationService`, `IExternalOrganisationContactService` → `ExternalOrganisationContactService`, `IOrganisationHistoryService` → `OrganisationHistoryService`, and `ExternalOrganisationValidationService` (concrete, the same lifetime as `MemberValidationService`) are all scoped. `IMenuItemProvider` → `OrganisationsMenuItemProvider`.

All of these except the menu provider were registered with US3: once `ExpensePaymentService` and `IncomeEntryService` take `IExternalOrganisationRepository`, and the finance pages inject `IExternalOrganisationService`, the existing finance screens and the DEBUG seeder cannot resolve without them. The menu provider still lands last (T080/T082), so the Organisations screens stay unreachable from the nav until then.

## 9. Additions to plan.md

These additions fill gaps in plan.md's file list. Plan.md and data-model.md should pick them up:

- `Modules/Organisations/ExternalOrganisationValidationService.cs` (§4.4). FR-004 needs field-level messages, and the existing `MemberValidationService` only throws one message, so the form could not place it next to a field.
- The three detail tabs (§3.3–§3.7) and `IOrganisationHistoryService` (§4.9). One detail page would have been edited by all three stories. Splitting it lets each story own its own page and service.
- `IExternalOrganisationRepository.GetByIdIncludingArchivedAsync` (§4.8). The inherited `GetByIdAsync` uses `FindAsync`, which applies the soft-delete filter. It returns null for an archived organisation, so the service could not tell "archived" (`ValidationException`) from "unknown" (`EntityNotFoundException`), which research §6 requires.
