# Conventions

Stable conventions for agents working in this repository.

## Scope

- Keep changes minimal and file-scoped.
- Prefer extending existing module structure over introducing new patterns.
- Do not modify unrelated code.

## Implementation

- Preserve auditability and append-only event history behavior.
- Do not introduce persistence layers or external dependencies unless explicitly requested.
- Avoid broad refactors unless explicitly requested.

## Validation

- Prefer focused validation for changed behavior.
- Prefer diff-based review over full-file review.
