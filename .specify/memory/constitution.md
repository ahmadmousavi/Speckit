<!-- Sync Impact Report
- Version change: uninitialized template → 1.0.0
- Modified principles: none
- Added principles: none
- Removed principles: none
- Follow-up TODOs: none
-->

# ChangeFlow Constitution

## Core Principles

### I. Simplicity and Maintainability

ChangeFlow implementations MUST choose the simplest design that satisfies the current approved requirements.
Speculative abstractions, unnecessary layers, premature extensibility, and dependencies introduced without
need MUST NOT be added. Every added dependency, abstraction, or source of architectural complexity MUST include
a documented rationale, the problem it solves, and the simpler alternative considered. Code MUST prioritize
readability and maintainability over clever or overly generic solutions.

### II. Business Workflow Integrity

Change Request state transitions MUST follow the explicitly documented set of valid transitions. The server MUST
validate business rules and reject a transition whose prerequisites or source state are invalid. Each rejected
transition MUST leave persisted state unchanged, including all related state and audit data. Approved Change
Requests MUST NOT be modified.

### III. Identity, Authorization, and Separation of Duties

Every state-changing operation MUST verify the caller's authenticated identity, the caller's current authorization,
and the resource's current state before changing it. Authorization decisions MUST be enforced by the server and
MUST NOT depend on client-provided role or identity claims unless those claims have been validated as trustworthy.
A requester MUST NOT approve their own Change Request, even if the requester has the Reviewer role. Unauthorized
operations MUST neither modify nor expose protected resource data.

### IV. Data Integrity and Concurrency

Concurrent updates MUST NOT silently overwrite newer data. State transitions and related persistence changes MUST
preserve data consistency, including through failure, cancellation, or partial execution. Concurrency conflicts
MUST be detected and returned explicitly so callers can retry or refresh. Failed operations MUST leave no partially
applied business changes.

### V. Automated Testing and Quality

Critical business rules and workflow transitions MUST have automated test coverage for both successful operations
and important rejection scenarios. Tests MUST include authorization, self-approval prevention, invalid transitions,
and concurrency behavior. A change MUST NOT be considered validated by compilation alone; relevant tests and
acceptance criteria MUST pass. Build, Test, or Validation status MUST be reported as successful only after the
relevant checks are actually executed and pass, and their evidence MUST be retained in the workflow record.

### VI. Explicit Decisions and Traceability

Product requirements, assumptions, and architectural decisions MUST be documented separately and traceably.
The Coding Agent MUST NOT independently resolve impactful product ambiguities; it MUST ask for clarification or
record the ambiguity for human review. Implementation MUST remain traceable from approved specifications and tasks
to the resulting changes. When implementation reveals a gap or conflict, it MUST be documented with traceable
follow-up work rather than silently changing intended behavior.

## Additional Constraints

ChangeFlow is an HTTP API; a frontend is outside the current scope. Implementation and architecture MUST remain
appropriate to the project's current scale. AI, RAG, MCP, multi-agent functionality, and other unrelated
capabilities MUST NOT be introduced. Authentication, persistence, and concurrency implementation choices MUST be
justified during planning rather than prematurely fixed by this constitution.

## Development Workflow

The project MUST follow the Spec Kit sequence: Constitution → Specify → Clarify → Plan → Tasks → Analyze →
Implement → Build/Test/Validation → Converge. Important product and architecture decisions MUST be reviewed and
approved before implementation. Analyze MUST remain a read-only consistency review of project artifacts. Converge
MUST assess the gap between documented intent and the codebase and MUST NOT directly modify implementation code;
remaining work MUST be recorded as traceable tasks. Tasks MUST NOT be marked complete or validation claimed without
evidence.

## Governance

This constitution takes precedence over conflicting project practices and implementation choices. Amendments MUST
be explicit, justified, and semantically versioned. Relevant project artifacts MUST be updated when an amendment
changes requirements or workflow. The Coding Agent MUST check proposed work against these principles before
implementation and before reporting completion. Every proposed amendment MUST be reviewed for compliance with the existing constitution before approval.

**Version**: 1.0.0 | **Ratified**: 2026-11-22 | **Last Amended**: 2026-11-22
