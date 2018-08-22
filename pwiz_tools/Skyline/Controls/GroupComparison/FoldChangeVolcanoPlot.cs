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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
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
        private readonly List<LabeledPoint> _labeledPoints;

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
            get { return Document.DocumentType != SrmDocument.DOCUMENT_TYPE.small_molecules; }
        }

        public bool AnyMolecules
        {
            get { return Document.DocumentType != SrmDocument.DOCUMENT_TYPE.proteomic; }
        }

        public bool PerProtein
        {
            get { return GroupComparisonDef.PerProtein; }
        }

        public static FontSpec CreateFontSpec(Color color, float size)
        {
            return new FontSpec("Arial", size, color, false, false, false, Color.Empty, null, FillType.None) // Not L10N
            {
                Border = { IsVisible = false }
            };
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

            foreach (var labeledPoint in _labeledPoints)
                if (labeledPoint.Label != null)
                    labeledPoint.Label.Location.Y = labeledPoint.Point.Y + labeledPoint.Label.FontSpec.Size / 2.0f /
                                                    pane.Rect.Height * (pane.YAxis.Scale.Max - pane.YAxis.Scale.Min);
        }

        public class LabeledPoint
        {
            public LabeledPoint(PointPair point, TextObj label, bool isSelected)
            {
                Point = point;
                Label = label;
                IsSelected = isSelected;
            }

            public PointPair Point { get; private set; }
            public TextObj Label { get; private set; }

            public bool IsSelected { get; private set; }
        }

        private void GraphPane_AxisChangeEvent(GraphPane pane)
        {
            AdjustLocations(pane);
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

            if (FoldChangeBindingSource != null)
            {
                AllowDisplayTip = true;

                _bindingListSource = FoldChangeBindingSource.GetBindingListSource();
                _bindingListSource.ListChanged += BindingListSourceOnListChanged;
                _bindingListSource.AllRowsChanged += BindingListSourceAllRowsChanged;
                zedGraphControl.GraphPane.AxisChangeEvent += GraphPane_AxisChangeEvent;

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

        private class PropertiesCutoffSettings : CutoffSettings
        {
            public override double Log2FoldChangeCutoff
            {
                get { return Settings.Default.Log2FoldChangeCutoff; }
                set { Settings.Default.Log2FoldChangeCutoff = value; } 
            }

            public override double PValueCutoff
            {
                get { return Settings.Default.PValueCutoff; }
                set { Settings.Default.PValueCutoff = value; } 
            }
        }

        public static CutoffSettings CutoffSettings = new PropertiesCutoffSettings();

        public static bool FoldChangCutoffValid
        {
            get { return !double.IsNaN(Settings.Default.Log2FoldChangeCutoff) && Settings.Default.Log2FoldChangeCutoff != 0.0; }
        }

        public static bool PValueCutoffValid
        {
            get { return !double.IsNaN(Settings.Default.PValueCutoff) && Settings.Default.PValueCutoff >= 0.0; }
        }

        public static bool AnyCutoffSettingsValid
        {
            get { return FoldChangCutoffValid || PValueCutoffValid; }
        }

        public static float PointSizeToFloat(PointSize pointSize)
        {
            //return 12.0f + 2.0f * ((int) pointSize - 2);
            return ((GraphFontSize[]) GraphFontSize.FontSizes)[(int) pointSize].PointSize;
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

            var rows = _bindingListSource.OfType<RowItem>()
                .Select(rowItem => rowItem.Value)
                .OfType<FoldChangeBindingSource.FoldChangeRow>()
                .ToArray();

            var selectedPoints = new PointPairList();
            var otherPoints = new PointPairList();

            var count = 0;

            // Create points and Selection objects
            foreach (var row in rows.OrderBy(r => r.FoldChangeResult.AdjustedPValue))
            {
                var foldChange = row.FoldChangeResult.Log2FoldChange;
                var pvalue = -Math.Log10(Math.Max(MIN_PVALUE, row.FoldChangeResult.AdjustedPValue));

                var point = new PointPair(foldChange, pvalue) { Tag = row };
                if (Settings.Default.GroupComparisonShowSelection && count < MAX_SELECTED && IsSelected(row))
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
            AddPoints(selectedPoints, Color.Red, PointSizeToFloat(PointSize.large), true, PointSymbol.Circle, true);

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
                    AddPoints(new PointPairList(matchedPoints), colorRow.Color, PointSizeToFloat(row.PointSize), row.Labeled, row.PointSymbol);
                    otherPoints = new PointPairList(otherPoints.Except(matchedPoints).ToArray());
                }
            }

            AddPoints(otherPoints, Color.Gray, PointSizeToFloat(PointSize.small), false, PointSymbol.Circle);

            // The coordinates that depened on the axis scale dont matter here, the AxisChangeEvent will fix those
            // Insert after selected items, but before all other items
            var index = 1;
            if (FoldChangCutoffValid)
            {
                _foldChangeCutoffLine1 = CreateAndInsert(index++, Settings.Default.Log2FoldChangeCutoff, Settings.Default.Log2FoldChangeCutoff, 0.0, 0.0);
                _foldChangeCutoffLine2 = CreateAndInsert(index++, -Settings.Default.Log2FoldChangeCutoff, -Settings.Default.Log2FoldChangeCutoff, 0.0, 0.0);
            }

            if (PValueCutoffValid)
            {
                _minPValueLine = CreateAndInsert(index, 0.0, 0.0, Settings.Default.PValueCutoff, Settings.Default.PValueCutoff);
            }

            zedGraphControl.GraphPane.YAxis.Scale.Min = 0.0;
            zedGraphControl.GraphPane.XAxis.Scale.MinAuto = zedGraphControl.GraphPane.XAxis.Scale.MaxAuto = zedGraphControl.GraphPane.YAxis.Scale.MaxAuto = true;       
            zedGraphControl.GraphPane.AxisChange();
            zedGraphControl.GraphPane.XAxis.Scale.MinAuto = zedGraphControl.GraphPane.XAxis.Scale.MaxAuto = zedGraphControl.GraphPane.YAxis.Scale.MaxAuto = false;

            zedGraphControl.Invalidate();
        }
        // ReSharper restore PossibleMultipleEnumeration

        private static TextObj CreateLabel(PointPair point, Color color, float size)
        {
            var row = point.Tag as FoldChangeBindingSource.FoldChangeRow;
            if (row == null)
                return null;

            var text = MatchExpression.GetRowDisplayText(row.Protein, row.Peptide);

            var textObj = new TextObj(text, point.X, point.Y, CoordType.AxisXYScale, AlignH.Center, AlignV.Bottom)
            {
                IsClippedToChartRect = true,
                FontSpec = CreateFontSpec(color, size),
                ZOrder = ZOrder.A_InFront
            };

            return textObj;
        }

        public static SymbolType PointSymbolToSymbolType(PointSymbol symbol)
        {
            switch (symbol)
            {
                case PointSymbol.Circle:
                    return SymbolType.Circle;
                case PointSymbol.Square:
                    return SymbolType.Square;
                case PointSymbol.Triangle:
                    return SymbolType.Triangle;
                case PointSymbol.TriangleDown:
                    return SymbolType.TriangleDown;
                case PointSymbol.Diamond:
                    return SymbolType.Diamond;
                case PointSymbol.XCross:
                    return SymbolType.XCross;
                case PointSymbol.Plus:
                    return SymbolType.Plus;
                case PointSymbol.Star:
                    return SymbolType.Star;
                default:
                    return SymbolType.Circle;
            }
        }

        private bool HasOutline(PointSymbol pointSymbol)
        {
            return pointSymbol == PointSymbol.Circle || pointSymbol == PointSymbol.Square ||
                   pointSymbol == PointSymbol.Triangle || pointSymbol == PointSymbol.TriangleDown ||
                   pointSymbol == PointSymbol.Diamond;
        }

        private void AddPoints(PointPairList points, Color color, float size, bool labeled, PointSymbol pointSymbol, bool selected = false)
        {
            var symbolType = PointSymbolToSymbolType(pointSymbol);

            LineItem lineItem;
            if (HasOutline(pointSymbol))
            {
                lineItem = new LineItem(null, points, Color.Black, symbolType)
                {
                    Line = { IsVisible = false },
                    Symbol = { Border = { IsVisible = false }, Fill = new Fill(color), Size = size, IsAntiAlias = true}
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
                    _labeledPoints.Add(new LabeledPoint(point, label, selected));
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
                Line = { Style = DashStyle.Dash }
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
            if (TryGetNearestCurveItem(point, ref nearestCurveItem, ref index))
            {
                var lineItem = nearestCurveItem as LineItem;
                if (lineItem == null || index < 0 || index >= lineItem.Points.Count || lineItem[index].Tag == null)
                    return false;

                _selectedRow = (FoldChangeBindingSource.FoldChangeRow) lineItem[index].Tag;
                zedGraphControl.Cursor = Cursors.Hand;

                if (_tip == null)
                    _tip = new NodeTip(this) { Parent = ParentForm };

                _tip.SetTipProvider(new FoldChangeRowTipProvider(_selectedRow), new Rectangle(point, new Size()),
                    point);

                return true;
            }
            else
            {
                if (_tip != null)
                    _tip.HideTip();

                _selectedRow = null;
                return false;
            }
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

        public void Select(IdentityPath identityPath)
        {
            var skylineWindow = _skylineWindow;
            if (skylineWindow == null)
                return;

            var alreadySelected = IsPathSelected(skylineWindow.SelectedPath, identityPath);
            if (alreadySelected)
                skylineWindow.SequenceTree.SelectedNode = null;

            skylineWindow.SelectedPath = identityPath;
            skylineWindow.UpdateGraphPanes();
        }

        public void MultiSelect(IdentityPath identityPath)
        {
            var skylineWindow = _skylineWindow;
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

        public void Deselect(IdentityPath identityPath)
        {
            var skylineWindow = _skylineWindow;
            if (skylineWindow == null)
                return;

            var list = skylineWindow.SequenceTree.SelectedPaths.ToList();
            var selectedPath = GetSelectedPath(identityPath);
            if (selectedPath != null)
            {
                if (selectedPath.Depth < identityPath.Depth)
                {
                    var protein = (PeptideGroupDocNode) skylineWindow.DocumentUI.FindNode(selectedPath);
                    var peptide = (PeptideDocNode) skylineWindow.DocumentUI.FindNode(identityPath);

                    var peptides = protein.Peptides.Except(new[] { peptide });
                    list.Remove(selectedPath);
                    list.AddRange(peptides.Select(p => new IdentityPath(selectedPath, p.Id)));
                }
                else if (selectedPath.Depth > identityPath.Depth)
                {
                    var protein = (PeptideGroupDocNode) skylineWindow.DocumentUI.FindNode(identityPath);
                    var peptidePaths = protein.Peptides.Select(p => new IdentityPath(identityPath, p.Id));

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

        private bool IsSelected(FoldChangeBindingSource.FoldChangeRow row)
        {
            var docNode = row.Peptide ?? (SkylineDocNode)row.Protein;
            return _skylineWindow != null && GetSelectedPath(docNode.IdentityPath) != null;
        }

        public IdentityPath GetSelectedPath(IdentityPath identityPath)
        {
            var skylineWindow = _skylineWindow;
            return skylineWindow != null ? skylineWindow.SequenceTree.SelectedPaths.FirstOrDefault(p => IsPathSelected(p, identityPath)) : null;
        }

        public bool IsPathSelected(IdentityPath selectedPath, IdentityPath identityPath)
        {
            return selectedPath != null && identityPath != null &&
                selectedPath.Depth <= (int)SrmDocument.Level.Molecules && identityPath.Depth <= (int)SrmDocument.Level.Molecules &&
                (selectedPath.Depth >= identityPath.Depth && Equals(selectedPath.GetPathTo(identityPath.Depth), identityPath) ||
                selectedPath.Depth <= identityPath.Depth && Equals(identityPath.GetPathTo(selectedPath.Depth), selectedPath));
        }

        private bool zedGraphControl_MouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
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

            var isSelected = IsSelected(_selectedRow);
            var ctrl = ModifierKeys.HasFlag(Keys.Control);

            if (!ctrl)
            {
                Select(docNode.IdentityPath);
                return true; // No need to call UpdateGraph
            }
            else if (isSelected)
                Deselect(docNode.IdentityPath);
            else
                MultiSelect(docNode.IdentityPath);

            FormUtil.OpenForms.OfType<FoldChangeVolcanoPlot>().ForEach(v => v.QueueUpdateGraph()); // Update all volcano plots
            return true;
        }

        #endregion

        private void zedGraphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            BuildContextMenu(sender, menuStrip, mousePt, objState);
        }

        protected override void BuildContextMenu(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            base.BuildContextMenu(sender, menuStrip, mousePt, objState);

            // Find the first seperator
            var index = menuStrip.Items.OfType<ToolStripItem>().ToArray().IndexOf(t => t is ToolStripSeparator);

            if (index >= 0)
            {
                menuStrip.Items.Insert(index++, new ToolStripSeparator());
                menuStrip.Items.Insert(index++, new ToolStripMenuItem(GroupComparisonStrings.FoldChangeVolcanoPlot_BuildContextMenu_Properties___, null, OnPropertiesClick));
                menuStrip.Items.Insert(index++, new ToolStripMenuItem(GroupComparisonStrings.FoldChangeVolcanoPlot_BuildContextMenu_Formatting___, null, OnFormattingClick));
                menuStrip.Items.Insert(index++, new ToolStripMenuItem(GroupComparisonStrings.FoldChangeVolcanoPlot_BuildContextMenu_Selection, null, OnSelectionClick)
                    { Checked = Settings.Default.GroupComparisonShowSelection });
                if (AnyCutoffSettingsValid)
                {
                    menuStrip.Items.Insert(index++, new ToolStripSeparator());
                    menuStrip.Items.Insert(index, new ToolStripMenuItem(GroupComparisonStrings.FoldChangeVolcanoPlot_BuildContextMenu_Remove_Below_Cutoffs, null, OnRemoveBelowCutoffsClick));
                }  
            }
        }

        private void OnFormattingClick(object o, EventArgs eventArgs)
        {
            ShowFormattingDialog();
        }

        public void ShowFormattingDialog()
        {
            var foldChangeRows = _bindingListSource.OfType<RowItem>()
                .Select(rowItem => rowItem.Value)
                .OfType<FoldChangeBindingSource.FoldChangeRow>()
                .ToArray();

            var backup = GroupComparisonDef.ColorRows.Select(r => (MatchRgbHexColor)r.Clone()).ToArray();
            // This list will later be used as a BindingList, so we have to create a mutable clone
            var copy = GroupComparisonDef.ColorRows.Select(r => (MatchRgbHexColor) r.Clone()).ToList();
            using (var form = new VolcanoPlotFormattingDlg(this, copy, foldChangeRows,
                rows =>
                {
                    EditGroupComparisonDlg.ChangeGroupComparisonDef(false, GroupComparisonModel, GroupComparisonDef.ChangeColorRows(rows));
                    UpdateGraph();
                }))
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

        private void OnRemoveBelowCutoffsClick(object o, EventArgs eventArgs)
        {
            RemoveBelowCutoffs();
        }

        private int GetGlobalIndex(FoldChangeBindingSource.FoldChangeRow row)
        {
            return row.Peptide != null ? row.Peptide.DocNode.Id.GlobalIndex : row.Protein.DocNode.Id.GlobalIndex;
        }

        public void RemoveBelowCutoffs()
        {
            var rows = _bindingListSource.OfType<RowItem>()
                .Select(rowItem => rowItem.Value)
                .OfType<FoldChangeBindingSource.FoldChangeRow>()
                .ToArray();

            var foldchangeCutoff = Math.Abs(Settings.Default.Log2FoldChangeCutoff);
            var pvalueCutoff = Math.Pow(10, -Settings.Default.PValueCutoff);

            var indices =
                rows.Where(r => PValueCutoffValid && r.FoldChangeResult.AdjustedPValue >= pvalueCutoff || FoldChangCutoffValid && r.FoldChangeResult.AbsLog2FoldChange <= foldchangeCutoff)
                    .Select(GetGlobalIndex)
                    .Distinct()
                    .ToArray();

            _skylineWindow.ModifyDocument(GroupComparisonStrings.FoldChangeVolcanoPlot_RemoveBelowCutoffs_Remove_peptides_below_cutoffs, document => (SrmDocument)document.RemoveAll(indices));
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

            var absLog2FCExists = columns.Any(c => c.Name == "FoldChangeResult.AbsLog2FoldChange"); // Not L10N
            var pValueExists = columns.Any(c => c.Name == "FoldChangeResult.AdjustedPValue"); // Not L10N

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

                if (FoldChangCutoffValid == (foldChangeFilter != null) &&
                    PValueCutoffValid == (pValueFilter != null) && !foldChangeUpdate && !pValueUpdate)
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
                if (FoldChangCutoffValid && !absLog2FCExists)
                    missingColumns.Add(new ColumnSpec(PropertyPath.Root.Property("FoldChangeResult").Property("AbsLog2FoldChange"))); // Not L10N
                if (PValueCutoffValid && !pValueExists)
                    missingColumns.Add(new ColumnSpec(PropertyPath.Root.Property("FoldChangeResult").Property("AdjustedPValue"))); // Not L10N

                if (missingColumns.Any())
                    SetColumns(_bindingListSource.ViewSpec.Columns.Concat(missingColumns));

                columnFilters.Clear();
                if (FoldChangCutoffValid)
                {
                    _absLog2FoldChangeFilter = CreateColumnFilter(new ColumnId("AbsLog2FoldChange"), // Not L10N
                        FilterOperations.OP_IS_GREATER_THAN, Settings.Default.Log2FoldChangeCutoff);
                    columnFilters.Add(_absLog2FoldChangeFilter);
                }

                if (PValueCutoffValid)
                {
                    _pValueFilter = CreateColumnFilter(new ColumnId("AdjustedPValue"), // Not L10N
                        FilterOperations.OP_IS_LESS_THAN, Math.Pow(10, -Settings.Default.PValueCutoff)); 
                    columnFilters.Add(_pValueFilter);  
                }
            }
            else
            {
                if (removeAbsLog2)
                {
                    // Remove AbsLog2FoldChange column
                    SetColumns(columns.Except(columns.Where(c => c.Name == "FoldChangeResult.AbsLog2FoldChange"))); // Not L10N 
                }
            }

            _bindingListSource.RowFilter = _bindingListSource.RowFilter.SetColumnFilters(columnFilters);
            return true;
        }

        private RowFilter.ColumnFilter FindFoldChangeFilter(IList<RowFilter.ColumnFilter> filters, out bool needsUpdate)
        {
            return CheckFilters(filters, new ColumnId("AbsLog2FoldChange"), FilterOperations.OP_IS_GREATER_THAN, // Not L10N
                Settings.Default.Log2FoldChangeCutoff, out needsUpdate);
        }

        private RowFilter.ColumnFilter FindPValueFilter(IList<RowFilter.ColumnFilter> filters, out bool needsUpdate)
        {
            return CheckFilters(filters, new ColumnId("AdjustedPValue"), FilterOperations.OP_IS_LESS_THAN, // Not L10N
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
            get { return 1 + (FoldChangCutoffValid ? 2 : 0) + (PValueCutoffValid ? 1 : 0); }
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

        #endregion
    }
}