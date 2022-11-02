/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ImportTransitionListTest : AbstractFunctionalTest
    {
        private const string TRIMETHYL = "Trimethyl (K)";
        private const string ACETYL = "Acetyl";

        [TestMethod]
        public void TestImportHighPrecTransitionList()
        {
            TestFilesZip = @"TestFunctional\ImportTransitionListTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ImportHighPrecTranList.sky")));
            ImportTransitionListSkipColumnSelect(TestFilesDir.GetTestPath("ThermoTransitionList.csv"));
            WaitForCondition(() => 0 != SkylineWindow.Document.MoleculeCount);
            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            RunUI(() => documentGrid.ChooseView("PeptideModSeqFullNames"));
            VerifyExpectedPeptides(documentGrid);
            AssertEx.VerifyModifiedSequences(SkylineWindow.Document);
            Assert.AreNotEqual(0, SkylineWindow.Document.PeptideCount);
            RunUI(()=>
            {
                SkylineWindow.SelectAll();
                SkylineWindow.EditDelete();
            });
            Assert.AreEqual(0, SkylineWindow.Document.PeptideCount);
            // TODO(nicksh): Try importing SciEx transition list.
        }

        /// <summary>
        /// Verifies that some of the peptides have the modifications that we expect
        /// on them. Makes sure that "Trimethyl" and "Acetyl" modifications, which require
        /// multiple decimal places, have been properly distinguished.
        /// </summary>
        private void VerifyExpectedPeptides(DocumentGridForm documentGrid)
        {
            Filter(documentGrid.DataboundGridControl, PropertyPath.Root, "KSAPATGGVKKPHR");
            var propPathModSeqFullNames = PropertyPath.Root.Property("ModifiedSequence").Property("FullNames");
            var modSequences = GetColumnValues(documentGrid.DataboundGridControl, propPathModSeqFullNames);
            AssertContainsModification(modSequences, TRIMETHYL, true);
            AssertContainsModification(modSequences, ACETYL, false);

            Filter(documentGrid.DataboundGridControl, PropertyPath.Root, "KSTGGKAPR");
            modSequences = GetColumnValues(documentGrid.DataboundGridControl, propPathModSeqFullNames);
            AssertContainsModification(modSequences, TRIMETHYL, false);
            AssertContainsModification(modSequences, ACETYL, true);

            Filter(documentGrid.DataboundGridControl, PropertyPath.Root, "TKQTAR");
            modSequences = GetColumnValues(documentGrid.DataboundGridControl, propPathModSeqFullNames);
            AssertContainsModification(modSequences, TRIMETHYL, true);
            AssertContainsModification(modSequences, ACETYL, true);
           
        }

        private void Filter(DataboundGridControl databoundGridControl, PropertyPath propertyPath, string filterValue)
        {
            WaitForConditionUI(() => databoundGridControl.IsComplete);
            var filterDlg = ShowDialog<QuickFilterForm>(() =>
            {
                databoundGridControl.QuickFilter(databoundGridControl.FindColumn(propertyPath));
            });
            RunUI(() =>
            {
                filterDlg.SetFilterOperation(0, FilterOperations.OP_EQUALS);
                filterDlg.SetFilterOperand(0, filterValue);
            });
            OkDialog(filterDlg, filterDlg.OkDialog);
            WaitForConditionUI(() => databoundGridControl.IsComplete);
        }

        private List<string> GetColumnValues(DataboundGridControl databoundGridControl, PropertyPath propertyPath)
        {
            List<string> list = new List<string>();
            RunUI(() =>
            {
                var column = databoundGridControl.FindColumn(propertyPath);
                for (int i = 0; i < databoundGridControl.RowCount; i++)
                {
                    var value = databoundGridControl.DataGridView.Rows[i].Cells[column.Index].FormattedValue;
                    if (value == null)
                    {
                        list.Add(null);
                    }
                    else
                    {
                        list.Add(value as string ?? value.ToString());
                    }
                }
            });
            return list;
        }

        private void AssertContainsModification(List<string> modSequences, string mod, bool expectedContains)
        {
            Assert.AreEqual(expectedContains, modSequences.Any(seq => seq.Contains(mod)), "Unexpected result looking for {0} in {1}", mod, string.Join(",", modSequences));
        }

    }
}
