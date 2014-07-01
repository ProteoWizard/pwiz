//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using IDPicker.DataModel;
using IDPicker.Controls;
using NHibernate.Linq;

namespace IDPicker.Forms
{
    public partial class FilterHistoryForm : DockableForm, IPersistentForm
    {
        public FilterHistoryForm()
        {
            InitializeComponent();

            Text = TabText = "Filter History";
            Icon = Properties.Resources.BlankIcon;

            SetDefaults();

            dataGridView.DataError += dataGridView_DataError;
            dataGridView.CellDoubleClick += dataGridView_CellDoubleClick;
            dataGridView.CellFormatting += dataGridView_CellFormatting;
            dataGridView.PreviewKeyDown += dataGridView_PreviewKeyDown;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            dataGridView.ClearSelection();
        }

        public DataGridView DataGridView
        {
            get { return dataGridView; }
        }

        public IEnumerable<DataGridViewColumn> Columns
        {
            get { return dataGridView.Columns.Cast<DataGridViewColumn>(); }
        }

        protected Dictionary<DataGridViewColumn, ColumnProperty> _columnSettings;

        private NHibernate.ISession session;
        private DataFilter viewFilter;

        public event EventHandler<LoadPersistentDataFilterEventArgs> LoadPersistentDataFilter;

        public void SetData(NHibernate.ISession session, DataFilter viewFilter)
        {
            this.session = session;
            this.viewFilter = viewFilter;

            ClearData();
            dataGridView.Rows.Clear();

            foreach (var filter in session.Query<PersistentDataFilter>().OrderByDescending(o => o.Id).ToList())
            {
                dataGridView.Rows.Add(filter.Id,
                                      filter.MaximumQValue,
                                      filter.MinimumDistinctPeptides,
                                      filter.MinimumSpectra,
                                      filter.MinimumAdditionalPeptides,
                                      filter.GeneLevelFiltering,
                                      filter.DistinctMatchFormat,
                                      filter.MinimumSpectraPerDistinctMatch,
                                      filter.MinimumSpectraPerDistinctPeptide,
                                      filter.MaximumProteinGroupsPerPeptide,
                                      filter.TotalCounts.Clusters,
                                      filter.TotalCounts.ProteinGroups,
                                      filter.TotalCounts.Proteins,
                                      filter.TotalCounts.DistinctPeptides,
                                      filter.TotalCounts.DistinctMatches,
                                      filter.TotalCounts.FilteredSpectra,
                                      filter.TotalCounts.ProteinFDR * 100,
                                      filter.TotalCounts.PeptideFDR * 100,
                                      filter.TotalCounts.SpectrumFDR * 100);

                dataGridView.Rows[dataGridView.RowCount - 1].Tag = filter;
            }

            dataGridView.Rows[0].Tag = viewFilter;

            dataGridView.Enabled = true;
            dataGridView.Refresh();
        }

        public void ClearData()
        {
            dataGridView.Enabled = false;
        }

        public void ClearData(bool clearBasicFilter)
        {
            ClearData();
        }

        public void ClearSession()
        {
            ClearData();
            if (session != null && session.IsOpen)
            {
                dataGridView.Rows.Clear();

                session.Close();
                session.Dispose();
                session = null;
            }
        }

        void dataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0)
                return;

            // ignore clicks on the current filter
            if (ReferenceEquals(dataGridView.Rows[e.RowIndex].Tag, viewFilter))
                return;

            if (LoadPersistentDataFilter != null)
                LoadPersistentDataFilter(this, new LoadPersistentDataFilterEventArgs
                {
                    PersistentDataFilter = dataGridView.Rows[e.RowIndex].Tag as PersistentDataFilter
                });
        }

        void dataGridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            Program.HandleException(e.Exception);
            e.ThrowException = false;
        }

        void dataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            // the current filter is bolded
            if (ReferenceEquals(dataGridView.Rows[e.RowIndex].Tag, viewFilter))
                e.CellStyle.Font = new Font(e.CellStyle.Font, FontStyle.Bold);

            // if control is disabled, set ForeColor to gray as a visual hint
            if (!dataGridView.Enabled)
                e.CellStyle.ForeColor = SystemColors.GrayText;

            var column = dataGridView.Columns[e.ColumnIndex];
            ColumnProperty columnProperty;
            if (_columnSettings.TryGetValue(column, out columnProperty))
            {
                if (columnProperty.ForeColor.HasValue)
                    e.CellStyle.ForeColor = _columnSettings[column].ForeColor.Value;

                if (columnProperty.BackColor.HasValue)
                    e.CellStyle.BackColor = _columnSettings[column].BackColor.Value;
            }
        }

        void dataGridView_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                dataGridView.ClearSelection();
                return;
            }
        }

        private void SetDefaults()
        {
            _columnSettings = new Dictionary<DataGridViewColumn, ColumnProperty>()
            {
                { maxQValueColumn, new ColumnProperty {Type = typeof(float)}},
                { minPeptidesColumn, new ColumnProperty {Type = typeof(int)}},
                { minSpectraColumn, new ColumnProperty {Type = typeof(int)}},
                { minAdditionalPeptidesColumn, new ColumnProperty {Type = typeof(int)}},
                { geneLevelFilteringColumn, new ColumnProperty {Type = typeof(bool)}},
                { distinctMatchFormatColumn, new ColumnProperty {Type = typeof(string)}},
                { minSpectraPerMatchColumn, new ColumnProperty {Type = typeof(int)}},
                { minSpectraPerPeptideColumn, new ColumnProperty {Type = typeof(int)}},
                { maxProteinGroupsColumn, new ColumnProperty {Type = typeof(int)}},
                { clustersColumn, new ColumnProperty {Type = typeof(int)}},
                { proteinGroupsColumn, new ColumnProperty {Type = typeof(int)}},
                { proteinsColumn, new ColumnProperty {Type = typeof(int)}},
                { distinctPeptidesColumn, new ColumnProperty {Type = typeof(int)}},
                { distinctMatchesColumn, new ColumnProperty {Type = typeof(int)}},
                { filteredSpectraColumn, new ColumnProperty {Type = typeof(int)}},
                { proteinFdrColumn, new ColumnProperty {Type = typeof(float), Precision = 4}},
                { peptideFdrColumn, new ColumnProperty {Type = typeof(float), Precision = 4}},
                { spectrumFdrColumn, new ColumnProperty {Type = typeof(float), Precision = 4}},
            };

            foreach (var kvp in _columnSettings)
            {
                kvp.Value.Name = kvp.Key.Name;
                kvp.Value.Index = kvp.Key.Index;
                kvp.Value.DisplayIndex = kvp.Key.DisplayIndex;
            }
        }

        private void setColumnVisibility()
        {
            // restore display index
            foreach (var kvp in _columnSettings)
            {
                kvp.Key.DisplayIndex = kvp.Value.DisplayIndex;

                // restore column size

                // update number formats
                if (kvp.Value.Type == typeof(float) && kvp.Value.Precision.HasValue)
                    kvp.Key.DefaultCellStyle.Format = "f" + kvp.Value.Precision.Value.ToString();

                // if visibility is not forced, default to on
                kvp.Key.Visible = kvp.Value.Visible ?? true;
            }
        }

        public void LoadLayout(FormProperty formProperty)
        {
            if (formProperty == null)
                return;

            var columnlist = _columnSettings.Keys.ToList();
            var untouchedColumns = _columnSettings.Keys.ToList();

            foreach (var column in columnlist)
            {
                var columnProperty = formProperty.ColumnProperties.SingleOrDefault(x => x.Name == column.Name);

                if (columnProperty == null)
                    continue;

                _columnSettings[column] = columnProperty;

                untouchedColumns.Remove(column);
            }

            //Set unspecified columns to blend in better
            foreach (var column in untouchedColumns)
            {
                _columnSettings[column].Visible = null;
                _columnSettings[column].BackColor = formProperty.BackColor;
                _columnSettings[column].ForeColor = formProperty.ForeColor;
            }

            dataGridView.ForeColor = dataGridView.DefaultCellStyle.ForeColor = formProperty.ForeColor ?? SystemColors.WindowText;
            dataGridView.BackgroundColor = dataGridView.GridColor = dataGridView.DefaultCellStyle.BackColor = formProperty.BackColor ?? SystemColors.Window;

            setColumnVisibility();

            dataGridView.Refresh();
        }

        public FormProperty GetCurrentProperties(bool pivotToo)
        {
            var result = new FormProperty()
            {
                Name = this.Name,
                BackColor = dataGridView.DefaultCellStyle.BackColor,
                ForeColor = dataGridView.DefaultCellStyle.ForeColor,
                ColumnProperties = _columnSettings.Values.ToList()
            };

            foreach (var kvp in _columnSettings)
            {
                kvp.Value.Index = kvp.Key.Index;
                kvp.Value.DisplayIndex = kvp.Key.Index;
            }

            return result;
        }

        public virtual List<List<string>> GetFormTable(bool selected)
        {
            var exportTable = new List<List<string>>();
            IList<int> exportedRows, exportedColumns;

            if (selected && dataGridView.SelectedCells.Count > 0 && !dataGridView.AreAllCellsSelected(false))
            {
                var selectedRows = new Set<int>();
                var selectedColumns = new Map<int, int>(); // ordered by DisplayIndex

                foreach (DataGridViewCell cell in dataGridView.SelectedCells)
                {
                    selectedRows.Add(cell.RowIndex);
                    selectedColumns[cell.OwningColumn.DisplayIndex] = cell.ColumnIndex;
                }

                exportedRows = selectedRows.ToList();
                exportedColumns = selectedColumns.Values;
            }
            else
            {
                exportedRows = dataGridView.Rows.Cast<DataGridViewRow>().Select(o => o.Index).ToList();
                exportedColumns = dataGridView.GetVisibleColumnsInDisplayOrder().Select(o => o.Index).ToList();
            }

            // add column headers
            exportTable.Add(new List<string>());
            foreach (var columnIndex in exportedColumns)
                exportTable.Last().Add(dataGridView.Columns[columnIndex].HeaderText);

            foreach (int rowIndex in exportedRows)
            {
                var rowText = new List<string>();
                foreach (var columnIndex in exportedColumns)
                {
                    object value = dataGridView[columnIndex, rowIndex].Value ?? String.Empty;
                    rowText.Add(value.ToString());
                }

                exportTable.Add(rowText);
            }

            return exportTable;
        }

        protected void ExportTable(object sender, EventArgs e)
        {
            var selected = sender == copyToClipboardSelectedToolStripMenuItem ||
                           sender == exportSelectedCellsToFileToolStripMenuItem ||
                           sender == showInExcelSelectToolStripMenuItem;

            /*var progressWindow = new Form
            {
                Size = new Size(300, 60),
                Text = "Exporting...",
                StartPosition = FormStartPosition.CenterScreen,
                ControlBox = false
            };
            var progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Marquee
            };
            progressWindow.Controls.Add(progressBar);
            progressWindow.Show();

            var bg = new BackgroundWorker();
            bg.DoWork += (x, y) =>
            {
                y.Result = GetFormTable(selected);
            };
            bg.RunWorkerCompleted += (x, y) =>
            {
                if (y.Error != null) Program.HandleException(y.Error);
                progressWindow.Close();
                var tempTable = y.Result as List<List<string>>;
                if (sender == clipboardToolStripMenuItem ||
                    sender == copyToClipboardSelectedToolStripMenuItem)
                    TableExporter.CopyToClipboard(tempTable);
                else if (sender == fileToolStripMenuItem ||
                         sender == exportSelectedCellsToFileToolStripMenuItem)
                    TableExporter.ExportToFile(tempTable);
                else if (sender == showInExcelToolStripMenuItem ||
                         sender == showInExcelSelectToolStripMenuItem)
                {
                    var exportWrapper = new Dictionary<string, List<List<string>>> { { Name, tempTable } };
                    TableExporter.ShowInExcel(exportWrapper, false);
                }
            };
            bg.RunWorkerAsync();*/
        }

        protected void exportButton_Click(object sender, EventArgs e)
        {
            copyToClipboardSelectedToolStripMenuItem.Enabled =
                exportSelectedCellsToFileToolStripMenuItem.Enabled =
                showInExcelSelectToolStripMenuItem.Enabled = dataGridView.SelectedCells.Count > 0;

            exportMenu.Show(Cursor.Position);
        }

        private void displayOptionsButton_Click(object sender, EventArgs e)
        {
            using (var ccf = new ColumnControlForm())
            {
                ccf.ColumnProperties = _columnSettings.ToDictionary(o => o.Key.HeaderText, o => o.Value);

                if (dataGridView.DefaultCellStyle.ForeColor.ToArgb() != SystemColors.WindowText.ToArgb())
                    ccf.DefaultForeColor = dataGridView.DefaultCellStyle.ForeColor;
                if (dataGridView.DefaultCellStyle.BackColor.ToArgb() != SystemColors.Window.ToArgb())
                    ccf.DefaultBackColor = dataGridView.DefaultCellStyle.BackColor;

                if (ccf.ShowDialog() != DialogResult.OK)
                    return;

                // update column properties
                foreach (var kvp in ccf.ColumnProperties)
                    _columnSettings[Columns.Single(o => o.HeaderText == kvp.Key)] = kvp.Value;

                setColumnVisibility();

                // update default cell style
                dataGridView.ForeColor = dataGridView.DefaultCellStyle.ForeColor = ccf.DefaultForeColor ?? SystemColors.WindowText;
                dataGridView.BackgroundColor = dataGridView.GridColor = dataGridView.DefaultCellStyle.BackColor = ccf.DefaultBackColor ?? SystemColors.Window;

                dataGridView.Refresh();
            }
        }
    }

    public class LoadPersistentDataFilterEventArgs : EventArgs { public PersistentDataFilter PersistentDataFilter { get; set; } }
}
