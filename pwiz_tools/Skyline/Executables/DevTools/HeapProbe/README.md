# HeapProbe

Diagnostic that measures how much a native Windows dialog grows the process's Win32 heaps
when it is shown and dismissed repeatedly, using **no Skyline code at all**. It exists to
answer one question when a functional test that drives a native dialog is reported as a
heap-memory leak: is the growth coming from Windows itself (its shell caches for the common
file dialog) or from our own code?

The heap is measured the same way `TestRunner` measures it (`GetProcessHeaps` + `HeapWalk`,
summing the committed BUSY blocks across every process heap), so its numbers are directly
comparable to a test's reported `heap` deltas.

## Author

Nicholas Shulman, MacCoss Lab

## Build

No project or solution — one file, compiled directly with the .NET Framework C# compiler
that ships with Windows:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /platform:x64 /target:exe ^
  /out:HeapProbe.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll HeapProbe.cs
```

Build for `x64` so the heap walk matches the 64-bit test process.

## Usage

```
HeapProbe.exe [save|msgbox] [iterations]
```

- `save` (default) — show and dismiss a native common **Save** file dialog each iteration.
- `msgbox` — show and dismiss a native **MessageBox** each iteration.
- `iterations` — number of measured iterations (default 30).

It warms up a few times, records a baseline, then shows/dismisses the dialog once per
iteration and prints the committed heap and its growth from baseline, ending with the mean
growth per iteration. A background thread finds the dialog (enumerating top-level windows)
and dismisses it with `WM_CLOSE`, so the run is unattended.

## Reading the result

- **Growth that keeps climbing and never plateaus** points at Windows: the common file
  dialog grows the shell caches on the process heap. A bare `SaveFileDialog` loop reproduces
  it; a bare `MessageBox` loop does not. The fix for a test flagged this way belongs in test
  infrastructure (e.g. a warm-up before the leak-check window), not in the connector.
- **Growth that saturates within the first ~20–30 dialogs and then goes flat** is the shell
  cache filling once and is not a leak.

Compare the probe's per-iteration growth on the affected machine against the test's reported
`heap` delta: if the bare probe grows the same way the test does, the test is measuring
Windows, not Skyline.
