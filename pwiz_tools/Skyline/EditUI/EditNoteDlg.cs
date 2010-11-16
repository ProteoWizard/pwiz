/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.EditUI
{
    public partial class EditNoteDlg : Form
    {
        public EditNoteDlg()
        {
            InitializeComponent();

            Icon = Resources.Skyline;
        }

        private int _colorIndex;       

        public void Init(SrmDocument document, AnnotationDef.AnnotationTarget annotationTarget, Annotations annotations)
        {
            textNote.Text = annotations.Note;
            foreach (var annotationDef in document.Settings.DataSettings.AnnotationDefs)
            {
                if (0 == (annotationDef.AnnotationTargets & annotationTarget))
                {
                    continue;
                }
                var row = dataGridView1.Rows[dataGridView1.Rows.Add()];
                row.Cells[colName.Index].Value = annotationDef.Name;
                var value = annotations.GetAnnotation(annotationDef.Name);
                if (annotationDef.Type == AnnotationDef.AnnotationType.true_false)
                {
                    row.Cells[colValue.Index] = new DataGridViewCheckBoxCell {Value = value != null};
                }
                else if (annotationDef.Type == AnnotationDef.AnnotationType.value_list)
                {
                    var cell = new DataGridViewComboBoxCell();
                    row.Cells[colValue.Index] = cell;
                    cell.Items.Add("");
                    foreach (var item in annotationDef.Items)
                    {
                        cell.Items.Add(item);
                    }
                    cell.Value = value;
                }
                else
                {
                    row.Cells[colValue.Index].Value = value;
                }
            }
            if (dataGridView1.Rows.Count == 0)
            {
                splitContainer1.Panel2Collapsed = true;
            }

            _colorIndex = annotations.IsEmpty ? Settings.Default.AnnotationColor : annotations.ColorIndex;
            ((ToolStripButton) toolStrip1.Items[_colorIndex]).Checked = true;
        }


        public Annotations GetAnnotations()
        {
            var annotations = new Dictionary<string, string>();
            for (int iRow = 0; iRow < dataGridView1.Rows.Count; iRow++)
            {
                var row = dataGridView1.Rows[iRow];
                var name = (string) row.Cells[colName.Index].Value;
                var objValue = row.Cells[colValue.Index].Value;
                string strValue;
                if (true.Equals(objValue))
                {
                    strValue = name;
                }
                else if (false.Equals(objValue))
                {
                    strValue = "";
                }
                else
                {
                    strValue = (objValue ?? "").ToString();
                }
                if (strValue == "")
                {
                    continue;
                }
                annotations[name] = strValue;
            }
            var text = textNote.Text;
            if (text == null && annotations.Count > 0 && _colorIndex != 0)
                text = "";
            return new Annotations(text, annotations, _colorIndex);
        }
        
        /// <summary>
        /// Returns the grid view on this form.  Used for testing.
        /// </summary>
        public DataGridView DataGridView { get { return dataGridView1; } }

        private void btnColor_Click(object sender, System.EventArgs e)
        {
            btnOrangeRed.Checked = btnRed.Checked = btnOrange.Checked = btnYellow.Checked = btnLightGreen.Checked =
                btnGreen.Checked = btnLightBlue.Checked = btnBlue.Checked = btnPurple.Checked = btnBlack.Checked = false;
            ((ToolStripButton) sender).Checked = true;
            _colorIndex = toolStrip1.Items.IndexOf((ToolStripButton) sender);
        }

        private void btnColor_Paint(object sender, PaintEventArgs e)
        {
            var rectangle = e.ClipRectangle;
            var colorIndex = toolStrip1.Items.IndexOf((ToolStripButton) sender);
            e.Graphics.FillRectangle(Annotations.COLOR_BRUSHES[colorIndex], rectangle.X + 5, rectangle.Y + 5, rectangle.Width - 10, rectangle.Height - 10);
        }
    }
}
