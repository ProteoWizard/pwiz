/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that "ChromCacheBuilder.GetMatchingGroups" pays attention to the MzMatchTolerance
    /// and "PeptideChromDataSets.AddDataSet" does not double count matches between transitions
    /// and chromatograms unless optimization is being performed.
    ///
    /// In this scenario, there is a molecule with the
    /// precursor 391.282 and product ions 391.282, 345.376, 343.394 and 327.282.
    /// Usually the best match is the set of chromatograms
    /// 391.282 &amp; (391.282, 345.278, 343.294, 327.28)
    /// 
    /// However, if we widen the MzMatchTolerance to .6 and say that we are doing
    /// CE optimization then it will match to:
    /// 391.3 &amp; (391.3x2, 345.4, 343.4, 327.4)
    /// because the 391.3 will be double counted.
    ///
    /// We figure out which chromatogram is being used by the transition by looking at
    /// "InterpolatedPointCount" in the Document Grid.
    /// </summary>
    [TestClass]
    public class ChromDataSetMatchingTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestChromDataSetMatching()
        {
            TestFilesZip = @"TestFunctional\ChromDataSetMatchingTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ChromDataSetMatchingTest.sky")));

            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.ChooseView("ChromatogramData"));

            SetMzMatchTolerance(0.1);
            ImportResultFile(false);
            Assert.AreEqual(236, GetPointCount(documentGrid));

            RemoveAllResults();
            ImportResultFile(true);
            Assert.AreEqual(236, GetPointCount(documentGrid));

            RemoveAllResults();
            SetMzMatchTolerance(0.6);
            ImportResultFile(false);
            Assert.AreEqual(236, GetPointCount(documentGrid));

            RemoveAllResults();
            ImportResultFile(true);
            Assert.AreEqual(530, GetPointCount(documentGrid));
        }

        private void SetMzMatchTolerance(double value)
        {
            var transitionSettingsDlg = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() => { transitionSettingsDlg.MZMatchTolerance = value; });
            OkDialog(transitionSettingsDlg, transitionSettingsDlg.OkDialog);
        }

        private void ImportResultFile(bool optimizing)
        {
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            if (optimizing)
            {
                RunUI(()=>importResultsDlg.OptimizationName = ExportOptimize.CE);
            }
            var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
            RunUI(() => openDataSourceDialog.SelectFile(TestFilesDir.GetTestPath("ChromDataSetMatchingTest.mzML")));
            OkDialog(openDataSourceDialog, openDataSourceDialog.Open);
            WaitForDocumentLoaded();
        }

        private void RemoveAllResults()
        {
            var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunUI(manageResultsDlg.RemoveAllReplicates);
            OkDialog(manageResultsDlg, manageResultsDlg.OkDialog);
            RunUI(()=>SkylineWindow.SaveDocument());
        }

        /// <summary>
        /// Returns the number from the "InterpolatedPointCount" column in the last row.
        /// This is the only number that the test cares about, and is used to determine whether
        /// the expected chromatogram was matched to the transition.
        /// </summary>
        private int GetPointCount(DocumentGridForm documentGridForm)
        {
            WaitForConditionUI(() => documentGridForm.IsComplete);
            int pointCount = 0;
            RunUI(()=>
            {
                var dataGridView = documentGridForm.DataGridView;
                pointCount = Convert.ToInt32(dataGridView.Rows[dataGridView.RowCount - 1]
                    .Cells[dataGridView.ColumnCount - 1].Value);
            });
            return pointCount;
        }
    }
}
