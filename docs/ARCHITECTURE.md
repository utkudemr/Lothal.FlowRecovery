# Architecture

## Style
The system is implemented as a modular monolith with microservice-friendly boundaries.
Current implementation runs in-memory in a single process.
Session and Workflow are the implemented modules today; Workflow is currently limited to `ValidateWorkflowTransition`.

## Core Modules
- Session: manages user sessions and current flow state
- Basket: planned module for basket data
- Workflow: currently only validates workflow transitions
- Operations: planned module for operator interventions
- Realtime: planned module for client updates

## Session Model
- commands: `StartSession`, `GetSession`, `EndSession`, `SetCurrentStep`
- `GetSession` returns a snapshot read model
- session events are append-only and preserved as audit history
- `EndSession` is idempotent and records an audit event when already ended
- `SetCurrentStep` is the current page or step control for an active session

## Data Strategy
- Current implementation: in-memory store only
- Planned data strategy: PostgreSQL as source of truth
- Redis: cache, locks, ephemeral state
- NATS: event-driven communication
- NoSQL (optional later): read models and audit projections

## Design Principles
- server state is the source of truth, not the mobile app
- all important state changes must be recorded as events
- operator interventions must be auditable
- workflow history should be append-only
- module coupling should stay minimal
- write-side and read-side concerns should remain separable

## Current Limits
- in-memory only
- single-process only
- no persistence yet
- no distributed consistency yet

## Contract Stability

This project is currently in an early development phase.

- Public module contracts (e.g. SessionModule results) are NOT considered stable yet
- Breaking changes in result shapes are allowed
- No backward compatibility is guaranteed at this stage

Stability guarantees will be introduced once external consumers are defined.

## Evolution Strategy
The system is designed to be split into microservices later if needed:
- Session service
- Basket service
- Workflow service
- Operations service
- Realtime service

Initially, everything runs in a single backend for simplicity and faster iteration.
