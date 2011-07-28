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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.MsData;
using pwiz.Topograph.ui.Util;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class ResultsPerReplicateForm : WorkspaceForm
    {
        private IViewContext _viewContext;
        public ResultsPerReplicateForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            bool hasTimePoints = Workspace.MsDataFiles.ListChildren().Where(f=>f.TimePoint != null).Count() != 0;
            bool hasCohorts = Workspace.MsDataFiles.ListChildren().Where(f=>f.Cohort != null).Count() != 0;
            bool hasSamples = Workspace.MsDataFiles.ListChildren().Where(f=>f.Sample != null).Count() != 0;
            bool hasTracerDefs = Workspace.GetTracerDefs().Count != 0;
            bool hasOneTracerDef = Workspace.GetTracerDefs().Count == 1;
            var defaultColumns = new[]
                                         {
                                             new ColumnSpec().SetName("Accept"),
                                             new ColumnSpec().SetName("Peptide"),
                                             new ColumnSpec().SetName("DataFile.Name").SetCaption("DataFile"),
                                             new ColumnSpec().SetName("Area"),
                                             new ColumnSpec().SetName("TracerPercent").SetVisible(hasTracerDefs),
                                             new ColumnSpec().SetName("DeconvolutionScore"),
                                             new ColumnSpec().SetName("DataFile.Cohort").SetVisible(hasCohorts),
                                             new ColumnSpec().SetName("DataFile.TimePoint").SetVisible(hasTimePoints),
                                             new ColumnSpec().SetName("DataFile.Sample").SetVisible(hasSamples),
                                             new ColumnSpec().SetName("IndividualTurnover.PrecursorEnrichment").SetCaption("Ind Precursor Enrichment").SetVisible(hasTracerDefs),
                                             new ColumnSpec().SetName("IndividualTurnover.Turnover").SetCaption("Ind Turnover").SetVisible(hasTracerDefs),
                                             new ColumnSpec().SetName("IndividualTurnover.Score").SetCaption("Ind Turnover Score").SetVisible(hasTracerDefs),
                                             new ColumnSpec().SetName("Peptide.ProteinName").SetCaption("Protein"),
                                             new ColumnSpec().SetName("Peptide.ProteinDescription"),
                                             new ColumnSpec().SetName("AverageTurnover.PrecursorEnrichment").SetCaption("Avg Precursor Enrichment").SetVisible(hasOneTracerDef),
                                             new ColumnSpec().SetName("AverageTurnover.Turnover").SetCaption("Avg Turnover").SetVisible(hasOneTracerDef),
                                             new ColumnSpec().SetName("AverageTurnover.Score").SetCaption("Avg Turnover Score").SetVisible(hasOneTracerDef),
                                             new ColumnSpec().SetName("Status"),
                                             new ColumnSpec().SetName("PsmCount"),
                                             new ColumnSpec().SetName("PeakIntegrationNote"),
                                         };

            var defaultViewSpec = new ViewSpec()
                .SetName("default")
                .SetColumns(defaultColumns);
            navBar21.ViewContext = _viewContext = new TopographViewContext(dataGridViewResults, workspace, typeof (ResultRow), new[] {defaultViewSpec});
        }

        private void btnRequery_Click(object sender, EventArgs e)
        {
            var calculator = new HalfLifeCalculator(Workspace, HalfLifeCalculationType.GroupPrecursorPool)
            {
                MaxResults = MaxResults,
            };
            using (var longWaitDialog = new LongWaitDialog(TopLevelControl, "Calculating Half Lives"))
            {
                var longOperationBroker = new LongOperationBroker(calculator, longWaitDialog);
                if (!longOperationBroker.LaunchJob())
                {
                    return;
                }
            }
            UpdateRows(calculator);
        }

     
        private void UpdateRows(HalfLifeCalculator halfLifeCalculator)
        {
            var resultRows = halfLifeCalculator.RowDatas.Select(rd => new ResultRow(rd)).ToArray();
            var bindingListView = bindingSourceResults.DataSource as BindingListView;
            if (bindingListView == null)
            {
                bindingListView = new BindingListView(new ViewInfo(_viewContext.ParentColumn, _viewContext.BuiltInViewSpecs.First()), resultRows);
            }
            else
            {
                bindingListView = new BindingListView(bindingListView.ViewInfo, resultRows);
            }
            bindingSourceResults.DataSource = bindingListView;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            GridUtil.ExportResults(dataGridViewResults, Path.GetFileNameWithoutExtension(Workspace.DatabasePath) + "ResultsPerReplicate");
        }

        private void dataGridView1_RowHeaderMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }
            var row = dataGridViewResults.Rows[e.RowIndex];
        }
        public int? MaxResults
        {
            get
            {
                int maxResults;
                if (int.TryParse(tbxMaxResults.Text, out maxResults))
                {
                    return maxResults;
                }
                return null;
            }
            set
            {
                if (value.HasValue)
                {
                    tbxMaxResults.Text = value.ToString();
                }
                else
                {
                    tbxMaxResults.Text = "";
                }
            }
        }
        public class TurnoverResult
        {
            public double PrecursorEnrichment {get; set;}
            public double Turnover { get; set; }
            public double Score { get; set; }

        }
        public class ResultRow
        {
            readonly HalfLifeCalculator.RowData _rowData;
            public ResultRow(HalfLifeCalculator.RowData rowData)
            {
                _rowData = rowData;
            }
            public bool Accept { get { return _rowData.Accept; } }
            public LinkValue<Peptide> Peptide {get
            {
                return new LinkValue<Peptide>(_rowData.Peptide, (o, e) => PeptideFileAnalysisFrame
                    .ShowPeptideFileAnalysis(_rowData.Peptide.Workspace, _rowData.PeptideFileAnalysisId));
            }}
            public MsDataFile DataFile { get { return _rowData.MsDataFile; } }
            public double? Area { get { return _rowData.AreaUnderCurve; } }
            public double? TracerPercent { get { return _rowData.TracerPercent; } }
            public double? DeconvolutionScore { get { return _rowData.DeconvolutionScore; } }
            public TurnoverResult IndividualTurnover 
            { 
                get 
                { 
                    if (!_rowData.IndTurnover.HasValue || !_rowData.IndPrecursorEnrichment.HasValue || !_rowData.IndTurnoverScore.HasValue) {
                        return null;
                    }
                    return new TurnoverResult
                               {
                                   PrecursorEnrichment = _rowData.IndPrecursorEnrichment.Value,
                                   Turnover = _rowData.IndTurnover.Value,
                                   Score = _rowData.IndTurnoverScore.Value,
                               };
                } 
            }
            public TurnoverResult AverageTurnover
            {
                get
                {
                    if (!_rowData.AvgTurnover.HasValue || !_rowData.AvgPrecursorEnrichment.HasValue || !_rowData.AvgTurnoverScore.HasValue) {
                        return null;
                    }
                    return new TurnoverResult
                               {
                                   PrecursorEnrichment = _rowData.AvgPrecursorEnrichment.Value,
                                   Turnover = _rowData.AvgTurnover.Value,
                                   Score = _rowData.AvgTurnoverScore.Value,
                               };
                }
            }
                public ValidationStatus Status {get { return _rowData.ValidationStatus;}}
                public int PsmCount { get { return _rowData.PsmCount;}}
                public IntegrationNote PeakIntegrationNote { get { return _rowData.IntegrationNote; }}
        }
    }
}
