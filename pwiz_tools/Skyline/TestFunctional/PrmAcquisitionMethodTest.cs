/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Common.Chemistry;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that the PRM acquisition method uses the MzMatchTolerance to decide which precursors should use
    /// a particular MS2 spectrum
    /// </summary>
    [TestClass]
    public class PrmAcquisitionMethodTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestFunctionalPrmAcquisitionMethod()
        {
            TestFilesZip = @"TestFunctional\PrmAcquisitionMethodTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky")));
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.AcquisitionMethod = FullScanAcquisitionMethod.PRM;
                transitionSettingsUi.MZMatchTolerance = new MzTolerance(0.055);
                transitionSettingsUi.OkDialog();
            });
            string dataPath = TestFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2.mzML");
            ImportResultsFile(dataPath);
            Assert.IsFalse(HasChromatogram("DTDYDDFEMR"));
            Assert.IsFalse(HasChromatogram("ENFYELTDFK"));
            Assert.IsTrue(HasChromatogram("EVVFDEVHFGK"));
            Assert.IsTrue(HasChromatogram("GAVGALIVYDISK"));
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.AcquisitionMethod = FullScanAcquisitionMethod.PRM;
                transitionSettingsUi.MZMatchTolerance = new MzTolerance(0.1);
                transitionSettingsUi.OkDialog();
            });
            var doc = SkylineWindow.Document;
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.SelectedChromatograms = doc.MeasuredResults.Chromatograms;
                manageResultsDlg.ReimportResults();
                manageResultsDlg.OkDialog();
            });
            WaitForDocumentChangeLoaded(doc);
            Assert.IsFalse(HasChromatogram("DTDYDDFEMR"));
            Assert.IsTrue(HasChromatogram("ENFYELTDFK"));
            Assert.IsTrue(HasChromatogram("EVVFDEVHFGK"));
            Assert.IsTrue(HasChromatogram("GAVGALIVYDISK"));
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.AcquisitionMethod = FullScanAcquisitionMethod.PRM;
                transitionSettingsUi.MZMatchTolerance = new MzTolerance(0.4);
                transitionSettingsUi.OkDialog();
            });
            doc = SkylineWindow.Document;
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.SelectedChromatograms = doc.MeasuredResults.Chromatograms;
                manageResultsDlg.ReimportResults();
                manageResultsDlg.OkDialog();
            });
            WaitForDocumentChangeLoaded(doc);
            Assert.IsTrue(HasChromatogram("DTDYDDFEMR"));
            Assert.IsTrue(HasChromatogram("ENFYELTDFK"));
            Assert.IsTrue(HasChromatogram("EVVFDEVHFGK"));
            Assert.IsTrue(HasChromatogram("GAVGALIVYDISK"));
        }

        private bool HasChromatogram(string peptideSequence)
        {
            var doc = SkylineWindow.Document;
            var tolerance =  doc.Settings.TransitionSettings.Instrument.IonMatchMzTolerance;
            var chromatogramSet = doc.Settings.MeasuredResults.Chromatograms[0];
            var peptideDocNode = doc.Peptides.FirstOrDefault(pep => pep.Target.Sequence == peptideSequence);
            Assert.IsNotNull(peptideDocNode);
            var transitionGroupDocNode = peptideDocNode.TransitionGroups.First();
            Assert.IsNotNull(peptideDocNode);

            return doc.Settings.MeasuredResults.TryLoadChromatogram(chromatogramSet, peptideDocNode,
                transitionGroupDocNode, tolerance, out _);
        }
    }
}
