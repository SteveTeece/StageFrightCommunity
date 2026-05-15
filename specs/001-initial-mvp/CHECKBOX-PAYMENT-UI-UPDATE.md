# Annual & Attendance Fee Payment Recording — Checkbox-Based UI Update

**Date**: February 4, 2026  
**Status**: ✅ Complete  
**Supersedes**: ANNUAL-FEE-PAYMENT-UPDATE.md (v1)  
**Files Modified**: 5 specification artifacts

---

## Summary

The annual and attendance fee payment process has been updated to use **checkbox-based batch payment recording** instead of form-based individual payments. This enables efficient bulk operations for recording payments from multiple members simultaneously.

**Key Changes**:
1. "Apply Annual Membership Fees" button applies to **ALL active members** (not selective)
2. Payment recording uses **MemberPaymentList.razor component** with checkboxes
3. Both **annual and attendance fee payments** recorded via same checkbox interface
4. **Batch payment recording** creates one Payment per selected member in atomic transaction

---

## Design Principles

### Application: ALL Active Members (Not Selective)

**Current (Old Design)**:
- Button applies fee individually per member (implied)

**New Design**:
- "Apply Annual Membership Fees" button applies fee to **EACH AND EVERY active member**
- No selection dialog; applies globally to all active members
- Sets AnnualFeeStatus = Unpaid for each member

---

### Payment Recording: Checkbox-Based Batch UI

**MemberPaymentList.razor Component**

**Display**:
- List of all members with outstanding fees (annual + attendance)
- Columns: Member Name | Annual Fee | Attendance Fees | Total Outstanding | Checkbox
- Sortable by name, outstanding amount
- Shows calculation: Annual + all unpaid RehearsalAttendance.FeeApplied

**User Interaction**:
1. User selects members via checkboxes (multiple selection)
2. Form above list:
   - Payment Method dropdown (Cash, Check, Card, BankTransfer, Other)
   - Date picker (defaults to today)
3. Button: "Record Selected Payments"

**Batch Recording**:
- For each selected member, creates ONE Payment transaction
- Amount = member's total outstanding balance (or pro-rata if custom amount UI added)
- Transactional: if any payment fails, entire batch fails and rolls back
- Standardized notes:
  - Annual fee: "Annual membership fee payment"
  - Attendance fee: "Attendance fees payment"
  - Both: "Annual membership fee payment" (primary) or aggregate note

**Service Methods Supporting This**:
- `RecordBatchAnnualFeePaymentsAsync(RecordBatchPaymentCommand)` — bulk annual fee recording
- `RecordBatchAttendanceFeePaymentsAsync(RecordBatchPaymentCommand)` — bulk attendance fee recording

---

### Attendance Fees: Automatic + Batch Payment

**How Attendance Fees Accrue**:
1. Rehearsal scheduled with AttendanceFee from Settings
2. Member marked present → RehearsalAttendance record created
3. RehearsalAttendance.FeeApplied = current Settings.AttendanceFee
4. Member balance increases by this amount

**How Attendance Fees Are Paid** (New):
- User opens MemberPaymentList in Financial → Payments section
- Selects members with unpaid attendance fees via checkboxes
- Chooses "Attendance" or "All Fees" (UI decision)
- Clicks "Record Selected Payments"
- System aggregates all unpaid RehearsalAttendance.FeeApplied for each selected member
- Creates one Payment per member with Note = "Attendance fees payment"
- Payment amount = sum of all unpaid attendance fees

---

### Default State: Unpaid

When annual fees applied via button:
- AnnualFeeStatus = Unpaid for each active member
- No automatic charging; explicit user action to record payment

---

## UI Components

### MemberPaymentList.razor

**Purpose**: Batch payment recording for annual and attendance fees via checkbox selection

**Display**:
```
┌─ Financial → Payments ─────────────────────────────┐
│                                                    │
│  [Payment Method: Dropdown]  [Date: DatePicker]    │
│  ┌──────────────────────────────────────────────┐  │
│  │ ☐ Member Name | Annual | Attendance | Total   │  │
│  ├──────────────────────────────────────────────┤  │
│  │ ☑ Alice       | $100   | $45        | $145    │  │
│  │ ☐ Bob         | $100   | $0         | $100    │  │
│  │ ☑ Carol       | $0     | $75        | $75     │  │
│  │ ☐ David       | $100   | $30        | $130    │  │
│  └──────────────────────────────────────────────┘  │
│  [Select All] [Deselect All] [Record Selected Payments] │
│                                                    │
└────────────────────────────────────────────────────┘
```

**Functionality**:
- Displays all members with any outstanding fees
- Columns sortable (Name, Annual, Attendance, Total)
- Checkboxes for multi-selection
- Payment Method: dropdown or radio buttons
- Date: defaults to today, changeable
- Button action: calls `RecordBatchAnnualFeePaymentsAsync` or `RecordBatchAttendanceFeePaymentsAsync`
- Shows loading state during batch recording
- Shows success/error toasts after operation

---

### OutstandingFeesReport.razor

**Purpose**: Report view of all members with unpaid fees

**Display**:
```
┌─ Financial → Reports → Outstanding Fees ──────────┐
│                                                    │
│  Filters: [Member Status: All/Active/Inactive]    │
│           [Fee Type: All/Annual/Attendance]        │
│                                                    │
│  ┌──────────────────────────────────────────────┐  │
│  │ Member Name | Status | Annual | Attendance |  │
│  ├──────────────────────────────────────────────┤  │
│  │ Alice       | Active | $100   | $45        │  │
│  │ Bob         | Active | $100   | $0         │  │
│  │ Carol       | Active | $0     | $75        │  │
│  │ David       | Inactive| $100   | $0        │  │
│  │ TOTAL       |        | $400   | $120       │  │
│  └──────────────────────────────────────────────┘  │
│                                                    │
└────────────────────────────────────────────────────┘
```

**Functionality**:
- Sortable by member, status, fee amounts
- Filterable by member status (Active/Inactive/All)
- Filterable by fee type (Annual/Attendance/Both)
- Shows breakdown of unpaid annual vs. attendance fees
- Summary totals at bottom
- Linked to MemberPaymentList for recording payments

---

## Service Interface Changes

### IFinancialService Methods

#### RecordBatchAnnualFeePaymentsAsync()
```csharp
/// <summary>
/// Records batch annual fee payments for multiple members.
/// Bulk payment recording for use case where checkboxes select members to pay.
/// Creates one Payment record per selected member with standardized notes.
/// </summary>
/// <remarks>
/// For each member in MemberIds list:
/// 1. Create Payment record with Notes = "Annual membership fee payment"
/// 2. Amount = member's outstanding balance
/// 3. All payments created in single transaction (atomic)
/// 4. If any fails, entire batch fails (rollback)
/// </remarks>
Task<IReadOnlyList<PaymentDto>> RecordBatchAnnualFeePaymentsAsync(
    RecordBatchPaymentCommand cmd);
```

#### RecordBatchAttendanceFeePaymentsAsync()
```csharp
/// <summary>
/// Records batch attendance fee payments for multiple members.
/// Creates one Payment per member aggregating all unpaid RehearsalAttendance fees.
/// </summary>
/// <remarks>
/// For each member in MemberIds list:
/// 1. Calculate total unpaid attendance fees from RehearsalAttendance
/// 2. Create Payment record with Notes = "Attendance fees payment"
/// 3. Amount = sum of all unpaid attendance fees
/// 4. All payments created in single transaction (atomic)
/// </remarks>
Task<IReadOnlyList<PaymentDto>> RecordBatchAttendanceFeePaymentsAsync(
    RecordBatchPaymentCommand cmd);
```

#### GetMembersWithOutstandingFeesAsync()
```csharp
/// <summary>
/// Gets all members with outstanding annual or attendance fees.
/// Used to populate MemberPaymentList and OutstandingFeesReport.
/// </summary>
Task<IReadOnlyList<MemberOutstandingFeeDto>> 
    GetMembersWithOutstandingFeesAsync();
```

### New Command DTO

```csharp
public record RecordBatchPaymentCommand(
    IReadOnlyList<Guid> MemberIds,     // Selected member IDs from checkboxes
    DateTime Date,                      // Payment date
    PaymentType Type,                   // Payment method
    string? Notes = null                // Optional notes (auto-filled with fee type)
);
```

---

## Data Relationships

```
Member (Active/Inactive)
├── AnnualFeeStatus (Unpaid/Paid/Exempted)
├── Balance = (Income - Payments + RehearsalAttendance Fees)
│
├── Income [Immutable]
│   └── Created by "Apply Annual Membership Fees" button
│       Amount = Settings.AnnualMembershipFee per active member
│
├── RehearsalAttendance [Soft Delete]
│   └── FeeApplied = Settings.AttendanceFee per rehearsal
│
└── Payment [Immutable, never deleted]
    ├── Created via MemberPaymentList checkbox selection
    ├── Multiple members can be selected; one Payment per member
    ├── Amount = total outstanding (annual + attendance) OR split by type
    ├── Notes: "Annual membership fee payment" or "Attendance fees payment"
    └── Type: Cash, Check, Card, BankTransfer, Other
```

---

## Workflow Examples

### Example 1: Apply Annual Fees to All Active Members

**Initial State**:
- Alice (Active), Bob (Active), Carol (Inactive) — all have no annual fee applied

**Step 1**: User clicks "Apply Annual Membership Fees" on Settings page
```
For each active member:
  - Create Income record: amount=$100, category=MembershipFees
  - Set AnnualFeeStatus = Unpaid

Result:
  - Alice: AnnualFeeStatus=Unpaid, Balance=$100
  - Bob: AnnualFeeStatus=Unpaid, Balance=$100
  - Carol: (unchanged, inactive)
```

### Example 2: Record Annual Fee Payments via Checkbox Batch

**Initial State**:
- Alice: $100 unpaid annual, $45 unpaid attendance = $145 total outstanding
- Bob: $100 unpaid annual, $0 attendance = $100 total outstanding
- Carol (Inactive): $0 annual, $75 unpaid attendance = $75 total outstanding

**Step 1**: User opens Financial → Payments, sees MemberPaymentList
```
Display:
  ☐ Alice    | $100 | $45 | $145
  ☐ Bob      | $100 | $0  | $100
  ☐ Carol    | $0   | $75 | $75
```

**Step 2**: User selects Alice and Bob via checkboxes
```
  ☑ Alice    | $100 | $45 | $145
  ☑ Bob      | $100 | $0  | $100
  ☐ Carol    | $0   | $75 | $75

Payment Method: [Cash ▼]  Date: [2026-02-04]
[Record Selected Payments]
```

**Step 3**: User clicks "Record Selected Payments"
```
System executes:
  RecordBatchAnnualFeePaymentsAsync({
    MemberIds: [Alice.Id, Bob.Id],
    Date: 2026-02-04,
    Type: Cash,
    Notes: "Annual membership fee payment"
  })

Creates:
  - Payment for Alice: amount=$100, type=Cash, notes="Annual membership fee payment"
  - Payment for Bob: amount=$100, type=Cash, notes="Annual membership fee payment"

Results:
  - Alice: Balance=$45 (remaining attendance fees)
  - Bob: Balance=$0 (fully paid)
  - AnnualFeeStatus for Alice & Bob optionally updates to Paid (Phase 7)
```

### Example 3: Record Attendance Fee Payments

**Initial State**:
- Alice: $0 unpaid annual, $45 unpaid attendance
- Carol: $0 unpaid annual, $75 unpaid attendance

**Step 1**: User selects only Alice and Carol for attendance fee payment
```
  ☑ Alice    | $0   | $45 | $45
  ☐ Bob      | $100 | $0  | $100
  ☑ Carol    | $0   | $75 | $75

(Switch payment type context: "Record Attendance Fees")
Payment Method: [Check ▼]  Check #: [12345]
[Record Selected Payments]
```

**Step 2**: System creates attendance fee payments
```
System executes:
  RecordBatchAttendanceFeePaymentsAsync({
    MemberIds: [Alice.Id, Carol.Id],
    Date: 2026-02-04,
    Type: Check,
    Notes: "Attendance fees payment"
  })

Calculates:
  - Alice total attendance fees: $45 (sum of all unpaid RehearsalAttendance.FeeApplied)
  - Carol total attendance fees: $75

Creates:
  - Payment for Alice: amount=$45, type=Check, notes="Attendance fees payment"
  - Payment for Carol: amount=$75, type=Check, notes="Attendance fees payment"

Results:
  - Alice: Balance=$0 (fully paid)
  - Carol: Balance=$0 (fully paid)
```

---

## Key Implementation Decisions

| Decision | Rationale | Impact |
|----------|-----------|--------|
| **Apply to ALL active members** | Eliminates selective application errors; fair distribution | Requires clear warning dialog |
| **Checkbox batch UI** | Faster than one-form-per-member; reduces UI interactions | Requires good UX (sortable, filterable) |
| **No explicit Income↔Payment linkage** | Simplifies balance calc; works for any fee combination | Must document in code |
| **Transactional batch recording** | All-or-nothing consistency; no partial failures | Need good error handling and rollback |
| **Aggregated attendance fees** | One Payment covers all unpaid attendance across rehearsals | Cleaner than multiple payments per rehearsal |
| **Standardized note format** | Enables reporting on fee type without complex logic | Document required note values |

---

## Testing Checklist

- [ ] "Apply Annual Membership Fees" button applies to ALL active members, not just one
- [ ] Button creates Income record for each active member only (not inactive)
- [ ] MemberPaymentList displays all members with any outstanding fees
- [ ] Checkbox selection works for multiple members
- [ ] Payment Method and Date controls work correctly
- [ ] RecordBatchAnnualFeePaymentsAsync creates one Payment per selected member
- [ ] RecordBatchAttendanceFeePaymentsAsync aggregates attendance fees correctly
- [ ] Batch payment fails atomically (if one fails, all roll back)
- [ ] Standardized note format applied correctly based on fee type
- [ ] OutstandingFeesReport shows accurate annual vs. attendance fee breakdown
- [ ] MemberPaymentList is sortable and filterable
- [ ] Inactive members shown in list but noted as Inactive (for reference)
- [ ] Balance recalculates correctly after batch payment recording

---

## Next Steps for Implementation

1. **Phase 6 (Financial Management)**: Update implementation
   - T114 (FinancialService): Update to use batch methods
   - T114b-c: Implement RecordBatchAnnualFeePaymentsAsync and RecordBatchAttendanceFeePaymentsAsync
   - T132a: Change from AnnualFeePaymentForm to MemberPaymentList.razor with checkboxes
   - T132b: Update to OutstandingFeesReport.razor

2. **Settings Page Update**:
   - T121: Verify "Apply Annual Membership Fees" button applies to ALL active members

3. **Error Handling**:
   - Confirmation dialog before batch payment recording
   - Rollback on failure with clear error messaging
   - Transaction logging for audit trail

---

**Status**: ✅ Specification updated. Ready for checkbox-based UI implementation.
