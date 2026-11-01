# Approval Rules Test Specification

## Test Categories

### TC-1: Manager Approval Limits
- **TC-1.1**: Manager approves invoice of 49,999 → SUCCESS
- **TC-1.2**: Manager approves invoice of 50,000 → SUCCESS (at limit)
- **TC-1.3**: Manager approves invoice of 50,001 → REJECTED with reason
- **TC-1.4**: Manager attempts to approve >500k invoice → REJECTED

### TC-2: Director Approval Limits
- **TC-2.1**: Director approves invoice of 499,999 → SUCCESS
- **TC-2.2**: Director approves invoice of 500,000 → SUCCESS (at limit)
- **TC-2.3**: Director approves invoice of 500,001 → REJECTED with reason
- **TC-2.4**: Director attempts to approve >1M invoice → REJECTED

### TC-3: Two-Approver Rule (>500k)
- **TC-3.1**: Invoice 501,000 approved by Manager + Director → SUCCESS
- **TC-3.2**: Invoice 501,000 approved by Manager + Manager → REJECTED
- **TC-3.3**: Invoice 501,000 approved by Director + Director → SUCCESS
- **TC-3.4**: Only first approver provided for >500k invoice → REJECTED

### TC-4: Approver Uniqueness
- **TC-4.1**: Same user attempts both approvals on 600k invoice → REJECTED

### TC-5: Rejection Behavior
- **TC-5.1**: Failed approval returns 400 status code
- **TC-5.2**: Failed approval includes clear error message mentioning limit
- **TC-5.3**: Invoice status remains PendingApproval after failed attempt

### TC-6: Status Guards
- **TC-6.1**: Approve on Draft invoice → REJECTED with clear reason
- **TC-6.2**: Approve on Approved invoice → REJECTED with clear reason
