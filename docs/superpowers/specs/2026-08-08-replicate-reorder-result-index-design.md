# Preserve Result Indexing During Replicate Reordering

## Problem

`ElementReorderer` changes the order of `MeasuredResults.Chromatograms`, but the Skyline command-line regression test shows that transition peak areas can remain in their former result-index positions. After saving and reopening, a replicate name can therefore display another replicate's quantitative values.

`SrmSettingsDiff.EqualExceptAnnotations` compares chromatogram sets positionally after removing names and selected metadata. Because `ChromatogramSet.Equals` does not compare `ChromatogramSet.Id`, distinct but otherwise equivalent replicates can compare equal after a reorder. `DiffResults` then remains false, preventing the existing ID-based result remapping in `TransitionGroupDocNode` from running.

## Design

Update `SrmSettingsDiff.EqualExceptAnnotations` so chromatogram sets at the same position must retain the same `ChromatogramSet.Id` reference before their non-annotation content can be considered equal. A changed identity at an ordinal position means result indexing changed and must set `DiffResults`.

Keep `ElementReorderer` and the SkylineCmd option on the normal document-model path. Do not add command-specific result-array manipulation. Once `DiffResults` is true, Skyline's existing `MeasuredResults.IdToIndexDictionary` logic maps each new replicate position back to its previous result index.

## Validation

- Retain `ConsoleReorderReplicatesTest` as the end-to-end regression test. It must verify partial and complete reversal, raw-file paths, and transition peak areas after save/reopen.
- Add a focused test around `SrmSettingsDiff` showing that swapping two distinct replicate identities sets `DiffResults`, even when their content is otherwise equivalent after ignored metadata is removed.
- Confirm ordinary metadata-only changes that `EqualExceptAnnotations` intentionally ignores still do not force result recalculation.
- Address the three new ReSharper warnings reported by TeamCity.
- Push the amended branch and require TeamCity's Skyline Windows test and code inspection to pass before marking the PR ready for review.

## Scope

The change is limited to replicate-order detection, regression coverage, and inspection cleanup. It will not add manual result-array permutation, alter file-cache serialization, or change the command-line file format.
