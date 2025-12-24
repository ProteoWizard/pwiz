---
name: skyline-exceptions
description: Use when working with Skyline exception reports, triage, or the LabKey MCP server. Activate for querying exceptions from skyline.ms, analyzing crash reports, reviewing exception patterns, or discussing the exception triage system.
---

# Skyline Exception Triage

When working with Skyline exception data from skyline.ms, consult these documentation files.

## Core Documentation

1. **ai/docs/exception-triage-system.md** - Complete system documentation
   - Architecture and components
   - MCP tools reference
   - Data schema
   - Setup instructions

## When to Read What

- **Before querying exceptions**: Read ai/docs/exception-triage-system.md (MCP tools section)
- **For daily triage**: Read ai/docs/exception-triage-system.md (Daily triage workflow)
- **For setup/debugging**: Read ai/docs/exception-triage-system.md (Setup section)
- **For code changes to MCP server**: Read ai/mcp/LabKeyMcp/README.md

## Quick Reference

**Data location**: skyline.ms → /home/issues/exceptions → announcement.Announcement

**MCP tools available**:
- `query_exceptions(days, max_rows)` - Recent exceptions
- `get_exception_details(exception_id)` - Full stack trace
- `list_schemas`, `list_queries`, `list_containers` - Discovery
- `query_table` - Generic queries

**Title format**:
```
ExceptionType | FileName.cs:line N | Version | InstallIdSuffix
```
