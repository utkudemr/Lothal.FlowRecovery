# Architecture

## Style
The system is implemented as a modular monolith with microservice-friendly boundaries.

## Core Modules
- Session: manages user sessions and current flow state
- Basket: stores and updates basket data
- Workflow: controls step transitions and rollback
- Operations: handles operator interventions
- Realtime: delivers updates to mobile and admin clients

## Data Strategy
- PostgreSQL: source of truth
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

## Evolution Strategy
The system is designed to be split into microservices later if needed:
- Session service
- Basket service
- Workflow service
- Operations service
- Realtime service

Initially, everything runs in a single backend for simplicity and faster iteration.