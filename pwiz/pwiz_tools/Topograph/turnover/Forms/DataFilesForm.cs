/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class DataFilesForm : WorkspaceForm
    {
        private readonly Dictionary<MsDataFile, DataGridViewRow> _dataFileRows 
            = new Dictionary<MsDataFile, DataGridViewRow>();

        public DataFilesForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            gridView.DataError += gridView_DataError;
        }

        private void Requery()
        {
            gridView.Rows.Clear();
            _dataFileRows.Clear();
            foreach (var row in AddRows(Workspace.MsDataFiles.ListChildren()))
            {
                UpdateRow(row);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Requery();
        }

        void gridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            Console.Out.WriteLine(e.Exception);
        }

        private IList<DataGridViewRow> AddRows(ICollection<MsDataFile> dataFiles)
        {
            List<DataGridViewRow> result = new List<DataGridViewRow>();
            foreach (var dataFile in dataFiles)
            {
                var row = new DataGridViewRow();
                row.Tag = dataFile;
                _dataFileRows.Add(dataFile, row);
                result.Add(row);
            }
            gridView.Rows.AddRange(result.ToArray());
            return result;
        }

        private DataGridViewRow AddRow(MsDataFile dataFile)
        {
            return AddRows(new[] {dataFile})[0];
        }

        private void UpdateRow(DataGridViewRow row)
        {
            var dataFile = (MsDataFile) row.Tag;
            row.Cells[colName.Name].Value = dataFile.Name;
            row.Cells[colLabel.Name].Value = dataFile.Label;
            row.Cells[colPath.Name].Value = dataFile.Path;
            row.Cells[colCohort.Name].Value = dataFile.Cohort;
            row.Cells[colTimePoint.Name].Value = dataFile.TimePoint;
            row.Cells[colStatus.Name].Value = dataFile.ValidationStatus;
        }

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            base.OnWorkspaceEntitiesChanged(args);
            foreach (var dataFile in args.GetEntities<MsDataFile>())
            {
                DataGridViewRow row;
                _dataFileRows.TryGetValue(dataFile, out row);
                if (args.IsRemoved(dataFile))
                {
                    if (row != null)
                    {
                        gridView.Rows.Remove(row);
                    }
                }
                else
                {
                    if (row == null)
                    {
                        row = AddRow(dataFile);
                    }
                    UpdateRow(row);
                }
            }
        }

        public static double? ToDouble(object value)
        {
            if (value == null)
            {
                return null;
            }
            try
            {
                return (double)Convert.ChangeType(value, typeof(double));
            }
            catch (Exception e)
            {
                Console.Out.WriteLine(e);
                return null;
            }
        }

        private void gridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            var row = gridView.Rows[e.RowIndex];
            var msDataFile = (MsDataFile) row.Tag;
            var column = gridView.Columns[e.ColumnIndex];
            var cell = row.Cells[e.ColumnIndex];
            if (column == colPath)
            {
                msDataFile.Path = Convert.ToString(cell.Value);
            }
            else if (column == colStatus)
            {
                msDataFile.ValidationStatus = (ValidationStatus) cell.Value;
            }
            else if (column == colCohort)
            {
                msDataFile.Cohort = Convert.ToString(cell.Value);
            }
            else if (column == colTimePoint)
            {
                msDataFile.TimePoint = ToDouble(cell.Value);
            }
            else if (column == colLabel)
            {
                msDataFile.Label = Convert.ToString(cell.Value);
            }
        }

        private void gridView_RowHeaderMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var dataFile = (MsDataFile) gridView.Rows[e.RowIndex].Tag;
            var dataFileFrame = Program.FindOpenEntityForm<DataFileSummary>(dataFile);
            if (dataFileFrame == null)
            {
                dataFileFrame = new DataFileSummary(dataFile);
                dataFileFrame.Show(DockPanel, DockState.Document);
            }
            else
            {
                dataFileFrame.Activate();
            }
        }
    }
}
