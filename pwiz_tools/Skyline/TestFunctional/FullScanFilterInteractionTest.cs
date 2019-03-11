/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for interaction of Transition Settings filter table with full-scan tab.
    /// </summary>
    [TestClass]
    public class FullScanFilterInteractionTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestFullScanFilterInteraction()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var docInitial = SkylineWindow.Document;

            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                // Begin with no precursors, no Full Scan
                transitionSettingsUI.SmallMoleculeFragmentTypes = "f"; 
                transitionSettingsUI.FragmentTypes = "y"; 
                transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.None;
                transitionSettingsUI.AcquisitionMethod = FullScanAcquisitionMethod.None;
                transitionSettingsUI.InstrumentMaxMz = transitionSettingsUI.InstrumentMaxMz + 1; // Force a document change in case these other values are already in place
                transitionSettingsUI.OkDialog();
            });

            docInitial = WaitForDocumentChange(docInitial);
    
            // Mess with full scan settings, verify interaction with fragment filters
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                // Enable full scan - should turn on precursors
                transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                Assert.IsTrue(transitionSettingsUI.FragmentTypes.Equals("y, p"));
                Assert.IsTrue(transitionSettingsUI.SmallMoleculeFragmentTypes.Equals("f, p"));

                transitionSettingsUI.AcquisitionMethod = FullScanAcquisitionMethod.DDA;
                Assert.IsTrue(transitionSettingsUI.SmallMoleculeFragmentTypes.Equals("f, p"));
                Assert.IsTrue(transitionSettingsUI.FragmentTypes.Equals("y, p"));

                transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.None;
                transitionSettingsUI.AcquisitionMethod = FullScanAcquisitionMethod.None;
                Assert.IsTrue(transitionSettingsUI.SmallMoleculeFragmentTypes.Equals("f")); // "p" was not set on entry, should be unset
                transitionSettingsUI.FragmentTypes = "y"; // "p" was not set on entry, should be unset

                transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                transitionSettingsUI.PrecursorMassAnalyzer = FullScanMassAnalyzerType.qit;
                Assert.IsTrue(transitionSettingsUI.FragmentTypes.Equals("y, p"));
                Assert.IsTrue(transitionSettingsUI.SmallMoleculeFragmentTypes.Equals("f, p"));

                transitionSettingsUI.OkDialog();
            });

            docInitial = WaitForDocumentChange(docInitial);
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                Assert.IsTrue(transitionSettingsUI.FragmentTypes.Equals("y, p"));
                Assert.IsTrue(transitionSettingsUI.SmallMoleculeFragmentTypes.Equals("f, p"));

                transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.None;
                transitionSettingsUI.AcquisitionMethod = FullScanAcquisitionMethod.None;
                Assert.IsTrue(transitionSettingsUI.SmallMoleculeFragmentTypes.Equals("f, p")); // "p" was set on entry, should stay so
                Assert.IsTrue(transitionSettingsUI.FragmentTypes.Equals("y, p")); // "p" was set on entry, should stay so

                transitionSettingsUI.FragmentTypes = "p";
                transitionSettingsUI.SmallMoleculeFragmentTypes = "p";

                transitionSettingsUI.OkDialog();
            });

            WaitForDocumentChange(docInitial);
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                Assert.IsTrue(transitionSettingsUI.FragmentTypes.Equals("p"));
                Assert.IsTrue(transitionSettingsUI.SmallMoleculeFragmentTypes.Equals("p"));

                transitionSettingsUI.AcquisitionMethod = FullScanAcquisitionMethod.DIA;
                Assert.IsTrue(transitionSettingsUI.SmallMoleculeFragmentTypes.Equals("f, p")); // Small mol fragments enabled
                Assert.IsTrue(transitionSettingsUI.FragmentTypes.Equals("p")); // We don't know which peptide fragments to enable

                transitionSettingsUI.AcquisitionMethod = FullScanAcquisitionMethod.None;
                Assert.IsTrue(transitionSettingsUI.SmallMoleculeFragmentTypes.Equals("p")); // Small mol fragments were not enabled at start, so should be cleared
                Assert.IsTrue(transitionSettingsUI.FragmentTypes.Equals("p")); // We don't know which peptide fragments to enable

                transitionSettingsUI.OkDialog();
            });
        }
    }
}
