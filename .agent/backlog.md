# Lothal.FlowRecovery - Implementation Backlog

## Phase 1 Focus
Operator-driven flow recovery backend with:
- Clear documentation describing operator-driven recovery design
- Session and Workflow behavior preserved
- Operations module for recovery coordination
- RecoveryCase domain model
- Stale session detection and listing
- Manual recovery actions with audit trails
- Build and test validation

## Phase 1 Backlog Items

### TASK-001: Update project documentation for operator-driven recovery
**Status:** done
**Goal:** Clarify the project as an operator-driven recovery system, not autonomous repair.  
**Acceptance Criteria:**
- README.md emphasizes "operator-driven" and "explicit recovery actions"
- ARCHITECTURE.md mentions Operations module as coordinator
- PROJECT_OVERVIEW.md clarifies non-goals: no autonomous repair, no advanced recovery logic
- No vague claims about automatic fixes

**Validation:**
- Read updated README.md and verify language matches MVP vision
- Confirm "operator-driven" appears prominently in first section

**Commit Message:** docs: clarify operator-driven recovery design in README and ARCHITECTURE

---

### TASK-002: Create Operations module scaffold
**Status:** done
**Goal:** Introduce the Operations module structure and solution wiring.
**Acceptance Criteria:**
- `backend/src/Modules/Operations/` directory exists
- `Lothal.FlowRecovery.Modules.Operations.csproj` created with minimal class library setup
- `OperationsModule` class exists in the Operations project
- Solution (.sln) includes the new Operations project
- Build succeeds without warnings
- No tests required beyond build validation at scaffold stage

**Validation:**
- `dotnet restore && dotnet build` from backend/
- Solution contains the Operations project
- No compilation errors

**Commit Message:** feat: add Operations module scaffold

---

### TASK-003: Add RecoveryCase domain model
**Status:** done
**Goal:** Create the RecoveryCase entity representing a recovery session for a stale flow.  
**Acceptance Criteria:**
- `RecoveryCase` class lives in `Operations/Domain/RecoveryCase.cs`
- RecoveryCase has: Id, SessionId, CreatedAtUtc, CreatedByOperatorId, Status (New, InProgress, Resolved, Abandoned)
- RecoveryCase events: RecoveryCaseOpened, RecoveryCaseStatusChanged, RecoveryActionRecorded
- Events are immutable, store operator id and reason
- No persistence; domain model only
- Tests exist for RecoveryCase construction and status transitions

**Validation:**
- `dotnet test` passes for new domain tests
- RecoveryCase can be instantiated with required fields
- Events record operator id and reason

**Commit Message:** feat: add RecoveryCase domain model with audit events

---

### TASK-004: Add ListStaleActiveSessions query to Operations module
**Status:** done
**Goal:** Enable operators to list sessions that are stale and require recovery.  
**Acceptance Criteria:**
- OperationsModule exposes `GetRecoveryCandidates(staleBeforeUtc)` query
- Query returns sessions from SessionModule that are stale and active
- Returns snapshot with SessionId, FlowId, CurrentStep, LastEventUtc
- No mutations; read-only operation
- Tests verify candidates are correctly filtered by stale threshold

**Validation:**
- `dotnet test` passes
- Query correctly filters active sessions by stale threshold
- No session state is modified

**Commit Message:** feat: add recovery candidates query to Operations module

---

### TASK-005: Create OpenRecoveryCase operation
**Status:** done
**Goal:** Allow operators to open a recovery case for a stale session.  
**Acceptance Criteria:**
- `OperationsModule.OpenRecoveryCase(sessionId, staleBeforeUtc, operatorId, reason)` creates RecoveryCase only for stale active sessions
- RecoveryCaseOpened event is recorded with operator metadata
- Recovery case is stored and can be retrieved
- Idempotent: opening same session twice returns existing non-terminal case and records a duplicate-open audit action
- Tests cover happy path and idempotency

**Validation:**
- `dotnet test` passes
- RecoveryCaseOpened event contains operator id and reason
- Operator can retrieve the opened case

**Commit Message:** feat: add OpenRecoveryCase operation with operator audit

---

### TASK-006: Add ManualEndSessionRecovery action
**Status:** done
**Goal:** Enable operators to manually end a stale session via recovery workflow.  
**Acceptance Criteria:**
- `OperationsModule.ManualEndSessionRecovery(recoveryId, operatorId, reason)` calls SessionModule.EndSession
- EndSession is called with operator metadata (id and reason)
- RecoveryActionRecorded event captures: operatorId, reason, action (EndSession), timestamp
- Action is idempotent; repeated attempts record Operations audit and do not duplicate SessionEnded events
- Tests verify operator metadata is preserved end-to-end

**Validation:**
- `dotnet test` passes
- RecoveryActionRecorded event has correct operator metadata
- Session is ended with audit trail preserved
- SessionEnded event is recorded exactly once for repeated manual end recovery

**Commit Message:** feat: add ManualEndSessionRecovery with operator audit

---

### TASK-007: Wire Operations module into solution and enable integration
**Status:** done
**Goal:** Ensure Operations module integrates cleanly with Session and Workflow without breaking existing behavior.  
**Acceptance Criteria:**
- Solution builds successfully with all modules
- No circular dependencies between modules
- OperationsModule can be injected alongside SessionModule
- Existing Session and Workflow tests still pass
- No breaking changes to public contracts

**Validation:**
- `dotnet restore && dotnet build` from backend/
- `dotnet test` passes all existing tests
- New Operations tests pass
- No compiler warnings

**Commit Message:** feat: integrate Operations module with Session workflow

---

### TASK-008: Add audit trail documentation
**Status:** done
**Goal:** Document the audit trail for recovery operations in code and README.  
**Acceptance Criteria:**
- `docs/AUDIT_TRAIL.md` created describing recovery audit events
- Lists: RecoveryCaseOpened, RecoveryCaseStatusChanged, RecoveryActionRecorded
- Shows required fields: operatorId, reason, timestamp
- Gives example of a complete recovery flow with audit trail
- Updated `docs/ARCHITECTURE.md` to mention audit requirements for Operations

**Validation:**
- Markdown files are valid and linkable
- Example audit trail is correct and illustrative
- References match domain model

**Commit Message:** docs: add audit trail documentation

---

### TASK-009: Verify build and test suite
**Status:** done
**Goal:** Final validation that the MVP compiles, tests pass, and builds cleanly.  
**Acceptance Criteria:**
- `dotnet restore` succeeds
- `dotnet build` succeeds with no warnings (or documented ignored warnings)
- `dotnet test` passes all tests (Session, Workflow, Realtime, Operations)
- No compiler errors or unhandled warnings
- Solution is buildable from clean state

**Validation:**
- Run all three commands in sequence from `backend/`
- All exit codes are 0
- Test output shows all tests passing
- No unresolved dependencies

**Commit Message:** chore: record final MVP verification

---

### TASK-010: Normalize Operations boundary error handling
**Status:** done
**Goal:** Make Operations boundary methods consistently return explicit result objects for expected business failures.
**Acceptance Criteria:**
- `OpenRecoveryCase` reports validation and eligibility failures through a result type instead of throwing expected boundary errors
- Domain objects still throw for invariant violations and invalid lifecycle usage
- `ManualEndSessionRecovery` result behavior remains compatible with existing tests
- Tests cover missing input, missing session, non-stale session, ended session, and terminal recovery case outcomes
- No Session or Workflow behavior changes

**Validation:**
- `dotnet restore`
- `dotnet build`
- `dotnet test`

**Commit Message:** refactor: normalize operations boundary failures

---

## Phase 2 Focus
Operations API Surface

The current in-memory Operations MVP needs a thin application-facing API layer so operators or external clients can exercise recovery flows without calling module code directly.

Phase 2 constraints:
- Do not add persistence yet
- Do not add authentication or authorization yet
- Do not add UI yet
- Do not add new recovery actions yet
- Keep the API thin and operator-driven
- Do not expose mutable domain objects directly
- Operations remains the owner of recovery coordination
- Session remains the owner of session state

## Phase 2 Backlog Items

### TASK-011: Add Operations API contracts
**Status:** done
**Goal:** Define request/response contracts for the recovery API without implementing endpoints yet.
**Acceptance Criteria:**
- Request and response DTOs exist for listing recovery candidates
- Request and response DTOs exist for opening a recovery case
- Request and response DTOs exist for manual end-session recovery
- Request and response DTOs exist for getting recovery case detail if supported by the current module
- DTOs do not expose internal domain objects directly
- Validation expectations are documented near the contracts or related API layer
- Build passes without changing production behavior

**Validation:**
- `dotnet restore`
- `dotnet build`
- `dotnet test`

**Commit Message:** feat: add operations API contracts

---

### TASK-012: Expose recovery candidate endpoint
**Status:** done
**Goal:** Add an endpoint that lists stale active sessions as recovery candidates.
**Acceptance Criteria:**
- Endpoint accepts a stale cutoff or stale duration in a clear way
- Endpoint returns recovery candidate DTOs
- Endpoint does not mutate state
- Tests cover stale and non-stale behavior through the API or application layer
- Build and tests pass

**Validation:**
- `dotnet restore`
- `dotnet build`
- `dotnet test`

**Commit Message:** feat: expose recovery candidates endpoint

---

### TASK-013: Expose open recovery case endpoint
**Status:** done
**Goal:** Add an endpoint to open a recovery case for a stale active session.
**Acceptance Criteria:**
- Endpoint accepts `sessionId`, `operatorId`, `reason`, and a stale cutoff or duration
- Expected failures return clear error responses
- Success returns recovery case information through DTOs
- Duplicate open behavior remains explicit
- Tests cover success and expected failures
- Build and tests pass

**Validation:**
- `dotnet restore`
- `dotnet build`
- `dotnet test`

**Commit Message:** feat: expose open recovery case endpoint

---

### TASK-014: Expose manual end session recovery endpoint
**Status:** done
**Goal:** Add an endpoint for operator-driven `ManualEndSessionRecovery`.
**Acceptance Criteria:**
- Endpoint accepts `recoveryCaseId`, `operatorId`, and `reason`
- Expected failures return clear error responses
- Success returns recovery action result information
- Repeated and idempotent attempts are represented clearly
- Tests cover success, invalid input, and terminal/idempotent behavior
- Build and tests pass

**Validation:**
- `dotnet restore`
- `dotnet build`
- `dotnet test`

**Commit Message:** feat: expose manual end session recovery endpoint

---

### TASK-015: Add API usage documentation
**Status:** done
**Goal:** Document the end-to-end recovery API flow.
**Acceptance Criteria:**
- Documentation explains listing recovery candidates
- Documentation explains opening a recovery case
- Documentation explains manual end-session recovery
- Documentation explains repeated or idempotent recovery attempts
- Documentation explains expected error responses
- Includes curl or HTTP examples if the project has HTTP endpoints
- Mentions current in-memory limitations
- Build and tests pass

**Validation:**
- `dotnet restore`
- `dotnet build`
- `dotnet test`
- Documentation review

**Commit Message:** docs: add operations API usage guide

---

### TASK-016: Record Phase 2 verification
**Status:** done
**Goal:** Run and record final Phase 2 verification.
**Acceptance Criteria:**
- `dotnet restore`, `dotnet build`, and `dotnet test` pass
- Test count is recorded
- `.agent/project-state.md` is updated with Phase 2 status
- `.agent/done.md` is updated
- Remaining limitations are documented

**Validation:**
- `dotnet restore`
- `dotnet build`
- `dotnet test`
- Documentation review

**Commit Message:** chore: record phase 2 verification

---

## Backlog Status Summary
- **Total Items:** 16
- **Todo:** 0
- **In-Progress:** 0
- **Done:** 16

## Next Planned Stage
- Phase 1 MVP is complete for the current in-memory Operations implementation
- Phase 2 Operations API surface is complete for the current in-memory implementation
