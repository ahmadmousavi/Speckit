# Research: ChangeFlow Technical Decisions

## Persistence

**Decision:** Use SQLite through Entity Framework Core for the initial educational API.

SQLite is easy to run locally on Windows/Linux, needs no separate database service, and supports EF Core transactions.
It is sufficient for the project's current scale.

**Alternatives considered:**

- SQL Server: strong operational support, but requires extra setup and tooling.
- PostgreSQL: strong production capabilities, but unnecessary local setup for this educational scope.
- In-memory storage: simplest, but does not validate persisted state or SQLite concurrency behavior.

**Setup:** one SQLite connection string, EF Core schema setup, and disposable test databases.

## Authentication

**Decision:** Use ASP.NET Core JWT bearer validation plus a minimal Development-only token issuer.

The issuer accepts a seeded development user UUID and returns a short-lived JWT. The JWT `sub` is the UUID string.
JWT role/permission claims are not authorization authority; the API reloads active permissions from the database.

The Development database is initialized with an idempotent seed for three stable UUID users:
Requester-only `10000000-0000-4000-8000-000000000001`, Reviewer-only
`20000000-0000-4000-8000-000000000002`, and dual-role `30000000-0000-4000-8000-000000000003`.
The seed creates users and active permission rows only when absent. Tests may reuse these identities.

Signing keys, issuer, and audience come from `dotnet user-secrets` or environment variables. No key is source-controlled.
No registration, passwords, refresh tokens, account recovery, or production identity provider is introduced.

## Identity and Permissions

**Decision:** use UUID strings consistently.

- `UserIdentity.Id`: UUID string.
- JWT `sub`: UUID string.
- `ChangeRequest.RequesterId`: UUID string.

A unique database index on `(UserId, Capability)` permits only one permission record per pair. Revocation updates
`RevokedAt`; restoration clears it. No permission-history table is used.

## Authorization and Permission Revocation

**Decision:** use an explicit immediate Microsoft.Data.Sqlite transaction for both state-changing operations and
coordinated permission revocations or restorations.

EF Core `Database.BeginTransactionAsync()` by itself is not selected as evidence that SQLite acquires a writer
reservation before reads. In Microsoft.Data.Sqlite, normal `BeginTransaction()` behavior is deferred unless
`deferred: false` is requested. The implementation will open `SqliteConnection.BeginTransaction(deferred: false)`
and attach the resulting `SqliteTransaction` to EF Core with `Database.UseTransaction(...)`. This begins an immediate
transaction and reserves the SQLite writer before permissions or request state are read.

Every state-changing operation and every permission change made by the coordinated database path uses this same pattern:
1. Open or reuse an application connection.
2. Begin immediate transaction.
3. Attach it to EF Core.
4. Load current permissions and request state.
5. Validate and mutate.
6. Commit or roll back.

No `ChangeFlowMutationLock` table or extra lock write is necessary.

Revocation is not an API capability unless a future product decision adds it. It is currently an out-of-band
administrative/database operation that must use the same coordinated immediate-transaction path. If revocation commits
first, a later mutation sees `RevokedAt` and rejects. If a mutation commits first, the later revocation is ordered after
it and cannot retroactively cancel the mutation. The guarantee applies only to permission changes performed through this
same database and coordinated transaction path. External writes that bypass this path are outside the guarantee.

For SQLite busy/locked failures, configure a bounded busy timeout of 5 seconds and retry the entire transaction with a new immediate transaction. Do not retry only the failed statement. Allow a maximum of 3 total attempts (the initial attempt plus 2 retries). A terminal locked outcome after 3 attempts returns an explicit retryable server or conflict problem and makes no partial changes.

## Concurrency

**Decision:** use an application-managed GUID/UUID `ConcurrencyToken` on each Change Request.

On create and every successful mutation, the API generates a new GUID. A mutation submits the observed GUID. EF Core
loads the entity after the immediate transaction is acquired and performs an update predicate equivalent to:

```sql
UPDATE ChangeRequest
SET ..., ConcurrencyToken = @newToken
WHERE Id = @id AND ConcurrencyToken = @observedToken
```

An affected-row count of exactly one succeeds. Zero affected rows means the submitted token is stale; the transaction
rolls back and returns `409 Conflict` with `ConcurrencyConflict`.

## Reviewer Comment and Requester Reason

The RequestChanges input property is `comment`. It must be server-trimmed for validation and rejected when missing,
empty, or whitespace-only with `400 MissingOrEmptyChangeComment`.

On success, persist the comment in nullable `LastChangeRequestComment` and replace it on later RequestChanges operations.
Do not overwrite `ChangeRequest.Reason` with a Reviewer comment. `Reason` is the Requester's original reason, but a
Requester may edit it through the normal Edit operation for states where the specification permits editing: Draft and
ChangesRequested.

## API Design

Use ASP.NET Core controllers and consistent `application/problem+json` responses.

- Successful creation: `201 Created` plus `Location: /change-requests/{id}`.
- Missing or whitespace-only `comment`: `400 Bad Request`, code `MissingOrEmptyChangeComment`.
- Malformed or invalid fields unrelated to the comment: `400 Bad Request`, code `MalformedInput`.
- Attempt to mutate an Approved request or another invalid source state: `409 Conflict`, code `InvalidState`.
- Stale concurrency token: `409 Conflict`, code `ConcurrencyConflict`.
- Authorization: `403 Forbidden`.
- Authentication: `401 Unauthorized`.
- Eligible-resource lookup failure: `404 NotFound`.
- Unexpected server failure: `500 Internal Server Error`, code `InternalError`.

## View Authorization Scope

Approved behavior permits Requesters and authorized Reviewers relevant to the workflow to view requests. The exact scope
for Reviewer visibility in states other than PendingReview is unresolved. Implementations must mark affected view behavior
blocked pending a human product decision and must not infer access rules.

## Testing

Tests must cover Requester-only, Reviewer-only, and dual-role users; comment validation/replacement; stale GUID tokens;
revocation ordered before or after mutation through the coordinated path; external/bypass limits; invalid Approved-state
operations; no partial changes; SQLite busy retry of entire transactions; and view behavior only where product scope is approved.
