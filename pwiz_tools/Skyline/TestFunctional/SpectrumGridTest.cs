/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Spectra;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SpectrumGridTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSpectrumGrid()
        {
            TestFilesZip = @"TestFunctional\SpectrumGridTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SpectrumGridTest.sky")));
            ImportResultsFiles(new[]{new MsDataFilePath(TestFilesDir.GetTestPath("20fmol.mzML")), new MsDataFilePath(TestFilesDir.GetTestPath("80fmol.mzML"))});
            
            // Most of the testing will be performed on the second peptide in this document
            var peptideIdentityPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 1);
            var peptideDocNode = (PeptideDocNode)SkylineWindow.Document.FindNode(peptideIdentityPath);
            Assert.AreEqual(1, peptideDocNode.TransitionGroupCount);
            var initialChromatogramPointCounts = LoadChromatograms(SkylineWindow.Document, peptideDocNode, peptideDocNode.TransitionGroups.First())
                .Select(GetFragmentChromatogramPointCount).ToList();
            CollectionAssert.DoesNotContain(initialChromatogramPointCounts, 0);
            var spectrumGrid = ShowDialog<SpectrumGridForm>(() => SkylineWindow.ViewMenu.ShowSpectrumGridForm());
            RunUI(()=>SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int) SrmDocument.Level.MoleculeGroups, 0));
            WaitForCondition(() => spectrumGrid.IsComplete());
            RunUI(()=>
            {
                Assert.AreEqual(3, spectrumGrid.DataGridView.RowCount);
                SkylineWindow.SelectedPath = peptideIdentityPath;
            });
            WaitForConditionUI(() => spectrumGrid.IsComplete());
            RunUI(()=>
            {
                AssertEx.AreEqual(2, spectrumGrid.DataGridView.RowCount);
                spectrumGrid.SetSpectrumClassColumnCheckState(SpectrumClassColumn.PresetScanConfiguration, CheckState.Unchecked);
                spectrumGrid.SetSpectrumClassColumnCheckState(SpectrumClassColumn.Ms2Precursors, CheckState.Unchecked);
                spectrumGrid.SetSpectrumClassColumnCheckState(SpectrumClassColumn.IsolationWindowWidth, CheckState.Unchecked);
            });
            WaitForConditionUI(() => spectrumGrid.IsComplete());
            RunUI(()=>spectrumGrid.DataGridView.SelectAll());
            Assert.AreEqual(2, SkylineWindow.Document.MoleculeTransitionGroupCount);
            RunDlg<AlertDlg>(spectrumGrid.AddSpectrumFiltersForSelectedRows, alertDlg =>
            {
                string expectedMessage =
                    string.Format(
                        Skyline.Properties.Resources
                            .SpectraGridForm_AddSpectrumFilters__0__spectrum_filters_will_be_added_to_the_document_, 2);

                AssertEx.AreEqual(expectedMessage, alertDlg.Message);
                alertDlg.OkDialog();
            });
            Assert.AreEqual(4, SkylineWindow.Document.MoleculeTransitionGroupCount);
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.SelectedChromatograms = SkylineWindow.Document.MeasuredResults.Chromatograms;
                manageResultsDlg.ReimportResults();
                manageResultsDlg.OkDialog();
            });
            WaitForDocumentLoaded();
            peptideDocNode = (PeptideDocNode)SkylineWindow.Document.FindNode(peptideIdentityPath);
            Assert.AreEqual(3, peptideDocNode.TransitionGroupCount);
            Assert.IsTrue(peptideDocNode.TransitionGroups.First().SpectrumClassFilter.IsEmpty);
            var fullChromatogramPointCounts = LoadChromatograms(SkylineWindow.Document, peptideDocNode, peptideDocNode.TransitionGroups.First())
                .Select(GetFragmentChromatogramPointCount).ToList();
            CollectionAssert.AreEqual(fullChromatogramPointCounts, initialChromatogramPointCounts);
            
            int[] totalChromatogramPointCounts = new int[fullChromatogramPointCounts.Count];
            foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups.Skip(1))
            {
                Assert.IsFalse(transitionGroupDocNode.SpectrumClassFilter.IsEmpty);
                var chromatograms = LoadChromatograms(SkylineWindow.Document,
                    peptideDocNode, transitionGroupDocNode).ToList();
                Assert.AreEqual(totalChromatogramPointCounts.Length, chromatograms.Count);
                for (int i = 0; i < totalChromatogramPointCounts.Length; i++)
                {
                    int pointCount = GetFragmentChromatogramPointCount(chromatograms[i]);
                    Assert.AreNotEqual(0, pointCount);
                    totalChromatogramPointCounts[i] += pointCount;
                }
            }
            CollectionAssert.AreEqual(fullChromatogramPointCounts, totalChromatogramPointCounts);
            
            OkDialog(spectrumGrid, spectrumGrid.Close);

            // Make sure that the document can be reopened
            RunUI(()=>
            {
                SkylineWindow.SaveDocument();
                SkylineWindow.OpenFile(SkylineWindow.DocumentFilePath);
            });
            WaitForDocumentLoaded();
        }

        private ChromatogramGroupInfo[] LoadChromatograms(SrmDocument document, PeptideDocNode peptideDocNode, TransitionGroupDocNode transitionGroupDocNode)
        {
            var measuredResults = document.Settings.MeasuredResults;
            Assert.IsNotNull(measuredResults);
            var result = new ChromatogramGroupInfo[measuredResults.Chromatograms.Count];
            float tolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            for (int iReplicate = 0; iReplicate < measuredResults.Chromatograms.Count; iReplicate++)
            {
                string message =
                    string.Format("Peptide: {0} Transition Group: {1} SpectrumClassFilter: {2} Replicate: {3}",
                        peptideDocNode, transitionGroupDocNode, transitionGroupDocNode.SpectrumClassFilter, iReplicate);
                Assert.IsTrue(measuredResults.TryLoadChromatogram(iReplicate, peptideDocNode, transitionGroupDocNode, tolerance,
                    out var infoSet), message);
                Assert.AreEqual(1, infoSet.Length, message);
                result[iReplicate] = infoSet[0];
            }

            return result;
        }

        private int GetFragmentChromatogramPointCount(ChromatogramGroupInfo chromatogramGroupInfo)
        {
            var chromatogramInfos = chromatogramGroupInfo.TransitionPointSets
                .Where(chromatogramInfo => chromatogramInfo.Source == ChromSource.fragment).ToList();
            Assert.AreNotEqual(0, chromatogramInfos.Count);
            var pointCount = chromatogramInfos[0].RawTimes.Count;
            for (int i = 1; i < chromatogramInfos.Count; i++)
            {
                AssertEx.AreEqual(pointCount, chromatogramInfos[i].RawTimes.Count);
            }

            return pointCount;
        }
    }
}
