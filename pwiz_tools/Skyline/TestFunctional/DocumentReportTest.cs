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
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DocumentReportTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDocumentReports()
        {
            Settings.Default.PersistedViews.ResetDefaults();
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string viewName = "WillAddToDocument";
            string documentFilePath = TestContext.GetTestPath("DocumentReportTest.sky");
            var exportLiveReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            var manageViewsForm = ShowDialog<ManageViewsForm>(exportLiveReportDlg.EditList);
            var viewEditor = ShowDialog<ViewEditor>(manageViewsForm.AddView);
            RunUI(() =>
            {
                viewEditor.ViewName = viewName;
                Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(PropertyPath.Parse("Proteins!*")));
                viewEditor.ChooseColumnsTab.AddSelectedColumn();
            });
            OkDialog(viewEditor, viewEditor.OkDialog);
            OkDialog(manageViewsForm, manageViewsForm.OkDialog);
            OkDialog(exportLiveReportDlg, exportLiveReportDlg.CancelClick);
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            RunUI(() =>
            {
                documentSettingsDlg.ChooseViewsControl.CheckedViews = new[]{PersistedViews.MainGroup.Id.ViewName(viewName)};
            });
            OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);
            RunUI(() =>
            {
                SkylineWindow.SaveDocument(documentFilePath);
            });
            exportLiveReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            manageViewsForm = ShowDialog<ManageViewsForm>(exportLiveReportDlg.EditList);
            RunUI(() =>
            {
                manageViewsForm.SelectView(viewName);
                manageViewsForm.Remove(false);
            });
            OkDialog(manageViewsForm, manageViewsForm.OkDialog);
            OkDialog(exportLiveReportDlg, exportLiveReportDlg.CancelClick);
            Assert.IsNull(Settings.Default.PersistedViews.GetViewSpecList(PersistedViews.MainGroup.Id).GetView(viewName));
            RunUI(() =>
            {
                SkylineWindow.OpenFile(documentFilePath);
            });
            Assert.IsNotNull(Settings.Default.PersistedViews.GetViewSpecList(PersistedViews.MainGroup.Id).GetView(viewName));
        }
    }
}
