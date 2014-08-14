/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using NHibernate;
using pwiz.Topograph.Model;
using pwiz.Topograph.ui.Controls;
using pwiz.Topograph.Util;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PrecursorEnrichmentForm : WorkspaceForm
    {
        private ZedGraphControl _zedGraphControl;
        private IDictionary<CohortKey, IDictionary<double, int>> _queryRows;
        public PrecursorEnrichmentForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            _zedGraphControl = new ZedGraphControlEx
                                   {
                                       Dock = DockStyle.Fill,
                                   };
            _zedGraphControl.GraphPane.Title.Text = "Distribution of Precursor Enrichment Values";
            _zedGraphControl.GraphPane.XAxis.Title.Text = "Precursor Enrichment (%)";
            _zedGraphControl.GraphPane.YAxis.Title.Text = "# of peptide replicates";
            splitContainer1.Panel2.Controls.Add(_zedGraphControl);
            tbxMinScore.Text = workspace.GetMinDeconvolutionScoreForAvgPrecursorPool().ToString("0.####");
        }

        private void BtnRefreshOnClick(object sender, EventArgs e)
        {
            _queryRows = null;
            IDictionary<CohortKey, IDictionary<double, int>> rows = null;
            using (var session = Workspace.OpenSession())
            {
                using (var longWaitDialog = new LongWaitDialog(TopLevelControl, "Querying database"))
                {
                    var broker = new LongOperationBroker(delegate
                                                             {
                                                                 rows = QueryRecords(session);
                                                             },
                                                         longWaitDialog, session);
                    if (broker.LaunchJob())
                    {
                        _queryRows = rows;
                    }
                }
            }
            RefreshUi();
        }

        private bool _inRefreshUi;

        private void RefreshUi() {
            if (_inRefreshUi)
            {
                return;
            }
            try
            {
                
            _inRefreshUi = true;

            _zedGraphControl.GraphPane.CurveList.Clear();
            _zedGraphControl.GraphPane.GraphObjList.Clear();
            if (_queryRows == null)
            {
                return;
            }
            var values = new Dictionary<CohortKey, double[]>();
            var xValues = new double[101];
            for (int i = 0; i < xValues.Length; i++ )
            {
                xValues[i] = i;
            }
            foreach (var row in _queryRows)
            {
                double[] vector;
                if (!values.TryGetValue(row.Key, out vector))
                {
                    vector = new double[xValues.Length];
                    values.Add(row.Key, vector);
                }
                foreach (var entry in row.Value)
                {
                    
                    if (entry.Key < 0 || entry.Key > 1)
                    {
                        continue;
                    }
                    vector[(int) Math.Round(entry.Key * 100)] += entry.Value;
                }
            }
            var cohortKeys = new List<CohortKey>(values.Keys);
            cohortKeys.Sort();
            if (dataGridView1.Rows.Count != cohortKeys.Count)
            {
                dataGridView1.Rows.Clear();
                dataGridView1.Rows.Add(cohortKeys.Count);
            }
            for (int i = 0; i < cohortKeys.Count; i++)
            {
                var cohortKey = cohortKeys[i];
                var yValues = values[cohortKey];
                var row = dataGridView1.Rows[i];
                if (dataGridView1.SelectedRows.Count == 0 || dataGridView1.SelectedRows.Contains(row))
                {
                    _zedGraphControl.GraphPane.AddBar(cohortKey.ToString(), xValues, yValues, TracerChromatogramForm.GetColor(i, cohortKeys.Count));
                }
                row.Cells[colCohort.Index].Value = cohortKey;
                var statsX = new Statistics(xValues);
                var statsY = new Statistics(yValues);
                row.Cells[colMean.Index].Value = statsX.Mean(statsY);
                row.Cells[colStdDev.Index].Value = statsX.StdDev(statsY);
                row.Cells[colMedian.Index].Value = statsX.Median(statsY);
            }
            _zedGraphControl.GraphPane.AxisChange();
            _zedGraphControl.Invalidate();
            }
            finally
            {
                _inRefreshUi = false;
            }
        }

        private IDictionary<CohortKey, IDictionary<double, int>> QueryRecords(ISession session)
        {
            double minScore;
            int minTracers;
            Double.TryParse(tbxMinScore.Text, out minScore);
            int.TryParse(tbxMinTracers.Text, out minTracers);
            IQuery query;
            bool byFile = cbxByFile.Checked;
            if (byFile)
            {
                query = session.CreateQuery(
                    "SELECT F.PrecursorEnrichment, Count(F.Id), F.PeptideAnalysis.Peptide.Sequence, F.MsDataFile.Label FROM DbPeptideFileAnalysis F WHERE F.DeconvolutionScore >= :minScore GROUP BY F.PeptideAnalysis.Peptide.Sequence, F.PrecursorEnrichment, F.MsDataFile.Label")
                    .SetParameter("minScore", minScore);
            }
            else
            {
                query =
                    session.CreateQuery(
                    "SELECT F.PrecursorEnrichment, Count(F.Id), F.PeptideAnalysis.Peptide.Sequence, F.MsDataFile.Cohort, F.MsDataFile.TimePoint FROM DbPeptideFileAnalysis F WHERE F.DeconvolutionScore >= :minScore GROUP BY F.PeptideAnalysis.Peptide.Sequence, F.PrecursorEnrichment, F.MsDataFile.Cohort, F.MsDataFile.TimePoint")
                    .SetParameter("minScore", minScore);
            }
            var result = new Dictionary<CohortKey, IDictionary<double, int>>();
            foreach (object[] row in query.List())
            {
                if (row[0] == null)
                {
                    continue;
                }
                string cohort = null;
                double? timePoint = null;
                var peptideSequence = Convert.ToString(row[2]);
                if (minTracers > 0 && Workspace.GetMaxTracerCount(peptideSequence) < minTracers)
                {
                    continue;
                }
                if (row[3] != null)
                {
                    cohort = Convert.ToString(row[3]);
                }
                if (!byFile && row[4] != null)
                {
                    timePoint = Convert.ToDouble(row[4]);
                }
                var cohortKey = new CohortKey(cohort, timePoint);
                IDictionary<double, int> dict;
                if (!result.TryGetValue(cohortKey, out dict))
                {
                    dict = new Dictionary<double, int>();
                    result.Add(cohortKey, dict);
                }
                int count;
                double precursorEnrichment = Convert.ToDouble(row[0]);
                dict.TryGetValue(precursorEnrichment, out count);
                count += Convert.ToInt32(row[1]);
                dict[precursorEnrichment] = count;
            }
            return result;
        }

        private class CohortKey : IComparable<CohortKey>
        {
            public CohortKey(string cohort, double? timePoint)
            {
                Cohort = cohort;
                TimePoint = timePoint;
            }
            public string Cohort { get; private set; }
            public double? TimePoint { get; private set; }
            public int CompareTo(CohortKey other)
            {
                int result = string.Compare(Cohort ?? "", other.Cohort ?? "", StringComparison.CurrentCultureIgnoreCase);
                if (result != 0)
                {
                    return result;
                }
                return (TimePoint ?? 0.0).CompareTo(other.TimePoint ?? 0.0);
            }
            public override string ToString()
            {
                if (Cohort == null)
                {
                    return ((object) TimePoint ?? "").ToString();
                }
                if (TimePoint == null)
                {
                    return Cohort;
                }
                return Cohort + " " + TimePoint;
            }

            public bool Equals(CohortKey other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Equals(other.Cohort, Cohort) && other.TimePoint.Equals(TimePoint);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != typeof (CohortKey)) return false;
                return Equals((CohortKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Cohort != null ? Cohort.GetHashCode() : 0)*397) ^ (TimePoint.HasValue ? TimePoint.Value.GetHashCode() : 0);
                }
            }
        }

        private void DataGridView1OnSelectionChanged(object sender, EventArgs e)
        {
            RefreshUi();
        }
    }
}
