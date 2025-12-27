---
description: Generate support board activity report
---

# Support Board Activity Report

Generate a summary of recent support board activity on skyline.ms.

**Argument**: Number of days to look back (default: 1)

## Instructions

Call `get_support_summary()` with the days parameter:
- If user provided a number (e.g., `/pw-support 7`), use that as the days value
- If no number provided, default to 1 day

Example: `get_support_summary(days=$ARGUMENTS)` where $ARGUMENTS is the number provided, or 1 if none.

The report will be saved to `ai/.tmp/support-report-YYYYMMDD.md`.

## Report Contents

- **Unanswered threads**: New questions that need a response
- **Active threads**: Threads with ongoing discussion

## Follow-up Actions

To read a specific thread, use `get_support_thread(thread_id)`.
Thread content will be saved to `ai/.tmp/support-thread-{id}.md` for review.
