# Session Module

This module provides an in-memory session workflow for recovery of stuck sales flows.

It supports:
- starting a session for a flow
- updating the current step while the session is active; unchanged steps record a no-op `SessionCurrentStepUnchangedEvent`, do not change state, and do not emit a notification
- ending a session with operator/system metadata
- reading session snapshots with append-only event history

Scope currently includes:
- command handlers and result types
- session record, events, and audit metadata
- shared in-memory session store

Scope intentionally excludes:
- database persistence
- messaging, transport, or realtime infrastructure
- temporary in-Session event-to-notification boundary, which currently lives in Session and only supports `SessionCurrentStepSetEvent` for changed steps
- Basket, Workflow, and Operations module logic
