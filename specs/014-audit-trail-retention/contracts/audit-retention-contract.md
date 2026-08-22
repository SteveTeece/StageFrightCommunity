# Contract: Audit Trail Retention Fix & Customization

No new routes or REST/CLI endpoints. This documents the interface and UI-identifier surface tests and `implement` code against.

## Service contracts

```csharp
// StageFright.Core.Contracts.IAuditTrailService (extended)
Task PurgeOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
```

```csharp
// StageFright.Core.Modules.Settings.SetupRequest (extended, positional record — appended last to preserve existing positional callers)
public record SetupRequest(
    string OrganizationName,
    string Abn,
    decimal AnnualFee,
    decimal AttendanceFee,
    int MembershipRenewalMonth,
    bool IsGstRegistered,
    GstCode? AnnualFeeGstCode,
    GstCode? AttendanceFeeGstCode,
    Theme Theme,
    int CommitteeRenewalMonth = 1,
    IReadOnlyList<string>? CommitteeOfficeHolderTitles = null,
    int? GeneralCommitteeSeatCountTarget = null,
    int AuditRetentionYears = 1);
```

Validation (both `SettingsService.SaveAsync` and `SetupService.Validate`): `AuditRetentionYears` outside `[1, 7]` throws `ValidationException("Audit retention period must be between 1 and 7 years.", "Settings", <caller method>)`.

## UI identifiers (bUnit tests code against these)

| Page | Element `id` | Control | Values |
|---|---|---|---|
| `SetupWizard.razor` (Step 2) | `auditRetentionYears` | `InputSelect` | 1–7, default 1 |
| `GeneralSettingsTab.razor` | `auditRetentionYears` | `InputSelect` | 1–7, current value |

Both render as `<option value="@n">@n year(s)</option>` for `n` in 1..7, matching this codebase's existing `InputSelect` loop style (e.g. the month dropdowns in `GeneralSettingsTab.razor`).
