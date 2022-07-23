/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests remembering and restoring the current cell state in the Document Grid when an undo/redo happens
    /// </summary>
    [TestClass]
    public class DocumentGridUndoTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDocumentGridUndo()
        {
            RunFunctionalTest();
        }
        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.Paste(TextUtil.LineSeparate("ELVIS", "LIVES", "EVILS")));
            Assert.AreEqual(3, SkylineWindow.Document.PeptideCount);
            RunUI(()=>SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(()=>{documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides);});
            WaitForConditionUI(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                Assert.AreEqual(3, documentGrid.RowCount);
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[2].Cells[1];
            });
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 1);
                SkylineWindow.EditDelete();
            });
            Assert.AreEqual(2, SkylineWindow.Document.PeptideCount);
            RunUI(()=>
            {
                SkylineWindow.Undo();
                Assert.AreEqual(3, SkylineWindow.Document.PeptideCount);
                Assert.AreEqual(new Point(1, 2), documentGrid.DataGridView.CurrentCellAddress);
                documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Transitions);
            });
            WaitForConditionUI(() => documentGrid.IsComplete);
            var cellAddressInTransitionsReport = new Point(6, 3);
            var otherCellAddress = new Point(0, 1);
            RunUI(() =>
            {
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[cellAddressInTransitionsReport.Y]
                    .Cells[cellAddressInTransitionsReport.X];
                SkylineWindow.Redo();
                // Current cell was not changed in document grid because the current report is different than what was in the undo record.
                Assert.AreEqual(cellAddressInTransitionsReport, documentGrid.DataGridView.CurrentCellAddress);
                SkylineWindow.Undo();

                // This next time, when we do a redo, it is going to change the current cell in the document grid because the report
                // name matches the undo record.
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[otherCellAddress.Y].Cells[otherCellAddress.X];
                SkylineWindow.Redo();
                Assert.AreEqual(cellAddressInTransitionsReport, documentGrid.DataGridView.CurrentCellAddress);

                SkylineWindow.Undo();
                Assert.AreEqual(otherCellAddress, documentGrid.DataGridView.CurrentCellAddress);
            });
        }
    }
}
