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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Lists;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Drives the ONE dialog Skyline shows on a thread of its own: the BackgroundThreadLongWaitDlg that
    /// LongOperationRunner puts up for a big paste into a grid. It is the inverse of every other connector
    /// scenario -- the work runs on the MAIN UI thread, which is therefore not pumping, while the dialog runs
    /// its own message loop on its own thread. So the connector cannot reach it through the main window: it has
    /// to enumerate the window off any thread and then marshal to THAT dialog's thread.
    ///
    /// <para>Cancelling is the whole point. The dialog is the only way to stop the paste, so a connector that
    /// cannot drive it cannot stop a paste it started -- and the main window will not be free again until the
    /// paste finishes.</para>
    /// </summary>
    [TestClass]
    public class ConnectorBackgroundDialogTest : McpConnectorTest
    {
        // Enough rows that the paste is still running when the connector gets to the dialog. The paste converts
        // and validates every cell, so this takes seconds, not milliseconds.
        private const int PROPERTY_COUNT = 50000;

        [TestMethod]
        public void TestConnectorBackgroundDialog()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            StartToolService();

            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var listDesigner = ShowDialog<ListDesigner>(documentSettingsDlg.AddList);
            RunUI(() => listDesigner.ListName = @"BigList");

            var propertiesText = new StringBuilder();
            for (int i = 0; i < PROPERTY_COUNT; i++)
                propertiesText.Append(@"Property").Append(i).Append('\t').AppendLine(@"Text");

            // The paste is anchored at the current cell (it sets each cell through the grid's editing control, the
            // way a user typing does, so a value entered in the new row commits and the grid grows).
            var grid = listDesigner.ListPropertiesGrid;
            RunUI(() => grid.CurrentCell = grid.Rows[0].Cells[0]);

            // Start it WITHOUT waiting: the paste occupies the MAIN UI thread (LongOperationRunner runs the work on
            // the calling thread and puts only the dialog on a thread of its own), so a blocking call here would
            // leave no one to drive that dialog. The test thread goes on to drive it through the connector.
            listDesigner.BeginInvoke((Action) (() =>
                DataGridViewPasteHandler.PasteText(grid, propertiesText.ToString())));

            // The dialog is a window like any other to the connector -- found by enumerating the top-level windows,
            // which needs no thread at all, NOT by asking the main window (which is busy pasting).
            string dialogId = WaitForConnectorForm(@"BackgroundThreadLongWaitDlg");

            // Read it: this must land on the DIALOG's thread. The main window's thread is inside the paste and would
            // never run the read.
            var controls = Connector.GetControls(dialogId);
            Assert.IsTrue(controls.Any(control => Equals(control.Path.Type, @"Button")),
                @"The background dialog's Cancel button was not read back.");

            // Cancel it, which is the only way to stop the paste.
            AssertComplete(Connector.DismissWithCancelButton(dialogId));

            // The dialog is gone, which means the paste it was reporting on stopped -- and the main window's thread is
            // free again, which is what lets these reads run at all. It stopped EARLY: the grid never got all the
            // properties (nor did the paste run to the end and merely look cancelled).
            WaitForConditionUI(() => !Connector.GetOpenForms()
                .Any(form => Equals(form.Type, @"BackgroundThreadLongWaitDlg")));
            RunUI(() => Assert.IsTrue(grid.RowCount < PROPERTY_COUNT,
                string.Format(@"The paste was not cancelled: all {0} properties landed in the grid.", PROPERTY_COUNT)));

            // The List Designer has no cancel button of its own (FormEx.CancelDialog would throw); just close it.
            OkDialog(listDesigner, listDesigner.Close);
            OkDialog(documentSettingsDlg, documentSettingsDlg.CancelDialog);
        }
    }
}
