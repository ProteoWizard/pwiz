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
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.MsData;
using pwiz.Topograph.ui.DataBinding;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class ResultsPerReplicateForm : WorkspaceForm
    {
        public ResultsPerReplicateForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            bool hasTimePoints = Workspace.MsDataFiles.Count(f => f.TimePoint != null) != 0;
            bool hasCohorts = Workspace.MsDataFiles.Count(f => f.Cohort != null) != 0;
            bool hasSamples = Workspace.MsDataFiles.Count(f => f.Sample != null) != 0;
            bool hasTracerDefs = Workspace.GetTracerDefs().Count != 0;
            bool hasOneTracerDef = Workspace.GetTracerDefs().Count == 1;
            var defaultColumns = new List<ColumnSpec>
                                     {
                                         new ColumnSpec().SetName("Accept"),
                                         new ColumnSpec().SetName("Peptide"),
                                         new ColumnSpec().SetName("DataFile.Name").SetCaption("DataFile"),
                                         new ColumnSpec().SetName("Area"),
                                     };
            if (hasTracerDefs)
            {
                defaultColumns.Add(new ColumnSpec().SetName("TracerPercent"));
            }
            defaultColumns.Add(new ColumnSpec().SetName("DeconvolutionScore"));
            if (hasCohorts)
            {
                defaultColumns.Add(new ColumnSpec().SetName("DataFile.Cohort"));
            }
            if (hasTimePoints)
            {
                defaultColumns.Add(new ColumnSpec().SetName("DataFile.TimePoint"));
            }
            if (hasSamples)
            {
                defaultColumns.Add(new ColumnSpec().SetName("DataFile.Sample"));
            }
            if (hasTracerDefs)
            {
                defaultColumns.AddRange(new[]{new ColumnSpec().SetName("IndividualTurnover.PrecursorEnrichment").SetCaption("Ind Precursor Enrichment"),
                                             new ColumnSpec().SetName("IndividualTurnover.Turnover").SetCaption("Ind Turnover"),
                                             new ColumnSpec().SetName("IndividualTurnover.Score").SetCaption("Ind Turnover Score"),
                });
            }
            defaultColumns.AddRange(new[]{new ColumnSpec().SetName("Peptide.ProteinName").SetCaption("Protein"),
                                             new ColumnSpec().SetName("Peptide.ProteinDescription"),
                                             });
            if (hasOneTracerDef)
            {
                defaultColumns.AddRange(new[]{new ColumnSpec().SetName("AverageTurnover.PrecursorEnrichment").SetCaption("Avg Precursor Enrichment"),
                                             new ColumnSpec().SetName("AverageTurnover.Turnover").SetCaption("Avg Turnover"),
                                             new ColumnSpec().SetName("AverageTurnover.Score").SetCaption("Avg Turnover Score"),
            });
            }
            defaultColumns.AddRange(new[]{                                             new ColumnSpec().SetName("Status"),
                                             new ColumnSpec().SetName("PsmCount"),
                                             new ColumnSpec().SetName("PeakIntegrationNote"),});
            var defaultViewSpec = new ViewSpec()
                .SetName("default")
                .SetColumns(defaultColumns);
            bindingSourceResults.SetViewContext(new TopographViewContext(workspace, typeof (PerReplicateResult), new PerReplicateResult[0], new[] {defaultViewSpec}));
        }

        private void BtnRequeryOnClick(object sender, EventArgs e)
        {
            var calculator = new HalfLifeCalculator(Workspace, HalfLifeSettings.Default)
            {
                MaxResults = MaxResults,
            };
            using (var longWaitDialog = new LongWaitDialog(TopLevelControl, "Calculating Half Lives"))
            {
                var longOperationBroker = new LongOperationBroker(calculator.Run, longWaitDialog);
                if (!longOperationBroker.LaunchJob())
                {
                    return;
                }
            }
            UpdateRows(calculator);
        }

     
        private void UpdateRows(HalfLifeCalculator halfLifeCalculator)
        {
            var resultRows = halfLifeCalculator.RowDatas.Select(rd => new PerReplicateResult(halfLifeCalculator.ComputeAvgTurnover(rd))).ToArray();
            bindingSourceResults.RowSource = resultRows;
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

        public class PerReplicateResult
        {
            private readonly HalfLifeCalculator.ProcessedRowData _rowData;

            public PerReplicateResult(HalfLifeCalculator.ProcessedRowData rowData)
            {
                _rowData = rowData;
            }

            public bool Accept
            {
                get { return null == _rowData.RejectReason; }
            }

            public LinkValue<Peptide> Peptide
            {
                get
                {
                    return new LinkValue<Peptide>(_rowData.RawRowData.Peptide, (o, e) => PeptideFileAnalysisFrame
                                                                                             .ShowPeptideFileAnalysis(
                                                                                                 _rowData.RawRowData
                                                                                                         .Peptide
                                                                                                         .Workspace,
                                                                                                 _rowData.RawRowData
                                                                                                         .PeptideFileAnalysisId));
                }
            }

            public MsDataFile DataFile
            {
                get { return _rowData.RawRowData.MsDataFile; }
            }

            public double? Area
            {
                get { return _rowData.RawRowData.AreaUnderCurve; }
            }

            public double? TracerPercent
            {
                get { return _rowData.RawRowData.TracerPercent; }
            }

            public double? DeconvolutionScore
            {
                get { return _rowData.RawRowData.DeconvolutionScore; }
            }

            public TurnoverResult IndividualTurnover
            {
                get
                {
                    if (!_rowData.RawRowData.IndTurnover.HasValue ||
                        !_rowData.RawRowData.IndPrecursorEnrichment.HasValue ||
                        !_rowData.RawRowData.IndTurnoverScore.HasValue)
                    {
                        return null;
                    }
                    return new TurnoverResult
                        {
                            PrecursorEnrichment = _rowData.RawRowData.IndPrecursorEnrichment.Value,
                            Turnover = _rowData.RawRowData.IndTurnover.Value,
                            Score = _rowData.RawRowData.IndTurnoverScore.Value,
                        };
                }
            }

            public TurnoverResult AverageTurnover
            {
                get
                {
                    if (!_rowData.Turnover.HasValue || !_rowData.CurrentPrecursorPool.HasValue ||
                        !_rowData.TurnoverScore.HasValue)
                    {
                        return null;
                    }
                    return new TurnoverResult
                        {
                            PrecursorEnrichment = _rowData.CurrentPrecursorPool.Value,
                            Turnover = _rowData.Turnover.Value,
                            Score = _rowData.TurnoverScore.Value,
                        };
                }
            }

            public ValidationStatus Status
            {
                get { return _rowData.RawRowData.ValidationStatus; }
            }

            public int PsmCount
            {
                get { return _rowData.RawRowData.PsmCount; }
            }

            public IntegrationNote PeakIntegrationNote
            {
                get { return _rowData.RawRowData.IntegrationNote; }
            }

            public double? TotalIonCurrent
            {
                get
                {
                    if (!_rowData.RawRowData.StartTime.HasValue || !_rowData.RawRowData.EndTime.HasValue)
                    {
                        return null;
                    }
                    return _rowData.RawRowData.MsDataFile.GetTotalIonCurrent(_rowData.RawRowData.StartTime.Value,
                                                                             _rowData.RawRowData.EndTime.Value);
                }
            }

            public double? PeakStart { get { return _rowData.RawRowData.StartTime; } }
            public double? PeakEnd { get { return _rowData.RawRowData.EndTime; } }
        }
    }
}
