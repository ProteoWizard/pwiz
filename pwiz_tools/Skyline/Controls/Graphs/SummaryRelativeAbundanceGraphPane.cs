/*
 * Original author: Henry Sanford <henrytsanford .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;
using pwiz.Skyline.Util.Extensions;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace pwiz.Skyline.Controls.Graphs
{

    public abstract class SummaryRelativeAbundanceGraphPane : SummaryBarGraphPaneBase
    {

        protected GraphData _graphData;
        private SkylineDataSchema _schema;
        public bool AnyProteomic;
        public bool AnyMolecules;
        public SrmDocument Document;
        private bool _areaProteinTargets;
        private bool _excludePeptideLists;
        private bool _excludeStandards;
        private readonly List<DotPlotUtil.LabeledPoint> _labeledPoints;
        public bool ShowingFormattingDlg { get; set; }
        public IList<MatchRgbHexColor> ColorRows { get; set; }
        protected SummaryRelativeAbundanceGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
            var xAxisTitle =
                Helpers.PeptideToMoleculeTextMapper.Translate(GraphsResources.SummaryIntensityGraphPane_SummaryIntensityGraphPane_Protein_Rank,
                    graphSummary.DocumentUIContainer.DocumentUI.DocumentType);
            XAxis.Title.Text = xAxisTitle;
            XAxis.Type = AxisType.Linear;
            Document = graphSummary.DocumentUIContainer.DocumentUI;
            XAxis.Scale.Max = Document.MoleculeGroups.Count();
            AnyMolecules = Document.HasSmallMolecules;
            AnyProteomic = Document.HasPeptides;
            _areaProteinTargets = Settings.Default.AreaProteinTargets;
            _excludePeptideLists = Settings.Default.ExcludePeptideListsFromAbundanceGraph;
            _excludeStandards = Settings.Default.ExcludeStandardsFromAbundanceGraph;
            ColorRows = new List<MatchRgbHexColor>();
            var container = new MemoryDocumentContainer();
            container.SetDocument(Document, null);
            _schema = new SkylineDataSchema(container, DataSchemaLocalizer.INVARIANT);
            _labeledPoints = new List<DotPlotUtil.LabeledPoint>();
        }

        protected override int SelectedIndex
        {
            get { return _graphData != null ? _graphData.SelectedIndex : -1; }
        }

        protected override IdentityPath GetIdentityPath(CurveItem curveItem, int barIndex)
        {
            var pointData = (GraphPointData)curveItem[barIndex].Tag;
            return pointData.Peptide != null ? pointData.Peptide.IdentityPath : pointData.Protein.IdentityPath;
        }

        private bool IsDocumentChanged(SrmDocument docNew)
        {
            var documentChanged = Document != null && !ReferenceEquals(docNew, Document);
            if (documentChanged)
            {
                Document = docNew;
            }

            return documentChanged;
        }

        /// <summary>
        /// Have any of the settings relevant to this graph pane changed since the last update?
        /// </summary>
        /// <returns>True if relevant settings have changed, false if not</returns>
        private bool IsAbundanceGraphSettingsChanged()
        {
            var settingsChanged = false;
            if (Settings.Default.AreaProteinTargets != _areaProteinTargets)
            {
                _areaProteinTargets = Settings.Default.AreaProteinTargets;
                settingsChanged = true;
            }
            if (Settings.Default.ExcludePeptideListsFromAbundanceGraph != _excludePeptideLists)
            {
                _excludePeptideLists = Settings.Default.ExcludePeptideListsFromAbundanceGraph;
                settingsChanged = true;
            }
            if (Settings.Default.ExcludeStandardsFromAbundanceGraph != _excludeStandards)
            {
                _excludeStandards = Settings.Default.ExcludeStandardsFromAbundanceGraph;
                settingsChanged = true;
            }
            return settingsChanged;
        }

        public void ShowFormattingDialog()
        {
            var copy = ColorRows.Select(r => (MatchRgbHexColor)r.Clone()).ToList();
            ShowingFormattingDlg = true;
            GraphSummary.ShowFormattingDlg = false;
            using (var dlg = new VolcanoPlotFormattingDlg(this, copy, 
                       _graphData.PointPairList.Select(pointPair => (GraphPointData)pointPair.Tag).ToArray(), 
                       rows  =>
                       {
                           ColorRows = rows;
                           GraphSummary.UpdateUI();
                       }))
            {
                if (dlg.ShowDialog(Program.MainWindow) == DialogResult.OK)
                    UpdateGraph(false);
            }

            ShowingFormattingDlg = false;
        }

        public override bool HandleMouseDownEvent(ZedGraphControl sender, MouseEventArgs mouseEventArgs)
        {
            var ctrl = Control.ModifierKeys.HasFlag(Keys.Control); //CONSIDER allow override of modifier keys?
            int iNearest;
            var axis = GetNearestXAxis(sender, mouseEventArgs);
            if (axis != null)
            {
                iNearest = (int)axis.Scale.ReverseTransform(mouseEventArgs.X - axis.MajorTic.Size);
                if (iNearest < 0)
                {
                    return false;
                }
                ChangeSelection(iNearest, GraphSummary.StateProvider.SelectedPath, ctrl);
                return true;
            }
            if (!FindNearestPoint(new PointF(mouseEventArgs.X, mouseEventArgs.Y), out var nearestCurve, out iNearest))
            {
                return false;
            }
            var identityPath = GetIdentityPath(nearestCurve, iNearest);
            if (identityPath == null)
            {
                return false;
            }
            ChangeSelection(iNearest, identityPath, ctrl);
            return true;
        }

        private void ChangeSelection(int selectedIndex, IdentityPath identityPath, bool ctrl)
        {
            if (ctrl)
            {
                DotPlotUtil.MultiSelect(GraphSummary.Window, identityPath);
            }
            else
            {
                ChangeSelection(selectedIndex, identityPath);
            }
        }

        protected override void ChangeSelection(int selectedIndex, IdentityPath identityPath)
        {
            
            if (0 <= selectedIndex && selectedIndex < _graphData.XScalePaths.Length)
            {
                GraphSummary.StateProvider.SelectedPath = identityPath;
            }
        }

        public override void UpdateGraph(bool selectionChanged)
        {
            PeptideGroupDocNode selectedProtein = null;
            GraphSummary.Window ??= Program.MainWindow;
            Clear();
            var selectedTreeNode = GraphSummary.StateProvider.SelectedNode as SrmTreeNode;
            if (selectedTreeNode != null)
            {
                var proteinTreeNode = selectedTreeNode.GetNodeOfType<PeptideGroupTreeNode>();
                if (proteinTreeNode != null)
                {
                    selectedProtein = proteinTreeNode.DocNode;
                }
            }

            var isDocumentChanged = false;
            if (GraphSummary.Window != null)
            {
                isDocumentChanged = IsDocumentChanged(GraphSummary.Window.Document);
            }

            // Only create graph data (and recalculate abundances)
            // if settings have changed, the document has changed, or if it
            // is not yet created
            if (_graphData?.GraphPointList == null ||
                isDocumentChanged ||
                IsAbundanceGraphSettingsChanged())
            {
                _graphData = CreateGraphData(_schema);
            }
            // Calculate y values and order which can change based on the
            // replicate display option or the show CV option
            _graphData.CalcDataPositions(GraphSummary.ResultsIndex, selectedProtein);

            // For proper z-order, add the selected points, then the matched points, then the unmatched points
            var selectedPoints = new PointPairList();
            if (ShowSelection)
            {
                foreach (var point in from point in _graphData.PointPairList let 
                             pointData = (GraphPointData)point.Tag where 
                             DotPlotUtil.IsTargetSelected(GraphSummary.Window, pointData.Peptide, pointData.Protein) select 
                             point)
                {
                    selectedPoints.Add(point);
                }
                AddPoints(new PointPairList(selectedPoints), GraphSummary.ColorSelected, DotPlotUtil.PointSizeToFloat(PointSize.large), true, PointSymbol.Circle, true);
            }
            var pointList = _graphData.PointPairList;
            // For each valid match expression specified by the user
            foreach (var colorRow in ColorRows.Where(r => r.MatchExpression != null))
            {
                var matchedPoints = pointList.Where(p =>
                {
                    var pointData = (GraphPointData)p.Tag;
                    return colorRow.MatchExpression.Matches(Document, pointData.Protein, pointData.Peptide, null, null) && !selectedPoints.Contains(p);
                }).ToArray();

                if (matchedPoints.Any())
                {
                    AddPoints(new PointPairList(matchedPoints), colorRow.Color, DotPlotUtil.PointSizeToFloat(colorRow.PointSize), colorRow.Labeled, colorRow.PointSymbol);
                }
            }
            AddPoints(new PointPairList(pointList), Color.Gray, DotPlotUtil.PointSizeToFloat(PointSize.normal), false, PointSymbol.Circle);
            UpdateAxes();
            DotPlotUtil.AdjustLabelLocations(_labeledPoints, GraphSummary.GraphControl.GraphPane.YAxis.Scale, GraphSummary.GraphControl.GraphPane.Rect.Height);
            if (GraphSummary.ShowFormattingDlg && !ShowingFormattingDlg)
            {
                ShowFormattingDialog();
            }
        }

        private void AddPoints(PointPairList points, Color color, float size, bool labeled, PointSymbol pointSymbol, bool selected = false)
        {
            var symbolType = DotPlotUtil.PointSymbolToSymbolType(pointSymbol);

            LineItem lineItem;
            if (DotPlotUtil.HasOutline(pointSymbol))
            {
                lineItem = new LineItem(null, points, Color.Black, symbolType)
                {
                    Line = { IsVisible = false },
                    Symbol = { Border = { IsVisible = false }, Fill = new Fill(color), Size = size, IsAntiAlias = true }
                };
            }
            else
            {
                lineItem = new LineItem(null, points, Color.Black, symbolType)
                {
                    Line = { IsVisible = false },
                    Symbol = { Border = { IsVisible = true, Color = color }, Size = size, IsAntiAlias = true }
                };
            }

            if (labeled)
            {
                foreach (var point in points)
                {
                    var pointData = point.Tag as GraphPointData;
                    if (pointData == null)
                    {
                        continue;
                    }
                    var label = DotPlotUtil.CreateLabel(point, pointData.Protein, pointData.Peptide, color, size);
                    _labeledPoints.Add(new DotPlotUtil.LabeledPoint(point, label, selected));
                    GraphObjList.Add(label);
                }
            }
            CurveList.Add(lineItem);
        }

        protected abstract GraphData CreateGraphData(SkylineDataSchema schema);

        protected virtual void UpdateAxes()
        {
            if (AnyMolecules)
            {
                XAxis.Title.Text = GraphsResources.SummaryRelativeAbundanceGraphPane_UpdateAxes_Molecule_Rank;
            }
            else
            {
                XAxis.Title.Text = Settings.Default.AreaProteinTargets ? GraphsResources.SummaryIntensityGraphPane_SummaryIntensityGraphPane_Protein_Rank : GraphsResources.AreaPeptideGraphPane_UpdateAxes_Peptide_Rank;
            }
            const double xAxisGrace = 0;
            XAxis.Scale.MaxGrace = xAxisGrace;
            XAxis.Scale.MinGrace = xAxisGrace;
            YAxis.Scale.MinGrace = xAxisGrace;
            YAxis.Scale.MaxGrace = xAxisGrace;
            YAxis.Scale.MaxAuto = true;
            YAxis.Scale.MinAuto = true;
            XAxis.Scale.MaxAuto = true;
            XAxis.Scale.MinAuto = true;
            if (Settings.Default.AreaLogScale )
            {
                YAxis.Title.Text = TextUtil.SpaceSeparate(GraphsResources.SummaryPeptideGraphPane_UpdateAxes_Log, YAxis.Title.Text);
                YAxis.Type = AxisType.Log;
            }
            else
            {
                YAxis.Type = AxisType.Linear;
                if (_graphData.MinY.HasValue)
                {
                    if (!IsZoomed && !YAxis.Scale.MinAuto)
                        YAxis.Scale.MinAuto = true;
                }
                else
                {
                    YAxis.Scale.MinAuto = false;
                    FixedYMin = YAxis.Scale.Min = 0;
                    YAxis.Scale.Max = _graphData.MaxY * 1.05;
                }
            }
            var aggregateOp = GraphValues.AggregateOp.FromCurrentSettings();
            if (aggregateOp.Cv)
            {
                YAxis.Title.Text = aggregateOp.AnnotateTitle(YAxis.Title.Text);
            }

            if (!_graphData.MinY.HasValue && aggregateOp.Cv)
            {
                if (_graphData.MaxCvSetting != 0)
                {
                    YAxis.Scale.MaxAuto = false;
                    YAxis.Scale.Max = _graphData.MaxCvSetting;
                }
                else if (!IsZoomed && !YAxis.Scale.MaxAuto)
                {
                    YAxis.Scale.MaxAuto = true;
                }
            }
            else if (_graphData.MaxValueSetting != 0 || _graphData.MinValueSetting != 0)
            {
                if (_graphData.MaxValueSetting != 0)
                {
                    YAxis.Scale.MaxAuto = false;
                    YAxis.Scale.Max = _graphData.MaxValueSetting;
                }
                if (_graphData.MinValueSetting != 0)
                {
                    YAxis.Scale.MinAuto = false;
                    YAxis.Scale.Min = _graphData.MinValueSetting;
                    if (!_graphData.MinY.HasValue)
                        FixedYMin = YAxis.Scale.Min;
                }
            }

            AxisChange();
        }

        private static bool ContainsStandards(PeptideGroupDocNode nodeGroupPep)
        {
            return nodeGroupPep.Children.Cast<PeptideDocNode>().Any(IsStandard);
        }

        private static bool IsStandard(PeptideDocNode pepDocNode)
        {
            return pepDocNode.GlobalStandardType != null;
        }

        public abstract class GraphData : Immutable
        {
            // ReSharper disable PossibleMultipleEnumeration
            protected GraphData(SrmDocument document, SkylineDataSchema schema, bool anyMolecules)
            {
                // Build the list of points to show.
                var listPoints = new List<GraphPointData>();
                foreach (var nodeGroupPep in document.MoleculeGroups)
                {
                    if (nodeGroupPep.IsPeptideList && Settings.Default.ExcludePeptideListsFromAbundanceGraph &&
                        !anyMolecules)
                    {
                        continue;
                    }

                    if (Settings.Default.ExcludeStandardsFromAbundanceGraph && ContainsStandards(nodeGroupPep))
                    {
                        continue;
                    }

                    if (Settings.Default.AreaProteinTargets && !anyMolecules)
                    {
                        var path = new IdentityPath(IdentityPath.ROOT, nodeGroupPep.PeptideGroup);
                        var protein = new Protein(schema, path);
                        listPoints.Add(new GraphPointData(protein));
                    }
                    else
                    {
                        foreach (PeptideDocNode nodePep in nodeGroupPep.Children)
                        {
                            var pepPath = new IdentityPath(nodeGroupPep.PeptideGroup,
                                nodePep.Peptide);
                            foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                            {
                                var path = new IdentityPath(nodeGroupPep.PeptideGroup,
                                    nodePep.Peptide, nodeGroup.TransitionGroup);
                                var peptide = new Peptide(schema, pepPath);
                                listPoints.Add(new GraphPointData(peptide, nodeGroup, path));
                            }
                        }
                    }
                }
                GraphPointList = listPoints;
            }
            // ReSharper restore PossibleMultipleEnumeration

            public void CalcDataPositions(int iResult, PeptideGroupDocNode selectedProtein)
            {
                // Init calculated values
                var xscalePaths = new List<IdentityPath>();
                double maxY = 0;
                var minY = double.MaxValue;
                var selectedIndex = -1;

                var pointPairList = new PointPairList();

                foreach (var dataPoint in GraphPointList)
                {
                    double groupMaxY = 0;
                    var groupMinY = double.MaxValue;
                    // ReSharper disable DoNotCallOverridableMethodsInConstructor
                    var pointPair = CreatePointPair(dataPoint, ref groupMaxY, ref groupMinY, iResult);
                    // ReSharper restore DoNotCallOverridableMethodsInConstructor
                    pointPairList.Add(pointPair);
                    maxY = Math.Max(maxY, groupMaxY);
                    minY = Math.Min(minY, groupMinY);
                }

                pointPairList.Sort(CompareYValues);
                for (var i = 0; i < pointPairList.Count; i++)
                {
                    // Save the selected index and its y extent
                    var dataPoint = (GraphPointData)pointPairList[i].Tag;
                    if (ReferenceEquals(selectedProtein, dataPoint.NodePepGroup))
                    {
                        selectedIndex = i;
                    }
                    // 1-index the proteins
                    pointPairList[i].X = i + 1;
                    xscalePaths.Add(dataPoint.IdentityPath);
                }
                PointPairList = pointPairList;
                XScalePaths = xscalePaths.ToArray();
                SelectedIndex = selectedIndex - 1;
                MaxY = maxY;
                if (minY != double.MaxValue)
                {
                    MinY = minY;
                }
            }

            private static int CompareYValues(PointPair p1, PointPair p2)
            {
                return Comparer.Default.Compare(p2.Y, p1.Y);
            }
            public List<GraphPointData> GraphPointList;
            public PointPairList PointPairList { get; private set; }
            public IdentityPath[] XScalePaths { get; private set; }
            public double MaxY { get; private set; }
            public double? MinY { get; private set; }
            public int SelectedIndex { get; private set; }

            public virtual double MaxValueSetting { get { return 0; } }
            public virtual double MinValueSetting { get { return 0; } }
            public virtual double MaxCvSetting { get { return 0; } }

            protected virtual PointPair CreatePointPair(GraphPointData pointData, ref double maxY, ref double minY, int? resultIndex)
            {
                var yValue = GetY(pointData, resultIndex);
                var pointPair = new PointPair(0, yValue)
                    { Tag = pointData };
                maxY = Math.Max(maxY, pointPair.Y);
                minY = Math.Min(minY, pointPair.Y);
                return pointPair;
            }

            private static double GetY(GraphPointData pointData, int? resultIndex)
            {
                if (RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.single)
                {
                    if (resultIndex != null && pointData.Areas.Count > resultIndex)
                    {
                        var replicate = pointData.Areas[resultIndex.Value];
                        if (replicate != null)
                        {
                            return replicate.Value;
                        }
                    }
                }
                var listValues = pointData.Areas.Where(a => a != null).Select(d => d.Value);
                var statValues = new Statistics(listValues);
                var cv = statValues.StdDev() / statValues.Mean();
                if (Settings.Default.ShowPeptideCV)
                {
                    return cv;
                }
                return RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.best ? statValues.Max() :
                    statValues.Mean();
            }
        }

        public class GraphPointData 
        {
            public GraphPointData(Protein protein)
            {
                Areas = new List<double?>();
                NodePepGroup = protein.DocNode; 
                IdentityPath = new IdentityPath(IdentityPath.ROOT, NodePepGroup.PeptideGroup);
                SetAreas(protein.GetProteinAbundances());
                Protein = protein;
            }

            public GraphPointData(Peptide peptide, TransitionGroupDocNode nodeGroup, IdentityPath identityPath)
            {
                Areas = new List<double?>();
                Peptide = peptide;
                Protein = peptide.Protein;
                SetAreas(nodeGroup);
            }
            public PeptideGroupDocNode NodePepGroup { get; private set; }
            public Protein Protein { get; private set; }
            public Peptide Peptide { get; private set; }
            public List<double?> Areas { get; set; }
            public IdentityPath IdentityPath { get; set; }
            private void SetAreas(IDictionary<int, Protein.AbundanceValue> abundanceValues)
            {
                foreach (var abundanceValue in abundanceValues)
                {
                    double? abundance = null;
                    if (!abundanceValue.Value.Incomplete)
                    {
                        abundance = abundanceValue.Value.Abundance;
                    }
                    Areas.Add(abundance);
                }
            }

            private void SetAreas(TransitionGroupDocNode nodeGroup)
            {
                foreach (var chromInfo in nodeGroup.ChromInfos)
                {
                    Areas.Add(chromInfo.Area);
                }
            }
        }
    }
}
