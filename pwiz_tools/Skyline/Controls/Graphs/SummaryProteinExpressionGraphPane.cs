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

        protected GraphData _graphData;
        public bool AnyProteomic;
        public bool AnyMolecules;
        public SrmDocument Document;
        public bool ShowingFormattingDlg { get; set; }
        public IList<MatchRgbHexColor> ColorRows { get; set; }
        private List<ProteinAbundanceResult> _proteinAbundanceResults;

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
        }

        protected override int SelectedIndex
        {
            get { return _graphData != null ? _graphData.SelectedIndex : -1; }
        }

        protected override IdentityPath GetIdentityPath(CurveItem curveItem, int barIndex)
        {
            if (0 <= barIndex && barIndex < _graphData.XScalePaths.Length)
            {
                return _graphData.XScalePaths[barIndex];
            }
            return null;
        }

        protected void ShowFormattingDialog()
        {
            var proteinAbundanceResults = GetProteinAbundanceResults().ToArray();
            var copy = ColorRows.Select(r => (MatchRgbHexColor)r.Clone()).ToList(); //TODO is this necessary
            var window = GraphSummary.Window;
            ShowingFormattingDlg = true;
            using (var dlg = new VolcanoPlotFormattingDlg(this, copy, 
                       proteinAbundanceResults, 
                       rows  =>
                       {
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
            // TODO Add if document changed
            if (_proteinAbundanceResults == null)
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
                    list.Add(proteinAbundanceResult);
                }

                _proteinAbundanceResults = list;
            }

            return _proteinAbundanceResults;
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
                    var completeReplicates = 0;
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

            var results = GetProteinAbundanceResults();
            foreach (var result in results)
            {
                result.CalculateAbundance(GraphSummary.ResultsIndex);
            }
            _graphData = CreateGraphData(selectedProtein, results);

            // For proper z-order, add the selected points, then the matched points, then the unmatched points
            var selectedPoints = new PointPairList();
            foreach (var point in from point in _graphData.PointPairList let proteinResult = (ProteinAbundanceResult)point.Tag where IsSelected(proteinResult.Protein) select point)
            {
                selectedPoints.Add(point);
            }
            AddPoints(new PointPairList(selectedPoints), GraphSummary.ColorSelected, DotPlotUtil.PointSizeToFloat(PointSize.large), true, PointSymbol.Circle);
            var pointList = _graphData.PointPairList;
            // For each valid match expression specified by the user
            foreach (var colorRow in ColorRows.Where(r => r.MatchExpression != null))
            {
                var matchedPoints = pointList.Where(p =>
                {
                    var proteinAbundanceResult = (ProteinAbundanceResult)p.Tag;
                    return colorRow.MatchExpression.Matches(Document, proteinAbundanceResult.Protein) && !selectedPoints.Contains(p);
                }).ToArray();

                if (matchedPoints.Any())
                {
                    AddPoints(new PointPairList(matchedPoints), colorRow.Color, DotPlotUtil.PointSizeToFloat(colorRow.PointSize), colorRow.Labeled, colorRow.PointSymbol);
                    pointList = new PointPairList(pointList.Except(matchedPoints).Except(selectedPoints).ToArray());
                }
            }
            AddPoints(new PointPairList(pointList), Color.Gray, DotPlotUtil.PointSizeToFloat(PointSize.normal), false, PointSymbol.Circle);

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


        protected abstract GraphData CreateGraphData(PeptideGroupDocNode selectedProtein, List<ProteinAbundanceResult> results);


        protected virtual void UpdateAxes()
        {
            if (Settings.Default.AreaLogScale )
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
            else if (!IsZoomed && !YAxis.Scale.MaxAuto)
            {
                YAxis.Scale.MaxAuto = true;
            }

            AxisChange();
        }

        public abstract class GraphData : Immutable
        {
            // ReSharper disable PossibleMultipleEnumeration
            protected GraphData(PeptideGroupDocNode selectedProtein,
                int? iResult, List<ProteinAbundanceResult> results)
            {

                // Build the list of points to show.
                var listPoints = new List<GraphPointData>();
                foreach (var result in results)
                {
                    var nodeGroupPep = result.Protein.DocNode;
                    if (AreaGraphController.AreaScope == AreaScope.protein)
                    {
                        if (!ReferenceEquals(nodeGroupPep, selectedProtein))
                            continue;
                    }
                    var graphPointData = new GraphPointData(result);
                    listPoints.Add(graphPointData);
                }

                // Sort into correct order
                listPoints.Sort(CompareGroupAreas);

                // Init calculated values
                var labels = new List<string>();
                var xscalePaths = new List<IdentityPath>();
                double maxY = 0;
                double minY = double.MaxValue;
                int selectedIndex = -1;

                var pointPairList = new PointPairList(); 

                foreach (var dataPoint in listPoints)
                {
                    int iGroup = labels.Count;

                    var label = iGroup.ToString();
                    labels.Add(label);
                    xscalePaths.Add(dataPoint.IdentityPath);
                    

                    double groupMaxY = 0;
                    double groupMinY = double.MaxValue;
                    // ReSharper disable DoNotCallOverridableMethodsInConstructor
                    var pointPair = CreatePointPair(iGroup, dataPoint.Result, ref groupMaxY, ref groupMinY, iResult);
                    // ReSharper restore DoNotCallOverridableMethodsInConstructor
                    pointPairList.Add(pointPair);

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

                PointPairList = pointPairList;
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

            public PointPairList PointPairList { get; private set; }
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
