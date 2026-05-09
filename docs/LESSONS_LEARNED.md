# Lessons Learned

- Opaque state mutation reduces traceability; event recording is required for important changes.
- Repeating `EndSession` should not mutate state; it should append an audit event.
- Recovery workflows are safer when clients reconcile with server state and server state wins on conflicts.
