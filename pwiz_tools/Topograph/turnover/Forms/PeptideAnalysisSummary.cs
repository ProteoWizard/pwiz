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
using NHibernate.Criterion;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PeptideAnalysisSummary : EntityModelForm
    {
        private readonly Dictionary<PeptideFileAnalysis, DataGridViewRow> peptideAnalysisRows 
            = new Dictionary<PeptideFileAnalysis, DataGridViewRow>();
        public PeptideAnalysisSummary(PeptideAnalysis peptideAnalysis) : base(peptideAnalysis)
        {
            InitializeComponent();
            colPeakStart.DefaultCellStyle.Format = "0.##";
            colPeakEnd.DefaultCellStyle.Format = "0.##";
            colScore.DefaultCellStyle.Format = "0.####";
            colTracerPercent.DefaultCellStyle.Format = "0.##%";
            gridViewExcludedMzs.PeptideAnalysis = peptideAnalysis;
            tbxSequence.Text = peptideAnalysis.Peptide.Sequence;
            TabText = "Summary";
        }

        public PeptideAnalysis PeptideAnalysis { get { return (PeptideAnalysis) EntityModel; } }
        public Peptide Peptide { get { return PeptideAnalysis.Peptide;} }

        private void btnCreateAnalyses_Click(object sender, EventArgs e)
        {
            dataGridView.Rows.Clear();
            peptideAnalysisRows.Clear();
            foreach (var msDataFile in Workspace.GetMsDataFiles())
            {
                if (!msDataFile.HasTimes())
                {
                    if (!msDataFile.HasSearchResults(Peptide))
                    {
                        continue;
                    }
                    if (!TurnoverForm.Instance.EnsureMsDataFile(msDataFile))
                    {
                        continue;
                    }
                }
                PeptideFileAnalysis peptideFileAnalysis = PeptideFileAnalysis.EnsurePeptideFileAnalysis(PeptideAnalysis, msDataFile);
                if (peptideFileAnalysis != null)
                {
                    UpdateRow(AddRow(peptideFileAnalysis));
                }
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            foreach (PeptideFileAnalysis peptideFileAnalysis in PeptideAnalysis.GetFileAnalyses(false))
            {
                UpdateRow(AddRow(peptideFileAnalysis));
            }
            OnPeptideAnalysisChanged();
        }

        private DataGridViewRow AddRow(PeptideFileAnalysis peptideFileAnalysis)
        {
            var row = dataGridView.Rows[dataGridView.Rows.Add()];
            row.Tag = peptideFileAnalysis;
            peptideAnalysisRows.Add(peptideFileAnalysis, row);
            return row;
        }

        private void Remove(PeptideFileAnalysis peptideFileAnalysis)
        {
            DataGridViewRow row;
            if (!peptideAnalysisRows.TryGetValue(peptideFileAnalysis, out row))
            {
                return;
            }
            dataGridView.Rows.Remove(row);
            peptideAnalysisRows.Remove(peptideFileAnalysis);
        }

        private void UpdateRow(DataGridViewRow row)
        {
            var peptideFileAnalysis = (PeptideFileAnalysis) row.Tag;
            row.Cells[colStatus.Index].Value = peptideFileAnalysis.ValidationStatus;
            row.Cells[colTimePoint.Name].Value = peptideFileAnalysis.MsDataFile.TimePoint;
            row.Cells[colCohort.Name].Value = peptideFileAnalysis.MsDataFile.Cohort;
            row.Cells[colDataFileLabel.Name].Value = peptideFileAnalysis.MsDataFile.Label;
            row.Cells[colPeakStart.Name].Value = peptideFileAnalysis.PeakStartTime;
            row.Cells[colPeakEnd.Name].Value = peptideFileAnalysis.PeakEndTime;
            var peptideDistribution =
                peptideFileAnalysis.PeptideDistributions.GetChild(Workspace.GetDefaultPeptideQuantity());
            if (peptideDistribution != null)
            {
                row.Cells[colTracerPercent.Index].Value = peptideDistribution.TracerPercent / 100;
                row.Cells[colScore.Index].Value = peptideDistribution.Score;
            }
            else
            {
                row.Cells[colTracerPercent.Index].Value = null;
                row.Cells[colScore.Index].Value = null;
            }
        }

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            base.OnWorkspaceEntitiesChanged(args);
            if (args.GetEntities<MsDataFile>().Count > 0)
            {
                UpdateRows(peptideAnalysisRows.Keys);
            }
            else
            {
                var peptideFileAnalyses = new HashSet<PeptideFileAnalysis>(args.GetEntities<PeptideFileAnalysis>());
                foreach (var peptideDistribution in args.GetEntities<PeptideDistribution>())
                {
                    peptideFileAnalyses.Add(peptideDistribution.PeptideFileAnalysis);
                }
                UpdateRows(peptideFileAnalyses);
            }
        }
        private void UpdateRows(ICollection<PeptideFileAnalysis> peptideAnalyses)
        {
            foreach (var peptideAnalysis in peptideAnalyses)
            {
                DataGridViewRow row;
                if (!peptideAnalysisRows.TryGetValue(peptideAnalysis, out row))
                {
                    continue;
                }
                UpdateRow(row);
            }
        }

        private void dataGridView_RowHeaderMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var row = dataGridView.Rows[e.RowIndex];
            var peptideAnalysis = (PeptideFileAnalysis) row.Tag;
            PeptideFileAnalysisFrame.ActivatePeptideDataForm<ChromatogramForm>(this, peptideAnalysis);
        }

        protected void OnPeptideAnalysisChanged()
        {
            var res = Workspace.GetAminoAcidFormulas();
            tbxFormula.Text = res.GetFormula(Peptide.Sequence).ToString();
            tbxMonoMass.Text = Peptide.GetChargedPeptide(1).GetMonoisotopicMass(res).ToString("0.####");
            tbxAvgMass.Text = Peptide.GetChargedPeptide(1).GetMassDistribution(res).AverageMass.ToString("0.####");
            tbxMinCharge.Text = PeptideAnalysis.MinCharge.ToString();
            tbxMaxCharge.Text = PeptideAnalysis.MaxCharge.ToString();
            tbxIntermediateLevels.Text = PeptideAnalysis.IntermediateLevels.ToString();
            tbxProtein.Text = Peptide.ProteinName + " " + Peptide.ProteinDescription;
            UpdateMassGrid();
            UpdateRows(peptideAnalysisRows.Keys);
        }

        protected override void EntityChanged(EntityModelChangeEventArgs args)
        {
            base.EntityChanged(args);
            OnPeptideAnalysisChanged();
        }

        private void tbxMinCharge_Leave(object sender, EventArgs e)
        {
            PeptideAnalysis.MinCharge = Convert.ToInt32(tbxMinCharge.Text);
        }

        private void tbxMaxCharge_TextChanged(object sender, EventArgs e)
        {
            PeptideAnalysis.MaxCharge = Convert.ToInt32(tbxMaxCharge.Text);
        }

        private void dataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            var row = dataGridView.Rows[e.RowIndex];
            var peptideAnalysis = (PeptideFileAnalysis) row.Tag;
            var column = dataGridView.Columns[e.ColumnIndex];
            var cell = row.Cells[e.ColumnIndex];
            if (column == colCohort)
            {
                peptideAnalysis.MsDataFile.Cohort = Convert.ToString(cell.Value);
            }
            else if (column == colTimePoint)
            {
                peptideAnalysis.MsDataFile.TimePoint = DataFilesForm.ToDouble(cell.Value);
            }
            else if (column == colDataFileLabel)
            {
                peptideAnalysis.MsDataFile.Label = Convert.ToString(cell.Value);
            }
            else if (column == colStatus)
            {
                peptideAnalysis.ValidationStatus = (ValidationStatus) cell.Value;
            }
    }

        private void tbxIntermediateLevels_Leave(object sender, EventArgs e)
        {
            PeptideAnalysis.IntermediateLevels = Convert.ToInt32(tbxIntermediateLevels.Text);
        }

        private void UpdateMassGrid()
        {
            gridViewExcludedMzs.UpdateGrid();
        }

        private void btnShowGraph_Click(object sender, EventArgs e)
        {
            var halfLifeForm = new HalfLifeForm(Workspace)
                                   {
                                       Peptide = Peptide.Sequence,
                                       ProteinName = Peptide.ProteinName,
                                   };
            halfLifeForm.Show(DockPanel, DockState);
        }

        private void dataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0)
            {
                return;
            }
            var row = dataGridView.Rows[e.RowIndex];
            var peptideAnalysis = (PeptideFileAnalysis)row.Tag;
            var column = dataGridView.Columns[e.ColumnIndex];
            if (e.ColumnIndex >= 0)
            {
                column = dataGridView.Columns[e.ColumnIndex];
            }
            if (column == colPeakStart || column == colPeakEnd)
            {
                PeptideFileAnalysisFrame.ActivatePeptideDataForm<ChromatogramForm>(this, peptideAnalysis);
            }
            else if (column == colTracerPercent || column == colScore)
            {
                if (Workspace.GetDefaultPeptideQuantity() == PeptideQuantity.tracer_count)
                {
                    PeptideFileAnalysisFrame.ActivatePeptideDataForm<TracerAmountsForm>(this, peptideAnalysis);
                }
                else
                {
                    PeptideFileAnalysisFrame.ActivatePeptideDataForm<PrecursorEnrichmentsForm>(this, peptideAnalysis);
                }
            }
        }
    }
}
