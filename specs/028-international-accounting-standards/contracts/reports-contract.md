# Contract: Financial statements

Covers US3 statement integrity (FR-010, FR-011), US4 basis of accounting (FR-012), and US5 bank
reconciliation (FR-013…015). Consumers: `PdfReportRenderer`, `CsvReportExporter`,
`ReportViewer.razor`, and the financial-statement providers in `StageFright.Reports/Providers`.

---

## `ReportData.BasisOfAccounting` (new optional field)

`src/StageFright.Reports/Models/ReportData.cs`

```csharp
/// <summary>
/// Optional basis-of-accounting disclosure shown on financial statements. Null for
/// non-financial reports (Member List, Committee).
/// </summary>
public string? BasisOfAccounting { get; init; }
```

Rendering contract:

| Renderer | Placement |
|----------|-----------|
| `PdfReportRenderer` | Header column, one line below the "Generated: …" line, grey, small; only when non-null/non-empty. |
| `CsvReportExporter` | After the grand-total row: one record with the label in column 0 and the basis text in column 1, remaining columns empty; only when non-null. |
| `ReportViewer.razor` | A `<p class="text-muted small">` directly under the subtitle; only when non-null. |

Providers that MUST set it (FR-012), from the shared key `Reports_Common_BasisOfAccounting`:

* `IncomeStatementReportProvider`
* `BalanceSheetReportProvider`
* `TrialBalanceReportProvider`
* `TaxSummaryReportProvider`
* `AccountRegisterReportProvider`
* `GeneralLedgerReportProvider`
* `BankReconciliationReportProvider`
* `MemberAccountSummaryReportProvider`

`MemberListReportProvider` and `CommitteeReportProvider` leave it null.

The `Reports_Common_BasisOfAccounting` text must describe the hybrid basis accurately — member fees
recognised when levied (accrual); all other income and expenditure recognised when received or paid
(cash) — and must not claim a single blanket basis (FR-012).

---

## Trial Balance — exact debit/credit equality (FR-011)

`src/StageFright.Reports/Providers/TrialBalanceReportProvider.cs`

| Before | After |
|--------|-------|
| `if (Math.Abs(totalDebits - totalCredits) > 0.01m) throw new GLBalanceException(...)` | `if (totalDebits != totalCredits) throw new GLBalanceException(...)` |

* No tolerance band. A one-cent difference fails generation (SC-006).
* The thrown exception message key `Reports_TrialBalance_GLImbalanceError` is reworded to drop any
  "within tolerance" phrasing.
* The class doc-comment's stale `FR-034` reference is corrected.
* The viewer already catches `GLBalanceException` and offers "Try Again" — unchanged.

---

## Balance Sheet — explicit out-of-balance line (FR-010)

`src/StageFright.Reports/Providers/BalanceSheetReportProvider.cs`

After building the Asset, Liability and Equity sections and totals:

* Compute `difference = totalAssets - (totalLiabilities + totalEquity)`.
* If `difference != 0m`: append a bold row to the report using the new key
  `Reports_BalanceSheet_OutOfBalance` (label + `MoneyFormatter.Format(difference)`), rendered after
  the grand total. A clean statement is never produced when the sheet does not balance.
* If `difference == 0m`: unchanged output — total assets equal total liabilities plus equity and the
  statement renders normally (SC-005).

The Balance Sheet balances by construction today, so a non-zero difference indicates a real ledger
integrity fault; the surfaced amount is the diagnostic (research.md, Decision 5).

---

## Bank Reconciliation — conventional adjusted-balance layout (FR-013…015)

`src/StageFright.Reports/Providers/BankReconciliationReportProvider.cs` — `BuildAccountSectionsAsync`
is rewritten. For each bank/cash account's most recent reconciliation, at its statement date:

```
Balance per bank statement                         <StatementClosingBalance>
  Add: outstanding deposits (each listed)          <+ sum>
  Less: outstanding payments (each listed)         <- sum>
Adjusted bank balance                              <computed>
Balance per general ledger (as at statement date)  <GetAccountBalanceAsync(accountId, statementDate)>
Reconciled                                          <"in agreement" / difference>
```

Contract:

* Both `balance per bank statement` **and** `balance per general ledger` appear on every finalised
  reconciliation (FR-013, SC-008).
* Outstanding deposits and outstanding payments are summed and **carried into** the adjusted-bank-balance
  arithmetic, not merely listed (FR-014).
* `adjusted bank balance` and `balance per general ledger` are shown to be equal (a `Reconciled`
  line stating agreement, or the residual if any).
* Outstanding items come from `IGLRepository.GetUnreconciledByAccountAsync(accountId, statementDate)`;
  the ledger balance from `IGLRepository.GetAccountBalanceAsync(accountId, statementDate)` (both
  already exist).
* With no outstanding items, the report still shows both balances and proves agreement (Edge Cases).
* FR-015 (finalisation still requires the reconciliation to balance; a finalised reconciliation stays
  immutable) is already enforced in `BankReconciliationService` — covered by added tests, no
  behaviour change here.

New `ReportsResource` keys (+ `.en-US`, `.fr-FR`):
`Reports_BankReconciliation_BalancePerBankStatement`,
`Reports_BankReconciliation_AddOutstandingDeposits`,
`Reports_BankReconciliation_LessOutstandingPayments`,
`Reports_BankReconciliation_AdjustedBankBalance`,
`Reports_BankReconciliation_BalancePerGeneralLedger`,
`Reports_BankReconciliation_Reconciled`.

---

## Money formatting in reports

Every provider's private `FormatCurrency` helper (currently `amount.ToString("F2")`) routes through
`MoneyFormatter.Format` / `FormatWithCode` so amounts carry the configured currency symbol and
minor-unit precision (FR-003) and never a mismatched symbol/code (FR-004). See
currency-formatting-contract.md.

---

## Out of scope for this contract

The sales-tax internationalisation assessment (US10) may recommend reclassifying recoverable sales
tax (general-ledger accounts **`2310`** / **`2320`**) but this feature makes **no** change to tax
account classification or any stored tax value (FR-033). That work, if taken forward, is a follow-on
issue.
