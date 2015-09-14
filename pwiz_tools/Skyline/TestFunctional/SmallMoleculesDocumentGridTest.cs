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
using pwiz.Skyline.Properties;
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
            CheckDocumentGridAndColumns(mixedSky,
                Resources.SkylineViewContext_GetTransitionListReportSpec_Mixed_Transition_List,
                49, 27, "C19H33", "custom");

            CheckDocumentGridAndColumns(mixedSky,
                Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors,
                4, 15);

            CheckDocumentGridAndColumns(mixedSky,
                Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides,
                2, 14);

        }

        private void TestSmallMoleculeOnlyDoc()
        {
            // Try a small-molecule-only document
            const string smallMoleculeSky = "small_molecule.sky";
            CheckDocumentGridAndColumns(smallMoleculeSky, 
                Resources.SkylineViewContext_GetTransitionListReportSpec_Small_Molecule_Transition_List,
                1, 12, "C19H33");

            CheckDocumentGridAndColumns(smallMoleculeSky,
                Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors,
                1, 14);

            CheckDocumentGridAndColumns(smallMoleculeSky,
                Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides,
                1, 10);

        }

        private void TestPeptideOnlyDoc()
        {
            // Try a peptide-only document
            const string peptideSky = "peptide.sky";
            CheckDocumentGridAndColumns(peptideSky,
                Resources.SkylineViewContext_GetTransitionListReportSpec_Peptide_Transition_List,
                48, 21, null, "y");

            CheckDocumentGridAndColumns(peptideSky,
                            Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors,
                            3, 12);

            CheckDocumentGridAndColumns(peptideSky,
                            Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides,
                            1, 10);

        }

        private void CheckDocumentGridAndColumns(string docName,
            string viewName,
            int rowCount, int colCount,  // Expected row and column count for document grid
            string expectedProductIonFormula = null,
            string expectedFragmentIon = null)
        {
            var oldDoc = SkylineWindow.Document;
            OpenDocument(docName);
            WaitForDocumentChangeLoaded(oldDoc);
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = WaitForOpenForm<DocumentGridForm>();
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
            var colFragmentIon = documentGrid.FindColumn(PropertyPath.Parse("FragmentIonType"));
            if (expectedProductIonFormula == null)
                Assert.IsNull(colProductIonFormula);
            else RunUI(() =>
            {
                var formula = documentGrid.DataGridView.Rows[0].Cells[colProductIonFormula.Index].Value.ToString();
                Assert.AreEqual(expectedProductIonFormula, formula);
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
