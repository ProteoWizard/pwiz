using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NHibernate.Criterion;
using turnover.Data;
using turnover.Enrichment;
using turnover.Model;

namespace turnover.ui.Forms
{
    public partial class PeptideAnalysisForm : EntityModelForm
    {
        private readonly Dictionary<PeptideFileAnalysis, DataGridViewRow> peptideAnalysisRows 
            = new Dictionary<PeptideFileAnalysis, DataGridViewRow>();
        public PeptideAnalysisForm(PeptideAnalysis peptideAnalysis) : base(peptideAnalysis)
        {
            InitializeComponent();
            gridViewExcludedMzs.PeptideAnalysis = peptideAnalysis;
            tbxSequence.Text = peptideAnalysis.Peptide.Sequence;
            TabText = PeptideAnalysis.GetLabel();
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
            foreach (PeptideFileAnalysis peptideFileAnalysis in PeptideAnalysis.GetFileAnalyses())
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
            if (!peptideFileAnalysis.EnsureCalculated())
            {
                Workspace.EnsureChromatograms(peptideFileAnalysis);
            }
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
            var peptideAnalysis = (PeptideFileAnalysis) row.Tag;
            row.Cells[colTimePoint.Name].Value = peptideAnalysis.MsDataFile.TimePoint;
            row.Cells[colCohort.Name].Value = peptideAnalysis.MsDataFile.Cohort;
            row.Cells[colDataFileLabel.Name].Value = peptideAnalysis.MsDataFile.Label;
            bool calculated = peptideAnalysis.EnsureCalculated();
            row.Cells[colPeakStart.Name].Value = peptideAnalysis.PeakStart;
            row.Cells[colPeakEnd.Name].Value = peptideAnalysis.PeakEnd;
            if (calculated)
            {
                List<double> apes = new List<double>();
                TurnoverCalculator.DistributionResult result;
                peptideAnalysis.CalculateEnrichments(out apes, out result);
                double turnover = 0;
                double sum = result.Amounts.Sum();
                double ape = 0;
                if (sum != 0)
                {
                    turnover = 100 * (sum - result.Amounts[0])/sum;
                    for (int i = 0; i < apes.Count; i++)
                    {
                        ape += result.Amounts[i]*apes[i]/sum;
                    }
                }
                row.Cells[colTurnover.Name].Value = turnover;
                row.Cells[colAPE.Name].Value = ape;
            }
            else
            {
                row.Cells[colTurnover.Name].Value = null;
                row.Cells[colAPE.Name].Value = null;
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
                UpdateRows(args.GetEntities<PeptideFileAnalysis>());
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
            PeptideFileAnalysisFrame.ActivatePeptideDataForm<PeptideInfoForm>(this, peptideAnalysis);
        }

        protected void OnPeptideAnalysisChanged()
        {
            var res = Workspace.GetResidueComposition();
            tbxFormula.Text = res.DictionaryToFormula(
                res.FormulaToDictionary(res.MolecularFormula(Peptide.Sequence)));
            tbxMonoMass.Text = res.GetMonoisotopicMz(Peptide.GetChargedPeptide(1)).ToString("0.####");
            tbxAvgMass.Text = res.GetAverageMz(Peptide.GetChargedPeptide(1)).ToString("0.####");
            tbxMinCharge.Text = PeptideAnalysis.MinCharge.ToString();
            tbxMaxCharge.Text = PeptideAnalysis.MaxCharge.ToString();
            tbxInitialEnrichment.Text = PeptideAnalysis.InitialEnrichment.ToString();
            tbxFinalEnrichment.Text = PeptideAnalysis.FinalEnrichment.ToString();
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
    }

        private void tbxInitialEnrichment_Leave(object sender, EventArgs e)
        {
            PeptideAnalysis.InitialEnrichment = Convert.ToDouble(tbxInitialEnrichment.Text);
        }

        private void tbxFinalEnrichment_Leave(object sender, EventArgs e)
        {
            PeptideAnalysis.FinalEnrichment = Convert.ToDouble(tbxFinalEnrichment.Text);
        }

        private void tbxIntermediateLevels_Leave(object sender, EventArgs e)
        {
            PeptideAnalysis.IntermediateLevels = Convert.ToInt32(tbxIntermediateLevels.Text);
        }

        private void dataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            var row = dataGridView.Rows[e.RowIndex];
            var peptideAnalysis = (PeptideFileAnalysis) row.Tag;
            DataGridViewColumn column = null;
            if (e.ColumnIndex >= 0)
            {
                column = dataGridView.Columns[e.ColumnIndex];
            }
            if (column == colPeakStart || column == colPeakEnd)
            {
                PeptideFileAnalysisFrame.ActivatePeptideDataForm<ChromatogramForm>(this, peptideAnalysis);
            }
            else if (column == colTurnover || column == colAPE)
            {
                PeptideFileAnalysisFrame.ActivatePeptideDataForm<PrecursorEnrichmentsForm>(this, peptideAnalysis);
            }
        }
        private void UpdateMassGrid()
        {
            gridViewExcludedMzs.UpdateGrid();
        }

        private void btnShowGraph_Click(object sender, EventArgs e)
        {
            var graphForm = new GraphForm(PeptideAnalysis);
            graphForm.Show(DockPanel, DockState);
        }
    }
}
