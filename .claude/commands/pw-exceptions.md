---
description: Generate daily exception report
---

# Daily Exception Report

Generate a summary of recent exceptions submitted by Skyline users.

**Argument**: Number of days to look back (default: 1)

## Instructions

Call `query_exceptions()` with the days parameter:
- If user provided a number (e.g., `/pw-exceptions 7`), use that as the days value
- If no number provided, default to 1 day

Example: `query_exceptions(days=$ARGUMENTS)` where $ARGUMENTS is the number provided, or 1 if none.

## Report Contents

- **Exception type and title**
- **User comments and email** (if provided)
- **Stack trace** (in FormattedBody)
- **Created/Modified dates**
- **Skyline version**

## Follow-up Actions

To get full details for a specific exception:
```
get_exception_details(exception_id)
```

This returns:
- Full stack trace
- User email and comments
- Skyline version
- Installation ID

For detailed triage workflow, see **ai/docs/exception-triage-system.md**.
