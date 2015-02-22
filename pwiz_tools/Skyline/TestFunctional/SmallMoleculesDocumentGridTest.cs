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

using System.Windows.Forms;
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
            if (!IsEnableLiveReports)
                return;

            TestMixedDoc();
            TestPeptideOnlyDoc();
            TestSmallMoleculeOnlyDoc();
        }

        private void TestMixedDoc()
        {
            // Try a mixed document
            DataGridViewColumn colProductIonFormula;
            DataGridViewColumn colFragmentIon;
            var documentGrid = GetDocumentGridAndColumns("mixed.sky",
                Resources.SkylineViewContext_GetTransitionListReportSpec_Mixed_Transition_List,
                49, 26, out colProductIonFormula, out colFragmentIon);
            RunUI(() =>
            {
                var productIonFormula = documentGrid.DataGridView.Rows[0].Cells[colProductIonFormula.Index].Value.ToString();
                Assert.AreEqual(productIonFormula, "C19H33");
                var frag = documentGrid.DataGridView.Rows[1].Cells[colFragmentIon.Index].Value.ToString();
                Assert.AreEqual("y", frag);
                documentGrid.Close();
            });
        }

        private void TestSmallMoleculeOnlyDoc()
        {
            // Try a small-molecule-only document
            DataGridViewColumn colProductIonFormula;
            DataGridViewColumn colFragmentIon;
            var documentGrid = GetDocumentGridAndColumns("small_molecule.sky", 
                Resources.SkylineViewContext_GetTransitionListReportSpec_Small_Molecule_Transition_List,
                1, 11, out colProductIonFormula, out colFragmentIon);
            Assert.IsNull(colFragmentIon);
            RunUI(() =>
            {
                var productIonFormula = documentGrid.DataGridView.Rows[0].Cells[colProductIonFormula.Index].Value.ToString();
                Assert.AreEqual(productIonFormula, "C19H33");
                documentGrid.Close();
            });
        }

        private void TestPeptideOnlyDoc()
        {
            // Try a peptide-only document
            DataGridViewColumn colProductIonFormula;
            DataGridViewColumn colFragmentIon;
            var documentGrid = GetDocumentGridAndColumns("peptide.sky",
                Resources.SkylineViewContext_GetTransitionListReportSpec_Peptide_Transition_List,
                48, 21, out colProductIonFormula, out colFragmentIon);
            Assert.IsNull(colProductIonFormula);
            RunUI(() =>
            {
                var frag = documentGrid.DataGridView.Rows[0].Cells[colFragmentIon.Index].Value.ToString();
                Assert.AreEqual("y", frag);
                documentGrid.Close();
            });
        }

        private DocumentGridForm GetDocumentGridAndColumns(string docName, 
            string viewName,
            int rowCount, int colCount,  // Expected row and column count for document grid
            out DataGridViewColumn colProductIonFormula, 
            out DataGridViewColumn colFragmentIon)
        {
            var oldDoc = SkylineWindow.Document;
            OpenDocument(docName);
            WaitForDocumentChangeLoaded(oldDoc);
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = WaitForOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.ChooseView(viewName));
            WaitForCondition(() => (documentGrid.RowCount == rowCount)); // Let it initialize
            WaitForCondition(() => (documentGrid.ColumnCount == colCount)); // Let it initialize
            colProductIonFormula = documentGrid.FindColumn(PropertyPath.Parse("ProductIonFormula"));
            colFragmentIon = documentGrid.FindColumn(PropertyPath.Parse("FragmentIonType"));
            return documentGrid;
        }

    }  
}
