# Operations API Guide

## Purpose

The Operations API exposes the current operator-driven recovery flow over a thin HTTP surface. It exists so external callers can inspect recovery candidates and perform explicit recovery actions without calling module code directly.

This API does not autonomously repair business flows. Recovery remains operator-driven, auditable, and constrained by the current in-memory MVP rules.

## Current Endpoints

### `GET /operations/recovery-candidates`

Lists stale active sessions that may need operator review.

Query options:

- `staleBeforeUtc=<ISO-8601 timestamp>`
- `staleForMinutes=<positive number>`

Provide exactly one stale boundary.

Example:

```bash
curl "http://localhost:5000/operations/recovery-candidates?staleBeforeUtc=2026-06-13T12:30:00.0000000Z"
```

Successful response example:

```json
[
  {
    "sessionId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    "flowId": "checkout-flow-123",
    "currentStep": "payment",
    "lastEventAtUtc": "2026-06-13T12:10:00Z"
  }
]
```

Expected validation failure example:

```json
{
  "errors": {
    "staleBoundary": [
      "Provide exactly one of staleBeforeUtc or staleFor."
    ]
  }
}
```

### `POST /operations/recovery-cases`

Opens a recovery case for a stale active session.

Request body:

```json
{
  "sessionId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
  "operatorId": "operator-001",
  "reason": "Session has been inactive past the recovery cutoff.",
  "staleBeforeUtc": "2026-06-13T12:30:00Z",
  "staleFor": null
}
```

Provide exactly one stale boundary via `staleBeforeUtc` or `staleFor`.

Successful response example:

```json
{
  "success": true,
  "recoveryCase": {
    "recoveryCaseId": "11111111-2222-3333-4444-555555555555",
    "sessionId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    "createdAtUtc": "2026-06-13T12:31:00Z",
    "createdByOperatorId": "operator-001",
    "status": "New",
    "events": [
      {
        "eventType": "RecoveryCaseOpened",
        "operatorId": "operator-001",
        "reason": "Session has been inactive past the recovery cutoff.",
        "timestampUtc": "2026-06-13T12:31:00Z",
        "actionName": null,
        "newStatus": null,
        "sessionId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
      }
    ]
  },
  "error": null
}
```

Duplicate open behavior remains explicit. Repeating the same request for a non-terminal recovery case returns the same recovery case and appends a `RecoveryActionRecorded` event with action `OpenRecoveryCaseDuplicate`.

Expected error response example:

```json
{
  "success": false,
  "recoveryCase": null,
  "error": "Recovery case can only be opened for a stale active session."
}
```

### `POST /operations/recovery-cases/manual-end-session`

Runs operator-driven manual end-session recovery.

Request body:

```json
{
  "recoveryCaseId": "11111111-2222-3333-4444-555555555555",
  "operatorId": "operator-001",
  "reason": "Customer abandoned the flow; end the stuck session."
}
```

Successful response example:

```json
{
  "success": true,
  "error": null,
  "outcome": "SessionEnded"
}
```

Repeated or idempotent attempt example:

```json
{
  "success": true,
  "error": null,
  "outcome": "AlreadyEnded"
}
```

Expected error response example:

```json
{
  "success": false,
  "error": "Recovery case is already terminal.",
  "outcome": null
}
```

## End-to-End Flow

1. Call `GET /operations/recovery-candidates` with a stale cutoff or stale duration.
2. Choose a stale active session that needs operator review.
3. Call `POST /operations/recovery-cases` with `sessionId`, `operatorId`, `reason`, and a stale boundary.
4. Review the recovery case response and confirm the case is open.
5. Call `POST /operations/recovery-cases/manual-end-session` with `recoveryCaseId`, `operatorId`, and `reason`.
6. If the same manual recovery is repeated after the session is already ended, expect `outcome: "AlreadyEnded"` rather than a duplicate business state transition.

## Error Handling Notes

- Recovery endpoints return explicit failure payloads for expected business failures.
- `GET /operations/recovery-candidates` returns validation-problem style errors when the stale boundary query is invalid.
- `POST /operations/recovery-cases` returns a failure payload when the session is missing, ended, not stale, or the case is terminal.
- `POST /operations/recovery-cases/manual-end-session` returns a failure payload for missing recovery cases, invalid input, or terminal recovery cases.

## Current Limitations

- State and audit history are still stored in memory.
- Audit history is append-only by design but not durable until persistence exists.
- Authentication and authorization are not implemented yet.
- The API is intentionally thin and currently covers only the recovery candidate query and the manual recovery flow.
- The current API surface is suitable for development and integration exercises, not production deployment.
