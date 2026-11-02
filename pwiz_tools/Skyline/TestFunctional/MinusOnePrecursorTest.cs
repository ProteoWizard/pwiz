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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests the "and M-1" checkbox on the Full-Scan tab of the Transition Settings dialog, which
    /// extends MS1 filtering one isotope peak below the monoisotopic peak.
    /// </summary>
    [TestClass]
    public class MinusOnePrecursorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMinusOnePrecursor()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.Paste("ELVISLIVESK"));

            // The checkbox is disabled until MS1 filtering is enabled with a high resolution
            // precursor mass analyzer
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                var cbIncludeMinusOnePrecursor = transitionSettingsUI.CbIncludeMinusOnePrecursor;
                transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.None;
                Assert.IsFalse(cbIncludeMinusOnePrecursor.Enabled);
                Assert.IsFalse(cbIncludeMinusOnePrecursor.Checked);

                transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                transitionSettingsUI.PrecursorMassAnalyzer = FullScanMassAnalyzerType.qit;
                Assert.IsFalse(cbIncludeMinusOnePrecursor.Enabled);
                Assert.IsFalse(cbIncludeMinusOnePrecursor.Checked);

                transitionSettingsUI.PrecursorMassAnalyzer = FullScanMassAnalyzerType.tof;
                transitionSettingsUI.Peaks = 3;
                Assert.IsTrue(cbIncludeMinusOnePrecursor.Enabled);
                cbIncludeMinusOnePrecursor.Checked = true;
                transitionSettingsUI.OkDialog();
            });
            WaitForDocumentLoaded();
            Assert.IsTrue(SkylineWindow.Document.Settings.TransitionSettings.FullScan.IncludeMinusOnePrecursor);
            AssertMinusOnePrecursorCount(1);

            // The checkbox comes up checked the next time the dialog is opened, and unchecking it
            // removes the M-1 precursor transitions again
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                var cbIncludeMinusOnePrecursor = transitionSettingsUI.CbIncludeMinusOnePrecursor;
                Assert.IsTrue(cbIncludeMinusOnePrecursor.Enabled);
                Assert.IsTrue(cbIncludeMinusOnePrecursor.Checked);
                cbIncludeMinusOnePrecursor.Checked = false;
                transitionSettingsUI.OkDialog();
            });
            WaitForDocumentLoaded();
            Assert.IsFalse(SkylineWindow.Document.Settings.TransitionSettings.FullScan.IncludeMinusOnePrecursor);
            AssertMinusOnePrecursorCount(0);
        }

        /// <summary>
        /// Asserts that each precursor in the document has the expected number of M-1 precursor
        /// transitions.
        /// </summary>
        private void AssertMinusOnePrecursorCount(int expectedCount)
        {
            var document = SkylineWindow.Document;
            Assert.AreNotEqual(0, document.PeptideTransitionGroupCount);
            foreach (var nodeGroup in document.PeptideTransitionGroups)
            {
                Assert.AreEqual(expectedCount, nodeGroup.Transitions.Count(nodeTran =>
                    nodeTran.Transition.IonType == IonType.precursor && nodeTran.Transition.MassIndex == -1));
            }
        }
    }
}
