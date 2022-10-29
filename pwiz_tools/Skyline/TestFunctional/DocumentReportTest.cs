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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
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
            string documentFilePath = TestContext.GetTestResultsPath("DocumentReportTest.sky");
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
                documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.reports);
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


            // Add some peptides to the document
            SetClipboardText("ELVIS\nLIVES");
            RunUI(SkylineWindow.Paste);
            Assert.AreEqual(2, SkylineWindow.Document.PeptideCount);

            // Test using the "Add" and "Edit List" buttons on reports tab.
            const string addedFromDocumentSettings = "Added From Document Settings";
            {
                var documentReportNames = SkylineWindow.Document.Settings.DataSettings.ViewSpecList.ViewSpecs
                    .Select(spec => spec.Name).ToList();
                CollectionAssert.Contains(documentReportNames, viewName);
                CollectionAssert.DoesNotContain(documentReportNames, addedFromDocumentSettings);
            }

            documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            RunUI(()=>documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.reports));
            CollectionAssert.DoesNotContain(documentSettingsDlg.ChooseViewsControl.CheckedViews.ToList(), 
                PersistedViews.MainGroup.Id.ViewName(addedFromDocumentSettings));

            viewEditor = ShowDialog<ViewEditor>(documentSettingsDlg.NewReport);
            RunUI(()=>
            {
                viewEditor.ViewName = addedFromDocumentSettings;
                viewEditor.ChooseColumnsTab.AddColumn(PropertyPath.Root
                    .Property(nameof(SkylineDocument.Proteins)).LookupAllItems()
                    .Property(nameof(Protein.Peptides)).LookupAllItems());
                viewEditor.ChooseColumnsTab.AddColumn(PropertyPath.Root
                    .Property(nameof(SkylineDocument.Proteins)).LookupAllItems()
                    .Property(nameof(Protein.Peptides)).LookupAllItems()
                    .Property(nameof(Skyline.Model.Databinding.Entities.Peptide.MoleculeFormula)));
            });


            var previewForm = ShowDialog<DocumentGridForm>(viewEditor.ShowPreview);
            WaitForConditionUI(() => previewForm.IsComplete);
            Assert.AreEqual(2, previewForm.DataGridView.RowCount);
            OkDialog(previewForm, previewForm.Close);
            OkDialog(viewEditor, viewEditor.OkDialog);
            CollectionAssert.Contains(documentSettingsDlg.ChooseViewsControl.CheckedViews.ToList(), 
                PersistedViews.MainGroup.Id.ViewName(addedFromDocumentSettings));
            manageViewsForm = ShowDialog<ManageViewsForm>(documentSettingsDlg.EditReportList);
            RunUI(()=>
            {
                manageViewsForm.SelectView(viewName);
                manageViewsForm.Remove(false);
            });
            OkDialog(manageViewsForm, manageViewsForm.OkDialog);
            OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);
            {
                var documentReportNames = SkylineWindow.Document.Settings.DataSettings.ViewSpecList.ViewSpecs
                    .Select(spec => spec.Name).ToList();
                CollectionAssert.DoesNotContain(documentReportNames, viewName);
                CollectionAssert.Contains(documentReportNames, addedFromDocumentSettings);
            }
        }
    }
}
