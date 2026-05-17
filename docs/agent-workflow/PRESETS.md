# Workflow Presets

Use these as short defaults for common agent work. See `docs/agent-workflow/DONE_CONTRACT.md` for the completion checklist.

## `standard-agent-improvement-flow`

- Purpose: small, scoped implementation or maintenance changes.
- Default constraints: keep the diff minimal, stay file-scoped, preserve existing patterns, and avoid unrelated refactors.
- Default flow: confirm scope, inspect the relevant files, make the smallest useful change, do a quick review pass, then report the result.
- Command examples:
  - `standard-agent-improvement-flow`
  - `use standard-agent-improvement-flow for a small scoped change`

## `standard-learning-flow`

- Purpose: coordinator-led learning workflow for one small next step.
- Default constraints: keep scope narrow, use the planner/coder/reviewer/tester sequence, and stop if scope grows.
- Default flow: use `planner_deep` to pick the next smallest item, implement the scoped change with the right coder, review the diff, hand off to tester after approval, then run executable validation or record an explicit skip reason for docs/process-only changes (for example, `docs-only change; no executable validation needed`), then summarize the result.
- Command examples:
  - `standard-learning-flow`
  - `use standard-learning-flow to inspect one next item`

## `documentation-only-improvement-flow`

- Purpose: docs-only edits such as workflow guidance, references, or short clarifications.
- Default constraints: change only documentation files, keep wording practical and brief, and avoid introducing behavior changes.
- Default flow: locate the target doc, update only the requested text, link to existing references when needed, and verify the final wording.
- Command examples:
  - `documentation-only-improvement-flow`
  - `use documentation-only-improvement-flow for a docs-only edit`
