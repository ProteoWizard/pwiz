/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Menus;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    /// <summary>
    /// Tutorial test for the Skyline part of "Spectrum-centric analysis of DIA datasets" (Skyline Webinar 26,
    /// "DIA with FragPipe, DIA-NN and Skyline"). The webinar's earlier sections drive FragPipe and DIA-NN, which
    /// are outside Skyline; this covers the part that is not -- opening the Skyline document FragPipe produced and
    /// inspecting the chromatograms it extracted.
    ///
    /// Every action goes through the in-process <see cref="SkylineTool.IJsonToolService"/> (see
    /// <see cref="McpConnectorTest"/>), so the test doubles as proof the connector can do it.
    /// </summary>
    [TestClass]
    public class DiaFragPipeTutorialTest : McpConnectorTest
    {
        // The peptide the tutorial inspects first, and the protein it belongs to.
        private const string PEPTIDE_HPT = "HYEGSTVPEK";
        // The peptide the tutorial ends on, asking what is odd about it in some of the samples.
        private const string PEPTIDE_ODD = "AASGTQNNVLR";

        // While this tutorial is a draft, its screenshots live under Documentation\Tutorial-Drafts.
        protected override string TutorialDocumentationFolder => "Tutorial-Drafts";

        // The workflow is still being built out, so there is no recorded audit log to compare against yet.
        public override bool AuditLogCompareLogs => false;

        [TestMethod]
        public void TestDiaFragPipeTutorial()
        {
            // Set true to look at / regenerate tutorial screenshots.
            // IsPauseForScreenShots = true;
            CoverShotName = "DiaFragPipe";

            TestFilesZip = @"https://skyline.ms/webinars/Webinar26_data/Webinar26.zip";

            // Most of the ~4 GB download is the Astral DIA runs (.mzML). The document FragPipe produced ships with
            // its .skyd, so the runs are never re-read -- but they are what its replicates point at, so they must be
            // on disk. Persist them: extracted once and reused in place, rather than re-extracted every run.
            TestFilesPersistent = new[] { @".mzML" };

            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            return TestFilesDir.GetTestPath(Path.Combine("Webinar26", relativePath));
        }

        protected override void DoTest()
        {
            StartToolService();

            OpenFragPipeDocument();                     // s-01
            ShowPeakAreasForPeptide(PEPTIDE_HPT);       // s-02
            ShowPeakAreasForPeptide(PEPTIDE_ODD);       // s-03
        }

        /// <summary>
        /// Opens the Skyline document FragPipe wrote, which holds every protein and peptide the spectrum-centric
        /// search identified, along with the chromatograms it extracted (it ships with its .skyd, so nothing is
        /// re-read from the runs). The tutorial calls the folder "output"; in the download it is "backup".
        /// </summary>
        private void OpenFragPipeDocument()
        {
            // File > Open opens the native Open dialog, so the menu click does not complete -- take the dialog
            // straight from the action's ActionResult.FormId.
            var openDlg = ResolveNativeFileDialog(McpConnector.ClickMainMenuItem(
                MenuPath<SkylineWindow>("fileToolStripMenuItem", "openMenuItem")));
            McpConnector.SetFormValue(openDlg, "FileName",
                GetTestPath(Path.Combine("backup", "skyline_files", "fragpipe.sky")));
            McpConnector.DismissWithAcceptButton(openDlg);

            // A big document (75 MB of XML, 10 replicates) loads behind a progress dialog; wait it out.
            WaitForDocumentLoaded(10 * 60 * 1000);
            PauseForMcpScreenShot(GetOpenFormId<SkylineWindow>(), "Targets from the FragPipe search"); // s-01
        }

        /// <summary>
        /// Finds a peptide by sequence and shows its peak areas across the ten samples -- the tutorial's way of
        /// asking "in which samples was this identified, and where is the signal higher?".
        /// </summary>
        private void ShowPeakAreasForPeptide(string peptideSequence)
        {
            FindPeptide(peptideSequence);

            // Peak Areas > Replicate Comparison is a docked graph, not a dialog, so the menu click completes.
            AssertComplete(McpConnector.ClickMainMenuItem(MenuPath<ViewMenu>(
                "viewToolStripMenuItem", "peakAreasMenuItem", "areaReplicateComparisonMenuItem")));
            var peakAreas = GetMcpConnectorGraph(GraphsResources.SkylineWindow_CreateGraphPeakArea_Peak_Areas);

            // The tutorial has the reader confirm, through the graph's right-click menu, that the areas are raw --
            // Normalized To is "Default (None)". The graph has no menu bar and no toolbar, so naming NO control
            // reaches its RIGHT-CLICK menu, which is the only menu it has. The item's caption is composed at run
            // time from the selected peptide's normalization method ("Default ({0})"), so it is built the same way
            // here rather than hard-coded -- which also keeps it localized.
            var defaultNone = string.Format(
                QuantificationStrings.SkylineWindow_MakeNormalizeToMenuItem_Default___0__,
                NormalizationMethod.NONE.NormalizeToCaption);
            McpConnector.ClickControlMenuItem(peakAreas, string.Empty, string.Join(@" > ",
                GetLocalizedText<PeakAreasContextMenu>("areaNormalizeContextMenuItem"), defaultNone));

            PauseForMcpScreenShot(peakAreas, "Peak areas across the samples: " + peptideSequence);
        }

        /// <summary>
        /// Edit &gt; Find... the peptide by its sequence, then close the Find window. FindNodeDlg is MODELESS (it is
        /// Show()n, not ShowDialog()n), so the menu click COMPLETES and does not name it -- wait for it rather than
        /// resolving it from the action's ActionResult.
        /// </summary>
        private void FindPeptide(string peptideSequence)
        {
            AssertComplete(McpConnector.ClickMainMenuItem(
                MenuPath<EditMenu>("editToolStripMenuItem", "findPeptideMenuItem")));
            var findDlg = WaitForMcpConnectorForm<FindNodeDlg>();

            AssertComplete(McpConnector.SetFormValue(findDlg,
                GetLocalizedText<FindNodeDlg>("label1"), peptideSequence));   // "Find what:"
            AssertComplete(McpConnector.ClickFormButton(findDlg,
                GetLocalizedText<FindNodeDlg>("btnFindNext")));
            WaitForConditionUI(() => SkylineWindow.SelectedNode?.Text == peptideSequence);

            McpConnector.DismissWithCancelButton(findDlg);   // its cancel button is captioned "Close"
        }
    }
}
