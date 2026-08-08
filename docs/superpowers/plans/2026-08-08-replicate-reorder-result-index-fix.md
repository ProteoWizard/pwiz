# Replicate Reorder Result-Index Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Skyline recognize replicate identity reordering as a results change so peak areas and other result-indexed values remain attached to the correct replicate.

**Architecture:** Keep `ElementReorderer` on Skyline's existing document-model path. Strengthen `SrmSettingsDiff.EqualExceptAnnotations` to compare replicate identity at each ordinal position, which activates the existing `MeasuredResults.IdToIndexDictionary` remapping in transition result recalculation.

**Tech Stack:** C#/.NET Framework, MSTest, Skyline document model, TeamCity CI

## Global Constraints

- Preserve the `--reorder-replicates-file=<path>` input format and validation behavior.
- Do not manipulate serialized `.sky` XML or transition result arrays directly.
- A partial order places listed replicates first and preserves the relative order of unlisted replicates.
- Raw-file paths and transition peak areas must remain associated with their original replicate after save/reopen.

---

### Task 1: Prove identity-order changes require result remapping

**Files:**
- Modify: `pwiz_tools/Skyline/Test/SrmSettingsTest.cs`
- Modify: `pwiz_tools/Skyline/Model/DocSettings/SrmSettings.cs`

**Interfaces:**
- Consumes: `SrmSettingsDiff(SrmSettings settingsOld, SrmSettings settingsNew)` and `SrmSettingsDiff.DiffResults`
- Produces: position-sensitive replicate identity comparison inside `EqualExceptAnnotations`

- [ ] **Step 1: Add a focused failing unit test**

Add a test that constructs two distinct `ChromatogramSet` objects with equivalent file content, creates old and reversed `MeasuredResults`, and asserts that the reversed settings produce `DiffResults == true`. In the same test, rename a replicate without changing its identity and assert that the intentionally ignored metadata-only change leaves `DiffResults == false`.

```csharp
[TestMethod]
public void ReplicateOrderSettingsDiffTest()
{
    var replicateOne = new ChromatogramSet("One", new[] { "same.raw" });
    var replicateTwo = new ChromatogramSet("Two", new[] { "same.raw" });
    var settings = SrmSettingsList.GetDefault();
    var oldResults = new MeasuredResults(new[] { replicateOne, replicateTwo });
    var oldSettings = settings.ChangeMeasuredResults(oldResults);

    var reorderedResults = oldResults.ChangeChromatograms(new[] { replicateTwo, replicateOne });
    Assert.IsTrue(new SrmSettingsDiff(oldSettings,
        settings.ChangeMeasuredResults(reorderedResults)).DiffResults);

    var renamedResults = oldResults.ChangeChromatograms(new[]
    {
        replicateOne.ChangeName("Renamed"), replicateTwo
    });
    Assert.IsFalse(new SrmSettingsDiff(oldSettings,
        settings.ChangeMeasuredResults(renamedResults)).DiffResults);
}
```

- [ ] **Step 2: Verify the new test fails for the intended reason**

Run the focused Skyline test through the available Skyline test runner or, if the local Visual Studio/MSBuild toolchain remains unavailable, push a temporary test-only commit and use TeamCity. Expected result before the production fix: the reordered assertion fails because `DiffResults` is false.

- [ ] **Step 3: Implement the minimal identity comparison**

In the positional loop in `EqualExceptAnnotations`, reject equality when the `ChromatogramSet.Id` references differ.

```csharp
var chromatogramSetNewSource = measuredResultsNew.Chromatograms[i];
var chromatogramSetOldSource = measuredResultsOld.Chromatograms[i];
if (!ReferenceEquals(chromatogramSetNewSource.Id, chromatogramSetOldSource.Id))
{
    return false;
}
```

Then derive the annotation-stripped comparison values from those source variables.

- [ ] **Step 4: Verify the focused test passes**

Run the same test path. Expected: both the identity-order assertion and metadata-only assertion pass.

- [ ] **Step 5: Commit the result-index fix**

```powershell
git add pwiz_tools/Skyline/Model/DocSettings/SrmSettings.cs pwiz_tools/Skyline/Test/SrmSettingsTest.cs
git commit -m "Preserve replicate result indexing when reordered"
```

### Task 2: Validate the end-to-end command and inspection quality

**Files:**
- Modify only files identified by TeamCity's three inspection warnings, if the warnings originate in this PR.
- Test: `pwiz_tools/Skyline/TestData/CommandLineTest.cs`

**Interfaces:**
- Consumes: `ConsoleReorderReplicatesTest` and TeamCity build status APIs
- Produces: a clean PR branch whose command-line regression test preserves replicate values

- [ ] **Step 1: Run static checks locally**

```powershell
git -c core.whitespace=cr-at-eol diff --check origin/master...HEAD
git status --short
```

Expected: no whitespace errors and only intentional changes.

- [ ] **Step 2: Push the branch and trigger TeamCity**

```powershell
git push fork skylinecmd-reorder-replicates
```

- [ ] **Step 3: Verify the end-to-end regression test**

Poll PR #4549 until `Skyline master and PRs (Windows x86_64)` finishes. Expected: `ConsoleReorderReplicatesTest-en` passes, including its save/reopen peak-area assertions.

- [ ] **Step 4: Inspect and resolve ReSharper warnings**

Read the TeamCity inspection report for the new commit. Apply only warning-specific edits in PR-touched code, then push and confirm `Skyline code inspection` passes with zero new warnings.

- [ ] **Step 5: Confirm all dependent checks**

Verify the Docker/Wine snapshot-dependent check recovers after the Windows build succeeds and that no new failed tests appear.

### Task 3: Prepare the draft PR for maintainer review

**Files:**
- Remove from the upstream patch before final review: `docs/superpowers/specs/2026-08-08-replicate-reorder-result-index-design.md`
- Remove from the upstream patch before final review: `docs/superpowers/plans/2026-08-08-replicate-reorder-result-index-fix.md`

**Interfaces:**
- Consumes: passing TeamCity results and the corrected branch
- Produces: PR #4549 containing only Skyline source, resources, and tests

- [ ] **Step 1: Remove internal workflow documents from the PR patch**

Use `git rm` for the two `docs/superpowers` files after validation. These documents guide implementation but are not part of the ProteoWizard product change.

- [ ] **Step 2: Commit and push PR cleanup**

```powershell
git commit -m "Remove internal implementation notes"
git push fork skylinecmd-reorder-replicates
```

- [ ] **Step 3: Update the PR description**

Add a concise note that the implementation now explicitly detects changed replicate identity order and uses Skyline's existing ID-based remapping path.

- [ ] **Step 4: Final verification**

Confirm PR #4549 is open, draft status is unchanged, the expected source/resources/tests are the only changed files, and all required checks are successful.
