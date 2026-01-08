# LabKey MCP Tool Hierarchy

This document defines which tools to use for different tasks. **Always start with PRIMARY tools** - they encode domain knowledge and produce better results.

## Tool Categories

### PRIMARY Tools (Use First)

These tools aggregate data, apply domain logic, and save comprehensive reports. Use these before drilling down.

| Tool | Purpose | Output |
|------|---------|--------|
| `get_daily_test_summary` | Daily nightly test review | `ai/.tmp/nightly-report-YYYYMMDD.md` |
| `save_exceptions_report` | Daily exception review | `ai/.tmp/exceptions-report-YYYYMMDD.md` |
| `get_support_summary` | Support board activity | `ai/.tmp/support-report-YYYYMMDD.md` |
| `save_issues_report` | Issue tracker overview | `ai/.tmp/issues-report-{status}-YYYYMMDD.md` |

### DRILL-DOWN Tools (After Primary)

Use these to investigate specific items found in primary reports.

**Test Details:**
| Tool | Purpose |
|------|---------|
| `get_run_failures` | Stack traces for a specific run |
| `get_run_leaks` | Memory/handle leaks for a specific run |
| `save_test_failure_history` | Compare stack traces across multiple failures |
| `save_run_log` | Full test output for deep investigation |
| `save_run_xml` | Structured test results |
| `query_test_runs` | Browse test runs in a folder |

**Exception Details:**
| Tool | Purpose |
|------|---------|
| `get_exception_details` | Full stack trace for one exception |
| `query_exceptions` | Browse recent exceptions |

**Support Details:**
| Tool | Purpose |
|------|---------|
| `get_support_thread` | Full thread with all posts |
| `query_support_threads` | Browse recent threads |
| `list_attachments` | Attachments for a post |
| `get_attachment` | Download an attachment |

**Issue Details:**
| Tool | Purpose |
|------|---------|
| `get_issue_details` | Full issue with comments |
| `query_issues` | Browse issues |

**Wiki:**
| Tool | Purpose |
|------|---------|
| `get_wiki_page` | Read wiki page content |
| `list_wiki_pages` | Browse wiki pages |
| `list_wiki_attachments` | List wiki attachments |
| `get_wiki_attachment` | Download wiki attachment |
| `update_wiki_page` | Modify wiki content |

### Limited Discovery (For Proposing Schema Documentation)

Only `list_queries` is available. Raw table queries (`query_table`) and schema exploration (`list_schemas`, `list_containers`) have been **removed** - they led to unproductive exploration and dangerous queries (50MB blobs, 700M row tables).

| Tool | Purpose |
|------|---------|
| `list_queries` | See what tables exist in a schema (to propose documentation) |

**When you find a table you need, don't try to query it directly.** Instead, propose a documentation workflow:

1. **Create schema stub** - `LabKeyMcp/queries/{schema}/{table}-schema.md`
2. **Human populates** - From LabKey Schema Browser UI
3. **Design server-side query** - As `*.sql` file with proper filtering
4. **Add high-level MCP tool** - That uses the server-side query

This is documented in [development-guide.md](development-guide.md#schema-first-development).

**Why raw queries were removed:**
- `query_table` could return 50MB binary blobs or timeout on 700M row tables
- Schema exploration wasted tokens without producing reusable knowledge
- High-level tools with documented schemas are far more efficient

## Usage Patterns

### Daily Review Workflow

```
1. get_daily_test_summary("2025-12-27")  # Start here
2. Read the saved report for overview
3. get_run_failures(run_id) only for runs with failures
4. save_test_failure_history() if a test failed multiple times
```

### Exception Triage Workflow

```
1. save_exceptions_report("2025-12-27")  # Start here
2. Read the saved report for overview
3. get_exception_details(id) only for interesting exceptions
```

### Support Review Workflow

```
1. get_support_summary(days=7)  # Start here
2. Read the saved report for unanswered threads
3. get_support_thread(id) for threads needing response
```

## Anti-Patterns

**Don't do this:**
```
# BAD: Seeing a table in list_queries and trying to access it directly
list_queries("testresults")  # Shows "testpasses" table exists
# Then asking: "Let me query testpasses to see what's there..."
# NO! That table has 700M rows - propose schema documentation instead.
```

**Do this instead:**
```
# GOOD: Start with PRIMARY tools for daily work
get_daily_test_summary("2025-12-27")
save_exceptions_report("2025-12-27")

# GOOD: When you need new data access, propose documentation:
list_queries("testresults")  # See what exists
# "I see testpasses exists. Let me create a stub schema doc at
#  queries/nightly/testpasses-schema.md for you to populate from
#  the LabKey Schema Browser, then we can design a proper query."
```

## Proposing New Data Access

When you need data that no high-level tool provides, use `list_queries` to see what's available, then propose a collaborative workflow:

### Step 1: Create Schema Documentation Stub
```
LabKeyMcp/queries/{schema}/{table}-schema.md
```
Include: Container path, schema name, table name, and placeholder for columns.

### Step 2: Human Populates Schema
The human goes to LabKey Schema Browser (`query-begin.view`) and copies the actual column metadata.

### Step 3: Design Server-Side Query
Create `*.sql` file with proper:
- `PARAMETERS` clause for filtering large tables
- JOINs to resolve lookups (e.g., userid → computer name)
- Only the columns actually needed

### Step 4: Create High-Level MCP Tool
Add a Python tool that:
- Calls the server-side query
- Saves large results to `ai/.tmp/`
- Returns brief summary + file path

**This workflow is far more efficient than discovery because:**
- Schema docs persist across sessions (no re-exploration)
- Server-side queries are tested and optimized
- No risk of 50MB responses or 700M row timeouts
- High-level tools encode domain knowledge permanently

See [development-guide.md](development-guide.md) for implementation details.
