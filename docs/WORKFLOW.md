# Workflow

## High-Level Flow

1. Mobile app sends a command
2. Backend validates the request
3. State is updated in PostgreSQL
4. A domain event is recorded
5. An outbox message is created
6. Event is published through NATS
7. Realtime layer pushes updates to clients
8. Mobile app and admin panel reconcile their local state

## Key Concepts

### Command
An action requested by the client or an operator.

Examples:
- StartSession
- UpdateBasket
- ForceStepBack
- ResumeSession

### Event
A recorded fact that something happened.

Examples:
- SessionStarted
- BasketUpdated
- StepForcedBack
- OperatorIntervened

### Operator Intervention
Any manual change performed by an operator.

Every intervention should record:
- who performed it
- what changed
- why it changed
- before and after state when relevant
- timestamp
- related session identifier

## Recovery Flow Example

1. User is stuck on the payment step
2. Operator opens the session in the admin panel
3. Operator modifies the basket or forces the step back
4. Backend records the intervention and emits an event
5. Mobile app receives the update
6. UI is corrected and the user can continue

## Realtime Sync
- backend publishes state change events
- clients subscribe through the realtime channel
- clients reconcile local state with server state
- server state wins when conflict exists