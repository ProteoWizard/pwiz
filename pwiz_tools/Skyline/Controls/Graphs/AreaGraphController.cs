/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public enum GraphTypeArea { replicate, peptide }

    public enum AreaNormalizeToView{ area_percent_view, area_maximum_view, area_ratio_view, area_global_standard_view, none }

    public enum AreaScope{ document, protein }

    public sealed class AreaGraphController : GraphSummary.IController
    {
        public static GraphTypeArea GraphType
        {
            get { return Helpers.ParseEnum(Settings.Default.AreaGraphType, GraphTypeArea.replicate); }
            set { Settings.Default.AreaGraphType = value.ToString(); }
        }

        public static AreaNormalizeToView AreaView
        {
            get { return Helpers.ParseEnum(Settings.Default.AreaNormalizeToView, AreaNormalizeToView.none); }
            set { Settings.Default.AreaNormalizeToView = value.ToString(); }
        }

        public static AreaScope AreaScope
        {
            get { return Helpers.ParseEnum(Settings.Default.PeakAreaScope, AreaScope.document); }
            set { Settings.Default.PeakAreaScope = value.ToString(); }
        }

        public GraphSummary GraphSummary { get; set; }

        public IFormView FormView { get { return new GraphSummary.AreaGraphView(); } }

        public void OnActiveLibraryChanged()
        {
            if (GraphSummary.GraphPanes.OfType<AreaReplicateGraphPane>().Any())
                GraphSummary.UpdateUI();
        }

        public void OnResultsIndexChanged()
        {
            if (GraphSummary.GraphPanes.OfType<AreaReplicateGraphPane>().Any() /* || !Settings.Default.AreaAverageReplicates */ ||
                    RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.single)
                GraphSummary.UpdateUI();
        }

        public void OnRatioIndexChanged()
        {
            if (GraphSummary.GraphPanes.OfType<AreaReplicateGraphPane>().Any() /* || !Settings.Default.AreaAverageReplicates */)
                GraphSummary.UpdateUI();
        }

        public void OnUpdateGraph()
        {
            PaneKey[] paneKeys = null;
            if (Settings.Default.SplitChromatogramGraph)
            {
                if (GraphType == GraphTypeArea.replicate)
                {
                    var selectedTreeNode = GraphSummary.StateProvider.SelectedNode as SrmTreeNode;
                    if (null != selectedTreeNode)
                    {
                        TransitionGroupDocNode[] transitionGroups;
                        bool transitionSelected = false;
                        // ReSharper disable once CanBeReplacedWithTryCastAndCheckForNull
                        if (selectedTreeNode.Model is PeptideDocNode)
                        {
                            transitionGroups = ((PeptideDocNode) selectedTreeNode.Model).TransitionGroups.ToArray();
                        }
                        // ReSharper disable once CanBeReplacedWithTryCastAndCheckForNull
                        else if (selectedTreeNode.Model is TransitionGroupDocNode)
                        {
                            transitionGroups = new[] {(TransitionGroupDocNode) selectedTreeNode.Model};
                        }
                        else if (selectedTreeNode.Model is TransitionDocNode)
                        {
                            transitionGroups = new[]
                                {(TransitionGroupDocNode) ((SrmTreeNode) selectedTreeNode.Parent).Model};
                            transitionSelected = true;
                        }
                        else
                        {
                            transitionGroups = new TransitionGroupDocNode[0];
                        }
                        if (transitionGroups.Length == 1)
                        {
                            if (GraphChromatogram.DisplayType == DisplayTypeChrom.all 
                                || (GraphChromatogram.DisplayType == DisplayTypeChrom.single && !transitionSelected))
                            {
                                var transitionGroup = transitionGroups[0];
                                bool hasPrecursors = transitionGroup.Transitions.Any(transition => transition.IsMs1);
                                bool hasProducts = transitionGroup.Transitions.Any(transition => !transition.IsMs1);
                                if (hasPrecursors && hasProducts)
                                {
                                    paneKeys = new[] { PaneKey.PRECURSORS, PaneKey.PRODUCTS };
                                }
                            }
                        }
                        else if (transitionGroups.Length > 1)
                        {
                            paneKeys = transitionGroups.Select(group => new PaneKey(group))
                                .Distinct().ToArray();
                        }
                    }
                }
                else
                {
                    paneKeys = GraphSummary.StateProvider.SelectionDocument.MoleculeTransitionGroups.Select(
                            group => new PaneKey(group.TransitionGroup.LabelType)).Distinct().ToArray();
                }
            }
            paneKeys = paneKeys ?? new[] {PaneKey.DEFAULT};
            Array.Sort(paneKeys);

            bool panesValid = paneKeys.SequenceEqual(GraphSummary.GraphPanes.Select(pane => pane.PaneKey));
            if (panesValid)
            {
                switch (GraphType)
                {
                    case GraphTypeArea.replicate:
                        panesValid = GraphSummary.GraphPanes.All(pane => pane is AreaReplicateGraphPane);
                        break;
                    case GraphTypeArea.peptide:
                        panesValid = GraphSummary.GraphPanes.All(pane => pane is AreaPeptideGraphPane);
                        break;
                }
            }
            if (panesValid)
            {
                return;
            }

            switch (GraphType)
            {
                case GraphTypeArea.replicate:
                    GraphSummary.GraphPanes = paneKeys.Select(key => new AreaReplicateGraphPane(GraphSummary, key));
                    break;
                case GraphTypeArea.peptide:
                    GraphSummary.GraphPanes = paneKeys.Select(key => new AreaPeptideGraphPane(GraphSummary, key));
                    break;
            }
        }

        public bool HandleKeyDownEvent(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
//                case Keys.D3:
//                    if (e.Alt)
//                        GraphSummary.Hide();
//                    break;
                case Keys.F7:
                    if (!e.Alt && !(e.Shift && e.Control))
                    {
                        if (e.Control)
                            Settings.Default.AreaGraphType = GraphTypeArea.peptide.ToString();
                        else
                            Settings.Default.AreaGraphType = GraphTypeArea.replicate.ToString();
                        GraphSummary.UpdateUI();
                    }
                    break;
            }
            return false;
        }
    }
}
