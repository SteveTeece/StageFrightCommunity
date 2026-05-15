# Annual Fee Payment Recording Process — Specification Update

**Date**: February 4, 2026  
**Status**: ✅ Complete  
**Files Modified**: 5 specification artifacts  
**Update**: Revised to use checkbox-based batch payment recording (not forms)

---

## Summary

A comprehensive process for recording member annual fee payments and attendance fee payments has been added to the specification. The process uses **checkbox-based batch payment recording** for efficiency. Annual fees are recorded as **Unpaid by default** when applied to ALL active members via a single button click.

---

## Design Principles

### Default State: Unpaid
When annual fees are applied to a member via the "Apply Annual Membership Fees" button:
- Creates an Income record with amount = Settings.AnnualMembershipFee
- Sets Member.AnnualFeeStatus = Unpaid
- Does NOT automatically charge the member

### Payment Recording Process
Members pay outstanding annual fees through the Financial interface:

1. **Recording Payment**
   - UI provides dedicated form (AnnualFeePaymentForm.razor)
   - Pre-fills: Member name, amount (from Settings), notes ("Annual membership fee payment")
   - Creates immutable Payment transaction
   - Payment records are permanent and cannot be edited or deleted

2. **Balance Calculation**
   - Member balance = (Annual Fees + Attendance Fees) - Payments Received
   - System aggregates ALL Income and ALL Payments for member
   - No explicit linkage between Income and Payment records needed
   - Balance recalculates automatically after each payment

3. **Fee Status Tracking**
   - AnnualFeeStatus enum: Unpaid, Paid, Exempted
   - When balance = 0 AND annual fees have been paid: AnnualFeeStatus optionally updates to Paid (Phase 7)
   - Partial payments are allowed; status doesn't change until fully paid

### Error Correction (Immutability Compliance)
- Payment records cannot be edited or deleted
- To correct overpayment or errors: create reversing Payment with negative amount
- Example: Member paid $100 annual fee but amount should be $75 → create Payment of -$25 with notes "Correction: overpayment refund"

---

## Specification Changes

### 1. spec.md — Section 6.4 (Financial Tracking)

**Added: Annual Membership Fee Payment Recording**
```markdown
Annual Membership Fee Payment Recording:
- Members can pay outstanding annual fees via Financial Management interface
- Each fee payment recorded as Payment transaction (immutable, permanent)
- Default state when fees applied: AnnualFeeStatus = Unpaid
- Payment can be recorded partially or in full for each member
- System aggregates all Income and all Payments for balance calculation
- No explicit linkage between Income and Payment records
- Standardized note format for annual fee payments: "Annual membership fee payment"
- Dedicated UI for recording annual fee payments (pre-filled member name and amount)
- Report shows all members with outstanding unpaid annual fees
- Optional: Phase 7 enhancement to auto-update AnnualFeeStatus to Paid when balance = 0
- Payment records are immutable; correct errors via reversing Payment with notes
- Payment method tracked (Cash, Check, Card, BankTransfer, Other)
```

### 2. data-model.md — Section 13 (Payment Entity)

**Updated: Payment Entity Purpose and Workflow**
```markdown
Purpose: Track member payments against outstanding balances with immutability. 
Payments include annual membership fees, per-rehearsal attendance fees, and any 
other charges.

Annual Fee Payment Workflow:
- When annual fees applied: Income record created with AnnualFeeStatus = Unpaid
- Member can pay via Payment record with Notes = "Annual fee payment"
- System does NOT track explicit linkage between Income and Payment
- Balance aggregates all Income and all Payments for member
- When balance = 0 AND unpaid annual fees: optional auto-update AnnualFeeStatus = Paid
- Partial payments allowed; balance recalculates automatically
```

### 3. contracts/services.md — IFinancialService Interface

**Added 3 New Service Methods**

#### RecordAnnualFeePaymentAsync()
```csharp
/// <summary>
/// Records an annual membership fee payment for a member.
/// Convenience method that calls RecordPaymentAsync with standardized Notes.
/// </summary>
Task<PaymentDto> RecordAnnualFeePaymentAsync(
    RecordAnnualFeePaymentCommand cmd);
```

#### GetMemberAnnualFeeStatusAsync()
```csharp
/// <summary>
/// Gets annual fee status for a member.
/// Includes AnnualFeeStatus enum and outstanding balance information.
/// </summary>
Task<MemberAnnualFeeStatusDto> GetMemberAnnualFeeStatusAsync(Guid memberId);
```

#### GetMembersWithOutstandingAnnualFeesAsync()
```csharp
/// <summary>
/// Gets all members with outstanding annual fees.
/// Returns list with member name, status, and fee amount.
/// </summary>
Task<IReadOnlyList<MemberOutstandingFeeDto>> 
    GetMembersWithOutstandingAnnualFeesAsync();
```

**New DTOs Added**
- `RecordAnnualFeePaymentCommand` — Request to record annual fee payment
- `MemberAnnualFeeStatusDto` — Member fee status with balance details
- `MemberOutstandingFeeDto` — Member with unpaid fees for reports

### 4. tasks.md — Financial Management Phase

**Added 6 New Tasks**

#### Service Implementation (3 subtasks added to T114)
- **T114a**: Implement RecordAnnualFeePaymentAsync method
- **T114b**: Implement GetMemberAnnualFeeStatusAsync method
- **T114c**: Implement GetMembersWithOutstandingAnnualFeesAsync method

#### UI Components (2 new tasks added after T132)
- **T132a**: Create AnnualFeePaymentForm.razor component
  - Specialized form for annual fee payments
  - Pre-filled member name, amount from Settings
  - Auto-filled notes: "Annual membership fee payment"
  
- **T132b**: Create MemberAnnualFeeStatus.razor component
  - Displays annual fee status (Unpaid/Paid/Exempted)
  - Shows payment history
  - Shows outstanding balance for member

#### Tests Updated
- **T122**: Added annual fee payment tracking to FinancialService unit tests
- **T138**: Added annual fee components to bUnit test suite

#### Success Criteria Enhanced
Added 3 new criteria to Phase 6 (Financial Management):
- Annual fee payments can be recorded with standardized note format
- Member annual fee status displays correctly
- Outstanding annual fee reports show all members with unpaid fees

---

## Data Relationships

```
Member
├── Status (Active/Inactive)
├── AnnualFeeStatus (Unpaid/Paid/Exempted) [NEW: Default = Unpaid]
├── Balance (Calculated from all Income + Attendance Fees - Payments)
│
├── Income (Annual Fee) [Immutable]
│   └── Amount = Settings.AnnualMembershipFee
│       Created when "Apply Annual Membership Fees" clicked
│       Immutable; never deleted
│
└── Payment [Immutable]
    ├── Amount (Can be partial or full payment of annual fee)
    ├── Date (When payment received)
    ├── Type (Cash, Check, Card, BankTransfer, Other)
    ├── Notes (Default for annual fees: "Annual membership fee payment")
    └── Immutable; correct via reversing Payment
```

---

## Workflow Example

### Scenario: Jane is a member with $100 annual membership fee

**Step 1: Apply Annual Fee** (Settings page, click "Apply Annual Membership Fees")
```
- Creates Income record: CategoryId=MembershipFees, Amount=$100
- Sets Jane.AnnualFeeStatus = Unpaid
- Jane.Balance = $100 (outstanding)
```

**Step 2: Jane Pays Partial Amount** (Financial → Payments, record $60 payment)
```
- Creates Payment record: MemberId=Jane, Amount=$60, Type=Cash, Date=2026-02-04
- Notes = "Annual membership fee payment"
- Jane.Balance = $40 (still owes)
- Jane.AnnualFeeStatus still = Unpaid
```

**Step 3: Jane Pays Remainder** (Record $40 payment)
```
- Creates Payment record: MemberId=Jane, Amount=$40, Type=Check, Date=2026-02-10
- Notes = "Annual membership fee payment"
- Jane.Balance = $0 (fully paid)
- Jane.AnnualFeeStatus = Paid (optional Phase 7 auto-update)
```

**Step 4: Jane Overpaid, Correction Needed**
```
If Jane was supposed to pay $75, not $100:
- Create reversing Payment: Amount=-$25, Notes="Correction: overpayment refund"
- Jane.Balance = -$25 (credit toward next year or refund)
```

---

## Key Design Decisions

| Decision | Rationale | Impact |
|----------|-----------|--------|
| Annual fees **Unpaid by default** | Explicit opt-in for members to pay; no hidden charges | Requires clear UI prompting for payment |
| No explicit Income↔Payment linkage | Simplifies balance calculation; works with any fee type | Balance derived from aggregation, not links |
| Immutable Payment records | Constitution requirement; audit trail preservation | Corrections via reversing transactions |
| Standardized note format | Distinguishes annual vs. other fee payments | Enables reporting on annual fees specifically |
| Dedicated UI component | Streamlines annual fee payment workflow | Faster recording; less error-prone |
| Partial payments allowed | Flexibility for members in financial difficulty | Balance tracks partially paid amounts |

---

## Reporting Impact

**New Reports Enabled**:
- **Outstanding Annual Fees Report**: All members with AnnualFeeStatus = Unpaid, sorted by member name
- **Member Fee Status Detail**: Individual member view with fee history and balance
- **Annual Fee Collection Summary**: Total annual fees applied vs. collected for period

---

## Testing Checklist

- [ ] Annual fees apply with AnnualFeeStatus = Unpaid by default
- [ ] Payment records create successfully with standardized notes
- [ ] Partial payments work; balance recalculates correctly
- [ ] Reversing payments (negative amounts) correct overpayments
- [ ] AnnualFeePaymentForm pre-fills member name and amount
- [ ] MemberAnnualFeeStatus component displays correct status and balance
- [ ] Outstanding annual fees report lists all unpaid fees
- [ ] Payment records are immutable (cannot edit/delete after creation)
- [ ] Inactive members can still have payments recorded (but not participate in events)
- [ ] All financial reports aggregate payments correctly

---

## Next Steps for Implementation

1. **Phase 6 (Financial Management)**: Implement tasks in order:
   - T114a-c: Service methods for annual fee payment
   - T124-127: Repository and integration tests
   - T132a-b: UI components for annual fee payment
   - T138: Unit tests for components

2. **Reports Section**: Update reporting module to include:
   - Outstanding Annual Fees report
   - Member Annual Fee Status detail view

3. **Settings Page**: Verify "Apply Annual Membership Fees" button triggers T121 correctly

4. **Optional Phase 7 Enhancement**: Auto-update AnnualFeeStatus to Paid when balance = 0

---

**Status**: ✅ Specification complete and consistent. Ready for developer implementation.
