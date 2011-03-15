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
using pwiz.Topograph.ui.Forms;

namespace pwiz.Topograph.ui.Controls
{
    public class ExcludedMzsGrid : DataGridView
    {
        private PeptideAnalysis _peptideAnalysis;
        private PeptideFileAnalysis _peptideFileAnalysis;

        public ExcludedMzsGrid()
        {
            AllowUserToAddRows = false;
            AllowUserToDeleteRows = false;
        }

        public DataGridViewTextBoxColumn MassColumn { get; private set; }
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
            if (Rows.Count != PeptideAnalysis.GetMassCount())
            {
                Rows.Clear();
                Rows.Add(PeptideAnalysis.GetMassCount());
                for (int iRow = 0; iRow < Rows.Count; iRow++)
                {
                    Rows[iRow].Tag = iRow;
                }
            }
            for (int iRow = 0; iRow < Rows.Count; iRow++)
            {
                var row = Rows[iRow];
                var iMass = (int) row.Tag;
                double massDifference = masses[iMass].Center - monoisotopicMass;

                var label = massDifference.ToString("0.#");
                if (label[0] != '-')
                {
                    label = "+" + label;
                }
                label = "M" + label;
                row.Cells[MassColumn.Index].Value = label;
                row.Cells[MassColumn.Index].ToolTipText = "Mass:" + masses[iMass];
                row.Cells[MassColumn.Index].Style.BackColor = TracerChromatogramForm.GetColor(iRow, Rows.Count);
                row.Cells[ExcludedColumn.Index].Value = ExcludedMzs.IsMassExcluded(iMass);
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
    }
}
