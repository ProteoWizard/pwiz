# TODO: Reorganize .md Documentation System

## Branch Information
- **Branch**: Skyline/work/20251105_reorg_md_docs
- **Created**: 2025-11-05
- **PR**: #3666
- **Objective**: Reorganize .md documentation to improve information density and make critical rules immediately accessible

## Context
Current .md documentation has grown to the point where critical rules are buried in detailed explanations. When agents search for essential constraints (like "CRLF line endings"), they must wade through too much context. This dilutes signal-to-noise ratio and makes critical rules harder to find.

## Problem Statement
- Core .md files have grown too large (MEMORY.md, STYLEGUIDE.md)
- Critical rules are mixed with detailed explanations and historical context
- Agents add new information that "floods" existing documents
- Search for critical rules requires parsing hundreds of lines
- Information density has degraded over time

## Goals
1. **Critical rules immediately accessible** - no parsing required
2. **Core files stay small** (<200 lines each)
3. **Tiered information architecture** - essential vs. supplementary
4. **Clear loading strategy** - what to read when
5. **Sustainable growth** - new details go to docs/, not core files

## Proposed Structure

```
project-root/
├── CRITICAL-RULES.md          # <100 lines, immutable constraints
├── MEMORY.md                  # <200 lines, pointers only
├── WORKFLOW.md                # <150 lines, process essentials
├── docs/                      # Supplementary detail (unlimited size)
│   ├── README.md              # Index: when to read each doc
│   ├── style-guide.md         # Full coding standards
│   ├── architecture.md        # System design
│   ├── edge-cases.md          # Special scenarios
│   ├── historical.md          # "Why we do X"
│   └── testing-patterns.md
└── todos/
    ├── active/
    └── backlog/
```

## Tasks

### Phase 0: Update WORKFLOW.md with Improved TODO Lifecycle
- [x] Update Workflow 1: Move TODO to active on master before branching
- [x] Add Workflow 5b: Creating backlog TODOs during active branch work

### Phase 1: Create New Structure
- [ ] Create `docs/` directory at project root
- [ ] Create `CRITICAL-RULES.md` (empty, ready for extraction)
- [ ] Create `docs/README.md` as documentation index

### Phase 2: Extract Critical Rules
- [ ] Review MEMORY.md and identify critical constraints
  - File format requirements (UTF-8 BOM, CRLF, etc.)
  - Build requirements (zero warnings, clean ReSharper)
  - Naming conventions (must-follow rules only)
  - Absolute prohibitions ("Never do X")
- [ ] Move critical rules to CRITICAL-RULES.md (bare constraints only, no explanations)
- [ ] Keep CRITICAL-RULES.md to ~50-100 lines maximum

### Phase 3: Reorganize Existing Files
- [ ] Slim MEMORY.md to high-level pointers
  - Keep: Project overview, key technologies, team structure
  - Move detailed content to docs/
  - Add references: "See docs/[file].md for details"
- [ ] Extract detailed style guide content
  - Move from MEMORY.md or STYLEGUIDE.md → `docs/style-guide.md`
  - Keep only essential patterns in root
- [ ] Extract architectural details → `docs/architecture.md`
- [ ] Extract edge cases and special scenarios → `docs/edge-cases.md`
- [ ] Extract historical context/rationale → `docs/historical.md`

### Phase 4: Create Documentation Index
- [ ] Write `docs/README.md` with guidance on when to read each doc
  - style-guide.md: Before writing new code
  - architecture.md: Before major refactoring
  - edge-cases.md: When handling legacy data
  - testing-patterns.md: Before writing tests
  - historical.md: When wondering "why do we..."

### Phase 5: Update References
- [ ] Update MEMORY.md with pointer to docs/README.md
- [ ] Update WORKFLOW.md to reference new structure
- [ ] Update TODO template handoff prompts to reference CRITICAL-RULES.md
- [ ] Verify all cross-references are correct

### Phase 6: Validate
- [ ] Test handoff prompt with new structure
- [ ] Verify CRITICAL-RULES.md is <100 lines
- [ ] Verify MEMORY.md is <200 lines
- [ ] Verify WORKFLOW.md is <150 lines
- [ ] Confirm all detailed content has been moved to docs/

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
- Critical rules are immediately findable (<100 lines, no noise)
- Core files remain small and focused
- Detailed explanations are organized and accessible
- New information can be added to docs/ without diluting core files
- Agents can load essential context quickly every session

## Notes
- **Core files should be append-hostile** - finished reference cards
- **docs/ files can grow** - encyclopedic detail
- When agent wants to add detail: redirect to docs/, add pointer in core
- Goal: <500 total lines in core files (CRITICAL-RULES + MEMORY + WORKFLOW)

## Handoff Prompt for Next Session

```
I'm reorganizing our .md documentation system to improve information density.

Read todos/active/TODO-20251105_reorg_md_docs.md for the full plan.
Read MEMORY.md and WORKFLOW.md for current project context.

Goals:
1. Create CRITICAL-RULES.md (<100 lines) with bare constraints only
2. Slim existing core files to pointers
3. Move detailed content to docs/ folder
4. Create docs/README.md as navigation index

Start with Phase 1: creating the new directory structure and empty files.
```

## Completion Checklist
- [ ] All phases completed
- [ ] File size targets met (CRITICAL-RULES <100, MEMORY <200, WORKFLOW <150)
- [ ] All detailed content moved to docs/
- [ ] docs/README.md provides clear navigation
- [ ] Handoff prompt tested with new structure
- [ ] TODO moved to completed/