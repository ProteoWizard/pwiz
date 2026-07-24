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
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Lists;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// The ONE dialog Skyline shows on a thread of its own: the BackgroundThreadLongWaitDlg that
    /// LongOperationRunner puts up for a big paste into a grid. It is the inverse of every other connector
    /// scenario -- the work runs on the MAIN UI thread, which is therefore not pumping, while the dialog runs
    /// its own message loop on its own thread. So the connector cannot reach it through the main window: it has
    /// to enumerate the window off any thread and then marshal to THAT dialog's thread.
    ///
    /// <para>The two things that proves are separate nested tests, each with its own DoTest: that the connector
    /// can READ the dialog while the main thread is busy (the read must marshal to the dialog's thread), and that
    /// CANCELLING it actually stops the paste (the only way to stop it). Splitting them keeps each test to a
    /// single aspect, so a nightly leak report names exactly which one to look at; the shared paste setup lives
    /// on this abstract base.</para>
    /// </summary>
    public abstract class McpConnectorBackgroundDialogTest : McpConnectorTest
    {
        // Enough rows that the paste is still running when the connector gets to the dialog. The paste converts
        // and validates every cell, so this takes seconds, not milliseconds.
        protected const int PROPERTY_COUNT = 50000;

        /// <summary>
        /// Opens the List Designer and starts a big paste into its grid WITHOUT waiting: the paste occupies the
        /// MAIN UI thread (LongOperationRunner runs the work on the calling thread and puts only the dialog on a
        /// thread of its own), so a blocking call would leave no one to drive that dialog. Returns the id of the
        /// BackgroundThreadLongWaitDlg -- found by enumerating the top-level windows, which needs no thread at
        /// all, NOT by asking the main window (which is busy pasting) -- and hands back the designer dialogs and
        /// grid for the caller to verify against and tear down.
        /// </summary>
        protected string StartBackgroundPaste(out DocumentSettingsDlg documentSettingsDlg,
            out ListDesigner listDesigner, out DataGridView grid)
        {
            documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var designer = listDesigner = ShowDialog<ListDesigner>(documentSettingsDlg.AddList);
            RunUI(() => designer.ListName = @"BigList");

            var propertiesText = new StringBuilder();
            for (int i = 0; i < PROPERTY_COUNT; i++)
                propertiesText.Append(@"Property").Append(i).Append('\t').AppendLine(@"Text");

            // The paste is anchored at the current cell (it sets each cell through the grid's editing control, the
            // way a user typing does, so a value entered in the new row commits and the grid grows).
            var pasteGrid = grid = designer.ListPropertiesGrid;
            RunUI(() => pasteGrid.CurrentCell = pasteGrid.Rows[0].Cells[0]);

            // Start it on the main thread, without waiting; the test thread goes on to drive the dialog.
            designer.BeginInvoke((Action) (() =>
                DataGridViewPasteHandler.PasteText(pasteGrid, propertiesText.ToString())));

            // The dialog is a window like any other to the connector -- found by enumerating the top-level windows.
            return WaitForMcpConnectorForm(@"BackgroundThreadLongWaitDlg");
        }

        /// <summary>Cancels the background dialog (the only way to stop the paste) and waits for it to close --
        /// which frees the main window's thread again.</summary>
        protected void CancelBackgroundDialog(string dialogId)
        {
            AssertComplete(McpConnector.DismissWithCancelButton(dialogId));
            WaitForConditionUI(() => !McpConnector.GetOpenForms()
                .Any(form => Equals(form.Type, @"BackgroundThreadLongWaitDlg")));
        }

        /// <summary>Closes the List Designer and Document Settings dialogs left open by the setup.</summary>
        protected void CloseListDesigner(DocumentSettingsDlg documentSettingsDlg, ListDesigner listDesigner)
        {
            // The List Designer has no cancel button of its own (FormEx.CancelDialog would throw); just close it.
            OkDialog(listDesigner, listDesigner.Close);
            OkDialog(documentSettingsDlg, documentSettingsDlg.CancelDialog);
        }

        /// <summary>
        /// Reads the background-thread dialog through the connector while the main thread is busy pasting: the
        /// read must land on the DIALOG's thread (the main window's thread is inside the paste and would never
        /// run it), and it reads back the dialog's Cancel button.
        /// </summary>
        [TestClass]
        public class ReadWhileMainThreadBusy : McpConnectorBackgroundDialogTest
        {
            [TestMethod]
            public void TestMcpConnectorBackgroundDialogRead()
            {
                RunFunctionalTest();
            }

            protected override void DoTest()
            {
                StartToolService();
                var dialogId = StartBackgroundPaste(out var documentSettingsDlg, out var listDesigner, out _);

                // Read it: this must land on the DIALOG's thread, not the busy main thread.
                var controls = McpConnector.GetControls(dialogId);
                Assert.IsTrue(controls.Any(control => Equals(control.Path.Type, @"Button")),
                    @"The background dialog's Cancel button was not read back.");

                // Cancel to stop the paste and free the main thread so the designer can be closed cleanly.
                CancelBackgroundDialog(dialogId);
                CloseListDesigner(documentSettingsDlg, listDesigner);
            }
        }

        /// <summary>
        /// Cancels the background-thread dialog through the connector and proves it stopped the paste EARLY: the
        /// grid never received all the properties (nor did the paste run to the end and merely look cancelled).
        /// </summary>
        [TestClass]
        public class CancelStopsPaste : McpConnectorBackgroundDialogTest
        {
            [TestMethod]
            public void TestMcpConnectorBackgroundDialogCancel()
            {
                RunFunctionalTest();
            }

            protected override void DoTest()
            {
                StartToolService();
                var dialogId = StartBackgroundPaste(out var documentSettingsDlg, out var listDesigner, out var grid);

                // Cancel it, which is the only way to stop the paste. The dialog going away means the paste it was
                // reporting on stopped -- and the main window's thread is free again.
                CancelBackgroundDialog(dialogId);
                RunUI(() => Assert.IsTrue(grid.RowCount < PROPERTY_COUNT,
                    string.Format(@"The paste was not cancelled: all {0} properties landed in the grid.", PROPERTY_COUNT)));

                CloseListDesigner(documentSettingsDlg, listDesigner);
            }
        }
    }
}
