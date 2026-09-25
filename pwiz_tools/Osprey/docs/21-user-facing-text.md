# 21. User-Facing Text (C#)

> Applies to every `LogInfo` / `LogWarning` / `LogError` line, every `ProgressReporter`
> label, and every CLI error. Corresponds to `Osprey.Core/OspreyOutput.cs` (the one
> output seam) and the `[TASK]` markers parsed by `perfviz.py` and `regression.ps1`.

The log is the only UI Osprey has. Its reader is a mass spectrometrist running a search,
not the developer who named the classes, and many of the words in the code came from
class and method names during development. This page records which of those words never
appear in default-tier text, what replaces them, and the small set of dev terms that
stay. It is a review checklist: run every new or edited user-facing string against it.

## Words that never appear in user text

| Developer term | User text | Why |
|---|---|---|
| sidecar | **intermediate file** / intermediate results file / intermediate analysis file | "Intermediate" reads instantly as "not the end result". "Sidecar" was never the user's word. |
| entries | **precursor candidates (targets + decoys)** | "entries" is shorthand for a library row, coined for memory assertions ("no O(files x entries)"). |
| base_id(s) | **precursor candidates** | Same reason; consistency with the line above. |
| compaction | say what was kept and why | The operation name says nothing about the result. |
| stratum / protein-compact subset | **precursor candidates from proteins with 2 or more detections** | A noun swap ("set") is as opaque as the original; the phrase names the criterion. "protein-compact subset" is acceptable only where the flag itself is named. "Detections" is the general word for passing the current cutoff. |
| boundary file pair | **intermediate files** | Do not add "that the next task needs". |
| bundle / envelope / reconciliation plan | **cross-run reconciliation files** | "Reconciliation files" already implies intermediate; do not stack the two words. |
| results (for anything written beside an input) | **intermediate file** | "Results" is reserved for the end results a user looks at. |
| use_cwt / forced / gap-fill (reconciliation actions) | **peaks re-picked** / **peak boundaries imputed** / **peak boundaries imputed for missing peaks** | Skyline's "peak boundary imputation" vocabulary. |
| frozen model / no retrain | (dropped) | Records what the code no longer does. Diagnostic channel only if a test needs it. |
| resident / byproduct / interned / hydrate | (dropped) | Implementation words. If a test needs them, they are diagnostic output, not user text. |
| fold (verb) | (dropped) | Even as a dev term. |
| Stage N | the `--task` name | The CLI help documents the task names; nothing documents stage numbers for a user. |
| class, method and env-var names | (dropped from default-tier lines) | Code-path narration goes behind `--verbose`. |

## Terms that stay

- **reconciliation** / **cross-run reconciliation**: not a standard term, but "cross-run"
  carries the meaning and the nearest published concept (TRIC, transfer of identification
  confidence) is no clearer.
- **coelution**: a known DIA term; Skyline has a "coelution score".
- **`[TASK] Name:status` markers**: unchanged; `perfviz.py` and `regression.ps1` parse them.
- The `--task` names (`PerFileScoring`, `FirstPassFDR`, `PerFileRescoring`,
  `SecondPassFDR`): documented in the CLI help.

Still open, with no objection recorded to the proposals: survivors, scalars, competition,
worker.

## Numbers

Every count gets a thousands separator (`{0:N0}`). Counts of five or more digits
(1448698, 2110341) cannot be read for magnitude without one; at the 2026-09-09 review only
3 of 582 call sites used it.

A count states its denominator where the reader needs scale: "kept 996,439 of 1,448,698
precursor candidates". Never print two counts that look like the same quantity on
neighbouring lines.

## Grammar

No possessive on an inanimate noun; use the noun attributively or rephrase. "precursor
candidate q-values", "the best charge state of the same peptide", "the inputs of this run",
not "the peptide's best charge state" or "this run's inputs".

## Outcomes, not mechanisms

A noun swap does not fix a data-structure word. Say the outcome in the reader's terms:
which candidates get new q-values, what was kept, what will be imputed. A line does not
have to report an action; a plain outcome ("Unique library strings: 1,433,253 / 5,241,881
total (72.7% reduced)") is often the clearer form, where "Interned library strings ...
collapsed" named the code's operation. A warning keeps the one sentence the user needs and
the remedy they can apply; the mechanism, issue numbers and measured costs go in a code
comment beside the call.

## Enums

Use Skyline's pattern: a `GetLocalizedString(this Enum)` extension over a
`LOCALIZED_VALUES` array (see `Skyline/Model/Export.cs`), so the text can move to RESX
without touching the call sites. `DecoyMethod`, `ResolutionMode`, `FdrMethod` and
`LibraryFormat` have one; never print an enum's `ToString()` in user text.

## Tiers

- **Default**: what the researcher reads. Everything above applies.
- **`--verbose`**: code-path narration, per-file detail, the feature table.
- **`-d` diagnostics**: developer output; may use any vocabulary, and lives off the
  mainline path (see `00-pipeline-architecture.md`).
