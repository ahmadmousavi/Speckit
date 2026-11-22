# Tasks: ChangeFlow Change Request Management

**Input**: Design documents from `/specs/001-manage-software-change/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the single .NET API project and test project.

- [x] T001 Create the ASP.NET Core 10 solution and API project in src/ChangeFlow.Api/ per plan.md
- [x] T002 Add Microsoft.EntityFrameworkCore.Sqlite and ASP.NET Core JWT bearer dependencies to src/ChangeFlow.Api/ChangeFlow.Api.csproj
- [x] T003 Create the test project in tests/ChangeFlow.Api.Tests/ with unit, integration, and contract test folders
- [x] T004 Add test dependencies for EF Core SQLite and HTTP contract testing to tests/ChangeFlow.Api.Tests/ChangeFlow.Api.Tests.csproj
- [x] T005 Configure base application settings and Development signing-key placeholders outside source control in src/ChangeFlow.Api/appsettings.Development.json

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement infrastructure required by every user story.

**CRITICAL**: No user story implementation can begin until this checkpoint is complete.

- [x] T006 Define ChangeFlow entities for `data-model.md` entities `ChangeRequest.*`, `UserIdentity.Id`, `Permission.UserId`, `Permission.Capability`, and `Permission.RevokedAt` in src/ChangeFlow.Api/Models/
- [x] T007 Configure EF Core `DbContext` mappings, UUID string storage, and the unique `(UserId, Capability)` index for `data-model.md` `Permission.UserId` and `Permission.Capability` in src/ChangeFlow.Api/Data/ChangeFlowDbContext.cs
- [x] T008 Implement an explicit Microsoft.Data.Sqlite immediate transaction helper using `BeginTransaction(deferred: false)` and `Database.UseTransaction(...)` in src/ChangeFlow.Api/Data/SqliteImmediateTransaction.cs
- [x] T009 Implement a bounded SQLite busy timeout of 5 seconds and a maximum of 3 total attempts that rerun the complete immediate transaction in src/ChangeFlow.Api/Data/SqliteImmediateTransaction.cs
- [x] T010 Implement JWT bearer validation and stable UUID `sub` resolution in src/ChangeFlow.Api/Program.cs
- [x] T011 Implement Development-only token issuance from seeded UUID users in src/ChangeFlow.Api/Controllers/DevelopmentTokensController.cs
- [x] T012 Implement idempotent Development seeding for requester-only UUID `10000000-0000-4000-8000-000000000001`, reviewer-only UUID `20000000-0000-4000-8000-000000000002`, and dual-role UUID `30000000-0000-4000-8000-000000000003` in src/ChangeFlow.Api/Data/DevelopmentSeed.cs
- [x] T013 Implement server-side active permission loading and uniqueness-preserving revocation/restoration for `data-model.md` `Permission.UserId`, `Permission.Capability`, and `Permission.RevokedAt` in src/ChangeFlow.Api/Data/PermissionStore.cs
- [x] T014 Implement application/problem+json error mapping for MalformedInput, MissingOrEmptyChangeComment, Unauthorized, Forbidden, NotFound, InvalidState, ConcurrencyConflict, and InternalError in src/ChangeFlow.Api/
- [x] T015 Implement shared optimistic-concurrency update handling for `data-model.md` `ChangeRequest.ConcurrencyToken`, including stale-token predicate updates and rollback mapping to ConcurrencyConflict, in src/ChangeFlow.Api/Data/ChangeRequestStore.cs
- [x] T016 Add EF Core schema initialization/migrations setup that runs the Development seed for a fresh database in src/ChangeFlow.Api/

**Checkpoint**: Foundation ready - user story implementation can begin.

---

## Phase 3: User Story 1 - Create and Submit Change Requests (Priority: P1) 🎯 MVP

**Goal**: Allow an authenticated Requester to create, edit, submit a Draft, and reject unsupported deletion.

**Independent Test**: A requester-only test user creates a Draft, edits it, submits it to PendingReview, and receives the Location header and a new concurrency token; an attempted Draft deletion is rejected without state change.

### Tests for User Story 1

- [x] T017 [P] [US1] Add contract tests for POST `/change-requests` and `/change-requests/{id}/submit` in tests/ChangeFlow.Api.Tests/Contract/ChangeRequestContractTests.cs
- [x] T018 [P] [US1] Add integration tests for creation, Draft ownership editing, and Draft submission/rejection in tests/ChangeFlow.Api.Tests/Integration/UserStory1Tests.cs
- [x] T019 [P] [US1] Add unit tests for Draft state validation and Requester ownership in tests/ChangeFlow.Api.Tests/Unit/DraftWorkflowTests.cs
- [x] T020 [P] [US1] Add a rejection test for attempting to delete a Draft ChangeRequest, preserving FR-020 and SC-011, in tests/ChangeFlow.Api.Tests/Integration/DraftDeletionTests.cs

### Implementation for User Story 1

- [x] T021 [P] [US1] Implement `data-model.md` `ChangeRequest.Id`, `ChangeRequest.Title`, `ChangeRequest.Description`, `ChangeRequest.Reason`, `ChangeRequest.RequesterId`, `ChangeRequest.Status`, and `ChangeRequest.ConcurrencyToken` in src/ChangeFlow.Api/Models/ChangeRequest.cs
- [x] T022 [US1] Implement creation and owned Draft edit operations in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [x] T023 [US1] Implement Draft-to-PendingReview submission and resubmission lookup in src/ChangeFlow.Api/Controllers/ChangeRequestsController.cs
- [x] T024 [US1] Map Draft creation success to `201`, `Location: /change-requests/{id}`, and the new response concurrency token in src/ChangeFlow.Api/Controllers/ChangeRequestsController.cs
- [x] T025 [US1] Reject invalid submit operations and unauthorized ownership with InvalidState or Forbidden responses in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [x] T026 [US1] Ensure no delete operation is exposed or implemented for a Draft ChangeRequest and return the documented unsupported-operation rejection for any attempted deletion in src/ChangeFlow.Api/Controllers/ChangeRequestsController.cs

**Checkpoint**: User Story 1 is independently testable and provides the MVP increment.

---

## Phase 4: User Story 2 - Review and Approve Change Requests (Priority: P1)

**Goal**: Let an authorized non-requester Reviewer approve a PendingReview request.

**Independent Test**: Reviewer-only approval succeeds for another Requester's request, while self-approval and non-Reviewer approval are rejected.

### Tests for User Story 2

- [x] T027 [P] [US2] Add contract tests for POST `/change-requests/{id}/approve` in tests/ChangeFlow.Api.Tests/Contract/ApprovalContractTests.cs
- [x] T028 [P] [US2] Add integration tests for successful approval, self-approval, and unauthorized approval in tests/ChangeFlow.Api.Tests/Integration/UserStory2Tests.cs
- [x] T029 [P] [US2] Add unit tests for PendingReview-to-Approved transition rules in tests/ChangeFlow.Api.Tests/Unit/ApprovalWorkflowTests.cs

### Implementation for User Story 2

- [x] T030 [US2] Implement active Reviewer capability and Requester identity comparison in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [x] T031 [US2] Implement PendingReview-to-Approved mutation with a new `data-model.md` `ChangeRequest.ConcurrencyToken` in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [x] T032 [US2] Implement POST approve controller behavior and InvalidState/Forbidden/ConcurrencyConflict mapping in src/ChangeFlow.Api/Controllers/ChangeRequestsController.cs
- [x] T033 [US2] Prevent editing or revoking Approved requests in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs

**Checkpoint**: User Story 2 independently demonstrates controlled approval and separation of duties.

---

## Phase 5: User Story 3 - Request Changes and Resubmit (Priority: P1)

**Goal**: Let an authorized Reviewer request changes with a comment, then let the Requester edit and resubmit.

**Independent Test**: A reviewer adds a comment and changes status; the requester edits `Reason` and resubmits; later comments replace earlier comments without changing `Reason`.

### Tests for User Story 3

- [x] T034 [P] [US3] Add contract tests for POST `/change-requests/{id}/request-changes` in tests/ChangeFlow.Api.Tests/Contract/RequestChangesContractTests.cs
- [x] T035 [P] [US3] Add integration tests for comment replacement and ChangesRequested edit/resubmit in tests/ChangeFlow.Api.Tests/Integration/UserStory3Tests.cs
- [x] T036 [P] [US3] Add unit tests for missing, empty, and whitespace-only comments in tests/ChangeFlow.Api.Tests/Unit/RequestChangesWorkflowTests.cs

### Implementation for User Story 3

- [x] T037 [P] [US3] Implement `data-model.md` `ChangeRequest.LastChangeRequestComment` in src/ChangeFlow.Api/Models/ChangeRequest.cs
- [x] T038 [US3] Implement server validation and trimming of RequestChanges `comment` for `MissingOrEmptyChangeComment` in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [x] T039 [US3] Implement PendingReview-to-ChangesRequested mutation that replaces `data-model.md` `ChangeRequest.LastChangeRequestComment` without changing `data-model.md` `ChangeRequest.Reason` in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [x] T040 [US3] Implement ChangesRequested edit and separate ChangesRequested-to-PendingReview resubmit operations in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [x] T041 [US3] Implement POST request-changes and PUT edit behavior in src/ChangeFlow.Api/Controllers/ChangeRequestsController.cs
- [x] T042 [US3] Include `LastChangeRequestComment` and the preserved/editable `Reason` in eligible ChangeRequest responses in src/ChangeFlow.Api/Models/ChangeRequestResponse.cs

**Checkpoint**: User Story 3 independently demonstrates the review-change-resubmit loop.

---

## Phase 6: User Story 4 - Integrity and Rejection Behavior (Priority: P2)

**Goal**: Rejected operations, stale updates, revocation ordering, authorization, and read-only view behavior preserve data integrity.

**Independent Test**: Concurrent and unauthorized operations cannot overwrite state or persisted business data, and eligible view-authorized users receive complete ChangeRequest responses without modification authority.

### Tests for User Story 4

- [x] T043 [P] [US4] Add integration tests for stale concurrency tokens and invalid transitions in tests/ChangeFlow.Api.Tests/Integration/ConcurrencyAndStateTests.cs
- [x] T044 [P] [US4] Add integration tests for immediate permission revocation/revocation ordering through the coordinated transaction path in tests/ChangeFlow.Api.Tests/Integration/PermissionRevocationTests.cs
- [x] T045 [P] [US4] Add unit tests for approved-state mutation, PendingReview edit rejection, and rollback in tests/ChangeFlow.Api.Tests/Unit/IntegrityWorkflowTests.cs
- [x] T046 [P] [US4] Add contract tests for GET `/change-requests/{id}` view authorization and read-only detail visibility in tests/ChangeFlow.Api.Tests/Contract/ViewAuthorizationContractTests.cs

### Implementation for User Story 4

- [x] T047 [US4] Implement immediate-transaction rechecks for `data-model.md` `Permission.UserId`, `Permission.Capability`, and `Permission.RevokedAt` before every state mutation in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [x] T048 [US4] Reject all unsupported transitions and Approved mutations with InvalidState in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs
- [x] T049 [US4] Implement view and modification authorization independently for approved visibility scope and GET `/change-requests/{id}` in src/ChangeFlow.Api/Services/ChangeRequestAuthorization.cs
- [x] T050 [US4] Implement GET `/change-requests/{id}` controller behavior and map eligible results to `ChangeRequestResponse`, with no visibility policy invented for states outside PendingReview in src/ChangeFlow.Api/Controllers/ChangeRequestsController.cs
- [x] T051 [US4] Preserve all persisted request/comment/concurrency state across rejected operations in src/ChangeFlow.Api/Services/ChangeRequestWorkflow.cs

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Complete validation and implementation readiness without changing product behavior.

- [x] T052 [P] Consume the OpenAPI planning contract and verify every response code and the Location header in implementation tests and src/ChangeFlow.Api/; do not modify specs/001-manage-software-change/contracts/openapi.yaml from implementation tasks
- [x] T053 [P] Add structured logs without sensitive data in src/ChangeFlow.Api/
- [x] T054 [P] Add unit and integration test configuration for disposable SQLite databases in tests/ChangeFlow.Api.Tests/
- [x] T055 Verify all tasks implement constrained fields using `data-model.md` citations and update tasks.md if gaps remain
- [x] T056 Run the quickstart build, test, and API validation flow documented in specs/001-manage-software-change/quickstart.md
- [x] T057 Review constitution and spec compliance before reporting implementation validation complete

**Dependencies & Execution Order**

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

---

## Phase 8: Convergence

- [x] T058 CRITICAL: Route `PermissionStore` revocation and restoration through the explicit immediate Microsoft.Data.Sqlite transaction path used by state-changing operations so permission and mutation changes are serialized per FR-018 and Constitution III/IV (contradicts).
- [x] T059 [US4] Restrict Reviewer visibility to the already approved PendingReview scope and block view behavior for states outside PendingReview instead of allowing all status values per FR-005/FR-016 and plan: View Authorization Scope (partial).
- [x] T060 Review and remove or justify the unapproved 200-character `ChangeRequest.Title` limit in `ChangeFlowDbContext` per data-model.md and the simplicity constitution principle (unrequested).

## Phase 9: Convergence

- [ ] T061 [US2] Add a dual-role Requester and Reviewer test proving own-request approval is rejected per FR-010 and SC-004 (partial).
- [ ] T062 [US4] Add tests proving mutation-before-revocation and revocation-before-mutation ordering through the coordinated immediate transaction path per FR-018 (partial).
- [ ] T063 Remove or justify the unrequested template WeatherForecast and UnitTest1 artifacts per plan scope and Constitution I (unrequested).
