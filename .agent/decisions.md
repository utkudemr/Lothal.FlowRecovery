# Lothal.FlowRecovery - Architecture Decisions

## Overview
This file records architecture and product decisions made during the autonomous backlog workflow.
Decisions are categorized by domain and include rationale.

## Accepted Decisions (From existing docs/memory/decisions.md)

### Core Design Principles
1. **Server State as Source of Truth**
   - Mobile app state is not trusted for recovery decisions
   - All important decisions are based on server-side session state
   - Backup server state from previous snapshots when needed

2. **Event Sourcing for Audit**
   - All important state changes are recorded as immutable events
   - Events are append-only and form the audit trail
   - Snapshots can be computed from event history

3. **Operator-Driven Recovery**
   - The system does NOT autonomously repair flows
   - All recovery actions require explicit operator intervention
   - Operator id and reason are mandatory for auditing

4. **Idempotent Operations**
   - EndSession is idempotent; repeating it records SessionEndAlreadyEndedAudit
   - Other recovery actions should be designed for safe repetition

5. **Session Event History is Append-Only**
   - Historical events are never deleted or modified
   - State is computed from event sequence, not stored directly
   - Enables full auditability and event replay

### Architecture Style
6. **Modular Monolith with Microservice Boundaries**
   - Single deployment but clear module boundaries
   - Can be split into independent services later without code changes
   - Modules: Session, Basket, Workflow, Operations, Realtime
   - No circular dependencies between modules

### Current Runtime Constraints (Intentional for MVP)
7. **In-Memory Store Only**
   - No PostgreSQL persistence yet (planned for later)
   - No Redis caching or locks
   - Single-process runtime
   - Sufficient for validation and feature development

## MVP-Phase Decisions

### Decision: Do Not Hardcode Codex Model Names in Workflow Files
**Proposed:** Repository workflow files should avoid hardcoded Codex model names and prefer the default Codex model unless the user explicitly selects a supported override.
**Rationale:**
- ChatGPT sign-in and API key sign-in may expose different model availability
- Hardcoded Codex-only model names can fail for agents using ChatGPT sign-in
- Workflow documentation should remain portable across supported Codex authentication modes

**Status:** Accepted

### Decision: Operations Module Coordination Role
**Proposed:** Operations module coordinates manual recovery workflows.  
**Rationale:** 
- Keeps Session module focused on session state
- Keeps Workflow module focused on validation
- Operations handles recovery orchestration without mutating Session internals
- Cleaner dependency: Operations depends on Session/Workflow, not vice versa

**Status:** Accepted for MVP

### Decision: Recovery Case as Domain Entity
**Proposed:** RecoveryCase is a domain entity in Operations, separate from Session.  
**Rationale:**
- Recovery is a distinct concern from session lifecycle
- Operator metadata (id, reason) lives with recovery, not session
- Allows multiple recovery attempts for one session
- Supports recovery case status tracking independently

**Status:** Accepted for MVP

### Decision: Manual Recovery Actions Require Operator Metadata
**Proposed:** All recovery actions require operatorId and reason (mandatory, not optional).  
**Rationale:**
- Supports full auditability and operator accountability
- Enables future approval workflows or audit queries
- Prevents accidental recovery actions from being invisible
- Aligns with system principles

**Status:** Accepted for MVP

### Decision: Recovery Actions are Operations Concerns, Not Session Concerns
**Proposed:** Operations module initiates recovery actions; Session module records them with provided metadata.  
**Rationale:**
- Clear separation of concerns
- Operations is the coordinator; Session is the state holder
- Session doesn't need to know about recovery workflows
- Enables recovery actions to be composed (e.g., multiple steps)

**Status:** Accepted for MVP

## Future Decisions (Not Yet Implemented)

### To Be Decided
- Persistence layer (PostgreSQL schema for sessions and recovery cases)
- Redis integration for distributed locks
- NATS integration for inter-module messaging
- API layer (HTTP/gRPC contract for recovery operations)
- Operator authentication and authorization
- Recovery action approval workflows
- Read models and audit projections

---

## Decision Log Format

For future decisions, use this format:

### Decision: [Title]
**Proposed:** [What is proposed]  
**Rationale:** [Why this is the best choice]  
**Alternatives Considered:** [Other options and why they were rejected]  
**Status:** [Accepted | Proposed | Rejected | Deferred]  
**Date:** [When decided]  
**Relates To:** [Other decisions or backlog items]
