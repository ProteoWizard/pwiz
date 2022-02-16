/*
 * Original author: Brian Pratt <bspratt at proteinms.net>,
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SmallMoleculesDocumentGridTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestSmallMoleculesDocumentGrid()
        {
            TestFilesZip = @"TestFunctional\SmallMoleculesDocumentGrid.zip";
            RunFunctionalTest();
        }

        /// <summary>
        /// Verify the context-dependent column selections in the "Transition List" document grid view
        /// </summary>
        protected override void DoTest()
        {
            TestMixedDoc();
            TestPeptideOnlyDoc();
            TestSmallMoleculeOnlyDoc();
        }

        private void TestMixedDoc()
        {
            // Try a mixed document
            const string mixedSky = "mixed.sky";
            EnsureMixedTransitionListReport();
            CheckDocumentGridAndColumns(mixedSky,
                MIXED_TRANSITION_LIST_REPORT_NAME,
                49, 32, SrmDocument.DOCUMENT_TYPE.mixed, "C19H34[M-H]", "custom", "C12H19", "C12H19", "C12H18");

            CheckDocumentGridAndColumns(mixedSky,
                Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors,
                4, 15, SrmDocument.DOCUMENT_TYPE.mixed);

            CheckDocumentGridAndColumns(mixedSky,
                Resources.SkylineViewContext_GetDocumentGridRowSources_Molecules,
                2, 14, SrmDocument.DOCUMENT_TYPE.mixed);

        }

        private void TestSmallMoleculeOnlyDoc()
        {
            // Try a small-molecule-only document
            const string smallMoleculeSky = "small_molecule.sky";
            CheckDocumentGridAndColumns(smallMoleculeSky, 
                Resources.SkylineViewContext_GetTransitionListReportSpec_Small_Molecule_Transition_List,
                1, 17, SrmDocument.DOCUMENT_TYPE.small_molecules, "C19H34[M-H]", null,
                "C12H20", "C12H19H'", "C12H17H'"); // Distinguish molecule formula (no labels or ionization) from precursor formula (labels) from ion formula (labels and ionization)

            CheckDocumentGridAndColumns(smallMoleculeSky,
                Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors,
                1, 14, SrmDocument.DOCUMENT_TYPE.small_molecules);

            CheckDocumentGridAndColumns(smallMoleculeSky,
                Resources.SkylineViewContext_GetDocumentGridRowSources_Molecules,
                1, 10, SrmDocument.DOCUMENT_TYPE.small_molecules);

        }

        private void TestPeptideOnlyDoc()
        {
            // Try a peptide-only document
            const string peptideSky = "peptide.sky";
            CheckDocumentGridAndColumns(peptideSky,
                Resources.SkylineViewContext_GetTransitionListReportSpec_Peptide_Transition_List,
                48, 21, SrmDocument.DOCUMENT_TYPE.proteomic, null, "y");

            CheckDocumentGridAndColumns(peptideSky,
                            Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors,
                            3, 11, SrmDocument.DOCUMENT_TYPE.proteomic);

            CheckDocumentGridAndColumns(peptideSky,
                            Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides,
                            1, 10, SrmDocument.DOCUMENT_TYPE.proteomic);

        }

        private void CheckDocumentGridAndColumns(string docName,
            string viewName,
            int rowCount, int colCount,  // Expected row and column count for document grid
            SrmDocument.DOCUMENT_TYPE expectedDocumentType,
            string expectedProductIonFormula = null,
            string expectedFragmentIon = null,
            string expectedMolecularFormula = null,
            string expectedPrecursorNeutralFormula = null,
            string expectedPrecursorIonFormula = null)
        {
            var oldDoc = SkylineWindow.Document;
            OpenDocument(docName);
            WaitForDocumentChangeLoaded(oldDoc);
            Assume.AreEqual(expectedDocumentType, SkylineWindow.Document.DocumentType);
            WaitForClosedForm<DocumentGridForm>();
            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            RunUI(() => documentGrid.ChooseView(viewName));
            WaitForCondition(() => (documentGrid.RowCount == rowCount)); // Let it initialize
            int iteration = 0;
            WaitForCondition(() =>
            {
                bool result = documentGrid.ColumnCount == colCount;
                if (!result && iteration++ > 9)
                    Assert.AreNotEqual(colCount, documentGrid.ColumnCount);   // Put breakpoint on this line, if you have changed columns and need to update the numbers
                return result;
            }); // Let it initialize

            var colProductIonFormula = documentGrid.FindColumn(PropertyPath.Parse("ProductIonFormula"));
            var colProductNeutralFormula = documentGrid.FindColumn(PropertyPath.Parse("ProductNeutralFormula"));
            var colProductAdduct = documentGrid.FindColumn(PropertyPath.Parse("ProductAdduct"));
            var colFragmentIon = documentGrid.FindColumn(PropertyPath.Parse("FragmentIonType"));
            var colMoleculeFormula = documentGrid.FindColumn(PropertyPath.Parse("Precursor.Peptide.MoleculeFormula"));
            var colPrecursorNeutralFormula = documentGrid.FindColumn(PropertyPath.Parse("Precursor.NeutralFormula"));
            var colPrecursorIonFormula = documentGrid.FindColumn(PropertyPath.Parse("Precursor.IonFormula"));
            if (expectedProductIonFormula == null)
            {
                Assert.IsNull(colProductIonFormula);
                Assert.IsNull(colProductNeutralFormula);
                Assert.IsNull(colProductAdduct);
            }
            else RunUI(() =>
            {
                var formula = documentGrid.DataGridView.Rows[0].Cells[colProductIonFormula.Index].Value.ToString();
                if (expectedProductIonFormula.Contains("["))
                {
                    var formulaNeutral = documentGrid.DataGridView.Rows[0].Cells[colProductNeutralFormula.Index].Value.ToString();
                    var adduct = documentGrid.DataGridView.Rows[0].Cells[colProductAdduct.Index].Value.ToString();
                    Assert.AreEqual(expectedProductIonFormula, formulaNeutral+adduct);
                }
                else
                {
                    Assert.AreEqual(expectedProductIonFormula, formula);
                }
                if (!string.IsNullOrEmpty(expectedMolecularFormula))
                {
                    Assert.AreEqual(expectedMolecularFormula, documentGrid.DataGridView.Rows[0].Cells[colMoleculeFormula.Index].Value.ToString());
                    Assert.AreEqual(expectedPrecursorNeutralFormula, documentGrid.DataGridView.Rows[0].Cells[colPrecursorNeutralFormula.Index].Value.ToString());
                    Assert.AreEqual(expectedPrecursorIonFormula, documentGrid.DataGridView.Rows[0].Cells[colPrecursorIonFormula.Index].Value.ToString());
                }
            });
            if (expectedFragmentIon == null)
                Assert.IsNull(colFragmentIon);
            else RunUI(() =>
            {
                var frag = documentGrid.DataGridView.Rows[0].Cells[colFragmentIon.Index].Value.ToString();
                Assert.AreEqual(expectedFragmentIon, frag);
            });
            RunUI(() => documentGrid.Close());
        }

    }  
}
