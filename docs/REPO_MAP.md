# Repo Map

## Root Structure
- AGENTS.md -> global repository rules for agents
- docs/ -> architecture, workflow, and project context
- agents/ -> role definitions and behavior guidance
- backend/ -> .NET backend implementation
- docs/DOMAIN_NOTES.md -> domain terms and current implementation notes

## Planned Backend Structure

```text
backend/
  src/
    BuildingBlocks/
    Modules/
      Session/
      Basket/
      Workflow/
      Operations/
      Realtime/
```

## Current Backend State
- Session, Workflow, and Realtime modules exist in the current backend
- Workflow currently covers `ValidateWorkflowInitialStep`, `ValidateWorkflowTransition`, and `ValidateWorkflowCurrentStep`
- Session uses an in-memory shared store
- Session exposes `StartSession`, `SetCurrentStep`, `EndSession`, `GetSession`, `GetActiveSessionByFlowId`, and `ListActiveSessions`

## Current Test Projects
- `backend/tests/Lothal.FlowRecovery.Modules.Session.Tests/Lothal.FlowRecovery.Modules.Session.Tests.csproj`
- `backend/tests/Lothal.FlowRecovery.Modules.Workflow.Tests/Lothal.FlowRecovery.Modules.Workflow.Tests.csproj`
- `backend/tests/Lothal.FlowRecovery.Modules.Realtime.Tests/Lothal.FlowRecovery.Modules.Realtime.Tests.csproj`
