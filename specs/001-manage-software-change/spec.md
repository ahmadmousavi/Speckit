# Feature Specification: ChangeFlow Change Request Management

**Feature Branch**: 001-manage-software-change

**Created**: 2026-11-22

**Status**: Draft

**Input**: User description: "Create a complete product specification for ChangeFlow, an HTTP API for managing software Change Requests."

## Clarifications

### Session 2026-11-22

- Q: Should an authorized Reviewer be able to review every PendingReview request, or only requests assigned to that Reviewer? → A: All authorized Reviewers can review all PendingReview requests.
- Q: Which requests can a user view if they are not authorized to modify that request? → A: Only Requesters and authorized Reviewers can view requests relevant to their workflow.
- Decision: A user authorized to view a Change Request but not authorized to modify it can view all details of that request. View authorization and modification authorization are independent.
- Q: Is a reason or comment required whenever a Reviewer requests changes? → A: A reason or comment is mandatory.
- Q: Can a Requester modify a PendingReview request after submission, or must they wait until a Reviewer requests changes? → A: Requesters cannot modify PendingReview requests; they must wait for ChangesRequested.
- Q: What should happen to pending Change Requests when a Requester's or Reviewer's role or permission is revoked? → A: Suspend pending-state operations immediately.
- Q: Can an Approved request ever be revoked or reopened? → A: Approved requests cannot be revoked or reopened.
- Q: Can a Requester delete a Change Request while it is in Draft? → A: Draft deletion is not allowed.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create and Submit a Change Request (Priority: P1)

A Requester creates a software Change Request with a title, description, reason, and their identity,
then submits it for review. This establishes the basic controlled workflow and provides a reviewable item.

**Why this priority**: Creation and submission are required before any review or approval can occur.

**Independent Test**: A Requester can create a Draft, edit it, submit it, and receive a valid persisted
Change Request in PendingReview.

**Acceptance Scenarios**:

1. **Given** an authenticated Requester, **When** the Requester creates a Change Request with all required
   information, **Then** a Draft is created and attributable to that Requester.
2. **Given** a Requester's Draft, **When** the Requester submits it, **Then** its status becomes PendingReview
   and no prior review decision is assigned.
3. **Given** a Change Request that is not in Draft, **When** its Requester attempts to submit it, **Then**
   submission is rejected and the request remains unchanged.

---

### User Story 2 - Review and Approve a Change Request (Priority: P1)

An authorized Reviewer examines a PendingReview request and approves it. Approved requests become immutable
and cannot be changed or approved again.

**Why this priority**: Approval is the central outcome of ChangeFlow.

**Independent Test**: An authorized non-requester Reviewer can approve a PendingReview request, while a
Requester cannot approve their own request.

**Acceptance Scenarios**:

1. **Given** a PendingReview request submitted by another person, **When** an authorized Reviewer approves it,
   **Then** the status becomes Approved and the approved request is not editable.
2. **Given** a PendingReview request, **When** a user who is not an authorized Reviewer attempts approval,
   **Then** approval is rejected and the request remains PendingReview.
3. **Given** a PendingReview request whose requester also holds the Reviewer role, **When** that user attempts
   to approve their own request, **Then** approval is rejected and the request remains PendingReview.

---

### User Story 3 - Request Changes and Resubmit (Priority: P1)

A Reviewer returns a PendingReview request with a ChangesRequested status so its Requester can revise and
resubmit it. The Requester may edit and resubmit only their own request while it is in ChangesRequested.

**Why this priority**: This supports the expected iterative review workflow without allowing uncontrolled edits.

**Independent Test**: An authorized Reviewer can request changes, and the Requester can edit and resubmit the
request through one valid state transition.

**Acceptance Scenarios**:

1. **Given** a PendingReview request, **When** an authorized Reviewer requests changes, **Then** the status
   becomes ChangesRequested.
2. **Given** a Requester's ChangesRequested request, **When** the Requester edits and resubmits it, **Then**
   the status becomes PendingReview and the edits are retained.
3. **Given** a Change Request in Draft or Approved state, **When** a Reviewer requests changes, **Then** the
   request remains unchanged and the action is rejected.

---

### User Story 4 - Prevent Invalid Editing and State Changes (Priority: P2)

Requesters and Reviewers can perform only operations authorized for their role and the request's current state.
Invalid edits, transitions, identities, and stale updates are rejected without partial changes.

**Why this priority**: Workflow and data integrity require every operation to preserve the controlled lifecycle.

**Independent Test**: Rejected operations leave the request and related persisted business data unchanged.

**Acceptance Scenarios**:

1. **Given** an Approved request, **When** its Requester attempts to edit it, **Then** editing is rejected.
2. **Given** a user without the Requester relationship or required role, **When** that user attempts to edit a
   Change Request, **Then** editing is rejected and no content is changed.
3. **Given** a request in any state, **When** an operation would use an unsupported transition, **Then** the
   operation is rejected and the request remains in its previous state.
4. **Given** two callers updating the same current request version, **When** one update is applied, **Then** a
   later stale update is rejected rather than overwriting the newer state.

### Edge Cases

- A Requester attempts to edit a request after its status has changed by another operation.
- A Reviewer attempts to approve or request changes for a request with a stale view of its status.
- A Draft is submitted after the user's required role or permission is revoked.
- A PendingReview request is submitted for approval while another valid operation changes its status.
- A stale edit targets a ChangesRequested request after it has been resubmitted.
- A rejected operation occurs after some business data has been prepared for persistence.
- A request is associated with an identity that differs from the caller initiating an operation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow an authenticated Requester to create a Change Request in Draft.
- **FR-002**: A created Change Request MUST contain a title, description, reason for change, requester identity,
  and current status.
- **FR-003**: A Requester MUST be able to edit their own Draft.
- **FR-004**: A Requester MUST be able to submit their own Draft, changing it from Draft to PendingReview.
- **FR-005**: An authorized Reviewer MUST be able to review any PendingReview request.
- **FR-006**: An authorized Reviewer MUST be able to approve a PendingReview request, changing it to Approved.
- **FR-007**: An authorized Reviewer MUST be able to request changes on a PendingReview request, changing it to
  ChangesRequested. A reason or comment MUST be provided.
- **FR-008**: A Requester MUST be able to edit their own ChangesRequested request and resubmit it, changing it to
  PendingReview.
- **FR-009**: System MUST prevent a Requester from editing an Approved request.
- **FR-010**: System MUST prevent a Requester from approving their own request, even when the requester also holds
  the Reviewer role.
- **FR-011**: Every state-changing operation MUST verify the caller's identity, current authorization, and the
  Change Request's current state before applying a change.
- **FR-012**: System MUST reject operations that are not in the explicitly supported transition set:
  Draft → PendingReview, PendingReview → Approved, PendingReview → ChangesRequested, and
  ChangesRequested → PendingReview.
- **FR-013**: Concurrent updates MUST be detected and stale updates MUST be rejected rather than silently overwriting
  newer changes.
- **FR-014**: Rejected operations MUST leave the Change Request and all related persisted business data unchanged.
- **FR-015**: Authorization MUST be enforced through the system's server-side workflow rather than client-only checks.
- **FR-016**: Request viewing MUST be restricted to the Requester and authorized Reviewers relevant to the request's workflow. Users authorized to view a request MUST be able to view all of its details. Lack of modification authorization MUST NOT hide or mask request details. Viewing a request MUST NOT grant permission to modify it.
- **FR-017**: A Requester MUST NOT be able to modify a Change Request while it is in PendingReview.
- **FR-018**: If a Requester's or Reviewer's role or permission is revoked, pending-state operations by that user MUST be suspended immediately and the request state MUST remain unchanged.
- **FR-019**: Approved requests MUST NOT be revoked or reopened.
- **FR-020**: A Requester MUST NOT be able to delete a Change Request while it is in Draft.

### Key Entities *(include if feature involves data)*

- **Change Request**: A proposed software change containing its title, description, reason for change, requester
  identity, and current status.
- **Requester**: A user who creates Change Requests and is permitted to edit and submit requests they own in
  supported states.
- **Reviewer**: A user authorized to review PendingReview requests and approve or request changes for them.
- **Lifecycle State**: Draft, PendingReview, Approved, or ChangesRequested. It represents the Change Request's
  current position in the controlled workflow.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-002**: 100% of valid approvals change exactly one eligible PendingReview request to Approved; the action
  cannot be repeated on that approved request.
- **SC-003**: 100% of valid request-changes actions change exactly one eligible PendingReview request to ChangesRequested, after which its Requester can edit and resubmit it to PendingReview.
- **SC-004**: 100% of attempted self-approvals are rejected, including when the caller holds both roles.
- **SC-005**: 100% of attempted edits to Approved requests are rejected without changing request content or status.
- **SC-006**: 100% of unsupported state transitions are rejected, and the request remains in its prior state.
- **SC-009**: 100% of rejected PendingReview edit attempts leave the request unchanged.
- **SC-007**: 100% of stale concurrent updates are identified and rejected, with no silent overwrite of newer data.
- **SC-008**: 100% of rejected operations preserve the complete previously persisted Change Request and related
  business data.
- **SC-010**: 100% of attempts to revoke or reopen an Approved request are rejected.
- **SC-011**: 100% of Draft deletion attempts by Requesters are rejected and leave the Draft unchanged.
- **SC-012**: 100% of view-authorized but modification-unauthorized users can view all details of an eligible Change Request while 100% of their modification attempts are rejected.
- **SC-013**: 100% of attempts to request changes without a non-empty reason or comment are rejected, leaving the request unchanged.

## Assumptions

- A Requester has an authenticated identity and can create only for their own identity.
- Rejected operations return a clear failure outcome that distinguishes authorization, state, and stale-update failures.
- The system does not include a frontend or implementation-specific HTTP presentation details in this specification.
