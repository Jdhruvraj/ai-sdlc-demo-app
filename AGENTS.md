# Project conventions for AI agents

Invoice approval demo app: ASP.NET Core 8 Web API with a small static web UI.

## Layout

- `backend/src/InvoiceApp.Domain`: business rules, no web or storage code. `ApprovalService` owns the invoice lifecycle (Draft, PendingApproval, Approved, Rejected).
- `backend/src/InvoiceApp.Api`: controllers, sign-in (`Auth/`), in-memory repository, and the web UI under `wwwroot/`.
- `backend/tests/InvoiceApp.Tests`: xUnit tests, one test class per class under test, named `<ClassUnderTest>Tests`.
- `e2e`: Playwright end-to-end tests (`tests/`) and page objects (`pages/`).
- `docs/use-cases`: short briefs for features we may build.

## Conventions

- Controllers live in `Controllers/`, one per resource, route `api/[controller]`. Request bodies are `record` types declared inside the controller.
- Controllers inherit `ApiControllerBase` and use `Handle(...)` so service errors map to 404, 409 and 400.
- Every endpoint is behind sign-in. Restrict actions with `[RequireRole("...")]`: destructive actions are Admin only, approvals are Approver or Admin.
- Business rules go in the Domain project, never in controllers.
- Register services in `Program.cs`. Repositories are interfaces in Domain with an in-memory implementation in Api.
- Every new rule or endpoint needs tests. Use `FakeInvoiceRepository` in tests.
- Use clear names; no one-letter variables or `flag` parameters.
- End-to-end tests use page objects and role-based locators (`getByRole`, `getByLabel`), never CSS or XPath selectors.

## Commands

- Run the app: `dotnet run --project backend/src/InvoiceApp.Api` then open http://localhost:5000
- Run the tests: `dotnet test backend/InvoiceApp.sln`
- Run end-to-end tests: `cd e2e && npm install && npx playwright install chromium && npm test`

## Demo accounts (not real credentials)

- `demo.approver` / `ChangeMe123!` (role Approver)
- `demo.admin` / `ChangeMe123!` (role Admin)

## Never

- Commit real credentials, tokens or customer data.
- Add business logic to `Program.cs` or controllers.

## Approval Rules

The approval service enforces role-based amount limits:

| Role      | Max Amount    | Special Requirements                    |
|-----------|---------------|-----------------------------------------|
| Manager   | ≤ 50,000      | Single approver                         |
| Director  | ≤ 500,000     | Single approver                         |
| Any       | > 500,000     | Two distinct approvers; second MUST be Director |

### Critical Rules to Test
1. Manager cannot approve invoices above 50,000
2. Director cannot approve invoices above 500,000
3. Invoices above 500,000 require exactly two approvers
4. Second approver on high-value invoices MUST be a Director
5. Same user cannot appear as both approvers (even if roles allow it)
6. Rejected attempts: invoice status remains unchanged, clear error returned

## Test Commands

Run all tests: `dotnet test backend/InvoiceApp.sln`

Run mutation testing: `dotnet stryker run --project backend/src/InvoiceApp.Domain`

Target: >80% mutation score for ApprovalService
