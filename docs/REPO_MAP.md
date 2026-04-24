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
- Session module is the only implemented module
- Session uses an in-memory shared store
- Session exposes `StartSession`, `SetCurrentStep`, `EndSession`, and `GetSession`
