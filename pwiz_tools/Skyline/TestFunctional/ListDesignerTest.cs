/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Lists;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ListDesignerTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestListDesigner()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            Assert.AreEqual(0, SkylineWindow.Document.Settings.DataSettings.Lists.Count);
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var listDesigner = ShowDialog<ListDesigner>(documentSettingsDlg.AddList);
            RunUI(() =>
            {
                listDesigner.ListName = "Planets";
            });
            SetCellValue(listDesigner.ListPropertiesGrid, 0, 0, "Name");
            SetCellValue(listDesigner.ListPropertiesGrid, 1, 0, "Symbol");
            SetCellValue(listDesigner.ListPropertiesGrid, 2, 0, "Distance");
            SetCellValue(listDesigner.ListPropertiesGrid, 2, 1, ListPropertyType.GetAnnotationTypeName(AnnotationDef.AnnotationType.number));
            SetCellValue(listDesigner.ListPropertiesGrid, 3, 0, "Rings");
            SetCellValue(listDesigner.ListPropertiesGrid, 3, 1, ListPropertyType.GetAnnotationTypeName(AnnotationDef.AnnotationType.true_false));
            RunUI(() =>
            {
                listDesigner.ListPropertiesGrid.CurrentCell = listDesigner.ListPropertiesGrid.Rows[0].Cells[0]; listDesigner.IdProperty = "Name";
                listDesigner.IdProperty = "Name";
            });
            OkDialog(listDesigner, listDesigner.OkDialog);
            OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);

            var listDef = SkylineWindow.Document.Settings.DataSettings.Lists[0].ListDef;
            CollectionAssert.AreEqual(
                new[]
                {
                    AnnotationDef.AnnotationType.text, AnnotationDef.AnnotationType.text,
                    AnnotationDef.AnnotationType.number, AnnotationDef.AnnotationType.true_false
                },
                listDef.Properties.Select(p => p.Type).ToArray());

            RunUI(()=>SkylineWindow.ShowList("Planets"));
            var listGridForm = FindOpenForm<ListGridForm>();
            WaitForConditionUI(() => listGridForm.IsComplete);
            SetCellAddress(listGridForm.DataGridView, 0, 0);
            SetClipboardText(string.Join(TextUtil.SEPARATOR_TSV_STR, "Earth", "\u2641", 1.00000011, false));
            RunUI(listGridForm.DataGridView.SendPaste);
            SetCellAddress(listGridForm.DataGridView, 0, 0);
            WaitForConditionUI(() => listGridForm.IsComplete);
            var listData = SkylineWindow.Document.Settings.DataSettings.Lists.First();
            Assert.AreEqual(1, listData.RowCount);
            SetClipboardText(TextUtil.LineSeparate(
                string.Join(TextUtil.SEPARATOR_TSV_STR, "Mercury", "\u263F", 0.38709893, false),
                string.Join(TextUtil.SEPARATOR_TSV_STR, "Venus", "\u2640", 0.72333199, false),
                string.Join(TextUtil.SEPARATOR_TSV_STR, "Mars", "\u2642", 1.52366231, false),
                string.Join(TextUtil.SEPARATOR_TSV_STR, "Jupiter", "\u2643", 5.20336301, true),
                string.Join(TextUtil.SEPARATOR_TSV_STR, "Saturn", "\u2644", 9.53707032, true), 
                string.Join(TextUtil.SEPARATOR_TSV_STR, "Uranus", "\u2645", 19.19126393, true),
                string.Join(TextUtil.SEPARATOR_TSV_STR, "Neptune", "\u2646", 30.06896348, true),
                string.Join(TextUtil.SEPARATOR_TSV_STR, "Pluto", "\u2647", 39.482, false)
                ));
            SetCellAddress(listGridForm.DataGridView, 1, 0);
            RunUI(listGridForm.DataGridView.SendPaste);
            SetCellAddress(listGridForm.DataGridView, 0, 0);
            listData = SkylineWindow.Document.Settings.DataSettings.Lists.First();
            Assert.AreEqual(9, listData.RowCount);
            SetCellAddress(listGridForm.DataGridView, 8, 0);
            var alertDlg = ShowDialog<AlertDlg>(listGridForm.DataboundGridControl.NavBar.ViewContext.Delete);
            OkDialog(alertDlg, alertDlg.ClickOk);
            SetCellAddress(listGridForm.DataGridView, 0, 0);
            listData = SkylineWindow.Document.Settings.DataSettings.Lists.First();
            Assert.AreEqual(8, listData.RowCount);
            OkDialog(listGridForm, listGridForm.Close);
            AssertEx.Serializable(SkylineWindow.Document);
        }

        private void SetCellAddress(DataGridView grid, int irow, int icol)
        {
            RunUI(()=>grid.CurrentCell = grid.Rows[irow].Cells[icol]);
        }

        private void SetCellValue(DataGridView grid, int irow, int icol, object value)
        {
            SetCellAddress(grid, irow, icol);
            RunUI(()=>
            {
                SetCurrentCellValue(grid, value);
            });
        }

        private void SetCurrentCellValue(DataGridView grid, object value)
        {
            IDataGridViewEditingControl editingControl = null;
            DataGridViewEditingControlShowingEventHandler onEditingControlShowing =
                (sender, args) =>
                {
                    Assume.IsNull(editingControl);
                    editingControl = args.Control as IDataGridViewEditingControl;
                };
            try
            {
                grid.EditingControlShowing += onEditingControlShowing;
                grid.BeginEdit(true);
                if (null != editingControl)
                {
                    editingControl.EditingControlFormattedValue = value;
                }
                else
                {
                    grid.CurrentCell.Value = value;
                }
            }
            finally
            {
                grid.EditingControlShowing -= onEditingControlShowing;
            }
        }
    }
}
