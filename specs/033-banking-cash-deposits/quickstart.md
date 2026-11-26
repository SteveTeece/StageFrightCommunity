# Quickstart: Validating the Banking Rework (spec 033)

This guide shows how to prove the feature works. The design is in [data-model.md](./data-model.md) and [contracts/ui-contract.md](./contracts/ui-contract.md). The guide shows no implementation code.

## Prerequisites

- .NET 10 SDK with the MAUI workload. Run from the repo root on branch `033-banking-cash-deposits`.
- `dotnet build` (a full rebuild, `dotnet build -t:Rebuild`, when judging warnings, per CLAUDE.md).

## 1. Automated suite

```bash
dotnet test
# focused runs while iterating
dotnet test --filter "FullyQualifiedName~ReceiptDestinationResolver"
dotnet test --filter "FullyQualifiedName~BankDepositService"
dotnet test --filter "FullyQualifiedName~CashWithdrawalService"
dotnet test --filter "FullyQualifiedName~BankingQueryService"
dotnet test --filter "FullyQualifiedName~Backup"
dotnet test tests/StageFright.Localization.Tests/
```

The suite must pass in full, and these scenarios must each have a test:

| # | Scenario | Expected |
|---|---|---|
| A1 | Member payment, for each method (Cash, Cheque, Other, Card, EFT) | Debit lines on 1100 / 1105 / 1105 / bank / bank. A `ReceivedItem` only for Cheque and Other. The member balance falls. |
| A2 | Income, for each method; with and without an organisation; taxable under Inclusive and Exclusive | The same routing. The item amount = gross. `JournalEntry.PaymentMethod` is set. The organisation link is unchanged. |
| A3 | Card/EFT with no bank, with Petty Cash, with a non-bank or archived account | `ValidationException` / `EntityNotFoundException`, and nothing is written. |
| A4 | Deposit with items + cash; items only; cash only; nothing | Balanced DR bank / CR 1105 / CR 1100. `Validation_Deposit_Empty` for nothing. |
| A5 | Cash > min(balance on date, current balance) | Refused, and the message shows the lower figure. Cover both cases: the date balance lower, and the current balance lower. |
| A6 | Item already deposited (stale form); deposit date before receipt | Refused. The in-transaction re-check is also covered. |
| A7 | Reversal: success; already reversed; legacy; closed period; draft clearing; finalised clearing on the bank line and on the Petty Cash line | Balances restored and items awaiting again / refused with the right key / the draft line is removed. |
| A8 | Withdrawal: success; amount ≤ 0; no bank; Petty Cash as source | DR 1100 / CR bank, or refused. |
| A9 | Opening balance on 1105: positive / negative | One "Opening balance" item / refused. |
| A10 | General journal with a 1105 line; account list | Refused; 1105 is absent from the list. |
| A11 | Invariant: after each of A1–A9, `balance(1105)` = Σ awaiting | Holds. |
| A12 | Migration on a pre-033 database | Account `…0001` is named "Petty Cash" with an unchanged balance. `…0008` exists at 1105 with balance 0. No other row changed. |
| A13 | Backup round trip at 1.4.0 | Items, deposits, lines, reversal stamps and the journal `PaymentMethod` all restore exactly. |
| A14 | Restore 1.2.0 and 1.3.0 files | No error. "Petty Cash" keeps its name. Nothing awaiting. Report totals and organisation history totals are equal (SC-006). A 1.3.0 file keeps its organisation links. |
| A15 | Organisation history before and after deposit and reversal | Totals unchanged (FR-025). |
| A16 | bUnit: each Banking page; the payment and income forms' conditional fields and FR-004 order | Rendered per the contract §2, with select-all and the running totals. |

## 2. Manual check in the running app (Verification plan items 3–5)

```bash
dotnet run --project src/StageFright.App/
```

In a DEBUG build the seeder runs. The Banking screen should open with the last month's cheques awaiting (including the bowling-club sponsorship), a Petty Cash balance at about the float, and history showing deposits, floats and one *Reversed* deposit.

1. **Receive**: record a $50 member payment by cheque (reference "000123"), a $30 cash payment, and a $40 cheque income from an organisation. On *Finance → Banking*, both cheques are listed with their payer and reference. Undeposited Funds rose by $90, Petty Cash by $30, and no bank balance changed (SC-001).
2. **Deposit**: open *Record deposit*, tick the $50 cheque, enter $20 cash, and check the totals read Cash $20 / Cheques $50 / Total $70. Save. The bank rose by $70, Undeposited Funds fell by $50 and Petty Cash by $20. The $40 cheque is still awaiting. *Reports → Trial Balance* is balanced (SC-002).
3. **Reconcile**: start a reconciliation on the bank account. The deposit shows as one $70 line (SC-003). Tick it and leave the reconciliation as a draft.
4. **Reverse**: open the deposit from the history, choose *Reverse deposit*, and confirm. All three balances are back to their step-1 values. The $50 cheque is awaiting again. The draft reconciliation no longer has the line ticked. The history shows the deposit as Reversed (SC-005).
5. **Finalised block**: deposit again, reconcile and **finalise**, then try to reverse. The reversal is refused with the finalised-reconciliation message.
6. **Float**: record a $200 cash withdrawal ("Opening night float"). The bank falls by $200 and Petty Cash rises by $200. Then deposit $200 of cash back. Petty Cash ends where it started (verification item 4).
7. **Edge cases**: archive every bank account except Petty Cash. *Record deposit*, *Record cash withdrawal* and the Card/EFT bank picker then show the "add a bank account" notice, while Cash and Cheque receipts still save.
8. **Legacy route**: navigating to `/finance/bank-deposit` shows the Banking screen.
9. **Language and currency**: switch to each shipped language in *Settings → General*. Every Banking string is translated, and every amount uses the configured currency (verification item 5).

Report the build and test results (CLAUDE.md "Build & Test Verification") before marking the implementation done.
