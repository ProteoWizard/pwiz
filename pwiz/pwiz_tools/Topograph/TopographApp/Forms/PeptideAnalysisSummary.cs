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
        private readonly Dictionary<PeptideFileAnalysis, DataGridViewRow> _peptideFileAnalysisRows 
            = new Dictionary<PeptideFileAnalysis, DataGridViewRow>();

        private int? _originalMinCharge;
        private int? _originalMaxCharge;
        public PeptideAnalysisSummary(PeptideAnalysis peptideAnalysis) : base(peptideAnalysis)
        {
            InitializeComponent();
            colPeakStart.DefaultCellStyle.Format = "0.##";
            colPeakEnd.DefaultCellStyle.Format = "0.##";
            colScore.DefaultCellStyle.Format = "0.####";
            colTracerPercent.DefaultCellStyle.Format = "0.##%";
            colTurnover.DefaultCellStyle.Format = "0.##%";
            colPrecursorEnrichment.DefaultCellStyle.Format = "0.##%";
            gridViewExcludedMzs.PeptideAnalysis = peptideAnalysis;
            tbxSequence.Text = peptideAnalysis.Peptide.Sequence;
            TabText = "Summary";
        }

        public PeptideAnalysis PeptideAnalysis { get { return (PeptideAnalysis) EntityModel; } }
        public Peptide Peptide { get { return PeptideAnalysis.Peptide;} }

        private void btnCreateAnalyses_Click(object sender, EventArgs e)
        {
            using (var form = new CreateFileAnalysesForm(PeptideAnalysis))
            {
                form.ShowDialog(TopLevelControl);
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
            InitializeOriginalMinMaxCharge();
        }

        private void InitializeOriginalMinMaxCharge()
        {
            using (var session = Workspace.OpenSession())
            {
                var query = session.CreateQuery("SELECT MIN(S.MinCharge), MAX(S.MaxCharge) FROM " +
                                                typeof (DbPeptideSearchResult) + " S WHERE S.Peptide.Id = :peptideId")
                    .SetParameter("peptideId", Peptide.Id);
                var row = (object[]) query.UniqueResult();
                _originalMinCharge = Convert.ToInt32(row[0]);
                _originalMaxCharge = Convert.ToInt32(row[1]);
            }
            BeginInvoke(new Action(OnPeptideAnalysisChanged));
        }

        private DataGridViewRow AddRow(PeptideFileAnalysis peptideFileAnalysis)
        {
            var row = dataGridView.Rows[dataGridView.Rows.Add()];
            row.Tag = peptideFileAnalysis;
            _peptideFileAnalysisRows.Add(peptideFileAnalysis, row);
            return row;
        }

        private void Remove(PeptideFileAnalysis peptideFileAnalysis)
        {
            DataGridViewRow row;
            if (!_peptideFileAnalysisRows.TryGetValue(peptideFileAnalysis, out row))
            {
                return;
            }
            dataGridView.Rows.Remove(row);
            _peptideFileAnalysisRows.Remove(peptideFileAnalysis);
        }

        private void UpdateRow(DataGridViewRow row)
        {
            var peptideFileAnalysis = (PeptideFileAnalysis) row.Tag;
            row.Cells[colStatus.Index].Value = peptideFileAnalysis.ValidationStatus;
            row.Cells[colTimePoint.Name].Value = peptideFileAnalysis.MsDataFile.TimePoint;
            row.Cells[colCohort.Name].Value = peptideFileAnalysis.MsDataFile.Cohort;
            row.Cells[colDataFileLabel.Name].Value = peptideFileAnalysis.MsDataFile.Label;
            row.Cells[colPeakStart.Name].Value = peptideFileAnalysis.Peaks.StartTime;
            row.Cells[colPeakEnd.Name].Value = peptideFileAnalysis.Peaks.EndTime;
            row.Cells[colPeakStart.Index].Style.Font 
                = row.Cells[colPeakEnd.Index].Style.Font
                = peptideFileAnalysis.AutoFindPeak
                    ? row.DataGridView.Font
                    : new Font(row.DataGridView.Font, FontStyle.Bold);
            if (peptideFileAnalysis.Peaks.TracerPercent.HasValue)
            {
                row.Cells[colTracerPercent.Index].Value = peptideFileAnalysis.Peaks.TracerPercent/100;
            }
            else
            {
                row.Cells[colTracerPercent.Index].Value = null;
            }
            row.Cells[colScore.Index].Value = peptideFileAnalysis.Peaks.DeconvolutionScore;
            row.Cells[colScore.Index].Style.BackColor
                = peptideFileAnalysis.FirstDetectedScan.HasValue ? Color.White : Color.LightGray;

            if (Workspace.GetTracerDefs().Count > 1)
            {
                row.Cells[colPrecursorEnrichment.Index].Value = peptideFileAnalysis.Peaks.PrecursorEnrichmentFormula;
            }
            else
            {
                row.Cells[colPrecursorEnrichment.Index].Value = peptideFileAnalysis.Peaks.PrecursorEnrichment;
            }
            row.Cells[colTurnover.Index].Value = peptideFileAnalysis.Peaks.Turnover;
        }

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            base.OnWorkspaceEntitiesChanged(args);
            if (args.GetEntities<MsDataFile>().Count > 0 || args.Contains(PeptideAnalysis))
            {
                UpdateRows(PeptideAnalysis.FileAnalyses.ListChildren());
            }
            else
            {
                var peptideFileAnalyses = new HashSet<PeptideFileAnalysis>(
                    args.GetEntities<PeptideFileAnalysis>().Where(f=>Equals(PeptideAnalysis, f.PeptideAnalysis)));
                foreach (var peaks in args.GetEntities<Peaks>())
                {
                    if (Equals(PeptideAnalysis, peaks.PeptideAnalysis))
                    {
                        peptideFileAnalyses.Add(peaks.PeptideFileAnalysis);
                    }
                }
                UpdateRows(peptideFileAnalyses);
            }
        }
        private void UpdateRows(ICollection<PeptideFileAnalysis> peptideFileAnalyses)
        {
            foreach (var peptideFileAnalysis in peptideFileAnalyses)
            {
                DataGridViewRow row;
                if (!_peptideFileAnalysisRows.TryGetValue(peptideFileAnalysis, out row))
                {
                    row = AddRow(peptideFileAnalysis);
                }
                UpdateRow(row);
            }
        }

        private void dataGridView_RowHeaderMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var row = dataGridView.Rows[e.RowIndex];
            var peptideAnalysis = (PeptideFileAnalysis) row.Tag;
            PeptideFileAnalysisFrame.ActivatePeptideDataForm<AbstractChromatogramForm>(this, peptideAnalysis);
        }

        protected void OnPeptideAnalysisChanged()
        {
            var res = Workspace.GetAminoAcidFormulas();
            tbxFormula.Text = res.GetFormula(Peptide.Sequence).ToString();
            tbxMonoMass.Text = Peptide.GetChargedPeptide(1).GetMonoisotopicMass(res).ToString("0.####");
            tbxAvgMass.Text = Peptide.GetChargedPeptide(1).GetMassDistribution(res).AverageMass.ToString("0.####");
            tbxMinCharge.Text = PeptideAnalysis.MinCharge.ToString();
            tbxMaxCharge.Text = PeptideAnalysis.MaxCharge.ToString();
            tbxProtein.Text = Peptide.ProteinName + " " + Peptide.ProteinDescription;
            tbxMassAccuracy.Text = PeptideAnalysis.GetMassAccuracy().ToString();
            if (PeptideAnalysis.MassAccuracy == null)
            {
                tbxMassAccuracy.Font = Font;
            }
            else
            {
                tbxMassAccuracy.Font = new Font(Font, FontStyle.Bold);
            }
            if (_originalMinCharge.HasValue && _originalMinCharge != PeptideAnalysis.MinCharge)
            {
                tbxMinCharge.Font = new Font(Font, FontStyle.Bold);
            }
            else
            {
                tbxMinCharge.Font = Font;
            }
            if (_originalMaxCharge.HasValue && _originalMaxCharge != PeptideAnalysis.MaxCharge)
            {
                tbxMaxCharge.Font = new Font(Font, FontStyle.Bold);
            }
            else
            {
                tbxMaxCharge.Font = Font;
            }

            UpdateMassGrid();
            UpdateRows(PeptideAnalysis.FileAnalyses.ListChildren());
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
            var peptideFileAnalysis = (PeptideFileAnalysis) row.Tag;
            var column = dataGridView.Columns[e.ColumnIndex];
            var cell = row.Cells[e.ColumnIndex];
            if (column == colCohort)
            {
                peptideFileAnalysis.MsDataFile.Cohort = Convert.ToString(cell.Value);
            }
            else if (column == colTimePoint)
            {
                peptideFileAnalysis.MsDataFile.TimePoint = DataFilesForm.ToDouble(cell.Value);
            }
            else if (column == colDataFileLabel)
            {
                peptideFileAnalysis.MsDataFile.Label = Convert.ToString(cell.Value);
            }
            else if (column == colStatus)
            {
                peptideFileAnalysis.ValidationStatus = (ValidationStatus) cell.Value;
            }
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
            var peptideFileAnalysis = (PeptideFileAnalysis)row.Tag;
            var column = dataGridView.Columns[e.ColumnIndex];
            if (e.ColumnIndex >= 0)
            {
                column = dataGridView.Columns[e.ColumnIndex];
            }
            if (column == colPeakStart || column == colPeakEnd)
            {
                PeptideFileAnalysisFrame.ActivatePeptideDataForm<AbstractChromatogramForm>(this, peptideFileAnalysis);
            }
            else if (column == colTracerPercent || column == colScore)
            {
                PeptideFileAnalysisFrame.ActivatePeptideDataForm<TracerChromatogramForm>(this, peptideFileAnalysis);
            }
        }

        private void tbxMassAccuracy_Leave(object sender, EventArgs e)
        {
            double value;
            try
            {
                value = double.Parse(tbxMassAccuracy.Text);
            }
            catch
            {
                return;
            }
            using (Workspace.GetWriteLock())
            {
                if (value == Workspace.GetMassAccuracy())
                {
                    PeptideAnalysis.MassAccuracy = null;
                }
                else
                {
                    PeptideAnalysis.MassAccuracy = value;
                }
            }
        }
    }
}
