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
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.MSGraph;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Prosit;
using pwiz.Skyline.Model.Prosit.Models;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Crawdad;
using pwiz.Skyline.Model.Themes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;
using Timer = System.Windows.Forms.Timer;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Interface for any window that contains a graph, to allow non-blocking
    /// updates with a <see cref="System.Windows.Forms.Timer"/>.
    /// </summary>
    public interface IGraphContainer : IUpdatable
    {
        /// <summary>
        /// Locks/unlocks the Y-axis, so that it auto-scales.
        /// </summary>
        /// <param name="lockY">True to use Y-axis auto-scaling</param>
        void LockYAxis(bool lockY);
    }

    public enum SpectrumControlType { LibraryMatch, FullScanViewer }

    public interface IMzScalePlot
    {
        void SetMzScale(MzRange range);
        MzRange Range { get; }
        void ApplyMZZoomState(ZoomState scaleState);
        event EventHandler<ZoomEventArgs> ZoomEvent;
        SpectrumControlType ControlType { get; }
        bool IsAnnotated { get; }
    }

    public interface ISpectrumScaleProvider
    {
        MzRange GetMzRange(SpectrumControlType controlType);
    }
    
    public partial class GraphSpectrum : DockableFormEx, IGraphContainer, IMzScalePlot
    {

        private static readonly double YMAX_SCALE = 1.25;

        public interface IStateProvider : ISpectrumScaleProvider
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

            public MzRange GetMzRange(SpectrumControlType controlType)
            {
                return new MzRange();
            }
        }

        private readonly IDocumentUIContainer _documentContainer;
        private readonly IStateProvider _stateProvider;
        private readonly UpdateManager _updateManager;
        private TransitionGroupDocNode _nodeGroup;

        private ImmutableList<Precursor> Precursors => _updateManager.Precursors;
        private int PrecursorCount => Precursors?.Count ?? 0;
        private int SpectraCount => Precursors?.Sum(s => s.Spectra.Count) ?? 0;

        private SpectrumDisplayInfo _mirrorSpectrum;
        private SpectrumDisplayInfo _spectrum;

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
            _updateManager = new UpdateManager(this);

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

        public Exception GraphException
        {
            get
            {
                if (GraphItem != null)
                    return null;

                if (GraphPane.CurveList.Count != 1)
                    return null;

                var curveItem = GraphPane.CurveList[0];
                var item = curveItem.Tag as ExceptionMSGraphItem;
                return item?.Exception;
            }
        }

        public bool HasSpectrum { get { return GraphItem != null; }}

        /// <summary>
        /// Normalized collisition energy for Prosit
        /// </summary>
        public int PrositNCE
        {
            get { return (int) comboCE.SelectedItem; }
            set { comboCE.SelectedItem = value; }
        }

        public bool IsToolbarVisible
        {
            get { return toolBar.Visible; }
        }

        public bool PrecursorComboVisible => comboPrecursor.Visible;

        public bool NCEVisible
        {
            get { return comboCE.Visible; }
        }

        public bool MirrorComboVisible
        {
            get { return comboMirrorSpectrum.Visible; }
        }

        public string GraphTitle
        {
            get
            {
                if (HasSpectrum)
                    return GraphItem.Title;
                return GraphPane.CurveList[0].Label.Text;
            }
        }

        public bool IsGraphUpdatePending => _updateManager.IsUpdating;

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
                _updateManager.ClearPrecursors();
                UpdateUI();
            }
        }

        private void ZoomXAxis(Axis axis)
        {
            var instrument = DocumentUI.Settings.TransitionSettings.Instrument;
            double xMin;
            if (!instrument.IsDynamicMin || _nodeGroup == null)
                xMin = instrument.MinMz;
            else
                xMin = instrument.GetMinMz(_nodeGroup.PrecursorMz);

            var requestedRange = new MzRange(xMin, instrument.MaxMz);
            if (Settings.Default.SyncMZScale)
            {
                if(_documentContainer is ISpectrumScaleProvider scaleProvider)
                    requestedRange = scaleProvider.GetMzRange(SpectrumControlType.FullScanViewer) ?? requestedRange;
            } 

            ZoomXAxis(axis, requestedRange.Min, requestedRange.Max);
        }

        private void ZoomXAxis(Axis axis, double xMin, double xMax)
        {
            axis.Scale.Min = xMin;
            axis.Scale.MinAuto = false;
            axis.Scale.Max = xMax;
            axis.Scale.MaxAuto = false;
        }

        public void ZoomXAxis(double xMin, double xMax)
        {
            ZoomXAxis(GraphPane.XAxis, xMin, xMax);
        }

        public void ZoomSpectrumToSettings()
        {
            ZoomXAxis(GraphPane.XAxis);
            ZoomXAxis(GraphPane.X2Axis);

            var showingMain = DisplayedSpectrum != null;
            var showingMirror = DisplayedMirrorSpectrum != null;
            GraphPane.YAxis.MajorGrid.IsZeroLine = showingMirror && showingMain; // This is set in the MSGraphPane ctor, but we want it here
            GraphPane.LockYAxisMinAtZero = !showingMirror;
            GraphPane.X2Axis.IsVisible = GraphPane.LockYAxisMaxAtZero = showingMirror && !showingMain;

            if (showingMain || showingMirror)
            {
                var maxIntensity = 0.0;

                if (DisplayedSpectrum != null)
                    maxIntensity = DisplayedSpectrum.Intensities.Max();

                if (DisplayedMirrorSpectrum != null)
                    maxIntensity = Math.Max(maxIntensity, DisplayedMirrorSpectrum.Intensities.Max());

                maxIntensity *= YMAX_SCALE;

                GraphPane.YAxis.Scale.Max = DisplayedSpectrum == null ? 0.0 : maxIntensity;
                GraphPane.YAxis.Scale.Min = DisplayedMirrorSpectrum == null ? 0.0 : -maxIntensity;
            }

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

        private class ToolbarUpdate : IDisposable
        {
            private GraphSpectrum _spectrum;

            public ToolbarUpdate(GraphSpectrum spectrum)
            {
                _spectrum = spectrum;
                _spectrum._inToolbarUpdate = true;
            }

            public void Dispose()
            {
                _spectrum._inToolbarUpdate = false;
            }
        }

        private bool IsNotSmallMolecule => _nodeGroup == null || !_nodeGroup.IsCustomIon;

        private bool UsingProsit => Settings.Default.Prosit && IsNotSmallMolecule;

        private void UpdateToolbar()
        {
            if (SpectraCount < 2 && !UsingProsit)
            {
                toolBar.Visible = false;
            }
            else
            {
                var selectedPrecursor = comboPrecursor.SelectedItem;
                var selectedPrecursorIndex = comboPrecursor.SelectedIndex;
                var selectedSpectrum = comboSpectrum.SelectedItem;
                var selectedSpectrumIndex = comboSpectrum.SelectedIndex;
                var selectedMirror = comboMirrorSpectrum.SelectedItem;

                var showPrecursorSelect = SpectraCount > 1;
                comboPrecursor.Visible = labelPrecursor.Visible = showPrecursorSelect;
                if (!_updateManager.GetPrecursorStrings(out var precursorStrings))
                {
                    using (new ToolbarUpdate(this))
                    {
                        comboPrecursor.Items.Clear();
                        comboPrecursor.Items.AddRange(precursorStrings.ToArray());

                        if (selectedPrecursorIndex == 0 || selectedPrecursor == null || comboPrecursor.Items.IndexOf(selectedPrecursor) == -1)
                        {
                            if (comboPrecursor.Items.Count > 0)
                                comboPrecursor.SelectedIndex = 0;
                        }
                        else
                        {
                            comboPrecursor.SelectedItem = selectedPrecursor;
                        }

                        if (!Equals(selectedPrecursor, comboPrecursor.SelectedItem))
                        {
                            comboSpectrum.Items.Clear();
                            comboMirrorSpectrum.Items.Clear();
                            selectedSpectrum = null;
                            selectedSpectrumIndex = -1;
                            selectedMirror = null;
                        }
                    }

                    ComboHelper.AutoSizeDropDown(comboPrecursor);
                }

                var thisSpectra = SelectedPrecursor?.Spectra ?? new List<SpectrumDisplayInfo>();

                var showMirror = !UsingProsit && Settings.Default.LibMatchMirror;
                var showSpectraSelect = thisSpectra.Count > 1 && (!UsingProsit || Settings.Default.LibMatchMirror);
                comboMirrorSpectrum.Visible = mirrorLabel.Visible = showSpectraSelect && showMirror;
                comboSpectrum.Visible = labelSpectrum.Visible = showSpectraSelect;

                // Check to see if the list of spectra has changed.
                var spectraStrings = thisSpectra.Select(spectrum => spectrum.Identity).ToArray();
                if (!spectraStrings.SequenceEqual(from object item in comboSpectrum.Items select item.ToString()))
                {
                    using (new ToolbarUpdate(this))
                    {
                        comboSpectrum.Items.Clear();
                        comboSpectrum.Items.AddRange(spectraStrings);
                        comboMirrorSpectrum.Items.Clear();
                        comboMirrorSpectrum.Items.Add(string.Empty); // No mirror
                        comboMirrorSpectrum.Items.AddRange(spectraStrings);

                        if (selectedSpectrumIndex == 0 || selectedSpectrum == null || comboSpectrum.Items.IndexOf(selectedSpectrum) == -1)
                        {
                            if (comboSpectrum.Items.Count > 0)
                                comboSpectrum.SelectedIndex = 0;
                        }
                        else
                        {
                            comboSpectrum.SelectedItem = selectedSpectrum;
                        }

                        if (selectedMirror != null && comboMirrorSpectrum.Items.IndexOf(selectedMirror) != -1)
                            comboMirrorSpectrum.SelectedItem = selectedMirror;
                    }

                    ComboHelper.AutoSizeDropDown(comboSpectrum);
                    ComboHelper.AutoSizeDropDown(comboMirrorSpectrum);
                }

                var enableCE = false;
                // Update CE toolbar
                if (UsingProsit)
                {
                    var ces = Enumerable.Range(PrositConstants.MIN_NCE, PrositConstants.MAX_NCE - PrositConstants.MIN_NCE + 1).ToArray();

                    using (var _ = new ToolbarUpdate(this))
                    {
                        // TODO: figure out better way with _inToolBarUpdate
                        // Not a great way of doing this, but we need to ensure that we don't get into
                        // an infinite recursion
                        if (!comboCE.Items.Cast<int>().SequenceEqual(ces) || (int)comboCE.SelectedItem !=
                            Settings.Default.PrositNCE)
                        {
                            comboCE.Items.Clear();
                            comboCE.Items.AddRange(ces.Select(c => (object)c).ToArray());

                            comboCE.SelectedItem = Settings.Default.PrositNCE;
                        }
                    }

                    enableCE = true;

                    ComboHelper.AutoSizeDropDown(comboCE);
                }

                comboCE.Visible = enableCE;
                ceLabel.Visible = enableCE;

                // Show only if we made any of the things visible
                toolBar.Visible = showPrecursorSelect || showSpectraSelect || enableCE;
            }
        }

        public void SelectSpectrum(SpectrumIdentifier spectrumIdentifier)
        {
            if (PrecursorCount == 0)
                return;

            for (var i = 0; i < PrecursorCount; i++)
            {
                // Selection by file name and retention time should not select best spectrum
                var iSpectrum = Precursors[i].Spectra.IndexOf(spectrumInfo => !spectrumInfo.IsBest && SpectrumMatches(spectrumInfo, spectrumIdentifier));
                if (iSpectrum != -1)
                {
                    comboPrecursor.SelectedIndex = i;
                    comboSpectrum.SelectedIndex = iSpectrum;
                    return;
                }
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

        // For unit test
        public SpectrumDisplayInfo PrositSpectrum { get; private set; }

        public Precursor SelectedPrecursor =>
            PrecursorCount == 1 || (PrecursorCount > 1 && comboPrecursor.SelectedIndex >= 0)
                ? Precursors[PrecursorCount == 1 ? 0 : comboPrecursor.SelectedIndex]
                : null;

        public SpectrumDisplayInfo SelectedSpectrum
        {
            get
            {
                var spectra = SelectedPrecursor?.Spectra ?? new List<SpectrumDisplayInfo>();
                if (spectra.Count == 1)
                    return spectra[0];
                if (spectra.Count > 1 && comboSpectrum.SelectedIndex >= 0)
                    return spectra[comboSpectrum.SelectedIndex];
                return null;
            }
        }

        public SpectrumDisplayInfo SelectedMirrorSpectrum
        {
            get
            {
                var spectra = SelectedPrecursor?.Spectra ?? new List<SpectrumDisplayInfo>();
                if (spectra.Count == 1)
                    return spectra[0];
                if (spectra.Count > 1 && comboMirrorSpectrum.SelectedIndex >= 0)
                    return spectra[comboMirrorSpectrum.SelectedIndex];
                return null;
            }
        }

        public bool SelectionHasLibInfo
        {
            get
            {
                var precursor = SpectrumNodeSelection.GetCurrent(_stateProvider).NodeTranGroup ?? SelectedPrecursor.DocNode;
                return precursor != null && precursor.HasLibInfo;
            }
        }

        public LibraryRankedSpectrumInfo DisplayedSpectrum
        {
            get
            {
                if (GraphPane.CurveList.Count == 0)
                    return null;

                var spectrumGraphItem = GraphPane.CurveList.Select(ci => ci.Tag).OfType<SpectrumGraphItem>()
                    .FirstOrDefault(gi => !gi.Invert);
                return spectrumGraphItem?.SpectrumInfo;
            }
        }

        public LibraryRankedSpectrumInfo DisplayedMirrorSpectrum
        {
            get
            {
                if (GraphPane.CurveList.Count == 0)
                    return null;

                var spectrumGraphItem = GraphPane.CurveList.Select(ci => ci.Tag).OfType<SpectrumGraphItem>()
                    .FirstOrDefault(gi => gi.Invert);
                return spectrumGraphItem?.SpectrumInfo;
            }
        }

        public bool ShouldShowMirrorPlot
        {
            get { return Settings.Default.LibMatchMirror && SelectionHasLibInfo; }
        }

        public IEnumerable<SpectrumDisplayInfo> AvailableSpectra
        {
            get { return Precursors?.SelectMany(s => s.Spectra); }
        }

        public class Precursor
        {
            public Precursor(TreeNodeMS selectedTreeNode, Peptide peptide, Target lookupTarget,
                ExplicitMods lookupMods, TransitionGroupDocNode precursor)
            {
                SelectedNodeIsProtein = selectedTreeNode is PeptideGroupTreeNode;
                Peptide = peptide;
                LookupTarget = lookupTarget;
                LookupMods = lookupMods;
                DocNode = precursor;
                Spectra = new List<SpectrumDisplayInfo>();
                _prositCacheSettings = null;
                _prositSpectra = new Dictionary<Tuple<IsotopeLabelType, int>, SpectrumDisplayInfo>();
            }

            private bool SelectedNodeIsProtein { get; }
            public Peptide Peptide { get; }
            public Target LookupTarget { get; }
            public ExplicitMods LookupMods { get; }
            public TransitionGroupDocNode DocNode { get; }
            public List<SpectrumDisplayInfo> Spectra { get; }

            private SrmSettings _prositCacheSettings;
            private readonly Dictionary<Tuple<IsotopeLabelType, int>, SpectrumDisplayInfo> _prositSpectra;

            public bool TryGetPrositSpectrum(SrmSettings settings, IsotopeLabelType labelType, int nce, out SpectrumDisplayInfo spectrum)
            {
                if (!ReferenceEquals(settings, _prositCacheSettings))
                {
                    _prositSpectra.Clear();
                    spectrum = null;
                    return false;
                }
                return _prositSpectra.TryGetValue(Tuple.Create(labelType, nce), out spectrum);
            }

            public void CachePrositSpectrum(SrmSettings settings, IsotopeLabelType labelType, int nce, SpectrumDisplayInfo spectrum)
            {
                if (!ReferenceEquals(settings, _prositCacheSettings))
                {
                    _prositSpectra.Clear();
                    _prositCacheSettings = settings;
                }
                _prositSpectra[Tuple.Create(labelType, nce)] = spectrum;
            }

            public string PrecursorString
            {
                get
                {
                    var s = TransitionGroupTreeNode.GetLabel(DocNode.TransitionGroup, DocNode.PrecursorMz.RawValue, null);
                    return !SelectedNodeIsProtein ? s : $@"{DocNode.Peptide.Target}, {s}";
                }
            }
        }

        public class SpectrumNodeSelection
        {
            private SpectrumNodeSelection(TreeNodeMS selectedTreeNode, PeptideGroupDocNode nodePepGroup, PeptideDocNode nodePep,
                TransitionGroupDocNode nodeTranGroup, TransitionDocNode nodeTran)
            {
                SelectedTreeNode = selectedTreeNode;
                NodePepGroup = nodePepGroup;
                NodePep = nodePep;
                NodeTranGroup = nodeTranGroup;
                NodeTran = nodeTran;
            }

            public static SpectrumNodeSelection GetCurrent(IStateProvider stateProvider)
            {
                switch (stateProvider.SelectedNode)
                {
                    case PeptideGroupTreeNode p:
                    {
                        return new SpectrumNodeSelection(stateProvider.SelectedNode, p.DocNode, null, null, null);
                    }
                    case PeptideTreeNode p:
                    {
                        var usingProsit = p.DocNode.IsProteomic && Settings.Default.Prosit;
                        var listInfoGroups = GetChargeGroups(p, !usingProsit);
                        return new SpectrumNodeSelection(stateProvider.SelectedNode, p.PepGroupNode, p.DocNode,
                            listInfoGroups.Length == 1 ? listInfoGroups[0] : null, null);
                    }
                    case TransitionGroupTreeNode pr:
                    {
                        return new SpectrumNodeSelection(stateProvider.SelectedNode, pr.PepGroupNode, pr.PepNode, pr.DocNode, null);
                    }
                    case TransitionTreeNode t:
                    {
                        return new SpectrumNodeSelection(stateProvider.SelectedNode, t.PepGroupNode, t.PepNode, t.TransitionGroupNode, t.DocNode);
                    }
                }
                return new SpectrumNodeSelection(stateProvider.SelectedNode, null, null, null, null);
            }

            public static explicit operator PeptidePrecursorPair(SpectrumNodeSelection sel)
            {
                return new PeptidePrecursorPair(sel.NodePep, sel.NodeTranGroup);
            }

            public static explicit operator PrositIntensityModel.PeptidePrecursorNCE(SpectrumNodeSelection sel)
            {
                return new PrositIntensityModel.PeptidePrecursorNCE(sel.NodePep, sel.NodeTranGroup);
            }

            public TreeNodeMS SelectedTreeNode { get; }
            public PeptideGroupDocNode NodePepGroup { get; }
            public PeptideDocNode NodePep { get; }
            public TransitionGroupDocNode NodeTranGroup { get; }
            public TransitionDocNode NodeTran { get; }

            public PeptideDocNode GetPeptide(TransitionGroupDocNode nodeTranGroup)
            {
                return NodePep ?? NodePepGroup.Peptides.First(pep => ReferenceEquals(pep.Peptide, nodeTranGroup.Peptide));
            }
        }

        private PrositHelpers.PrositRequest _prositRequest;

        private SpectrumDisplayInfo UpdatePrositPrediction(SpectrumNodeSelection selection, IsotopeLabelType labelType, out Exception ex)
        {
            var settings = DocumentUI.Settings;
            var nce = Settings.Default.PrositNCE;

            // Try to get cached spectrum first
            var match = Precursors.FirstOrDefault(s =>
                ReferenceEquals(s.DocNode, selection.NodeTranGroup ?? SelectedPrecursor.DocNode));
            if (match != null && match.TryGetPrositSpectrum(settings, labelType, nce, out var spectrum))
            {
                ex = null;
                return spectrum;
            }

            try
            {
                var precursor = selection.NodeTranGroup ?? SelectedPrecursor.DocNode;
                var prositRequest = new PrositHelpers.PrositRequest(
                    settings, selection.GetPeptide(precursor), precursor, labelType, nce,
                    () => CommonActionUtil.SafeBeginInvoke(this, () => UpdateUI()));

                if (_prositRequest == null || !_prositRequest.Equals(prositRequest))
                {
                    // Cancel old request
                    _prositRequest?.Cancel();
                    _prositRequest = prositRequest.Predict();

                    throw new PrositPredictingException();
                }
                else if (_prositRequest.Spectrum == null)
                {
                    // Rethrow the exception caused by Prosit, otherwise
                    // we are still predicting
                    throw _prositRequest.Exception ?? new PrositPredictingException();
                }

                ex = null;
                match?.CachePrositSpectrum(settings, labelType, nce, _prositRequest.Spectrum);
                return _prositRequest.Spectrum;

            }
            catch (Exception x)
            {
                ex = x;
                return null;
            }
        }

        private SpectrumGraphItem MakeGraphItem(SpectrumDisplayInfo spectrum, SpectrumNodeSelection selection, SrmSettings settings, SpectrumPeaksInfo spectrumPeaksOverride = null)
        {
            var precursor = selection.NodeTranGroup ?? SelectedPrecursor.DocNode;
            var peptide = selection.GetPeptide(precursor);

            var group = precursor.TransitionGroup;
            var types = _stateProvider.ShowIonTypes(group.IsProteomic);
            var adducts =
                (group.IsProteomic
                    ? Transition.DEFAULT_PEPTIDE_LIBRARY_CHARGES
                    : precursor.InUseAdducts).ToArray();
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

            var spectrumInfoR = LibraryRankedSpectrumInfo.NewLibraryRankedSpectrumInfo(spectrumPeaksOverride ?? spectrum.SpectrumPeaksInfo,
                spectrum.LabelType,
                precursor,
                settings,
                peptide.SourceUnmodifiedTarget,
                peptide.SourceExplicitMods,
                showAdducts,
                types,
                rankAdducts,
                rankTypes,
                null);

            return new SpectrumGraphItem(peptide, precursor, selection.NodeTran, spectrumInfoR, spectrum.Name)
            {
                ShowTypes = types,
                ShowCharges = charges,
                ShowRanks = Settings.Default.ShowRanks,
                ShowScores = Settings.Default.ShowLibraryScores,
                ShowMz = Settings.Default.ShowIonMz,
                ShowObservedMz = Settings.Default.ShowObservedMz,
                ShowMassError = Settings.Default.ShowFullScanMassError,
                ShowDuplicates = Settings.Default.ShowDuplicateIons,
                FontSize = Settings.Default.SpectrumFontSize,
                LineWidth = Settings.Default.SpectrumLineWidth
            };
        }

        private SpectrumPeaksInfo RescaleMirrorSpectrum(SpectrumDisplayInfo mirrorSpectrum, SpectrumDisplayInfo mainSpectrum)
        {
            // Rescale so that mirror max is same as main max
            var spectrumPeaksInfo = mirrorSpectrum.SpectrumPeaksInfo;
            var maxIntensity = spectrumPeaksInfo.Intensities.Max();
            var mainMax = mainSpectrum.SpectrumPeaksInfo.Intensities.Max();
            return new SpectrumPeaksInfo(spectrumPeaksInfo.Peaks.Select(mi =>
                    new SpectrumPeaksInfo.MI
                    {
                        Mz = mi.Mz,
                        Intensity = (float) (mi.Intensity / maxIntensity * mainMax)
                    })
                .ToArray());
        }

        private void UpdateChromatogram(MSGraphPane graphPane, LibraryChromGroup chromatogramData, SpectrumNodeSelection selection)
        {
            _graphHelper.ResetForChromatograms(new[] { selection.NodeTranGroup.TransitionGroup });

            var displayType = GraphChromatogram.GetDisplayType(DocumentUI, selection.NodeTranGroup);
            IList<TransitionDocNode> displayTransitions =
                GraphChromatogram.GetDisplayTransitions(selection.NodeTranGroup, displayType).ToArray();
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
                ? GraphChromatogram.GetDisplayTransitions(selection.NodeTranGroup, DisplayTypeChrom.precursors).Count()
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
                MakeChromatogramInfo(selection.NodeTranGroup.PrecursorMz, chromatogramData, chromData,
                    out chromatogramInfo, out tranPeakInfo);
                var graphItem = new ChromGraphItem(selection.NodeTranGroup, matchingTransition,
                    chromatogramInfo,
                    iChromData == iChromDataPrimary ? tranPeakInfo : null, null,
                    new[] { iChromData == iChromDataPrimary }, null, 0, false, false, null, 0,
                    color, Settings.Default.ChromatogramFontSize, 1);
                LineItem curve =
                    (LineItem)_graphHelper.AddChromatogram(PaneKey.DEFAULT, graphItem);
                if (matchingTransition == null)
                {
                    curve.Label.Text = label;
                }

                curve.Line.Width = Settings.Default.ChromatogramLineWidth;
                if (selection.NodeTran != null)
                {
                    if (IonMatches(selection.NodeTran.Transition, chromData))
                    {
                        color = ColorScheme.ChromGraphItemSelected;
                    }
                }

                curve.Color = color;
            }

            graphPane.Title.IsVisible = false;
            graphPane.Legend.IsVisible = true;
            _graphHelper.FinishedAddingChromatograms(chromatogramData.StartTime,
                chromatogramData.EndTime, false);
        }

        private void ClearGraphPane()
        {
            // Clear existing data from the graph pane
            var graphPane = (MSGraphPane)graphControl.MasterPane[0];
            graphPane.CurveList.Clear();
            graphPane.GraphObjList.Clear();
            GraphItem = null;

            GraphHelper.FormatGraphPane(graphControl.GraphPane);
            GraphHelper.FormatFontSize(graphControl.GraphPane, Settings.Default.SpectrumFontSize);
        }

        public void UpdateUI(bool selectionChanged = true)
        {
            _updateManager.QueueUpdate(false);
        }

        private class UpdateManager : IDisposable
        {
            private readonly GraphSpectrum _parent;
            private readonly Timer _timer;

            public ImmutableList<Precursor> Precursors { get; private set; }
            private TreeNodeMS _treeNode;
            private SrmSettings _settings;
            private bool _spectraLoaded;
            private ImmutableList<string> _precursorStrings;

            public UpdateManager(GraphSpectrum parent)
            {
                _parent = parent;
                _timer = new Timer { Interval = 100 };
                _timer.Tick += DoUpdate;
            }

            public bool IsUpdating => _timer.Tag != null;

            public void QueueUpdate(bool isUserAction)
            {
                // Restart the timer at 100ms, giving the UI time to interrupt.
                _timer.Stop();
                _timer.Interval = 100;
                _timer.Tag = isUserAction;
                _timer.Start();
            }

            private void DoUpdate(object sender, EventArgs e)
            {
                // Stop the timer immediately, to keep from getting called again
                // for the same triggering event.
                _timer.Stop();

                _parent.DoUpdate();
                _parent.FireSelectedSpectrumChanged((bool)_timer.Tag);

                if (!_timer.Enabled)
                    _timer.Tag = null;
            }

            public void ClearPrecursors()
            {
                UpdatePrecursors(null, null, null);
            }

            public void CalculatePrecursors(SpectrumNodeSelection selection, SrmSettings settings)
            {
                if (ReferenceEquals(_treeNode, selection.SelectedTreeNode) && ReferenceEquals(_settings, settings))
                    return;

                var precursors = new List<Precursor>();
                const int limit = 100; // for performance reasons
                if (selection.NodePepGroup != null)
                {
                    if (selection.NodeTranGroup != null)
                        precursors.Add(new Precursor(selection.SelectedTreeNode, selection.NodePep.Peptide,
                            selection.NodePep.SourceUnmodifiedTarget, selection.NodePep.SourceExplicitMods, selection.NodeTranGroup));
                    else
                        precursors.AddRange((
                            from peptide in selection.NodePep != null ? new[] { selection.NodePep } : selection.NodePepGroup.Peptides
                            from precursor in peptide.TransitionGroups
                            select new Precursor(selection.SelectedTreeNode, peptide.Peptide, peptide.SourceUnmodifiedTarget,
                                peptide.SourceExplicitMods, precursor)).Take(limit));
                }

                UpdatePrecursors(precursors, selection.SelectedTreeNode, settings);
            }

            private void UpdatePrecursors(ICollection<Precursor> precursors, TreeNodeMS treeNode, SrmSettings settings)
            {
                Precursors = precursors != null && precursors.Count > 0 ? ImmutableList<Precursor>.ValueOf(precursors) : null;
                _treeNode = treeNode;
                _settings = settings;
                _spectraLoaded = false;
                _precursorStrings = null;
            }

            public void LoadSpectra()
            {
                if (_spectraLoaded)
                    return;

                foreach (var p in Precursors)
                {
                    p.Spectra.Clear();
                    p.Spectra.AddRange(_settings
                        .GetBestSpectra(p.LookupTarget, p.DocNode.PrecursorAdduct, p.LookupMods)
                        .Select(s => new SpectrumDisplayInfo(s, p.DocNode)));
                }

                // Showing redundant spectra is only supported for full-scan filtering when
                // the document has results files imported.
                if ((_settings.TransitionSettings.FullScan.IsEnabled || _settings.PeptideSettings.Libraries.HasMidasLibrary) && _settings.HasResults)
                {
                    try
                    {
                        for (var i = 0; i < Precursors.Count; i++)
                        {
                            var precursor = Precursors.ElementAt(i);
                            var spectraRedundant = new List<SpectrumDisplayInfo>();
                            var dictReplicateNameFiles = new Dictionary<string, HashSet<string>>();
                            foreach (var spectrumInfo in _settings.GetRedundantSpectra(precursor.Peptide,
                                         precursor.LookupTarget, precursor.DocNode.PrecursorAdduct,
                                         precursor.DocNode.LabelType, precursor.LookupMods))
                            {
                                var matchingFile = _settings.MeasuredResults.FindMatchingMSDataFile(MsDataFileUri.Parse(spectrumInfo.FilePath));
                                if (matchingFile == null)
                                    continue;

                                var replicateName = matchingFile.Chromatograms.Name;
                                spectraRedundant.Add(new SpectrumDisplayInfo(spectrumInfo, precursor.DocNode,
                                    replicateName, matchingFile.FilePath, matchingFile.FileOrder,
                                    spectrumInfo.RetentionTime, false));

                                // Include the best spectrum twice, once displayed in the normal
                                // way and once displayed with its replicate and retention time.
                                if (spectrumInfo.IsBest)
                                {
                                    var iBest = Precursors[i].Spectra.IndexOf(s =>
                                        Equals(s.Name, spectrumInfo.Name) &&
                                        Equals(s.LabelType, spectrumInfo.LabelType));
                                    if (iBest != -1)
                                    {
                                        Precursors[i].Spectra[iBest] = new SpectrumDisplayInfo(
                                            Precursors[i].Spectra[iBest].SpectrumInfo, precursor.DocNode,
                                            replicateName,
                                            matchingFile.FilePath, 0, spectrumInfo.RetentionTime, true);
                                    }
                                }

                                if (!dictReplicateNameFiles.TryGetValue(replicateName, out var setFiles))
                                {
                                    setFiles = new HashSet<string>();
                                    dictReplicateNameFiles.Add(replicateName, setFiles);
                                }

                                setFiles.Add(spectrumInfo.FilePath);
                            }

                            // Determine if replicate name is sufficient to uniquely identify the file
                            foreach (var spectrumInfo in spectraRedundant)
                            {
                                var replicateName = spectrumInfo.ReplicateName;
                                if (replicateName != null && dictReplicateNameFiles[replicateName].Count < 2)
                                    spectrumInfo.IsReplicateUnique = true;
                            }
                            spectraRedundant.Sort();
                            Precursors[i].Spectra.AddRange(spectraRedundant);
                        }
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

                _spectraLoaded = true;
            }

            public bool GetPrecursorStrings(out ImmutableList<string> outStrings)
            {
                if (_precursorStrings != null)
                {
                    outStrings = _precursorStrings;
                    return true;
                }

                outStrings = Precursors != null
                    ? ImmutableList.ValueOf(Precursors.Select(p => p.PrecursorString))
                    : ImmutableList<string>.EMPTY;
                _precursorStrings = outStrings;
                return false;
            }

            public void Dispose()
            {
                _timer.Dispose();
            }
        }

        private void DoUpdate()
        {
            // Only worry about updates, if the graph is visible
            // And make sure it is not disposed, since rendering happens on a timer
            if (!Visible || IsDisposed)
                return;

            // Try to find a tree node with spectral library info associated
            // with the current selection.
            var selection = SpectrumNodeSelection.GetCurrent(_stateProvider);

            // Check for appropriate spectrum to load
            var settings = DocumentUI.Settings;
            var libraries = settings.PeptideSettings.Libraries;
            var available = false;

            try
            {
                Exception prositEx = null;
                var usingProsit = (selection.NodePep == null || selection.NodePep.IsProteomic) && Settings.Default.Prosit;

                if (usingProsit && !PrositHelpers.PrositSettingsValid)
                {
                    prositEx = new PrositNotConfiguredException();

                    if (!Settings.Default.LibMatchMirror)
                        throw prositEx;
                }

                _updateManager.CalculatePrecursors(selection, settings);
                if (Precursors == null || (!Precursors.Any(p => p.DocNode.HasLibInfo) && !libraries.HasMidasLibrary && !usingProsit))
                {
                    _updateManager.ClearPrecursors();
                    PrositSpectrum = null;
                }
                else if (libraries.HasLibraries && libraries.IsLoaded || usingProsit)
                {
                    // Try to load a list of spectra matching the criteria for
                    // the current node group.
                    UpdateToolbar();

                    // Need this to make sure we still update the toolbar if the prosit prediction throws
                    SpectrumDisplayInfo spectrum = null;
                    PrositSpectrum = null;

                    if (usingProsit && !Settings.Default.LibMatchMirror && prositEx == null)
                    {
                        spectrum = PrositSpectrum = UpdatePrositPrediction(selection, null, out prositEx);
                    }

                    var loadFromLib = libraries.HasLibraries && libraries.IsLoaded && (!usingProsit || Settings.Default.LibMatchMirror);

                    try
                    {
                        if (loadFromLib)
                        {
                            _updateManager.LoadSpectra();

                            // For a mirrored spectrum, make sure the isotope label types between library and Prosit match
                            if (usingProsit && Settings.Default.LibMatchMirror && prositEx == null)
                                PrositSpectrum = UpdatePrositPrediction(selection, SelectedPrecursor?.DocNode.LabelType, out prositEx);
                        }
                        UpdateToolbar();
                    }
                    catch (Exception)
                    {
                        _updateManager.ClearPrecursors();
                        UpdateToolbar();
                        throw;
                    }

                    if (prositEx != null && !Settings.Default.LibMatchMirror)
                        throw prositEx;

                    if (!usingProsit || ShouldShowMirrorPlot)
                        spectrum = SelectedSpectrum;

                    if (prositEx is PrositPredictingException && DisplayedMirrorSpectrum != null)
                    {
                        var libraryStr = _spectrum == null
                            ? _mirrorSpectrum.Name
                            : string.Format(PrositResources.GraphSpectrum_UpdateUI__0__vs___1_,
                                _spectrum.Name, _mirrorSpectrum.Name);
                        GraphPane.Title.Text = TextUtil.LineSeparate(libraryStr,
                            SpectrumGraphItem.GetTitle(null, selection.GetPeptide(_mirrorSpectrum.Precursor),
                                _mirrorSpectrum.Precursor, _mirrorSpectrum.LabelType), prositEx.Message);
                        graphControl.Refresh();
                        return;
                    }

                    var spectrumChanged = !Equals(_spectrum?.SpectrumInfo, spectrum?.SpectrumInfo);
                    _spectrum = spectrum;

                    ClearGraphPane();

                    LibraryChromGroup chromatogramData = null;
                    if (Settings.Default.ShowLibraryChromatograms)
                        chromatogramData = spectrum?.LoadChromatogramData();

                    if (spectrum != null)
                        GraphItem = MakeGraphItem(spectrum, selection, settings);

                    if (null == chromatogramData)
                    {
                        if (spectrum != null)
                        {
                            _graphHelper.ResetForSpectrum(new[] { spectrum.Precursor.TransitionGroup });
                            // Don't refresh here, it will be refreshed on zoom
                            _graphHelper.AddSpectrum(GraphItem, false);
                        }

                        var mirrorSpectrum = SelectedMirrorSpectrum;
                        if (Settings.Default.LibMatchMirror)
                        {
                            if (usingProsit)
                                mirrorSpectrum = PrositSpectrum;
                        }
                        else
                        {
                            mirrorSpectrum = null;
                        }

                        spectrumChanged |= !Equals(_mirrorSpectrum?.SpectrumInfo, mirrorSpectrum?.SpectrumInfo);
                        _mirrorSpectrum = mirrorSpectrum;

                        double? dotp = null;
                        SpectrumGraphItem mirrorGraphItem = null;
                        if (mirrorSpectrum != null)
                        {
                            var peaksInfo = spectrum != null
                                ? RescaleMirrorSpectrum(mirrorSpectrum, spectrum)
                                : mirrorSpectrum.SpectrumPeaksInfo;
                            mirrorGraphItem = MakeGraphItem(mirrorSpectrum, selection, settings, peaksInfo);
                            mirrorGraphItem.Invert = true;

                            _graphHelper.AddSpectrum(mirrorGraphItem, false);
                            
                            if (spectrum != null)
                                dotp = PrositHelpers.CalculateSpectrumDotpMzMatch(GraphItem.SpectrumInfo, mirrorGraphItem.SpectrumInfo,
                                    settings.TransitionSettings.Libraries.IonMatchTolerance);
                        }

                        if (mirrorSpectrum != null && mirrorGraphItem != null) // one implies the other, but resharper..
                        {
                            if (dotp != null)
                            {
                                GraphPane.Title.Text = TextUtil.LineSeparate(
                                    string.Format(PrositResources.GraphSpectrum_UpdateUI__0__vs___1_,
                                        GraphItem.LibraryName, mirrorSpectrum.Name),
                                    SpectrumGraphItem.RemoveLibraryPrefix(GraphItem.Title, GraphItem.LibraryName),
                                    string.Format(Resources.GraphSpectrum_DoUpdate_dotp___0_0_0000_, dotp));
                            }
                            else
                            {
                                GraphPane.Title.Text = TextUtil.LineSeparate(
                                    mirrorSpectrum.Name,
                                    mirrorGraphItem.Title,
                                    PrositResources.GraphSpectrum_UpdateUI_No_spectral_library_match);
                            }

                        }
                        else if (prositEx != null)
                        {
                            if (DisplayedSpectrum != null)
                            {
                                GraphPane.Title.Text = TextUtil.LineSeparate(
                                    string.Format(PrositResources.GraphSpectrum_UpdateUI__0__vs___1_,
                                        GraphItem.LibraryName, SpectrumInfoProsit.NAME),
                                    SpectrumGraphItem.RemoveLibraryPrefix(GraphItem.Title, GraphItem.LibraryName),
                                    prositEx.Message);
                            }
                            else
                            {
                                throw prositEx;
                            }
                        }
                        
                        _graphHelper.ZoomSpectrumToSettings(DocumentUI, selection.NodeTranGroup);
                    }
                    else
                    {
                        UpdateChromatogram(GraphPane, chromatogramData, selection);
                    }

                    if (spectrum != null || _mirrorSpectrum != null)
                    {
                        graphControl.IsEnableVPan = graphControl.IsEnableVZoom =
                            !Settings.Default.LockYAxis;
                        available = true;
                    }
                    
                    if (spectrumChanged && chromatogramData == null)
                    {
                        _nodeGroup = selection.NodeTranGroup;
                        ZoomSpectrumToSettings();
                    }
                    else
                    {
                        graphControl.Refresh();
                    }
                }
            }
            catch (PrositException ex)
            {
                ClearGraphPane();
                _graphHelper.SetErrorGraphItem(new ExceptionMSGraphItem(ex));
                return;
            }
            catch (Exception)
            {
                ClearGraphPane();
                //_graphHelper.SetErrorGraphItem(new NoDataMSGraphItem(ex.Message));
                _graphHelper.SetErrorGraphItem(new NoDataMSGraphItem(
                    Resources.GraphSpectrum_UpdateUI_Failure_loading_spectrum__Library_may_be_corrupted));
                return;
            }

            // Show unavailable message, if no spectrum loaded
            if (!available)
            {
                ClearGraphPane();
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
            if(transition.IonType.Equals(chromData.IonType) && Equals(transition.Adduct, chromData.Charge))
            {
                if(transition.IsPrecursor())
                {
                    return transition.MassIndex == chromData.MassIndex;
                }
                else
                {
                    return transition.IonType == IonType.custom ?
                        Equals(transition.FragmentIonName, chromData.FragmentName) :
                        transition.Ordinal == chromData.Ordinal;
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

        public void LockYAxis(bool lockY)
        {
            graphControl.IsEnableVPan = graphControl.IsEnableVZoom = !lockY;
            graphControl.Refresh();
        }

        public void SetMzScale(MzRange range)
        {
            ZoomXAxis(GraphPane.XAxis, range.Min, range.Max);
            graphControl.Invalidate();
        }
        public MzRange Range
        {
            get {return new MzRange(GraphPane.XAxis.Scale.Min, GraphPane.XAxis.Scale.Max);}
        }

        public Scale IntensityScale
        {
            get { return GraphPane.YAxis.Scale; }
        }

        public void ApplyMZZoomState(ZoomState newState)
        {
            newState.XAxis.ApplyScale(GraphPane.XAxis);
            graphControl.Refresh();
        }

        public event EventHandler<ZoomEventArgs> ZoomEvent;
        private void graphControl_ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState, PointF mousePosition)
        {
            FireZoomEvent(newState);
        }

        private void FireZoomEvent(ZoomState zoomState = null)
        {
            if (ZoomEvent != null && Settings.Default.SyncMZScale)
            {
                if (zoomState == null)
                    zoomState = new ZoomState(GraphPane, ZoomState.StateType.Zoom);
                ZoomEvent.Invoke(this, new ZoomEventArgs(zoomState));
            }
        }

        public SpectrumControlType ControlType { get { return SpectrumControlType.LibraryMatch;} }
        public bool IsAnnotated => true;

        public double MzMax
        {
            get { return GraphPane.XAxis.Scale.Max; }
        }

// ReSharper disable SuggestBaseTypeForParameter
        private static TransitionGroupDocNode[] GetChargeGroups(PeptideTreeNode nodeTree, bool requireLibInfo)
// ReSharper restore SuggestBaseTypeForParameter
        {
            // Return the first group of each charge stat that has library info.
            var listGroups = new List<TransitionGroupDocNode>();
            foreach (TransitionGroupDocNode nodeGroup in nodeTree.ChildDocNodes)
            {
                if (requireLibInfo && !nodeGroup.HasLibInfo)
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
            _stateProvider.BuildSpectrumMenu(IsNotSmallMolecule, sender, menuStrip);
        }

        protected override void OnClosed(EventArgs e)
        {
            _updateManager.Dispose();
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

        private void comboPrecursor_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_inToolbarUpdate)
            {
                _updateManager.QueueUpdate(true);
            }
        }

        private void comboSpectrum_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_inToolbarUpdate)
            {
                _updateManager.QueueUpdate(true);
            }
        }

        private void comboMirrorSpectrum_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_inToolbarUpdate)
            {
                UpdateUI();
            }
        }

        private void comboCE_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Default.PrositNCE = (int) comboCE.SelectedItem;
            if (!_inToolbarUpdate)
            {
                UpdateUI();
            }
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
            var ionMobilityFilter = IonMobilityFilter.GetIonMobilityFilter(chromData.IonMobility,null, chromGroup.CCS);
            transitionChromInfo = new TransitionChromInfo(null, 0, chromPeak,
                ionMobilityFilter,
                Annotations.EMPTY,
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
                0, -1, -1, null, null, chromGroup.CCS, ionMobilityFilter.IonMobilityUnits);
            var groupInfo = new ChromatogramGroupInfo(header,
                new[]
                {
                    new ChromTransition(chromData.Mz, 0,
                        (float) (ionMobilityFilter.IonMobilityAndCCS.IonMobility.Mobility ?? 0),
                        (float) (ionMobilityFilter.IonMobilityExtractionWindowWidth ?? 0), ChromSource.unknown),
                }, peaks, TimeIntensitiesGroup.Singleton(timeIntensities));
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

        public bool ShowZHIons
        {
            get { return Set.ShowZHIons; }
            set { ActAndUpdate(() => Set.ShowZHIons = value); }
        }

        public bool ShowZHHIons
        {
            get { return Set.ShowZHHIons; }
            set { ActAndUpdate(() => Set.ShowZHHIons = value); }
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
                AddItem(types, IonType.zh, Set.ShowZHIons);
                AddItem(types, IonType.zhh, Set.ShowZHHIons);
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
        public SpectrumDisplayInfo(SpectrumInfo spectrumInfo, TransitionGroupDocNode precursor, double? retentionTime = null)
        {
            SpectrumInfo = spectrumInfo;
            Precursor = precursor;
            IsBest = true;
            RetentionTime = retentionTime;
        }

        public SpectrumDisplayInfo(SpectrumInfo spectrumInfo, TransitionGroupDocNode precursor, string replicateName,
            MsDataFileUri filePath, int fileOrder, double? retentionTime, bool isBest)
        {
            SpectrumInfo = spectrumInfo;
            Precursor = precursor;
            ReplicateName = replicateName;
            FilePath = filePath;
            FileOrder = fileOrder;
            RetentionTime = retentionTime;
            IsBest = isBest;
        }

        public SpectrumInfo SpectrumInfo { get; }
        public TransitionGroupDocNode Precursor { get; private set; }
        public string Name { get { return SpectrumInfo.Name; } }
        public IsotopeLabelType LabelType { get { return SpectrumInfo.LabelType; } }
        public string ReplicateName { get; private set; }
        public bool IsReplicateUnique { get; set; }
        public MsDataFileUri FilePath { get; private set; }
        public string FileName { get { return FilePath.GetFileName(); } }
        public int FileOrder { get; private set; }
        public double? RetentionTime { get; private set; }
        public bool IsBest { get; private set; }

        public string Identity { get { return ToString(); } }

        public SpectrumPeaksInfo SpectrumPeaksInfo { get { return SpectrumInfo.SpectrumPeaksInfo; } }
        public LibraryChromGroup LoadChromatogramData() { return SpectrumInfo.ChromatogramData; }

        public int CompareTo(SpectrumDisplayInfo other)
        {
            if (other == null) return 1;
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
    public sealed class MzRange
    {
        public MzRange(double min, double max)
        {
            Min = min;
            Max = max;
        }

        public MzRange() : this(0, 1){}

        public double Min { get; private set; }
        public double Max { get; private set; }
        public override bool Equals(object obj)
        {
            if (obj is MzRange other)
                return Min == other.Min && Max == other.Max;
            else
                return false;
        }

        public override int GetHashCode()
        {
            return Min.GetHashCode() * 397 ^ Max.GetHashCode();
        }
    }
}
