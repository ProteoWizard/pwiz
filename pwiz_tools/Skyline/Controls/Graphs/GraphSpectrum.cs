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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;

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

    public partial class GraphSpectrum : DockableFormEx, IGraphContainer
    {

        private static readonly double YMAX_SCALE = 1.25;

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

        public TransitionGroupDocNode Precursor
        {
            get { return GraphItem.TransitionGroupNode; }
        }

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

        private void ZoomXAxis(Axis axis)
        {
            var instrument = DocumentUI.Settings.TransitionSettings.Instrument;
            if (!instrument.IsDynamicMin || _nodeGroup == null)
                axis.Scale.Min = instrument.MinMz;
            else
                axis.Scale.Min = instrument.GetMinMz(_nodeGroup.PrecursorMz);
            axis.Scale.MinAuto = false;
            axis.Scale.Max = instrument.MaxMz;
            axis.Scale.MaxAuto = false;
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
                    labelSpectrum.Visible = true;
                }
                else
                {
                    comboSpectrum.Visible = false;
                    labelSpectrum.Visible = false;
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

                        using (new ToolbarUpdate(this))
                        {
                            comboSpectrum.Items.Clear();
                            comboMirrorSpectrum.Items.Clear();
                            comboMirrorSpectrum.Items.Add(string.Empty); // No mirror
                            foreach (string name in listNames)
                            {
                                comboSpectrum.Items.Add(name);
                                comboMirrorSpectrum.Items.Add(name);
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

                            if (selectedMirror != null && comboMirrorSpectrum.Items.IndexOf(selectedMirror) != -1)
                            {
                                comboMirrorSpectrum.SelectedItem = selectedMirror;
                            }
                        }

                        ComboHelper.AutoSizeDropDown(comboSpectrum);
                        ComboHelper.AutoSizeDropDown(comboMirrorSpectrum);
                    }
                }

                var enableCE = false;
                // Update CE toolbar
                if (Settings.Default.Prosit)
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

        // For unit test
        public SpectrumDisplayInfo PrositSpectrum { get; private set; }

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

        public bool SelectionHasLibInfo
        {
            get
            {
                var selectedNodes = SpectrumNodeSelection.GetCurrent(_stateProvider);
                return selectedNodes.Precursor != null &&
                       selectedNodes.Precursor.HasLibInfo;
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
            get { return _spectra; }
        }

        public class SpectrumNodeSelection
        {
            public SpectrumNodeSelection(PeptideDocNode peptide,
                TransitionGroupDocNode precursor, TransitionDocNode transition)
            {
                Peptide = peptide;
                Precursor = precursor;
                Transition = transition;
            }

            public static SpectrumNodeSelection GetCurrent(IStateProvider stateProvider)
            {
                switch (stateProvider.SelectedNode)
                {
                    case PeptideTreeNode p:
                    {
                        var listInfoGroups = GetChargeGroups(p, !Settings.Default.Prosit);
                        return new SpectrumNodeSelection(p.DocNode,
                            listInfoGroups.Length == 1 ? listInfoGroups[0] : null, null);
                    }
                    case TransitionGroupTreeNode pr:
                    {
                        return new SpectrumNodeSelection(pr.PepNode, pr.DocNode, null);
                    }
                    case TransitionTreeNode t:
                    {
                        return new SpectrumNodeSelection(t.PepNode, t.TransitionGroupNode, t.DocNode);
                    }
                }

                return new SpectrumNodeSelection(null, null, null);
            }

            public static explicit operator PeptidePrecursorPair(SpectrumNodeSelection sel)
            {
                return new PeptidePrecursorPair(sel.Peptide, sel.Precursor);
            }

            public static explicit operator PrositIntensityModel.PeptidePrecursorNCE(SpectrumNodeSelection sel)
            {
                return new PrositIntensityModel.PeptidePrecursorNCE(sel.Peptide, sel.Precursor);
            }

            public PeptideDocNode Peptide { get; private set; }
            public TransitionGroupDocNode Precursor { get; private set; }
            public TransitionDocNode Transition { get; private set; }
        }

        private PrositHelpers.PrositRequest _prositRequest;

        private SpectrumDisplayInfo UpdatePrositPrediction(SpectrumNodeSelection selection, IsotopeLabelType labelType, out Exception ex)
        {
            try
            {
                var prositRequest = new PrositHelpers.PrositRequest(
                    DocumentUI.Settings, selection.Peptide, selection.Precursor, labelType,
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
            var group = selection.Precursor.TransitionGroup;
            var types = _stateProvider.ShowIonTypes(group.IsProteomic);
            var adducts =
                (group.IsProteomic
                    ? Transition.DEFAULT_PEPTIDE_LIBRARY_CHARGES
                    : selection.Precursor.InUseAdducts).ToArray();
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


            var lookupData = new LookupData(selection);
            var spectrumInfoR = new LibraryRankedSpectrumInfo(spectrumPeaksOverride ?? spectrum.SpectrumPeaksInfo,
                spectrum.LabelType,
                selection.Precursor,
                settings,
                lookupData.LookupSequence,
                lookupData.LookupMods,
                showAdducts,
                types,
                rankAdducts,
                rankTypes,
                null);
            return new SpectrumGraphItem(selection.Precursor, selection.Transition, spectrumInfoR,
                spectrum.Name)
            {
                ShowTypes = types,
                ShowCharges = charges,
                ShowRanks = Settings.Default.ShowRanks,
                ShowScores = Settings.Default.ShowLibraryScores,
                ShowMz = Settings.Default.ShowIonMz,
                ShowObservedMz = Settings.Default.ShowObservedMz,
                ShowDuplicates = Settings.Default.ShowDuplicateIons,
                FontSize = Settings.Default.SpectrumFontSize,
                LineWidth = Settings.Default.SpectrumLineWidth
            };
        }

        private class LookupData
        {
            public LookupData(SpectrumNodeSelection selection)
            {
                var group = selection.Precursor.TransitionGroup;
                LookupSequence = group.Peptide.Target; // Sequence or custom ion id

                if (selection.Peptide != null)
                {
                    LookupSequence = selection.Peptide.SourceUnmodifiedTarget;
                    LookupMods = selection.Peptide.SourceExplicitMods;
                }
            }

            public Target LookupSequence { get; }
            public ExplicitMods LookupMods { get; }
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
            _graphHelper.ResetForChromatograms(new[] { selection.Precursor.TransitionGroup });

            var displayType = GraphChromatogram.GetDisplayType(DocumentUI, selection.Precursor);
            IList<TransitionDocNode> displayTransitions =
                GraphChromatogram.GetDisplayTransitions(selection.Precursor, displayType).ToArray();
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
                ? GraphChromatogram.GetDisplayTransitions(selection.Precursor,
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
                MakeChromatogramInfo(selection.Precursor.PrecursorMz, chromatogramData, chromData,
                    out chromatogramInfo, out tranPeakInfo);
                var graphItem = new ChromGraphItem(selection.Precursor, matchingTransition,
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
                if (selection.Transition != null)
                {
                    if (IonMatches(selection.Transition.Transition, chromData))
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

                if (Settings.Default.Prosit && !PrositHelpers.PrositSettingsValid)
                {
                    prositEx = new PrositNotConfiguredException();

                    if (!Settings.Default.LibMatchMirror)
                        throw prositEx;
                }
                    
                if (selection.Precursor == null || (!selection.Precursor.HasLibInfo && !libraries.HasMidasLibrary &&
                                                    !Settings.Default.Prosit))
                {
                    _spectra = null;
                    PrositSpectrum = null;
                }
                else
                {
                    // Try to load a list of spectra matching the criteria for
                    // the current node group.
                    if (libraries.HasLibraries && libraries.IsLoaded || Settings.Default.Prosit)
                    {
                        // Need this to make sure we still update the toolbar if the prosit prediction throws
                        
                        SpectrumDisplayInfo spectrum = null;
                        PrositSpectrum = null;

                        if (Settings.Default.Prosit && !Settings.Default.LibMatchMirror && prositEx == null)
                        {
                            spectrum = PrositSpectrum = UpdatePrositPrediction(selection, null, out prositEx);
                            if (prositEx == null)
                                _spectra = new[] { spectrum };
                        }

                        var loadFromLib = libraries.HasLibraries && libraries.IsLoaded &&
                                          (!Settings.Default.Prosit || Settings.Default.LibMatchMirror);

                        try
                        {
                            if (loadFromLib)
                            {
                                UpdateSpectra(selection.Precursor, new LookupData(selection));

                                // For a mirrored spectrum, make sure the isotope label types between library and Prosit match
                                if (Settings.Default.Prosit && Settings.Default.LibMatchMirror && prositEx == null)
                                {
                                    var labelType = _spectra != null ? _spectra[0].LabelType : null;
                                    PrositSpectrum = UpdatePrositPrediction(selection, labelType, out prositEx);
                                }
                            }
                            UpdateToolbar();
                        }
                        catch (Exception)
                        {
                            _spectra = null;
                            UpdateToolbar();
                            throw;
                        }

                        if (prositEx != null && !Settings.Default.LibMatchMirror)
                            throw prositEx;

                        if (!Settings.Default.Prosit || ShouldShowMirrorPlot)
                            spectrum = SelectedSpectrum;
                        
                        if (prositEx is PrositPredictingException && DisplayedMirrorSpectrum != null)
                        {
                            var libraryStr = _spectrum == null
                                ? _mirrorSpectrum.Name
                                : string.Format(PrositResources.GraphSpectrum_UpdateUI__0__vs___1_,
                                    _spectrum.Name, _mirrorSpectrum.Name);
                            GraphPane.Title.Text = TextUtil.LineSeparate(
                                libraryStr,
                                SpectrumGraphItem.GetTitle(_mirrorSpectrum.Precursor, _mirrorSpectrum.LabelType),
                                prositEx.Message);
                            graphControl.Refresh();
                            return;
                        }

                        var spectrumChanged = !Equals(_spectrum, spectrum);
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
                                _graphHelper.ResetForSpectrum(new[] { selection.Precursor.TransitionGroup });
                                // Don't refresh here, it will be refreshed on zoom
                                _graphHelper.AddSpectrum(GraphItem, false);
                            }

                            var mirrorSpectrum = SelectedMirrorSpectrum;
                            if (Settings.Default.LibMatchMirror)
                            {
                                if (Settings.Default.Prosit)
                                    mirrorSpectrum = PrositSpectrum;
                            }
                            else
                            {
                                mirrorSpectrum = null;
                            }

                            spectrumChanged |= !Equals(_mirrorSpectrum, mirrorSpectrum);
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
                                        GraphItem.Title,
                                        string.Format(@"dotp: {0:0.0000}", dotp));
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
                                        GraphItem.Title,
                                        prositEx.Message);
                                }
                                else
                                {
                                    throw prositEx;
                                }
                            }
                            
                            _graphHelper.ZoomSpectrumToSettings(DocumentUI, selection.Precursor);
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
                            _nodeGroup = selection.Precursor;
                            ZoomSpectrumToSettings();
                        }
                        else
                        {
                            graphControl.Refresh();
                        }
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

        private void UpdateSpectra(TransitionGroupDocNode nodeGroup, LookupData lookup)
        {
            _spectra = GetSpectra(nodeGroup, lookup);
            if (!_spectra.Any())
                _spectra = null;
        }

        private IList<SpectrumDisplayInfo> GetSpectra(TransitionGroupDocNode nodeGroup, LookupData lookup)
        {
            var settings = DocumentUI.Settings;
            var charge = nodeGroup.PrecursorAdduct;
            var spectra = settings.GetBestSpectra(lookup.LookupSequence, charge, lookup.LookupMods).Select(s => new SpectrumDisplayInfo(s, nodeGroup)).ToList();
            // Showing redundant spectra is only supported for full-scan filtering when
            // the document has results files imported.
            if ((!settings.TransitionSettings.FullScan.IsEnabled && !settings.PeptideSettings.Libraries.HasMidasLibrary) || !settings.HasResults)
                return spectra;

            try
            {
                var spectraRedundant = new List<SpectrumDisplayInfo>();
                var dictReplicateNameFiles = new Dictionary<string, HashSet<string>>();
                foreach (var spectrumInfo in settings.GetRedundantSpectra(nodeGroup.Peptide, lookup.LookupSequence, charge, nodeGroup.TransitionGroup.LabelType, lookup.LookupMods))
                {
                    var matchingFile = settings.MeasuredResults.FindMatchingMSDataFile(MsDataFileUri.Parse(spectrumInfo.FilePath));
                    if (matchingFile == null)
                        continue;

                    string replicateName = matchingFile.Chromatograms.Name;
                    spectraRedundant.Add(new SpectrumDisplayInfo(spectrumInfo,
                        nodeGroup,
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
                                nodeGroup,
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
            Settings.Default.PrositNCE = (int) comboCE.SelectedItem;
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
                    new byte[0],
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
