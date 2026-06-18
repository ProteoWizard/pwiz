/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises three AI Connector verbs on the Document Grid:
    ///   * <see cref="JsonUiService.SetGridText"/> pastes tab/newline-separated text into a
    ///     <see cref="DataboundGridControl"/> grid at an anchor cell -- like a Ctrl-V, but without the
    ///     system clipboard;
    ///   * <see cref="JsonUiService.GetGridText"/> reads the whole grid back as tab-separated text;
    ///   * <see cref="JsonUiService.CloseForm"/> closes the grid form.
    /// The grid is found with a null controlId/gridId (the Document Grid form has a single grid), and
    /// the "Note" column is targeted by its visible index, so the test is translation-proof.
    /// </summary>
    [TestClass]
    public class SetGridTextConnectorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSetGridTextConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Two peptides whose Note column we will fill by pasting.
            RunUI(() => SkylineWindow.NewDocument());
            RunUI(() => SkylineWindow.SequenceTree.SelectPath(new IdentityPath(SequenceTree.NODE_INSERT_ID)));
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg, pasteDlg =>
            {
                SetClipboardText(TextUtil.LineSeparate("RPKPQQFFGLM\tSubstance P", "DVPKSDQFVGLM\tKassinin"));
                pasteDlg.PastePeptides();
                pasteDlg.OkDialog();
            });
            Assert.AreEqual(2, SkylineWindow.Document.PeptideCount);

            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = WaitForOpenForm<DocumentGridForm>();
            // The grid opens on the Proteins view by default; switch to Peptides so the rows are the
            // peptides whose Note column we paste into.
            RunUI(() => documentGrid.ChooseView(
                Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides));
            WaitForConditionUI(() => documentGrid.IsComplete);
            string gridId = JsonUiService.GetOpenForms()
                .First(form => form.Type == nameof(DocumentGridForm)).Id;

            // The "Note" column is editable; find its index among the visible columns -- that is the
            // column coordinate SetGridText expects.
            int noteColumn = -1;
            RunUI(() =>
            {
                var noteColumnObj = documentGrid.FindColumn(PropertyPath.Root.Property(@"Note"));
                Assert.IsNotNull(noteColumnObj, @"The Document Grid view does not contain a Note column.");
                var visibleColumns = documentGrid.DataGridView.Columns.Cast<DataGridViewColumn>()
                    .Where(col => col.Visible).OrderBy(col => col.DisplayIndex).ToList();
                noteColumn = visibleColumns.IndexOf(noteColumnObj);
            });
            Assert.IsTrue(noteColumn >= 0);

            // Paste two newline-separated values into the Note column starting at row 0. controlId is
            // null because the Document Grid form has a single grid.
            JsonUiService.SetGridText(gridId, null, noteColumn, 0, TextUtil.LineSeparate(@"First note", @"Second note"));
            WaitForConditionUI(() => documentGrid.IsComplete);

            var notes = SkylineWindow.Document.Peptides.Select(pep => pep.Note).ToArray();
            CollectionAssert.AreEqual(new[] { @"First note", @"Second note" }, notes,
                @"SetGridText did not paste the Note values into the grid.");

            // GetGridText returns the whole grid as tab-separated text: a header row plus the two
            // peptide rows, including the notes just pasted. gridId is null (single grid on the form).
            string gridText = JsonUiService.GetGridText(gridId, null);
            var lines = gridText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(3, lines.Length, @"GetGridText should return a header row plus two peptide rows.");
            StringAssert.Contains(lines[0], @"Note", @"GetGridText header row is missing the Note column.");
            StringAssert.Contains(gridText, @"First note");
            StringAssert.Contains(gridText, @"Second note");

            // CloseForm closes the (floating) Document Grid; GetOpenForms then no longer lists it.
            JsonUiService.CloseForm(gridId);
            WaitForCondition(() => JsonUiService.GetOpenForms().All(form => form.Type != nameof(DocumentGridForm)));
        }
    }
}
