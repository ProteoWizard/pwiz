---
name: debugging
description: Use when investigating bugs, failures, leaks, crashes, or unexpected behavior. Activate when user describes something not working as expected, provides error messages or stack traces, mentions nightly test failures, or asks "why is this happening?" This skill shifts Claude into debugging mode with systematic investigation techniques.
---

# Debugging Mode

When investigating bugs, failures, or unexpected behavior, shift into debugging mode. This is fundamentally different from development mode.

## Core Documentation

Read these files before investigating:

1. **ai/docs/debugging-principles.md** - Complete methodology
   - Cycle time analysis
   - Printf debugging techniques
   - Bisection methodology
   - Long-cycle/intermittent strategies
   - Diagnostic output toolkit

2. **ai/docs/leak-debugging-guide.md** - Handle/memory leak specifics
   - Handle types and their causes
   - TestRunner flags (`-ReportHandles`, `-SortHandlesByCount`)
   - Case studies with detailed bisection examples

## The First Questions

Always start by answering these:

1. **Can it be reproduced?** → Determines entire strategy
2. **What is the cycle time?** → How long to confirm presence/absence?
3. **Can cycle time be reduced?** → Invest effort here first
4. **What is reproduction confidence?** → 100% vs intermittent

## Quick Reference: Strategy by Cycle Time

| Cycle Time | Strategy |
|------------|----------|
| < 1 min | Printf debugging, rapid bisection, **be self-sufficient** |
| 1-60 min | Batch diagnostics, careful hypothesis |
| Hours+ | Statistical bisection or strategic instrumentation |
| Intermittent only | DocChangeLogger pattern, deploy and wait |

## Critical Principle: Self-Sufficiency

> **Never ask the user about runtime behavior you can observe yourself.**

When cycle time is fast:
- Don't ask "is it the same object?" → Add `Console.WriteLine($"Instance: {RuntimeHelpers.GetHashCode(obj)}")`
- Don't ask "what thread?" → Add `Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}")`
- Don't ask the user to run the debugger → Instrument the code with printf statements and run the test yourself

You can answer your own questions faster and more comprehensively than any human operating a debugger.

## Diagnostic Toolkit

```csharp
// Object identity
Console.WriteLine($"Instance: {RuntimeHelpers.GetHashCode(obj)}");

// Thread identity
Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}");

// Call stack
Console.WriteLine(Environment.StackTrace);

// Method entry with context
Console.WriteLine($"[DEBUG] {nameof(MethodName)}: param={value}");
```

## Bisection Pattern

```csharp
protected override void DoTest()
{
    // ... first half ...

    return; // BISECT: Testing if bug is in first half

    // ... second half ...
}
```

If bug present → it's before the return
If bug absent → it's after the return
Repeat to isolate.

## Related Skills

- **skyline-nightlytests** - Query nightly test data, find affected computers
- **skyline-exceptions** - Query exception reports from skyline.ms
- **skyline-development** - For implementing fixes after isolation

## When to Activate This Skill

Recognize debugging mode when user:
- Describes unexpected behavior
- Mentions failures, crashes, leaks, errors
- References test results or exception reports
- Asks "why?" rather than "how do I build?"
- Provides stack traces or error messages

**The mode shift:** Stop thinking "how do I implement?" → Start thinking "how do I observe and isolate?"
