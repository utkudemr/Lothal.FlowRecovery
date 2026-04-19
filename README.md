# Lothal.FlowRecovery

Lothal.FlowRecovery is a backend system for recovering and managing stuck sales flows in a Flutter-based sales application.

## Project Overview

The system exists to restore control when a sales flow becomes blocked, inconsistent, or abandoned. It gives operators a structured way to inspect and recover sessions while preserving an auditable history of important actions.

## Problem Statement

Sales flows can get stuck because of client errors, interrupted sessions, or partial workflow completion. In those cases, the backend must support operator intervention without losing traceability. This project is designed to make that intervention explicit, safe, and reviewable.

## Architecture Overview

The codebase follows a modular monolith structure with clear module boundaries:

- `Session`
- `Basket`
- `Workflow`
- `Operations`
- `Realtime`

The architecture is event-driven by design. Important state transitions are represented as events so the system can support auditability, recovery, and later integration with external messaging or read models.

## Session Module

The `Session` module is the first implemented slice and defines the core lifecycle for a recovered flow session.

### `StartSession`

Creates a new session for a flow when no active session already exists. The command validates the required input, creates a session started event, and stores the session in the current in-memory store.

### `GetSession`

Returns the current session snapshot by session identifier. This is the read path for inspection and recovery workflows.

### `EndSession`

Ends an existing session using operator metadata. The command records the end action, updates session state, and preserves the relevant audit information.

## Audit and Event-Based Design

The system treats key workflow changes as recorded events rather than opaque state mutations. This supports:

- traceability of operator actions
- recovery of flow history
- clearer debugging and operational review
- future persistence and integration options

Operator interventions should remain explicit and auditable.

## Current Technical State

This repository is still in an early technical stage:

- state is held in an in-memory store
- no persistence layer is implemented yet
- the current implementation assumes a single-process runtime

These constraints are intentional for the current phase and keep the first vertical slice small and testable.

## Build and Test

From the `backend` directory:

```powershell
dotnet build
dotnet test
```

## Development Approach

The repository is organized around an agent-based workflow:

- `planner` defines scope and sequence
- `coder` implements small, targeted changes
- `reviewer` checks correctness and regression risk
- `tester` focuses on validation and critical branches

Development should follow a vertical slice strategy: implement one end-to-end flow at a time, keep changes narrow, and avoid broad refactors until they are justified by the next slice.
