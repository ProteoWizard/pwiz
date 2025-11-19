# Phase 2: UI‑free Model and report export backlog

Status: Planned (merge Phase 1 first)
Scope: Finish eliminating Model→UI references and strengthen static checks so new violations can’t sneak in. Defer risky refactors until after merge to master.

## Objectives
- Zero Model→UI dependencies reported by CodeInspectionTest
- Headless (UI‑free) report export for ReportSharing, ToolDescription, and CommandLine
- Tighten static checks to catch current loopholes
- Clarify utility layering (Model‑only vs UI‑capable) so Model code can’t accidentally call UI helpers

## Deliverables (high level)
1) Headless report export API (no UI types)
- Model/Databinding/Export/ReportExporter.cs
  - ExportToFile(SkylineDataSchema dataSchema, ViewInfo viewInfo, string filePath, char separator, ViewLayout viewLayout = null, CancellationToken? token = null)
  - Export(SkylineDataSchema dataSchema, ViewInfo viewInfo, TextWriter writer, char separator, ViewLayout viewLayout = null, CancellationToken? token = null)
  - CreateDsvWriter(dataSchema, separator): ensures RoundTrip numeric format when localizer is INVARIANT
- Model/Databinding/Export/ReplicatePivotWriter.cs
  - WriteDataWithStatus(DsvWriter dsvWriter, TextWriter writer, RowItemEnumerator enumerator, IProgressMonitor pm, ref IProgressStatus status)
  - Returns true if it handled pivoted replicate layout; else caller falls back to default writer
- Controls/Databinding/SkylineViewContext
  - Call ReplicatePivotWriter first in WriteDataWithStatus; on false, fall back to existing behavior (ensures identical output UI vs headless)

2) Replace UI view‑context usage in Model and CLI
- Model/Databinding/ReportSharing.cs
  - Replace DocumentGridViewContext with ReportExporter
  - Build SkylineDataSchema with DataSchemaLocalizer.INVARIANT, construct ViewInfo, and export
- Model/Tools/ToolDescription.cs
  - Same as ReportSharing: invariant localization, ViewInfo, ReportExporter
- SkylineCmd (CLI)
  - Add/report export path using the same ReportExporter API (input doc, report name/spec, output, CSV/TSV, invariant flag)

3) Utility folder split and guardrails
- Create Model/Util and move truly model‑only utilities here (no UI deps)
- Rename Skyline/Util → Skyline/UtilUI (or split into Skyline/UtilUI and Skyline/UtilShared if needed)
- Update namespaces accordingly (pwiz.Skyline.Model.Util, pwiz.Skyline.UtilUI)
- Add CodeInspectionTest rules so Model files cannot:
  - using pwiz.Skyline.UtilUI
  - Reference fully‑qualified pwiz.Skyline.UtilUI.*
- Add a short guide: “When to add to Model/Util vs UtilUI”

## CodeInspectionTest: tighten rules (address known loopholes)
Current rules (as of 2025‑11‑04):
- For files containing "namespace pwiz.Skyline.Model":
  - Forbid using of pwiz.Skyline.(Alerts|Controls|.*UI), System.Windows.Forms, pwiz.Common.GUI
  - Forbid fully‑qualified references to those namespaces
- Similar checks for CommandLine.cs and CommandArgs.cs

Loopholes and proposals:
1) Folder‑based scan (not namespace‑based) for Model
- Problem: A file under Model/ that doesn’t declare namespace pwiz.Skyline.Model (e.g., odd nesting or generated code) can evade checks.
- Proposal: Add a second inspection scoped by path pattern: pwiz_tools/Skyline/Model/** (case‑insensitive, Windows separators). Apply the same forbidden patterns regardless of namespace.

2) Transitive UI via Common or shared helpers
- Problem: Moving a UI‑dependent helper into Common (e.g., a NonUiViewContext that actually uses WinForms) won’t be flagged by Model checks if Model only sees the Common type names.
- Pragmatic guardrails (regex‑based):
  - Forbid Model from referencing types in known UI namespaces in Common:
    - pwiz.Common.DataBinding.Controls
    - pwiz.Common.GUI
  - (Already covered) Ensure fully‑qualified patterns catch these as well.
- Stretch goal (post‑merge): Roslyn‑based symbol inspection to fail if any type referenced from Model resolves to namespaces containing ".Controls" or ".UI" (across all projects). This avoids false negatives due to transitive dependencies.

3) Skyline/Util mixing model and UI
- Problem: Model can "using pwiz.Skyline.Util" and reach UI code that lives there.
- Proposal:
  - Split: Model/Util (model‑only) and Skyline/UtilUI (UI)
  - Add CodeInspectionTest rules:
    - For Model files: Forbid using/fully‑qualified references to pwiz.Skyline.UtilUI
    - Optional: Temporarily forbid using pwiz.Skyline.Util entirely until split is complete

4) Aliases and disguised imports
- Existing regex catches "using UI = pwiz.Skyline.Controls;" due to the namespace token; keep it
- Add check for extern aliases if introduced (rare): forbid lines like "extern alias" if used to reach UI assemblies from Model

5) Non‑code leaks
- String‑based Type.GetType("pwiz.Skyline.Controls.DocumentGridViewContext") is caught by current fully‑qualified regex; keep this behavior

6) Tolerance counters
- Bring Model tolerance to 0 after Phase 1 merge
- Keep CLI tolerance at 0 after CommandLine paths are moved to headless export

## Tasks and milestones
- T0 (post‑merge): Set Model UI‑dependency tolerance to 0 in CodeInspectionTest
- T1: Add folder‑based Model scan (same forbidden patterns) in CodeInspectionTest
- T2: Add Util split guardrails (forbid pwiz.Skyline.UtilUI in Model)
- T3: Implement ReportExporter + ReplicatePivotWriter in Model; refactor SkylineViewContext to use shared writer
- T4: Replace DocumentGridViewContext usage in ReportSharing and ToolDescription with ReportExporter
- T5: Add CLI export via ReportExporter; drop CLI tolerance to 0
- T6: Audit Common for any WinForms usage that could be pulled by Model; if needed, move UI bits to Common.GUI and ensure tests forbid Model from referencing it

## Acceptance criteria
- CodeInspectionTest: PASS with 0 Model violations and 0 CLI violations
- Report export parity: Exports produced by UI (Save…) and ReportExporter are byte‑for‑byte identical for a sample set (CSV/TSV, with/without pivot)
- No new references to System.Windows.Forms or UI namespaces in Model files by path or namespace

## Risks and mitigations
- Risk: Refactoring BindingListSource to avoid ViewContext requires Common changes
  - Mitigation: Introduce a minimal non‑interactive context in Common, or add SetView(IDataSchema, ViewInfo) overload
- Risk: Util split churns many files
  - Mitigation: Start by moving obvious model‑only helpers; keep namespaces stable via partial rename plan and test with CodeInspection before full rename of Skyline/Util → UtilUI

## Notes
- The SublistPaths move (Phase 1) reduced Model violations; ReportExporter work will remove the remaining DocumentGridViewContext usages in Model/CLI.
- Keep Phase 2 changes small and staged to reduce branch complexity.
