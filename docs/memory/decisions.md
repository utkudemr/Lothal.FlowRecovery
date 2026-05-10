# Decisions

Accepted durable product, architecture, and workflow decisions.

## Accepted Decisions

- Server state is the source of truth.
- Important state changes are recorded as events.
- Operator interventions are auditable.
- Session event history is append-only.
- `EndSession` is idempotent; repeating it on an ended session records `SessionEndAlreadyEndedAudit`.
- Current runtime is in-memory and single-process.
- Architecture style is a modular monolith with microservice-ready boundaries.
