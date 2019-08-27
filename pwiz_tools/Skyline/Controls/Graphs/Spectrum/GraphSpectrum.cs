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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NHibernate.Util;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.MSGraph;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Prosit;
using pwiz.Skyline.Model.Prosit.Communication;
using pwiz.Skyline.Model.Prosit.Models;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Crawdad;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs.Spectrum
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

    public partial class GraphSpectrum : DockableFormEx, IGraphContainer
    {
        public interface IStateProvider
        {
            TreeNodeMS SelectedNode { get; }
            IList<IonType> ShowIonTypes(bool isProteomic);

            // N.B. we're interested in the absolute value of charge here, so output list 
            // may be shorter than input list
            // CONSIDER(bspratt): we may want finer per-adduct control for small molecule use
            IList<int> ShowIonCharges(IEnumerable<Adduct> adductPriority);

            void BuildSpectrumMenu(bool isProteomic, ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip);
        }

        private class DefaultStateProvider : IStateProvider
        {
            public TreeNodeMS SelectedNode
            {
                get { return null; }
            }

            public IList<IonType> ShowIonTypes(bool isProteomic)
            {
                return isProteomic ? new[] { IonType.y } :  new[] { IonType.custom }; 
            }

            public IList<int> ShowIonCharges(IEnumerable<Adduct> adductPriority)
            {
                return Adduct.OrderedAbsoluteChargeValues(adductPriority).ToList();
            }

            public void BuildSpectrumMenu(bool isProteomic, ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip)
            {
            }
        }

        private readonly IDocumentUIContainer _documentContainer;
        private readonly IStateProvider _stateProvider;
        private TransitionGroupDocNode _nodeGroup;
        private IList<SpectrumDisplayInfo> _spectra;
        private bool _inToolbarUpdate;
        // TODO
        // private object _spectrumKeySave;
        private readonly GraphHelper _graphHelper;

        public GraphSpectrum(IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();

            Icon = Resources.SkylineData;
            _graphHelper = GraphHelper.Attach(graphControl);
            _documentContainer = documentUIContainer;
            _documentContainer.ListenUI(OnDocumentUIChanged);
            _stateProvider = documentUIContainer as IStateProvider ??
                             new DefaultStateProvider();

            if (DocumentUI != null)
                ZoomSpectrumToSettings();
        }

        private SrmDocument DocumentUI
        {
            get { return _documentContainer.DocumentUI; }
        }

        private MSGraphPane GraphPane
        {
            get { return (MSGraphPane) graphControl.MasterPane[0]; }
        }

        private SpectrumGraphItem GraphItem { get; set; }

        public bool HasSpectrum { get { return GraphItem != null; }}

        public string LibraryName
        {
            get { return GraphItem.LibraryName; }
        }

        public int PeaksCount
        {
            get { return GraphItem.PeaksCount; }
        }

        public int PeaksMatchedCount
        {
            get { return GraphItem.PeaksMatchedCount; }
        }

        public int PeaksRankedCount
        {
            get { return GraphItem.PeaksRankedCount; }
        }

        public IEnumerable<string> IonLabels
        {
            get { return GraphItem.IonLabels; }
        }

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

        [Browsable(true)]
        public event EventHandler<SelectedSpectrumEventArgs> SelectedSpectrumChanged;

        public void FireSelectedSpectrumChanged(bool isUserAction)
        {
            if (SelectedSpectrumChanged != null)
            {
                var spectrumInfo = SelectedSpectrum;
                if (spectrumInfo != null && !spectrumInfo.RetentionTime.HasValue)
                    spectrumInfo = null;
                SelectedSpectrumChanged(this, new SelectedSpectrumEventArgs(spectrumInfo, isUserAction));
            }
        }

        public void OnDocumentUIChanged(object sender, DocumentChangedEventArgs e)
        {
            // If document changed, update spectrum x scale to instrument
            // Or if library settings changed, show new ranks etc
            if (e.DocumentPrevious == null ||
                !ReferenceEquals(DocumentUI.Id, e.DocumentPrevious.Id) ||
                !ReferenceEquals(DocumentUI.Settings.TransitionSettings.Libraries, 
                                 e.DocumentPrevious.Settings.TransitionSettings.Libraries) ||
                !ReferenceEquals(DocumentUI.Settings.PeptideSettings.Libraries.Libraries,
                                 e.DocumentPrevious.Settings.PeptideSettings.Libraries.Libraries))
            {
                ZoomSpectrumToSettings();
                _spectra = null;
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
            GraphPane.LockYAxisAtZero = false;
            GraphPane.YAxis.MajorGrid.IsZeroLine = true; // This is set in the MSGraphPane ctor, but we want it here
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

        /// <summary>
        /// Small helper class to distinguish the 10-50 CEs from the
        /// one calculated CE and to provide custom formatting and equality
        /// </summary>
        private class CEComboItem
        {
            public CEComboItem(double ce, bool isCalculated = false)
            {
                CE = ce;
                IsCalculated = isCalculated;
            }

            public double CE { get; private set; }
            public bool IsCalculated { get; private set; }

            public override string ToString()
            {
                return CE.ToString(@"0.####", LocalizationHelper.CurrentUICulture);
            }

            protected bool Equals(CEComboItem other)
            {
                return CE.Equals(other.CE) && IsCalculated == other.IsCalculated;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((CEComboItem) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (CE.GetHashCode() * 397) ^ IsCalculated.GetHashCode();
                }
            }
        }

        private void UpdateToolbar()
        {
            if ((_spectra == null || _spectra.Count < 2) && !Settings.Default.Prosit)
            {
                toolBar.Visible = false;
            }
            else
            {
                var showMirror = !Settings.Default.Prosit && Settings.Default.LibMatchMirror;
                var showSpectraSelect = (_spectra != null && _spectra.Count >= 2) &&
                                        (!Settings.Default.Prosit || Settings.Default.LibMatchMirror);
                if (showSpectraSelect)
                {
                    comboMirrorSpectrum.Visible = showMirror;
                    mirrorLabel.Visible = showMirror;
                    comboSpectrum.Visible = true;
                    toolStripLabel1.Visible = true;
                }
                else
                {
                    comboSpectrum.Visible = false;
                    toolStripLabel1.Visible = false;
                    comboMirrorSpectrum.Visible = false;
                    mirrorLabel.Visible = false;
                }

                // We still need to do this, even if we hide those items,
                // since other code relies on the combo box selection being right

                if (_spectra != null)
                {
                    // Check to see if the list of files has changed.
                    var listNames = _spectra.Select(spectrum => spectrum.Identity).ToArray();
                    var listExisting = new List<string>();
                    foreach (var item in comboSpectrum.Items)
                        listExisting.Add(item.ToString());

                    if (!ArrayUtil.EqualsDeep(listNames, listExisting)) {
                        // If it has, update the list, trying to maintain selection, if possible.
                        object selected = comboSpectrum.SelectedItem;
                        // Unless the current selected index is the one matching the one currently
                        // in use by the precursor (zero), then try to stay viewing the in-use spectrum (zero)
                        int selectedIndex = comboSpectrum.SelectedIndex;
                        object selectedMirror = comboMirrorSpectrum.SelectedItem;

                        _inToolbarUpdate = true;
                        comboSpectrum.Items.Clear();
                        comboMirrorSpectrum.Items.Clear();
                        comboMirrorSpectrum.Items.Add(string.Empty); // No mirror
                        foreach (string name in listNames) {
                            comboSpectrum.Items.Add(name);
                            comboMirrorSpectrum.Items.Add(name);
                        }

                        if (selectedIndex == 0 || selected == null ||
                            comboSpectrum.Items.IndexOf(selected) == -1) {
                            comboSpectrum.SelectedIndex = 0;
                        }
                        else {
                            comboSpectrum.SelectedItem = selected;
                        }

                        if (selectedMirror != null && comboMirrorSpectrum.Items.IndexOf(selectedMirror) != -1) {
                            comboMirrorSpectrum.SelectedItem = selectedMirror;
                        }

                        _inToolbarUpdate = false;
                        ComboHelper.AutoSizeDropDown(comboSpectrum);
                        ComboHelper.AutoSizeDropDown(comboMirrorSpectrum);
                    }
                }

                var enableCE = false;
                // Update CE toolbar
                if (Settings.Default.Prosit)
                {
                    // TODO: remove redundacy between this and UpdateUI
                    // Get peptide and precursor
                    var nodeTree = _stateProvider.SelectedNode as SrmTreeNode;
                    var nodeGroupTree = nodeTree as TransitionGroupTreeNode;
                    var nodeTranTree = nodeTree as TransitionTreeNode;
                    if (nodeTranTree != null)
                        nodeGroupTree = nodeTranTree.Parent as TransitionGroupTreeNode;

                    var nodeGroup = nodeGroupTree?.DocNode;
                    PeptideDocNode peptideDocNode = null;
                    if (nodeGroup == null)
                    {
                        var nodePepTree = nodeTree as PeptideTreeNode;
                        if (nodePepTree != null)
                        {
                            peptideDocNode = nodePepTree.DocNode;
                            var listInfoGroups = nodePepTree.DocNode.TransitionGroups.ToArray();
                            if (listInfoGroups.Length == 1)
                                nodeGroup = listInfoGroups[0];
                        }
                    }
                    else
                    {
                        peptideDocNode = (nodeGroupTree.Parent as PeptideTreeNode)?.DocNode;
                    }

                    if (peptideDocNode != null && nodeGroup != null)
                    {
                        const int CE_MIN = 10;
                        const int CE_MAX = 50;

                        var ce = _documentContainer.DocumentUI.Settings.TransitionSettings.Prediction.CollisionEnergy
                            .GetCollisionEnergy(
                                nodeGroup.PrecursorAdduct,
                                _documentContainer.DocumentUI.Settings.GetRegressionMz(peptideDocNode, nodeGroup));

                        var ces = Enumerable.Range(CE_MIN, CE_MAX - CE_MIN + 1).Select(c => (object)new CEComboItem(c)).ToList();
                        var intCE = (int)ce;
                        var ceIndex = intCE - CE_MIN + 1;
                        ceIndex = Math.Max(ceIndex, 0);
                        ceIndex = Math.Min(ceIndex, 50);
                        ces.Insert(ceIndex, new CEComboItem(ce, true));
                        var oldSelection = comboCE.SelectedItem as CEComboItem;

                        // Not a great way of doing this, but we need to ensure that we don't get into
                        // an infinite recursion
                        if (!ArrayUtil.EqualsDeep(comboCE.Items.Cast<CEComboItem>().ToArray(), ces))
                        {
                            comboCE.Items.Clear();
                            comboCE.Items.AddRange(ces.ToArray());


                            // Only maintain the pre-defined selections
                            if (oldSelection != null && !oldSelection.IsCalculated) {
                                comboCE.SelectedIndex = (int)oldSelection.CE + (oldSelection.CE <= ce ? 0 : 1) - CE_MIN;
                            }
                            else {
                                // Select the calculated CE
                                comboCE.SelectedIndex = ceIndex;
                            }
                        }

                        enableCE = true;
                    }

                    ComboHelper.AutoSizeDropDown(comboCE);
                }

                comboCE.Visible = enableCE;
                ceLabel.Visible = enableCE;

                // Show only if we made any of the things visible
                toolBar.Visible = showSpectraSelect || enableCE;
            }
            FireSelectedSpectrumChanged(false);
        }

        public void SelectSpectrum(SpectrumIdentifier spectrumIdentifier)
        {
            if (_spectra != null && _spectra.Count > 1)
            {
                // Selection by file name and retention time should not select best spectrum
                int iSpectrum = _spectra.IndexOf(spectrumInfo => !spectrumInfo.IsBest &&
                                                                 SpectrumMatches(spectrumInfo, spectrumIdentifier));

                if (iSpectrum != -1)
                    comboSpectrum.SelectedIndex = iSpectrum;
            }
        }

        private bool SpectrumMatches(SpectrumDisplayInfo spectrumDisplayInfo, SpectrumIdentifier spectrumIdentifier)
        {
            if (!string.Equals(spectrumDisplayInfo.FilePath.ToString(), spectrumIdentifier.SourceFile.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (!spectrumDisplayInfo.RetentionTime.HasValue)
            {
                return false;
            }
            return Equals((float) spectrumDisplayInfo.RetentionTime, (float) spectrumIdentifier.RetentionTime);
        }

        public SpectrumDisplayInfo SelectedSpectrum
        {
            get
            {
                if (_spectra == null)
                    return null;

                if (toolBar.Visible)
                {
                    return comboSpectrum.SelectedIndex != -1
                               ? _spectra[comboSpectrum.SelectedIndex]
                               : null;
                }

                return _spectra[0];
            }
        }

        public SpectrumDisplayInfo SelectedMirrorSpectrum
        {
            get
            {
                if (_spectra == null)
                    return null;

                if (toolBar.Visible) {
                    return comboMirrorSpectrum.SelectedIndex > 0
                        ? _spectra[comboMirrorSpectrum.SelectedIndex - 1]
                        : null;
                }

                return null;
            }
        }

        public bool MirrorPlot
        {
            get { return Settings.Default.LibMatchMirror && _spectra != null; }
        }

        public IEnumerable<SpectrumDisplayInfo> AvailableSpectra
        {
            get { return _spectra; }
        }

        public void UpdateUI(bool selectionChanged = true)
        {
            // Only worry about updates, if the graph is visible
            // And make sure it is not disposed, since rendering happens on a timer
            if (!Visible || IsDisposed)
                return;

            // Clear existing data from the graph pane
            var graphPane = (MSGraphPane) graphControl.MasterPane[0];
            graphPane.CurveList.Clear();
            graphPane.GraphObjList.Clear();
            GraphItem = null;

            GraphHelper.FormatGraphPane(graphControl.GraphPane);
            GraphHelper.FormatFontSize(graphControl.GraphPane,Settings.Default.SpectrumFontSize);
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
                    var listInfoGroups = Settings.Default.Prosit
                        ? nodePepTree.DocNode.TransitionGroups.ToArray()
                        : GetLibraryInfoChargeGroups(nodePepTree);
                    if (listInfoGroups.Length == 1)
                        nodeGroup = listInfoGroups[0];
                    else if (listInfoGroups.Length > 1)
                    {
                        _nodeGroup = null;
                        toolBar.Visible = false;
                        _graphHelper.SetErrorGraphItem(new NoDataMSGraphItem(
                                         Resources.GraphSpectrum_UpdateUI_Multiple_charge_states_with_library_spectra));
                        return;
                    }
                }
            }
            else
            {
                nodePepTree = nodeGroupTree.Parent as PeptideTreeNode;
            }

            // Check for appropriate spectrum to load
            SrmSettings settings = DocumentUI.Settings;
            PeptideLibraries libraries = settings.PeptideSettings.Libraries;
            bool available = false;
            if (nodeGroup == null || (!nodeGroup.HasLibInfo && !libraries.HasMidasLibrary && !Settings.Default.Prosit))
            {
                _spectra = null;
            }
            else
            {
                TransitionGroup group = nodeGroup.TransitionGroup;
                TransitionDocNode transition = (nodeTranTree == null ? null : nodeTranTree.DocNode);
                var lookupSequence = group.Peptide.Target; // Sequence or custom ion id
                ExplicitMods lookupMods = null;
                if (nodePepTree != null)
                {
                    lookupSequence = nodePepTree.DocNode.SourceUnmodifiedTarget;
                    lookupMods = nodePepTree.DocNode.SourceExplicitMods;
                }

                try
                {
                    // Try to load a list of spectra matching the criteria for
                    // the current node group.
                    //if (libraries.HasLibraries && libraries.IsLoaded)
                    {
                        var nodeGroupChanged = NodeGroupChanged(nodeGroup);
                        try
                        {
                            if (nodeGroupChanged)
                                UpdateSpectra(nodeGroup, lookupSequence, lookupMods);
                            UpdateToolbar();
                        }
                        catch (Exception)
                        {
                            _spectra = null;
                            UpdateToolbar();
                            throw;
                        }

                        if (nodeGroupChanged)
                        {
                            _nodeGroup = nodeGroup;
                            if (settings.TransitionSettings.Instrument.IsDynamicMin)
                                ZoomSpectrumToSettings();
                        }

                        SpectrumDisplayInfo spectrum = null;
                        SpectrumDisplayInfo prositSpectrum = null;
                        if (Settings.Default.Prosit)
                        {
                            var prositClient = PrositPredictionClient.Current;
                            var selectedCEItem = comboCE.SelectedItem as CEComboItem;
                            double? ce = null;
                            if (selectedCEItem != null && !selectedCEItem.IsCalculated) // Just recalculate...
                                ce = selectedCEItem.CE;
                            var massSpectrum = PrositIntensityModel.Instance.PredictSingle(prositClient, DocumentUI.Settings,
                                new PeptidePrecursorPair(nodePepTree.DocNode, nodeGroup, ce));
                            var iRT = PrositRetentionTimeModel.Instance.PredictSingle(prositClient, DocumentUI.Settings,
                                nodePepTree.DocNode);
                            prositSpectrum = new SpectrumDisplayInfo(new SpectrumInfoProsit(massSpectrum, nodeGroup), iRT[nodePepTree.DocNode]);

                            if (!MirrorPlot)
                                spectrum = prositSpectrum;
                        }

                        if (!Settings.Default.Prosit || MirrorPlot)
                            spectrum = SelectedSpectrum;

                        if (spectrum != null)
                        {
                            IsotopeLabelType typeInfo = spectrum.LabelType;
                            var types = _stateProvider.ShowIonTypes(group.IsProteomic);
                            var adducts =
                                (group.IsProteomic
                                    ? Transition.DEFAULT_PEPTIDE_LIBRARY_CHARGES
                                    : nodeGroup.InUseAdducts).ToArray();
                            var charges = _stateProvider.ShowIonCharges(adducts);
                            var rankTypes = group.IsProteomic
                                ? settings.TransitionSettings.Filter.PeptideIonTypes
                                : settings.TransitionSettings.Filter.SmallMoleculeIonTypes;
                            var rankAdducts = group.IsProteomic
                                ? settings.TransitionSettings.Filter.PeptideProductCharges
                                : settings.TransitionSettings.Filter.SmallMoleculeFragmentAdducts;
                            var rankCharges = Adduct.OrderedAbsoluteChargeValues(rankAdducts);
                            // Make sure the types and charges in the settings are at the head
                            // of these lists to give them top priority, and get rankings correct.
                            int i = 0;
                            foreach (IonType type in rankTypes)
                            {
                                if (types.Remove(type))
                                    types.Insert(i++, type);
                            }

                            i = 0;
                            var showAdducts = new List<Adduct>();
                            foreach (var charge in rankCharges)
                            {
                                if (charges.Remove(charge))
                                    charges.Insert(i++, charge);
                                // NB for all adducts we just look at abs value of charge
                                // CONSIDER(bspratt): we may want finer per-adduct control for small molecule use
                                showAdducts.AddRange(adducts.Where(a => charge == Math.Abs(a.AdductCharge)));
                            }

                            showAdducts.AddRange(adducts.Where(a =>
                                charges.Contains(Math.Abs(a.AdductCharge)) && !showAdducts.Contains(a)));

                            var spectrumInfoR = new LibraryRankedSpectrumInfo(spectrum.SpectrumPeaksInfo,
                                typeInfo,
                                nodeGroup,
                                settings,
                                lookupSequence,
                                lookupMods,
                                showAdducts,
                                types,
                                rankAdducts,
                                rankTypes);
                            GraphItem = new SpectrumGraphItem(nodeGroup, transition, spectrumInfoR, spectrum.Name)
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
                            LibraryChromGroup chromatogramData = null;
                            if (Settings.Default.ShowLibraryChromatograms)
                            {
                                chromatogramData = spectrum.LoadChromatogramData();
                            }

                            if (null == chromatogramData)
                            {
                                _graphHelper.ResetForSpectrum(new[] {nodeGroup.TransitionGroup});

                                var mirrorSpectrum = SelectedMirrorSpectrum;
                                if (MirrorPlot)
                                {
                                    if (Settings.Default.Prosit)
                                        mirrorSpectrum = prositSpectrum;
                                }
                                else
                                {
                                    // TODO: eventually, if the combobox gets cleared up we can remove this
                                    mirrorSpectrum = null;
                                }

                                var hasMirror = mirrorSpectrum != null;

                                _graphHelper.AddSpectrum(GraphItem, !hasMirror);

                                if (hasMirror)
                                {
                                    var type = mirrorSpectrum.LabelType;
                                    // Rescale so that mirror max is same as main max
                                    var spectrumPeaksInfo = mirrorSpectrum.SpectrumPeaksInfo;
                                    var maxIntensity = spectrumPeaksInfo.Intensities.Max();
                                    var mainMax = spectrum.SpectrumPeaksInfo.Intensities.Max();
                                    spectrumPeaksInfo = new SpectrumPeaksInfo(spectrumPeaksInfo.Peaks.Select(mi =>
                                            new SpectrumPeaksInfo.MI
                                            {
                                                Mz = mi.Mz, Intensity = (float) (mi.Intensity / maxIntensity * mainMax)
                                            })
                                        .ToArray());
                                    var mirrorSpectrumInfoR = new LibraryRankedSpectrumInfo(spectrumPeaksInfo,
                                        type,
                                        nodeGroup,
                                        settings,
                                        lookupSequence,
                                        lookupMods,
                                        showAdducts,
                                        types,
                                        rankAdducts,
                                        rankTypes);

                                    var mirrorGraphItem = new SpectrumGraphItem(nodeGroup, transition, mirrorSpectrumInfoR, mirrorSpectrum.Name)
                                    {
                                        ShowTypes = types,
                                        ShowCharges = charges,
                                        ShowRanks = Settings.Default.ShowRanks,
                                        ShowMz = Settings.Default.ShowIonMz,
                                        ShowObservedMz = Settings.Default.ShowObservedMz,
                                        ShowDuplicates = Settings.Default.ShowDuplicateIons,
                                        FontSize = Settings.Default.SpectrumFontSize,
                                        LineWidth = Settings.Default.SpectrumLineWidth,
                                        Invert = true
                                    };

                                    _graphHelper.AddSpectrum(mirrorGraphItem, false);
                                    
                                    // Calculate dot product
                                    var matched1 = spectrumInfoR.PeaksMatched.ToArray();
                                    var matched2 = mirrorSpectrumInfoR.PeaksMatched.ToArray();
                                    var intensities1 = new List<double>(matched1.Length); // Either capacity will be enough..
                                    var intensities2 = new List<double>(matched2.Length);
                                    var intensities1All = new List<double>(matched1.Length + matched2.Length);
                                    var intensities2All = new List<double>(matched1.Length + matched2.Length);
                                    var matchIndex1 = 0;
                                    var matchIndex2 = 0;
                                    while (matchIndex1 < matched1.Length && matchIndex2 < matched2.Length)
                                    {
                                        var mz1 = matched1[matchIndex1].ObservedMz;
                                        var mz2 = matched2[matchIndex2].ObservedMz;
                                        if (Math.Abs(mz1 - mz2) < settings.TransitionSettings.Libraries.IonMatchTolerance)
                                        {
                                            intensities1.Add(matched1[matchIndex1].Intensity);
                                            intensities2.Add(matched2[matchIndex2].Intensity);
                                            intensities1All.Add(matched1[matchIndex1].Intensity);
                                            intensities2All.Add(matched2[matchIndex2].Intensity);

                                            ++matchIndex1;
                                            ++matchIndex2;
                                        }
                                        else if (mz1 < mz2)
                                        {
                                            intensities1All.Add(matched1[matchIndex1].Intensity);
                                            intensities2All.Add(0.0);
                                            ++matchIndex1;
                                        }
                                        else
                                        {
                                            intensities1All.Add(0.0);
                                            intensities2All.Add(matched2[matchIndex2].Intensity);
                                            ++matchIndex2;
                                        }
                                    }

                                    var dotp = new Statistics(intensities1).NormalizedContrastAngleSqrt(
                                        new Statistics(intensities2));
                                    var dotp0 = new Statistics(intensities1All).NormalizedContrastAngleSqrt(
                                        new Statistics(intensities2All));

                                    GraphPane.Title.Text = string.Format("{0} vs {1}\r\n{2}\r\ndotp: {3}",
                                        GraphItem.LibraryName,
                                        mirrorGraphItem.LibraryName, GraphItem.Title, dotp0.ToString("0.0000",
                                            LocalizationHelper.CurrentUICulture));

                                    _graphHelper.GraphControl.Refresh();
                                }


                                _graphHelper.ZoomSpectrumToSettings(DocumentUI, nodeGroup);
                            }
                            else
                            {
                                _graphHelper.ResetForChromatograms(new[] {nodeGroup.TransitionGroup});

                                var displayType = GraphChromatogram.GetDisplayType(DocumentUI, nodeGroup);
                                IList<TransitionDocNode> displayTransitions =
                                    GraphChromatogram.GetDisplayTransitions(nodeGroup, displayType).ToArray();
                                int numTrans = displayTransitions.Count;
                                var allChromDatas =
                                    chromatogramData.ChromDatas.Where(
                                        chromData => DisplayTypeMatches(chromData, displayType)).ToList();
                                var chromDatas = new List<LibraryChromGroup.ChromData>();
                                for (int iTran = 0; iTran < numTrans; iTran++)
                                {
                                    var displayTransition = displayTransitions[iTran];
                                    var indexMatch =
                                        allChromDatas.IndexOf(chromData =>
                                            IonMatches(displayTransition.Transition, chromData));
                                    if (indexMatch >= 0)
                                    {
                                        chromDatas.Add(allChromDatas[indexMatch]);
                                        allChromDatas.RemoveAt(indexMatch);
                                    }
                                    else
                                    {
                                        chromDatas.Add(null);
                                    }
                                }

                                allChromDatas.Sort((chromData1, chromData2) => chromData1.Mz.CompareTo(chromData2.Mz));
                                chromDatas.AddRange(allChromDatas);
                                double maxHeight = chromDatas.Max(chromData =>
                                    null == chromData ? double.MinValue : chromData.Height);
                                int iChromDataPrimary = chromDatas.IndexOf(chromData =>
                                    null != chromData && maxHeight == chromData.Height);
                                int colorOffset = displayType == DisplayTypeChrom.products
                                    ? GraphChromatogram.GetDisplayTransitions(nodeGroup,
                                        DisplayTypeChrom.precursors).Count()
                                    : 0;
                                for (int iChromData = 0; iChromData < chromDatas.Count; iChromData++)
                                {
                                    var chromData = chromDatas[iChromData];
                                    if (chromData == null)
                                    {
                                        continue;
                                    }

                                    string label;
                                    var pointAnnotation = GraphItem.AnnotatePoint(new PointPair(chromData.Mz, 1.0));
                                    if (null != pointAnnotation)
                                    {
                                        label = pointAnnotation.Label;
                                    }
                                    else
                                    {
                                        label = chromData.Mz.ToString(@"0.####");
                                    }

                                    TransitionDocNode matchingTransition;
                                    Color color;
                                    if (iChromData < numTrans)
                                    {
                                        matchingTransition = displayTransitions[iChromData];
                                        color =
                                            GraphChromatogram.COLORS_LIBRARY[
                                                (iChromData + colorOffset) % GraphChromatogram.COLORS_LIBRARY.Count];
                                    }
                                    else
                                    {
                                        matchingTransition = null;
                                        color =
                                            GraphChromatogram.COLORS_GROUPS[
                                                iChromData % GraphChromatogram.COLORS_GROUPS.Count];
                                    }

                                    TransitionChromInfo tranPeakInfo;
                                    ChromatogramInfo chromatogramInfo;
                                    MakeChromatogramInfo(nodeGroup.PrecursorMz, chromatogramData, chromData,
                                        out chromatogramInfo, out tranPeakInfo);
                                    var graphItem = new ChromGraphItem(nodeGroup, matchingTransition, chromatogramInfo,
                                        iChromData == iChromDataPrimary ? tranPeakInfo : null, null,
                                        new[] {iChromData == iChromDataPrimary}, null, 0, false, false, null, 0,
                                        color, Settings.Default.ChromatogramFontSize, 1);
                                    LineItem curve =
                                        (LineItem) _graphHelper.AddChromatogram(PaneKey.DEFAULT, graphItem);
                                    if (matchingTransition == null)
                                    {
                                        curve.Label.Text = label;
                                    }

                                    curve.Line.Width = Settings.Default.ChromatogramLineWidth;
                                    if (null != transition)
                                    {
                                        if (IonMatches(transition.Transition, chromData))
                                        {
                                            color = ChromGraphItem.ColorSelected;
                                        }
                                    }

                                    curve.Color = color;
                                }

                                graphPane.Title.IsVisible = false;
                                graphPane.Legend.IsVisible = true;
                                _graphHelper.FinishedAddingChromatograms(chromatogramData.StartTime,
                                    chromatogramData.EndTime, false);
                                graphControl.Refresh();
                            }

                            graphControl.IsEnableVPan = graphControl.IsEnableVZoom =
                                !Settings.Default.LockYAxis;
                            available = true;
                        }
                    }
                }
                catch (PrositException ex)
                {
                    _graphHelper.SetErrorGraphItem(new NoDataMSGraphItem(ex.Message));
                    return;
                }
                catch (Exception)
                {
                    _graphHelper.SetErrorGraphItem(new NoDataMSGraphItem(
                                     Resources.GraphSpectrum_UpdateUI_Failure_loading_spectrum__Library_may_be_corrupted));
                    return;
                }
            }
            // Show unavailable message, if no spectrum loaded
            if (!available)
            {
                UpdateToolbar();
                _nodeGroup = null;
                _graphHelper.SetErrorGraphItem(new UnavailableMSGraphItem());
            }
        }

        private static bool DisplayTypeMatches(LibraryChromGroup.ChromData chromData, DisplayTypeChrom displayType)
        {
            switch (displayType)
            {
                case DisplayTypeChrom.products:
                    return (chromData.IonType != IonType.precursor);
                case DisplayTypeChrom.precursors:
                    return (chromData.IonType == IonType.precursor);
                default:
                    return true;
            }  
        }

        private static bool IonMatches(Transition transition, LibraryChromGroup.ChromData chromData)
        {
            if(transition.IonType.Equals(chromData.IonType) && transition.Charge == chromData.Charge)
            {
                if(transition.IsPrecursor())
                {
                    return transition.MassIndex == chromData.MassIndex;
                }
                else
                {
                    return transition.Ordinal == chromData.Ordinal;
                }
            }
            return false;
        }

        private static int FindNearestIndex(IList<float> times, float time)
        {
            int index = CollectionUtil.BinarySearch(times, time);
            if (index >= 0)
            {
                return index;
            }
            index = ~index;
            if (index >= times.Count)
            {
                return times.Count - 1;
            }
            if (index > 0 && time - times[index - 1] < times[index] - time)
            {
                return index - 1;
            }
            return index;
        }

        private void UpdateSpectra(TransitionGroupDocNode nodeGroup, Target lookupSequence, ExplicitMods lookupMods)
        {
            _spectra = GetSpectra(nodeGroup, lookupSequence, lookupMods);
            if (!_spectra.Any())
                _spectra = null;
        }

        private IList<SpectrumDisplayInfo> GetSpectra(TransitionGroupDocNode nodeGroup, Target lookupSequence, ExplicitMods lookupMods)
        {
            var settings = DocumentUI.Settings;
            var charge = nodeGroup.PrecursorAdduct;
            var spectra = settings.GetBestSpectra(lookupSequence, charge, lookupMods).Select(s => new SpectrumDisplayInfo(s)).ToList();
            // Showing redundant spectra is only supported for full-scan filtering when
            // the document has results files imported.
            if ((!settings.TransitionSettings.FullScan.IsEnabled && !settings.PeptideSettings.Libraries.HasMidasLibrary) || !settings.HasResults)
                return spectra;

            try
            {
                var spectraRedundant = new List<SpectrumDisplayInfo>();
                var dictReplicateNameFiles = new Dictionary<string, HashSet<string>>();
                foreach (var spectrumInfo in settings.GetRedundantSpectra(nodeGroup.Peptide, lookupSequence, charge, nodeGroup.TransitionGroup.LabelType, lookupMods))
                {
                    var matchingFile = settings.MeasuredResults.FindMatchingMSDataFile(MsDataFileUri.Parse(spectrumInfo.FilePath));
                    if (matchingFile == null)
                        continue;

                    string replicateName = matchingFile.Chromatograms.Name;
                    spectraRedundant.Add(new SpectrumDisplayInfo(spectrumInfo,
                                                                 replicateName,
                                                                 matchingFile.FilePath,
                                                                 matchingFile.FileOrder,
                                                                 spectrumInfo.RetentionTime,
                                                                 false));

                    // Include the best spectrum twice, once displayed in the normal
                    // way and once displayed with its replicate and retetion time.
                    if (spectrumInfo.IsBest)
                    {
                        string libName = spectrumInfo.Name;
                        var labelType = spectrumInfo.LabelType;
                        int iBest = spectra.IndexOf(s => Equals(s.Name, libName) &&
                                                         Equals(s.LabelType, labelType));
                        if (iBest != -1)
                        {
                            spectra[iBest] = new SpectrumDisplayInfo(spectra[iBest].SpectrumInfo,
                                                                     replicateName,
                                                                     matchingFile.FilePath,
                                                                     0,
                                                                     spectrumInfo.RetentionTime,
                                                                     true);
                        }
                    }

                    HashSet<string> setFiles;
                    if (!dictReplicateNameFiles.TryGetValue(replicateName, out setFiles))
                    {
                        setFiles = new HashSet<string>();
                        dictReplicateNameFiles.Add(replicateName, setFiles);
                    }
                    setFiles.Add(spectrumInfo.FilePath);
                }

                // Determine if replicate name is sufficient to uniquely identify the file
                foreach (var spectrumInfo in spectraRedundant)
                {
                    string replicateName = spectrumInfo.ReplicateName;
                    if (replicateName != null && dictReplicateNameFiles[replicateName].Count < 2)
                        spectrumInfo.IsReplicateUnique = true;
                }

                spectraRedundant.Sort();
                spectra.AddRange(spectraRedundant);
                return spectra;
            }
            catch (Exception)
            {
                return spectra;
            }
        }

        public void LockYAxis(bool lockY)
        {
            graphControl.IsEnableVPan = graphControl.IsEnableVZoom = !lockY;
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

                var precursorCharge = nodeGroup.TransitionGroup.PrecursorAdduct;
                if (!listGroups.Contains(g => g.TransitionGroup.PrecursorAdduct == precursorCharge))
                    listGroups.Add(nodeGroup);
            }
            return listGroups.ToArray();
        }

        private void graphControl_ContextMenuBuilder(ZedGraphControl sender,
            ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            var isProteomic = _nodeGroup == null || !_nodeGroup.IsCustomIon;
            _stateProvider.BuildSpectrumMenu(isProteomic, sender, menuStrip);
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

            if (!_inToolbarUpdate)
            {
                FireSelectedSpectrumChanged(true);
            }
        }

        private void comboMirrorSpectrum_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateUI();
        }

        private void comboCE_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateUI();
        }

        public static void MakeChromatogramInfo(SignedMz precursorMz, LibraryChromGroup chromGroup, LibraryChromGroup.ChromData chromData, out ChromatogramInfo chromatogramInfo, out TransitionChromInfo transitionChromInfo)
        {
            var timeIntensities = new TimeIntensities(chromGroup.Times, chromData.Intensities, null, null);
            var crawPeakFinder = Crawdads.NewCrawdadPeakFinder();
            crawPeakFinder.SetChromatogram(chromGroup.Times, chromData.Intensities);
            var crawdadPeak =
                crawPeakFinder.GetPeak(
                    FindNearestIndex(chromGroup.Times, (float) chromGroup.StartTime),
                    FindNearestIndex(chromGroup.Times, (float) chromGroup.EndTime));
            var chromPeak = new ChromPeak(crawPeakFinder, crawdadPeak, 0, timeIntensities, null);
            transitionChromInfo = new TransitionChromInfo(null, 0, chromPeak,
                IonMobilityFilter.EMPTY, // CONSIDER(bspratt) IMS in chromatogram libraries?
                new float?[0], Annotations.EMPTY,
                                                            UserSet.FALSE);
            var peaks = new[] {chromPeak};
            var header = new ChromGroupHeaderInfo(precursorMz,
                0,  // file index
                1, // numTransitions
                0, // startTransitionIndex
                peaks.Length, // numPeaks
                0, // startPeakIndex
                0, // startscoreindex
                0,// maxPeakIndex
                chromGroup.Times.Length, // numPoints
                0, // compressedSize
                0, // uncompressedsize
                0,  //location
                0, -1, -1, null, null, null, eIonMobilityUnits.none); // CONSIDER(bspratt) IMS in chromatogram libraries?
            var driftTimeFilter = IonMobilityFilter.EMPTY; // CONSIDER(bspratt) IMS in chromatogram libraries?
            var groupInfo = new ChromatogramGroupInfo(header,
                    new Dictionary<Type, int>(),
                    new ChromCachedFile[0],
                    new[] { new ChromTransition(chromData.Mz, 0, (float)(driftTimeFilter.IonMobility.Mobility??0), (float)(driftTimeFilter.IonMobilityExtractionWindowWidth??0), ChromSource.unknown), },
                    peaks,
                    null) { TimeIntensitiesGroup = TimeIntensitiesGroup.Singleton(timeIntensities) };

            chromatogramInfo = new ChromatogramInfo(groupInfo, 0);
        }
    }

    public class GraphSpectrumSettings
    {
        private readonly Action<bool> _update;

        public GraphSpectrumSettings(Action<bool> update)
        {
            _update = update;
        }

        private void ActAndUpdate(Action act)
        {
            act();
            _update(true);
        }

        private static Settings Set
        {
            get { return Settings.Default; }
        }

        public bool ShowAIons
        {
            get { return Set.ShowAIons; }
            set { ActAndUpdate(() => Set.ShowAIons = value); }
        }

        public bool ShowBIons
        {
            get { return Set.ShowBIons; }
            set { ActAndUpdate(() => Set.ShowBIons = value); }
        }

        public bool ShowCIons
        {
            get { return Set.ShowCIons; }
            set { ActAndUpdate(() => Set.ShowCIons = value); }
        }

        public bool ShowXIons
        {
            get { return Set.ShowXIons; }
            set { ActAndUpdate(() => Set.ShowXIons = value); }
        }

        public bool ShowYIons
        {
            get { return Set.ShowYIons; }
            set { ActAndUpdate(() => Set.ShowYIons = value); }
        }

        public bool ShowZIons
        {
            get { return Set.ShowZIons; }
            set { ActAndUpdate(() => Set.ShowZIons = value); }
        }

        public bool ShowFragmentIons
        {
            get { return Set.ShowFragmentIons; }
            set { ActAndUpdate(() => Set.ShowFragmentIons = value); }
        }

        public bool ShowPrecursorIon
        {
            get { return Set.ShowPrecursorIon; }
            set { ActAndUpdate(() => Set.ShowPrecursorIon = value); }
        }

        public bool ShowCharge1
        {
            get { return Set.ShowCharge1; }
            set { ActAndUpdate(() => Set.ShowCharge1 = value); }
        }

        public bool ShowCharge2
        {
            get { return Set.ShowCharge2; }
            set { ActAndUpdate(() => Set.ShowCharge2 = value); }
        }

        public bool ShowCharge3
        {
            get { return Set.ShowCharge3; }
            set { ActAndUpdate(() => Set.ShowCharge3 = value); }
        }

        public bool ShowCharge4
        {
            get { return Set.ShowCharge4; }
            set { ActAndUpdate(() => Set.ShowCharge4 = value); }
        }

        public bool Prosit
        {
            get { return Set.Prosit; }
            set { ActAndUpdate(() => Set.Prosit = value);}
        }

        public bool Mirror
        {
            get { return Set.LibMatchMirror; }
            set { ActAndUpdate(() => Set.LibMatchMirror = value); }
        }

        public IList<IonType> ShowIonTypes(bool isProteomic)
        {
            var types = new List<IonType>();
            if (isProteomic)
            {
                // Priority ordered
                AddItem(types, IonType.y, Set.ShowYIons);
                AddItem(types, IonType.b, Set.ShowBIons);
                AddItem(types, IonType.z, Set.ShowZIons);
                AddItem(types, IonType.c, Set.ShowCIons);
                AddItem(types, IonType.x, Set.ShowXIons);
                AddItem(types, IonType.a, Set.ShowAIons);
                // FUTURE: Add custom ions when LibraryRankedSpectrumInfo can support them
                AddItem(types, IonType.precursor, Set.ShowPrecursorIon);
            }
            else
            {
                AddItem(types, IonType.custom, Set.ShowFragmentIons); // CONSIDER(bspratt) eventually, user-defined fragment types?
                AddItem(types, IonType.precursor, Set.ShowPrecursorIon);
            }
            return types;
        }

        // NB for all adducts we just look at abs value of charge
        // CONSIDER(bspratt): we may want finer per-adduct control for small molecule use
        public IList<int> ShowIonCharges(IEnumerable<Adduct> adductPriority)
        {
            var chargePriority = Adduct.OrderedAbsoluteChargeValues(adductPriority);
            var charges = new List<int>();
            int i = 0;
            foreach (var charge in chargePriority)
            {
                // Priority ordered
                if (i == 0)
                    AddItem(charges, charge, ShowCharge1);
                else if (i == 1)
                    AddItem(charges, charge, ShowCharge2);
                else if (i == 2)
                    AddItem(charges, charge, ShowCharge3);
                else if (i == 3)
                    AddItem(charges, charge, ShowCharge4);
                else
                    break;
                i++;
            }
            return charges;
        }

        private static void AddItem<TItem>(ICollection<TItem> items, TItem item, bool add)
        {
            if (add)
                items.Add(item);
        }
    }

    public sealed class SpectrumDisplayInfo : IComparable<SpectrumDisplayInfo>
    {
        private readonly SpectrumInfo _spectrumInfo;

        public SpectrumDisplayInfo(SpectrumInfo spectrumInfo, double? retentionTime = null)
        {
            _spectrumInfo = spectrumInfo;
            IsBest = true;
            RetentionTime = retentionTime;
        }

        public SpectrumDisplayInfo(SpectrumInfo spectrumInfo, string replicateName,
            MsDataFileUri filePath, int fileOrder, double? retentionTime, bool isBest)
        {
            _spectrumInfo = spectrumInfo;

            ReplicateName = replicateName;
            FilePath = filePath;
            FileOrder = fileOrder;
            RetentionTime = retentionTime;
            IsBest = isBest;
        }

        public SpectrumInfo SpectrumInfo { get { return _spectrumInfo; } }
        public string Name { get { return _spectrumInfo.Name; } }
        public IsotopeLabelType LabelType { get { return _spectrumInfo.LabelType; } }
        public string ReplicateName { get; private set; }
        public bool IsReplicateUnique { get; set; }
        public MsDataFileUri FilePath { get; private set; }
        public string FileName { get { return FilePath.GetFileName(); } }
        public int FileOrder { get; private set; }
        public double? RetentionTime { get; private set; }
        public bool IsBest { get; private set; }

        public string Identity { get { return ToString(); } }

        public SpectrumPeaksInfo SpectrumPeaksInfo { get { return _spectrumInfo.SpectrumPeaksInfo; } }
        public LibraryChromGroup LoadChromatogramData() { return _spectrumInfo.ChromatogramData; }

        public int CompareTo(SpectrumDisplayInfo other)
        {
            int i = Comparer.Default.Compare(FileOrder, other.FileOrder);
            if (i == 0)
            {
                if (RetentionTime.HasValue && other.RetentionTime.HasValue)
                    i = Comparer.Default.Compare(RetentionTime.Value, other.RetentionTime);
                // No retention time is less than having a retention time
                else if (RetentionTime.HasValue)
                    i = 1;
                else if (other.RetentionTime.HasValue)
                    i = -1;
                else
                    i = 0;
            }

            return i;
        }

        public override string ToString()
        {
            if (IsBest)
                return ReferenceEquals(LabelType, IsotopeLabelType.light) ? Name : String.Format(@"{0} ({1})", Name, LabelType);
            if (IsReplicateUnique)
                return string.Format(@"{0} ({1:F02} min)", ReplicateName, RetentionTime);
            return string.Format(@"{0} - {1} ({2:F02} min)", ReplicateName, FileName, RetentionTime);
        }
    }

    public sealed class SpectrumIdentifier
    {
        public SpectrumIdentifier(string sourceFile, double retentionTime)
            : this(MsDataFileUri.Parse(sourceFile), retentionTime)
        {
            
        }
        public SpectrumIdentifier(MsDataFileUri sourceFile, double retentionTime)
        {
            SourceFile = sourceFile;
            RetentionTime = retentionTime;
        }

        public MsDataFileUri SourceFile { get; private set; }
        public double RetentionTime { get; private set; }
    }

    public sealed class SelectedSpectrumEventArgs : EventArgs
    {
        public SelectedSpectrumEventArgs(SpectrumDisplayInfo spectrum, bool isUserAction)
        {
            Spectrum = spectrum;
            IsUserAction = isUserAction;
        }

        public SpectrumDisplayInfo Spectrum { get; private set; }
        public bool IsUserAction { get; private set; }
    }
}
