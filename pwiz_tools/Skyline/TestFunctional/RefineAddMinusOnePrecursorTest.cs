/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests the "Add M-1 precursor transition" checkbox on the Refine dialog, including the
    /// validation that requires high-resolution MS1 full-scan filtering.
    /// </summary>
    [TestClass]
    public class RefineAddMinusOnePrecursorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRefineAddMinusOnePrecursor()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.Paste("ELVISLIVESK"));

            // With the default settings, MS1 full-scan filtering is not enabled, so checking the
            // box reports that error and leaves the box unchecked.
            RunLongDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
            {
                RunDlg<MessageDlg>(() => refineDlg.AddMinusOnePrecursor = true, messageDlg =>
                {
                    Assert.AreEqual(
                        EditUIResources.RefineDlg_cbAddMinusOnePrecursor_CheckedChanged_MS1_full_scan_filtering_must_be_enabled_to_add_M_1_precursor_transitions_,
                        messageDlg.Message);
                    messageDlg.OkDialog();
                });
                RunUI(() => Assert.IsFalse(refineDlg.AddMinusOnePrecursor));
            }, refineDlg => refineDlg.CancelDialog());

            // Enable MS1 full-scan filtering with a low-resolution (ion trap) precursor mass analyzer.
            SetFullScanPrecursor(FullScanMassAnalyzerType.qit);
            RunLongDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
            {
                RunDlg<MessageDlg>(() => refineDlg.AddMinusOnePrecursor = true, messageDlg =>
                {
                    Assert.AreEqual(
                        EditUIResources.RefineDlg_cbAddMinusOnePrecursor_CheckedChanged_M_1_precursor_transitions_can_only_be_added_with_a_high_resolution_precursor_mass_analyzer_,
                        messageDlg.Message);
                    messageDlg.OkDialog();
                });
                RunUI(() => Assert.IsFalse(refineDlg.AddMinusOnePrecursor));
            }, refineDlg => refineDlg.CancelDialog());

            // Switch to a high-resolution precursor mass analyzer: now the box can be checked and
            // the refinement adds an M-1 precursor transition to each precursor.
            SetFullScanPrecursor(FullScanMassAnalyzerType.tof);
            foreach (var nodeGroup in SkylineWindow.Document.PeptideTransitionGroups)
                Assert.IsTrue(nodeGroup.AutoManageChildren);
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
            {
                refineDlg.AddMinusOnePrecursor = true;
                Assert.IsTrue(refineDlg.AddMinusOnePrecursor);
                refineDlg.OkDialog();
            });
            foreach (var nodeGroup in SkylineWindow.Document.PeptideTransitionGroups)
            {
                Assert.IsFalse(nodeGroup.AutoManageChildren);
                Assert.IsTrue(nodeGroup.Transitions.Any(nodeTran =>
                    nodeTran.Transition.IonType == IonType.precursor && nodeTran.Transition.MassIndex == -1));
            }
        }

        private void SetFullScanPrecursor(FullScanMassAnalyzerType analyzer)
        {
            RunUI(() => SkylineWindow.ModifyDocument("Enable MS1 filtering", doc =>
            {
                var transitionSettings = doc.Settings.TransitionSettings;
                // A QIT (low resolution) mass analyzer only supports a single isotope peak
                bool highRes = TransitionFullScan.IsHighResAnalyzer(analyzer);
                // Clear first so the precursor mass analyzer is reset before reapplying
                var fullScan = transitionSettings.FullScan
                    .ChangePrecursorIsotopes(FullScanPrecursorIsotopes.None, null, null)
                    .ChangePrecursorIsotopes(FullScanPrecursorIsotopes.Count, highRes ? 3 : 1, IsotopeEnrichmentsList.GetDefault());
                if (!highRes)
                    fullScan = fullScan.ChangePrecursorResolution(analyzer, TransitionFullScan.DEFAULT_RES_QIT, null);
                var filter = transitionSettings.Filter.ChangePeptideIonTypes(new[] { IonType.precursor });
                return doc.ChangeSettings(doc.Settings.ChangeTransitionSettings(
                    transitionSettings.ChangeFullScan(fullScan).ChangeFilter(filter)));
            }, AuditLogEntry.SettingsLogFunction));
        }
    }
}
