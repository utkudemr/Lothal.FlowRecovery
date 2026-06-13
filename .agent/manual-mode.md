# Manual Task Mode - Step-by-Step Workflow

## Purpose
Use this mode when you want to give the agent one specific task and have it implement just that task.
The agent will NOT automatically continue to the next backlog item.

## Codex Model Selection

- Prefer Codex's default model when using ChatGPT sign-in.
- Do not hardcode Codex model names in repository workflow files.
- If a model override is needed, choose a model supported by the current Codex authentication mode.
- ChatGPT sign-in and API key sign-in may expose different model availability.

## When to Use Manual Mode
- You have a specific, focused request
- You want to review the result before the next task
- You want to provide feedback or guidance between tasks
- You're exploring or validating one particular feature
- You want to preserve control over what gets implemented

## How to Use Manual Mode

### Step 1: Pick a Task
Give the agent a specific task in one of these forms:
```
Do TASK-001: Update project documentation for operator-driven recovery

Implement the RecoveryCase domain model from TASK-003

Work on: add audit trail documentation
```

### Step 2: Agent Implements
The agent will:
1. Read the task description from `.agent/backlog.md`
2. Inspect the relevant code or files
3. Make the minimal changes needed to satisfy acceptance criteria
4. Run validation commands
5. Commit the changes (if successful)
6. Report what changed and stop

### Step 3: Review and Decide
After the task is complete, you can:
- Ask for the next task
- Request changes to the implementation
- Move on to a different task
- Switch to autonomous mode

### Step 4: Validate and Commit
If validation passes and the agent committed the work:
- Changes are captured in git
- The agent updates `.agent/done.md`
- The task is marked as complete in `.agent/backlog.md`

## Task Format in Backlog

Each task in `.agent/backlog.md` includes:
- **Task ID**: TASK-001, TASK-002, etc.
- **Status**: todo, in-progress, done
- **Goal**: One-sentence purpose
- **Acceptance Criteria**: What must be true when done
- **Validation**: Commands to run to verify correctness
- **Commit Message**: Expected message for the git commit

## Example Manual Mode Session

```
User: Do TASK-001: Update project documentation for operator-driven recovery

Agent: I'll read TASK-001 from backlog.md and implement it.
[agent reads task]
[agent updates README.md and ARCHITECTURE.md]
[agent runs validation: reads updated files]
[agent commits: "docs: clarify operator-driven recovery design in README and ARCHITECTURE"]

Summary:
- Updated: README.md, docs/ARCHITECTURE.md
- Validation: Files updated and verified
- Commit: docs: clarify operator-driven recovery design...
- Status: TASK-001 moved to done

Next? You can ask for TASK-002 or request changes.
```

## Important Rules

1. **Only the requested task** is implemented
2. **Validation is required** unless explicitly skipped
3. **One commit per task** in manual mode
4. **Agent stops after completion** and waits for the next instruction
5. **No automatic backlog progression** in manual mode
6. **Git state must be clean** before starting a task

## Switching Modes

### From Manual to Autonomous
To switch from manual mode to autonomous backlog mode, say:
```
Use autonomous mode to continue with the backlog

Switch to autonomous backlog mode

Run the autonomous development loop
```

The agent will then use `.agent/autonomous-mode.md` to process remaining tasks automatically.

### From Autonomous to Manual
If autonomous mode encounters a stop condition or the agent wants to hand off, you can:
1. Review the stopped state
2. Request a specific follow-up task in manual mode
3. Get back control to guide the next step

## Expected Workflow

Most projects use a mix:
1. **Manual mode** for the first task or two (learning the codebase)
2. **Switch to autonomous mode** once the first task is proven
3. **Manual handoff** if a stop condition occurs during autonomous execution

This gives you both control and efficiency.
