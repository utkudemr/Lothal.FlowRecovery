# Workflow

## High-Level Flow

1. Client or operator sends a command
2. Backend validates the request
3. Session state is updated in the in-memory store
4. A domain event is appended to the session history
5. `GetSession` reads the snapshot read model
6. Realtime updates can be pushed from the current state
7. Clients reconcile their local state with server state

## Key Concepts

### Command
An action requested by the client or an operator.

Examples:
- StartSession
- GetSession
- EndSession
- SetCurrentStep

### Event
A recorded fact that something happened.

Examples:
- SessionStarted
- SessionCurrentStepSet
- SessionEnded
- SessionEndAlreadyEndedAudit

### Operator Intervention
Any manual change performed by an operator.

Every intervention should record:
- who performed it
- what changed
- why it changed
- before and after state when relevant
- timestamp
- related session identifier

### Snapshot Read Model
`GetSession` returns the current snapshot for a session.

The snapshot includes:
- session identity and flow metadata
- current status
- current step
- timestamps
- append-only event history

## Session Lifecycle

1. `StartSession` creates an active session and records `SessionStarted`
2. `GetSession` returns the current snapshot read model
3. `SetCurrentStep` updates the current page or step for an active session
4. `EndSession` marks the session ended and records `SessionEnded`
5. Repeating `EndSession` on an already ended session is idempotent and records `SessionEndAlreadyEndedAudit`

## Recovery Flow Example

1. User is stuck on the payment step
2. Operator opens the session in the admin panel
3. Operator sets the current step back or corrects it
4. Backend records the intervention and appends an audit event
5. Mobile app receives the update
6. UI is corrected and the user can continue

## Realtime Sync
- backend publishes state change events
- clients subscribe through the realtime channel
- clients reconcile local state with server state
- server state wins when conflict exists
