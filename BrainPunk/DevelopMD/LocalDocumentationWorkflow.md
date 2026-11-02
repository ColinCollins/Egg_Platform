# Local Documentation Completion Workflow

## 2026-06-17 Update

### Requirement / Background

Completed BrainPunk requirements should leave durable local documentation, not only a chat summary. The requested workflow is: after each new requirement, bug fix, or behavior-changing implementation, create or update a local document in the corresponding feature/module folder before final reporting.

### Affected Files And Systems

- `BrainPunk/AGENTS.md`: adds the hard rule that local feature/module documentation is mandatory before final reporting.
- `.specify/templates/tasks-template.md`: adds a required polish-phase task so future Speckit task generation includes the documentation work.
- `.agents/skills/brainpunk-local-doc-update/SKILL.md`: adds the reusable workflow for choosing the target document and required content.
- `.agents/skills/brainpunk-local-doc-update/agents/openai.yaml`: generated skill UI metadata.

### Behavior Before

- `BrainPunk/AGENTS.md` required recording reusable facts and had special rules for level-specific feature/risk documents.
- General completed requirements did not have a universal, explicit local-documentation completion rule.
- The Speckit task template had only a broad documentation update item.

### Behavior After

- Every completed new requirement, bug fix, or behavior-changing implementation must create or update a local feature/module document before final reporting.
- Future Speckit task lists should include a concrete local documentation task.
- The `brainpunk-local-doc-update` skill documents the expected target selection, content, evidence rules, and final reporting.

### Data / Resource / Event Flow

No runtime data, resources, save data, analytics events, UI flow, or platform flow changed. This is an agent workflow and project documentation rule change only.

### Verification

- Ran `quick_validate.py` for `.agents/skills/brainpunk-local-doc-update`: `Skill is valid!`.
- Inspected the resulting changes with `git diff` and `git status`.

### Remaining Risks / Unverified Gaps

- Future `/speckit-tasks` output has not yet been generated after this template change, so one future task-generation pass should verify the local documentation task appears as expected.
- Skill auto-discovery in future sessions depends on the Codex environment loading repository skills from `.agents/skills/`.

### Rollback Path

Revert the additions in `BrainPunk/AGENTS.md`, `.specify/templates/tasks-template.md`, remove `.agents/skills/brainpunk-local-doc-update/`, and delete this document.
