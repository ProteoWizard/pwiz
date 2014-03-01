/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class LiveReportsFilterTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLiveReportsFilter()
        {
            TestFilesZip = @"TestFunctional\LiveReportsFilterTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestRatioFilter();
        }
        private void TestRatioFilter()
        {
            const double filterValue = 5.0;
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MultiLabel.sky"));
                SkylineWindow.ShowDocumentGrid(true);
                
            });
            DocumentGridForm documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(()=>documentGrid.ChooseView("Precursors"));
            var modifications = SkylineWindow.Document.Settings.PeptideSettings.Modifications;
            var isotopeLabel = modifications.InternalStandardTypes.First(
                    label => label.Name == "heavy 15N");
            AddFilter(documentGrid, PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Results!*.Value")
                        .Property(RatioPropertyDescriptor.MakePropertyName(RatioPropertyDescriptor.RATIO_PREFIX, isotopeLabel)), 
                    FilterOperations.OP_IS_GREATER_THAN_OR_EQUAL,
                    filterValue.ToString(CultureInfo.CurrentCulture));
            RunUI(() =>
            {
                var colPeptide = documentGrid.FindColumn(PropertyPath.Parse("Peptide"));
                var filteredPeptides = Enumerable.Range(0, documentGrid.RowCount).Select(
                    rowIndex => (Peptide) documentGrid.DataGridView.Rows[rowIndex].Cells[colPeptide.Index].Value)
                    .ToArray();
                CollectionAssert.AreEqual(new []{"AEVAALAAENK", "AIDYVEATANSHSR"}, 
                    filteredPeptides.Select(peptide=>peptide.ToString()).ToArray());
                var allPeptides = new Peptides(new SkylineDataSchema(SkylineWindow), new []{IdentityPath.ROOT});
                var ratioIndex = modifications.InternalStandardTypes.IndexOf(isotopeLabel);
                Assert.IsTrue(ratioIndex >= 0);
                foreach (var peptide in allPeptides)
                {
                    bool hasMatchingPrecursorResult =
                        peptide.Precursors.Any(
                            precursor =>
                                precursor.Results.Values.Any(
                                    precursorResult =>
                                        RatioValue.GetRatio(precursorResult.ChromInfo.Ratios[ratioIndex]) >= filterValue));
                    Assert.AreEqual(hasMatchingPrecursorResult, filteredPeptides.Any(filteredPeptide => filteredPeptide.IdentityPath.Equals(peptide.IdentityPath)));
                }
            }
            );
        }
        private void AddFilter(DataboundGridForm databoundGridForm, PropertyPath propertyPath,
            IFilterOperation filterOperation, string filterOperand)
        {
            var viewEditor = ShowDialog<ViewEditor>(databoundGridForm.NavBar.CustomizeView);
            RunUI(() =>
            {
                viewEditor.TabControl.SelectTab(1);
                Assert.IsTrue(viewEditor.FilterTab.TrySelectColumn(propertyPath));
                int iFilter = viewEditor.ViewInfo.Filters.Count;
                viewEditor.FilterTab.AddSelectedColumn();
                viewEditor.FilterTab.SetFilterOperation(iFilter, filterOperation);
                if (null != filterOperand)
                {
                    viewEditor.FilterTab.SetFilterOperand(iFilter, filterOperand);
                }
            });
            OkDialog(viewEditor, viewEditor.OkDialog);
            WaitForConditionUI(() => databoundGridForm.IsComplete);
        }
    }
}
