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
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Graphs
{

    public abstract class SummaryProteinExpressionGraphPane : SummaryBarGraphPaneBase
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
        public bool ShowingFormattingDlg { get; set; }
        public IList<MatchRgbHexColor> ColorRows { get; set; }
        private ReplicateDisplay ReplicateDisplayType { get; set; }

        protected SummaryProteinExpressionGraphPane(GraphSummary graphSummary, PaneKey paneKey)
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
            ColorRows = new List<MatchRgbHexColor>();
            ReplicateDisplayType = RTLinearRegressionGraphPane.ShowReplicate;
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
            var proteinAbundanceRows = GetProteinAbundanceResults().ToArray();
            // This list will later be used as a BindingList, so we have to create a mutable clone
            var copy = ColorRows.Select(r => (MatchRgbHexColor)r.Clone()).ToList();
            var window = GraphSummary.Window;
            ShowingFormattingDlg = true;
            using (var dlg = new VolcanoPlotFormattingDlg(this, copy, 
                       proteinAbundanceRows, 
                       rows  =>
                       {
                           //TODO can we just use ChangeColorRows here instead
                           ColorRows = rows;
                           UpdateGraph(false);
                       }))
            {
                if (dlg.ShowDialog(window) == DialogResult.OK)
                    UpdateGraph(false);
            }

            ShowingFormattingDlg = false;
        }

        private List<ProteinAbundanceResult> GetProteinAbundanceResults()
        {
            var list = new List<ProteinAbundanceResult>();
            var container = new MemoryDocumentContainer();
            container.SetDocument(Document, null);
            var dataSchema = new SkylineDataSchema(container, DataSchemaLocalizer.INVARIANT);
            foreach (var pepGroupDocNode in Document.MoleculeGroups)
            {
                var path = new IdentityPath(IdentityPath.ROOT, pepGroupDocNode.PeptideGroup);
                var protein = new Protein(dataSchema, path);
                var proteinAbundances = protein.GetProteinAbundances();
                var proteinAbundanceResult = new ProteinAbundanceResult(protein, proteinAbundances);
                proteinAbundanceResult.CalculateAbundance(GraphSummary.ResultsIndex); //TODO only do this when replicate changed
                list.Add(proteinAbundanceResult);
            }

            return list;
        }

        public class ProteinAbundanceResult
        {
            public Protein Protein { get; private set; }
            public IDictionary<int, Protein.AbundanceValue> AbundanceValues { get; private set; }
            public double CalculatedAbundance { get; private set; }
            public ProteinAbundanceResult(Protein protein, IDictionary<int, Protein.AbundanceValue> abundanceValues)
            {
                Protein = protein;
                AbundanceValues = abundanceValues;
            }

            public void CalculateAbundance(int replicateNumber)
            {
                if (RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.best)
                {
                    double maxAbundance = 0;
                    foreach (var replicate in AbundanceValues.Values)
                    {
                        if (replicate.Incomplete == false)
                        {
                            if (replicate.Abundance > maxAbundance)
                            {
                                maxAbundance = replicate.Abundance;
                            }
                        }
                    }

                    CalculatedAbundance = maxAbundance;
                } 
                else if (RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.all)
                {
                    double totalAbundance = 0;
                    int completeReplicates = 0;
                    foreach (var replicate in AbundanceValues.Values)
                    {
                        if (replicate.Incomplete == false)
                        {
                            totalAbundance += replicate.Abundance;
                            completeReplicates++;
                        }
                    }
                    CalculatedAbundance = totalAbundance / completeReplicates;
                    
                }
                else if (RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.single)
                {
                    if (AbundanceValues.TryGetValue(replicateNumber, out var replicate))
                    {
                        if (replicate.Incomplete == false)
                        {
                            CalculatedAbundance = replicate.Abundance;
                        }
                    }
                }
            }
        }
        public override bool HandleMouseDownEvent(ZedGraphControl sender, MouseEventArgs mouseEventArgs)
        {
            var ctrl = Control.ModifierKeys.HasFlag(Keys.Control); //TODO allow overide of modifier keys?
            CurveItem nearestCurve;
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
            if (!FindNearestPoint(new PointF(mouseEventArgs.X, mouseEventArgs.Y), out nearestCurve, out iNearest))
            {
                return false;
            }
            IdentityPath identityPath = GetIdentityPath(nearestCurve, iNearest);
            if (identityPath == null)
            {
                return false;
            }

            ChangeSelection(iNearest, identityPath, ctrl);
            return true;
        }

        private void ChangeSelection(int selectedIndex, IdentityPath identityPath, bool ctrl)
        {
            if (!ctrl)
            {
                ChangeSelection(selectedIndex, identityPath);
            }
            else
            {
                MultiSelect(identityPath);
            }
        }

        private void MultiSelect(IdentityPath identityPath)
        {
            var skylineWindow = GraphSummary.Window;
            if (skylineWindow == null)
                return;

            var list = skylineWindow.SequenceTree.SelectedPaths;
            if (GetSelectedPath(identityPath) == null)
            {
                list.Insert(0, identityPath);
                skylineWindow.SequenceTree.SelectedPaths = list;
                if (!IsPathSelected(skylineWindow.SelectedPath, identityPath))
                    skylineWindow.SequenceTree.SelectPath(identityPath);
            }
            skylineWindow.UpdateGraphPanes();
        }
        protected override void ChangeSelection(int selectedIndex, IdentityPath identityPath)
        {
            
            if (0 <= selectedIndex && selectedIndex < _graphData.XScalePaths.Length)
            {
                GraphSummary.StateProvider.SelectedPath = identityPath;
                //GraphSummary.StateProvider.SelectedPath = _graphData.XScalePaths[selectedIndex];
            }
        }

        // TODO can we centralize checking selection with the volcano plots logic?
        private bool IsSelected(Protein protein)
        {
            var docNode = (SkylineDocNode)protein; 
            var window = GraphSummary.Window;
            return window != null && GetSelectedPath(docNode.IdentityPath) != null;
        }

        private IdentityPath GetSelectedPath(IdentityPath identityPath)
        {
            var skylineWindow = GraphSummary.Window;
            return skylineWindow != null ? skylineWindow.SequenceTree.SelectedPaths.FirstOrDefault(p => IsPathSelected(p, identityPath)) : null;
        }

        public bool IsPathSelected(IdentityPath selectedPath, IdentityPath identityPath)
        {
            return selectedPath != null && identityPath != null &&
                   selectedPath.Depth <= (int)SrmDocument.Level.Molecules && identityPath.Depth <= (int)SrmDocument.Level.Molecules &&
                   (selectedPath.Depth >= identityPath.Depth && Equals(selectedPath.GetPathTo(identityPath.Depth), identityPath) ||
                    selectedPath.Depth <= identityPath.Depth && Equals(identityPath.GetPathTo(selectedPath.Depth), selectedPath));
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
            var rows = GetProteinAbundanceResults();
            _graphData = CreateGraphData(document, selectedProtein, displayType, rows);

            // For proper z-order, add the selected points, then the matched points, then the unmatched points
            var selectedPoints = new PointPairList();
            foreach (var pointPairList in _graphData.PointPairLists)
            {
                foreach (var point in from point in pointPairList let proteinResult = (ProteinAbundanceResult)point.Tag where IsSelected(proteinResult.Protein) select point)
                {
                    selectedPoints.Add(point);
                }
            }
            AddPoints(new PointPairList(selectedPoints), GraphSummary.ColorSelected, DotPlotUtil.PointSizeToFloat(PointSize.large), true, PointSymbol.Circle);
            foreach (var pointPairList in _graphData.PointPairLists)
            {
                var pointList = pointPairList;
                // Foreach valid match expression specified by the user
                foreach (var colorRow in ColorRows.Where(r => r.MatchExpression != null))
                {
                    var row = colorRow;
                    var matchedPoints = pointPairList.Where(p =>
                    {
                        var proteinAbundanceResult = (ProteinAbundanceResult)p.Tag;
                        return row.MatchExpression.Matches(Document, proteinAbundanceResult.Protein) && !selectedPoints.Contains(p);
                    }).ToArray();

                    if (matchedPoints.Any())
                    {
                        AddPoints(new PointPairList(matchedPoints), colorRow.Color, DotPlotUtil.PointSizeToFloat(row.PointSize), row.Labeled, row.PointSymbol);
                        pointList = new PointPairList(pointPairList.Except(matchedPoints).Except(selectedPoints).ToArray());
                    }
                }
                AddPoints(new PointPairList(pointList), Color.Gray, DotPlotUtil.PointSizeToFloat(PointSize.normal), false, PointSymbol.Circle);
            }

            UpdateAxes();
            if (GraphSummary.ShowFormattingDlg && !ShowingFormattingDlg)
            {
                ShowFormattingDialog();
            }
        }
        private void AddPoints(PointPairList points, Color color, float size, bool labeled, PointSymbol pointSymbol)
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
                    GraphObjList.Add(label);
                }
            }

            CurveList.Add(lineItem);
        }

        private static TextObj CreateLabel(PointPair point, Color color, float size)
        {
            var result = point.Tag as ProteinAbundanceResult;
            if (result == null)
                return null;
            var text = GetProteinName(result.Protein);
            var textObj = new TextObj(text, point.X, point.Y, CoordType.AxisXYScale, AlignH.Center, AlignV.Bottom)
            {
                IsClippedToChartRect = true,
                FontSpec = DotPlotUtil.CreateFontSpec(color, size, true),
                ZOrder = ZOrder.A_InFront,
            };

            return textObj;
        }

        private static string GetProteinName(Protein protein)
        {
            var displayMode = Helpers.ParseEnum(Settings.Default.ShowPeptidesDisplayMode,
                ProteinMetadataManager.ProteinDisplayMode.ByName);
            return MatchExpression.GetProteinText(protein, displayMode);
        }


        protected abstract GraphData CreateGraphData(SrmDocument document, PeptideGroupDocNode selectedProtein, DisplayTypeChrom displayType, List<ProteinAbundanceResult> rows);

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
                PaneKey paneKey, List<ProteinAbundanceResult> rows)
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
                                var pointPair = CreatePointPair(iGroup, dataPoint.Result, ref groupMaxY, ref groupMinY, resultIndex);
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

            protected virtual PointPair CreatePointPair(int iGroup, ProteinAbundanceResult result, ref double maxY, ref double minY, int? resultIndex)
            {
                var abundance = result.CalculatedAbundance;
                var pointPair = MeanErrorBarItem.MakePointPair(iGroup, abundance, result);
                maxY = Math.Max(maxY, pointPair.Y);
                minY = Math.Min(minY, pointPair.Y);
                return pointPair;
            }


            protected abstract double? GetValue(TransitionGroupChromInfo chromInfo);

            protected abstract double GetValue(TransitionChromInfo info);
        }

        private class GraphPointData
        {
            public GraphPointData(ProteinAbundanceResult result)
            {
                
                NodePepGroup = result.Protein.DocNode;
                NodePep = (PeptideDocNode)NodePepGroup.Children.First(); // TODO get rid of this variable
                NodeGroup = (TransitionGroupDocNode)NodePep.Children.First();
                IdentityPath = new IdentityPath(IdentityPath.ROOT, NodePepGroup.PeptideGroup);
                AreaGroup = result.CalculatedAbundance;
                Result = result;
            }
            public PeptideGroupDocNode NodePepGroup { get; private set; }
            public PeptideDocNode NodePep { get; private set; }
            public TransitionGroupDocNode NodeGroup { get; private set; }
            public IdentityPath IdentityPath { get; private set; }
            public double AreaGroup { get; private set; }
            public ProteinAbundanceResult Result { get; private set; }

        }
    }
}
