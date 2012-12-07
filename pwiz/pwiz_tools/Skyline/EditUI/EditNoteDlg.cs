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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class EditNoteDlg : FormEx
    {
        public EditNoteDlg()
        {
            InitializeComponent();

            Icon = Resources.Skyline;
        }

        public int ColorIndex { get; set; }
        private Annotations _originalAnnotations;
        private string _originalText;

        public void Init(SrmDocument document, IList<IdentityPath> selPaths)
        {
            AnnotationDef.AnnotationTargetSet annotationTarget;
            var annotations = MergeAnnotations(document, selPaths, out annotationTarget);

            textNote.Text = _originalText = annotations.Note ?? string.Empty;
            _originalAnnotations = annotations;

            foreach (var annotationDef in document.Settings.DataSettings.AnnotationDefs)
            {
                // Definition must apply to all targets.
                if (!annotationTarget.Intersect(annotationDef.AnnotationTargets).Equals(annotationTarget))
                {
                    continue;
                }
                AddAnnotationToGridView(annotationDef, annotations);
            }
            if (dataGridView1.Rows.Count == 0)
            {
                splitContainer1.Panel2Collapsed = true;
            }

            ColorIndex = annotations.ColorIndex;
            if (ColorIndex >= 0 && ColorIndex < toolStrip1.Items.Count)
                ((ToolStripButton) toolStrip1.Items[ColorIndex]).Checked = true;

            ClearAll = false;
        }

        public void AddAnnotationToGridView(AnnotationDef annotationDef, Annotations annotations)
        {
            var row = dataGridView1.Rows[dataGridView1.Rows.Add()];
            row.Tag = annotationDef;
            row.Cells[colName.Index].Value = annotationDef.Name;
            var value = annotations.GetAnnotation(annotationDef);
            if (annotationDef.Type == AnnotationDef.AnnotationType.true_false)
            {
                row.Cells[colValue.Index] = new DataGridViewCheckBoxCell {Value = value};
            }
            else if (annotationDef.Type == AnnotationDef.AnnotationType.value_list)
            {
                var cell = new DataGridViewComboBoxCell();
                row.Cells[colValue.Index] = cell;
                cell.Items.Add(string.Empty);
                foreach (var item in annotationDef.Items)
                {
                    cell.Items.Add(item);
                }
                cell.Value = value;
            }
            else
            {
                var cell = row.Cells[colValue.Index];
                if (annotationDef.Type == AnnotationDef.AnnotationType.number)
                {
                    cell.ValueType = typeof (double);
                }
                cell.Value = value;
            }
        }

        public static Annotations MergeAnnotations(SrmDocument document, IEnumerable<IdentityPath> selPaths, 
            out AnnotationDef.AnnotationTargetSet annotationTarget)
        {
            annotationTarget = AnnotationDef.AnnotationTargetSet.EMPTY;

            // If the nodes have matching text, colors, or annotations, then we should display these values
            // in the EditNodeDlg. Otherwise, we should not display any value for that variable.
            bool matchingText = true;
            bool matchingColors = true;
            bool isFirstSelNode = true;
            bool allEmpty = true;

            // These are the default values we want for the annotations, if the node(s) do not already have
            // annotations, or if the annotations do not match.
            string text = null;
            int colorIndex = -1;
            var dictMatchingAnnotations = new Dictionary<string, string>();

            // Find what all nodes have in common as far as note, annotations, and color.
            foreach (IdentityPath selPath in selPaths)
            {
                if(Equals(selPath.Child, SequenceTree.NODE_INSERT_ID))
                    continue;

                var nodeDoc = document.FindNode(selPath);
                var nodeAnnotations = nodeDoc.Annotations;
                var dictNodeAnnotations = nodeAnnotations.ListAnnotations()
                    .ToDictionary(nodeAnnotation => nodeAnnotation.Key,
                                  nodeAnnotation => nodeAnnotation.Value);

                // If this is the first iteration, use the value for this node to start matching.
                if (isFirstSelNode)
                {
                    foreach (KeyValuePair<string, string> annotation in dictNodeAnnotations)
                    {
                        dictMatchingAnnotations.Add(annotation.Key, annotation.Value);
                    }
                    text = nodeAnnotations.Note;
                    colorIndex = nodeAnnotations.ColorIndex;
                    isFirstSelNode = false;
                }
                foreach (string key in dictMatchingAnnotations.Keys.ToArray())
                {
                    string value;
                    // If the list of annotations we are building for the dialog contains this key,
                    // check that the values are the same, otherwise the value for this annotation needs
                    // to be null for the dialog.
                    if (!dictNodeAnnotations.TryGetValue(key, out value) || !Equals(dictMatchingAnnotations[key], value))
                        dictMatchingAnnotations.Remove(key);
                }
                
                matchingText = matchingText && nodeAnnotations.Note != null && Equals(text, nodeAnnotations.Note);
                matchingColors = matchingColors 
                    && !nodeAnnotations.IsEmpty
                    && nodeAnnotations.ColorIndex != -1 
                    && Equals(colorIndex, nodeAnnotations.ColorIndex);

                allEmpty = allEmpty && nodeAnnotations.IsEmpty;

                // Update annotation target to include this type of node.
                annotationTarget = annotationTarget.Union(nodeDoc.AnnotationTarget);
            }
            if (!matchingText)
                text = string.Empty;
            if (allEmpty)
                colorIndex = Settings.Default.AnnotationColor;
            else if (!matchingColors)
                colorIndex = -1;
            return new Annotations(text, dictMatchingAnnotations, colorIndex);
        }


        public IList<KeyValuePair<string, string>> GetChangedAnnotations()
        {
            var annotations = GetAnnotations();
            // Only want to return new annotations if the user has changed the annotations.
            if(ArrayUtil.EqualsDeep(_originalAnnotations.ListAnnotations(), annotations.ToArrayStd()))
                return null;
            return annotations;
        }

        public IList<KeyValuePair<string, string>> GetAnnotations()
        {
            IList<KeyValuePair<string, string>> annotations = new List<KeyValuePair<string, string>>();
            for (int iRow = 0; iRow < dataGridView1.Rows.Count; iRow++)
            {
                var row = dataGridView1.Rows[iRow];
                var name = (string)row.Cells[colName.Index].Value;
                var objValue = row.Cells[colValue.Index].Value;
                string strValue;
                if (true.Equals(objValue))
                {
                    strValue = name;
                }
                else if (false.Equals(objValue))
                {
                    strValue = string.Empty;
                }
                else
                {
                    strValue = (objValue ?? string.Empty).ToString();
                }
                if (strValue == string.Empty && string.IsNullOrEmpty(_originalAnnotations.GetAnnotation(name)))
                {
                    continue;
                }
                annotations.Add(new KeyValuePair<string, string>(name, strValue));
            }
            return annotations;
        }
        
        public string NoteText
        {
            get { return textNote.Text; }
            set { textNote.Text = value; }
        }

        public string GetText()
        {
            // Only want to return new text if the user has changed the text.
            return Equals(_originalText, textNote.Text) ? null : textNote.Text;
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
            ColorIndex = toolStrip1.Items.IndexOf((ToolStripButton) sender);
        }

        private void btnColor_Paint(object sender, PaintEventArgs e)
        {
            var rectangle = e.ClipRectangle;
            var colorIndex = toolStrip1.Items.IndexOf((ToolStripButton) sender);
            e.Graphics.FillRectangle(Annotations.COLOR_BRUSHES[colorIndex], rectangle.X + 5, rectangle.Y + 5, rectangle.Width - 10, rectangle.Height - 10);
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        private void btnClearAll_Click(object sender, System.EventArgs e)
        {
            ClearAll = true;
            OkDialog();
        }

        public bool ClearAll { get; set; }

        private void dataGridView1_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (e.ColumnIndex == colValue.Index)
            {
                var row = DataGridView.Rows[e.RowIndex];
                var annotationDef = (AnnotationDef) row.Tag;
                MessageDlg.Show(this, annotationDef.ValidationErrorMessage);
                return;
            }
            e.ThrowException = true;
        }
    }
}
