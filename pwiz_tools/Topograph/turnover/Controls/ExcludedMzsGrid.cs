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
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Controls
{
    public class ExcludedMzsGrid : DataGridView
    {
        private PeptideAnalysis _peptideAnalysis;
        private PeptideFileAnalysis _peptideFileAnalysis;

        private readonly Dictionary<int, DataGridViewTextBoxColumn> _intensityColumns
            = new Dictionary<int, DataGridViewTextBoxColumn>();

        private readonly Dictionary<int, DataGridViewRow> _massRows 
            = new Dictionary<int, DataGridViewRow>();

        public ExcludedMzsGrid()
        {
            AllowUserToAddRows = false;
            AllowUserToDeleteRows = false;
        }

        public DataGridViewTextBoxColumn MassColumn { get; private set; }
        public DataGridViewTextBoxColumn IntensityColumn { get; private set; }
        public DataGridViewCheckBoxColumn ExcludedColumn { get; private set; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (Workspace != null)
            {
                Workspace.EntitiesChange += Workspace_EntitiesChangedEvent;
                UpdateGrid();
            }
        }

        void Workspace_EntitiesChangedEvent(EntitiesChangedEventArgs args)
        {
            if (IsDisposed)
            {
                return;
            }
            bool changed = false;
            changed = changed || args.Contains(PeptideAnalysis);
            changed = changed || PeptideFileAnalysis != null && args.Contains(PeptideFileAnalysis);

            if (!changed && PeptideFileAnalysis == null)
            {
                foreach (var peptideFileAnalysis in args.GetEntities<PeptideFileAnalysis>())
                {
                    if (PeptideAnalysis.GetFileAnalysis(peptideFileAnalysis.Id.Value) != null)
                    {
                        changed = true;
                        break;
                    }
                }
            }
            if (!changed)
            {
                return;
            }
            UpdateGrid();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            if (Workspace != null)
            {
                Workspace.EntitiesChange -= Workspace_EntitiesChangedEvent;    
            }
        }

        protected override void OnCellEndEdit(DataGridViewCellEventArgs e)
        {
            base.OnCellEndEdit(e);
            var row = Rows[e.RowIndex];
            var cell = row.Cells[e.ColumnIndex];
            using (Workspace.GetWriteLock())
            {
                ExcludedMzs.SetExcluded((int)row.Tag, (bool)cell.Value);
            }
        }

        protected override void OnCellValueChanged(DataGridViewCellEventArgs e)
        {
            base.OnCellValueChanged(e);
            EndEdit();
        }

        protected override void OnCellBeginEdit(DataGridViewCellCancelEventArgs e)
        {
            base.OnCellBeginEdit(e);
            EndEdit();
        }

        protected override void OnColumnHeaderMouseClick(DataGridViewCellMouseEventArgs e)
        {
            base.OnColumnHeaderMouseClick(e);
            var col = Columns[e.ColumnIndex];
            if (col.Selected)
            {
                col.Selected = false;
            }
            else if (col is DataGridViewCheckBoxColumn)
            {
                col.Selected = true;
            }
        }

        public Workspace Workspace
        {
            get
            {
                if (PeptideAnalysis == null)
                {
                    return null;
                }
                return PeptideAnalysis.Workspace;
            }
        }

        public PeptideAnalysis PeptideAnalysis
        {
            get 
            { 
                return _peptideAnalysis;
            } 
            set 
            { 
                _peptideAnalysis = value;
            }
        }
        public PeptideFileAnalysis PeptideFileAnalysis
        {
            get 
            { 
                return _peptideFileAnalysis;
            }
            set
            {
                _peptideFileAnalysis = value;
                if (_peptideFileAnalysis != null)
                {
                    _peptideAnalysis = PeptideFileAnalysis.PeptideAnalysis;
                }
            }
        }
        public ExcludedMzs ExcludedMzs
        {
            get
            {
                if (PeptideFileAnalysis != null)
                {
                    return PeptideFileAnalysis.ExcludedMzs;
                }
                return PeptideAnalysis.ExcludedMzs;
            }
        }
        public void UpdateGrid()
        {
            double monoisotopicMass =
                Workspace.GetAminoAcidFormulas().GetMonoisotopicMass(PeptideAnalysis.Peptide.Sequence);
            var masses = PeptideAnalysis.GetTurnoverCalculator().GetMzs(0);
            if (MassColumn == null)
            {
                MassColumn = new DataGridViewTextBoxColumn
                {
                    HeaderText = "Mass",
                    Name = "colMass",
                    Width = 60,
                    ReadOnly = true,
                };
                Columns.Add(MassColumn);
            }
            if (IntensityColumn == null)
            {
                IntensityColumn = new DataGridViewTextBoxColumn
                {
                    HeaderText = "Intensity",
                    Name = "colIntensity",
                    Width = 50,
                    ReadOnly = true
                };
                IntensityColumn.DefaultCellStyle.Format = "0.#";
                Columns.Add(IntensityColumn);
            }
            if (ExcludedColumn == null)
            {
                ExcludedColumn = new DataGridViewCheckBoxColumn
                                     {
                                         HeaderText = "Excluded",
                                         Name = "colExcluded",
                                         Width = 50,
                                         SortMode = DataGridViewColumnSortMode.NotSortable,
                                     };
                Columns.Add(ExcludedColumn);
            }
            foreach (var entry in _intensityColumns.ToArray())
            {
                if (entry.Key >= PeptideAnalysis.MinCharge && entry.Key <= PeptideAnalysis.MaxCharge)
                {
                    continue;
                }
                Columns.Remove(entry.Value);
                _intensityColumns.Remove(entry.Key);
            }
            for (int charge = PeptideAnalysis.MinCharge; charge <= PeptideAnalysis.MaxCharge; charge ++)
            {
                if (!_intensityColumns.ContainsKey(charge))
                {
                    var column = new DataGridViewTextBoxColumn
                                     {
                                         HeaderText = "Int+" + charge,
                                         Name = "colIntensity" + charge,
                                         Width = 40,
                                         SortMode = DataGridViewColumnSortMode.NotSortable,
                                         ReadOnly = true,
                                         Tag = charge,
                                         DefaultCellStyle = {Format = "0.#"},
                                     };
                    DataGridViewTextBoxColumn nextColumn;
                    if (_intensityColumns.TryGetValue(charge + 1, out nextColumn))
                    {
                        Columns.Insert(nextColumn.Index, column);
                    }
                    else
                    {
                        Columns.Add(column);
                    }
                    _intensityColumns.Add(charge, column);
                }
            }
            if (Rows.Count != PeptideAnalysis.GetMassCount())
            {
                Rows.Clear();
                Rows.Add(PeptideAnalysis.GetMassCount());
                for (int iRow = 0; iRow < Rows.Count; iRow++)
                {
                    Rows[iRow].Tag = iRow;
                }
            }
            var intensitiesList = new List<IList<double>>();
            if (PeptideFileAnalysis != null)
            {
                intensitiesList.Add(PeptideFileAnalysis.GetAverageIntensities());
            }
            else
            {
                foreach (var peptideFileAnalysis in PeptideAnalysis.GetFileAnalyses(true))
                {
                    if (peptideFileAnalysis.OverrideExcludedMzs)
                    {
                        continue;
                    }
                    intensitiesList.Add(peptideFileAnalysis.GetAverageIntensities());
                }
            }
            var mzs = PeptideAnalysis.GetMzs();
            Dictionary<int, IList<double>> scaledIntensities = null;
            IList<double> averageIntensities = null;
            if (PeptideFileAnalysis != null)
            {
                scaledIntensities = PeptideFileAnalysis.GetScaledIntensities();
                averageIntensities = PeptideFileAnalysis.GetAverageIntensities();
            }

            for (int iRow = 0; iRow < Rows.Count; iRow++)
            {
                var row = Rows[iRow];
                var iMass = (int) row.Tag;
                foreach (var entry in _intensityColumns)
                {
                    var cell = row.Cells[entry.Value.Index];
                    var mzKey = new MzKey(entry.Key, iMass);
                    if (scaledIntensities != null)
                    {
                        var mzRange = mzs[mzKey.Charge][mzKey.MassIndex];
                        entry.Value.Visible = true;
                        cell.Value = scaledIntensities[mzKey.Charge][mzKey.MassIndex];
                        cell.ToolTipText = "M/Z:" + mzRange + "\nIntensity:" +
                                           scaledIntensities[mzKey.Charge][mzKey.MassIndex];
                    }
                    else
                    {
                        entry.Value.Visible = false;
                    }
                    cell.Style.BackColor = GetColor(mzKey);
                }
                double massDifference = masses[iMass].Center - monoisotopicMass;

                var label = massDifference.ToString("0.#");
                if (label[0] != '-')
                {
                    label = "+" + label;
                }
                label = "M" + label;
                row.Cells[MassColumn.Index].Value = label;
                row.Cells[MassColumn.Index].ToolTipText = "Mass:" + masses[iMass];
                row.Cells[ExcludedColumn.Index].Value = ExcludedMzs.IsMassExcluded(iMass);
                double totalIntensity = 0;
                var intensityCount = 0;
                foreach (var intensities in intensitiesList)
                {
                    if (double.IsNaN(intensities[iMass]))
                    {
                        continue;
                    }
                    totalIntensity += intensities[iMass];
                    intensityCount++;
                }

                if (intensityCount == 0)
                {
                    row.Cells[IntensityColumn.Index].Value = null;
                }
                else
                {
                    row.Cells[IntensityColumn.Index].Value = totalIntensity / intensityCount;
                }
            }
        }

        public ICollection<int> GetSelectedCharges()
        {
            var result = new HashSet<int>();
            if (SelectedColumns.Count != 0)
            {
                foreach (DataGridViewColumn column in SelectedColumns)
                {
                    if (!(column.Tag is int))
                    {
                        continue;
                    }
                    result.Add((int)column.Tag);
                }
            }
            else if (CurrentCell != null)
            {
                var selectedColumn = Columns[CurrentCell.ColumnIndex];
                if (selectedColumn.Tag is int)
                {
                    result.Add((int) selectedColumn.Tag);
                }
            }
            return result;
        }
        public ICollection<int> GetSelectedMasses()
        {
            var result = new HashSet<int>();
            if (Rows.Count == 0)
            {
                return result;
            }
            if (SelectedRows.Count != 0)
            {
                foreach (DataGridViewRow row in SelectedRows)
                {
                    result.Add((int) row.Tag);
                }
            }
            else if (CurrentCell != null)
            {
                var selectedColumn = Columns[CurrentCell.ColumnIndex];
                if (selectedColumn.Tag is int)
                {
                    result.Add((int) Rows[CurrentCell.RowIndex].Tag);
                }
            }
            return result;
        }
        public Color GetColor(MzKey mzKey)
        {
            int cCharge = PeptideAnalysis.MaxCharge - PeptideAnalysis.MinCharge;
            int iCharge = mzKey.Charge - PeptideAnalysis.MinCharge;
            int red = cCharge == 0 ? 0 : 128*iCharge/cCharge;
            int c = PeptideAnalysis.GetMassCount() - 1;
            if (c == 0)
            {
                return Color.FromArgb(red, 0, 255);
            }
            return Color.FromArgb(red, 255*mzKey.MassIndex/c, 255*(c - mzKey.MassIndex)/c);
        }
    }
}
