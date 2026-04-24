# Domain Notes

## Current Terms
- `StartSession`: creates a new active session for a flow
- `GetSession`: returns the current snapshot read model for a session
- `SetCurrentStep`: updates the current page or step for an active session
- `EndSession`: ends an active session

## Event Behavior
- session history is append-only
- important state changes are recorded as events
- repeated `EndSession` calls on an already ended session create an audit event instead of changing state

## Current Implementation Limits
- in-memory only
- single-process only
- no persistence yet
- no distributed consistency yet

## Notes
- server state remains the source of truth
- session snapshots are read-side views built from the recorded session state and events
