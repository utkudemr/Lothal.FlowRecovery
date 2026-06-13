# Autonomous Backlog Mode - Development Loop

## Purpose
Use this mode to process the backlog automatically from start to finish.
The agent reads tasks, implements them one by one, validates, commits, and continues until done or a stop condition occurs.

## Codex Model Selection

- Prefer Codex's default model when using ChatGPT sign-in.
- Do not hardcode Codex model names in repository workflow files.
- If a model override is needed, choose a model supported by the current Codex authentication mode.
- ChatGPT sign-in and API key sign-in may expose different model availability.

## When to Use Autonomous Mode
- You want the agent to work through multiple backlog items
- The first task has been validated and the direction is clear
- You trust the agent to make scoped implementation decisions
- You want efficient batch progress on a backlog
- You're OK with batched review and commit after each task

## How Autonomous Mode Works

### The Loop
1. Agent reads `.agent/backlog.md`
2. Finds the first task with `Status: todo`
3. Changes status to `Status: in-progress`
4. Reads task details: goal, acceptance criteria, validation commands
5. Inspects relevant code
6. Implements minimal changes to satisfy criteria
7. Runs validation commands
8. If validation passes:
   - Marks task as `Status: done`
   - Updates `.agent/done.md` with task summary
   - Updates `.agent/project-state.md` if state changed
   - Adds decision to `.agent/decisions.md` if architecture decision was made
   - Creates one git commit with expected message
9. Continues to next task (step 2)
10. Stops when all `todo` items are `done` OR a stop condition is hit

### Important Loop Rules
- **One task per commit** - never combine unrelated work
- **Minimal changes only** - do the smallest safe change for each task
- **Validation first** - run the task's validation commands before committing
- **Update state files** - keep `.agent/done.md`, `.agent/project-state.md`, `.agent/decisions.md` current
- **Clear commit messages** - use the expected message from the task

## Stop Conditions

The agent STOPS (does not continue automatically) if:

1. **Product decision required** - A task needs a decision not in the backlog, and the agent can't infer it safely
2. **Existing tests fail** - Test failures that are unrelated to the agent's changes
3. **Build failures** - Changes that break the build or cause compilation errors
4. **Unrelated changes in working tree** - User has made changes that weren't part of the task
5. **Large refactor needed** - The task would require deleting or rewriting large parts of the codebase
6. **Workflow incompatibility** - The change would break the existing manual workflow mode

When a stop condition occurs, the agent:
1. Reports the stop condition clearly
2. Shows what has been completed so far
3. Shows what would need to happen next
4. Waits for user direction

## Validation Commands

Each task specifies validation commands to run. Examples:
- `dotnet restore && dotnet build` - compile and check for errors
- `dotnet test` - run the full test suite
- Code inspection - verify a file was created correctly
- Markdown link check - verify documentation links work

The agent runs validation **before** committing. If validation fails:
- Agent fixes the issue
- Reruns validation
- If still failing, reports the failure and asks for guidance

## Task Status in Backlog

During autonomous execution, backlog.md is updated:
```markdown
### TASK-001: Update documentation
**Status:** done  ✓ Updated from todo
[rest of task definition...]

### TASK-002: Create Operations module
**Status:** in-progress  ✓ Will be updated as agent works
[rest of task definition...]
```

After task completes:
```markdown
### TASK-001: Update documentation
**Status:** done  ✓ Final status
[rest of task definition...]

### TASK-002: Create Operations module
**Status:** done  ✓ Completed
[rest of task definition...]
```

## Example Autonomous Execution

```
User: Use autonomous mode to continue with the backlog

Agent: Starting autonomous backlog mode. Checking initial state...

### Processing TASK-001: Update documentation
[reads task details]
[inspects README.md]
[updates files]
[runs validation: reads files, confirms changes]
[updates .agent/done.md]
[commits: "docs: clarify operator-driven recovery design..."]
✓ TASK-001 complete

### Processing TASK-002: Create Operations module
[reads task details]
[creates directory structure]
[creates project file]
[adds to solution]
[runs validation: dotnet build]
[updates .agent/done.md]
[commits: "feat: add Operations module scaffold"]
✓ TASK-002 complete

### Processing TASK-003: Add RecoveryCase domain model
[reads task details]
[creates domain class]
[creates events]
[writes tests]
[runs validation: dotnet test]
[updates .agent/done.md]
[updates .agent/decisions.md]
[commits: "feat: add RecoveryCase domain model with audit events"]
✓ TASK-003 complete

[continues...]

### Status Report
- Completed: TASK-001, TASK-002, TASK-003
- In-progress: none
- Remaining: TASK-004, TASK-005, TASK-006, TASK-007, TASK-008, TASK-009
- Continue? (y/n or request manual mode)
```

## Controlling Autonomous Execution

### Pause and Check Status
```
User: Check progress

Agent: Reports completed tasks, current task, and remaining items
```

### Stop and Switch to Manual
```
User: Stop autonomous mode. I want to review before continuing.

Agent: Pauses execution and enters manual mode for the next task
```

### Continue After Pause
```
User: Continue autonomous mode

Agent: Resumes from the next todo item
```

### Full Backlog Completion
If all tasks complete without stop conditions:
```
Agent: All backlog items (TASK-001 through TASK-009) are complete.
Final state:
- All tests passing
- All modules integrated
- All commits created
- Project ready for MVP validation

Summary of work:
[list of all commits and changes]

Status: MVP Backlog Complete ✓
```

## Important Notes

- Autonomous mode is **non-interactive** during task execution
- Agent will **NOT skip validation** even if it seems quick
- Agent will **NOT skip test failures** without reporting
- Agent will **report all stop conditions** clearly
- Agent will **update all state files** (done.md, project-state.md, decisions.md)
- Agent will **use expected commit messages** from backlog
- Agent will **not rewrite AGENTS.md or existing workflows**

## When to Use Manual Mode Instead

Even during autonomous mode, if:
- You want to review after each task
- You want to provide feedback between tasks
- You want to make a manual decision mid-workflow
- You want to change the backlog mid-execution

Just ask to switch to manual mode, and the agent will hand back control.
