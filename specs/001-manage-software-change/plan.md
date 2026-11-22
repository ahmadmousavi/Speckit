# Implementation Plan: ChangeFlow Change Request Management

**Branch**: `001-manage-software-change` | **Date**: 2026-11-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-manage-software-change/spec.md`

## Summary

Implement ChangeFlow as a focused ASP.NET Core 10 Web API with MVC controllers and Entity Framework Core.
The system manages Draft, PendingReview, ChangesRequested, and Approved Change Requests with server-side
identity, current permissions, workflow validation, application-managed optimistic concurrency, and atomic
SQLite persistence. The initial implementation remains a single runnable API project.

## Technical Context

**Language/Version**: C# on .NET 10

**Primary Dependencies**: ASP.NET Core Web API; Entity Framework Core; Microsoft.Data.Sqlite; JWT bearer authentication

**Storage**: SQLite for local development and educational execution

**Testing**: .NET test framework with unit, integration, and API contract coverage

**Target Platform**: Local development on Windows/Linux and an ordinary HTTP host

**Project Type**: Web service / HTTP API

**Performance Goals**: No approved production-scale target; educational local and test workloads

**Constraints**: No frontend; no AI, RAG, MCP, or multi-agent functionality; minimal dependencies and project structure

**Scale/Scope**: One process and one small SQLite database; no distributed coordination

## Constitution Check

*GATE: Must pass before implementation.*

- **Simplicity**: PASS. A controller-based API and direct EF Core access remain appropriate for the current scale.
- **Workflow integrity**: PASS. State transitions, self-approval prevention, and immutability are server enforced.
- **Identity and separation of duties**: PASS. Authentication establishes a stable UUID subject; persisted permissions are checked.
- **Data integrity**: PASS. GUID concurrency tokens, EF Core update predicates, and explicit SQLite immediate transactions preserve consistency.
- **Testing**: PASS. Workflow, authorization, revocation, concurrency, and rejected-operation tests are required.
- **Traceability**: PASS. The plan references the approved specification and leaves product ambiguities visible.

Post-design re-check: no constitution violation is identified. SQLite, development JWT issuance, GUID concurrency,
and transaction coordination are implementation decisions documented here and in `research.md`.

## Project Structure

### Documentation

```text
specs/001-manage-software-change/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── openapi.yaml
└── spec.md
```

### Source Code

```text
src/
└── ChangeFlow.Api/
    ├── Controllers/
    ├── Data/
    ├── Models/
    └── Program.cs

tests/
└── ChangeFlow.Api.Tests/
    ├── Unit/
    ├── Integration/
    └── Contract/
```

**Structure Decision**: Single API project and one test project. No repository, unit-of-work, domain-framework, or extra library project is introduced.

## Architecture and Components

### Controllers

Controllers validate request shape, establish the authenticated caller, call workflow operations, and translate
malformed input, authorization, state, concurrency, and business-rule outcomes into problem responses.

### Workflow Logic

Workflow logic owns the approved state transitions and enforces ownership, permissions, self-approval,
mandatory non-empty change comments, Approved immutability, stale concurrency tokens, and replacement of
`LastChangeRequestComment` when RequestChanges is successful. It never overwrites `Reason` with a Reviewer comment.
The Requester may edit `Reason` through normal Draft or ChangesRequested edit operations when permitted.

### EF Core Data Layer

EF Core entities and `DbContext` are used directly. State-changing operations and coordinated permission changes
use an explicit Microsoft.Data.Sqlite immediate transaction.

## Persistence and Concurrency

SQLite does not provide SQL Server-style database-generated rowversion behavior. Each Change Request stores an
application-managed `ConcurrencyToken` GUID. Creation generates the initial GUID. Every successful mutation, including
an edit or state-only transition, generates a new GUID.

A mutation supplies the original token. EF Core performs an update predicate equivalent to:

```sql
UPDATE ChangeRequest
SET ..., ConcurrencyToken = @newToken
WHERE Id = @id AND ConcurrencyToken = @observedToken
```

If exactly one row is updated, the mutation succeeds and returns the new token. If no row is updated, the transaction
rolls back and returns `409 Conflict` with code `ConcurrencyConflict`. Concurrent callers with the same original token
cannot both succeed.

## SQLite Permission and Mutation Serialization

A normal EF Core `Database.BeginTransactionAsync()` call does not alone establish an immediate SQLite write transaction
for this design. Microsoft.Data.Sqlite's normal deferred behavior must not be assumed to acquire the writer reservation
before reads. The implementation will therefore open a `Microsoft.Data.Sqlite.SqliteConnection`, open it, and call
`SqliteConnection.BeginTransaction(deferred: false)` explicitly. This begins an immediate transaction and acquires SQLite's
writer reservation before the transaction reads permissions or request state. The resulting `SqliteTransaction` is attached
to the DbContext with `Database.UseTransaction(transaction)` so EF Core operations execute in that transaction.

Every state-changing operation and every coordinated permission revocation/restoration uses this same transaction pattern:
open, acquire the immediate transaction, re-read current permissions and request state, validate, perform the mutation,
commit, or roll back.

No separate `ChangeFlowMutationLock` table is required. The immediate transaction itself is the write serialization mechanism.

If revocation commits first, a later state-changing transaction acquires the writer reservation and sees `RevokedAt`, then
rejects. If a state-changing mutation commits first, a later revocation is ordered after the mutation and revocation cannot
retroactively cancel it. Operations made through this same database and coordinated transaction path are linearized by the
single SQLite writer. This guarantee does not extend to permission rows changed by an external database connection that
bypasses the coordinated transaction path.

For SQLite busy/locked outcomes, configure a bounded busy timeout and map transient `SQLiteBusy`/`SQLiteLocked` failures to a
retry of the entire state-changing or revocation transaction. Reopening and re-beginning a new immediate transaction must be
performed for each retry; re-executing only the failed SQL statement is not sufficient. A finite retry count is used, and a
terminal locked outcome returns an explicit conflict or retryable server problem without partial changes.

Permission revocation is not an API capability unless it is later added to the product specification. It is currently an
out-of-band administrative/database operation that must use the same coordinated transaction method described above.

## Identity and Permissions

`UserIdentity.Id` is a UUID string. It is the stable JWT `sub` value and matches `ChangeRequest.RequesterId`.
JWT subject parsing and JWT subject storage use UUID format consistently.

`Permission` uses `(UserId, Capability)` with one record per user/capability pair. A unique database index on
`(UserId, Capability)` prevents duplicates. Revocation sets `RevokedAt`; subsequent restoration clears `RevokedAt`
in the same record. No separate permission-history table is required.

A fresh Development database is deterministically seeded once on database initialization in idempotent Development-only seed code.
Stable seed UUIDs are: Requester-only `10000000-0000-4000-8000-000000000001`, Reviewer-only
`20000000-0000-4000-8000-000000000002`, and dual-role `30000000-0000-4000-8000-000000000003`. Seeding creates users and
active Requester/Reviewer permission rows only when absent. Tests may reuse these profiles or seed their own disposable database.

## Local Development Authentication

Only in Development, provide a minimal token-issuer endpoint unavailable outside Development. It accepts a seeded UUID
`userId`, verifies the development user, and returns a short-lived signed JWT. The JWT `sub` is the UUID string.

Signing keys, issuer, and audience come from `dotnet user-secrets` or environment variables. No signing key is source controlled.
No registration, passwords, refresh tokens, account recovery, or production identity provider is planned.

## Reviewer Change Comment

`ChangeRequest.LastChangeRequestComment` is nullable. `Reason` remains the Requester's reason and is not overwritten by
Reviewer comments. A normal Edit may update `Reason` for a permitted Draft or ChangesRequested edit. When a valid
RequestChanges operation supplies a non-empty comment, persist it as `LastChangeRequestComment` and generate a new token.
A later RequestChanges operation replaces the existing comment. No general audit-history feature is introduced.

## API and Error Contract

Create returns `201 Created`, a complete Change Request representation, and `Location: /change-requests/{id}`.

- `400 Bad Request`, code `MissingOrEmptyChangeComment`: missing, empty, or whitespace-only RequestChanges `comment`.
- `400 Bad Request`, code `MalformedInput`: malformed JSON or invalid/missing fields unrelated to the change comment.
- `401 Unauthorized`: missing or invalid authentication.
- `403 Forbidden`: caller lacks a required permission or violates self-approval separation of duties.
- `404 Not Found`: caller is not eligible to view or locate the resource.
- `409 Conflict`, code `InvalidState`: request is not in the required source state, including mutating an Approved request.
- `409 Conflict`, code `ConcurrencyConflict`: submitted concurrency token is stale.
- `500 Internal Server Error`, code `InternalError`: unexpected server or persistence failure.

All problem responses use application/problem+json and include a stable `code`, `title`, `status`, `detail`, and
`instance` where applicable. Mutation failures roll back request, comment, permission, and concurrency changes.

## View Authorization Scope

The approved scope remains: Requesters and authorized Reviewers relevant to the workflow may view requests. The exact
Reviewer visibility scope for states other than PendingReview is unresolved and requires a human product decision.
Implementation of view behavior for those states is blocked and must not be inferred.

## Testing Approach

Tests cover ownership, role profiles, self-approval, comment replacement, comment validation, stale GUID tokens,
permission revocation ordering, Approved invalid-state mapping, unauthorized viewing/modification, and rollback.
Contract tests verify status, stable problem code, response fields, comment replacement, and Location header.

## Complexity Tracking

No constitution violation requires a complexity exception. The plan does not introduce repositories, generic
unit-of-work abstractions, domain-framework projects, or extra source projects.
