# Lothal.FlowRecovery - Project State

## Current Implementation Status

### Modules Implemented
1. **Session Module**
   - Commands: StartSession, GetSession, EndSession, SetCurrentStep
   - Queries: GetActiveSessionByFlowId, ListActiveSessions, ListStaleActiveSessions
   - Event-sourced with append-only history
   - In-memory store
   - Fully tested

2. **Workflow Module**
   - Validates workflow steps and transitions
   - InMemoryWorkflowDefinitionProvider for testing
   - Supports ValidateWorkflowInitialStep, ValidateWorkflowTransition, ValidateWorkflowCurrentStep
   - Fully tested

3. **Realtime Module**
   - In-memory pub/sub for session notifications
   - Session-scoped and flow-scoped subscriptions
   - Emits notifications for: SessionStartedEvent, SessionCurrentStepSetEvent, SessionEndedEvent
   - Fully tested

4. **Operations Module**
   - Coordinates operator-driven recovery workflows
   - Exposes stale active sessions as recovery candidates
   - Provides RecoveryCase domain model with New, InProgress, Resolved, and Abandoned statuses
   - Records RecoveryCaseOpened, RecoveryCaseStatusChanged, and RecoveryActionRecorded events
   - Opens recovery cases only for stale active sessions
   - Requires operator id and reason for recovery case opening and manual recovery actions
   - Enforces explicit recovery case lifecycle transitions: New -> InProgress -> Resolved or Abandoned
   - Supports duplicate-open audit behavior for existing non-terminal recovery cases
   - Provides ManualEndSessionRecovery through SessionModule.EndSession
   - Makes repeated manual EndSession recovery idempotent and audited
   - Avoids duplicate SessionEnded events on repeated manual recovery
   - Rejects normal recovery actions on resolved or abandoned cases while allowing explicit idempotent audit records
   - Documents the current audit trail contract in `docs/AUDIT_TRAIL.md`
   - Fully tested for current in-memory MVP behavior

### Modules Planned (Not Yet Implemented)
- **Basket Module** - not started

### Infrastructure
- Solution: `backend/Lothal.FlowRecovery.sln`
- Test framework: xUnit
- Build: `dotnet build` from backend/ directory
- Test: `dotnet test` from backend/ directory

### Code Style & Conventions
- Modular monolith with clear module boundaries
- Event-sourcing for important state changes
- Immutable events with audit metadata
- Commands and queries separate
- In-memory store (no persistence yet)
- Single-process runtime (no distributed consistency)

## Current Known Limitations

### By Design (Intentional for MVP)
- **In-memory store only** - no PostgreSQL persistence yet
- **Single-process runtime** - no distributed consistency
- **No Redis** - no caching or distributed locks
- **No NATS** - no inter-service messaging yet
- **No API layer** - no HTTP/gRPC exposure yet
- **No UI** - backend-only development
- **No authentication/authorization** - operator identity is passed as metadata only

### Known Issues or Gaps
- Basket module is not implemented yet
- Persistence for sessions and recovery cases is not implemented yet
- Operator authentication and authorization are not implemented yet
- API layer for operator workflows is not implemented yet
- Operations boundary error handling is mixed and tracked for normalization in `.agent/backlog.md`

### Documentation Gaps
- No dedicated Operations module README yet
- Audit trail documentation exists in `docs/AUDIT_TRAIL.md`
- Agent bookkeeping was reconciled after the Operations multi-commit batch

## Design Decisions (From decisions.md)
- Server state is the source of truth
- Important state changes are recorded as events
- Operator interventions are auditable
- Session event history is append-only
- EndSession is idempotent; repeating it records SessionEndAlreadyEndedAudit
- Current runtime is in-memory and single-process
- Architecture style is modular monolith with microservice-ready boundaries
- Agent bookkeeping files must be reconciled after autonomous or multi-commit batches

## Repository Structure
```
backend/
  src/
    Modules/
      Session/
        Lothal.FlowRecovery.Modules.Session/
      Workflow/
        Lothal.FlowRecovery.Modules.Workflow/
      Realtime/
        Lothal.FlowRecovery.Modules.Realtime/
      Operations/
        Lothal.FlowRecovery.Modules.Operations/
  tests/
    Lothal.FlowRecovery.Modules.Session.Tests/
    Lothal.FlowRecovery.Modules.Workflow.Tests/
    Lothal.FlowRecovery.Modules.Realtime.Tests/
    Lothal.FlowRecovery.Modules.Operations.Tests/
  Lothal.FlowRecovery.sln
```

## Supported .NET Version
Inferred from solution structure; check Directory.Build.props for exact target framework.

## Build & Test Status
- **Build Command:** `dotnet build` from backend/
- **Test Command:** `dotnet test` from backend/
- **Last Known Validation:** `dotnet restore`, `dotnet build`, and `dotnet test` passed after audit trail documentation; latest observed suite total was 208 passing tests.

## Next Phase: MVP Completion
See `.agent/backlog.md` for detailed task list.

Remaining MVP work:
1. Run and record final MVP build/test verification
2. Normalize Operations boundary error handling if selected before MVP closeout
