/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public partial class FoldChangeVolcanoPlot : FoldChangeForm, ITipDisplayer
    {
        private const int MAX_SELECTED = 100;
        private const double MIN_PVALUE = 1E-6;

        private BindingListSource _bindingListSource;
        private SkylineWindow _skylineWindow;
        private bool _updatePending;

        private LineItem _foldChangeCutoffLine1;
        private LineItem _foldChangeCutoffLine2;
        private LineItem _minPValueLine;

        private readonly List<LineItem> _points;
        // List of graph points that have labels
        private readonly List<LabeledPoint> _labeledPoints;
        //
        private static readonly Dictionary<string, List<LabeledPoint.PointLayout>> _labelsLayouts = new Dictionary<string, List<LabeledPoint.PointLayout>>();

        private FoldChangeBindingSource.FoldChangeRow _selectedRow;

        private NodeTip _tip;
        private RowFilter.ColumnFilter _absLog2FoldChangeFilter;
        private RowFilter.ColumnFilter _pValueFilter;

        private GroupComparisonModel GroupComparisonModel
        {
            get { return FoldChangeBindingSource.GroupComparisonModel; }
        }

        private GroupComparisonDef GroupComparisonDef
        {
            get { return GroupComparisonModel.GroupComparisonDef; }
        }

        public FoldChangeVolcanoPlot()
        {
            InitializeComponent();
            
            zedGraphControl.GraphPane.Title.Text = null;
            zedGraphControl.MasterPane.Border.IsVisible = false;
            zedGraphControl.GraphPane.Border.IsVisible = false;
            zedGraphControl.GraphPane.Chart.Border.IsVisible = false;
            zedGraphControl.GraphPane.Chart.Border.IsVisible = false;

            zedGraphControl.GraphPane.XAxis.Title.Text = GroupComparisonStrings.FoldChange_Log2_Fold_Change_;
            zedGraphControl.GraphPane.YAxis.Title.Text = GroupComparisonStrings.FoldChange__Log10_P_Value_;
            zedGraphControl.GraphPane.X2Axis.IsVisible = false;
            zedGraphControl.GraphPane.Y2Axis.IsVisible = false;
            zedGraphControl.GraphPane.XAxis.MajorTic.IsOpposite = false;
            zedGraphControl.GraphPane.YAxis.MajorTic.IsOpposite = false;
            zedGraphControl.GraphPane.XAxis.MinorTic.IsOpposite = false;
            zedGraphControl.GraphPane.YAxis.MinorTic.IsOpposite = false;
            zedGraphControl.GraphPane.IsFontsScaled = false;
            zedGraphControl.GraphPane.YAxis.Scale.MaxGrace = 0.1;
            zedGraphControl.IsZoomOnMouseCenter = true;

            _points = new List<LineItem>();
            _labeledPoints = new List<LabeledPoint>();
        }

        public SrmDocument Document
        {
            get { return _skylineWindow != null ? _skylineWindow.Document : null; }
        }

        public bool AnyProteomic
        {
            get { return Document.IsEmptyOrHasPeptides; } // Treat empty as proteomic per tradition
        }

        public bool AnyMolecules
        {
            get { return Document.HasSmallMolecules; }
        }

        public bool PerProtein
        {
            get { return GroupComparisonDef.PerProtein; }
        }

        private void AdjustLocations(GraphPane pane)
        {
            pane.YAxis.Scale.Min = 0.0;

            if (_foldChangeCutoffLine1 != null && _foldChangeCutoffLine2 != null)
            {
                _foldChangeCutoffLine1[0].Y = _foldChangeCutoffLine2[0].Y = pane.YAxis.Scale.Min;
                _foldChangeCutoffLine1[1].Y = _foldChangeCutoffLine2[1].Y = pane.YAxis.Scale.Max;
            }

            if (_minPValueLine != null)
            {
                _minPValueLine[0].X = pane.XAxis.Scale.Min;
                _minPValueLine[1].X = pane.XAxis.Scale.Max;
            }
        }

        private void GraphPane_AxisChangeEvent(GraphPane pane)
        {
            AdjustLocations(pane);
        }

        private void zedGraphControl_ZoomEvent(ZedGraphControl sender, ZoomState oldState,
            ZoomState newState, PointF mousePosition)
        {
            if (Settings.Default.GroupComparisonAvoidLabelOverlap)
            {
                if (!Settings.Default.GroupComparisonSuspendLabelLayout)
                {
                    zedGraphControl.GraphPane.AdjustLabelSpacings(_labeledPoints);
                    _labelsLayouts[GroupComparisonName] = zedGraphControl.GraphPane.Layout?.PointsLayout;
                }
                else
                {
                    if (_labelsLayouts.TryGetValue(GroupComparisonName, out var savedLayout))
                    {
                        zedGraphControl.GraphPane.AdjustLabelSpacings(_labeledPoints, savedLayout);
                        _labelsLayouts[GroupComparisonName] = zedGraphControl.GraphPane.Layout?.PointsLayout;
                    }
                }
            }
            else
                zedGraphControl.GraphPane.EnableLabelLayout = false;
            AdjustLocations(zedGraphControl.GraphPane);
        }

        private void zedGraphControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                Program.MainWindow.FocusDocument();
        }

        public override string GetTitle(string groupComparisonName)
        {
            return base.GetTitle(groupComparisonName) + ':' + GroupComparisonStrings.FoldChangeVolcanoPlot_GetTitle_Volcano_Plot;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            Settings.Default.PropertyChanged += OnLabelOverlapPropertyChange;

            if (FoldChangeBindingSource != null)
            {
                AllowDisplayTip = true;

                _bindingListSource = FoldChangeBindingSource.GetBindingListSource();
                _bindingListSource.ListChanged += BindingListSourceOnListChanged;
                _bindingListSource.AllRowsChanged += BindingListSourceAllRowsChanged;
                zedGraphControl.GraphPane.AxisChangeEvent += GraphPane_AxisChangeEvent;
                zedGraphControl.ZoomEvent += zedGraphControl_ZoomEvent;

                if (_skylineWindow == null)
                {
                    _skylineWindow = ((SkylineDataSchema)_bindingListSource.ViewInfo.DataSchema).SkylineWindow;
                    if (_skylineWindow != null)
                    {
                        _skylineWindow.SequenceTree.AfterSelect += SequenceTreeOnAfterSelect;
                    }
                }

                UpdateGraph(Settings.Default.FilterVolcanoPlotPoints);
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_tip != null)
            {
                _tip.HideTip();
                _tip.Dispose();
            }
            AllowDisplayTip = false;

            if (_skylineWindow != null && _skylineWindow.SequenceTree != null)
            {
                _skylineWindow.SequenceTree.AfterSelect -= SequenceTreeOnAfterSelect;
                _skylineWindow = null;
            }

            zedGraphControl.GraphPane.AxisChangeEvent -= GraphPane_AxisChangeEvent;
            zedGraphControl.ZoomEvent -= zedGraphControl_ZoomEvent;
            Settings.Default.PropertyChanged -= OnLabelOverlapPropertyChange;

            if (_bindingListSource != null)
            {
                _bindingListSource.AllRowsChanged -= BindingListSourceAllRowsChanged;
                _bindingListSource.ListChanged -= BindingListSourceOnListChanged;
            }

            UpdateFilter(false);

            base.OnHandleDestroyed(e);
        }

        private void SequenceTreeOnAfterSelect(object sender, TreeViewEventArgs treeViewEventArgs)
        {
            QueueUpdateGraph();
        }

        public void QueueUpdateGraph()
        {
            if (!IsHandleCreated)
                return;

            if (!_updatePending)
            {
                _updatePending = true;
                BeginInvoke(new Action(() =>
                {
                    _updatePending = false;
                    UpdateGraph(Settings.Default.FilterVolcanoPlotPoints);
                }));
            }
        }

        public class PropertiesCutoffSettings : AuditLogOperationSettings<PropertiesCutoffSettings>, ICutoffSettings
        {
            protected class FoldChangeDefaults : DefaultValues
            {
                protected override IEnumerable<object> _values
                {
                    get { yield return double.NaN; yield return 0.0; }
                }
            }

            protected class PValueDefaults : DefaultValues
            {
                public override bool IsDefault(object obj, object parentObject)
                {
                    var dbl = (double)obj;
                    return double.IsNaN(dbl) || dbl < 0.0;
                }
            }

            [Track(defaultValues: typeof(FoldChangeDefaults))]
            public double Log2FoldChangeCutoff
            {
                get { return Settings.Default.Log2FoldChangeCutoff; }
                set { Settings.Default.Log2FoldChangeCutoff = value; }
            }

            [Track(defaultValues: typeof(PValueDefaults))]
            public double PValueCutoff
            {
                get { return Settings.Default.PValueCutoff; }
                set { Settings.Default.PValueCutoff = value; }
            }

            public bool FoldChangeCutoffValid
            {
                get { return Model.GroupComparison.CutoffSettings.IsFoldChangeCutoffValid(Log2FoldChangeCutoff); }
            }

            public bool PValueCutoffValid
            {
                get { return Model.GroupComparison.CutoffSettings.IsPValueCutoffValid(PValueCutoff); }
            }
        }

        public static PropertiesCutoffSettings CutoffSettings = new PropertiesCutoffSettings();

        public static bool AnyCutoffSettingsValid
        {
            get { return CutoffSettings.FoldChangeCutoffValid || CutoffSettings.PValueCutoffValid; }
        }

        // ReSharper disable PossibleMultipleEnumeration
        private void UpdateGraph()
        {
            if (!IsHandleCreated || _bindingListSource == null)
                return;
            
            zedGraphControl.GraphPane.GraphObjList.Clear();
            zedGraphControl.GraphPane.CurveList.Clear();
            _points.Clear();
            _labeledPoints.Clear();
            _foldChangeCutoffLine1 = _foldChangeCutoffLine2 = _minPValueLine = null;

            var rows = GetFoldChangeRows(_bindingListSource).ToList();
            if (!rows.Any()) // Nothing to graph
                return;

            var selectedPoints = new PointPairList();
            var otherPoints = new PointPairList();

            var count = 0;

            // Create points and Selection objects
            foreach (var row in rows.OrderBy(r => r.FoldChangeResult.AdjustedPValue))
            {
                var foldChange = row.FoldChangeResult.Log2FoldChange;
                var pvalue = -Math.Log10(Math.Max(MIN_PVALUE, row.FoldChangeResult.AdjustedPValue));
                var point = new PointPair(foldChange, pvalue) { Tag = row };
                if (Settings.Default.GroupComparisonShowSelection && count < MAX_SELECTED && DotPlotUtil.IsTargetSelected(_skylineWindow, row.Peptide, row.Protein))
                {
                    selectedPoints.Add(point);
                    ++count;
                }
                else
                {
                    otherPoints.Add(point);
                }
            }

            // The order matters here, selected points should be highest in the zorder, followed by matched points and other(unmatched) points
            AddPoints(selectedPoints, Color.Red, DotPlotUtil.PointSizeToFloat(PointSize.large), true, PointSymbol.Circle, true);

            foreach (var colorRow in GroupComparisonDef.ColorRows.Where(r => r.MatchExpression != null))
            {
                var row = colorRow;
                var matchedPoints = otherPoints.Where(p =>
                {
                    var foldChangeRow = (FoldChangeBindingSource.FoldChangeRow) p.Tag;
                    return row.MatchExpression.Matches(Document, foldChangeRow.Protein, foldChangeRow.Peptide,
                        foldChangeRow.FoldChangeResult, CutoffSettings);
                }).ToArray();

                if (matchedPoints.Any())
                {
                    AddPoints(new PointPairList(matchedPoints), colorRow.Color, DotPlotUtil.PointSizeToFloat(row.PointSize), row.Labeled, row.PointSymbol);
                    otherPoints = new PointPairList(otherPoints.Except(matchedPoints).ToArray());
                }
            }

            AddPoints(otherPoints, Color.Gray, DotPlotUtil.PointSizeToFloat(PointSize.small), false, PointSymbol.Circle);

            // The coordinates that depend on the axis scale don't matter here, the AxisChangeEvent will fix those
            // Insert after selected items, but before all other items
            var index = 1;
            if (CutoffSettings.FoldChangeCutoffValid)
            {
                _foldChangeCutoffLine1 = CreateAndInsert(index++, Settings.Default.Log2FoldChangeCutoff, Settings.Default.Log2FoldChangeCutoff, 0.0, 0.0);
                _foldChangeCutoffLine2 = CreateAndInsert(index++, -Settings.Default.Log2FoldChangeCutoff, -Settings.Default.Log2FoldChangeCutoff, 0.0, 0.0);
            }

            if (CutoffSettings.PValueCutoffValid)
            {
                _minPValueLine = CreateAndInsert(index, 0.0, 0.0, Settings.Default.PValueCutoff, Settings.Default.PValueCutoff);
            }

            zedGraphControl.GraphPane.YAxis.Scale.Min = 0.0;
            zedGraphControl.GraphPane.XAxis.Scale.MinAuto = zedGraphControl.GraphPane.XAxis.Scale.MaxAuto = zedGraphControl.GraphPane.YAxis.Scale.MaxAuto = true;       
            zedGraphControl.GraphPane.AxisChange();
            zedGraphControl.GraphPane.XAxis.Scale.MinAuto = zedGraphControl.GraphPane.XAxis.Scale.MaxAuto = zedGraphControl.GraphPane.YAxis.Scale.MaxAuto = false;

            if (Settings.Default.GroupComparisonAvoidLabelOverlap)
            {
                zedGraphControl.GraphPane.AdjustLabelSpacings(_labeledPoints,
                    _labelsLayouts.TryGetValue(GroupComparisonName, out var layout) ? layout : null);
                _labelsLayouts[GroupComparisonName] = zedGraphControl.GraphPane.Layout?.PointsLayout;
            }
            zedGraphControl.Invalidate();
        }
        // ReSharper restore PossibleMultipleEnumeration

        protected override string GetPersistentString()
        {
            if (_labelsLayouts.ContainsKey(GroupComparisonName))
            {
                return PersistentString.Parse(base.GetPersistentString())
                    .Append(JsonConvert.SerializeObject(_labelsLayouts[GroupComparisonName])).ToString();
            }
            else
                return null;
        }

        public void SetLayout(string groupComparisonName, string jsonLayout)
        {
            try
            {
                var layout = JsonConvert.DeserializeObject<List<LabeledPoint.PointLayout>>(jsonLayout);
                _labelsLayouts[GroupComparisonName] = layout;
            }
            catch (Exception e)
            {
                Trace.Write(@"Cannot deserialize labels layout. Error message: " + e.Message);
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
                    var row = (FoldChangeBindingSource.FoldChangeRow)point.Tag;
                    if (row == null)
                    {
                        continue;
                    }
                    var label = DotPlotUtil.CreateLabel(point, row.Protein, row.Peptide, color, size);
                    _labeledPoints.Add(new LabeledPoint(selected, row.Peptide?.IdentityPath ?? row.Protein.IdentityPath){Point = point, Label = label, Curve = lineItem}); 
                    zedGraphControl.GraphPane.GraphObjList.Add(label);
                }
            }

            _points.Add(lineItem);
            zedGraphControl.GraphPane.CurveList.Add(lineItem);
        }

        private LineItem CreateAndInsert(int index, double fromX, double toX, double fromY, double toY)
        {
            var item = CreateLineItem(null, fromX, toX, fromY, toY, Color.Black);
            zedGraphControl.GraphPane.CurveList.Insert(index, item);

            return item;
        }

        private LineItem CreateLineItem(string text, double fromX, double toX, double fromY, double toY, Color color)
        {
            return new LineItem(text, new[] { fromX, toX }, new[] { fromY, toY }, color, SymbolType.None, 1.0f)
            {
                Line = { Style = DashStyle.Dash },
                
            };
        }

        private void BindingListSourceAllRowsChanged(object sender, EventArgs e)
        {
            QueueUpdateGraph();
        }

        private void BindingListSourceOnListChanged(object sender, ListChangedEventArgs listChangedEventArgs)
        {
            QueueUpdateGraph();
        }

        private bool zedGraphControl_MouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            return MoveMouse(e.Button, e.Location);
        }

        #region Selection Code

        public bool MoveMouse(MouseButtons buttons, Point point)
        {
            // Pan
            if (ModifierKeys.HasFlag(Keys.Control) && buttons.HasFlag(MouseButtons.Left))
                AdjustLocations(zedGraphControl.GraphPane);

            CurveItem nearestCurveItem = null;
            var index = -1;
            var isSelected = false;
            if (TryGetNearestCurveItem(point, ref nearestCurveItem, ref index))
            {
                var lineItem = nearestCurveItem as LineItem;
                if (lineItem == null || index < 0 || index >= lineItem.Points.Count || lineItem[index].Tag == null)
                    return false;

                _selectedRow = (FoldChangeBindingSource.FoldChangeRow) lineItem[index].Tag;
                isSelected = true;
            }
            else
            {
                var labPoint = zedGraphControl.GraphPane.OverLabel(point, out var isOverBoundary);
                if (labPoint != null )
                {
                    if (!isOverBoundary)
                    {
                        _selectedRow = (FoldChangeBindingSource.FoldChangeRow)labPoint.Point.Tag;
                        isSelected = true;
                    }
                }
                else
                {
                    using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    {
                        zedGraphControl.GraphPane.FindNearestObject(point, g, out var nearestObj, out _);
                        if (nearestObj is TextObj nearestText)
                        {
                            var labels = LabeledPoints.FindAll(lp => lp.Label.Equals(nearestText));
                            if (labels.Any())
                            {
                                _selectedRow = (FoldChangeBindingSource.FoldChangeRow)labels.First().Point.Tag;
                                isSelected = true;
                            }
                        }
                    }
                }
            }

            if (isSelected)
            {
                if (Control.ModifierKeys != zedGraphControl.EditModifierKeys)
                    zedGraphControl.Cursor = Cursors.Hand;

                if (_tip == null)
                    _tip = new NodeTip(this) { Parent = this };
                _tip.SetTipProvider(new FoldChangeRowTipProvider(_selectedRow), new Rectangle(point, new Size()),
                    point);
            }
            else
            {
                _tip?.HideTip();
                _selectedRow = null;
            }
            return isSelected;
        }

        private bool TryGetNearestCurveItem(Point point, ref CurveItem nearestCurveItem, ref int index)
        {
            foreach (var item in _points)
            {
                if (zedGraphControl.GraphPane.FindNearestPoint(point, item, out nearestCurveItem, out index))
                    return true;
            }
            return false;
        }

        public void Deselect(IdentityPath identityPath)
        {
            var skylineWindow = _skylineWindow;
            if (skylineWindow == null)
                return;

            var list = skylineWindow.SequenceTree.SelectedPaths.ToList();
            var selectedPath = DotPlotUtil.GetSelectedPath(_skylineWindow, identityPath);
            if (selectedPath != null)
            {
                if (selectedPath.Depth < identityPath.Depth)
                {
                    var protein = (PeptideGroupDocNode) skylineWindow.DocumentUI.FindNode(selectedPath);
                    var peptide = (PeptideDocNode) skylineWindow.DocumentUI.FindNode(identityPath);

                    var peptides = protein.Molecules.Except(new[] { peptide });
                    list.Remove(selectedPath);
                    list.AddRange(peptides.Select(p => new IdentityPath(selectedPath, p.Id)));
                }
                else if (selectedPath.Depth > identityPath.Depth)
                {
                    var protein = (PeptideGroupDocNode) skylineWindow.DocumentUI.FindNode(identityPath);
                    var peptidePaths = protein.Molecules.Select(p => new IdentityPath(identityPath, p.Id));

                    list = list.Except(list.Where(path => peptidePaths.Contains(path))).ToList();
                }
                else
                {
                    list.Remove(identityPath);
                }

                skylineWindow.SequenceTree.SelectedPaths = list;

                if (list.Any())
                {
                    if (Equals(skylineWindow.SelectedPath, selectedPath))
                        skylineWindow.SequenceTree.SelectPath(list.First());
                    else
                        skylineWindow.SequenceTree.Refresh();
                }
                else
                {
                    skylineWindow.SequenceTree.SelectedNode = null;
                }
            }
            skylineWindow.UpdateGraphPanes();
        }

        private bool zedGraphControl_MouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys == zedGraphControl.EditModifierKeys)
            {
                _tip?.HideTip();
                _selectedRow = null;
                return false;
            }

            return e.Button.HasFlag(MouseButtons.Left) && ClickSelectedRow();
        }

        public static SkylineDocNode GetSkylineDocNodeFromRow(FoldChangeBindingSource.FoldChangeRow row)
        {
            if (row.Peptide != null)
                return row.Peptide;
            else
                return row.Protein;
        }

        public bool ClickSelectedRow()
        {
            if (_selectedRow == null)
                return false;

            var docNode = GetSkylineDocNodeFromRow(_selectedRow);

            if (docNode == null || ModifierKeys.HasFlag(Keys.Shift))
                return false;

            var isSelected = DotPlotUtil.IsTargetSelected(_skylineWindow, _selectedRow.Peptide, _selectedRow.Protein);
            var ctrl = ModifierKeys.HasFlag(Keys.Control);

            if (!ctrl)
            {
                DotPlotUtil.Select(_skylineWindow, docNode.IdentityPath);
                return true; // No need to call UpdateGraph
            }
            else if (isSelected)
                Deselect(docNode.IdentityPath);
            else
                DotPlotUtil.MultiSelect(_skylineWindow, docNode.IdentityPath);

            FormUtil.OpenForms.OfType<FoldChangeVolcanoPlot>().ForEach(v => v.QueueUpdateGraph()); // Update all volcano plots
            return true;
        }

        #endregion

        private void zedGraphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            BuildContextMenu(sender, menuStrip, mousePt, objState);
        }

        private bool zedGraphControl_LabelDragComplete(ZedGraphControl sender, MouseEventArgs mouseEvent)
        {
            _labelsLayouts[GroupComparisonName] = zedGraphControl.GraphPane.Layout.PointsLayout;
            return true;
        }

        protected override void BuildContextMenu(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            base.BuildContextMenu(sender, menuStrip, mousePt, objState);

            // Find the first separator
            var index = menuStrip.Items.OfType<ToolStripItem>().ToArray().IndexOf(t => t is ToolStripSeparator);

            if (index >= 0)
            {
                menuStrip.Items.Insert(index++, new ToolStripSeparator());
                menuStrip.Items.Insert(index++, new ToolStripMenuItem(GroupComparisonStrings.FoldChangeVolcanoPlot_BuildContextMenu_Selection, null, OnSelectionClick)
                    { Checked = Settings.Default.GroupComparisonShowSelection });
                menuStrip.Items.Insert(index++, new ToolStripMenuItem(GraphsResources.FoldChangeVolcanoPlot_BuildContextMenu_Auto_Arrange_Labels, null, OnLabelOverlapClick)
                    { Checked = Settings.Default.GroupComparisonAvoidLabelOverlap });
                if (Settings.Default.GroupComparisonAvoidLabelOverlap)
                {
                    if (Settings.Default.GroupComparisonSuspendLabelLayout)
                        menuStrip.Items.Insert(index++, new ToolStripMenuItem(GraphsResources.FoldChangeVolcanoPlot_BuildContextMenu_RestartLabelLayout, null, OnSuspendLayout));
                    else
                        menuStrip.Items.Insert(index++, new ToolStripMenuItem(GraphsResources.FoldChangeVolcanoPlot_BuildContextMenu_PauseLabelLayout, null, OnSuspendLayout));
                }
                menuStrip.Items.Insert(index++, new ToolStripSeparator());
                menuStrip.Items.Insert(index++, new ToolStripMenuItem(GroupComparisonStrings.FoldChangeVolcanoPlot_BuildContextMenu_Properties___, null, OnPropertiesClick));
                menuStrip.Items.Insert(index++, new ToolStripMenuItem(GroupComparisonStrings.FoldChangeVolcanoPlot_BuildContextMenu_Formatting___, null, OnFormattingClick));
                if (AnyCutoffSettingsValid)
                {
                    menuStrip.Items.Insert(index++, new ToolStripSeparator());
                    menuStrip.Items.Insert(index, new ToolStripMenuItem(GroupComparisonStrings.FoldChangeVolcanoPlot_BuildContextMenu_Remove_Below_Cutoffs, null, OnRemoveBelowCutoffsClick));
                }
            }
        }

        /// <summary>
        /// Detect changes in settings shared with <see cref="SummaryRelativeAbundanceGraphPane"/> right-click menu
        /// </summary>
        private void OnLabelOverlapPropertyChange(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == @"GroupComparisonAvoidLabelOverlap")
                UpdateGraph();
            else if (e.PropertyName == @"GroupComparisonSuspendLabelLayout")
            {
                if (!Settings.Default.GroupComparisonSuspendLabelLayout)
                {
                    zedGraphControl.GraphPane.AdjustLabelSpacings(_labeledPoints);
                    _labelsLayouts[GroupComparisonName] = zedGraphControl.GraphPane.Layout?.PointsLayout;
                    zedGraphControl.Invalidate();
                }
            }
        }

        private void OnLabelOverlapClick(object o, EventArgs eventArgs)
        {
            Settings.Default.GroupComparisonAvoidLabelOverlap = !Settings.Default.GroupComparisonAvoidLabelOverlap;
        }

        private void OnFormattingClick(object o, EventArgs eventArgs)
        {
            ShowFormattingDialog();
        }

        private void OnSuspendLayout(object sender, EventArgs eventArgs)
        {
            Settings.Default.GroupComparisonSuspendLabelLayout = !Settings.Default.GroupComparisonSuspendLabelLayout;
        }

        public void ShowFormattingDialog()
        {
            var foldChangeRows = GetFoldChangeRows(_bindingListSource).ToArray();

            var backup = GroupComparisonDef.ColorRows.Select(r => r.Clone()).ToArray();
            using var form = new VolcanoPlotFormattingDlg(this, GroupComparisonDef.ColorRows, foldChangeRows, UpdateColorRows);
            {
                if (form.ShowDialog(FormEx.GetParentForm(this)) == DialogResult.OK)
                {
                    EditGroupComparisonDlg.ChangeGroupComparisonDef(true, GroupComparisonModel, GroupComparisonDef);
                }
                else
                {
                    EditGroupComparisonDlg.ChangeGroupComparisonDef(false, GroupComparisonModel, GroupComparisonDef.ChangeColorRows(backup));
                }

                UpdateGraph();
            }     
        }

        private void UpdateColorRows(IEnumerable<MatchRgbHexColor> colorRows)
        {
            EditGroupComparisonDlg.ChangeGroupComparisonDef(false, GroupComparisonModel,
                GroupComparisonDef.ChangeColorRows(colorRows));
            zedGraphControl.GraphPane.EnableLabelLayout = Settings.Default.GroupComparisonAvoidLabelOverlap;
            UpdateGraph();
        }

        private void OnRemoveBelowCutoffsClick(object o, EventArgs eventArgs)
        {
            RemoveBelowCutoffs();
        }

        public void RemoveBelowCutoffs()
        {
            var rows = GetFoldChangeRows(_bindingListSource).ToArray();

            var foldchangeCutoff = Math.Abs(Settings.Default.Log2FoldChangeCutoff);
            var pvalueCutoff = Math.Pow(10, -Settings.Default.PValueCutoff);

            var indices = new int[0];
            if (CutoffSettings.PValueCutoffValid || CutoffSettings.FoldChangeCutoffValid)
            {
                indices = GroupComparisonRefinementData.IndicesBelowCutoff(CutoffSettings.PValueCutoffValid ? pvalueCutoff : double.NaN, CutoffSettings.FoldChangeCutoffValid ? foldchangeCutoff : double.NaN,
                    double.NaN, rows).ToArray();
            }
                        

            _skylineWindow.ModifyDocument(GroupComparisonStrings.FoldChangeVolcanoPlot_RemoveBelowCutoffs_Remove_peptides_below_cutoffs, document => (SrmDocument)document.RemoveAll(indices),
                docPair => AuditLogEntry.CreateSimpleEntry(indices.Length == 1 ? MessageType.removed_single_below_cutoffs : MessageType.removed_below_cutoffs, docPair.NewDocumentType,
                    indices.Length, GroupComparisonDef.Name).Merge(CutoffSettings.EntryCreator.Create(docPair)));
        }

        private void OnSelectionClick(object o, EventArgs eventArgs)
        {
            Settings.Default.GroupComparisonShowSelection = !Settings.Default.GroupComparisonShowSelection;
            UpdateGraph();
        }

        public void ShowProperties()
        {
            using (var dlg = new VolcanoPlotPropertiesDlg())
            {
                dlg.ShowDialog(FormEx.GetParentForm(this));
            }
        }

        private void OnPropertiesClick(object o, EventArgs eventArgs)
        {
            ShowProperties();
        }

        public void UpdateGraph(bool filter)
        {
            if (!UpdateFilter(filter))
                UpdateGraph();
        }

        private bool UpdateFilter(bool filter)
        {
            if (!IsHandleCreated || _bindingListSource == null)
                return false;

            if (!_bindingListSource.IsComplete)
                return true;

            var columnFilters = _bindingListSource.RowFilter.ColumnFilters.ToList();
            var columns = _bindingListSource.ViewSpec.Columns;

            var absLog2FCExists = columns.Any(c => c.Name == @"FoldChangeResult.AbsLog2FoldChange");
            var pValueExists = columns.Any(c => c.Name == @"FoldChangeResult.AdjustedPValue");

            bool foldChangeUpdate;
            var foldChangeFilter = FindFoldChangeFilter(columnFilters, out foldChangeUpdate);

            bool pValueUpdate;
            var pValueFilter = FindPValueFilter(columnFilters, out pValueUpdate);

            if (filter)
            {
                if (foldChangeFilter != null)
                    _absLog2FoldChangeFilter = foldChangeFilter;

                if (pValueFilter != null)
                    _pValueFilter = pValueFilter;

                if (CutoffSettings.FoldChangeCutoffValid == (foldChangeFilter != null) &&
                    CutoffSettings.PValueCutoffValid == (pValueFilter != null) && !foldChangeUpdate && !pValueUpdate)
                {
                    return false;
                }
            }
            else
            {
                if (_absLog2FoldChangeFilter == null && _pValueFilter == null)
                {
                    return false;
                }
            }

            var removeAbsLog2 = !filter && absLog2FCExists && _absLog2FoldChangeFilter != null;

            columnFilters.Remove(_absLog2FoldChangeFilter);
            columnFilters.Remove(_pValueFilter);
            _absLog2FoldChangeFilter = _pValueFilter = null;

            if (AnyCutoffSettingsValid && filter)
            {
                var missingColumns = new List<ColumnSpec>();
                if (CutoffSettings.FoldChangeCutoffValid && !absLog2FCExists)
                    missingColumns.Add(new ColumnSpec(PropertyPath.Root.Property(@"FoldChangeResult").Property(@"AbsLog2FoldChange")));
                if (CutoffSettings.PValueCutoffValid && !pValueExists)
                    missingColumns.Add(new ColumnSpec(PropertyPath.Root.Property(@"FoldChangeResult").Property(@"AdjustedPValue")));

                if (missingColumns.Any())
                    SetColumns(_bindingListSource.ViewSpec.Columns.Concat(missingColumns));

                columnFilters.Clear();
                if (CutoffSettings.FoldChangeCutoffValid)
                {
                    _absLog2FoldChangeFilter = CreateColumnFilter(new ColumnId(@"AbsLog2FoldChange"),
                        FilterOperations.OP_IS_GREATER_THAN, Settings.Default.Log2FoldChangeCutoff);
                    columnFilters.Add(_absLog2FoldChangeFilter);
                }

                if (CutoffSettings.PValueCutoffValid)
                {
                    _pValueFilter = CreateColumnFilter(new ColumnId(@"AdjustedPValue"),
                        FilterOperations.OP_IS_LESS_THAN, Math.Pow(10, -Settings.Default.PValueCutoff)); 
                    columnFilters.Add(_pValueFilter);  
                }
            }
            else
            {
                if (removeAbsLog2)
                {
                    // Remove AbsLog2FoldChange column
                    SetColumns(columns.Except(columns.Where(c => c.Name == @"FoldChangeResult.AbsLog2FoldChange")));
                }
            }

            _bindingListSource.RowFilter = _bindingListSource.RowFilter.SetColumnFilters(columnFilters);
            return true;
        }

        private RowFilter.ColumnFilter FindFoldChangeFilter(IList<RowFilter.ColumnFilter> filters, out bool needsUpdate)
        {
            return CheckFilters(filters, new ColumnId(@"AbsLog2FoldChange"), FilterOperations.OP_IS_GREATER_THAN,
                Settings.Default.Log2FoldChangeCutoff, out needsUpdate);
        }

        private RowFilter.ColumnFilter FindPValueFilter(IList<RowFilter.ColumnFilter> filters, out bool needsUpdate)
        {
            return CheckFilters(filters, new ColumnId(@"AdjustedPValue"), FilterOperations.OP_IS_LESS_THAN,
                Math.Pow(10, -Settings.Default.PValueCutoff), out needsUpdate);
        }

        RowFilter.ColumnFilter CheckFilters(IEnumerable<RowFilter.ColumnFilter> filters, ColumnId columnId, IFilterOperation filterOp, double operand, out bool needsUpdate)
        {
            var filter = filters.FirstOrDefault(f => Equals(f.ColumnId, columnId) &&
                                                     ReferenceEquals(f.Predicate.FilterOperation, filterOp));

            if (filter == null)
            {
                needsUpdate = false;
                return null;
            }

            needsUpdate =
                filter.Predicate.GetOperandDisplayText(_bindingListSource.ViewInfo.DataSchema, typeof(double)) !=
                operand.ToString(CultureInfo.CurrentCulture);

            return filter;
        }

        private void SetColumns(IEnumerable<ColumnSpec> columns)
        {
            _bindingListSource.SetViewContext(_bindingListSource.ViewContext,
                new ViewInfo(_bindingListSource.ViewInfo.DataSchema,
                    typeof(FoldChangeBindingSource.FoldChangeRow),
                    _bindingListSource.ViewSpec.SetColumns(columns)));
        }

        private RowFilter.ColumnFilter CreateColumnFilter(ColumnId columnId, IFilterOperation filterOp, double operand)
        {
            var op = FilterPredicate.CreateFilterPredicate(_bindingListSource.ViewInfo.DataSchema,
                typeof(double), filterOp,
                operand.ToString(CultureInfo.CurrentCulture));

            return new RowFilter.ColumnFilter(columnId, op);
        }

        public Rectangle ScreenRect { get { return  Screen.GetBounds(this); } }
        public bool AllowDisplayTip { get; private set; }
        public Rectangle RectToScreen(Rectangle r)
        {
            return RectangleToScreen(r);
        }

        #region Functional Test Support

        public bool UseOverridenKeys { get; set; }
        public Keys OverridenModifierKeys { get; set; }
        private new Keys ModifierKeys
        {
            get { return UseOverridenKeys ? OverridenModifierKeys : Control.ModifierKeys; }
        }

        public bool UpdatePending { get { return _updatePending; } }

        public FoldChangeBindingSource.FoldChangeRow GetSelectedRow()
        {
            return zedGraphControl.GraphPane.CurveList[0].Points[0].Tag as FoldChangeBindingSource.FoldChangeRow;
        }

        public Point GraphToScreenCoordinates(double x, double y)
        {
            var pt = new PointF((float)x, (float)y);
            pt = zedGraphControl.GraphPane.GeneralTransform(pt, CoordType.AxisXYScale);
            return new Point((int)pt.X, (int)pt.Y);
        }

        public int MatchedPointsStartIndex
        {
            get { return 1 + (CutoffSettings.FoldChangeCutoffValid ? 2 : 0) + (CutoffSettings.PValueCutoffValid ? 1 : 0); }
        }

        public CurveCounts GetCurveCounts()
        {
            var curveList = zedGraphControl.GraphPane.CurveList;

            var selectedCount = curveList[0].Points.Count;
            var outCount = 0;
            var inCount = 0;

            var otherPoints = curveList[MatchedPointsStartIndex].Points;
            for (var i = 0; i < otherPoints.Count; ++i)
            {
                var pair = otherPoints[i];
                var row = (FoldChangeBindingSource.FoldChangeRow) pair.Tag;
                var pvalue = -Math.Log10(Math.Max(MIN_PVALUE, row.FoldChangeResult.AdjustedPValue));

                if ((!CutoffSettings.FoldChangeCutoffValid || row.FoldChangeResult.AbsLog2FoldChange > CutoffSettings.Log2FoldChangeCutoff) &&
                    (!CutoffSettings.PValueCutoffValid || pvalue > CutoffSettings.PValueCutoff))
                    ++outCount;
                else
                    ++inCount;
            }

            return new CurveCounts(curveList.Count, selectedCount,
                outCount, inCount);
        }

        public List<LabeledPoint> LabeledPoints
        {
            get { return _labeledPoints; }
        }

        public GraphObjList GraphObjList
        {
            get { return zedGraphControl.GraphPane.GraphObjList; }
        }

        public CurveList CurveList
        {
            get { return zedGraphControl.GraphPane.CurveList; }
        }
            
        public class CurveCounts
        {
            public CurveCounts(int curveCount, int selectedCount, int outCount, int inCount)
            {
                CurveCount = curveCount;
                SelectedCount = selectedCount;
                OutCount = outCount;
                InCount = inCount;
            }

            public int CurveCount { get; private set; }
            public int SelectedCount { get; private set; }
            public int OutCount { get; private set; }
            public int InCount { get; private set; }
        }

        public LabelLayout LabelLayout
        {
            get { return zedGraphControl.GraphPane.Layout; }
        }

        #endregion
    }
}
