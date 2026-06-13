# Audit Trail

## Purpose

Lothal.FlowRecovery is an operator-driven recovery system. It helps operators inspect and recover stuck sales flows, but it does not autonomously repair business flows or decide recovery actions on its own.

The audit trail exists so every meaningful recovery decision can be reviewed later with:

- who performed the action
- why the action was performed
- when the action was recorded
- which session or recovery case was affected

## Current Scope

This document describes the current in-memory Operations MVP. Audit history is append-only in intent: recovery and session events are appended to event lists rather than replacing earlier entries. The current storage is in-memory and single-process only, so audit history is not durable across process restarts yet.

Persistence, external audit projections, authentication, authorization, and UI review tooling are not implemented in this MVP.

## Operator-Driven Recovery

Recovery actions require explicit operator input because they can affect business flow state. The system should not infer that a stale or blocked flow is safe to repair automatically.

Operations boundary methods require:

- `operatorId`: identifies the operator or system actor initiating the recovery step
- `reason`: records the business or operational reason for the intervention

These fields make recovery actions traceable and discourage opaque state mutation.

## Operations Audit Events

The Operations module records audit history through `RecoveryCase` events.

### `RecoveryCaseOpened`

Recorded when an operator opens a recovery case for a stale active session.

Required audit fields:

- `RecoveryCaseId`
- `SessionId`
- `OperatorId`
- `Reason`
- `TimestampUtc`

`OpenRecoveryCase` only opens cases for existing stale active sessions. It rejects nonexistent sessions, ended sessions, and active sessions that are not stale.

### `RecoveryCaseStatusChanged`

Recorded when a recovery case moves through its lifecycle.

Required audit fields:

- `RecoveryCaseId`
- `NewStatus`
- `OperatorId`
- `Reason`
- `TimestampUtc`

The current manual recovery lifecycle is:

- `New`
- `InProgress`
- `Resolved`

`Abandoned` is also a terminal status. Terminal cases do not accept normal recovery actions. Repeated or idempotent audit entries must use the explicit idempotent audit path.

### `RecoveryActionRecorded`

Recorded when an operator performs or repeats a recovery action.

Required audit fields:

- `RecoveryCaseId`
- `ActionName`
- `OperatorId`
- `Reason`
- `TimestampUtc`

Current action names include:

- `OpenRecoveryCaseDuplicate`: a duplicate open attempt for an existing non-terminal recovery case
- `EndSession`: a manual end-session recovery action
- `EndSessionAlreadyEnded`: an idempotent repeated manual end-session recovery attempt

## OpenRecoveryCase Audit Behavior

When an operator opens a recovery case for a stale active session, Operations creates a `RecoveryCase` and records `RecoveryCaseOpened`.

If a non-terminal case already exists for the session, Operations returns the existing case and records `OpenRecoveryCaseDuplicate` as a `RecoveryActionRecorded` event. This makes duplicate attempts visible without creating another recovery case.

## ManualEndSessionRecovery Audit Behavior

`ManualEndSessionRecovery` is an operator-approved recovery action. It coordinates with Session by calling `SessionModule.EndSession`; Operations does not directly mutate Session internals.

For a normal successful manual end-session recovery, the audit timeline is:

1. `RecoveryCaseStatusChanged` to `InProgress`
2. Session records one `SessionEndedEvent`
3. Operations records `RecoveryActionRecorded` with action `EndSession`
4. `RecoveryCaseStatusChanged` to `Resolved`

## Repeated ManualEndSessionRecovery

Repeated manual end-session recovery is idempotent when the session has already ended and the recovery case is resolved.

The system must not append duplicate `SessionEndedEvent` entries because the business state has not ended twice. Instead:

- Session records `SessionEndAlreadyEndedAuditEvent`
- Operations records `RecoveryActionRecorded` with action `EndSessionAlreadyEnded`
- the recovery case remains resolved

This preserves traceability for the repeated operator attempt without pretending that a second state transition occurred.

## Example Audit Timeline

Example flow:

1. A stale active session is detected by `GetRecoveryCandidates(staleBeforeUtc)`.
2. Operator `operator-001` opens a recovery case with reason `Checkout session has been inactive past the cutoff`.
   - Operations records `RecoveryCaseOpened`.
3. Operator `operator-001` approves manual end-session recovery with reason `Customer abandoned checkout; close stuck session`.
   - Operations records `RecoveryCaseStatusChanged` to `InProgress`.
   - Session records `SessionEndedEvent`.
   - Operations records `RecoveryActionRecorded` with action `EndSession`.
   - Operations records `RecoveryCaseStatusChanged` to `Resolved`.
4. Operator `operator-002` retries the same manual end-session recovery with reason `Second console retry after page refresh`.
   - Session does not record another `SessionEndedEvent`.
   - Operations records `RecoveryActionRecorded` with action `EndSessionAlreadyEnded`.
   - The recovery case remains `Resolved`.

## Limitations

- Audit history is currently stored in memory.
- Audit history is lost when the process restarts.
- Operator identity is accepted as metadata; authentication and authorization are not implemented.
- There is no audit export, query API, external projection, or operator UI yet.
- Distributed ordering and consistency are not implemented because the current runtime is single-process.
