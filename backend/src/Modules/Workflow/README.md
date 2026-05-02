# Workflow Module

This module currently contains only `ValidateWorkflowTransition`.

Scope:
- validates workflow transition rules

What it does not do:
- does not change session state
- does not write events or audit records
- does not perform persistence

Session state is owned by the Session module.

Future workflow capabilities may be added here, but only when they are implemented explicitly.
