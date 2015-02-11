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
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public partial class FoldChangeBarGraph : FoldChangeForm
    {
        private BindingListSource _bindingListSource;
        private AxisLabelScaler _axisLabelScaler;
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
                UpdateGraph();
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
            var points = new PointPairList();
            var groupComparisonModel = FoldChangeBindingSource.GroupComparisonModel;
            var groupComparisonDef = groupComparisonModel.GroupComparisonDef;
            var document = groupComparisonModel.Document;
            var sequences = new List<Tuple<string, bool>>();
            foreach (var nodePep in document.Molecules)
                sequences.Add(new Tuple<string, bool>(nodePep.RawTextId, nodePep.IsProteomic));
            var uniquePrefixGenerator = new UniquePrefixGenerator(sequences, 3);
            var textLabels = new List<string>();

            foreach (var rowItem in _bindingListSource.OfType<RowItem>())
            {
                var row = rowItem.Value as FoldChangeBindingSource.FoldChangeRow;
                if (null == row)
                {
                    continue;
                }
                var foldChangeResult = row.FoldChangeResult;
                var point = MeanErrorBarItem.MakePointPair(points.Count, foldChangeResult.Log2FoldChange,
                    Math.Log(foldChangeResult.MaxFoldChange / foldChangeResult.FoldChange, 2.0));
                points.Add(point);
                if (null != row.Peptide)
                {
                    string label = uniquePrefixGenerator.GetUniquePrefix(row.Peptide.GetDocNode().RawTextId, row.Peptide.GetDocNode().IsProteomic);
                    textLabels.Add(label);
                }
                else
                {
                    textLabels.Add(row.Protein.Name);
                }
            }
            zedGraphControl.GraphPane.XAxis.Title.Text = groupComparisonDef.PerProtein ? GroupComparisonStrings.FoldChangeBarGraph_UpdateGraph_Protein : GroupComparisonStrings.FoldChangeBarGraph_UpdateGraph_Peptide;
            zedGraphControl.GraphPane.YAxis.Title.Text = GroupComparisonStrings.FoldChangeBarGraph_UpdateGraph_Log_2_Fold_Change;
            zedGraphControl.GraphPane.CurveList.Add(new MeanErrorBarItem(null, points, Color.Black, Color.Blue));
            zedGraphControl.GraphPane.XAxis.Type = AxisType.Text;
            zedGraphControl.GraphPane.XAxis.Scale.TextLabels = textLabels.ToArray();
            _axisLabelScaler.ScaleAxisLabels();
            zedGraphControl.GraphPane.AxisChange();
            zedGraphControl.Invalidate();
        }

        private void BindingListSourceOnListChanged(object sender, ListChangedEventArgs listChangedEventArgs)
        {
            UpdateGraph();
        }

        public ZedGraphControl ZedGraphControl { get { return zedGraphControl; } }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (null != _axisLabelScaler)
            {
                _axisLabelScaler.ScaleAxisLabels();
            }
        }
    }
}