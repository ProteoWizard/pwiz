# MCP Development Guide

This guide covers patterns and best practices for extending Claude Code's data access capabilities via MCP (Model Context Protocol) servers.

## General Principles

### Use Official SDKs Where Possible

When connecting to external services, prefer official SDKs over direct HTTP/REST calls:

- **LabKey**: Use the `labkey` Python package for data queries
- **Gmail**: Use Google's official Python client libraries
- **Other services**: Check for official SDK support first

**Why?**
- SDKs handle authentication, retries, and edge cases
- They receive updates and security fixes from maintainers
- Less code to maintain in our MCP server

**Exception**: Discovery APIs (like LabKey's `getSchemas`, `getQueries`, `getContainers`) may not be exposed by SDKs and require direct HTTP calls.

### Keep MCP Server Code Simple

The MCP server should be a thin adapter layer. Avoid implementing complex business logic in Python. Instead:

- Use server-side capabilities where available (views, stored procedures, custom queries)
- Let the data source handle joins, aggregations, and filtering
- Keep Python code focused on protocol translation and formatting

## LabKey Server Patterns

### Server-Side Custom Queries

When Claude Code needs aggregated or joined data, create **custom queries on the LabKey server** rather than implementing complex joins in Python.

**Why server-side queries?**
- Runs on the database server (faster)
- Claude Code queries it like any other table via `query_table`
- No complex Python code to maintain
- Reusable by other tools and users
- Easier to modify without updating the MCP server

**How to create a custom query:**

1. Go to skyline.ms → Query Schema Browser → select your schema
2. Click "Create New Query"
3. Name it descriptively (e.g., `handleleaks_by_computer`)
4. Select the base table
5. Replace the default SQL with your JOIN/GROUP BY query
6. Click "Execute Query" to test
7. Click "Save & Finish"

The new query immediately becomes available to the MCP server via the existing `query_table` tool.

### Example: Aggregating by Computer

A common need is to see which computers are experiencing a problem. Raw tables store `userid` which must be joined to the `user` table to get the computer name.

**handleleaks_by_computer:**
```sql
SELECT
    u.username AS computer,
    h.testname,
    COUNT(*) AS leak_count,
    AVG(h.handles) AS avg_handles,
    MAX(t.posttime) AS last_seen
FROM handleleaks h
JOIN testruns t ON h.testrunid = t.id
JOIN "user" u ON t.userid = u.id
GROUP BY u.username, h.testname
ORDER BY leak_count DESC
```

**testfails_by_computer:**
```sql
SELECT
    u.username AS computer,
    f.testname,
    COUNT(*) AS failure_count,
    MAX(t.posttime) AS last_seen
FROM testfails f
JOIN testruns t ON f.testrunid = t.id
JOIN "user" u ON t.userid = u.id
GROUP BY u.username, f.testname
ORDER BY failure_count DESC
```

### Parameterized Queries for Large Tables

Some tables are too large for unfiltered queries. The `testpasses` table has 700M+ rows - a join without filtering would timeout. Use **parameterized queries** to filter BEFORE joining.

**How to create a parameterized query:**

1. Add a `PARAMETERS` clause at the top of your SQL
2. Use the parameter in a `WHERE` clause to filter the large table
3. The MCP server passes parameters via `param_name` and `param_value`

**Example: testpasses_detail**
```sql
PARAMETERS (RunId INTEGER)

SELECT
    t.posttime AS run_date,
    u.username AS computer,
    p.testrunid,
    p.testname,
    p.pass AS passnum,
    p.handles,
    p.userandgdihandles,
    p.managedmemory,
    p.totalmemory,
    p.duration
FROM testpasses p
JOIN testruns t ON p.testrunid = t.id
JOIN "user" u ON t.userid = u.id
WHERE p.testrunid = RunId
ORDER BY p.testname, p.pass
```

**Calling from Claude Code:**
```
query_table(
    schema_name="testresults",
    query_name="testpasses_detail",
    param_name="RunId",
    param_value="74829",
    filter_column="testname",
    filter_value="TestMethodRefinementTutorial"
)
```

**When to use parameterized queries:**
- Table has millions of rows
- You always filter by a specific column (like run ID)
- Non-parameterized query times out or is slow

### Authentication

LabKey authentication uses netrc files in standard locations:
- **Unix/macOS**: `~/.netrc`
- **Windows**: `~/.netrc` or `~/_netrc`

The `labkey` SDK reads these automatically. Do not implement custom netrc handling.

### Container Paths

LabKey organizes data in containers (folders). Always use the `container_path` parameter to specify which folder to query:

- `/home/issues/exceptions` - Exception reports
- `/home/development/Nightly x64` - Nightly test results
- `/home/development/Integration` - Integration test results

Note: The `testresults` schema may need to be enabled per-container by a LabKey administrator.

## Future Data Sources

### Gmail Integration

*(To be documented when implemented)*

The `claude.c.skyline@gmail.com` account could receive:
- Exception report notifications
- Nightly test result summaries
- Other automated alerts

Will require Gmail MCP server setup with OAuth2.

### Panoramaweb.org

*(To be documented when implemented)*

Another LabKey server with proteomics datasets. The same patterns apply:
- Use the `labkey` SDK
- Create server-side queries for complex joins
- Configure netrc for authentication

## MCP Server Location

```
pwiz_tools/Skyline/Executables/DevTools/LabKeyMcp/
├── server.py        # MCP server with all tools
├── pyproject.toml   # Dependencies
├── test_connection.py
└── README.md
```

## Related Documentation

- [Developer Setup Guide](developer-setup-guide.md) - Environment configuration
- [Exception Triage System](exception-triage-system.md) - Exception data access
- [Nightly Test Analysis](nightly-test-analysis.md) - Test results data access
