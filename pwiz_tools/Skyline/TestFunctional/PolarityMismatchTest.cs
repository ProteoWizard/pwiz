/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Test our ability to give hints to user when doc and data polarity disagree
    /// </summary>
    [TestClass]
    public class PolarityMismatchTest : AbstractFunctionalTestEx
    {
        private const string ZIP_FILE = @"TestFunctional\NegativeIonChromatogramsTest.zip";

        [TestMethod]
        public void TestPolarityMismatch()
        {
            TestFilesZip = ZIP_FILE;
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            var replicatePath = testFilesDir.GetTestPath("090215_033.mzML"); // properly converted, with polarity sense
            var allNegativePath = testFilesDir.GetTestPath("all_negative.mzML"); // Hacked to declare all chromatograms as negative
            var noPolarityPath = testFilesDir.GetTestPath("no_polarity.mzML"); // Converted by older msconvert without any ion polarity sense, so all positive
            var replicateName = Path.GetFileNameWithoutExtension(replicatePath);

            var docProperPolarity = LoadDocWithReplicate(testFilesDir, replicateName, replicatePath, null); // Mixed polarity doc and data
            var docPosPolarity = LoadDocWithReplicate(testFilesDir, replicateName, noPolarityPath, -1);  // All neg doc, all pos data
            var docNegPolarity = LoadDocWithReplicate(testFilesDir, replicateName, allNegativePath, 1);  // All pos doc, all neg data

            var transProperPolarity = docProperPolarity.MoleculeTransitions.ToArray();
            var transNoPolarity = docPosPolarity.MoleculeTransitions.ToArray();
            var transNegPolarity = docNegPolarity.MoleculeTransitions.ToArray();
            Assert.AreEqual(transProperPolarity.Length, transNoPolarity.Length);
            Assert.AreEqual(transNegPolarity.Length, transNoPolarity.Length);
            var countPeaksProperPolarity = 0;
            var countPeaksPosPolarity = 0;
            var countPeaksNegPolarity = 0;
            var properList = new List<string>();

            var i = 0;
            bool integrateAll = docPosPolarity.Settings.TransitionSettings.Integration.IsIntegrateAll;
            foreach (var nodeGroup in docProperPolarity.MoleculeTransitionGroups)
            {
                foreach (var trans in nodeGroup.Transitions)
                {
                    if ((transProperPolarity[i].GetPeakCountRatio(0, integrateAll) ?? 0) >= 1)
                    {
                        countPeaksProperPolarity++;
                        properList.Add(string.Format("{0} {1}", nodeGroup, trans.Transition));
                    }
                    if ((transNoPolarity[i].GetPeakCountRatio(0, integrateAll) ?? 0) >= 1)
                    {
                        countPeaksPosPolarity++;
                    }
                    if ((transNegPolarity[i].GetPeakCountRatio(0, integrateAll) ?? 0) >= 1)
                    {
                        countPeaksNegPolarity++;
                    }
                    i++;
                }
            }
            // There are 236 total transitions, 98 of which have decent peaks that match declared explict RT values
            Assert.AreEqual(98, countPeaksProperPolarity, "countPeaksProperPolarity: " + string.Join(", ", properList));
            Assert.AreEqual(0, countPeaksNegPolarity, "countPeaksNegPolarity"); //Should be total polarity mismatch
            Assert.AreEqual(0, countPeaksPosPolarity, "countPeaksNoPolarity"); //Should be total polarity mismatch
            testFilesDir.Dispose();
        }

        // Load a skyline doc, half of which is positve charges and half negative, so we can verify interaction with 
        // polarity in the replicate mass spec files
        private SrmDocument LoadDocWithReplicate(TestFilesDir testFilesDir, string replicateName, string replicatePath, int? charge)
        {
            var docPathBase = testFilesDir.GetTestPath("NegativeIonChromatograms.sky");
            var docPath = docPathBase.Replace(".sky", replicatePath.Split('\\').Last() + ".sky"); // Make sure name in unique
            File.Copy(docPathBase, docPath);
            var doc0 = SkylineWindow.Document;
            RunUI(() => SkylineWindow.NewDocument(true));
            WaitForDocumentChange(doc0);
            RunUI(() => SkylineWindow.OpenFile(docPath));
            var docEmpty = WaitForDocumentLoaded();
            AssertEx.IsDocumentState(docEmpty, null, 0, 0, 0, 0);
            // Class,Name,Pre charge,Pre,Prod,Prod charge,RT,window,CE
            var columnOrder = new[]
                {
                    SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                    SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                    SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                    SmallMoleculeTransitionListColumnHeaders.mzPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.mzProduct,
                    SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                    SmallMoleculeTransitionListColumnHeaders.rtPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.rtWindowPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.cePrecursor,
                };
            var clipText = File.ReadAllText(testFilesDir.GetTestPath("SRMs.csv")).Replace(',', TextUtil.CsvSeparator)
                .Replace(".", LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            string expectedError = null;
            if (charge.HasValue)
            {
                // Hack the CSV text to create a document that's completely + or -
                if (charge < 0)
                {
                    clipText = clipText.Replace("+1", "-1");
                    expectedError = Resources.ChromCacheBuilder_BuildCache_This_document_contains_only_negative_ion_mode_transitions__and_the_imported_file_contains_only_positive_ion_mode_data_so_nothing_can_be_loaded_;
                }
                else
                {
                    clipText = clipText.Replace("-1", "+1");
                    expectedError = Resources.ChromCacheBuilder_BuildCache_This_document_contains_only_positive_ion_mode_transitions__and_the_imported_file_contains_only_negative_ion_mode_data_so_nothing_can_be_loaded___Negative_ion_mode_transitions_need_to_have_negative_charge_values_;
                }
            }

            var importDialog3 = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var col4Dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog3.TransitionListText = clipText);

            RunUI(() => {
                col4Dlg.radioMolecule.PerformClick();
                col4Dlg.SetSelectedColumnTypes(
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Charge,
                    Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time,
                    Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time_Window,
                    Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Energy);
            });
            OkDialog(col4Dlg, col4Dlg.OkDialog);
            
            var document = WaitForDocumentChangeLoaded(docEmpty);
            AssertEx.IsDocumentState(document, null, 1, 236, 236, 236);
            if (expectedError == null)
            {
                ImportResultsFile(replicatePath);
            }
            else
            {
                ImportResultsAsync(replicatePath);
                AllChromatogramsGraph importProgress = null;
                while (importProgress == null)
                {
                    importProgress = FindOpenForm<AllChromatogramsGraph>();
                }
                WaitForConditionUI(10000, () => !string.IsNullOrEmpty(importProgress.Error) && importProgress.Error.Contains(expectedError),
                    () => string.Format("Timed out waiting for error message containing \"{0}\"", expectedError));
            }
            document = WaitForDocumentChangeLoaded(document);
            return document;
        }
    }
}