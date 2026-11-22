# Tasks: ChangeFlow Change Request Management

**Input**: Design documents from `/specs/001-manage-software-change/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the single .NET API project and test project.

- [ ] T001 Create the ASP.NET Core 10 solution and API project in src/ChangeFlow.Api/ per plan.md
- [ ] T002 Add Microsoft.EntityFrameworkCore.Sqlite and ASP.NET Core JWT bearer dependencies to src/ChangeFlow.Api/ChangeFlow.Api.csproj
- [ ] T003 Create the test project in tests/ChangeFlow.Api.Tests/ with unit, integration, and contract test folders
- [ ] T004 Add test dependencies for EF Core SQLite and HTTP contract testing to tests/ChangeFlow.Api.Tests/ChangeFlow.Api.Tests.csproj
- [ ] T005 Configure base application settings and Development signing-key placeholders outside source control in src/ChangeFlow.Api/appsettings.Development.json

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement infrastructure required by every user story.

**CRITICAL**: No user story implementation can begin until this checkpoint is complete.

- [ ] T006 Define ChangeFlow entities for `data-model.md` entities `ChangeRequest.*`, `UserIdentity.Id`, and `Permission.(UserId,Capability,RevokedAt)` in src/ChangeFlow.Api/Models/
- [ ] T007 Configure EF Core `DbContext` mappings, UUID string storage, and the unique `(UserId, Capability)` index in src/ChangeFlow.Api/Data/ChangeFlowDbContext.cs
- [ ] T008 Implement an explicit Microsoft.Data.Sqlite immediate transaction helper using `BeginTransaction(deferred: false)` and `Database.UseTransaction(...)` in src/ChangeFlow.Api/Data/SqliteImmediateTransaction.cs
- [ ] T009 Implement bounded SQLite busy/locked transaction retry that reruns the complete operation in src/ChangeFlow.Api/Data/SqliteImmediateTransaction.cs
- [ ] T010 Implement JWT bearer validation and stable UUID `sub` resolution in src/ChangeFlow.Api/Program.cs
- [ ] T011 Implement Development-only token issuance from seeded UUID users in src/ChangeFlow.Api/Controllers/DevelopmentTokensController.cs
- [ ] T012 Implement idempotent Development seeding for requester-only UUID `10000000-0000-4000-8000-000000000001`, reviewer-only UUID `20000000-0000-4000-8000-000000000002`, and dual-role UUID `30000000-0000-4000-8000-000000000003` in src/ChangeFlow.Api/Data/DevelopmentSeed.cs
- [ ] T013 Implement server-side active permission loading and uniqueness-preserving revocation/restoration in src/ChangeFlow.Api/Data/PermissionStore.cs
- [ ] T014 Implement application/problem+json error mapping for MalformedInput, MissingOrEmptyChangeComment, Unauthorized, Forbidden, NotFound, InvalidState, ConcurrencyConflict, and InternalError in src/ChangeFlow.Api/
- [ ] T015 Implement shared optimistic-concurrency update handling for `data-model.md` `ChangeRequest.ConcurrencyToken`, including stale-token predicate updates and rollback mapping to ConcurrencyConflict, in src/ChangeFlow.Api/Data/ChangeRequestStore.cs
- [ ] T016 Add EF Core schema initialization/migrations setup that runs the Development seed for a fresh database in src/ChangeFlow.Api/

**Checkpoint**: Foundation ready - user story implementation can begin.

---

## Phase 3: User Story 1 - Create and Submit Change Requests (Priority: P1) 🎯 MVP

**Goal**: Allow an authenticated Requester to create, edit, and submit a Draft.

**Independent Test**: A requester-only test user creates a Draft, edits it, submits it to PendingReview, and receives the Location header and a new concurrency token.

### Tests for User Story 1

- [ ] T017 [P] [US1] Add contract tests for POST `/change-requests` and `/change-requests/{id}/submit` in tests/ChangeFlow.Api.Tests/Contract/ChangeRequestContractTests.cs
- [ ] T018 [P] [US1] Add integration tests for creation, Draft ownership editing, and Draft submission/rejection in tests/ChangeFlow.Api.Tests/Integration/UserStory1Tests.cs
- [ ] T019 [P] [US1] Add unit tests for Draft state validation and Requester ownership in tests/ChangeFlow.Api.Tests/Unit/DraftWorkflowTests.cs

### Implementation for User Story 1

- [ ] T020 [P] [US1] Implement `data-model.md` `ChangeRequest.Id`, `ChangeRequest.Title`, `ChangeRequest.Description`, `ChangeRequest.Reason`, `ChangeRequest.RequesterId`, `ChangeRequest.Status`, and `ChangeRequest.ConcurrencyToken` in src/ChangeFlow.Api/Models/ChangeRequest.cs
- [ ] T021 [US1] Implement creation and owned Draft edit operations in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [ ] T022 [US1] Implement Draft-to-PendingReview submission and resubmission lookup in src/ChangeFlow.Api/Controllers/ChangeRequestsController.cs
- [ ] T023 [US1] Map Draft creation success to `201`, `Location: /change-requests/{id}`, and the new response concurrency token in src/ChangeFlow.Api/Controllers/ChangeRequestsController.cs
- [ ] T024 [US1] Reject invalid submit operations and unauthorized ownership with InvalidState or Forbidden responses in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs

**Checkpoint**: User Story 1 is independently testable and provides the MVP increment.

---

## Phase 4: User Story 2 - Review and Approve Change Requests (Priority: P1)

**Goal**: Let an authorized non-requester Reviewer approve a PendingReview request.

**Independent Test**: Reviewer-only approval succeeds for another Requester's request, while self-approval and non-Reviewer approval are rejected.

### Tests for User Story 2

- [ ] T025 [P] [US2] Add contract tests for POST `/change-requests/{id}/approve` in tests/ChangeFlow.Api.Tests/Contract/ApprovalContractTests.cs
- [ ] T026 [P] [US2] Add integration tests for successful approval, self-approval, and unauthorized approval in tests/ChangeFlow.Api.Tests/Integration/UserStory2Tests.cs
- [ ] T027 [P] [US2] Add unit tests for PendingReview-to-Approved transition rules in tests/ChangeFlow.Api.Tests/Unit/ApprovalWorkflowTests.cs

### Implementation for User Story 2

- [ ] T028 [US2] Implement active Reviewer capability and Requester identity comparison in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [ ] T029 [US2] Implement PendingReview-to-Approved mutation with a new `data-model.md` `ChangeRequest.ConcurrencyToken` in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [ ] T030 [US2] Implement POST approve controller behavior and InvalidState/Forbidden/ConcurrencyConflict mapping in src/ChangeFlow.Api/Controllers/ChangeRequestsController.cs
- [ ] T031 [US2] Prevent editing or revoking Approved requests in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs

**Checkpoint**: User Story 2 independently demonstrates controlled approval and separation of duties.

---

## Phase 5: User Story 3 - Request Changes and Resubmit (Priority: P1)

**Goal**: Let an authorized Reviewer request changes with a comment, then let the Requester edit and resubmit.

**Independent Test**: A reviewer adds a comment and changes status; the requester edits `Reason` and resubmits; later comments replace earlier comments without changing `Reason`.

### Tests for User Story 3

- [ ] T032 [P] [US3] Add contract tests for POST `/change-requests/{id}/request-changes` in tests/ChangeFlow.Api.Tests/Contract/RequestChangesContractTests.cs
- [ ] T033 [P] [US3] Add integration tests for comment replacement and ChangesRequested edit/resubmit in tests/ChangeFlow.Api.Tests/Integration/UserStory3Tests.cs
- [ ] T034 [P] [US3] Add unit tests for missing, empty, and whitespace-only comments in tests/ChangeFlow.Api.Tests/Unit/RequestChangesWorkflowTests.cs

### Implementation for User Story 3

- [ ] T035 [P] [US3] Implement `data-model.md` `ChangeRequest.LastChangeRequestComment` in src/ChangeFlow.Api/Models/ChangeRequest.cs
- [ ] T036 [US3] Implement server validation and trimming of RequestChanges `comment` for `MissingOrEmptyChangeComment` in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [ ] T037 [US3] Implement PendingReview-to-ChangesRequested mutation that replaces `data-model.md` `ChangeRequest.LastChangeRequestComment` without changing `data-model.md` `ChangeRequest.Reason` in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [ ] T038 [US3] Implement ChangesRequested edit and separate ChangesRequested-to-PendingReview resubmit operations in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [ ] T039 [US3] Implement POST request-changes and PUT edit behavior in src/ChangeFlow.Api/Controllers/ChangeRequestsController.cs
- [ ] T040 [US3] Include `LastChangeRequestComment` and the preserved/editable `Reason` in eligible ChangeRequest responses in src/ChangeFlow.Api/Models/ChangeRequestResponse.cs

**Checkpoint**: User Story 3 independently demonstrates the review-change-resubmit loop.

---

## Phase 6: User Story 4 - Integrity and Rejection Behavior (Priority: P2)

**Goal**: Rejected operations, stale updates, revocation ordering, authorization, and read-only view behavior preserve data integrity.

**Independent Test**: Concurrent and unauthorized operations cannot overwrite state or persisted business data, and eligible view-authorized users receive complete ChangeRequest responses without modification authority.

### Tests for User Story 4

- [ ] T041 [P] [US4] Add integration tests for stale concurrency tokens and invalid transitions in tests/ChangeFlow.Api.Tests/Integration/ConcurrencyAndStateTests.cs
- [ ] T042 [P] [US4] Add integration tests for immediate permission revocation/revocation ordering through the coordinated transaction path in tests/ChangeFlow.Api.Tests/Integration/PermissionRevocationTests.cs
- [ ] T043 [P] [US4] Add unit tests for approved-state mutation, PendingReview edit rejection, and rollback in tests/ChangeFlow.Api.Tests/Unit/IntegrityWorkflowTests.cs
- [ ] T044 [P] [US4] Add contract tests for GET `/change-requests/{id}` view authorization and read-only detail visibility in tests/ChangeFlow.Api.Tests/Contract/ViewAuthorizationContractTests.cs

### Implementation for User Story 4

- [ ] T045 [US4] Implement immediate-transaction rechecks for `data-model.md` `Permission.(UserId,Capability,RevokedAt)` before every state mutation in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [ ] T046 [US4] Reject all unsupported transitions and Approved mutations with InvalidState in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [ ] T047 [US4] Implement view and modification authorization independently for approved visibility scope and GET `/change-requests/{id}` in src/ChangeFlow.Api/Services/ChangeRequestAuthorization.cs
- [ ] T048 [US4] Implement GET `/change-requests/{id}` controller behavior and map eligible results to `ChangeRequestResponse`, with no visibility policy invented for states outside PendingReview in src/ChangeFlow.Api/Controllers/ChangeRequestsController.cs
- [ ] T049 [US4] Preserve all persisted request/comment/concurrency state across rejected operations in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs

**Checkpoint**: User Story 4 independently demonstrates integrity under rejection and concurrency scenarios.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Complete validation and implementation readiness without changing product behavior.

- [ ] T050 [P] Add OpenAPI contract documentation for every response code and the Location header in src/ChangeFlow.Api/Program.cs and specs/001-manage-software-change/contracts/openapi.yaml
- [ ] T051 [P] Add structured logs without sensitive data in src/ChangeFlow.Api/
- [ ] T052 [P] Add unit and integration test configuration for disposable SQLite databases in tests/ChangeFlow.Api.Tests/
- [ ] T053 Verify all tasks implement constrained fields using `data-model.md` citations and update tasks.md if gaps remain
- [ ] T054 Run the quickstart build, test, and API validation flow documented in specs/001-manage-software-change/quickstart.md
- [ ] T055 Review constitution and spec compliance before reporting implementation validation complete

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 setup has no dependencies.
- Phase 2 foundational work blocks every user story.
- User Stories 1-4 depend on Phase 2 but may proceed in priority order or in parallel after foundation.
- Phase 7 depends on all selected user stories.

### User Story Dependencies

- **US1**: Independent MVP; depends only on Foundation.
- **US2**: Depends on Foundation and ChangeRequest state model; can proceed independently of US3/US4.
- **US3**: Depends on Foundation and ChangeRequest state model; integrates with US1 and US2 workflows.
- **US4**: Depends on Foundation and state-transition implementation; applies cross-cutting integrity checks.

### Parallel Opportunities

- Phase 1 tasks T002-T004 can run in parallel after project creation.
- Foundation model, DbContext, authentication, and error tasks can run in parallel once project structure exists.
- Tests within a user story can run in parallel.
- US1, US2, and US3 implementation phases can proceed in parallel after Foundation if shared workflow integration is coordinated.

## Implementation Strategy

### MVP First (User Story 1)

1. Complete Phases 1-2.
2. Complete Phase 3 (US1).
3. Stop and validate creation/edit/submission independently.

### Incremental Delivery

1. Foundation ready.
2. Deliver US1 Draft workflow.
3. Add US2 approval workflow.
4. Add US3 request-changes/resubmit loop.
5. Complete US4 integrity behavior and polish.

## Notes

- [P] tasks are independent of other incomplete tasks in the same phase.
- User-story phases contain complete independently testable increments.
- External/bypass permission changes are explicitly outside the transaction guarantee.
- Reviewer visibility outside PendingReview remains blocked pending human product clarification.
