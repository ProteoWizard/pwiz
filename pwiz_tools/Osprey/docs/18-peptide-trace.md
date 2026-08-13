# 18. Peptide Trace Diagnostics (C#)

> Pipeline stage: Cross-cutting (diagnostics). C# port of Rust docs/18-peptide-trace.md. Corresponds to Rust osprey peptide-trace (`OSPREY_TRACE_PEPTIDE`).

## Summary up front

The Rust `OSPREY_TRACE_PEPTIDE` facility — an env-var-gated, `[trace]`-prefixed
per-peptide log narrative keyed by `modified_sequence`, emitted at five pipeline
stages — **has no equivalent in the C# port.** There is no `OSPREY_TRACE_PEPTIDE`
env var, no `trace_set()` / `is_traced()` predicate, no `[trace]` log lines, and no
`--trace-peptide` CLI flag anywhere under `pwiz_tools/Osprey`. This was verified by a
repository-wide search (`OSPREY_TRACE_PEPTIDE`, `TracePeptide`, `is_traced`,
`IsTraced`, `trace_set`, `[trace]` all return zero matches).

What the C# port has instead is a **structured, byte-stable dump architecture** —
the `IOspreyDiagnostics` seam gated by the `OSPREY_DUMP_*` / `OSPREY_DIAG_*` env-var
family — built for a different purpose: cross-implementation byte-diff bisection
against the Rust reference, not human-readable single-peptide storytelling. It
overlaps the same five pipeline stages the Rust trace covers, but it keys on
**`entry_id` (a `uint`)**, writes **TSV/txt files** (not log lines), and dumps
**all entries** (or one selected `entry_id`) rather than a peptide-name subset.

This document describes the C# diagnostics that a Skyline engineer would actually
reach for to answer the per-peptide questions the Rust trace was designed for, then
maps each Rust trace stage to its closest C# dump, and finally records the
divergence precisely.

## The C# diagnostics architecture (what replaces the trace)

### The injection seam: `IOspreyDiagnostics`

`Osprey.Diagnostics/IOspreyDiagnostics.cs:51` defines the pipeline-wide diagnostics
interface. Its doc comment states the design intent directly: it is "the
cross-implementation bisection diagnostics seam for the whole pipeline." Tasks in
the library layer never reach a static dump surface; they emit dumps through an
injected `ctx.Diagnostics?.X` reference (an `IOspreyDiagnostics`), so the task layer
can live in a library that does not reference the top-level exe project. When
diagnostics are off, the injected reference is `null` and every call site
short-circuits through the null-conditional operator (`diag?.ShouldDump() ?? false`,
`diag?.Write(...)`) — a single null check, no virtual dispatch, byte-identical to and
as fast as diagnostics-off.

The concrete sink is `Osprey/OspreyFileDiagnostics.cs:68`
(`internal sealed class OspreyFileDiagnostics : IScoringDiagnostics, IOspreyDiagnostics`).
It reads every `OSPREY_DUMP_*` / `OSPREY_DIAG_*` gate once at process start
(`IsOne(...)`, `ParseNullableUint(...)`, etc.) into readonly properties, and owns
the byte-stable file writers.

### Bootstrap and the `-d` master switch

`Osprey/OspreyDiagnostics.cs:39` is the exe-only bootstrap. `Initialize(bool forceDumps)`
(`OspreyDiagnostics.cs:84`) is called once at pipeline entry:

- If `forceDumps` is true (the `-d`/`--diagnostics` CLI flag), it turns on a bundle
  of `OSPREY_DUMP_*` env vars in-process first (`s_forcedDumpBundle`,
  `OspreyDiagnostics.cs:51`), so the sink picks them up exactly as an external env
  var would.
- It constructs an `OspreyFileDiagnostics` and keeps it as the live sink only if
  `sink.AnyEnabled` (`OspreyFileDiagnostics.cs:410`) is true; otherwise the sink is
  `null` and a production run carries no diagnostic state.

`OspreyDiagnostics.Active` (`OspreyDiagnostics.cs:106`) exposes the sink as
`IOspreyDiagnostics` (or `null`), which the driver injects into `PipelineContext`.

### Shared logging / formatting helpers

`Osprey.Diagnostics/OspreyDiagnosticsLog.cs:34` holds the stateless helpers the task
layer can call without referencing the exe:

- `LogAction` (`OspreyDiagnosticsLog.cs:40`) — the log hook the pipeline points at
  its `LogInfo`, so `[COUNT]` / `[BISECT]` dump messages flow through the normal
  logging channel.
- `F10(double)` (`OspreyDiagnosticsLog.cs:49`) — round-half-to-even 10-decimal
  formatter matching Rust `{:.10}`.
- `ExitAfterDump(string)` (`OspreyDiagnosticsLog.cs:60`) — logs
  `[BISECT] <var> set - aborting after dump` and calls `Environment.Exit(0)`, used
  by the `*_ONLY` gates.

The round-trip f64 formatter used by Stage 5+ dumps lives in
`Osprey.Core/Diagnostics.cs:60` (`FormatF64Roundtrip`), which emits the shortest
decimal that round-trips to the same f64 bit pattern — matching Rust's ryu-based
`format!("{}", v)` byte-for-byte. This is the C# counterpart of Rust's
`osprey_core::diagnostics` module and is covered by `Osprey.Test/DiagnosticsTest.cs`.

## Mapping the five Rust trace stages to C# dumps

The Rust trace emits at five stages. Here is where a C# engineer gets the
equivalent information. All C# dumps are declared on `IOspreyDiagnostics`
(`Osprey.Diagnostics/IOspreyDiagnostics.cs`) and implemented in
`Osprey/OspreyFileDiagnostics.cs`.

### 1. First-pass CWT peak scoring — Rust `[trace] ... CWT candidates`

Closest C# dumps:

- `OSPREY_DUMP_CWT_PATH` → `WriteCwtPathRow` / `CloseCwtPathDump`
  (`IOspreyDiagnostics.cs:181`, gate `DumpCwtPath` at `OspreyFileDiagnostics.cs:248`).
  Emits `cs_stage6_cwt_path.tsv`: per-(file, entry_id) counts of CWT peaks, final
  peaks after median-polish and reference-XIC fallbacks, peaks that passed the
  apex-acceptance filter, and a "scored" flag. This localizes *whether* CWT found a
  peak for an entry and at which seam it was lost — the C# analog of the Rust trace's
  "did CWT actually find the correct peak?" question, but as counts, not per-candidate
  penalized-score rows.
- `OSPREY_DIAG_SEARCH_ENTRY_IDS=<ids>` → `ShouldDumpSearchXicFor` / `WriteSearchXicDump`
  (`IOspreyDiagnostics.cs:156`, `OspreyFileDiagnostics.cs:989`). For a comma-separated
  set of `entry_id`s, dumps `cs_search_xic_entry_<ID>.txt` with the full extracted XIC,
  candidate scans, and RT window during the main search. This is the deepest per-entry
  view — it exposes the raw chromatogram the Rust trace's `coelution`/`apex`/`intensity`
  fields summarize.

The Rust trace's explicit per-candidate ranking table (rank 0..N with `coelution`,
`rt_penalty`, `int_weight`, `penalized`) has no direct C# TSV equivalent; the C#
side exposes the inputs (XIC) and the outcome counts (CWT path) rather than the
intermediate penalized-score ledger.

### 2. `compute_consensus_rts` — Rust `[trace] consensus ...`

Closest C# dumps:

- `OSPREY_DUMP_CONSENSUS` → `WriteStage6ConsensusDump` (`IOspreyDiagnostics.cs:195`,
  gate `DumpConsensus` at `OspreyFileDiagnostics.cs:255`). Emits
  `cs_stage6_consensus.tsv`: the per-peptide consensus-RT planning state at the start
  of Stage 6 (`consensus_library_rt`, median peak width) — the C# analog of the Rust
  trace's `consensus ... → consensus_library_rt=..., median_peak_width=...` summary line.
- `OSPREY_DUMP_INV_PREDICT` → `WriteStage6InvPredictDump` (`IOspreyDiagnostics.cs:211`,
  gate `DumpInvPredict` at `OspreyFileDiagnostics.cs:318`). Emits
  `cs_stage6_inv_predict.tsv`: the per-detection `(apex_rt, library_rt, weight)` triples
  flowing into `ConsensusRts.Compute`. This is the C# analog of the Rust trace's
  per-detection `file=... apex_rt=... weight=...` rows — the weight is the
  `sigmoid(SVM score)` weighted-median weight
  (`Osprey.FDR/Reconciliation/ConsensusRts.cs:75,182`;
  `Osprey.FDR/Reconciliation/InvPredictRecord.cs`).

Note the C# `InvPredictRecord` is itself internally called a "cross-impl bisection
trace" in its own doc comment (`InvPredictRecord.cs:27`) — but it is a structured
TSV record keyed by the detection, not the `[trace]`-log facility from the Rust doc.

### 3. `plan_reconciliation` — Rust `[trace] plan ...`

Closest C# dump:

- `OSPREY_DUMP_RECONCILIATION` → `WriteStage6ReconciliationDump`
  (`IOspreyDiagnostics.cs:204`, gate `DumpReconciliation` at
  `OspreyFileDiagnostics.cs:287`). Emits `cs_stage6_reconciliation.tsv`: the
  per-(file, entry_id) `ReconcileAction` map produced by the reconciliation planner,
  with the action variant plus apex / start / end / half-width fields — the C# analog
  of the Rust trace's `→ <action>` (`Keep` / `UseCwtPeak` / `ForcedIntegration`) line.
  See `11-boundary-overrides.md` and `10-cross-run-reconciliation.md` for the action
  semantics.

The Rust trace's tolerance-derivation narrative (`global_MAD`, `this_peptide_MAD`,
`ceiling`, and the stored `cwt[i]` candidate rows) is not reproduced field-for-field
in the C# TSV; the C# dump reports the chosen action and its integration window.

### 4. Gap-fill identification — Rust `[trace] gap-fill: ...`

The C# port folds gap-fill targets into the same Stage 6 planning path. The closest
dumps are `OSPREY_DUMP_RECONCILIATION` (above) and `OSPREY_DUMP_MULTICHARGE` →
`WriteStage6MultichargeDump` (`IOspreyDiagnostics.cs:197`, gate `DumpMulticharge` at
`OspreyFileDiagnostics.cs:264`), which emits `cs_stage6_multicharge.tsv` — the per-file
rescore targets. There is no dedicated `gap-fill` trace line; a missing-in-this-file
target surfaces as a forced-integration `ReconcileAction` row rather than a distinct
`gap-fill:` message.

### 5. First-pass and second-pass FDR q-values — Rust `[trace] fdr(<stage>) ...`

Closest C# dumps:

- First pass: `OSPREY_DUMP_PERCOLATOR` → `WriteStage5PercolatorDump`
  (`IOspreyDiagnostics.cs:191`, gate `DumpPercolator` at `OspreyFileDiagnostics.cs:150`).
  Emits `cs_stage5_percolator.tsv`: per-precursor `score`, `pep`, and the four q-values
  after first-pass Percolator FDR and before protein FDR / compaction.
- Second pass: `OSPREY_DUMP_RESCORED` → `WriteStage6RescoredDump`
  (`IOspreyDiagnostics.cs:193`, gate `DumpRescored` at `OspreyFileDiagnostics.cs:200`).
  Emits `cs_stage6_rescored.tsv` with the same column shape after the Stage 6 rescore
  (consensus + reconciliation overlay). Its doc comment states it "Mirrors Rust's
  dump_stage6_rescored / OSPREY_DUMP_RESCORED gate."

Together these give the C# analog of the Rust trace's `fdr(first-pass)` /
`fdr(second-pass)` per-entry q-value lines — the disposition of a precursor at both
passes — but as full-population TSVs keyed by entry, not per-peptide log lines. See
`07-fdr-control.md`.

## Per-entry selectors (the closest thing to "trace one peptide")

Where the Rust trace narrows to a `modified_sequence`, the C# system narrows by
identifier instead:

- `OSPREY_DIAG_XIC_ENTRY_ID=<id>` + `OSPREY_DIAG_XIC_PASS={1,2}`
  (`DiagXicEntryId`, `OspreyFileDiagnostics.cs:379`; `DiagXicPass`,
  `OspreyFileDiagnostics.cs:386`) — dumps `cs_xic_entry_<ID>.txt` for one entry during
  calibration scoring, then `Environment.Exit(0)` (`ShouldDumpCalXicFor` /
  `WriteCalXicEntryDumpAndExit`, `OspreyFileDiagnostics.cs:880,895`).
- `OSPREY_DIAG_SEARCH_ENTRY_IDS=<id,id,...>` (`DiagSearchEntryIds`,
  `OspreyFileDiagnostics.cs:393`) — non-exiting per-entry search-XIC dump for a set of
  entries.
- `OSPREY_DIAG_MP_SCAN=<scan>` (`DiagMpScan`, `OspreyFileDiagnostics.cs:400`) — dumps
  the median-polish inputs for one MS2 scan, additionally filtered "to a specific
  DECOY_ALQFAQWWK target by historical convention." This is the only place a
  peptide-name-like filter appears, and it is hardcoded, not user-supplied.

The practical consequence: to trace "one peptide" in C#, an engineer first resolves
its `entry_id` (from the library load order or a `cs_cal_*` dump), then sets the
`OSPREY_DIAG_*` selector — a two-step, id-based workflow, versus the Rust trace's
direct `OSPREY_TRACE_PEPTIDE=<modified_sequence>` name match.

## Cache invalidation for a clean run

The Rust doc's advice to delete per-file caches so the traced code paths actually
execute applies to the C# port's dumps too: the `.scores.parquet` caches and
`.1st-pass.fdr_scores.bin` / `.2nd-pass.fdr_scores.bin` FDR score sidecars let the
C# pipeline shortcut past scoring and Percolator on reruns (skip-Percolator fast
path). To force the Stage 4/5/6 producers to re-run and emit their dumps, clear the
per-file caches first. See `14-intermediate-files.md` for the C# cache/sidecar layout
and the `CacheValidity` invalidation rules.

## Flags and switches

This stage is diagnostics-only; nothing here changes search outputs. All gates
default **off** (unset), so a production run carries no diagnostic state.

| Flag / env var | Default | Effect on this stage |
|---|---|---|
| `-d` / `--diagnostics` (`OspreyCommandArgs.cs:283`) | off | Master switch: forces on the `OSPREY_DUMP_*` bundle (`OspreyDiagnostics.cs:51,84`) so the structured dumps are written. Sets `config.Diagnostics = true`. |
| `--model-diagnostics` (`OspreyCommandArgs.cs:285`) | off | Writes a self-contained interactive HTML report of the trained scoring model and FDR calibration. Unrelated to per-peptide tracing. |
| `OSPREY_DUMP_CWT_PATH` (`OspreyFileDiagnostics.cs:248`) | off | `cs_stage6_cwt_path.tsv` — per-(file, entry) CWT path counts (Rust trace stage 1 analog). |
| `OSPREY_DIAG_SEARCH_ENTRY_IDS=<ids>` (`OspreyFileDiagnostics.cs:393`) | unset | `cs_search_xic_entry_<ID>.txt` per selected entry during main search (deepest per-entry XIC view). Non-exiting. |
| `OSPREY_DUMP_CONSENSUS` (`OspreyFileDiagnostics.cs:255`) | off | `cs_stage6_consensus.tsv` — per-peptide consensus RT planning (Rust trace stage 2 analog). |
| `OSPREY_DUMP_INV_PREDICT` (`OspreyFileDiagnostics.cs:318`) | off | `cs_stage6_inv_predict.tsv` — per-detection `(apex_rt, library_rt, weight)` into consensus (Rust trace stage 2 per-detection analog). |
| `OSPREY_DUMP_RECONCILIATION` (`OspreyFileDiagnostics.cs:287`) | off | `cs_stage6_reconciliation.tsv` — per-entry `ReconcileAction` (Rust trace stage 3/4 analog). |
| `OSPREY_DUMP_MULTICHARGE` (`OspreyFileDiagnostics.cs:264`) | off | `cs_stage6_multicharge.tsv` — per-file rescore targets. |
| `OSPREY_DUMP_PERCOLATOR` (`OspreyFileDiagnostics.cs:150`) | off | `cs_stage5_percolator.tsv` — first-pass per-precursor score/pep/q-values (Rust trace stage 5 first-pass analog). |
| `OSPREY_DUMP_RESCORED` (`OspreyFileDiagnostics.cs:200`) | off | `cs_stage6_rescored.tsv` — second-pass per-precursor score/pep/q-values (Rust trace stage 5 second-pass analog). |
| `OSPREY_DIAG_XIC_ENTRY_ID=<id>` (`OspreyFileDiagnostics.cs:379`) | unset | `cs_xic_entry_<ID>.txt` for one entry during calibration; exits after dump. |
| `OSPREY_DIAG_XIC_PASS={1,2}` (`OspreyFileDiagnostics.cs:386`) | 1 | Selects the calibration pass for the XIC dump. |
| `OSPREY_DIAG_MP_SCAN=<scan>` (`OspreyFileDiagnostics.cs:400`) | unset | Dumps median-polish inputs for one scan (hardcoded DECOY_ALQFAQWWK filter). |
| `OSPREY_<NAME>_ONLY=1` (per gate, e.g. `OSPREY_CONSENSUS_ONLY`) | off | Exit-after-dump companion for many gates; calls `ExitAfterDump` (`OspreyDiagnosticsLog.cs:60`). |
| **`OSPREY_TRACE_PEPTIDE`** | **N/A** | **Not implemented in C#.** No env var, no CLI flag, no code path. See divergences below. |

The full `OSPREY_DUMP_*` / `OSPREY_DIAG_*` roster (stages 1–7) is enumerated on
`IOspreyDiagnostics` (`Osprey.Diagnostics/IOspreyDiagnostics.cs:53-109`) and gated in
`OspreyFileDiagnostics.cs:84-423`; only the subset relevant to per-peptide behavior
is tabulated here. The README documents the bisection env vars at
`README.md:238-251`.

## Divergences from the Rust documentation

- **[INTENTIONAL-CSHARP-DESIGN] `OSPREY_TRACE_PEPTIDE` per-peptide trace facility is absent; replaced by a structured dump architecture** - Rust doc 17 says an env-var-gated `[trace]` log facility keyed by `modified_sequence` emits per-peptide narrative at five pipeline stages, with `OSPREY_TRACE_PEPTIDE=<seq>` matching bare modified sequences (and paired `DECOY_<target>` automatically), an `OnceLock`-guarded `is_traced()` probe, and `log_fdr_qvalues()` after each FDR pass. C# implements none of this: repository-wide search for `OSPREY_TRACE_PEPTIDE`, `TracePeptide`, `is_traced`/`IsTraced`, `trace_set`/`TraceSet`, and `[trace]` returns zero matches. Instead the C# port carries the `IOspreyDiagnostics` seam gated by the `OSPREY_DUMP_*` / `OSPREY_DIAG_*` family — a byte-stable, TSV/txt, `entry_id`-keyed dump system built for cross-impl bisection (its own doc comment: "the cross-implementation bisection diagnostics seam for the whole pipeline"). Evidence: `Osprey.Diagnostics/IOspreyDiagnostics.cs:51`; gates at `Osprey/OspreyFileDiagnostics.cs:84-423`; bootstrap `Osprey/OspreyDiagnostics.cs:39-107`; CLI `Osprey/OspreyCommandArgs.cs:283-285` (only `-d` and `--model-diagnostics`, no trace flag). Severity: major. Rationale for classification: diagnostics are output-neutral (they cannot change search results), and the C# port carries a deliberate, elaborate alternative diagnostics architecture serving the same five stages — this is an infrastructure design difference, not a behavioral port error. A reader wanting the Rust workflow will not find it; use the mapped `OSPREY_DUMP_*` dumps instead.

- **[INTENTIONAL-CSHARP-DESIGN] Selection is by `entry_id`, not `modified_sequence`** - Rust doc 17 selects the peptide(s) to trace by bare `modified_sequence` (comma-separated), matching all charge states and the paired decoy automatically. The C# per-entry selectors key on numeric `entry_id` instead (`OSPREY_DIAG_XIC_ENTRY_ID`, `OSPREY_DIAG_SEARCH_ENTRY_IDS`) or MS2 scan number (`OSPREY_DIAG_MP_SCAN`), so narrowing to "one peptide" requires resolving its id first and does not auto-group charge states or paired decoys. Evidence: `Osprey/OspreyFileDiagnostics.cs:379,393,400`. Severity: minor.

- **[INTENTIONAL-CSHARP-DESIGN] Dumps are byte-stable files diffed cross-impl, not interleaved `[trace]` log lines** - Rust doc 17 emits trace at `log::info!` sharing a `[trace]` prefix, interleaved with normal log output and grep-isolated, preserved in the log file. The C# dumps write dedicated TSV/txt files (e.g. `cs_stage6_consensus.tsv`) with round-half-to-even (`F10`, `OspreyDiagnosticsLog.cs:49`) and shortest-round-trip f64 (`FormatF64Roundtrip`, `Osprey.Core/Diagnostics.cs:60`) formatting so they can be SHA-256/numerically diffed against the Rust reference dumps; only `[COUNT]` / `[BISECT]` status lines flow to the log. Evidence: `Osprey/OspreyFileDiagnostics.cs:68-82` (LF newline discipline), `Osprey.Test/DiagnosticsTest.cs`. Severity: minor.

- **[STALE-RUST-DOC] `OSPREY_DUMP_PREDICT_RT` producer is disabled in C#** - Not a Rust-doc-17 claim per se, but relevant to anyone tracing RT prediction per entry: the C# `OSPREY_DUMP_PREDICT_RT` gate throws `NotImplementedException` at sink construction because its per-candidate producer was removed from the scoring hotspot; the doc comment names exactly what to uncomment to restore it. Evidence: `Osprey/OspreyFileDiagnostics.cs:431-445`. Severity: info.

### What was verified

- Read Rust `docs/18-peptide-trace.md` in full (all five trace stages, enabling,
  cache-invalidation, workflows, and the implementation table).
- Confirmed by repository-wide search that no C# symbol implements
  `OSPREY_TRACE_PEPTIDE`, `is_traced`, `trace_set`, or `[trace]` logging.
- Read the C# diagnostics seam end-to-end: `IOspreyDiagnostics.cs`,
  `OspreyDiagnosticsLog.cs`, `OspreyDiagnostics.cs`, `OspreyFileDiagnostics.cs`
  (gates + per-entry XIC/consensus/reconciliation/FDR dump methods),
  `Osprey.Core/Diagnostics.cs`, `Osprey.Core/OspreyEnvironment.cs`, and
  `Osprey.Test/DiagnosticsTest.cs`.
- Confirmed the CLI exposes only `-d`/`--diagnostics` and `--model-diagnostics` for
  this area (`OspreyCommandArgs.cs:283-285`); no trace flag.

See also: `14-intermediate-files.md` (caches and sidecars whose fast paths shortcut
past traced/dumped producers), `10-cross-run-reconciliation.md` and
`11-boundary-overrides.md` (consensus + `ReconcileAction` semantics),
`07-fdr-control.md` (the first/second-pass q-values surfaced by the Percolator /
Rescored dumps), and `19-testing.md` (the cross-impl bisection methodology the dump
system feeds).
