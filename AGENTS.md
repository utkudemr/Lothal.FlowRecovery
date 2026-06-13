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
* identifies durable lessons and memory-worthy findings

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
* Consult project memory docs before planning or coding

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

   * hand off to tester for validation; executable tests may be skipped only for docs/process-only changes with an explicit reason

5. Do not skip coder, reviewer, or tester handoff.

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

9. If reviewer identifies a durable lesson or decision:

   * suggest a minimal memory update

---

## Reviewer Findings Policy

* Reviewer findings are human checkpoints, not automatic retry triggers.
* Medium/High findings stop the workflow until explicitly resolved or accepted.
* Do not automatically restart planner/coder loops after reviewer findings.
* Follow-up fixes should be handled as a new small scoped task.
* Avoid infinite reviewer/coder retry cycles.
* Reviewer may suggest memory updates when durable findings are discovered.

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

* completing incomplete workflow capabilities
* small functional improvements
* operator-facing workflow behavior
* missing domain behaviors
* vertical slice progression

Use test-only hardening when:

* regression risk is high
* behavior is unclear
* validation coverage is genuinely missing
* recent implementation lacks protection

Avoid selecting multiple consecutive test-only tasks unless explicitly justified.

---

## Project Memory

Before planning or coding, agents should read the relevant project memory files:

* `docs/memory/decisions.md` - accepted durable decisions
* `docs/memory/conventions.md` - stable repository conventions
* `docs/memory/review-lessons.md` - recurring review lessons
* `docs/memory/deprecated.md` - deprecated or rejected decisions

Use project memory as durable guidance, not as a scratchpad.

Update memory only when:

* a durable architecture, product, or workflow decision is accepted
* a stable convention is established
* a recurring review lesson should affect future work
* a decision or approach is explicitly deprecated

Do not write:

* temporary notes
* private reasoning
* speculative future ideas
* implementation noise
* content already covered by `README.md` or `docs/ARCHITECTURE.md`

## Agent Workflow

Use `docs/agent-workflow/DONE_CONTRACT.md` as the completion checklist for small tasks.
Use `docs/agent-workflow/PRESETS.md` for reusable workflow presets.

---

## Development Workflows

The repository supports **two optional development workflows** in parallel:

### Workflow 1: Manual Task Mode (Step-by-Step)

**When to Use:**
- You have one specific task in mind
- You want to review and decide on the next step after each task
- You want to maintain full control over sequencing

**How to Invoke:**
```
Do TASK-002: Create Operations module scaffold

Work on: add RecoveryCase domain model
```

**Agent Behavior:**
1. Reads the task from `.agent/backlog.md`
2. Inspects relevant code
3. Implements minimal changes for the task
4. Runs validation commands
5. Commits the task
6. Reports completion and **stops**
7. Waits for your next instruction

**See:** `.agent/manual-mode.md` for detailed instructions

---

### Workflow 2: Autonomous Backlog Mode (Development Loop)

**When to Use:**
- You want the agent to work through multiple backlog items automatically
- The first task has been validated and the direction is clear
- You want efficient batch progress

**How to Invoke:**
```
Use autonomous mode to complete the backlog

Switch to autonomous backlog mode

Run the autonomous development loop
```

**Agent Behavior:**
1. Reads `.agent/backlog.md`
2. Finds first `todo` task
3. Marks it as `in-progress`
4. Implements and validates (same as manual mode)
5. Marks as `done` and commits
6. Updates state files (`.agent/done.md`, `.agent/project-state.md`, `.agent/decisions.md`)
7. Continues to next task
8. Stops only when all tasks are done or a stop condition occurs

**Stop Conditions:**
- Product decision required but not documented
- Build or test failures unrelated to current task
- Unrelated changes in working tree
- Would need to delete or rewrite large parts of codebase
- Would break existing manual workflow

**See:** `.agent/autonomous-mode.md` for detailed instructions

---

## Choosing Your Workflow

| Scenario | Use Workflow | Why |
|----------|----------|------|
| "Do TASK-001" | Manual Task Mode | Clear, focused scope |
| "Complete the backlog" | Autonomous Mode | Efficient batch progress |
| "I want to review after each task" | Manual Task Mode | Full control |
| "First task is done, continue" | Autonomous Mode | Hands-off, batch efficiency |
| "There's a stop condition, need guidance" | Manual Task Mode | Take control back |

---

## Backlog Planning Files

The autonomous workflow uses these agent-readable files in `.agent/`:

- **`.agent/backlog.md`** - Ordered list of MVP tasks with acceptance criteria and validation commands
- **`.agent/project-state.md`** - Current implementation status, known gaps, and limitations
- **`.agent/done.md`** - Completed task log (updated as backlog progresses)
- **`.agent/decisions.md`** - Architecture and product decisions made during backlog execution
- **`.agent/manual-mode.md`** - How to use step-by-step task mode
- **`.agent/autonomous-mode.md`** - How to use automatic backlog processing mode

These files support both workflows:
- Manual mode reads them to understand the single requested task
- Autonomous mode reads them to drive the full loop

---

## Integration with Existing Workflows

**Important:** Both workflows respect the existing project structure and constraints:

- Do not break existing test infrastructure
- Do not modify agent roles or coordination rules (above)
- Do not remove or rename `AGENTS.md` sections
- Preserve module boundaries and architecture principles
- Follow the same Change Guidelines and Audit Rules
- Update project memory (`docs/memory/`) for durable decisions

Both workflows are optional and coexist with the existing step-by-step agent-based collaboration described earlier in this document.

---

## Memory Quality Rules

Memory files should remain:

* concise
* durable
* high-signal
* scoped to accepted decisions, conventions, review lessons, and deprecated decisions

Avoid:

* duplicating README or architecture content
* recording one-off fixes
* storing noisy debugging history
* adding plans that are not yet accepted
