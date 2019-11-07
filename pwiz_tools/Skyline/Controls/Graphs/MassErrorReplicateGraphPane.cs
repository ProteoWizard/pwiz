/*
 * Original author: Alex MacLean <alexmaclean2000 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public class MassErrorReplicateGraphPane : SummaryReplicateGraphPane
    {
        public bool CanShowMassErrorLegend { get; private set; }

        public MassErrorReplicateGraphPane(GraphSummary graphSummary, PaneKey paneKey)
            : base(graphSummary)
        {
            PaneKey = paneKey;
        }

        public override void UpdateGraph(bool selectionChanged)
        {
            CurveList.Clear();
            GraphObjList.Clear();
            SrmDocument document = GraphSummary.DocumentUIContainer.DocumentUI;
            var selectedTreeNode = GraphSummary.StateProvider.SelectedNode as SrmTreeNode;
            if (selectedTreeNode == null || document.FindNode(selectedTreeNode.Path) == null)
            {
                Title.Text =  Helpers.PeptideToMoleculeTextMapper.Translate(Resources.MassErrorReplicateGraphPane_UpdateGraph_Select_a_peptide_to_see_the_mass_error_graph, document.DocumentType);
                EmptyGraph(document);
                return;
            }
            if (!document.Settings.HasResults)
            {
                Title.Text = Resources.AreaReplicateGraphPane_UpdateGraph_No_results_available;
                EmptyGraph(document);
                return;
            }
            DisplayTypeChrom displayType;
            if (Equals(PaneKey, PaneKey.PRECURSORS))
            {
                displayType = DisplayTypeChrom.precursors;
            }
            else if (Equals(PaneKey, PaneKey.PRODUCTS))
            {
                displayType = DisplayTypeChrom.products;
            }
            else
            {
                displayType = GraphChromatogram.GetDisplayType(document, selectedTreeNode);
            }
            Title.Text = null;
            var aggregateOp = GraphValues.AggregateOp.FromCurrentSettings();
            YAxis.Title.Text = aggregateOp.Cv
                ? aggregateOp.AnnotateTitle(Resources.MassErrorReplicateGraphPane_UpdateGraph_Mass_Error_No_Ppm)
                : Resources.MassErrorReplicateGraphPane_UpdateGraph_Mass_Error;
            DocNode selectedNode = selectedTreeNode.Model;
            DocNode parentNode = selectedNode;
            IdentityPath identityPath = selectedTreeNode.Path;
            // If the selected tree node is a transition, then its siblings are displayed.
            if (selectedTreeNode is TransitionTreeNode)
            {
                if (displayType != DisplayTypeChrom.single)
                {
                    SrmTreeNode parentTreeNode = selectedTreeNode.SrmParent;
                    parentNode = parentTreeNode.Model;
                    identityPath = parentTreeNode.Path;
                }
            }
            // If the selected node is a peptide with one child, then show the children,
            // unless chromatogram display type is total
            else if (selectedTreeNode is PeptideTreeNode)
            {
                var children = ((PeptideDocNode)selectedNode).TransitionGroups
                    .Where(PaneKey.IncludesTransitionGroup)
                    .ToArray();
                if (children.Length == 1 && displayType != DisplayTypeChrom.total)
                {
                    selectedNode = parentNode = children[0];
                    identityPath = new IdentityPath(identityPath, parentNode.Id);
                }
            }
            else if (!(selectedTreeNode is TransitionGroupTreeNode))
            {
                Title.Text =  Helpers.PeptideToMoleculeTextMapper.Translate(Resources.MassErrorReplicateGraphPane_UpdateGraph_Select_a_peptide_to_see_the_mass_error_graph, document.DocumentType);
                EmptyGraph(document);
                CanShowMassErrorLegend = false;
                return;
            }
            // If a precursor is going to be displayed with display type single
            if (parentNode is TransitionGroupDocNode && displayType == DisplayTypeChrom.single)
            {
                // If no optimization data, then show all the transitions  
                displayType = DisplayTypeChrom.all;
            }

            var replicateGroupOp = ReplicateGroupOp.FromCurrentSettings(document);
            GraphData graphData = new MassErrorGraphData(document,
                                            identityPath,
                                            displayType,
                                            replicateGroupOp,
                                            PaneKey);
           CanShowMassErrorLegend = graphData.DocNodes.Count != 0;
           InitFromData(graphData);

           int selectedReplicateIndex = SelectedIndex;
           double minRetentionTime = double.MaxValue;
           double maxRetentionTime = double.MinValue;
           
           int iColor = 0, iCharge = -1;
           var charge = Adduct.EMPTY;
           int countLabelTypes = document.Settings.PeptideSettings.Modifications.CountLabelTypes;
           int colorOffset = 0;
           var transitionGroupDocNode = parentNode as TransitionGroupDocNode;
           if (transitionGroupDocNode != null && displayType == DisplayTypeChrom.products)
           {
               // If we are only displaying product ions, we want to use an offset in the colors array
               // so that we do not re-use colors that would be used for any precursor ions.
               colorOffset =
                   GraphChromatogram.GetDisplayTransitions(transitionGroupDocNode, DisplayTypeChrom.precursors).Count();
           }
           for (int i = 0; i < graphData.DocNodes.Count; i++)
           {
                var docNode = graphData.DocNodes[i];
                var pointPairLists = graphData.PointPairLists[i];
                int numSteps = pointPairLists.Count / 2;
                for (int iStep = 0; iStep < pointPairLists.Count; iStep++)
                {
                    int step = iStep - numSteps;
                    var pointPairList = pointPairLists[iStep];
                    Color color;
                    // ReSharper disable ExpressionIsAlwaysNull
                    var nodeGroup = docNode as TransitionGroupDocNode;
                    if (parentNode is PeptideDocNode)
                    {
                        int iColorGroup = GetColorIndex(nodeGroup, countLabelTypes, ref charge, ref iCharge);
                        color = COLORS_GROUPS[iColorGroup % COLORS_GROUPS.Count];
                    }
                    else if (displayType == DisplayTypeChrom.total)
                    {
                        color = COLORS_GROUPS[iColor % COLORS_GROUPS.Count];
                    }
                    else if (docNode.Equals(selectedNode) && step == 0)
                    {
                        color = ChromGraphItem.ColorSelected;
                    }
                    else
                    {
                        color = COLORS_TRANSITION[(iColor + colorOffset) % COLORS_TRANSITION.Count];
                    }
                    // ReSharper restore ExpressionIsAlwaysNull
                    iColor++;

                    string label = graphData.DocNodeLabels[i];
                    if (step != 0)
                        label = string.Format(Resources.RTReplicateGraphPane_UpdateGraph_Step__0__, step);
                    BarItem curveItem = new MeanErrorBarItem(label, pointPairList, color, Color.Black);

                    if (selectedReplicateIndex != -1 && selectedReplicateIndex < pointPairList.Count)
                    {
                        PointPair pointPair = pointPairList[selectedReplicateIndex];
                        if (!pointPair.IsInvalid)
                        {
                            minRetentionTime = Math.Min(minRetentionTime, pointPair.Y);
                            maxRetentionTime = Math.Max(maxRetentionTime, pointPair.Y);
                        }
                    }

                    curveItem.Bar.Border.IsVisible = false;
                    curveItem.Bar.Fill.Brush = new SolidBrush(color);
                    curveItem.Tag = new IdentityPath(identityPath, docNode.Id);
                    CurveList.Add(curveItem);
                }
            }

           // Draw a box around the currently selected replicate
           if (ShowSelection && minRetentionTime != double.MaxValue)
           {
               maxRetentionTime = Math.Max(maxRetentionTime, 0);
               minRetentionTime = Math.Min(minRetentionTime, 0);
               GraphObjList.Add(new BoxObj(selectedReplicateIndex + .5, maxRetentionTime, 1,
                                           maxRetentionTime - minRetentionTime, Color.Black, Color.Empty)
               {
                   IsClippedToChartRect = true,
               });
           }

	        XAxis.Scale.MinAuto = XAxis.Scale.MaxAuto = selectionChanged;
            YAxis.Scale.MinAuto = YAxis.Scale.MaxAuto = true;
            if (Settings.Default.MinMassError != 0)
                YAxis.Scale.Min = Settings.Default.MinMassError;
            if (Settings.Default.MaxMassError != 0)
                YAxis.Scale.Max = Settings.Default.MaxMassError;
            Legend.IsVisible = Settings.Default.ShowMassErrorLegend;
            AxisChange();
        }

        private class MassErrorGraphData : GraphData
        {
            public MassErrorGraphData(SrmDocument document,
                IdentityPath identityPath,
                DisplayTypeChrom displayType,
                ReplicateGroupOp replicateGroupOp,
                PaneKey paneKey)
                : base(document, identityPath, displayType, replicateGroupOp, paneKey)
            {
            }

            protected override bool IsMissingValue(TransitionChromInfoData chromInfo)
            {
                return false;
            }

            protected override bool IsMissingValue(TransitionGroupChromInfoData chromInfoData)
            {
                return !GetValue(chromInfoData.ChromInfo).HasValue;
            }

            protected override PointPair CreatePointPair(int iResult, ICollection<TransitionChromInfoData> chromInfoDatas)
            {
                return ReplicateGroupOp.AggregateOp.MakeBarValue(iResult,
                    chromInfoDatas.Select(chromInfoData => (double)(GetValue(chromInfoData.ChromInfo) ?? 0)));
            }

            protected override PointPair CreatePointPair(int iResult, ICollection<TransitionGroupChromInfoData> chromInfoDatas)
            {
                return ReplicateGroupOp.AggregateOp.MakeBarValue(iResult,
                    chromInfoDatas.Select(chromInfoData => (double)(GetValue(chromInfoData.ChromInfo) ?? 0)));
            }

            private float? GetValue(TransitionGroupChromInfo chromInfo)
            {
                if (chromInfo == null)
                    return null;
        
                return chromInfo.MassError;       
            }

            private float? GetValue(TransitionChromInfo chromInfo)
            {
                if (chromInfo == null)
                    return null;
                
                return chromInfo.MassError;
            }
        }
    }
}
