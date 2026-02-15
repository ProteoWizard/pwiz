/*
 * Original author: Henry Sanford <henrytsanford .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using ZedGraph;

// Alias for the specific generic type used in this test
using GraphDataCachingReceiver = pwiz.Skyline.Controls.Graphs.ReplicateCachingReceiver<
    pwiz.Skyline.Controls.Graphs.SummaryRelativeAbundanceGraphPane.GraphDataParameters,
    pwiz.Skyline.Controls.Graphs.SummaryRelativeAbundanceGraphPane.GraphData>;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakAreaRelativeAbundanceGraphTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakAreaRelativeAbundanceGraph()
        {
            TestFilesZip = @"TestFunctional\PeakAreaRelativeAbundanceGraphTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_plasma.sky")));
            WaitForDocumentLoaded();
            VerifyFindSelectedPaths();
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                SkylineWindow.ShowPeakAreaRelativeAbundanceGraph();
            });
            var graphPane = FindGraphPane();
            WaitForConditionUI(() => graphPane.IsComplete);
            RunUI(() =>
            {
                Assert.IsNotNull(graphPane);
                var peakAreaGraph = FormUtil.OpenForms.OfType<GraphSummary>().FirstOrDefault(graph =>
                    graph.Type == GraphTypeSummary.abundance && graph.Controller is AreaGraphController);
                Assert.IsNotNull(peakAreaGraph);

                // Initial calculation should be full (no cache)
                Assert.AreEqual(0, graphPane.CachedNodeCount, "Initial graph should have no cached nodes");

                // Verify that setting the targets to Proteins or Peptides produces the correct number of points
                SkylineWindow.SetAreaProteinTargets(true);
            });
            WaitForConditionUI(() => graphPane.IsComplete);
            RunUI(() =>
            {
                Assert.AreEqual(48, graphPane.CurveList.Sum(curve => curve.NPts));
                // Mode change requires full recalculation
                Assert.AreEqual(0, graphPane.CachedNodeCount, "Switching to protein mode should trigger full calculation");
                SkylineWindow.SetAreaProteinTargets(false);
            });
            WaitForConditionUI(() => graphPane.IsComplete);
            RunUI(() =>
            {
                Assert.AreEqual(125, graphPane.CurveList.Sum(curve => curve.NPts));
                // Mode change requires full recalculation
                Assert.AreEqual(0, graphPane.CachedNodeCount, "Switching to peptide mode should trigger full calculation");

                // Verify that excluding peptide lists reduces the number of points
                SkylineWindow.SetExcludePeptideListsFromAbundanceGraph(true);
            });
            WaitForConditionUI(() => graphPane.IsComplete);
            RunUI(() =>
            {
                Assert.AreEqual(45, graphPane.CurveList.Sum(curve => curve.NPts));
                // Excluding peptide lists uses incremental update - remaining peptides are cached
                Assert.AreEqual(45, graphPane.CachedNodeCount, "Remaining peptides should be cached");
                Assert.AreEqual(0, graphPane.RecalculatedNodeCount, "No peptides should need recalculation");
            });

            TestFormattingDialog();
            TestIncrementalUpdate();
        }

        private void TestFormattingDialog()
        {
            RunUI(()=>
            {
                Settings.Default.ExcludeStandardsFromAbundanceGraph = false;
                SkylineWindow.SetExcludePeptideListsFromAbundanceGraph(true);
                SkylineWindow.SetAreaProteinTargets(false);
            });
            Assert.AreEqual(RelativeAbundanceFormatting.DEFAULT, SkylineWindow.Document.Settings.DataSettings.RelativeAbundanceFormatting);
            var pane = FindGraphPane();

            var formattingDlg = ShowDialog<VolcanoPlotFormattingDlg>(pane.ShowFormattingDialog);
            // Add a line which says all peptides containing "QE" should be indigo diamonds 
            // and all peptides containing "GQ" should be turquoise triangles
            RunUI(() =>
            {
                Assert.AreEqual(Skyline.Controls.GroupComparison.GroupComparisonResources
                        .VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Protein_Expression_Formatting,
                    formattingDlg.Text);
                var row = formattingDlg.AddRow();
                Assert.IsNotNull(row);
                row.Expression = new MatchExpression("QE", new[] { MatchOption.PeptideSequence }).ToString();
                row.PointSymbol = PointSymbol.Diamond;
                row.Color = Color.FromArgb(Color.Indigo.ToArgb());
                row = formattingDlg.AddRow();
                Assert.IsNotNull(row);
                row.Expression = new MatchExpression("GQ", new[] { MatchOption.PeptideSequence }).ToString();
                row.PointSymbol = PointSymbol.Triangle;
                row.Color = Color.FromArgb(Color.Turquoise.ToArgb());
            });
            WaitForGraphs();

            // Verify that 2 peptides are drawn as diamonds and 2 are drawn as triangles
            RunUI(() =>
            {
                var diamondCurve = pane.CurveList.OfType<LineItem>().Single(curve => curve.Symbol.Type == SymbolType.Diamond);
                Assert.AreEqual(2, diamondCurve.Points.Count);
                var triangleCurve = pane.CurveList.OfType<LineItem>().Single(curve => curve.Symbol.Type == SymbolType.Triangle);
                Assert.AreEqual(2, triangleCurve.Points.Count);
            });

            // The document should still have its original RelativeAbundanceFormatting because the formatting dialog has not been OK'd yet
            Assert.AreEqual(RelativeAbundanceFormatting.DEFAULT, SkylineWindow.Document.Settings.DataSettings.RelativeAbundanceFormatting);
            
            OkDialog(formattingDlg, formattingDlg.OkDialog);
            // The document should have the new RelativeAbundanceFormatting
            WaitForCondition(() => !Equals(SkylineWindow.Document.Settings.DataSettings.RelativeAbundanceFormatting,
                RelativeAbundanceFormatting.DEFAULT));
            var relativeAbundanceFormatting = SkylineWindow.Document.Settings.DataSettings.RelativeAbundanceFormatting;
            Assert.AreEqual(2, relativeAbundanceFormatting.ColorRows.Count());
            Assert.AreEqual(PointSymbol.Diamond, relativeAbundanceFormatting.ColorRows.First().PointSymbol);
            Assert.AreEqual(PointSymbol.Triangle, relativeAbundanceFormatting.ColorRows.ElementAt(1).PointSymbol);
            
            // Include peptide lists and verify that the number of diamonds on the graph has changed to 4
            RunUI(() =>
            {
                SkylineWindow.SetExcludePeptideListsFromAbundanceGraph(false);
            });
            WaitForGraphs();
            RunUI(() =>
            {
                var diamondCurve = pane.CurveList.OfType<LineItem>().Single(curve => curve.Symbol.Type == SymbolType.Diamond);
                Assert.AreEqual(4, diamondCurve.Points.Count);
                var triangleCurve = pane.CurveList.OfType<LineItem>().Single(curve => curve.Symbol.Type == SymbolType.Triangle);
                Assert.AreEqual(3, triangleCurve.Points.Count);
            });

            // Save and reopen the document
            RunUI(() =>
            {
                SkylineWindow.SaveDocument();
                SkylineWindow.OpenFile(SkylineWindow.DocumentFilePath);
            });
            WaitForDocumentLoaded();

            relativeAbundanceFormatting = SkylineWindow.Document.Settings.DataSettings.RelativeAbundanceFormatting;
            Assert.AreEqual(2, relativeAbundanceFormatting.ColorRows.Count());
            Assert.AreEqual(PointSymbol.Diamond, relativeAbundanceFormatting.ColorRows.First().PointSymbol);
            Assert.AreEqual(PointSymbol.Triangle, relativeAbundanceFormatting.ColorRows.ElementAt(1).PointSymbol);

            pane = FindGraphPane();
            Assert.IsNotNull(pane);

            // Bring up the formatting dialog again, make a change, change it back, and OK dialog
            formattingDlg = ShowDialog<VolcanoPlotFormattingDlg>(pane.ShowFormattingDialog);
            RunUI(() =>
            {
                Assert.AreEqual(PointSymbol.Diamond, formattingDlg.GetRowPointSymbol(0));
                formattingDlg.SetRowPointSymbol(0, PointSymbol.Plus);
            });
            WaitForGraphs();
            RunUI(() => formattingDlg.SetRowPointSymbol(0, PointSymbol.Diamond));
            WaitForGraphs();
            OkDialog(formattingDlg, formattingDlg.OkDialog);
        }

        private SummaryRelativeAbundanceGraphPane FindGraphPane()
        {
            foreach (var graphSummary in SkylineWindow.ListGraphPeakArea)
            {
                if (graphSummary.TryGetGraphPane<SummaryRelativeAbundanceGraphPane>(out var pane))
                {
                    return pane;
                }
            }
            return null;
        }

        private void VerifyFindSelectedPaths()
        {
            foreach (var path1 in EnumerateIdentityPaths(SkylineWindow.Document))
            {
                foreach (var path2 in EnumerateIdentityPaths(SkylineWindow.Document))
                {
                    bool isPathSelected = DotPlotUtil.IsPathSelected(path1, path2);
                    bool anyFindSelectedPaths = DotPlotUtil.FindSelectedPaths(new[] { path1 }, new[] { path2 }).Any();
                    if (isPathSelected != anyFindSelectedPaths)
                    {
                        Assert.AreEqual(DotPlotUtil.IsPathSelected(path1, path2), DotPlotUtil.FindSelectedPaths(new[] { path1 }, new[] { path2 }).Any(), "Mismatch for path {0} and {1}", path1, path2);
                    }
                }
            }
        }

        private IEnumerable<IdentityPath> EnumerateIdentityPaths(SrmDocument document)
        {
            foreach (var moleculeGroup in document.MoleculeGroups.Take(2))
            {
                var moleculeGroupIdentityPath = new IdentityPath(moleculeGroup.PeptideGroup);
                yield return moleculeGroupIdentityPath;
                foreach (var molecule in document.Molecules.Take(2))
                {
                    var moleculeIdentityPath = new IdentityPath(moleculeGroupIdentityPath, molecule.Peptide);
                    yield return moleculeIdentityPath;
                    foreach (var transitionGroup in molecule.TransitionGroups.Take(2))
                    {
                        var transitionGroupIdentityPath =
                            new IdentityPath(moleculeIdentityPath, transitionGroup.TransitionGroup);
                        yield return transitionGroupIdentityPath;
                        foreach (var transition in transitionGroup.Transitions.Take(2))
                        {
                            yield return new IdentityPath(transitionGroupIdentityPath, transition.Transition);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Tests that incremental updates correctly reuse cached calculations when possible.
        /// Uses CachedNodeCount and RecalculatedNodeCount to verify the incremental update path was taken.
        /// </summary>
        private void TestIncrementalUpdate()
        {
            var pane = FindGraphPane();

            // === Test 1: Protein mode - delete a peptide, verify incremental update ===
            RunUI(() => SkylineWindow.SetAreaProteinTargets(true));
            WaitForConditionUI(() => pane.IsComplete);

            int originalProteinCount = 0;
            RunUI(() =>
            {
                originalProteinCount = pane.CurveList.Sum(curve => curve.NPts);
                // Verify this was a full calculation (first time in protein mode)
                Assert.AreEqual(0, pane.CachedNodeCount,
                    "Initial protein mode calculation should have no cached nodes");
                Assert.AreEqual(originalProteinCount, pane.RecalculatedNodeCount,
                    "Initial protein mode calculation should recalculate all nodes");

                // Select a peptide in the first protein group for deletion
                var peptidePath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                SkylineWindow.SelectedPath = peptidePath;
            });

            // Delete the selected peptide
            RunUI(SkylineWindow.EditDelete);
            WaitForConditionUI(() => pane.IsComplete);

            RunUI(() =>
            {
                int afterDeleteCount = pane.CurveList.Sum(curve => curve.NPts);
                // Same number of proteins (we deleted a peptide, not a protein)
                Assert.AreEqual(originalProteinCount, afterDeleteCount,
                    "Deleting a peptide should not change protein count");

                // Verify incremental update: most proteins cached, only 1 recalculated
                Assert.AreEqual(originalProteinCount - 1, pane.CachedNodeCount,
                    "After deleting peptide, all but one protein should be cached");
                Assert.AreEqual(1, pane.RecalculatedNodeCount,
                    "After deleting peptide, only the affected protein should be recalculated");
            });

            // === Test 2: Undo - verify incremental update restores protein ===
            RunUI(SkylineWindow.Undo);
            WaitForConditionUI(() => pane.IsComplete);

            RunUI(() =>
            {
                int afterUndoCount = pane.CurveList.Sum(curve => curve.NPts);
                Assert.AreEqual(originalProteinCount, afterUndoCount,
                    "Undo should restore protein count");

                // Verify incremental update: most proteins cached, only 1 recalculated (the restored one)
                Assert.AreEqual(originalProteinCount - 1, pane.CachedNodeCount,
                    "After undo, all but one protein should be cached");
                Assert.AreEqual(1, pane.RecalculatedNodeCount,
                    "After undo, only the restored protein should be recalculated");
            });

            // === Test 3: Peptide mode - delete multiple disjoint peptides, verify incremental update ===
            RunUI(() => SkylineWindow.SetAreaProteinTargets(false));
            WaitForConditionUI(() => pane.IsComplete);

            int originalPeptideCount = 0;
            const int peptidesToDelete = 4;
            RunUI(() =>
            {
                originalPeptideCount = pane.CurveList.Sum(curve => curve.NPts);
                // Verify this was a full calculation (switching to peptide mode)
                Assert.AreEqual(0, pane.CachedNodeCount,
                    "Switching to peptide mode should trigger full calculation with no cached nodes");
                Assert.AreEqual(originalPeptideCount, pane.RecalculatedNodeCount,
                    "Switching to peptide mode should recalculate all nodes");

                // Select multiple disjoint peptides for deletion (indices 0, 10, 50, 100)
                // These are spread across the document to test non-adjacent deletion
                var peptideIndices = new[] { 0, 10, 50, 100 };
                var paths = peptideIndices
                    .Where(i => i < SkylineWindow.Document.MoleculeCount)
                    .Select(i => SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, i))
                    .ToArray();
                SkylineWindow.SequenceTree.SelectedPaths = paths;
            });

            // Delete the selected peptides
            RunUI(SkylineWindow.EditDelete);
            WaitForConditionUI(() => pane.IsComplete);

            RunUI(() =>
            {
                int afterDeleteCount = pane.CurveList.Sum(curve => curve.NPts);
                // Fewer peptides after deletion
                Assert.AreEqual(originalPeptideCount - peptidesToDelete, afterDeleteCount,
                    $"Deleting {peptidesToDelete} peptides should reduce count by {peptidesToDelete}");

                // Verify incremental update: all remaining peptides cached, none recalculated
                // (the deleted peptides are just removed, not recalculated)
                Assert.AreEqual(originalPeptideCount - peptidesToDelete, pane.CachedNodeCount,
                    "After deleting peptides, all remaining peptides should be cached");
                Assert.AreEqual(0, pane.RecalculatedNodeCount,
                    "After deleting peptides, no peptides should be recalculated (just removed)");
            });

            // === Test 4: Undo - verify the restored peptides are recalculated ===
            // Track cached results to investigate whether multiple DocumentChanged events
            // cause parallel calculations during undo (same pattern as Test 6/PR #3830)
            using (new ScopedAction(
                       () => GraphDataCachingReceiver.TrackCaching = true,
                       () => GraphDataCachingReceiver.TrackCaching = false))
            {
                RunUI(SkylineWindow.Undo);
                WaitForConditionUI(() => pane.IsComplete);
            }

            RunUI(() =>
            {
                int afterUndoCount = pane.CurveList.Sum(curve => curve.NPts);
                Assert.AreEqual(originalPeptideCount, afterUndoCount,
                    "Undo should restore peptide count");

                var cachedResults = GraphDataCachingReceiver.CachedSinceTracked.ToList();
                Assert.IsTrue(cachedResults.Count > 0,
                    "Should have cached at least one result during undo");

                // Expect a single calculation. If multiple DocumentChanged events fired,
                // there will be multiple results -- fail with diagnostic info to investigate.
                var diagnosticInfo = string.Join("\n", cachedResults.Select((r, i) =>
                    $"  [{i}]: {r.GetDiagnosticInfo()}"));
                Assert.AreEqual(1, cachedResults.Count,
                    $"Expected a single calculation during undo, but got {cachedResults.Count}. " +
                    $"Multiple DocumentChanged events may be causing parallel calculations:\n{diagnosticInfo}");

                // Single calculation -- verify incremental update
                var result = cachedResults.First();
                Assert.AreEqual(originalPeptideCount - peptidesToDelete, result.CachedNodeCount,
                    $"After undo, existing peptides should be cached. " +
                    $"Result: {result.GetDiagnosticInfo()}");
                Assert.AreEqual(peptidesToDelete, result.RecalculatedNodeCount,
                    $"After undo, only the {peptidesToDelete} restored peptides should be recalculated. " +
                    $"Result: {result.GetDiagnosticInfo()}");
            });

            // === Test 5: Change quantification settings - verify full recalculation ===
            // Changing NormalizationMethod should trigger full recalculation via HasEqualQuantificationSettings
            ChangeNormalizationMethod(NormalizationMethod.EQUALIZE_MEDIANS, pane);

            RunUI(() =>
            {
                // Quantification settings change should trigger full recalculation
                Assert.AreEqual(0, pane.CachedNodeCount,
                    "Changing quantification settings should trigger full calculation with no cached nodes");
                Assert.AreEqual(originalPeptideCount, pane.RecalculatedNodeCount,
                    "Changing quantification settings should recalculate all nodes");
            });

            // === Test 5b: Delete peptide with median normalization - verify full recalculation ===
            // With EQUALIZE_MEDIANS, any target change affects all abundances because the median
            // is recalculated. Incremental updates cannot be used.
            RunUI(() => SkylineWindow.SelectedPath =
                SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0));
            RunUI(SkylineWindow.EditDelete);
            WaitForGraphs();
            WaitForConditionUI(() => pane.IsComplete);

            RunUI(() =>
            {
                int afterMedianDeleteCount = pane.CurveList.Sum(curve => curve.NPts);
                Assert.AreEqual(originalPeptideCount - 1, afterMedianDeleteCount,
                    "Deleting a peptide should reduce count by 1");

                // With median normalization, everything must be recalculated (no caching)
                Assert.AreEqual(0, pane.CachedNodeCount,
                    "With median normalization, deleting a peptide should trigger full recalculation");
                Assert.AreEqual(originalPeptideCount - 1, pane.RecalculatedNodeCount,
                    "With median normalization, all remaining peptides should be recalculated");
            });

            // Undo the delete so we have all peptides again (still in EQUALIZE_MEDIANS mode)
            RunUI(SkylineWindow.Undo);
            WaitForConditionUI(() => pane.IsComplete);

            // === Test 5c: Change to GLOBAL_STANDARDS and add new global standard ===
            // The document has VVLSGSDATLAYSAFK already marked as a global standard.
            // First, change to GLOBAL_STANDARDS normalization.
            ChangeNormalizationMethod(NormalizationMethod.GLOBAL_STANDARDS, pane);

            RunUI(() =>
            {
                // Normalization change should trigger full recalculation
                Assert.AreEqual(0, pane.CachedNodeCount,
                    "Changing to GLOBAL_STANDARDS should trigger full calculation with no cached nodes");
                Assert.AreEqual(originalPeptideCount, pane.RecalculatedNodeCount,
                    "Changing to GLOBAL_STANDARDS should recalculate all nodes");
            });

            // Now add HLNGFSVPR as another global standard - this should invalidate the cache
            // because _cachedPeptideStandards will change
            FindNode("HLNGFSVPR");
            RunUI(() => SkylineWindow.SetStandardType(StandardType.GLOBAL_STANDARD));
            WaitForGraphs();
            WaitForConditionUI(() => pane.IsComplete);

            RunUI(() =>
            {
                // Adding a global standard changes _cachedPeptideStandards, which should
                // cause HasEqualQuantificationSettings to return false and invalidate the cache
                Assert.AreEqual(0, pane.CachedNodeCount,
                    "Adding a global standard should trigger full calculation (cachedPeptideStandards changed)");
                Assert.AreEqual(originalPeptideCount, pane.RecalculatedNodeCount,
                    "Adding a global standard should recalculate all nodes");
            });

            // Undo both changes (global standard annotation and normalization method)
            RunUI(SkylineWindow.Undo);
            RunUI(SkylineWindow.Undo);
            WaitForConditionUI(() => pane.IsComplete);

            // === Test 6: Reopen document - verify document ID change triggers full recalculation ===
            // This tests the "document identity changed" branch in CleanCacheForIncrementalUpdates
            // Capture the current document ID before reopening
            var priorDoc = SkylineWindow.Document;

            // Track cached results during document reopen to capture the first (full) calculation
            // Opening a file causes multiple DocumentChanged events (OpenFile, ChromatogramManager, LibraryManager),
            // which can result in multiple cached calculations. The first should be full, subsequent incremental.
            using (new ScopedAction(
                       () => GraphDataCachingReceiver.TrackCaching = true,
                       () => GraphDataCachingReceiver.TrackCaching = false))
            {
                // Reopen the same file - this creates a new document with a new ID
                RunUI(() => SkylineWindow.OpenFile(SkylineWindow.DocumentFilePath));
                WaitForDocumentChangeLoaded(priorDoc);

                // Get the new graph pane (old one was disposed when document closed)
                pane = FindGraphPane();
                WaitForConditionUI(() => pane.IsComplete);
            }

            RunUI(() =>
            {
                // Verify document ID actually changed
                Assert.AreNotSame(priorDoc.Id, SkylineWindow.Document.Id,
                    "Reopening document should create a new document ID");

                // Get all cached results during document reopen
                var cachedResults = GraphDataCachingReceiver.CachedSinceTracked.ToList();
                Assert.IsTrue(cachedResults.Count > 0, "Should have cached at least one result during document reopen");

                // The first calculation after document reopen should be full (no cached nodes)
                var firstResult = cachedResults.First();
                Assert.IsTrue(firstResult.WasFullCalculation,
                    $"First calculation after reopen should be full, but was incremental. " +
                    $"First result: {firstResult.GetDiagnosticInfo()}");
                Assert.AreEqual(0, firstResult.CachedNodeCount,
                    $"First calculation after reopen should have no cached nodes. " +
                    $"First result: {firstResult.GetDiagnosticInfo()}");
                Assert.AreEqual(originalPeptideCount, firstResult.RecalculatedNodeCount,
                    $"First calculation should recalculate all {originalPeptideCount} nodes. " +
                    $"First result: {firstResult.GetDiagnosticInfo()}");

                // Subsequent calculations (if any) may be either:
                // - Full (125 recalculated): parallel calculation that started before any result was cached
                // - Incremental (0 recalculated): found a cached result and reused it
                // Once we see an incremental result, all subsequent must also be incremental
                bool sawIncremental = false;
                foreach (var result in cachedResults.Skip(1))
                {
                    if (result.RecalculatedNodeCount == 0)
                        sawIncremental = true;

                    if (sawIncremental)
                    {
                        Assert.AreEqual(0, result.RecalculatedNodeCount,
                            $"After seeing an incremental update, all subsequent must be incremental. " +
                            $"Result: {result.GetDiagnosticInfo()}");
                    }
                    else
                    {
                        // Full calculation is also valid - parallel execution before cache was populated
                        Assert.IsTrue(result.RecalculatedNodeCount == 0 || result.RecalculatedNodeCount == originalPeptideCount,
                            $"Result should be either full ({originalPeptideCount} recalculated) or incremental (0). " +
                            $"Result: {result.GetDiagnosticInfo()}");
                    }
                }
            });
        }

        private static void ChangeNormalizationMethod(NormalizationMethod method, SummaryRelativeAbundanceGraphPane pane)
        {
            RunUI(() =>
            {
                SkylineWindow.ModifyDocument($"Change normalization method to {method.NormalizationMethodCaption}",
                    doc => doc.ChangeSettings(doc.Settings.ChangePeptideQuantification(q =>
                        q.ChangeNormalizationMethod(method))));
            });
            // Wait for graph update timer to fire, triggering the new calculation
            WaitForGraphs();
            WaitForConditionUI(() => pane.IsComplete);
        }
    }
}
