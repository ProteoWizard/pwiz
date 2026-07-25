# WindowChurnProbe

Does creating and destroying Win32 windows leak native heap on **this machine**?

A standalone diagnostic with **no Skyline code**, for settling why nightly reports heap leaks
on the native-dialog / connector test family on some agents but not others.

## Why it exists

On 2026-07-24, four tests were reported leaking heap by nightly, and every other test in the
1065-test run was under the 20 KB threshold:

| test | heap KB/run (BRENDANX-UW7) | windows created per run |
|---|---:|---|
| `TestNativeMessageBox` | 171.6 | 3 native Save dialogs + 2 message boxes |
| `TestMcpConnectorBackgroundDialog` | 126.5 | ~100,000 grid editing controls (before it was rewritten) |
| `TestPrmMcpConnector` | 52.5 | 1 native file dialog |
| `TestNativeFileDialog` | 41.8 | 1 native file dialog |
| *ambient for an ordinary functional test* | *16–20* | |

But on the same commit (`a09eea912`), **RITACH-DSK and KAIPOT-PC1 reported zero leaks**, and the
two worst tests were flat there (−1.0 and −3.1 KB). Same code, opposite result, so the difference
is environmental. The leading explanation is that window create/destroy leaks native heap in a
**Terminal Services (remoted display) session**, and that the leaking tests are simply the ones
that create an unusual number of windows.

This probe tests that claim directly.

## Build and run

```
csc.exe /platform:x64 /target:exe /out:WindowChurnProbe.exe ^
        /r:System.Windows.Forms.dll /r:System.Drawing.dll WindowChurnProbe.cs

WindowChurnProbe.exe child  20000    REM child windows, as a grid editing control is
WindowChurnProbe.exe form    2000    REM top-level forms
WindowChurnProbe.exe dialog   300    REM modal dialogs
WindowChurnProbe.exe idle   20000    REM control: no windows at all
```

It prints `TerminalServerSession`, `SESSIONNAME` and `MonitorCount` first, so the output records
which kind of session produced it.

Committed heap is measured exactly the way TestRunner measures it (`GetProcessHeaps` + `HeapWalk`,
summing BUSY blocks — see `TestRunnerLib/RunTests.cs`, `MemoryManagement.GetProcessHeapSizes`), so
the numbers are directly comparable to the `heap` column in a nightly log.

## Reading the result

- **Plateaus** (bytes/iter decays toward zero, delta stops rising) — window churn is free here.
- **Dead linear** (bytes/iter stays roughly constant, no plateau) — it is not free, and
  `bytes/iter x windows-per-run` should account for that test's nightly heap number.

## Reference: a machine that does NOT report the leak

nicksh's machine, 2026-07-25, `TerminalServerSession=False`, `SESSIONNAME=RDP-Tcp#0`,
`MonitorCount=2` (note the mismatch — `TerminalServerSession` is not a reliable way to detect a
remoted session):

| mode | iterations | final delta | shape |
|---|---:|---:|---|
| `idle` | 20,000 | 864 B | flat |
| `child` | 20,000 | 591 KB | plateaus — 477 KB by iter 1,000, then oscillates 525–635 KB; bytes/iter 477 → 29.6 |
| `form` | 2,000 | 44.7 KB | plateaus — constant from iter 1,800 |
| `dialog` | 300 | 19.9 KB | plateaus — constant from iter 285 |

Nothing here grows without bound. **What is still needed is the same table from a machine that DOES
report the leak** (BRENDANX-UW7, SKYLINE-DEV6, BOSS-PC, …). If `child` is dead-linear there at a
few hundred bytes per iteration, the mechanism is confirmed and the fix for any affected test is to
reduce how many windows it creates.
