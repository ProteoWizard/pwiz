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
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using ZedGraph;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PeptideAnalysisSummary : EntityModelForm
    {
        private readonly Dictionary<PeptideFileAnalysis, DataGridViewRow> _peptideFileAnalysisRows 
            = new Dictionary<PeptideFileAnalysis, DataGridViewRow>();

        private int? _originalMinCharge;
        private int? _originalMaxCharge;
        private bool _normalizeRetentionTimes;
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

        private void BtnCreateAnalysesOnClick(object sender, EventArgs e)
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
                var query = session.CreateQuery("SELECT MIN(S.PrecursorCharge), MAX(S.PrecursorCharge) FROM " +
                                                typeof (DbPeptideSpectrumMatch) + " S WHERE S.Peptide.Id = :peptideId")
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
            row.Cells[colPeakStart.Index].Style.Font 
                = row.Cells[colPeakEnd.Index].Style.Font
                = peptideFileAnalysis.AutoFindPeak
                    ? row.DataGridView.Font
                    : new Font(row.DataGridView.Font, FontStyle.Bold);
            if (peptideFileAnalysis.CalculatedPeaks != null)
            {
                row.Cells[colPeakStart.Index].Value = peptideFileAnalysis.CalculatedPeaks.StartTime;
                row.Cells[colPeakEnd.Index].Value = peptideFileAnalysis.CalculatedPeaks.EndTime;
                row.Cells[colTracerPercent.Index].Value = peptideFileAnalysis.CalculatedPeaks.TracerPercent / 100;
                row.Cells[colScore.Index].Value = peptideFileAnalysis.CalculatedPeaks.DeconvolutionScore;
                if (Workspace.GetTracerDefs().Count > 1)
                {
                    row.Cells[colPrecursorEnrichment.Index].Value = peptideFileAnalysis.CalculatedPeaks.PrecursorEnrichmentFormula;
                }
                else
                {
                    row.Cells[colPrecursorEnrichment.Index].Value = peptideFileAnalysis.CalculatedPeaks.PrecursorEnrichment;
                }
                row.Cells[colTurnover.Index].Value = peptideFileAnalysis.CalculatedPeaks.Turnover;
            }
            else
            {
                row.Cells[colPeakStart.Index].Value = null;
                row.Cells[colPeakEnd.Index].Value = null;
                row.Cells[colTracerPercent.Index].Value = null;
                row.Cells[colScore.Index].Value = null;
                row.Cells[colPrecursorEnrichment.Index].Value = null;
                row.Cells[colTurnover.Index].Value = null;
            }
            row.Cells[colScore.Index].Style.BackColor
                = peptideFileAnalysis.PsmCount > 0 ? Color.White : Color.LightGray;

        }

//        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
//        {
//            base.OnWorkspaceEntitiesChanged(args);
////            if (args.GetEntities<MsDataFile>().Count > 0)
////            {
////                UpdateRows(PeptideAnalysis.FileAnalyses.Values);
////            }
////            else
//            {
////                var peptideFileAnalyses = new HashSet<PeptideFileAnalysis>(
////                    args.GetEntities<PeptideFileAnalysis>().Where(f=>Equals(PeptideAnalysis, f.PeptideAnalysis)));
////                foreach (var peaks in args.GetEntities<CalculatedPeaks>())
////                {
////                    if (Equals(PeptideAnalysis, peaks.PeptideAnalysis))
////                    {
////                        peptideFileAnalyses.Add(peaks.PeptideFileAnalysis);
////                    }
////                }
////                UpdateRows(peptideFileAnalyses);
//            }
//        }
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
            UpdateGraph();
        }

        private void UpdateGraph()
        {
            zedGraphControl.GraphPane.CurveList.Clear();
            zedGraphControl.GraphPane.GraphObjList.Clear();
            zedGraphControl.GraphPane.Title.IsVisible = false;
            MsDataFile normalizeTo = null;
            var fileAnalyses = new List<PeptideFileAnalysis>();
            for (int iRow = 0; iRow < dataGridView.Rows.Count; iRow++)
            {
                fileAnalyses.Add((PeptideFileAnalysis)dataGridView.Rows[iRow].Tag);
            }
            if (fileAnalyses.Count == 0)
            {
                return;
            }
            if (_normalizeRetentionTimes)
            {
                normalizeTo = fileAnalyses[0].MsDataFile;
            }
            var tracerFormulas = PeptideAnalysis.GetTurnoverCalculator().ListTracerFormulas();
            var pointPairLists = tracerFormulas.Select(tf=>new PointPairList()).ToArray();
            for (int iFileAnalysis = 0; iFileAnalysis < fileAnalyses.Count; iFileAnalysis++)
            {
                var fileAnalysis = fileAnalyses[iFileAnalysis];
                var peaks = fileAnalysis.CalculatedPeaks;
                if (peaks == null)
                {
                    continue;
                }
                for (int iTracerFormula = 0; iTracerFormula < tracerFormulas.Count; iTracerFormula++)
                {
                    var pointPairList = pointPairLists[iTracerFormula];
                    var peak = peaks.GetPeak(tracerFormulas[iTracerFormula]);
                    if (peak == null)
                    {
                        pointPairList.Add(new PointPair(iFileAnalysis + 1, PointPairBase.Missing, PointPairBase.Missing));
                    }
                    else
                    {
                        if (normalizeTo == null)
                        {
                            pointPairList.Add(new PointPair(iFileAnalysis + 1, peak.Value.EndTime, peak.Value.StartTime));
                        }
                        else
                        {
                            var alignment = fileAnalysis.MsDataFile.GetRetentionTimeAlignment(normalizeTo);
                            if (alignment.IsInvalid)
                            {
                                pointPairList.Add(new PointPair(iFileAnalysis + 1, PointPairBase.Missing, PointPairBase.Missing));
                            }
                            else
                            {
                                pointPairList.Add(new PointPair(iFileAnalysis + 1, alignment.GetTargetTime(peak.Value.EndTime), alignment.GetTargetTime(peak.Value.StartTime), fileAnalysis));
                            }
                        }
                    }
                }
            }
            zedGraphControl.GraphPane.XAxis.Type = AxisType.Text;
            zedGraphControl.GraphPane.XAxis.Scale.TextLabels =
                fileAnalyses.Select(fileAnalysis => fileAnalysis.MsDataFile.ToString()).ToArray();
            zedGraphControl.GraphPane.XAxis.Title.Text = "Data File";
            zedGraphControl.GraphPane.YAxis.Title.Text = normalizeTo == null ? "Retention Time" : "Normalized Retention Time";
            
            for (int iTracerFormula = 0; iTracerFormula < tracerFormulas.Count; iTracerFormula++)
            {
                zedGraphControl.GraphPane.AddHiLowBar(tracerFormulas[iTracerFormula].ToDisplayString(),
                                                                  pointPairLists[iTracerFormula],
                                                                  TracerChromatogramForm.GetColor(iTracerFormula,
                                                                                                  tracerFormulas.Count));
            }
            zedGraphControl.GraphPane.AxisChange();
            zedGraphControl.Invalidate();
        }

        private void DataGridViewOnRowHeaderMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var row = dataGridView.Rows[e.RowIndex];
            var peptideAnalysis = (PeptideFileAnalysis) row.Tag;
            PeptideFileAnalysisFrame.ActivatePeptideDataForm<AbstractChromatogramForm>(this, peptideAnalysis);
        }

        protected void OnPeptideAnalysisChanged()
        {
            PeptideAnalysis.EnsurePeaksCalculated();
            var res = Workspace.GetAminoAcidFormulas();
            tbxFormula.Text = res.GetFormula(Peptide.Sequence).ToString();
            tbxMonoMass.Text = Peptide.GetChargedPeptide(1).GetMonoisotopicMass(res).ToString("0.####");
            tbxAvgMass.Text = Peptide.GetChargedPeptide(1).GetMassDistribution(res).AverageMass.ToString("0.####");
            tbxMinCharge.Text = PeptideAnalysis.MinCharge.ToString(CultureInfo.CurrentCulture);
            tbxMaxCharge.Text = PeptideAnalysis.MaxCharge.ToString(CultureInfo.CurrentCulture);
            tbxProtein.Text = Peptide.ProteinName + " " + Peptide.ProteinDescription;
            tbxMassAccuracy.Text = PeptideAnalysis.GetMassAccuracy().ToString(CultureInfo.CurrentCulture);
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
            UpdateRows(PeptideAnalysis.FileAnalyses);
        }

        protected override void EntityChanged()
        {
            base.EntityChanged();
            OnPeptideAnalysisChanged();
        }

        private void TbxMinChargeOnLeave(object sender, EventArgs e)
        {
            PeptideAnalysis.MinCharge = Convert.ToInt32(tbxMinCharge.Text);
        }

        private void TbxMaxChargeOnTextChanged(object sender, EventArgs e)
        {
            PeptideAnalysis.MaxCharge = Convert.ToInt32(tbxMaxCharge.Text);
        }

        private void DataGridViewOnCellEndEdit(object sender, DataGridViewCellEventArgs e)
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

        private void BtnShowGraphOnClick(object sender, EventArgs e)
        {
            var halfLifeForm = new HalfLifeForm(Workspace)
                                   {
                                       Peptide = Peptide.Sequence,
                                       ProteinName = Peptide.ProteinName,
                                   };
            halfLifeForm.Show(DockPanel, DockState);
        }

        private void DataGridViewOnCellContentClick(object sender, DataGridViewCellEventArgs e)
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

        private void TbxMassAccuracyOnLeave(object sender, EventArgs e)
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
            if (value == Workspace.GetMassAccuracy())
            {
                PeptideAnalysis.MassAccuracy = null;
            }
            else
            {
                PeptideAnalysis.MassAccuracy = value;
            }
        }

        private void ZedGraphControlOnContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            menuStrip.Items.Add(new ToolStripMenuItem("Normalize Times", null, NormalizeTimesOnClick)
                                    {CheckOnClick = true, Checked = _normalizeRetentionTimes});
        }

        private void NormalizeTimesOnClick(object sender, EventArgs eventArgs)
        {
            var toolstripMenuItem = sender as ToolStripMenuItem;
            if (toolstripMenuItem != null)
            {
                _normalizeRetentionTimes = toolstripMenuItem.Checked;
                UpdateGraph();
            }
        }
    }
}
