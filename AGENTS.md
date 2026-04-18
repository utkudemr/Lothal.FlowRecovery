# Lothal.FlowRecovery — Agent Guidelines

## Purpose
This repository builds a backend system to recover and manage stuck sales flows in a Flutter-based sales application.

The system prioritizes:
- reliability
- auditability
- minimal and safe changes
- token-efficient AI usage

---

## Core Principles

- Server state is the source of truth
- All important changes must be recorded as events
- Operator interventions must be auditable
- Prefer small, scoped changes over large refactors
- Avoid unnecessary complexity
- Do not modify unrelated parts of the codebase

---

## Architecture Context

- Modular monolith with microservice-ready boundaries
- Core modules:
  - Session
  - Basket
  - Workflow
  - Operations
  - Realtime

- Data strategy:
  - PostgreSQL → source of truth
  - Redis → cache, locks, ephemeral state
  - NATS → event-driven communication
  - NoSQL → optional read model (later)

---

## Agent Roles

### Orchestrator
- coordinates work
- selects the correct subagent
- keeps scope small
- ensures review is performed when needed

### Planner
- breaks work into phases
- defines file scope
- identifies risks and dependencies

### Coder
- implements changes
- keeps diffs minimal
- respects architecture and boundaries

### Reviewer
- checks correctness and risks
- ensures audit/event consistency
- prevents scope creep

### Explorer
- read-only analysis
- maps code and flows
- helps locate bugs

### Tester
- creates minimal unit tests
- focuses on validation and regression risks
- ensures critical branches are covered
- avoids unnecessary test infrastructure

---

## Agent Routing Rules

Use the correct agent based on task complexity:

- planner_deep → planning medium/large tasks
- explorer → read-only investigation and bug analysis
- coder_fast → small, low-risk changes (1–3 files)
- coder_deep → complex, high-risk, or workflow-sensitive changes
- reviewer → validation after medium/high-risk changes
- tester → add or review tests for medium/high-risk features

---

## Execution Rules

- Prefer a single agent unless parallel execution is clearly safe
- Do not spawn subagents for trivial tasks
- Always define file scope before implementation
- Use planner when the task is unclear or large
- Use explorer before fixing unclear bugs
- Run reviewer after risky changes

---

## Token Efficiency Rules

- Do not load the entire repository unless required
- Use REPO_MAP.md to locate relevant modules
- Prefer summaries over repeating context
- Prefer diff-based review over full-file review
- Keep tasks narrow and well-defined

---

## Change Guidelines

- Each task should ideally modify 1–3 files
- Avoid large refactors unless explicitly requested
- Preserve existing naming and structure
- Do not introduce new patterns without justification
- Keep code consistent with the current architecture

---

## Audit & Workflow Rules

- Never modify workflow state without recording an event
- Always record operator interventions
- Maintain append-only event history where applicable
- Ensure changes are traceable

---

## When Unsure

- Use planner_deep to clarify the task
- Use explorer to understand the codebase
- Ask for clarification instead of guessing