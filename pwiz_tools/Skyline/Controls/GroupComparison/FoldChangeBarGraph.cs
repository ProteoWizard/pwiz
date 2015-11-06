/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public partial class FoldChangeBarGraph : FoldChangeForm
    {
        private BindingListSource _bindingListSource;
        private AxisLabelScaler _axisLabelScaler;
        private CurveItem _barGraph;
        private FoldChangeBindingSource.FoldChangeRow[] _rows;
        private SkylineWindow _skylineWindow;
        private bool _updatePending;
        public FoldChangeBarGraph()
        {
            InitializeComponent();
            zedGraphControl.GraphPane.Title.Text = null;
            zedGraphControl.MasterPane.Border.IsVisible = false;
            zedGraphControl.GraphPane.Border.IsVisible = false;
            zedGraphControl.GraphPane.Chart.Border.IsVisible = false;
            zedGraphControl.GraphPane.XAxis.MajorTic.IsOpposite = false;
            zedGraphControl.GraphPane.YAxis.MajorTic.IsOpposite = false;
            zedGraphControl.GraphPane.XAxis.MinorTic.IsOpposite = false;
            zedGraphControl.GraphPane.YAxis.MinorTic.IsOpposite = false;

            _axisLabelScaler = new AxisLabelScaler(zedGraphControl.GraphPane);
        }

        public override string GetTitle(string groupComparisonName)
        {
            return base.GetTitle(groupComparisonName) + ':' + GroupComparisonStrings.FoldChangeBarGraph_GetTitle_Graph;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (null != FoldChangeBindingSource)
            {
                _bindingListSource = FoldChangeBindingSource.GetBindingListSource();
                _bindingListSource.ListChanged += BindingListSourceOnListChanged;
                if (_skylineWindow == null)
                {
                    _skylineWindow = ((SkylineDataSchema) _bindingListSource.ViewInfo.DataSchema).SkylineWindow;
                    if (_skylineWindow != null)
                    {
                        _skylineWindow.SequenceTree.AfterSelect += SequenceTreeOnAfterSelect;
                    }
                }
                UpdateGraph();
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_skylineWindow != null)
            {
                _skylineWindow.SequenceTree.AfterSelect -= SequenceTreeOnAfterSelect;
                _skylineWindow = null;
            }
            base.OnHandleDestroyed(e);
        }

        private void SequenceTreeOnAfterSelect(object sender, TreeViewEventArgs treeViewEventArgs)
        {
            QueueUpdateGraph();
        }

        public void QueueUpdateGraph()
        {
            if (!IsHandleCreated)
            {
                return;
            }
            if (!_updatePending)
            {
                _updatePending = true;
                BeginInvoke(new Action(() =>
                {
                    _updatePending = false;
                    UpdateGraph();
                }));
            }
        }

        private void UpdateGraph()
        {
            if (!IsHandleCreated)
            {
                return;
            }
            zedGraphControl.GraphPane.GraphObjList.Clear();
            zedGraphControl.GraphPane.CurveList.Clear();
            _barGraph = null;
            _rows = null;
            var points = new PointPairList();
            var groupComparisonModel = FoldChangeBindingSource.GroupComparisonModel;
            var groupComparisonDef = groupComparisonModel.GroupComparisonDef;
            var document = groupComparisonModel.Document;
            var sequences = new List<Tuple<string, bool>>();
            foreach (var nodePep in document.Molecules)
                sequences.Add(new Tuple<string, bool>(nodePep.RawTextId, nodePep.IsProteomic));
            var uniquePrefixGenerator = new UniquePrefixGenerator(sequences, 3);
            var textLabels = new List<string>();
            var rows = _bindingListSource.OfType<RowItem>()
                .Select(rowItem => rowItem.Value)
                .OfType<FoldChangeBindingSource.FoldChangeRow>()
                .ToArray();
            bool showLabelType = rows.Select(row => row.IsotopeLabelType).Distinct().Count() > 1;
            bool showMsLevel = rows.Select(row => row.MsLevel).Distinct().Count() > 1;
            bool showGroup = rows.Select(row => row.Group).Distinct().Count() > 1;
            foreach (var row in rows)
            {
                var foldChangeResult = row.FoldChangeResult;
                double error = Math.Log(foldChangeResult.MaxFoldChange/foldChangeResult.FoldChange, 2.0);
                var point = MeanErrorBarItem.MakePointPair(points.Count, foldChangeResult.Log2FoldChange, error);
                points.Add(point);
                string label;
                if (null != row.Peptide)
                {
                    label = uniquePrefixGenerator.GetUniquePrefix(row.Peptide.GetDocNode().RawTextId, row.Peptide.GetDocNode().IsProteomic);
                }
                else
                {
                    label = row.Protein.Name;
                }
                if (showMsLevel && row.MsLevel.HasValue)
                {
                    label += " MS" + row.MsLevel; // Not L10N;
                }
                if (showLabelType && row.IsotopeLabelType != null)
                {
                    label += " (" + row.IsotopeLabelType.Title + ")"; // Not L10N
                }
                if (showGroup && !Equals(row.Group, default(GroupIdentifier)))
                {
                    label += " " + row.Group; // Not L10N
                }
                textLabels.Add(label);
                if (IsSelected(row))
                {
                    double y, height;
                    if (foldChangeResult.Log2FoldChange >= 0)
                    {
                        y = foldChangeResult.Log2FoldChange + error;
                        height = y;
                    }
                    else
                    {
                        y = 0;
                        height = error - foldChangeResult.Log2FoldChange;
                    }
                    zedGraphControl.GraphPane.GraphObjList.Add(new BoxObj(point.X + .5, y, .99, height)
                    {
                        ZOrder = ZOrder.E_BehindCurves,
                        IsClippedToChartRect = true
                    });
                }
            }
            zedGraphControl.GraphPane.XAxis.Title.Text = groupComparisonDef.PerProtein ? GroupComparisonStrings.FoldChangeBarGraph_UpdateGraph_Protein : GroupComparisonStrings.FoldChangeBarGraph_UpdateGraph_Peptide;
            zedGraphControl.GraphPane.YAxis.Title.Text = GroupComparisonStrings.FoldChangeBarGraph_UpdateGraph_Log_2_Fold_Change;
            var barGraph = new MeanErrorBarItem(null, points, Color.Black, Color.Blue);
            zedGraphControl.GraphPane.CurveList.Add(barGraph);
            zedGraphControl.GraphPane.XAxis.Type = AxisType.Text;
            zedGraphControl.GraphPane.XAxis.Scale.TextLabels = textLabels.ToArray();
            _axisLabelScaler.ScaleAxisLabels();
            zedGraphControl.GraphPane.AxisChange();
            zedGraphControl.Invalidate();
            _barGraph = barGraph;
            _rows = rows;
        }

        private void BindingListSourceOnListChanged(object sender, ListChangedEventArgs listChangedEventArgs)
        {
            QueueUpdateGraph();
        }

        public ZedGraphControl ZedGraphControl { get { return zedGraphControl; } }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (null != _axisLabelScaler)
            {
                _axisLabelScaler.ScaleAxisLabels();
                zedGraphControl.AxisChange();
            }
        }

        private bool zedGraphControl_MouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            var foldChangeRow = FoldChangeRowFromPoint(e.Location);
            if (null == foldChangeRow)
            {
                return false;
            }
            sender.Cursor = Cursors.Hand;
            return true;
        }

        private bool zedGraphControl_MouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            var foldChangeRow = FoldChangeRowFromPoint(e.Location);
            if (null == foldChangeRow)
            {
                return false;
            }
            if (null != foldChangeRow.Peptide)
            {
                foldChangeRow.Peptide.LinkValueOnClick(sender, new EventArgs());
            }
            else if (null != foldChangeRow.Protein)
            {
                foldChangeRow.Protein.LinkValueOnClick(sender, new EventArgs());
            }
            else
            {
                return false;
            }
            return true;
        }

        private FoldChangeBindingSource.FoldChangeRow FoldChangeRowFromPoint(PointF pt)
        {
            if (null == _barGraph || null == _rows)
            {
                return null;
            }
            CurveItem nearestCurve;
            int iNearest;
            if (!zedGraphControl.GraphPane.FindNearestPoint(pt, _barGraph, out nearestCurve, out iNearest))
            {
                return null;
            }
            if (iNearest < 0 || iNearest >= _rows.Length)
            {
                return null;
            }
            return _rows[iNearest];
        }

        private bool IsSelected(FoldChangeBindingSource.FoldChangeRow row)
        {
            if (null == _skylineWindow)
            {
                return false;
            }
            if (row.Peptide != null)
            {
                return IsPathSelected(_skylineWindow.SelectedPath, row.Peptide.IdentityPath);
            }
            return IsPathSelected(_skylineWindow.SelectedPath, row.Protein.IdentityPath);
        }

        private bool IsPathSelected(IdentityPath selectedPath, IdentityPath identityPath)
        {
            if (selectedPath.Depth < identityPath.Depth)
            {
                return false;
            }
            return Equals(selectedPath.GetPathTo(identityPath.Depth), identityPath);
        }

        private void zedGraphControl_ContextMenuBuilder(ZedGraphControl graphControl, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            ZedGraphHelper.BuildContextMenu(graphControl, menuStrip, true);
        }
    }
}