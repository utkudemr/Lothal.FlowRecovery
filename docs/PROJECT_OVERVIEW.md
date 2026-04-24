# Lothal.FlowRecovery - Project Overview

## Purpose
Lothal.FlowRecovery is a .NET backend system designed to recover and manage stuck sales flows in a Flutter-based sales application.

## Problem
The mobile sales application can become stuck due to:
- inconsistent UI state
- invalid transitions between steps
- network issues or partial updates
- incorrect basket data

When this happens, the user cannot proceed and the sale may be lost.

## Goal
Provide an operations-driven recovery system that allows:
- tracking active sessions
- inspecting the current workflow step through `GetSession`
- starting sessions with `StartSession`
- updating the current step with `SetCurrentStep`
- ending sessions with `EndSession`
- synchronizing corrected state back to the mobile app in real time

## Key Capabilities
- session tracking
- snapshot-based session reads
- current step control and rollback
- operator action auditing
- append-only session event history
- realtime synchronization with clients

## MVP Scope
- start and track sessions
- read session snapshots
- maintain current workflow step
- record all operator interventions as events
- push updates to mobile clients

## Non-Goals (Initial Phase)
- full product catalog management
- complex payment integrations
- distributed microservices deployment
- persistence and distributed consistency

Current implementation is in-memory and single-process. The system starts as a modular monolith and evolves over time.
