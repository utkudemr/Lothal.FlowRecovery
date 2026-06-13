# Lothal.FlowRecovery - Project State

## Current Implementation Status

### Modules Implemented
1. **Session Module** ✓
   - Commands: StartSession, GetSession, EndSession, SetCurrentStep
   - Queries: GetActiveSessionByFlowId, ListActiveSessions, ListStaleActiveSessions
   - Event-sourced with append-only history
   - In-memory store
   - Fully tested

2. **Workflow Module** ✓
   - Validates workflow steps and transitions
   - InMemoryWorkflowDefinitionProvider for testing
   - Supports ValidateWorkflowInitialStep, ValidateWorkflowTransition, ValidateWorkflowCurrentStep
   - Fully tested

3. **Realtime Module** ✓
   - In-memory pub/sub for session notifications
   - Session-scoped and flow-scoped subscriptions
   - Emits notifications for: SessionStartedEvent, SessionCurrentStepSetEvent, SessionEndedEvent
   - Fully tested

### Modules Planned (Not Yet Implemented)
- **Basket Module** - not started
- **Operations Module** - planned structure; not implemented yet

### Infrastructure
- Solution: `backend/Lothal.FlowRecovery.sln`
- Test framework: xUnit (inferred from test projects)
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

### Known Issues or Gaps
- RecoveryCase domain model does not exist yet
- Operations module is not structured yet
- Recovery actions are not coordinated yet
- Audit trail for recovery operations is not formalized
- No integration between Operations and Session for recovery workflows

### Documentation Gaps
- README does not explicitly state "operator-driven" (should be emphasized)
- No dedicated Operations module README yet
- No audit trail design document

## Design Decisions (From decisions.md)
- Server state is the source of truth
- Important state changes are recorded as events
- Operator interventions are auditable
- Session event history is append-only
- EndSession is idempotent; repeating it records SessionEndAlreadyEndedAudit
- Current runtime is in-memory and single-process
- Architecture style is modular monolith with microservice-ready boundaries

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
  tests/
    Lothal.FlowRecovery.Modules.Session.Tests/
    Lothal.FlowRecovery.Modules.Workflow.Tests/
    Lothal.FlowRecovery.Modules.Realtime.Tests/
  Lothal.FlowRecovery.sln
```

## Supported .NET Version
Inferred from solution structure; check Directory.Build.props for exact target framework.

## Build & Test Status
- **Build Command:** `dotnet build` from backend/
- **Test Command:** `dotnet test` from backend/
- **Last Build Status:** Not recorded (to be verified during backlog execution)

## Next Phase: MVP Completion
See `.agent/backlog.md` for detailed task list.

The MVP will:
1. Clarify operator-driven design in documentation
2. Introduce Operations module scaffold
3. Add RecoveryCase domain model
4. Enable stale session queries
5. Support opening recovery cases
6. Provide manual EndSession recovery action
7. Record audit trails with operator metadata
8. Ensure all builds and tests pass cleanly
