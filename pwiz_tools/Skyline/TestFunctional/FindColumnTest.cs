/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FindColumnTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestFindColumn()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            PropertyPath expectedPath1 = PropertyPath.Parse("Proteins!*.Peptides!*.Results!*");
            PropertyPath expectedPath2 = PropertyPath.Parse("Proteins!*.Peptides!*.Results!*.Value.PeptideRetentionTime");
            PropertyPath expectedPath3 = PropertyPath.Parse("Proteins!*.Peptides!*.Results!*.Value.DocumentLocation");

            var exportReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            RunUI(() =>
            {
                exportReportDlg.SetUseInvariantLanguage(true);
            });
            var manageViewsForm = ShowDialog<ManageViewsForm>(exportReportDlg.EditList);
            var viewEditor = ShowDialog<ViewEditor>(manageViewsForm.AddView);
            var findColumnDlg = ShowDialog<FindColumnDlg>(viewEditor.ShowFindDialog);
            // Search for "PeptideRe"
            RunUI(()=>findColumnDlg.FindText = "PeptideRe");
            WaitForConditionUI(findColumnDlg.IsReadyToSearch);
            RunUI(findColumnDlg.SearchForward);
            // Found "PeptideResults"
            Assert.AreEqual(expectedPath1, GetSelectedPath(viewEditor));
            WaitForConditionUI(findColumnDlg.IsReadyToSearch);
            RunUI(findColumnDlg.SearchForward);
            // Found "PeptideRetentionTime"
            Assert.AreEqual(expectedPath2, GetSelectedPath(viewEditor));
            WaitForConditionUI(findColumnDlg.IsReadyToSearch);
            RunUI(findColumnDlg.SearchForward);
            // Found "PeptideResultDocumentLocation"
            Assert.AreEqual(expectedPath3, GetSelectedPath(viewEditor));
            WaitForConditionUI(findColumnDlg.IsReadyToSearch);
            RunUI(findColumnDlg.SearchForward);
            // Didn't find anything so still on "PeptideResultDocumentLocation"
            Assert.AreEqual(expectedPath3, GetSelectedPath(viewEditor));
            WaitForConditionUI(findColumnDlg.IsReadyToSearch);
            RunUI(findColumnDlg.SearchBackward);
            // Searched backward to "PeptideRetentionTime"
            Assert.AreEqual(expectedPath2, GetSelectedPath(viewEditor));
            OkDialog(viewEditor, viewEditor.CancelButton.PerformClick);
            OkDialog(manageViewsForm, manageViewsForm.OkDialog);
            OkDialog(exportReportDlg, exportReportDlg.CancelClick);
        }

        private PropertyPath GetSelectedPath(ViewEditor viewEditor)
        {
            PropertyPath selectedPath = null;
            RunUI(() =>
            {
                var tree = viewEditor.ActiveAvailableFieldsTree;
                var column = tree.GetTreeColumn(tree.SelectedNode);
                if (null != column)
                {
                    selectedPath = column.PropertyPath;
                }
            });
            return selectedPath;
        }
    }
}
