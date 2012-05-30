/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    public partial class ResultsGridForm : DockableFormEx, IUpdatable
    {
        public static bool SynchronizeSelection
        {
            get { return Settings.Default.ResultsGridSynchSelection; }
            set { Settings.Default.ResultsGridSynchSelection = value; }
        }

        private int _resultsIndex;

        public ResultsGridForm(IDocumentUIContainer documentUiContainer, SequenceTree sequenceTree)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            resultsGrid.Init(documentUiContainer, sequenceTree);
        }

        public int ResultsIndex
        {
            get { return _resultsIndex; }
            set
            {
                if (_resultsIndex != value)
                {
                    _resultsIndex = value;

                    if (SynchronizeSelection)
                        resultsGrid.UpdateSelectedReplicate();
                }
            }
        }

        public void UpdateUI()
        {
            resultsGrid.UpdateGrid();
        }

        private void chooseColumnsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChooseColumns();
        }

        public void ChooseColumns()
        {
            CheckDisposed();
            using (var columnChooserForm = new ColumnChooser())
            {
                var columns = resultsGrid.GetAvailableColumns();
                var columnLabels = new List<String>();
                IList<bool> columnVisibles = new List<bool>();
                foreach (var column in columns)
                {
                    columnLabels.Add(column.HeaderText);
                    columnVisibles.Add(column.Visible);
                }
                columnChooserForm.SetColumns(columnLabels, columnVisibles);
                if (columnChooserForm.ShowDialog(this) == DialogResult.Cancel)
                {
                    return;
                }
                columnVisibles = columnChooserForm.GetCheckedList();
                for (int i = 0; i < columns.Count; i++)
                {
                    columns[i].Visible = columnVisibles[i];
                }
            }
        }

        private void synchronizeSelectionContextMenuItem_Click(object sender, EventArgs e)
        {
            SynchronizeSelection = synchronizeSelectionContextMenuItem.Checked;
        }

        private void contextMenuResultsGrid_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            synchronizeSelectionContextMenuItem.Checked = SynchronizeSelection;
        }

        /// <summary>
        /// Returns the grid on this form.  Used for testing.
        /// </summary>
        public ResultsGrid ResultsGrid { get { return resultsGrid;}}
    }
}
