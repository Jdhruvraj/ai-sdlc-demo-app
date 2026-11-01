# Invoice Approval Limits - Complete Requirement

## Business Context
The invoice approval service currently allows any approved user to approve invoices without amount-based restrictions. This introduces financial risk and requires role-based approval limits.

## Functional Requirements

### FR-1: Role-Based Approval Limits
- **Manager** role: Can approve invoices with Amount ≤ 50,000
- **Director** role: Can approve invoices with Amount ≤ 500,000
- **Any user**: Cannot approve invoices exceeding their role's limit

### FR-2: High-Value Invoice Approval (Amount > 500,000)
- Requires exactly two distinct approvers
- First approver: Any approved role (Manager or Director)
- Second approver: MUST be a Director
- Both approvals must be recorded before invoice is marked Approved

### FR-3: Approver Uniqueness
- Same user cannot appear as both approvers on the same invoice
- Even if roles would otherwise allow it, uniqueness constraint applies

### FR-4: Rejection Handling
- Failed approval attempts return HTTP 400 with clear error message
- Invoice status MUST NOT change on failed attempts
- No partial state updates allowed

## Non-Functional Requirements

- **Performance**: Approval decision under 100ms
- **Audit Trail**: All approval/rejection actions logged with timestamp, approver, and reason
- **Idempotency**: Duplicate approve calls should not cause errors or state changes

## Edge Cases to Handle

1. Invoice amount exactly at limit (50,000 for Manager, 500,000 for Director) - ALLOWED
2. Invoice amount just over limit - REJECTED with clear reason
3. First approver is Manager, second is also Manager on >500k invoice - REJECTED
4. User attempts to approve >500k invoice as sole approver - REJECTED
