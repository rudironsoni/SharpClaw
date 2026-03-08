Please also reference the following rules as needed. The list below is provided in TOON format, and `@` stands for the project root directory.

rules[1]{path}:
  @.codex/memories/40-mcp-and-tooling.md

# dotnet-agent-harness

Comprehensive .NET development guidance for modern C#, ASP.NET Core, MAUI, Blazor, and cloud-native apps.

## RuleSync-first architecture

- `.rulesync/` is the source of truth for this repository's agent content. Author changes in `rules`, `skills`,
  `subagents`, `commands`, `hooks`, and `mcp` there first.
- Generated target directories and prompt files are build artifacts. Regenerate them from RuleSync instead of editing
  them by hand.
- Keep the shared root `AGENTS.md` content in this overview rule and keep follow-up split rules off AGENTS-writing
  targets so regeneration stays stable.
- Runtime commands, exported bundles, and published agent packs all inherit from the RuleSync-authored catalog. When
  generated output looks wrong, fix `.rulesync/` and re-run `rulesync`.

## Surface ownership

- `rules`: always-on repository guidance, routing, and cross-cutting constraints.
- `skills`: reusable domain knowledge and implementation standards.
- `subagents`: named specialists with bounded scope and tool budgets.
- `commands`: user-invocable entry points that should prefer the local runtime over hand-written procedures.
- `hooks`: ambient reminders and lightweight automation; keep them advisory and portable.
- `mcp`: shared MCP server registrations and transport metadata for targets that support MCP.

## Working contract

- Keep frontmatter strictly RuleSync-compliant and add target-specific blocks only for real runtime differences.
- Keep commands deterministic, keep hooks advisory, and route hook-only targets through generated rules or hook text
  instead of unavailable skill or command surfaces.
- Validate and regenerate with the system-wide `rulesync` binary: use `rulesync generate --check` as the consistency
  gate, then `rulesync generate` when changes are intentional.
- Review source and generated diffs together; do not hand-edit generated target directories to patch a platform quirk.
- **Verify target configurations** using the per-target checklists in `.rulesync/verification/` before committing changes.

## Platform model

This toolkit provides:

- 189 skills
- 15 specialist agents/subagents
- shared RuleSync rules, commands, hooks, and MCP config

Compatible targets include:

- Claude Code
- GitHub Copilot CLI
- OpenCode
- Codex CLI
- Gemini CLI
- Antigravity
- Factory Droid

Target support is intentionally asymmetric. Author the shared behavior once, then add target-specific blocks only where
the runtime surface actually differs. See `.rulesync/rules/15-target-surfaces.md` for the target matrix and authoring
rules.

## Recommended install

Prefer the local runtime tool when this repository is installed into another .NET codebase:

```bash
dotnet new tool-manifest
dotnet tool install Rudironsoni.DotNetAgentHarness
dotnet agent-harness bootstrap --targets claudecode,opencode,codexcli,geminicli,copilot,antigravity,factorydroid --run-rulesync
```

RuleSync-only installation still works:

```bash
rulesync fetch rudironsoni/dotnet-agent-harness:.rulesync
rulesync generate --targets "claudecode,codexcli,opencode,geminicli,antigravity,copilot,factorydroid" --features "*"
```

If you use declarative sources:

```jsonc
{
  "sources": [{ "source": "rudironsoni/dotnet-agent-harness", "path": ".rulesync" }],
}
```

```bash
rulesync install
rulesync generate --targets "claudecode,codexcli,opencode,geminicli,antigravity,copilot,factorydroid" --features "*"
```

## OpenCode behavior

- Tab cycles **primary** agents only.
- `@mention` invokes subagents.
- `dotnet-architect` is configured as a primary OpenCode agent in this toolkit so it can appear in Tab rotation.

## Operating modes

- Architect: lead with the recommended repository-specific structure, trade-offs, and next implementation steps.
- Implementer: make the smallest safe change, verify it with narrow repo-native commands, and report target plus
  verification.
- Reviewer: produce findings-first output with severity, evidence, and risk.
- Tester: define the narrowest effective verification path, including exact commands and residual risk.

## Hook coverage

- Gemini CLI inherits the shared .NET session routing and now also receives the thin post-edit Roslyn, formatting, and
  Slopwatch advisories generated from `.rulesync/hooks.json`.
- Factory Droid consumes the same .NET session and MCP routing through generated rules and hooks, but its hook reminders
  stay compatible with that runtime's rules-plus-hooks surface instead of requiring imported skills or commands.

## Verification

Before committing changes or releasing updates, verify target configurations using the per-target checklists:

### Quick Verification
```bash
rulesync generate --targets "claudecode,codexcli,opencode,geminicli,antigravity,copilot,factorydroid" --features "*" --check
```

### Per-Target Checklists
- [Claude Code](verification/claudecode-checklist.md) - Full feature verification
- [OpenCode](verification/opencode-checklist.md) - Tab and @mention verification
- [GitHub Copilot CLI](verification/copilot-checklist.md) - Tool name mapping verification
- [Gemini CLI](verification/geminicli-checklist.md) - Portable hooks verification
- [Codex CLI](verification/codexcli-checklist.md) - Read-only sandbox verification
- [Factory Droid](verification/factorydroid-checklist.md) - Rules-only delivery verification
- [Antigravity](verification/antigravity-checklist.md) - Concise, portable verification

See [Verification README](verification/README.md) for detailed verification strategy.

## Troubleshooting

If RuleSync reports `Multiple root rulesync rules found`, ensure only one root overview rule exists in
`.rulesync/rules/`.

If `dotnet-agent-harness:*` commands are available, prefer executing the local runtime command
(`dotnet agent-harness ...`) instead of manually reproducing catalog, prompt, incident, or graph logic from source
files.

## Contributing

Edit source files in `.rulesync/` and validate with `npm run ci:rulesync`.

## License

MIT License. See `LICENSE`.


<!-- BEGIN BEADS INTEGRATION -->
## Issue Tracking with bd (beads)

**IMPORTANT**: This project uses **bd (beads)** for ALL issue tracking. Do NOT use markdown TODOs, task lists, or other tracking methods.

### Why bd?

- Dependency-aware: Track blockers and relationships between issues
- Git-friendly: Dolt-powered version control with native sync
- Agent-optimized: JSON output, ready work detection, discovered-from links
- Prevents duplicate tracking systems and confusion

### Quick Start

**Check for ready work:**

```bash
bd ready --json
```

**Create new issues:**

```bash
bd create "Issue title" --description="Detailed context" -t bug|feature|task -p 0-4 --json
bd create "Issue title" --description="What this issue is about" -p 1 --deps discovered-from:bd-123 --json
```

**Claim and update:**

```bash
bd update <id> --claim --json
bd update bd-42 --priority 1 --json
```

**Complete work:**

```bash
bd close bd-42 --reason "Completed" --json
```

### Issue Types

- `bug` - Something broken
- `feature` - New functionality
- `task` - Work item (tests, docs, refactoring)
- `epic` - Large feature with subtasks
- `chore` - Maintenance (dependencies, tooling)

### Priorities

- `0` - Critical (security, data loss, broken builds)
- `1` - High (major features, important bugs)
- `2` - Medium (default, nice-to-have)
- `3` - Low (polish, optimization)
- `4` - Backlog (future ideas)

### Workflow for AI Agents

1. **Check ready work**: `bd ready` shows unblocked issues
2. **Claim your task atomically**: `bd update <id> --claim`
3. **Work on it**: Implement, test, document
4. **Discover new work?** Create linked issue:
   - `bd create "Found bug" --description="Details about what was found" -p 1 --deps discovered-from:<parent-id>`
5. **Complete**: `bd close <id> --reason "Done"`

### Auto-Sync

bd automatically syncs via Dolt:

- Each write auto-commits to Dolt history
- Use `bd dolt push`/`bd dolt pull` for remote sync
- No manual export/import needed!

### Important Rules

- ✅ Use bd for ALL task tracking
- ✅ Always use `--json` flag for programmatic use
- ✅ Link discovered work with `discovered-from` dependencies
- ✅ Check `bd ready` before asking "what should I work on?"
- ❌ Do NOT create markdown TODO lists
- ❌ Do NOT use external issue trackers
- ❌ Do NOT duplicate tracking systems

For more details, see README.md and docs/QUICKSTART.md.

## Landing the Plane (Session Completion)

**When ending a work session**, you MUST complete ALL steps below. Work is NOT complete until `git push` succeeds.

**MANDATORY WORKFLOW:**

1. **File issues for remaining work** - Create issues for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **PUSH TO REMOTE** - This is MANDATORY:
   ```bash
   git pull --rebase
   bd sync
   git push
   git status  # MUST show "up to date with origin"
   ```
5. **Clean up** - Clear stashes, prune remote branches
6. **Verify** - All changes committed AND pushed
7. **Hand off** - Provide context for next session

**CRITICAL RULES:**
- Work is NOT complete until `git push` succeeds
- NEVER stop before pushing - that leaves work stranded locally
- NEVER say "ready to push when you are" - YOU must push
- If push fails, resolve and retry until it succeeds

<!-- END BEADS INTEGRATION -->
