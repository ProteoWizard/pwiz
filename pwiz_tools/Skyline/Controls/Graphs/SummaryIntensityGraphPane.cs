using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Graphs
{

    public abstract class SummaryIntensityGraphPane : SummaryBarGraphPaneBase
    {
        public static SummaryPeptideOrder PeptideOrder
        {
            get
            {
                return Helpers.ParseEnum(Settings.Default.AreaPeptideOrderEnum, SummaryPeptideOrder.area);
            }

            set { Settings.Default.AreaPeptideOrderEnum = value.ToString(); }
        }
        // TODO set all of these
        protected GraphData _graphData;
        public bool AnyProteomic;
        public bool AnyMolecules;
        public SrmDocument Document;
        private List<ProteinAbundanceBindingSource.ProteinAbundanceRow> _proteinAbundanceRows;
        private readonly List<DotPlotUtil.LabeledPoint> _labeledPoints;

        public GroupComparisonModel GroupComparisonModel;
        private GroupComparisonDef GroupComparisonDef
        {
            get { return GroupComparisonModel.GroupComparisonDef; }
        }

        protected SummaryIntensityGraphPane(GraphSummary graphSummary, PaneKey paneKey)
            : base(graphSummary)
        {
            PaneKey = paneKey;
            string xAxisTitle =
                Helpers.PeptideToMoleculeTextMapper.Translate(Resources.SummaryIntensityGraphPane_SummaryIntensityGraphPane_Protein_Rank,
                    graphSummary.DocumentUIContainer.DocumentUI.DocumentType);
            if (null != paneKey.IsotopeLabelType && !paneKey.IsotopeLabelType.IsLight)
            {
                xAxisTitle += @" (" + paneKey.IsotopeLabelType + @")";
            }
            XAxis.Title.Text = xAxisTitle;
            XAxis.Type = AxisType.Linear;
            Document = graphSummary.DocumentUIContainer.DocumentUI;
            XAxis.Scale.Max = Document.MoleculeGroups.Count();
            AnyMolecules = Document.HasSmallMolecules;
            AnyProteomic = Document.HasPeptides;
            GroupComparisonModel = new GroupComparisonModel(graphSummary.DocumentUIContainer, @"IntensityGraphGroupComparison");
            _labeledPoints = new List<DotPlotUtil.LabeledPoint>();
        }

        protected override int SelectedIndex
        {
            get { return _graphData != null ? _graphData.SelectedIndex : -1; }
        }

        protected override IdentityPath GetIdentityPath(CurveItem curveItem, int barIndex)
        {
            if (0 <= barIndex && barIndex < _graphData.XScalePaths.Length)
                return _graphData.XScalePaths[barIndex];
            return null;
        }

        protected void ShowFormattingDialog()
        {
            var proteinAbundanceRows = GetProteinAbundanceRows().ToArray();
            // This list will later be used as a BindingList, so we have to create a mutable clone
            var copy = GroupComparisonDef.ColorRows.Select(r => (MatchRgbHexColor)r.Clone()).ToList();
            var window = GraphSummary.Window;
            ShowingFormattingDlg = true;
            using (var dlg = new IntensityGraphFormattingDlg(this, copy, 
                       proteinAbundanceRows, 
                       rows  =>
                       {
                           //TODO can we just use ChangeColorRows here instead
                           EditGroupComparisonDlg.ChangeGroupComparisonDef(false, GroupComparisonModel, GroupComparisonDef.ChangeColorRows(rows));
                           UpdateGraph(false);
                       }))
            {
                if (dlg.ShowDialog(window) == DialogResult.OK)
                    UpdateGraph(false);
            }

            ShowingFormattingDlg = false;
        }

        private static string GetProteinName(PeptideGroupDocNode pepGroupDocNode, SrmDocument document)
        {
            if (pepGroupDocNode.IsProteomic)
            {
                var container = new MemoryDocumentContainer();
                container.SetDocument(document, null);
                var dataSchema = new SkylineDataSchema(container, DataSchemaLocalizer.INVARIANT);
                var path = new IdentityPath(IdentityPath.ROOT, pepGroupDocNode.PeptideGroup);
                var protein = new Protein(dataSchema, path);
                return GetProteinName(protein);
            }

            return pepGroupDocNode.Name;
        }
        // TODO consider what will happen when document changes in the background
        // TODO add replicate number as an argument?
        private List<ProteinAbundanceBindingSource.ProteinAbundanceRow> GetProteinAbundanceRows()
        {
            if (_proteinAbundanceRows == null)
            {
                var list = new List<ProteinAbundanceBindingSource.ProteinAbundanceRow>();
                var container = new MemoryDocumentContainer();
                container.SetDocument(Document, null);
                var dataSchema = new SkylineDataSchema(container, DataSchemaLocalizer.INVARIANT);
                foreach (var pepGroupDocNode in Document.MoleculeGroups)
                {
                    var path = new IdentityPath(IdentityPath.ROOT, pepGroupDocNode.PeptideGroup);
                    var protein = new Protein(dataSchema, path);
                    // if (!protein.GetProteinAbundances().TryGetValue(1, out var abundanceValue)) //TODO use the actual replicate number here
                    // {
                    //     continue;
                    // }
                    var proteinAbundances = protein.GetProteinAbundances();
                    var proteinAbundanceResult =
                        new ProteinAbundanceBindingSource.ProteinAbundanceResult(proteinAbundances[1].Abundance);
                    var groupIdentifier = GroupIdentifier.MakeGroupIdentifier(@"control");
                    var replicateCount = proteinAbundances.Count;
                    var row = new ProteinAbundanceBindingSource.ProteinAbundanceRow(protein, groupIdentifier,
                        replicateCount, proteinAbundanceResult,
                        new Dictionary<Replicate, ProteinAbundanceBindingSource.ReplicateRow>());
                    list.Add(row);
                }
                _proteinAbundanceRows = list;
            }

            return _proteinAbundanceRows;
        }
        protected override void ChangeSelection(int selectedIndex, IdentityPath identityPath)
        {
            if (0 <= selectedIndex && selectedIndex < _graphData.XScalePaths.Length)
                GraphSummary.StateProvider.SelectedPath = _graphData.XScalePaths[selectedIndex];
        }

        private bool IsSelected()
        {
            var window = GraphSummary.Window;
            return window != null ;
        }
        public override void UpdateGraph(bool selectionChanged)
        {
            Clear();

            PeptideGroupDocNode selectedProtein = null;
            var selectedTreeNode = GraphSummary.StateProvider.SelectedNode as SrmTreeNode;
            if (selectedTreeNode != null)
            {
                var proteinTreeNode = selectedTreeNode.GetNodeOfType<PeptideGroupTreeNode>();
                if (proteinTreeNode != null)
                {
                    selectedProtein = proteinTreeNode.DocNode;
                }
            }

            var document = GraphSummary.DocumentUIContainer.DocumentUI;

            var displayType = GraphChromatogram.GetDisplayType(document, selectedTreeNode);
            var rows = GetProteinAbundanceRows();
            _graphData = CreateGraphData(document, selectedProtein, displayType, rows);

            // For proper z-order, add the selected points first, then the matched points, then the unmatched points
            foreach (var pointPairList in _graphData.PointPairLists)
            {
                var pointList = pointPairList;
                // Foreach valid match expression specified by the user
                foreach (var colorRow in GroupComparisonDef.ColorRows.Where(r => r.MatchExpression != null))
                {
                    var row = colorRow;
                    var matchedPoints = pointPairList.Where(p =>
                    {
                        var proteinAbundanceRow = (ProteinAbundanceBindingSource.ProteinAbundanceRow)p.Tag;
                        return row.MatchExpression.Matches(Document, proteinAbundanceRow.Protein, 
                            proteinAbundanceRow.ProteinAbundanceResult); //TODO remove abundance argument
                    }).ToArray();

                    if (matchedPoints.Any())
                    {
                        AddPoints(new PointPairList(matchedPoints), colorRow.Color, DotPlotUtil.PointSizeToFloat(row.PointSize), row.Labeled, row.PointSymbol);
                        pointList = new PointPairList(pointPairList.Except(matchedPoints).ToArray());
                    }
                }
                AddPoints(new PointPairList(pointList), Color.Gray, DotPlotUtil.PointSizeToFloat(PointSize.normal), false, PointSymbol.Circle);
            }

            if (ShowSelection && SelectedIndex != -1)
            {
                var selectedY = (_graphData.SelectedMaxY + _graphData.SelectedMinY) / 2;
                GraphObjList.Add(new LineObj(Color.Black, SelectedIndex + 1, 0, SelectedIndex+ 1, selectedY)
                {
                    IsClippedToChartRect = true,
                    Line = new Line() { Width = 2, Color = Color.Black, Style = DashStyle.Dash }
                });
                GraphObjList.Add(new TextObj(_graphData.SelectedName, SelectedIndex +1, selectedY));
            }

            UpdateAxes();
            if (GraphSummary.Window != null && !ShowingFormattingDlg)
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
                    var label = CreateLabel(point, color, size);
                    _labeledPoints.Add(new DotPlotUtil.LabeledPoint(point, label, selected));
                    GraphObjList.Add(label);
                }
            }

            CurveList.Add(lineItem);
        }

        private static TextObj CreateLabel(PointPair point, Color color, float size)
        {
            var row = point.Tag as ProteinAbundanceBindingSource.ProteinAbundanceRow;
            if (row == null)
                return null;
            var text = GetProteinName(row.Protein);

            var textObj = new TextObj(text, point.X, point.Y, CoordType.AxisXYScale, AlignH.Center, AlignV.Bottom)
            {
                IsClippedToChartRect = true,
                FontSpec = DotPlotUtil.CreateFontSpec(color, size),
                ZOrder = ZOrder.A_InFront
            };

            return textObj;
        }

        private static string GetProteinName(Protein protein)
        {
            var displayMode = Helpers.ParseEnum(Settings.Default.ShowPeptidesDisplayMode,
                ProteinMetadataManager.ProteinDisplayMode.ByName);
            return MatchExpression.GetProteinText(protein, displayMode);
        }

        //TODO centralize with volcano plot


        protected abstract GraphData CreateGraphData(SrmDocument document, PeptideGroupDocNode selectedProtein, DisplayTypeChrom displayType, List<ProteinAbundanceBindingSource.ProteinAbundanceRow> rows);

        protected virtual void UpdateAxes()
        {
            UpdateAxes(true);
        }

        protected void UpdateAxes(bool allowLogScale)
        {
            if (Settings.Default.AreaLogScale && allowLogScale)
            {
                YAxis.Title.Text = TextUtil.SpaceSeparate(Resources.SummaryPeptideGraphPane_UpdateAxes_Log, YAxis.Title.Text);
                YAxis.Type = AxisType.Log;
                YAxis.Scale.MinAuto = true;
                YAxis.Scale.MaxGrace = 0.1;
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
                YAxis.Title.Text = aggregateOp.AnnotateTitle(YAxis.Title.Text);

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
            else if (!IsZoomed && !YAxis.Scale.MaxAuto)
            {
                YAxis.Scale.MaxAuto = true;
            }

            AxisChange();
        }

        public abstract class GraphData : Immutable
        {
            // ReSharper disable PossibleMultipleEnumeration
            protected GraphData(SrmDocument document, PeptideGroupDocNode selectedProtein,
                int? iResult, DisplayTypeChrom displayType,
                PaneKey paneKey, List<ProteinAbundanceBindingSource.ProteinAbundanceRow> rows)
            {

                int pointListCount = 0;
                var dictTypeToSet = new Dictionary<IsotopeLabelType, int>();
                // Figure out how many point lists to create
                bool displayTotals = (displayType == DisplayTypeChrom.total);
                if (displayTotals)
                {
                    foreach (var nodeGroup in document.MoleculeTransitionGroups)
                    {
                        if (!paneKey.IncludesTransitionGroup(nodeGroup))
                        {
                            continue;
                        }
                        IsotopeLabelType labelType = nodeGroup.TransitionGroup.LabelType;
                        if (!dictTypeToSet.ContainsKey(labelType))
                            dictTypeToSet.Add(labelType, pointListCount++);
                    }
                }

                // Build the list of points to show.
                var listPoints = new List<GraphPointData>();
                foreach (var row in rows)
                {
                    var nodeGroupPep = row.Protein.DocNode;
                    if (AreaGraphController.AreaScope == AreaScope.protein)
                    {
                        if (!ReferenceEquals(nodeGroupPep, selectedProtein))
                            continue;
                    }
                    var graphPointData = new GraphPointData(row);
                    listPoints.Add(graphPointData);
                }

                // Sort into correct order
                listPoints.Sort(CompareGroupAreas);

                // Init calculated values
                var pointPairLists = new List<PointPairList>();
                var labels = new List<string>();
                var xscalePaths = new List<IdentityPath>();
                double maxY = 0;
                double minY = double.MaxValue;
                int selectedIndex = -1;

                for (int i = 0; i < pointListCount; i++)
                {
                    pointPairLists.Add(new PointPairList());
                }

                foreach (var dataPoint in listPoints)
                {
                    var nodePep = dataPoint.NodePep;
                    var nodeGroup = dataPoint.NodeGroup;
                    int iGroup = labels.Count;

                    var label = iGroup.ToString();
                    labels.Add(label);
                    xscalePaths.Add(dataPoint.IdentityPath);
                    

                    double groupMaxY = 0;
                    double groupMinY = double.MaxValue;

                    // ReSharper disable DoNotCallOverridableMethodsInConstructor
                    int? resultIndex = iResult.HasValue && iResult >= 0 ? iResult : null;
                    if (RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.best && nodePep != null)
                    {
                        resultIndex = null;
                        int iBest = nodePep.BestResult;
                        if (iBest != -1)
                            resultIndex = iBest;
                    }
                    if (displayTotals)
                    {
                        var labelType = nodeGroup.TransitionGroup.LabelType;
                        if (dictTypeToSet.ContainsKey(labelType))
                        {
                            if (paneKey.IncludesTransitionGroup(nodeGroup)) //TODO rework logic to consider all transitions in protein
                            {
                                // var pointPair = CreatePointPair(iGroup, dataPoint.NodePepGroup,
                                //     ref groupMaxY, ref groupMinY,
                                //     resultIndex);
                                var pointPair = CreatePointPair(iGroup, dataPoint.Row, ref groupMaxY, ref groupMinY, resultIndex);
                                pointPairLists[dictTypeToSet[labelType]].Add(pointPair);
                            }
                            else
                            {
                                pointPairLists[dictTypeToSet[labelType]].Add(PointPairMissing(iGroup));
                            }
                        }
                    }
                    // ReSharper restore DoNotCallOverridableMethodsInConstructor

                    // Save the selected index and its y extent
                    if (ReferenceEquals(selectedProtein, dataPoint.NodePepGroup))
                    {
                        selectedIndex = labels.Count - 1;
                        SelectedY = dataPoint.AreaGroup;
                        SelectedName = GetProteinName(dataPoint.NodePepGroup, document);
                        SelectedMaxY = groupMaxY;
                        SelectedMinY = groupMinY;
                    }
                    // If multiple groups in the selection, make sure y extent is max of them
                    else if (selectedIndex == labels.Count - 1)
                    {
                        SelectedMaxY = Math.Max(groupMaxY, SelectedMaxY);
                        SelectedMinY = Math.Min(groupMinY, SelectedMinY);
                    }
                    maxY = Math.Max(maxY, groupMaxY);
                    minY = Math.Min(minY, groupMinY);
                }

                PointPairLists = pointPairLists;
                Labels = labels.ToArray();
                XScalePaths = xscalePaths.ToArray();
                SelectedIndex = selectedIndex - 1; //TODO find a better fix
                MaxY = maxY;
                if (minY != double.MaxValue)
                {
                    MinY = minY;
                }
            }
            // ReSharper restore PossibleMultipleEnumeration

            public IList<PointPairList> PointPairLists { get; private set; }
            public string[] Labels { get; private set; }
            public IdentityPath[] XScalePaths { get; private set; }
            public double MaxY { get; private set; }
            public double? MinY { get; private set; }
            public int SelectedIndex { get; private set; }
            public double SelectedY { get; private set; }
            public double SelectedMaxY { get; private set; }
            public double SelectedMinY { get; private set; }
            public string SelectedName { get; private set; }

            public virtual double MaxValueSetting { get { return 0; } }
            public virtual double MinValueSetting { get { return 0; } }
            public virtual double MaxCvSetting { get { return 0; } }

            private static int CompareGroupAreas(GraphPointData p1, GraphPointData p2)
            {
                return Comparer.Default.Compare(p2.AreaGroup, p1.AreaGroup);
            }

            protected static PointPair PointPairMissing(int iGroup)
            {
                return MeanErrorBarItem.MakePointPair(iGroup, PointPairBase.Missing, PointPairBase.Missing);
            }

            protected virtual PointPair CreatePointPairMissing(int iGroup)
            {
                return PointPairMissing(iGroup);
            }

            protected virtual PointPair CreatePointPair(int iGroup, ProteinAbundanceBindingSource.ProteinAbundanceRow row, ref double maxY, ref double minY, int? resultIndex)
            {
                var abundance = row.ProteinAbundanceResult.Abundance;
                var pointPair = MeanErrorBarItem.MakePointPair(iGroup, abundance, row);
                maxY = Math.Max(maxY, pointPair.Y);
                minY = Math.Min(minY, pointPair.Y);
                return pointPair;
            }

            // Create a point pair representing the abundance of a PeptideGroupDocNode
            // TODO multiple result indices representing the best replicate for each
            protected virtual PointPair CreatePointPair(int iGroup, PeptideGroupDocNode nodePeptideGroup,
                ref double maxY, ref double minY, int? resultIndex)
            {
                var transitionGroupAbundances = new List<double>();
                var transitionGroupVariances = new List<double>();
                foreach (PeptideDocNode nodePeptide in nodePeptideGroup.Children)
                {
                    foreach (TransitionGroupDocNode nodeTransitionGroup in nodePeptide.Children)
                    {
                        var listValues = new List<double>();
                        foreach (var chromInfo in nodeTransitionGroup.GetChromInfos(resultIndex)) // If result index is null we return all chrom infos
                        {
                            double? value = GetValue(chromInfo);
                            if (chromInfo.OptimizationStep == 0 && value.HasValue)
                                listValues.Add(value.Value);
                        }

                        if (listValues.Count == 0)
                        {
                            continue;
                        }
                        var statValues = new Statistics(listValues);
                        transitionGroupAbundances.Add(statValues.Mean()); // The abundance of the transition group
                        transitionGroupVariances.Add(statValues.Variance());
                    }
                }

                if (transitionGroupAbundances.Count == 0)
                {
                    return CreatePointPairMissing(iGroup);
                }
                var proteinAbundance = transitionGroupAbundances.Sum();
                var statTransitionGroupVariances = new Statistics(transitionGroupVariances);
                var proteinStdDev = Math.Sqrt(statTransitionGroupVariances.Mean());
                var pointPair = MeanErrorBarItem.MakePointPair(iGroup, proteinAbundance, proteinStdDev);
                maxY = Math.Max(maxY, MeanErrorBarItem.GetYTotal(pointPair));
                minY = Math.Min(minY, MeanErrorBarItem.GetYMin(pointPair));
                return pointPair;
            }

            protected abstract double? GetValue(TransitionGroupChromInfo chromInfo);

            protected abstract double GetValue(TransitionChromInfo info);
        }

        private class GraphPointData
        {
            public GraphPointData(PeptideGroupDocNode nodePepGroup)
            {
                NodePepGroup = nodePepGroup;
                IdentityPath = new IdentityPath(IdentityPath.ROOT, NodePepGroup.PeptideGroup);
                CalcStats(nodePepGroup);
            }
            public GraphPointData(ProteinAbundanceBindingSource.ProteinAbundanceRow row)
            {
                Row = row;
                NodePepGroup = row.Protein.DocNode;
                // TODO get rid of these variables
                NodePep = (PeptideDocNode)NodePepGroup.Children.First();
                NodeGroup = (TransitionGroupDocNode)NodePep.Children.First();
                IdentityPath = new IdentityPath(IdentityPath.ROOT, NodePepGroup.PeptideGroup);
                AreaGroup = row.ProteinAbundanceResult.Abundance;
            }
            public ProteinAbundanceBindingSource.ProteinAbundanceRow Row { get; private set; }
            public PeptideGroupDocNode NodePepGroup { get; private set; }
            public PeptideDocNode NodePep { get; private set; }
            public TransitionGroupDocNode NodeGroup { get; private set; }
            public IdentityPath IdentityPath { get; private set; }
            public double AreaGroup { get; private set; }
            //            public double AreaPepCharge { get; private set; }

            private void CalcStats(PeptideGroupDocNode nodePepGroup)
            {
                var areas = new List<double>();
                foreach (PeptideDocNode nodePep in nodePepGroup.Children)
                {
                    NodePep = nodePep;
                    foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                    {
                        double ? meanArea;
                        CalcStats(nodePep, nodeGroup, out meanArea);
                        areas.Add(meanArea ?? 0); // TODO deal with missing areas
                        NodeGroup = nodeGroup;
                    }
                }

                AreaGroup = areas.Sum(); //TODO is this the correct way to calculate area
            }
            // ReSharper disable SuggestBaseTypeForParameter
            private void CalcStats(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, out double? meanArea)
            // ReSharper restore SuggestBaseTypeForParameter
            {
                meanArea = null;
                foreach (TransitionGroupDocNode nodePepChild in nodePep.Children)
                {
                    double? meanTransitionGroupArea;
                    CalcStats(nodePepChild, out meanTransitionGroupArea);
                    if (!Equals(nodeGroup.TransitionGroup.PrecursorAdduct, nodePepChild.TransitionGroup.PrecursorAdduct))
                        continue;
                    if (ReferenceEquals(nodeGroup, nodePepChild))
                    {
                        meanArea = meanTransitionGroupArea ?? 0;
                    }
                }
                //                AreaPepCharge = (areas.Count > 0 ? new Statistics(areas).Mean() : 0);
            }

            private static void CalcStats(TransitionGroupDocNode nodeGroup, out double? meanArea)
            {
                var areas = new List<double>();
                foreach (var chromInfo in nodeGroup.ChromInfos)
                {
                    if (chromInfo.Area.HasValue)
                        areas.Add(chromInfo.Area.Value);
                }
                meanArea = null;
                if (areas.Count > 0)
                    meanArea = new Statistics(areas).Mean();
            }
        }
    }
}
