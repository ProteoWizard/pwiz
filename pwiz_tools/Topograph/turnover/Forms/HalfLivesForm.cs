using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.MsData;
using pwiz.Topograph.ui.Properties;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class HalfLivesForm : WorkspaceForm
    {
        private readonly Dictionary<String, CohortColumns> _cohortColumns
            = new Dictionary<string, CohortColumns>();
        public HalfLivesForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            var tracerDef = workspace.GetTracerDefs()[0];
            tbxInitialTracerPercent.Text = tracerDef.InitialApe.ToString();
            tbxFinalTracerPercent.Text = tracerDef.FinalApe.ToString();
        }

        public double MinScore
        {
            get
            {
                if (string.IsNullOrEmpty(tbxMinScore.Text))
                {
                    return 0;
                }
                try
                {
                    return double.Parse(tbxMinScore.Text);
                }
                catch
                {
                    return 0;
                }
            }
        }

        private void btnRequery_Click(object sender, EventArgs e)
        {
            double minScore = MinScore;
            var calculator = new HalfLifeCalculator(Workspace)
                                 {
                                     ByProtein = cbxByProtein.Checked,
                                     MinScore = minScore,
                                     InitialPercent = double.Parse(tbxInitialTracerPercent.Text),
                                     FinalPercent = double.Parse(tbxFinalTracerPercent.Text),
                                 };
            var longOperationBroker = new LongOperationBroker(calculator,
                                                              new LongWaitDialog(this, "Calculating Half Lives"));
            if (!longOperationBroker.LaunchJob())
            {
                return;
            }
            foreach (var entry in _cohortColumns.ToArray())
            {
                if (!calculator.Cohorts.Contains(entry.Key))
                {
                    dataGridView1.Columns.Remove(entry.Value.HalfLifeColumn);
                    dataGridView1.Columns.Remove(entry.Value.HalfLifeErrorColumn);
                    _cohortColumns.Remove(entry.Key);
                }
            }
            var cohorts = new List<String>(calculator.Cohorts);
            cohorts.Sort();
            foreach (var cohort in cohorts)
            {
                CohortColumns cohortColumns;
                if (!_cohortColumns.TryGetValue(cohort, out cohortColumns))
                {
                    cohortColumns = new CohortColumns
                                        {
                                            HalfLifeColumn = new DataGridViewLinkColumn
                                                                 {
                                                                     HeaderText = cohort + " half life",
                                                                     DefaultCellStyle = {Format = "0.####"},
                                                                     SortMode = DataGridViewColumnSortMode.Automatic,
                                                                 },
                                            HalfLifeErrorColumn = new DataGridViewTextBoxColumn
                                                                      {
                                                                          HeaderText = cohort + " half life range",
                                                                          SortMode = DataGridViewColumnSortMode.Automatic,
                                                                      },
                                        };
                    dataGridView1.Columns.Add(cohortColumns.HalfLifeColumn);
                    dataGridView1.Columns.Add(cohortColumns.HalfLifeErrorColumn);
                    _cohortColumns.Add(cohort, cohortColumns);
                }
            }
            dataGridView1.Rows.Clear();
            if (calculator.ResultRows.Count == 0)
            {
                MessageBox.Show(this, "No results.  The problem might be that you have not set the time point on any data files.", Program.AppName);
                return;
            }
            dataGridView1.Rows.Add(calculator.ResultRows.Count);
            for (int iRow = 0; iRow < calculator.ResultRows.Count; iRow++) 
            {
                var resultRow = calculator.ResultRows[iRow];
                var row = dataGridView1.Rows[iRow];
                row.Cells[colPeptide.Index].Value = resultRow.PeptideSequence;
                row.Cells[colProtein.Index].Value = resultRow.ProteinName;
                row.Cells[colProteinDescription.Index].Value = resultRow.ProteinDescription;
                foreach (var cohort in cohorts)
                {
                    var cohortColumns = _cohortColumns[cohort];
                    var resultData = resultRow.ResultDatas[cohort];
                    row.Cells[cohortColumns.HalfLifeColumn.Index].Value = resultData.HalfLife;
                    row.Cells[cohortColumns.HalfLifeErrorColumn.Index].Value = resultData.MinHalfLife.ToString("0.####") + "-" + resultData.MaxHalfLife.ToString("0.####") + " (" + resultData.PointCount + " pts)";
                }
            }
            btnSave.Enabled = true;
        }

        private class CohortColumns
        {
            public DataGridViewColumn HalfLifeColumn { get; set; }
            public DataGridViewColumn HalfLifeErrorColumn { get; set; }
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0)
            {
                return;
            }
            var row = dataGridView1.Rows[e.RowIndex];
            if (e.ColumnIndex == colPeptide.Index)
            {
                var sequence = Convert.ToString(row.Cells[colPeptide.Index].Value);
                DbPeptideAnalysis dbPeptideAnalysis;
                using (Workspace.GetReadLock())
                {
                    using (var session = Workspace.OpenSession())
                    {
                        var query =
                            session.CreateQuery("FROM " + typeof (DbPeptideAnalysis) +
                                                " T WHERE T.Peptide.Sequence = :sequence")
                                .SetParameter("sequence", sequence)
                                .SetMaxResults(1);
                        dbPeptideAnalysis = query.UniqueResult() as DbPeptideAnalysis;
                        if (dbPeptideAnalysis == null)
                        {
                            return;
                        }
                    }
                }
                var peptideAnalysis = TurnoverForm.Instance.LoadPeptideAnalysis(dbPeptideAnalysis.Id.Value);
                if (peptideAnalysis == null)
                {
                    return;
                }
                var form = Program.FindOpenEntityForm<PeptideAnalysisFrame>(peptideAnalysis);
                if (form != null)
                {
                    form.Activate();
                    return;
                }
                form = new PeptideAnalysisFrame(peptideAnalysis);
                form.Show(DockPanel, DockState);
                return;
            }
            foreach (var entry in _cohortColumns)
            {
                if (e.ColumnIndex == entry.Value.HalfLifeColumn.Index)
                {
                    var halfLifeForm = new HalfLifeForm(Workspace)
                                           {
                                               Peptide = Convert.ToString(row.Cells[colPeptide.Index].Value) ?? "",
                                               ProteinName = Convert.ToString(row.Cells[colProtein.Index].Value),
                                               Cohort = entry.Key,
                                               MinScore = MinScore,
                                               InitialPercent = double.Parse(tbxInitialTracerPercent.Text),
                                               FinalPercent = double.Parse(tbxFinalTracerPercent.Text),
                                           };
                    halfLifeForm.Show(DockPanel, DockState);
                }
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            Settings.Default.Reload();
            var dialog = new SaveFileDialog()
                             {
                                 Filter = "Tab Separated Values (*.tsv)|*.tsv|All Files|*.*",
                                 InitialDirectory = Settings.Default.ExportResultsDirectory,
                             };
            if (dialog.ShowDialog(this) == DialogResult.Cancel)
            {
                return;
            }
            String filename = dialog.FileName;
            Settings.Default.ExportResultsDirectory = Path.GetDirectoryName(filename);
            Settings.Default.Save();
            var columns = GetColumnsSortedByDisplayIndex();
            using (var stream = File.OpenWrite(filename))
            {
                var writer = new StreamWriter(stream);
                var tab = "";
                foreach (var column in columns)
                {
                    writer.Write(tab);
                    tab = "\t";
                    writer.Write(column.HeaderText);
                }
                writer.WriteLine();
                for (int iRow = 0; iRow < dataGridView1.Rows.Count; iRow ++)
                {
                    var row = dataGridView1.Rows[iRow];
                    tab = "";
                    foreach (var column in columns)
                    {
                        writer.Write(tab);
                        tab = "\t";
                        writer.Write(row.Cells[column.Index].Value);
                    }
                    writer.WriteLine();
                }
            }
        }
        private IList<DataGridViewColumn> GetColumnsSortedByDisplayIndex()
        {
            var result = new DataGridViewColumn[dataGridView1.Columns.Count];
            dataGridView1.Columns.CopyTo(result, 0);
            Array.Sort(result, (a, b) => a.DisplayIndex.CompareTo(b.DisplayIndex));
            return result;
        }

    }
}
