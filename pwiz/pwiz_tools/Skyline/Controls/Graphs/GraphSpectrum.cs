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
using System;
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
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Interface for any window that contains a graph, to allow non-blocking
    /// updates with a <see cref="Timer"/>.
    /// </summary>
    public interface IGraphContainer : IUpdatable
    {
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

            void BuildSpectrumMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip);
        }

        private class DefaultStateProvider : IStateProvider
        {
            public TreeNode SelectedNode { get { return null; } }
            public IList<IonType> ShowIonTypes { get { return new[] { IonType.y }; } }
            public IList<int> ShowIonCharges { get { return new[] { 1 }; } }
            public void BuildSpectrumMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip) { }
        }

        private readonly IDocumentUIContainer _documentContainer;
        private readonly IStateProvider _stateProvider;
        private TransitionGroupDocNode _nodeGroup;

        public GraphSpectrum(IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();

            Icon = Resources.SkylineData;

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
        
        private SpectrumGraphItem GraphItem { get; set; }

        public string LibraryName { get { return GraphItem.LibraryName; } }
        public int PeaksCount { get { return GraphItem.PeaksCount; } }
        public int PeaksMatchedCount { get { return GraphItem.PeaksMatchedCount; } }
        public int PeaksRankedCount { get { return GraphItem.PeaksRankedCount; } }        
        public IEnumerable<string> IonLabels { get { return GraphItem.IonLabels; } }

        public string SelectedIonLabel
        {
            get
            {
                foreach (var graphObj in GraphPane.GraphObjList)
                {
                    var label = graphObj as TextObj;
                    if (label != null && label.FontSpec.FontColor == AbstractSpectrumGraphItem.COLOR_SELECTED)
                        return label.Text;
                }
                return null;
            }
        }

        public void OnDocumentUIChanged(object sender, DocumentChangedEventArgs e)
        {
            // If document changed, update spectrum x scale to instrument
            if (e.DocumentPrevious == null ||
                !ReferenceEquals(DocumentUI.Id, e.DocumentPrevious.Id) ||
                !ReferenceEquals(DocumentUI.Settings.PeptideSettings.Libraries.Libraries,
                                 e.DocumentPrevious.Settings.PeptideSettings.Libraries.Libraries))
            {
                ZoomSpectrumToSettings();
                UpdateUI();
            }
        }

        public void ZoomSpectrumToSettings()
        {
            var axis = GraphPane.XAxis;
            var instrument = DocumentUI.Settings.TransitionSettings.Instrument;
            if (!instrument.IsDynamicMin || _nodeGroup == null)
                axis.Scale.Min = instrument.MinMz;
            else
                axis.Scale.Min = instrument.GetMinMz(_nodeGroup.PrecursorMz);
            axis.Scale.MinAuto = false;
            axis.Scale.Max = instrument.MaxMz;
            axis.Scale.MaxAuto = false;
            graphControl.Refresh();
        }

        private void GraphSpectrum_VisibleChanged(object sender, EventArgs e)
        {
            UpdateUI();
        }

        private bool NodeGroupChanged(TransitionGroupDocNode nodeGroup)
        {
            return (_nodeGroup == null) ||
                   (!ReferenceEquals(_nodeGroup.Id, nodeGroup.Id));
        }

        private void UpdateToolbar(IList<SpectrumInfo> spectra)
        {
            if (spectra == null || spectra.Count < 2)
            {
                toolBar.Visible = false;
            }
            else
            {
                // Check to see if the list of files has changed.
                var listNames = new List<String>();
                for (int idx = 0; idx < spectra.Count; idx++)
                {
                    listNames.Add(spectra[idx].Identity);
                }

                var listExisting = new List<string>();
                foreach (var item in comboSpectrum.Items)
                {
                    listExisting.Add(item.ToString());
                }

                if (!ArrayUtil.EqualsDeep(listNames, listExisting))
                {
                    // If it has, update the list, trying to maintain selection, if possible.
                    object selected = comboSpectrum.SelectedItem;
                    // Unless the current selected index is the one matching the one currently
                    // in use by the precursor (zero), then try to stay viewing the in-use spectrum (zero)
                    int selectedIndex = comboSpectrum.SelectedIndex;

                    comboSpectrum.Items.Clear();
                    foreach (string name in listNames)
                    {
                        comboSpectrum.Items.Add(name);
                    }

                    if (selectedIndex == 0 || selected == null ||
                        comboSpectrum.Items.IndexOf(selected) == -1)
                    {
                        comboSpectrum.SelectedIndex = 0;
                    }
                    else
                    {
                        comboSpectrum.SelectedItem = selected;
                    }
                    ComboHelper.AutoSizeDropDown(comboSpectrum);
                }

                // Show the toolbar after updating the files
                if (!toolBar.Visible)
                {
                    toolBar.Visible = true;
                }
            }
        }

        /// <summary>
        /// If more than one spectrum is found for the current node group, 
        /// a toolbar with a drop-down combobox is shown, and one item is 
        /// selected in the combobox. This method searches in the spectra
        /// list passed in and finds the spectrum selected in the combobox,
        /// and gets the spectra list index of that particular spectrum.
        /// </summary>
        /// <param name="spectra"> The spectra list to be searched. </param>
        /// <returns> The index of the spectrum found in the spectra list. </returns>
        private int GetSelectedSpectrumIndex(IList<SpectrumInfo> spectra)
        {
            if (spectra.Count > 1 && comboSpectrum.SelectedItem != null)
            {
                string identity = comboSpectrum.SelectedItem.ToString();
                for (int idx = 0; idx < spectra.Count; idx++)
                {
                    if (Equals(identity, spectra[idx].Identity))
                    {
                        return idx;
                    }
                }
            }

            return 0;
        }

        public void UpdateUI()
        {
            // Only worry about updates, if the graph is visible
            // And make sure it is not disposed, since rendering happens on a timer
            if (!Visible || IsDisposed)
                return;

            // Clear existing data from the graph pane
            var graphPane = (MSGraphPane)graphControl.MasterPane[0];
            graphPane.CurveList.Clear();
            graphPane.GraphObjList.Clear();

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
                if (nodePepTree != null)
                {
                    var listInfoGroups = GetLibraryInfoChargeGroups(nodePepTree);
                    if (listInfoGroups.Length == 1)
                        nodeGroup = listInfoGroups[0];
                    else if (listInfoGroups.Length > 1)
                    {
                        AddGraphItem(graphPane, new NoDataMSGraphItem("Multiple charge states with library spectra"));
                        return;
                    }
                }
            }
            else
            {
                nodePepTree = nodeGroupTree.Parent as PeptideTreeNode;
            }
            ExplicitMods mods = (nodePepTree != null ? nodePepTree.DocNode.ExplicitMods : null);

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
                    // Try to load a list of spectra matching the criteria for
                    // the current node group.
                    IList<SpectrumInfo> spectra;
                    if (libraries.HasLibraries && libraries.IsLoaded &&
                        settings.TryLoadSpectra(group.Peptide.Sequence, group.PrecursorCharge, 
                                                    mods, out spectra))
                    {
                        if (NodeGroupChanged(nodeGroup))
                        {   
                            UpdateToolbar(spectra);
                            _nodeGroup = nodeGroup;
                            if (settings.TransitionSettings.Instrument.IsDynamicMin)
                                ZoomSpectrumToSettings();
                        }

                        int selectedIndex = GetSelectedSpectrumIndex(spectra);
                        SpectrumPeaksInfo spectrumInfo = spectra[selectedIndex].SpectrumPeaksInfo;
                        IsotopeLabelType typeInfo = spectra[selectedIndex].LabelType;
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

                        GraphItem = new SpectrumGraphItem(nodeGroup, transition, spectrumInfoR, spectra[selectedIndex].LibName)
                                            {
                                                ShowTypes = types,
                                                ShowCharges = charges,
                                                ShowRanks = Settings.Default.ShowRanks,
                                                ShowMz = Settings.Default.ShowIonMz,
                                                ShowObservedMz = Settings.Default.ShowObservedMz,
                                                ShowDuplicates = Settings.Default.ShowDuplicateIons,
                                                FontSize = Settings.Default.SpectrumFontSize,
                                                LineWidth = Settings.Default.SpectrumLineWidth
                                            };
                        graphControl.IsEnableVPan = graphControl.IsEnableVZoom =
                                                    !Settings.Default.LockYAxis;
                        AddGraphItem(graphPane, GraphItem);
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
            {
                UpdateToolbar(null);
                _nodeGroup = null;
                AddGraphItem(graphPane, new UnavailableMSGraphItem());
            }
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

// ReSharper disable SuggestBaseTypeForParameter
        private static TransitionGroupDocNode[] GetLibraryInfoChargeGroups(PeptideTreeNode nodeTree)
// ReSharper restore SuggestBaseTypeForParameter
        {
            // Return the first group of each charge stat that has library info.
            var listGroups = new List<TransitionGroupDocNode>();
            foreach (TransitionGroupDocNode nodeGroup in nodeTree.ChildDocNodes)
            {
                if (!nodeGroup.HasLibInfo)
                    continue;

                int precursorCharge = nodeGroup.TransitionGroup.PrecursorCharge;
                if (!listGroups.Contains(g => g.TransitionGroup.PrecursorCharge == precursorCharge))
                    listGroups.Add(nodeGroup);
            }
            return listGroups.ToArray();
        }

        private void graphControl_ContextMenuBuilder(ZedGraphControl sender,
            ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            _stateProvider.BuildSpectrumMenu(sender, menuStrip);
        }

        protected override void OnClosed(EventArgs e)
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

        private void comboSpectrum_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateUI();
        }
    }
}