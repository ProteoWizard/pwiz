/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that changing annotation definitions in Skyline causes the document grid to update.
    /// </summary>
    [TestClass]
    public class UpdateAnnotationsGridTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestUpdateAnnotations()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string ANNOTATION_NAME = "MyAnnotation";

            var annotationPropertyPath = PropertyPath.Root.Property(AnnotationDef.ANNOTATION_PREFIX + ANNOTATION_NAME);
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg, pasteDlg =>
            {
                SetClipboardText(TextUtil.LineSeparate("ELVIS\tProtein1", "LIVES\tProtein2"));
                pasteDlg.PastePeptides();
                pasteDlg.OkDialog();
            });
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(()=>documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Proteins));

            // Define a text annotation, and make sure the column appears in the grid
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(
                documentSettingsDlg.EditAnnotationList);
            RunDlg<DefineAnnotationDlg>(editListDlg.AddItem, defineAnnotationDlg =>
            {
                defineAnnotationDlg.AnnotationName = ANNOTATION_NAME;
                defineAnnotationDlg.AnnotationType = AnnotationDef.AnnotationType.text;
                defineAnnotationDlg.AnnotationTargets 
                    = AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.protein);
                defineAnnotationDlg.OkDialog();
            });
            OkDialog(editListDlg, editListDlg.OkDialog);
            RunUI(()=>
            {
                for (int i = 0; i < documentSettingsDlg.AnnotationsCheckedListBox.Items.Count; i++)
                {
                    documentSettingsDlg.AnnotationsCheckedListBox.SetItemChecked(i, true);
                }
            });
            OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);
            WaitForConditionUI(() => documentGrid.IsComplete);
            var column = documentGrid.FindColumn(annotationPropertyPath);
            Assert.IsInstanceOfType(column, typeof(DataGridViewTextBoxColumn));

            // Now change the type of the annotation to true/false and make sure the column changes to a checkbox column
            documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(
                documentSettingsDlg.EditAnnotationList);
            RunUI(()=>editListDlg.SelectItem(ANNOTATION_NAME));
            RunDlg<DefineAnnotationDlg>(editListDlg.EditItem, defineAnnotationDlg =>
            {
                defineAnnotationDlg.AnnotationType = AnnotationDef.AnnotationType.true_false;
                defineAnnotationDlg.OkDialog();
            });
            OkDialog(editListDlg, editListDlg.OkDialog);
            OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);
            WaitForConditionUI(() => documentGrid.IsComplete);
            column = documentGrid.FindColumn(annotationPropertyPath);
            Assert.IsInstanceOfType(column, typeof(DataGridViewCheckBoxColumn));

            // Now change the annotation to a value list, and make sure the column becomes a combo box column
            documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(
                documentSettingsDlg.EditAnnotationList);
            RunUI(() => editListDlg.SelectItem(ANNOTATION_NAME));
            var valueList1 = new[] {"January", "February", "March"};
            RunDlg<DefineAnnotationDlg>(editListDlg.EditItem, defineAnnotationDlg =>
            {
                defineAnnotationDlg.AnnotationType = AnnotationDef.AnnotationType.value_list;
                defineAnnotationDlg.Items = valueList1;
                defineAnnotationDlg.OkDialog();
            });
            OkDialog(editListDlg, editListDlg.OkDialog);
            OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);
            WaitForConditionUI(() => documentGrid.IsComplete);
            column = documentGrid.FindColumn(annotationPropertyPath);
            Assert.IsInstanceOfType(column, typeof(DataGridViewComboBoxColumn));
            var cell = documentGrid.DataGridView.Rows[0].Cells[column.Index];
            Assert.IsInstanceOfType(cell, typeof(DataGridViewComboBoxCell));
            var comboBoxCell = (DataGridViewComboBoxCell)cell;
            CollectionAssert.AreEqual(valueList1.Prepend(string.Empty).ToArray(), comboBoxCell.Items);

            // Change the set of items for the value list annotation
            documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(
                documentSettingsDlg.EditAnnotationList);
            RunUI(() => editListDlg.SelectItem(ANNOTATION_NAME));
            var valueList2 = new[] {"Monday", "Tuesday", "Wednesday"};
            RunDlg<DefineAnnotationDlg>(editListDlg.EditItem, defineAnnotationDlg =>
            {
                defineAnnotationDlg.Items = valueList2;
                defineAnnotationDlg.OkDialog();
            });
            OkDialog(editListDlg, editListDlg.OkDialog);
            OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);
            WaitForConditionUI(() => documentGrid.IsComplete);
            column = documentGrid.FindColumn(annotationPropertyPath);
            Assert.IsInstanceOfType(column, typeof(DataGridViewComboBoxColumn));
            cell = documentGrid.DataGridView.Rows[1].Cells[column.Index];
            Assert.IsInstanceOfType(cell, typeof(DataGridViewComboBoxCell));
            comboBoxCell = (DataGridViewComboBoxCell)cell;
            CollectionAssert.AreEqual(valueList2.Prepend(string.Empty).ToArray(), comboBoxCell.Items);

            // Make it so the annotation no longer applies to Proteins, so that it disappears from the grid.
            documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(
                documentSettingsDlg.EditAnnotationList);
            RunUI(() => editListDlg.SelectItem(ANNOTATION_NAME));
            RunDlg<DefineAnnotationDlg>(editListDlg.EditItem, defineAnnotationDlg =>
            {
                defineAnnotationDlg.AnnotationTargets = AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate);
                defineAnnotationDlg.OkDialog();
            });
            OkDialog(editListDlg, editListDlg.OkDialog);
            OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);
            WaitForConditionUI(() => documentGrid.IsComplete);

            column = documentGrid.FindColumn(annotationPropertyPath);
            Assert.IsNull(column);
        }
    }
}
