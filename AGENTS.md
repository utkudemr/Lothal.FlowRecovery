# Lothal.FlowRecovery — Agent Guidelines

## Purpose

This repository builds a backend system to recover and manage stuck sales flows in a Flutter-based sales application.

The system prioritizes:

* reliability
* auditability
* minimal and safe changes
* token-efficient AI usage

---

## Core Principles

* Server state is the source of truth
* All important changes must be recorded as events
* Operator interventions must be auditable
* Prefer small, scoped changes over large refactors
* Avoid unnecessary complexity
* Do not modify unrelated parts of the codebase

---

## Architecture Context

* Modular monolith with microservice-ready boundaries

Core modules:

* Session
* Basket
* Workflow
* Operations
* Realtime

Data strategy:

* PostgreSQL → source of truth
* Redis → cache, locks, ephemeral state
* NATS → event-driven communication
* NoSQL → optional read model (later)

---

## Agent Roles

### Orchestrator

* coordinates work
* selects the correct subagent
* keeps scope small
* ensures workflow discipline

### Planner

* breaks work into phases
* defines file scope
* identifies risks and dependencies

### Coder

* implements changes
* keeps diffs minimal
* respects architecture and boundaries

### Reviewer

* checks correctness and risks
* ensures audit/event consistency
* prevents scope creep

### Explorer

* read-only analysis
* maps code and flows
* helps locate bugs

### Tester

* creates minimal unit tests
* focuses on validation and regression risks
* ensures critical branches are covered
* avoids unnecessary test infrastructure

---

## Agent Routing Rules

Use the correct agent based on task complexity:

* planner_deep → planning medium/large tasks
* explorer → read-only investigation and bug analysis
* coder_fast → small, low-risk changes (1–3 files)
* coder_deep → complex, high-risk, or workflow-sensitive changes
* reviewer → validation after implementation work
* tester → validation and regression checking

---

## Execution Rules

* Prefer a single agent unless parallel execution is clearly safe
* Do not spawn subagents for trivial tasks
* Always define file scope before implementation
* Use planner when the task is unclear or large
* Use explorer before fixing unclear bugs
* Run reviewer after implementation work
* Run tester after reviewer approval

---

## Token Efficiency Rules

* Do not load the entire repository unless required
* Use REPO_MAP.md to locate relevant modules
* Prefer summaries over repeating context
* Prefer diff-based review over full-file review
* Keep tasks narrow and well-defined

---

## Change Guidelines

* Each task should ideally modify 1–3 files
* Avoid large refactors unless explicitly requested
* Preserve existing naming and structure
* Do not introduce new patterns without justification
* Keep code consistent with the current architecture

---

## Audit & Workflow Rules

* Never modify workflow state without recording an event
* Always record operator interventions
* Maintain append-only event history where applicable
* Ensure changes are traceable

---

## When Unsure

* Use planner_deep to clarify the task
* Use explorer to understand the codebase
* Ask for clarification instead of guessing

---

## Standard Learning Workflow

When the user asks for:

* "standard learning workflow"
* "learning workflow"
* "standart öğrenme akışını çalıştır"

follow this process:

### Coordinator Behavior

* The main agent acts as coordinator only.
* The main agent should delegate planning/coding/review/testing to specialist agents when available.
* The main agent should avoid directly implementing changes during the standard learning workflow.

### Workflow

1. Use planner_deep to inspect the current repo state and select exactly one next smallest meaningful item.

2. Use:

   * coder_fast for small isolated implementation
   * coder_deep for domain-sensitive/state/audit/workflow/idempotency-related implementation

3. Use reviewer to review the resulting diff.

4. If reviewer approves:

   * use tester to validate the change and run relevant tests

5. Do not skip coder, reviewer, or tester.

6. Keep scope small and file-scoped.

7. Stop if:

   * scope expands
   * tests fail
   * reviewer reports Medium/High findings

8. At the end summarize:

   * selected item
   * planner output
   * coder used and why
   * reviewer findings
   * tests run
   * changed files
   * next smallest step

---

## Reviewer Findings Policy

* Reviewer findings are human checkpoints, not automatic retry triggers.
* Medium/High findings stop the workflow until explicitly resolved or accepted.
* Do not automatically restart planner/coder loops after reviewer findings.
* Follow-up fixes should be handled as a new small scoped task.
* Avoid infinite reviewer/coder retry cycles.

The user decides whether to:

* accept the finding
* request a focused fix
* continue to tester despite the finding

Examples:

* "Reviewer bulgusunu düzeltmek için standart öğrenme akışını çalıştır."
* "Reviewer bulgusunu kabul ediyorum, tester aşamasına geç."

---

## Global Constraints

Unless explicitly stated otherwise:

* do not introduce database or persistence layers
* do not add external dependencies
* keep implementations minimal
* prefer small diffs and limited file scope
* avoid unnecessary abstractions
* stay within the current module scope
* prefer extending existing files and structures before creating new ones

---

## Progression Bias

Do not repeatedly select only test-hardening tasks when meaningful feature progression is available.

Prefer:
- completing incomplete workflow capabilities
- small functional improvements
- operator-facing workflow behavior
- missing domain behaviors
- vertical slice progression

Use test-only hardening when:
- regression risk is high
- behavior is unclear
- validation coverage is genuinely missing
- recent implementation lacks protection

Avoid selecting multiple consecutive test-only tasks unless explicitly justified.