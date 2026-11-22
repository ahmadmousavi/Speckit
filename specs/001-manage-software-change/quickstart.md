# Quickstart Validation: ChangeFlow

This guide defines validation scenarios for the planned API.

## Prerequisites

- .NET 10 SDK
- SQLite and Microsoft.Data.Sqlite support
- Development JWT issuer configuration
- A development signing key from user-secrets or an environment variable
- A disposable local SQLite database

## Development Identity and Authentication

Use UUID string identities consistently as `UserIdentity.Id`, JWT `sub`, and `RequesterId`.
Development database initialization deterministically and idempotently seeds:

- Requester-only UUID `10000000-0000-4000-8000-000000000001` with active Requester permission.
- Reviewer-only UUID `20000000-0000-4000-8000-000000000002` with active Reviewer permission.
- Dual-role UUID `30000000-0000-4000-8000-000000000003` with active Requester and Reviewer permissions.

The Development-only issuer accepts one of these seeded UUID user IDs and returns a short-lived JWT whose `sub` is
that UUID. JWT role/permission claims are not trusted; the API loads active permission records.

## Run

```powershell
dotnet restore
dotnet ef database update --project src/ChangeFlow.Api
dotnet run --project src/ChangeFlow.Api
dotnet test tests/ChangeFlow.Api.Tests
```

## Core Success Scenarios

1. A seeded Requester-only user creates a Draft and receives an initial concurrency GUID.
2. The owner edits permitted fields, including `Reason` in an allowed edit state, and submits using the latest GUID.
3. A seeded Reviewer-only or dual-role user who is not the requester approves PendingReview.
4. An authorized Reviewer requests changes with a non-empty `comment`. `Reason` remains the Requester's value,
   `LastChangeRequestComment` is set, and a new concurrency GUID is returned.
5. A later RequestChanges operation replaces `LastChangeRequestComment` and returns a new GUID.
6. The original Requester edits permitted ChangesRequested fields, including `Reason`, then resubmits to PendingReview.
7. An eligible viewer receives `LastChangeRequestComment`; viewing never grants modification permission.

## Required Rejection Scenarios

- Requester approval of an owned request is rejected.
- A non-Reviewer cannot approve or request changes.
- Missing, empty, or whitespace-only `comment` returns `400` with `MissingOrEmptyChangeComment`; the request is unchanged.
- A mutation of an Approved request returns `409` with `InvalidState`.
- Unsupported state transitions return `409` with `InvalidState`.
- A Requester cannot edit PendingReview or Approved requests.
- Two mutations using the same original concurrency GUID cannot both succeed; the stale mutation returns `409 Conflict`
  with `ConcurrencyConflict`.
- Permission revocation performed through the coordinated immediate-transaction path before mutation recheck rejects the mutation.
- A mutation that commits first is ordered before a later coordinated revocation and may succeed.
- External permission changes outside the coordinated path are not guaranteed to be observed.
- SQLite busy/locked errors retry the entire transaction with a new immediate transaction; they do not retry only one SQL statement.
- Rejected operations leave persisted request, comment, permission, and concurrency state unchanged.
- Draft deletion and Approved revocation/reopening are rejected.

## Visibility Block

Reviewer visibility outside PendingReview is blocked pending a human product decision. Tests must not add or infer
Reviewer visibility rules for those states. Eligible viewers of an approved view receive all details,
including `LastChangeRequestComment`.

## Validation Evidence

A passing validation run must include test command output and HTTP contract test results. Compilation alone is not
validation evidence. Test and build status must only be reported after the relevant commands execute and pass.
