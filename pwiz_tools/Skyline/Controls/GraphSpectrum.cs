/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.MSGraph;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Controls
{
    /// <summary>
    /// Interface for any window that contains a graph, to allow non-blocking
    /// updates with a <see cref="Timer"/>.
    /// </summary>
    public interface IGraphContainer
    {
        /// <summary>
        /// Render the graph.
        /// </summary>
        void UpdateGraph();

        /// <summary>
        /// Locks/unlocks the Y-axis, so that it auto-scales.
        /// </summary>
        /// <param name="lockY">True to use Y-axis auto-scaling</param>
        void LockYAxis(bool lockY);
    }

    public partial class GraphSpectrum : DockableForm, IGraphContainer
    {
        public interface IStateProvider
        {
            TreeNode SelectedNode { get; }
            IList<IonType> ShowIonTypes { get; }
            IList<int> ShowIonCharges { get; }

            void BuildSpectrumMenu(ContextMenuStrip menuStrip);
        }

        private class DefaultStateProvider : IStateProvider
        {
            public TreeNode SelectedNode { get { return null; } }
            public IList<IonType> ShowIonTypes { get { return new[] { IonType.y }; } }
            public IList<int> ShowIonCharges { get { return new[] { 1 }; } }
            public void BuildSpectrumMenu(ContextMenuStrip menuStrip) {}
        }

        private readonly IDocumentUIContainer _documentContainer;
        private readonly IStateProvider _stateProvider;

        public GraphSpectrum(IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();

            graphControl.MasterPane.Border.IsVisible = false;
            var graphPane = GraphPane;
            graphPane.Border.IsVisible = false;
            graphPane.Title.IsVisible = true;
            graphPane.AllowCurveOverlap = true;

            _documentContainer = documentUIContainer;
            _documentContainer.ListenUI(OnDocumentUIChanged);
            _stateProvider = documentUIContainer as IStateProvider ??
                new DefaultStateProvider();

            if (DocumentUI != null)
                ZoomSpectrumToSettings();
        }

        private SrmDocument DocumentUI { get { return _documentContainer.DocumentUI; } }

        private MSGraphPane GraphPane { get { return (MSGraphPane) graphControl.MasterPane[0]; } }

        public void OnDocumentUIChanged(object sender, DocumentChangedEventArgs e)
        {
            // If document changed, update spectrum x scale to instrument
            if (e.DocumentPrevious == null ||
                    !ReferenceEquals(DocumentUI.Id, e.DocumentPrevious.Id) ||
                    !ReferenceEquals(DocumentUI.Settings.PeptideSettings.Libraries.Libraries,
                        e.DocumentPrevious.Settings.PeptideSettings.Libraries.Libraries))
            {
                ZoomSpectrumToSettings();
                UpdateGraph();
            }
        }

        public void ZoomSpectrumToSettings()
        {
            var axis = GraphPane.XAxis;
            var instrument = DocumentUI.Settings.TransitionSettings.Instrument;
            axis.Scale.Min = instrument.MinMz;
            axis.Scale.MinAuto = false;
            axis.Scale.Max = instrument.MaxMz;
            axis.Scale.MaxAuto = false;
            graphControl.Refresh();
        }

        private void GraphSpectrum_VisibleChanged(object sender, System.EventArgs e)
        {
            UpdateGraph();
        }

        public void UpdateGraph()
        {
            // Only worry about updates, if the graph is visible
            // And make sure it is not disposed, since rendering happens on a timer
            if (!Visible || IsDisposed)
                return;

            // Try to find a tree node with spectral library info associated
            // with the current selection.
            var nodeTree = _stateProvider.SelectedNode as SrmTreeNode;
            var nodeGroupTree = nodeTree as TransitionGroupTreeNode;
            var nodeTranTree = nodeTree as TransitionTreeNode;
            if (nodeTranTree != null)
                nodeGroupTree = nodeTranTree.Parent as TransitionGroupTreeNode;

            var nodeGroup = (nodeGroupTree != null ? nodeGroupTree.DocNode : null);
            PeptideTreeNode nodePepTree;
            if (nodeGroup == null)
            {
                nodePepTree = nodeTree as PeptideTreeNode;
                if (nodePepTree != null && SameChargeGroups(nodePepTree))
                    nodeGroup = nodePepTree.ChildDocNodes[0] as TransitionGroupDocNode;
            }
            else
            {
                nodePepTree = nodeGroupTree.Parent as PeptideTreeNode;
            }
            ExplicitMods mods = (nodePepTree != null ? nodePepTree.DocNode.ExplicitMods : null);

            // Clear existing data from the graph pane
            var graphPane = (MSGraphPane)graphControl.MasterPane[0];
            graphPane.CurveList.Clear();
            graphPane.GraphObjList.Clear();

            // Check for appropriate spectrum to load
            bool available = false;
            if (nodeGroup != null && nodeGroup.HasLibInfo)
            {
                SrmSettings settings = DocumentUI.Settings;
                PeptideLibraries libraries = settings.PeptideSettings.Libraries;
                TransitionGroup group = nodeGroup.TransitionGroup;
                TransitionDocNode transition = (nodeTranTree == null ? null : nodeTranTree.DocNode);
                try
                {
                    SpectrumPeaksInfo spectrumInfo;
                    IsotopeLabelType typeInfo;
                    if (libraries.HasLibraries && libraries.IsLoaded &&
                        settings.TryLoadSpectrum(group.Peptide.Sequence, group.PrecursorCharge, mods,
                            out typeInfo, out spectrumInfo))
                    {
                        var types = _stateProvider.ShowIonTypes;
                        var charges = _stateProvider.ShowIonCharges;
                        var rankTypes = settings.TransitionSettings.Filter.IonTypes;
                        var rankCharges = settings.TransitionSettings.Filter.ProductCharges;
                        // Make sure the types and charges in the settings are at the head
                        // of these lists to give them top priority, and get rankings correct.
                        int i = 0;
                        foreach (IonType type in rankTypes)
                        {
                            if (types.Remove(type))
                                types.Insert(i++, type);
                        }
                        i = 0;
                        foreach (int charge in rankCharges)
                        {
                            if (charges.Remove(charge))
                                charges.Insert(i++, charge);
                        }
                        var spectrumInfoR = new LibraryRankedSpectrumInfo(spectrumInfo,
                                                                          typeInfo,
                                                                          group,
                                                                          settings,
                                                                          mods,
                                                                          charges,
                                                                          types,
                                                                          rankCharges,
                                                                          rankTypes);

                        var graphItem = new SpectrumGraphItem(nodeGroup, transition, spectrumInfoR)
                        {
                            ShowTypes = types,
                            ShowCharges = charges,
                            ShowRanks = Settings.Default.ShowRanks,
                            ShowDuplicates = Settings.Default.ShowDuplicateIons,
                            FontSize = Settings.Default.SpectrumFontSize,
                            LineWidth = Settings.Default.SpectrumLineWidth
                        };
                        graphControl.IsEnableVPan = graphControl.IsEnableVZoom =
                            !Settings.Default.LockYAxis;
                        AddGraphItem(graphPane, graphItem);
                        available = true;
                    }
                }
                catch (IOException)
                {
                    AddGraphItem(graphPane, new NoDataMSGraphItem("Failure loading spectrum. Library may be corrupted."));
                    return;
                }
            }
            // Show unavailable message, if no spectrum loaded
            if (!available)
                AddGraphItem(graphPane, new UnavailableMSGraphItem());
        }

        public void LockYAxis(bool lockY)
        {
            graphControl.IsEnableVPan = graphControl.IsEnableVZoom = !lockY;
            graphControl.Refresh();
        }

        private void AddGraphItem(MSGraphPane pane, IMSGraphItemInfo item)
        {
            pane.Title.Text = item.Title;
            graphControl.AddGraphItem(pane, item);
            pane.CurveList[0].Label.IsVisible = false;
            graphControl.Refresh();
        }

        private static bool SameChargeGroups(PeptideTreeNode nodeTree)
        {
            // Check to see if all transition groups under a peptide tree node
            // have the same precursor charge.
            int charge = 0;
            foreach (TransitionGroupDocNode nodeGroup in nodeTree.DocNode.Children)
            {
                if (charge == 0)
                    charge = nodeGroup.TransitionGroup.PrecursorCharge;
                else if (charge != nodeGroup.TransitionGroup.PrecursorCharge)
                    return false;
            }
            // True only if there was at least one group
            return (charge != 0);
        }

        private void graphControl_ContextMenuBuilder(ZedGraphControl sender,
            ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            _stateProvider.BuildSpectrumMenu(menuStrip);
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _documentContainer.UnlistenUI(OnDocumentUIChanged);
        }

        private void GraphSpectrum_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    _documentContainer.FocusDocument();
                    break;
            }
        }
    }
}
