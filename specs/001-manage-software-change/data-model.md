# Data Model: ChangeFlow

## ChangeRequest

Represents one software change proposal.

| Field | Type | Rules |
|-------|------|-------|
| Id | UUID string | Unique; server generated |
| Title | String | Required; non-empty |
| Description | String | Required; non-empty |
| Reason | String | Required; non-empty; Requester's change reason; editable only through permitted normal edits |
| LastChangeRequestComment | String, nullable | Non-empty when set; last Reviewer comment from RequestChanges |
| RequesterId | UUID string | Required; set from authenticated JWT `sub` at creation |
| Status | Enum | Draft, PendingReview, ChangesRequested, or Approved |
| ConcurrencyToken | GUID string | Server generated on create; replaced with a new GUID after every successful mutation |

A Reviewer RequestChanges comment is persisted in `LastChangeRequestComment`; it never overwrites `Reason`. The normal
Edit operation may update `Reason` when editing is permitted by the existing lifecycle, namely Draft and ChangesRequested.
A later RequestChanges operation replaces `LastChangeRequestComment`.

Approved Change Requests are immutable. The product API does not delete or archive Drafts.

## UserIdentity

The minimal identity model uses one concrete type consistently.

| Field | Type | Rules |
|-------|------|-------|
| Id | UUID string | Matches the JWT `sub` claim and `ChangeRequest.RequesterId` |

No password account, registration record, or production identity-provider profile is required.

## Permission

Represents server-side Requester and Reviewer capabilities.

| Field | Type | Rules |
|-------|------|-------|
| UserId | UUID string | Foreign key to `UserIdentity.Id` |
| Capability | Enum | Requester or Reviewer |
| RevokedAt | Timestamp, nullable | Null means active; non-null means revoked |

A database unique index on `(UserId, Capability)` is required, preventing duplicate permission records for the same
pair. Revocation updates `RevokedAt`; restoration clears it in the same record. No permission-history table is required.
A user may hold both capabilities. JWT role/permission claims are not authoritative.

Permission revocation is not an API capability; it is an out-of-band administrative/database operation. It is meaningful
only when revocation uses the same immediate-transaction database path as state-changing operations. External changes made
outside that path are outside the transaction guarantee.

## State Transitions

| Source | Operation | Destination | Required Checks |
|--------|-----------|-------------|-----------------|
| Draft | Edit | Draft | Owner; active Requester permission; original token matches; may edit `Reason` |
| Draft | Submit | PendingReview | Owner; active Requester permission; original token matches |
| PendingReview | Approve | Approved | Active Reviewer permission; caller is not requester; original token matches |
| PendingReview | RequestChanges | ChangesRequested | Active Reviewer; non-empty `comment`; original token matches; set or replace `LastChangeRequestComment` |
| ChangesRequested | Edit | ChangesRequested | Owner; active Requester permission; original token matches; may edit `Reason` |
| ChangesRequested | Resubmit | PendingReview | Owner; active Requester permission; original token matches |

Editing a ChangesRequested request keeps it in ChangesRequested. Resubmission is a separate operation that changes it to
PendingReview. Editing or revoking an Approved request is prohibited and is an invalid state transition.

## Development Seed

Development database initialization deterministically and idempotently seeds:

- Requester-only UUID `10000000-0000-4000-8000-000000000001` with active Requester permission.
- Reviewer-only UUID `20000000-0000-4000-8000-000000000002` with active Reviewer permission.
- Dual-role UUID `30000000-0000-4000-8000-000000000003` with active Requester and Reviewer permissions.

The seed is applied when the database is initialized and creates missing users/permission rows without duplication.
It is Development-only.

## Relationships

- One `UserIdentity` owns zero or more `ChangeRequest` records.
- One `UserIdentity` has zero or more `Permission` records.
- A user may hold both Requester and Reviewer permissions.
- No general review-history or audit entity is required.
- View authorization and modification authorization are independent.

## Validation Rules

- Title, description, original Reason, requester UUID, and status are required for a created Change Request.
- `Reason` may be edited through permitted normal edit operations and is not overwritten by Reviewer comments.
- `LastChangeRequestComment` is nullable and is set/replaced only by successful RequestChanges operations.
- RequestChanges requires a server-trimmed non-empty `comment`; missing, empty, or whitespace-only values use
  `400 Bad Request` and `MissingOrEmptyChangeComment`.
- Every mutation supplies the original concurrency GUID.
- Every successful mutation assigns a new GUID concurrency token.
- EF Core UPDATE predicates include both request ID and original concurrency token.
- Mutating an Approved request is `409 Conflict` with `InvalidState`.
- Rejected operations return no partial state or data changes.
- Request viewing remains limited to the approved Requester and authorized Reviewer scope.
- The exact Reviewer visibility scope in states other than PendingReview is unresolved and blocked for implementation.
