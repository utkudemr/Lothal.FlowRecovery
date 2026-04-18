# Lothal.FlowRecovery — Project Overview

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
- inspecting the current workflow step
- modifying basket contents
- moving the user backward or forward in the flow
- synchronizing corrected state back to the mobile app in real time

## Key Capabilities
- session tracking
- basket management with operator intervention
- workflow step control and rollback
- operator action auditing
- realtime synchronization with clients

## MVP Scope
- start and track sessions
- maintain current workflow step
- store basket state
- record all operator interventions
- push updates to mobile clients

## Non-Goals (Initial Phase)
- full product catalog management
- complex payment integrations
- distributed microservices deployment

The system starts as a modular monolith and evolves over time.