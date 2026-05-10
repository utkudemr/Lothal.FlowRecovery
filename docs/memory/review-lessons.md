# Review Lessons

Recurring review lessons that should influence future work.

## Lessons

- Opaque state mutation reduces traceability; important changes need event recording.
- Repeating `EndSession` should not mutate state; it should append an audit event.
- Recovery workflows are safer when clients reconcile with server state and server state wins on conflicts.
