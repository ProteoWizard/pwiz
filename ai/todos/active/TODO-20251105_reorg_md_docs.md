# TODO: Reorganize .md Documentation System

## Branch Information
- **Branch**: Skyline/work/20251105_reorg_md_docs
- **Created**: 2025-11-05
- **PR**: #3666
- **Objective**: Reorganize .md documentation to improve information density and make critical rules immediately accessible

## Context
Current .md documentation has grown to the point where critical rules are buried in detailed explanations. When agents search for essential constraints (like "CRLF line endings"), they must wade through too much context. This dilutes signal-to-noise ratio and makes critical rules harder to find.

As the team scales LLM-assisted development (8 developers, 900 KLOC, multiple parallel sessions with tools like Claude Code), we need a foundation that supports growth and clear organization.

## Problem Statement
- Core .md files have grown too large (MEMORY.md, STYLEGUIDE.md, TESTING.md)
- Critical rules are mixed with detailed explanations and historical context
- Agents add new information that "floods" existing documents
- Search for critical rules requires parsing hundreds of lines
- Information density has degraded over time
- No clear distinction between LLM context and human documentation
- Root directory becoming cluttered with AI-focused files
- Existing `doc/` folder creates naming conflicts with proposed `docs/`

## Goals
1. **Clear separation** - LLM context in `ai/`, human docs in `doc/`, code in source tree
2. **Critical rules immediately accessible** - no parsing required
3. **Core files stay small** (<200 lines each)
4. **Tiered information architecture** - essential vs. supplementary
5. **Clear loading strategy** - what to read when
6. **Sustainable growth** - new details go to ai/docs/, not core files
7. **Scalable pattern** - root `ai/` can extend to subproject-specific `ai/` folders as needed

## Proposed Structure

```
project-root/
├── README.md                  # Human entry point (stays at root)
├── .cursorrules              # Tool configuration (stays at root)
├── ai/                       # LLM-assisted development context
│   ├── CRITICAL-RULES.md     # <100 lines, immutable constraints
│   ├── MEMORY.md             # <200 lines, pointers only
│   ├── WORKFLOW.md           # <150 lines, process essentials
│   ├── STYLEGUIDE.md         # <200 lines, essential patterns
│   ├── TESTING.md            # <200 lines, testing essentials
│   ├── docs/                 # Detailed AI context (unlimited size)
│   │   ├── README.md         # Index: when to read each doc
│   │   ├── style-guide.md    # Full coding standards
│   │   ├── architecture.md   # System design
│   │   ├── edge-cases.md     # Special scenarios
│   │   ├── historical.md     # "Why we do X"
│   │   └── testing-patterns.md
│   └── todos/                # Task tracking for LLM sessions
│       ├── active/
│       ├── backlog/
│       ├── completed/
│       └── archive/
├── doc/                      # ProteoWizard website (unchanged)
└── pwiz_tools/               # Source code (unchanged)
```

**Future extensibility:**
- Subprojects can add their own `ai/` folders (e.g., `pwiz_tools/Skyline/ai/`)
- Pattern mirrors Boost.Build's sub-Jamfile approach
- Root `ai/` provides project-wide context
- Subproject `ai/` provides component-specific context

## Tasks

### Phase 0: Update WORKFLOW.md with Improved TODO Lifecycle
- [x] Update Workflow 1: Move TODO to active on master before branching
- [x] Add Workflow 5b: Creating backlog TODOs during active branch work

### Phase 1: Create ai/ Directory Structure
- [ ] Create `ai/` directory at project root
- [ ] Create `ai/docs/` for detailed supplementary content
- [ ] Create `ai/CRITICAL-RULES.md` (empty, ready for extraction)
- [ ] Create `ai/docs/README.md` as documentation index

### Phase 2: Move Existing Files to ai/
- [ ] Move MEMORY.md → ai/MEMORY.md
- [ ] Move WORKFLOW.md → ai/WORKFLOW.md
- [ ] Move STYLEGUIDE.md → ai/STYLEGUIDE.md
- [ ] Move TESTING.md → ai/TESTING.md
- [ ] Move todos/ → ai/todos/ (preserving subdirectories)

### Phase 3: Update .cursorrules
- [ ] Update all references to MEMORY.md → ai/MEMORY.md
- [ ] Update all references to WORKFLOW.md → ai/WORKFLOW.md
- [ ] Update all references to STYLEGUIDE.md → ai/STYLEGUIDE.md
- [ ] Update all references to todos/ → ai/todos/
- [ ] Test that .cursorrules still works correctly

### Phase 4: Extract Critical Rules
- [ ] Review ai/MEMORY.md and identify critical constraints
  - File format requirements (UTF-8 BOM, CRLF, etc.)
  - Build requirements (zero warnings, clean ReSharper)
  - Naming conventions (must-follow rules only)
  - Absolute prohibitions ("Never do X")
- [ ] Move critical rules to ai/CRITICAL-RULES.md (bare constraints only, no explanations)
- [ ] Keep ai/CRITICAL-RULES.md to ~50-100 lines maximum

### Phase 5: Reorganize Existing Files
- [ ] Slim ai/MEMORY.md to high-level pointers
  - Keep: Project overview, key technologies, team structure
  - Move detailed content to ai/docs/
  - Add references: "See ai/docs/[file].md for details"
- [ ] Extract detailed style guide content
  - Move from ai/MEMORY.md or ai/STYLEGUIDE.md → `ai/docs/style-guide.md`
  - Keep only essential patterns in ai/STYLEGUIDE.md
- [ ] Extract architectural details → `ai/docs/architecture.md`
- [ ] Extract edge cases and special scenarios → `ai/docs/edge-cases.md`
- [ ] Extract historical context/rationale → `ai/docs/historical.md`

### Phase 6: Create Documentation Index
- [ ] Write `ai/docs/README.md` with guidance on when to read each doc
  - style-guide.md: Before writing new code
  - architecture.md: Before major refactoring
  - edge-cases.md: When handling legacy data
  - testing-patterns.md: Before writing tests
  - historical.md: When wondering "why do we..."

### Phase 7: Update Cross-References
- [ ] Update all file paths in ai/MEMORY.md to reference ai/ structure
- [ ] Update all file paths in ai/WORKFLOW.md to reference ai/todos/
- [ ] Update all file paths in ai/STYLEGUIDE.md
- [ ] Update all file paths in ai/TESTING.md
- [ ] Update handoff prompts in ai/todos/ templates

### Phase 8: Validate
- [ ] Test handoff prompt with new structure
- [ ] Verify ai/CRITICAL-RULES.md is <100 lines
- [ ] Verify ai/MEMORY.md is <200 lines
- [ ] Verify ai/WORKFLOW.md is <150 lines
- [ ] Verify ai/STYLEGUIDE.md is <200 lines
- [ ] Verify ai/TESTING.md is <200 lines
- [ ] Confirm all detailed content has been moved to ai/docs/
- [ ] Verify all cross-references work correctly

## Critical Rules File Template

```markdown
# CRITICAL RULES

## File Format Requirements
- All files: UTF-8 with BOM
- All files: CRLF line endings (Windows standard)
- No trailing whitespace

## Build Requirements
- Solution must build with zero warnings
- All tests must pass before commit
- ReSharper must show green (no inspections)

## Naming Conventions
- Classes: PascalCase
- Private fields: _camelCase with underscore
- Interfaces: IPascalCase

## Never
- [Add specific prohibitions]
```

## Documentation Index Template

```markdown
# Documentation Index

## Core Files (Read Every Session)
- **CRITICAL-RULES.md**: Non-negotiable constraints
- **MEMORY.md**: Project overview and pointers
- **WORKFLOW.md**: Branch strategy, TODO system, build process

## Supplementary Documentation (Read On-Demand)
- **style-guide.md**: Detailed coding standards and examples
- **architecture.md**: System design and component relationships
- **edge-cases.md**: Special scenarios and legacy handling
- **testing-patterns.md**: Test fixture patterns and conventions
- **historical.md**: Rationale for decisions ("why we do X")

## When to Read What
- Before writing new code: style-guide.md
- Before major refactoring: architecture.md
- When handling legacy data: edge-cases.md
- Before writing tests: testing-patterns.md
- When wondering "why": historical.md
```

## Success Criteria
- All LLM-focused documentation consolidated in `ai/` folder
- Critical rules are immediately findable (<100 lines, no noise) in `ai/CRITICAL-RULES.md`
- Core files remain small and focused (<200 lines each)
- Detailed explanations are organized in `ai/docs/` and accessible
- New information can be added to `ai/docs/` without diluting core files
- Agents can load essential context quickly every session
- Clear distinction between LLM context (`ai/`) and human docs (`doc/`)
- Root directory is cleaner with only README.md and .cursorrules for AI tooling
- Pattern supports future subproject-specific `ai/` folders

## Notes
- **Core files should be append-hostile** - finished reference cards
- **ai/docs/ files can grow** - encyclopedic detail
- When agent wants to add detail: redirect to ai/docs/, add pointer in core
- Goal: <1000 total lines in core files (CRITICAL-RULES + MEMORY + WORKFLOW + STYLEGUIDE + TESTING)
- The `ai/` pattern mirrors Boost.Build's Jamfile structure - extensible to subprojects
- This prepares for team-scale LLM-assisted development with multiple parallel sessions

## Handoff Prompt for Next Session

```
I'm reorganizing our .md documentation system to improve scalability for team LLM-assisted development.

Read ai/todos/active/TODO-20251105_reorg_md_docs.md for the full plan.
Read ai/MEMORY.md and ai/WORKFLOW.md for current project context.

Goals:
1. Consolidate all LLM context into ai/ folder (MEMORY, WORKFLOW, STYLEGUIDE, TESTING, todos)
2. Create ai/CRITICAL-RULES.md (<100 lines) with bare constraints only
3. Slim existing core files to pointers
4. Move detailed content to ai/docs/ folder
5. Create ai/docs/README.md as navigation index

Current phase: [Specify which phase to continue]
```

## Completion Checklist
- [ ] All phases completed
- [ ] File size targets met (CRITICAL-RULES <100, MEMORY <200, WORKFLOW <150, STYLEGUIDE <200, TESTING <200)
- [ ] All LLM-focused files moved to ai/
- [ ] All detailed content moved to ai/docs/
- [ ] ai/docs/README.md provides clear navigation
- [ ] .cursorrules updated with ai/ paths
- [ ] All cross-references updated
- [ ] Handoff prompt tested with new structure
- [ ] TODO moved to ai/todos/completed/