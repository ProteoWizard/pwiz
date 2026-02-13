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
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.MSGraph;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Koina;
using pwiz.Skyline.Model.Koina.Models;
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
        LibraryRankedSpectrumInfo SpectrumInfo { get; }
        bool ShowPropertiesSheet { get; set; }
        bool HasChromatogramData { get; }
    }

    public interface ISpectrumScaleProvider
    {
        MzRange GetMzRange(SpectrumControlType controlType);
    }
    
    public partial class GraphSpectrum : DockableFormEx, IGraphContainer, IMzScalePlot, IMenuControlImplementer, ITipDisplayer
    {

        private static readonly double YMAX_SCALE = 1.25;

        public interface IStateProvider : ISpectrumScaleProvider
        {
            TreeNodeMS SelectedNode { get; }
            IList<IonType> ShowIonTypes(bool isProteomic);

            IList<string> ShowLosses();

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

            public IList<string> ShowLosses()
            {
                return null;
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

        public class ToolTipImplementation : ITipProvider
        {
            LibraryRankedSpectrumInfo.RankedMI _peakRMI;
            private TableDesc _table;       // Used for test support

            // This property for testing purposes only, not visible to the user
            [Localizable(false)]
            public string ToolTipText
            {
                get
                {
                    if (_table == null)
                        return string.Empty;
                    StringBuilder sbText = new StringBuilder();
                    for (var rowIndex = 0; rowIndex < _table.Count; rowIndex++)
                    {
                        for (var colIndex = 0; colIndex < _table[rowIndex].Count; colIndex++)
                        {
                            sbText.Append(_table[rowIndex][colIndex].Text);
                            if (colIndex < _table[rowIndex].Count - 1)
                                sbText.Append("\t");
                        }
                        if (rowIndex < _table.Count - 1)
                            sbText.Append("\n");
                    }
                    return sbText.ToString();
                }
            }

            public ToolTipImplementation(LibraryRankedSpectrumInfo.RankedMI peakRMI)
            {
                _peakRMI = peakRMI;
            }
            public bool HasTip => _peakRMI != null;

            public Size RenderTip(Graphics g, Size sizeMax, bool draw)
            {
                if (!HasTip)
                    return Size.Empty;
                var table = new TableDesc();
                using(var rt = new RenderTools())
                {
                    table.AddDetailRow(GraphsResources.GraphSpectrum_ToolTip_mz,
                          _peakRMI.ObservedMz.ToString(Formats.Mz, CultureInfo.CurrentCulture), rt);
                    table.AddDetailRow(GraphsResources.GraphSpectrum_ToolTip_Intensity,
                        _peakRMI.Intensity.ToString(@"##", CultureInfo.CurrentCulture), rt);
                    if (_peakRMI.Rank > 0)
                        table.AddDetailRow(GraphsResources.GraphSpectrum_ToolTip_Rank, 
                            string.Format(@"{0}",  _peakRMI.Rank), rt);
                    if (_peakRMI.MatchedIons != null && _peakRMI.MatchedIons.Count > 0)
                    {
                        table.AddDetailRow(GraphsResources.GraphSpectrum_ToolTip_MatchedIons, GraphsResources.ToolTipImplementation_RenderTip_Calculated_Mass, rt, true);
                        foreach (var mfi in _peakRMI.MatchedIons)
                            table.AddDetailRow(AbstractSpectrumGraphItem.GetLabel(mfi, _peakRMI.Rank, false, false),
                                mfi.PredictedMz.ToString(Formats.Mz, CultureInfo.CurrentCulture) + @"  " +
                                AbstractSpectrumGraphItem.GetMassErrorString(_peakRMI, mfi), rt);
                    }
                    _table = table;
                    var size = table.CalcDimensions(g);
                    if (draw)
                        table.Draw(g);
                    return new Size((int)size.Width + 2, (int)size.Height + 2);
                }
            }
        }

        private readonly IDocumentUIContainer _documentContainer;
        private readonly IStateProvider _stateProvider;
        private readonly UpdateManager _updateManager;
        private TransitionGroupDocNode _nodeGroup;

        private ImmutableList<Precursor> Precursors => _updateManager.Precursors;
        private int PrecursorCount => Precursors?.Count ?? 0;

        private SpectrumDisplayInfo _spectrum;
        private NodeTip _toolTip;
                
        private string _userSelectedSpectrum;
        private SpectrumDisplayInfo _mirrorSpectrum;
        private string _userSelectedMirrorSpectrum;

        private bool _inToolbarUpdate;
        // TODO
        // private object _spectrumKeySave;
        private readonly GraphHelper _graphHelper;
        private MSGraphControl graphControl => msGraphExtension.Graph;

        public ZedGraphControl ZedGraphControl => graphControl;

        public GraphSpectrum(IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();
            graphControl.ContextMenuBuilder += graphControl_ContextMenuBuilder;
            graphControl.MouseMove += GraphControl_MouseMove;
            msGraphExtension.PropertiesSheetVisibilityChanged += msGraphExtension_PropertiesSheetVisibilityChanged;

            Icon = Resources.SkylineData;
            _graphHelper = GraphHelper.Attach(graphControl);
            _documentContainer = documentUIContainer;
            _documentContainer.ListenUI(OnDocumentUIChanged);
            _stateProvider = documentUIContainer as IStateProvider ??
                             new DefaultStateProvider();
            _updateManager = new UpdateManager(this);

            // ReSharper disable once PossibleNullReferenceException
            comboPrecursor.ComboBox.DisplayMember = nameof(Precursor.DisplayString);
            msGraphExtension.RestorePropertiesSheet();

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
        private SpectrumGraphItem MirrorGraphItem { get; set; }

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
        /// Normalized collision energy for Koina
        /// </summary>
        public int KoinaNCE
        {
            get { return (int) (comboCE.SelectedItem ?? -1); }
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
            if (e.DocumentPrevious != null && !ReferenceEquals(
                    DocumentUI.Settings.PeptideSettings.Modifications.StaticModifications,
                    e.DocumentPrevious.Settings.PeptideSettings.Modifications.StaticModifications))
            {
                var newMods = DocumentUI.Settings.PeptideSettings.Modifications.StaticModsLosses;
                var oldMods = e.DocumentPrevious.Settings.PeptideSettings.Modifications.StaticModsLosses;
                var addedMods = newMods.ToList().FindAll(newMod => !oldMods.Contains(newMod));
                if (addedMods.Any())
                    Settings.Default.ShowLosses = Settings.Default.ShowLosses + @"," + addedMods.ToString(@",");
            }

            // If document changed, update spectrum x scale to instrument
            // Or if library settings changed, show new ranks etc
            if (e.DocumentPrevious == null ||
                !ReferenceEquals(DocumentUI.Id, e.DocumentPrevious.Id) ||
                !ReferenceEquals(DocumentUI.Settings.TransitionSettings.Libraries, 
                                 e.DocumentPrevious.Settings.TransitionSettings.Libraries) ||
                !ReferenceEquals(DocumentUI.Settings.PeptideSettings.Libraries.Libraries,
                                 e.DocumentPrevious.Settings.PeptideSettings.Libraries.Libraries))
            {
                _userSelectedSpectrum = _userSelectedMirrorSpectrum = null;
                ZoomSpectrumToSettings();
                Settings.Default.ShowLosses = DocumentUI.Settings.PeptideSettings.Modifications.StaticModsLosses.ToString(@",");
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
            axis.Scale.MinAuto = axis.Scale.MinAuto = false;
            axis.Scale.Min = xMin;
            axis.Scale.Max = xMax;
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

                var hasDisplayedSpectrumValues = DisplayedSpectrum != null && DisplayedSpectrum.Intensities.Any();
                if (hasDisplayedSpectrumValues)
                    maxIntensity = DisplayedSpectrum.Intensities.Max();

                var hasDisplayedMirrorSpectrumValues = DisplayedMirrorSpectrum != null && DisplayedMirrorSpectrum.Intensities.Any();
                if (hasDisplayedMirrorSpectrumValues)
                    maxIntensity = Math.Max(maxIntensity, DisplayedMirrorSpectrum.Intensities.Max());
                if (maxIntensity == 0)
                {
                    // Prevent Scale.Max and Scale.Min being set to the same value because it makes the entire chart disappear
                    maxIntensity = 1;
                }
                maxIntensity *= YMAX_SCALE;

                GraphPane.YAxis.Scale.Max = maxIntensity;
                GraphPane.YAxis.Scale.Min = !hasDisplayedMirrorSpectrumValues ? 0.0 : -maxIntensity;
            }

            graphControl.Refresh();
        }

        private void GraphSpectrum_VisibleChanged(object sender, EventArgs e)
        {
            UpdateUI();
        }

        private void propertiesMenuItem_Click(object sender, EventArgs e)
        {
            ShowPropertiesSheet = !ShowPropertiesSheet;
        }
        private void msGraphExtension_PropertiesSheetVisibilityChanged(object sender, EventArgs e)
        {
            propertiesButton.Checked = ShowPropertiesSheet;
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

        private bool UsingKoina => Settings.Default.Koina && IsNotSmallMolecule;

        private void UpdateToolbar()
        {
            var selectedPrecursor = comboPrecursor.SelectedItem;
            var selectedPrecursorIndex = comboPrecursor.SelectedIndex;
            var selectedSpectrum = comboSpectrum.SelectedItem;
            var selectedSpectrumIndex = comboSpectrum.SelectedIndex;
            var selectedMirror = comboMirrorSpectrum.SelectedItem;

            var showPrecursorSelect = PrecursorCount > 1;
            comboPrecursor.Visible = labelPrecursor.Visible = showPrecursorSelect;
            if (!ArrayUtil.ReferencesEqual(_updateManager.Precursors, comboPrecursor.Items.Cast<Precursor>().ToArray()))
            {
                using (new ToolbarUpdate(this))
                {
                    comboPrecursor.Items.Clear();
                    if (_updateManager.Precursors != null)
                        comboPrecursor.Items.AddRange(_updateManager.Precursors.ToArray());

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
                        selectedSpectrumIndex = -1;
                        selectedSpectrum = _userSelectedSpectrum;
                        comboMirrorSpectrum.Items.Clear();
                        selectedMirror = _userSelectedMirrorSpectrum;
                    }
                }

                ComboHelper.AutoSizeDropDown(comboPrecursor);
            }

            var thisSpectra = SelectedPrecursor?.Spectra ?? ImmutableList<SpectrumDisplayInfo>.EMPTY;

            var showMirror = !UsingKoina && Settings.Default.LibMatchMirror;
            var showSpectraSelect = thisSpectra.Count > 1 && (!UsingKoina || Settings.Default.LibMatchMirror);
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
            if (UsingKoina)
            {
                var ces = Enumerable.Range(KoinaConstants.MIN_NCE, KoinaConstants.MAX_NCE - KoinaConstants.MIN_NCE + 1).ToArray();

                using (var _ = new ToolbarUpdate(this))
                {
                    // TODO: figure out better way with _inToolBarUpdate
                    // Not a great way of doing this, but we need to ensure that we don't get into
                    // an infinite recursion
                    if (!comboCE.Items.Cast<int>().SequenceEqual(ces) || (int)comboCE.SelectedItem !=
                        Settings.Default.KoinaNCE)
                    {
                        comboCE.Items.Clear();
                        comboCE.Items.AddRange(ces.Select(c => (object)c).ToArray());

                        comboCE.SelectedItem = Settings.Default.KoinaNCE;
                    }
                }

                enableCE = true;

                ComboHelper.AutoSizeDropDown(comboCE);
            }

            comboCE.Visible = enableCE;
            ceLabel.Visible = enableCE;

            // Show only if we made any of the things visible
            toolBar.Visible = showPrecursorSelect || showSpectraSelect || enableCE;
            propertiesButton.Checked = ShowPropertiesSheet;
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
                    if (comboPrecursor.SelectedIndex != i)
                    {
                        comboPrecursor.SelectedIndex = i;
                        DoUpdate();
                    }
                    comboSpectrum.SelectedIndex = iSpectrum;
                    return;
                }
            }
        }

        public void SelectSpectrum(string libraryName)
        {
            comboSpectrum.SelectedItem = libraryName;
        }

        public void SelectMirrorSpectrum(string libraryName)
        {
            comboMirrorSpectrum.SelectedItem = libraryName;
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
        public SpectrumDisplayInfo KoinaSpectrum { get; private set; }

        public Precursor SelectedPrecursor =>
            PrecursorCount == 1 || (PrecursorCount > 1 && comboPrecursor.SelectedIndex >= 0)
                ? Precursors[PrecursorCount == 1 ? 0 : comboPrecursor.SelectedIndex]
                : null;

        public SpectrumDisplayInfo SelectedSpectrum
        {
            get
            {
                var spectra = SelectedPrecursor?.Spectra;
                if (spectra == null || spectra.Count == 0)
                    return null;

                return spectra.Count == 1 ? spectra[0] : spectra[comboSpectrum.SelectedIndex];
            }
        }

        public SpectrumDisplayInfo SelectedMirrorSpectrum
        {
            get
            {
                // Return null if toolbar not visible or no mirror selected.
                // The spectrum index is one minus the selected index in the combobox, since the first item is empty (i.e. no mirror).
                var spectrumIndex = comboMirrorSpectrum.SelectedIndex - 1;
                if (!toolBar.Visible || spectrumIndex < 0)
                    return null;

                var spectra = SelectedPrecursor?.Spectra;
                if (spectra == null || spectra.Count == 0)
                    return null;

                return spectra[spectrumIndex];
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
            public Precursor(SrmSettings settings, TreeNodeMS selectedTreeNode, PeptideDocNode nodePep, TransitionGroupDocNode precursor)
            {
                Settings = settings;
                NodePep = nodePep;
                DocNode = precursor;
                _spectra = null;
                _koinaSpectra = new Dictionary<Tuple<IsotopeLabelType, int>, SpectrumDisplayInfo>();

                var s = TransitionGroupTreeNode.GetLabel(DocNode.TransitionGroup, DocNode.PrecursorMz.RawValue, null);
                DisplayString = !(selectedTreeNode is PeptideGroupTreeNode) ? s : $@"{DocNode.Peptide.Target}, {s}";
            }

            private SrmSettings Settings { get; }
            private PeptideDocNode NodePep { get; }
            private Peptide Peptide => NodePep.Peptide;
            private Target LookupTarget => NodePep.SourceUnmodifiedTarget;
            private ExplicitMods LookupMods => NodePep.SourceExplicitMods;
            public TransitionGroupDocNode DocNode { get; }
            public string DisplayString { get; } // The string displayed in the precursor combobox

            private ImmutableList<SpectrumDisplayInfo> _spectra;
            private Dictionary<Tuple<IsotopeLabelType, int>, SpectrumDisplayInfo> _koinaSpectra;

            public static void ReuseSpectra(ICollection<Precursor> newPrecursors, ICollection<Precursor> oldPrecursors)
            {
                if (newPrecursors == null || newPrecursors.Count == 0 ||
                    oldPrecursors == null || oldPrecursors.Count == 0 ||
                    !ReferenceEquals(newPrecursors.First().Settings, oldPrecursors.First().Settings))
                    return;

                var existingDict = oldPrecursors.ToDictionary(p => p.DocNode, p => p);
                foreach (var precursor in newPrecursors)
                {
                    if (existingDict.TryGetValue(precursor.DocNode, out var existing))
                    {
                        precursor._spectra = existing._spectra;
                        precursor._koinaSpectra = existing._koinaSpectra;
                    }
                }
            }

            public ImmutableList<SpectrumDisplayInfo> Spectra
            {
                get
                {
                    if (_spectra != null)
                        return _spectra;
                    else if (!Settings.PeptideSettings.Libraries.HasLibraries || !Settings.PeptideSettings.Libraries.IsLoaded)
                        return null;

                    var spectra = new List<SpectrumDisplayInfo>(Settings
                        .GetBestSpectra(LookupTarget, DocNode.PrecursorAdduct, LookupMods)
                        .Select(s => new SpectrumDisplayInfo(s, DocNode)));

                    // Showing redundant spectra is only supported for full-scan filtering when
                    // the document has results files imported.
                    if ((Settings.TransitionSettings.FullScan.IsEnabled || Settings.PeptideSettings.Libraries.HasMidasLibrary) && Settings.HasResults)
                    {
                        try
                        {
                            var spectraRedundant = new List<SpectrumDisplayInfo>();
                            var dictReplicateNameFiles = new Dictionary<string, HashSet<string>>();
                            foreach (var spectrumInfo in Settings.GetRedundantSpectra(Peptide, LookupTarget, DocNode.PrecursorAdduct, DocNode.LabelType, LookupMods))
                            {
                                var matchingFile = Settings.MeasuredResults.FindMatchingMSDataFile(MsDataFileUri.Parse(spectrumInfo.FilePath));
                                if (matchingFile == null)
                                    continue;

                                var replicateName = matchingFile.Chromatograms.Name;
                                spectraRedundant.Add(new SpectrumDisplayInfo(spectrumInfo, DocNode, replicateName,
                                    matchingFile.FilePath, matchingFile.FileOrder, spectrumInfo.RetentionTime, false));

                                // Include the best spectrum twice, once displayed in the normal
                                // way and once displayed with its replicate and retention time.
                                if (spectrumInfo.IsBest)
                                {
                                    var iBest = spectra.IndexOf(s =>
                                        Equals(s.Name, spectrumInfo.Name) &&
                                        Equals(s.LabelType, spectrumInfo.LabelType));
                                    if (iBest != -1)
                                    {
                                        spectra[iBest] = new SpectrumDisplayInfo(spectra[iBest].SpectrumInfo, DocNode,
                                            replicateName, matchingFile.FilePath, 0, spectrumInfo.RetentionTime, true);
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
                            spectra.AddRange(spectraRedundant);
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }

                    _spectra = ImmutableList<SpectrumDisplayInfo>.ValueOf(spectra);
                    return _spectra;
                }
            }

            public bool TryGetKoinaSpectrum(IsotopeLabelType labelType, int nce, out SpectrumDisplayInfo spectrum)
            {
                return _koinaSpectra.TryGetValue(Tuple.Create(labelType, nce), out spectrum);
            }

            public void CacheKoinaSpectrum(IsotopeLabelType labelType, int nce, SpectrumDisplayInfo spectrum)
            {
                _koinaSpectra[Tuple.Create(labelType, nce)] = spectrum;
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
                        var usingKoina = p.DocNode.IsProteomic && Settings.Default.Koina;
                        var listInfoGroups = GetChargeGroups(p.DocNode, !usingKoina).ToArray();
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

            public static explicit operator KoinaIntensityModel.PeptidePrecursorNCE(SpectrumNodeSelection sel)
            {
                return new KoinaIntensityModel.PeptidePrecursorNCE(sel.NodePep, sel.NodeTranGroup);
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

        private KoinaHelpers.KoinaRequest _koinaRequest;

        private SpectrumDisplayInfo UpdateKoinaPrediction(SpectrumNodeSelection selection, IsotopeLabelType labelType, out Exception ex)
        {
            var settings = DocumentUI.Settings;
            var nce = Settings.Default.KoinaNCE;

            // Try to get cached spectrum first
            var match = Precursors.FirstOrDefault(s =>
                ReferenceEquals(s.DocNode, selection.NodeTranGroup ?? SelectedPrecursor.DocNode));
            if (match != null && match.TryGetKoinaSpectrum(labelType, nce, out var spectrum))
            {
                ex = null;
                return spectrum;
            }

            try
            {
                var precursor = selection.NodeTranGroup ?? SelectedPrecursor.DocNode;
                var koinaRequest = new KoinaHelpers.KoinaRequest(
                    settings, selection.GetPeptide(precursor), precursor, labelType, nce,
                    () => CommonActionUtil.SafeBeginInvoke(this, () => UpdateUI()));

                if (_koinaRequest == null || !_koinaRequest.Equals(koinaRequest))
                {
                    // Cancel old request
                    _koinaRequest?.Cancel();
                    _koinaRequest = koinaRequest.Predict();

                    throw new KoinaPredictingException();
                }
                else if (_koinaRequest.Spectrum == null)
                {
                    // Rethrow the exception caused by Koina, otherwise
                    // we are still predicting
                    throw _koinaRequest.Exception ?? new KoinaPredictingException();
                }

                ex = null;
                match?.CacheKoinaSpectrum(labelType, nce, _koinaRequest.Spectrum);
                return _koinaRequest.Spectrum;

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
            var losses = _stateProvider.ShowLosses();
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

            double? score = null;
            if(spectrum.SpectrumInfo is SpectrumInfoLibrary libInfo)
                score = libInfo.SpectrumHeaderInfo?.Score;
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
                score);

            return new SpectrumGraphItem(peptide, precursor, selection.NodeTran, spectrumInfoR, spectrum.Name)
            {
                ShowTypes = types,
                ShowCharges = charges,
                ShowLosses = losses,
                ShowRanks = Settings.Default.ShowRanks,
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
                var graphItem = new ChromGraphItem(selection.NodeTranGroup, matchingTransition, chromatogramInfo,
                    iChromData == iChromDataPrimary ? tranPeakInfo : null, null,
                    new[] { iChromData == iChromDataPrimary }, null, 0, false, false, null, null, color,
                    Settings.Default.ChromatogramFontSize, 1);
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
            AllowDisplayTip = false;

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
            private bool _koina;

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
                UpdatePrecursors(null, null, null, false);
            }

            public void CalculatePrecursors(SpectrumNodeSelection selection, SrmSettings settings, bool koina)
            {
                if (ReferenceEquals(_treeNode, selection.SelectedTreeNode) && ReferenceEquals(_settings, settings) && _koina == koina)
                    return;

                var precursors = new List<Precursor>();
                const int limit = 100; // for performance reasons
                if (selection.NodePepGroup != null)
                {
                    if (selection.NodeTranGroup != null)
                    {
                        if (selection.NodeTranGroup.HasLibInfo || koina)
                            precursors.Add(new Precursor(settings, selection.SelectedTreeNode, selection.NodePep, selection.NodeTranGroup));
                    }
                    else
                    {
                        precursors.AddRange((
                            from nodePep in selection.NodePep != null ? new[] { selection.NodePep } : selection.NodePepGroup.Peptides
                            from nodeTranGroup in GetChargeGroups(nodePep, !koina)
                            select new Precursor(settings, selection.SelectedTreeNode, nodePep, nodeTranGroup)).Take(limit));
                    }
                }

                UpdatePrecursors(precursors, selection.SelectedTreeNode, settings, koina);
            }

            private void UpdatePrecursors(ICollection<Precursor> precursors, TreeNodeMS treeNode, SrmSettings settings, bool koina)
            {
                Precursor.ReuseSpectra(precursors, Precursors);
                Precursors = precursors != null && precursors.Count > 0 ? ImmutableList<Precursor>.ValueOf(precursors) : null;
                _treeNode = treeNode;
                _settings = settings;
                _koina = koina;
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
                Exception koinaEx = null;
                var usingKoina = (selection.NodePep == null || selection.NodePep.IsProteomic) && Settings.Default.Koina;

                if (usingKoina && !KoinaHelpers.KoinaSettingsValid)
                {
                    koinaEx = new KoinaNotConfiguredException();

                    if (!Settings.Default.LibMatchMirror)
                        throw koinaEx;
                }

                _updateManager.CalculatePrecursors(selection, settings, usingKoina);
                if (Precursors == null || (!Precursors.Any(p => p.DocNode.HasLibInfo) && !libraries.HasMidasLibrary && !usingKoina))
                {
                    _updateManager.ClearPrecursors();
                    KoinaSpectrum = null;
                }
                else if (libraries.HasLibraries && libraries.IsLoaded || usingKoina)
                {
                    // Try to load a list of spectra matching the criteria for
                    // the current node group.
                    UpdateToolbar();

                    // Need this to make sure we still update the toolbar if the koina prediction throws
                    SpectrumDisplayInfo spectrum = null;
                    KoinaSpectrum = null;

                    if (usingKoina && !Settings.Default.LibMatchMirror && koinaEx == null)
                    {
                        spectrum = KoinaSpectrum = UpdateKoinaPrediction(selection, null, out koinaEx);
                    }

                    try
                    {
                        var loadFromLib = libraries.HasLibraries && libraries.IsLoaded && (!usingKoina || Settings.Default.LibMatchMirror);
                        if (loadFromLib)
                        {
                            // For a mirrored spectrum, make sure the isotope label types between library and Koina match
                            if (usingKoina && Settings.Default.LibMatchMirror && koinaEx == null)
                                KoinaSpectrum = UpdateKoinaPrediction(selection, SelectedPrecursor?.DocNode.LabelType, out koinaEx);
                        }
                        UpdateToolbar();
                    }
                    catch (Exception)
                    {
                        _updateManager.ClearPrecursors();
                        UpdateToolbar();
                        throw;
                    }

                    if (koinaEx != null && !Settings.Default.LibMatchMirror)
                        throw koinaEx;

                    if (!usingKoina || ShouldShowMirrorPlot)
                        spectrum = SelectedSpectrum;

                    if (koinaEx is KoinaPredictingException && DisplayedMirrorSpectrum != null)
                    {
                        var libraryStr = _spectrum == null
                            ? _mirrorSpectrum.Name
                            : string.Format(KoinaResources.GraphSpectrum_UpdateUI__0__vs___1_,
                                _spectrum.Name, _mirrorSpectrum.Name);
                        GraphPane.Title.Text = TextUtil.LineSeparate(libraryStr,
                            SpectrumGraphItem.GetTitle(null, selection.GetPeptide(_mirrorSpectrum.Precursor),
                                _mirrorSpectrum.Precursor, _mirrorSpectrum.LabelType), koinaEx.Message);
                        graphControl.Refresh();
                        return;
                    }

                    var spectrumChanged = !Equals(_spectrum?.SpectrumInfo, spectrum?.SpectrumInfo);
                    _spectrum = spectrum;
                    if(spectrumChanged)
                        HasChromatogramData = _spectrum?.LoadChromatogramData() != null;

                    ClearGraphPane();

                    LibraryChromGroup chromatogramData = null;
                    if (Settings.Default.ShowLibraryChromatograms)
                        chromatogramData = spectrum?.LoadChromatogramData();

                    if (spectrum != null)
                    {
                        GraphItem = MakeGraphItem(spectrum, selection, settings);
                        AllowDisplayTip = true;
                    }

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
                            if (usingKoina)
                                mirrorSpectrum = KoinaSpectrum;
                        }
                        else
                        {
                            mirrorSpectrum = null;
                            MirrorGraphItem = null;
                        }

                        spectrumChanged |= !Equals(_mirrorSpectrum?.SpectrumInfo, mirrorSpectrum?.SpectrumInfo);
                        _mirrorSpectrum = mirrorSpectrum;

                        double? dotp = null;
                        // need to keep it at the class level to be able to extract tooltip info.
                        double? fullDotp = null;
                        if (mirrorSpectrum != null)
                        {
                            var peaksInfo = spectrum != null
                                ? RescaleMirrorSpectrum(mirrorSpectrum, spectrum)
                                : mirrorSpectrum.SpectrumPeaksInfo;
                            MirrorGraphItem = MakeGraphItem(mirrorSpectrum, selection, settings, peaksInfo);
                            MirrorGraphItem.Invert = true;

                            _graphHelper.AddSpectrum(MirrorGraphItem, false);

                            if (spectrum != null)
                            {
                                dotp = KoinaHelpers.CalculateSpectrumDotpMzMatch(GraphItem.SpectrumInfo,
                                    MirrorGraphItem.SpectrumInfo,
                                    settings.TransitionSettings.Libraries.IonMatchMzTolerance);
                                fullDotp = KoinaHelpers.CalculateSpectrumDotpMzFull(GraphItem.SpectrumInfo.Peaks,
                                    MirrorGraphItem.SpectrumInfo.Peaks,
                                    settings.TransitionSettings.Libraries.IonMatchMzTolerance, true, false);
                            }
                        }

                        if (mirrorSpectrum != null && MirrorGraphItem != null) // one implies the other, but resharper..
                        {
                            GraphPane.Title.Text = dotp != null
                                ? TextUtil.LineSeparate(
                                    string.Format(KoinaResources.GraphSpectrum_UpdateUI__0__vs___1_,
                                        GraphItem.LibraryName, mirrorSpectrum.Name),
                                    SpectrumGraphItem.RemoveLibraryPrefix(GraphItem.Title, GraphItem.LibraryName))
                                : TextUtil.LineSeparate(
                                    mirrorSpectrum.Name,
                                    MirrorGraphItem.Title,
                                    KoinaResources.GraphSpectrum_UpdateUI_No_spectral_library_match);
                        }
                        else if (koinaEx != null)
                        {
                            if (DisplayedSpectrum != null)
                            {
                                GraphPane.Title.Text = TextUtil.LineSeparate(
                                    string.Format(KoinaResources.GraphSpectrum_UpdateUI__0__vs___1_,
                                        GraphItem.LibraryName, SpectrumInfoKoina.NAME),
                                    SpectrumGraphItem.RemoveLibraryPrefix(GraphItem.Title, GraphItem.LibraryName),
                                    koinaEx.Message);
                            }
                            else
                            {
                                throw koinaEx;
                            }
                        }

                        _graphHelper.ZoomSpectrumToSettings(DocumentUI, selection.NodeTranGroup);

                        if (GraphItem != null && GraphItem.PeptideDocNode != null && GraphItem.TransitionGroupNode != null && spectrum?.SpectrumInfo is SpectrumInfoLibrary libInfo)
                        {
                            var pepInfo = new ViewLibraryPepInfo(
                                GraphItem.PeptideDocNode.ModifiedTarget.GetLibKey(GraphItem.TransitionGroupNode.PrecursorAdduct), 
                                    libInfo.SpectrumHeaderInfo)
                                .ChangePeptideNode(selection.NodePep);
                            var props = libInfo.CreateProperties(pepInfo, spectrum.Precursor, new LibKeyModificationMatcher());
                            if (GraphItem != null)
                            {
                                props.PeakCount = GraphItem.SpectrumInfo.Peaks.Count(mi => mi.Intensity > 0)
                                    .ToString(Formats.PEAK_AREA);
                                props.TotalIC = GraphItem.SpectrumInfo.Peaks.Sum(mi => mi.Intensity)
                                    .ToString(@"0.0000E+0", CultureInfo.CurrentCulture);
                            }
                            if (MirrorGraphItem != null)
                            {
                                props.MirrorPeakCount = MirrorGraphItem.SpectrumInfo.Peaks.Count(mi => mi.Intensity > 0)
                                    .ToString(Formats.PEAK_AREA);
                                props.MirrorTotalIC = MirrorGraphItem.SpectrumInfo.Peaks.Sum(mi => mi.Intensity)
                                    .ToString(@"0.0000E+0", CultureInfo.CurrentCulture);
                            }

                            if (dotp.HasValue)
                                props.KoinaDotpMatch = string.Format(GraphsResources.GraphSpectrum_DoUpdate_dotp___0_0_0000_, dotp);
                            if (fullDotp.HasValue)
                                props.KoinaDotpMatchFull =
                                    string.Format(GraphsResources.GraphSpectrum_DoUpdate_dotp___0_0_0000_, fullDotp);
                            msGraphExtension.SetPropertiesObject(props);
                        }
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
            catch (KoinaException ex)
            {
                ClearGraphPane();
                _graphHelper.SetErrorGraphItem(new ExceptionMSGraphItem(ex));
                msGraphExtension.SetPropertiesObject(null);
                return;
            }
            catch (Exception)
            {
                ClearGraphPane();
                //_graphHelper.SetErrorGraphItem(new NoDataMSGraphItem(ex.Message));
                _graphHelper.SetErrorGraphItem(new NoDataMSGraphItem(
                    GraphsResources.GraphSpectrum_UpdateUI_Failure_loading_spectrum__Library_may_be_corrupted));
                msGraphExtension.SetPropertiesObject(null);
                return;
            }

            // Show unavailable message, if no spectrum loaded
            if (!available)
            {
                ClearGraphPane();
                UpdateToolbar();
                _nodeGroup = null;
                _graphHelper.SetErrorGraphItem(new UnavailableMSGraphItem());
                msGraphExtension.SetPropertiesObject(null);
            }
        }
        
        public bool ShowPropertiesSheet 
        {
            set
            {
                msGraphExtension.ShowPropertiesSheet(value);
                propertiesButton.Checked = value;
            }
            get
            {
                return msGraphExtension.PropertiesVisible;
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

        public LibraryRankedSpectrumInfo SpectrumInfo => GraphItem?.SpectrumInfo;

        public bool HasChromatogramData
        {
            get;
            private set;
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

        private static IEnumerable<TransitionGroupDocNode> GetChargeGroups(PeptideDocNode nodePep, bool requireLibInfo)
        {
            // Return the first group of each charge stat that has library info.
            var adducts = new HashSet<Adduct>();
            foreach (var nodeGroup in nodePep.TransitionGroups)
            {
                if (requireLibInfo && !nodeGroup.HasLibInfo)
                    continue;

                var precursorCharge = nodeGroup.TransitionGroup.PrecursorAdduct;
                if (!adducts.Contains(precursorCharge))
                {
                    adducts.Add(precursorCharge);
                    yield return nodeGroup;
                }
            }
        }

        private void graphControl_ContextMenuBuilder(ZedGraphControl sender,
            ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            _stateProvider.BuildSpectrumMenu(IsNotSmallMolecule, sender, menuStrip);
        }

        public MenuControl<T> GetHostedControl<T>() where T : Panel, IControlSize, new()
        {
            if (graphControl.ContextMenuStrip != null)
            {
                var chargesItem = graphControl.ContextMenuStrip.Items.OfType<ToolStripMenuItem>()
                    .FirstOrDefault(item => item.DropDownItems.OfType<MenuControl<T>>().Any());
                if (chargesItem != null)
                    return chargesItem.DropDownItems[0] as MenuControl<T>;
            }
            return null;
        }

        public void DisconnectHandlers()
        {
            if (_documentContainer is SkylineWindow skylineWindow)
            {
                var chargeSelector = GetHostedControl<ChargeSelectionPanel>();

                if (chargeSelector != null)
                {
                    chargeSelector.HostedControl.OnChargeChanged -= skylineWindow.IonChargeSelector_ionChargeChanged;
                }

                var ionTypeSelector = GetHostedControl<IonTypeSelectionPanel>();
                if (ionTypeSelector != null)
                {
                    ionTypeSelector.HostedControl.IonTypeChanged -= skylineWindow.IonTypeSelector_IonTypeChanges;
                    ionTypeSelector.HostedControl.LossChanged -= skylineWindow.IonTypeSelector_LossChanged;
                }
            }

            if (msGraphExtension.PropertiesSheet.PropertySort == PropertySort.Alphabetical)
                Settings.Default.ViewLibraryPropertiesSorted = true;
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
                _userSelectedSpectrum = comboSpectrum.SelectedItem.ToString();
                _updateManager.QueueUpdate(true);
            }
        }

        private void comboMirrorSpectrum_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_inToolbarUpdate)
            {
                _userSelectedMirrorSpectrum = comboMirrorSpectrum.SelectedItem.ToString();
                UpdateUI();
            }
        }

        private void comboCE_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Default.KoinaNCE = (int) comboCE.SelectedItem;
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
            var chromPeak = new ChromPeak(crawPeakFinder, crawdadPeak, 0, timeIntensities, null, null);
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
                null, // maxPeakScore
                chromGroup.Times.Length, // numPoints
                0, // compressedSize
                0, // uncompressedsize
                0,  //location
                0, null, null, chromGroup.CCS, ionMobilityFilter.IonMobilityUnits);
            var groupInfo = new ChromatogramGroupInfo(header,null,
                new[]
                {
                    new ChromTransition(chromData.Mz, 0,
                        (float) (ionMobilityFilter.IonMobilityAndCCS.IonMobility.Mobility ?? 0),
                        (float) (ionMobilityFilter.IonMobilityExtractionWindowWidth ?? 0),
                        ChromSource.unknown, transitionChromInfo.OptimizationStep),
                }, peaks, TimeIntensitiesGroup.Singleton(timeIntensities));
            chromatogramInfo = new ChromatogramInfo(groupInfo, 0);
        }

        public void DisplayTooltip(MouseEventArgs e)
        {
            LibraryRankedSpectrumInfo.RankedMI peakRmi = null;
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
            {
                // Check if the mouse is over a label
                if (GraphPane.FindNearestObject(e.Location, g, out var nearestObject, out var index) && nearestObject is TextObj label)
                {
                    var screenRect = GraphPane.GetRectScreen(label, g);
                    GraphPane.FindClosestCurve(GraphPane.CurveList, e.Location, (int)(screenRect.Width + screenRect.Height) , out var curve, out var closestPoint);
                    if (curve.Tag is SpectrumGraphItem graphItem)
                    {
                        peakRmi = graphItem.SpectrumInfo.PeaksMatched.FirstOrDefault(rmi => graphItem.GetLabel(rmi).Equals(label.Text));
                    }
                }
            }
            // Check if the mouse is over a stick
            if (GraphPane.FindNearestStick(e.Location, out var nearestCurve, out var nearestIndex))
            {
                if (nearestCurve != null)
                {
                    if (nearestIndex >= 0 && nearestIndex < nearestCurve.NPts)
                    {
                        var hasNegativePeaks = Enumerable.Range(0, nearestCurve.NPts)
                            .Select(i => nearestCurve.Points[i]).Any(pt => pt.Y < 0);
                        SpectrumGraphItem gItem = null;
                        if (hasNegativePeaks)
                            gItem = MirrorGraphItem;
                        else
                            gItem = GraphItem;
                        // nearestIndex is in graph points. Need to convert it into the ranked spectrum index
                        var spectrumIndex = Enumerable.Range(0, gItem.SpectrumInfo.Peaks.Count).FirstOrDefault(i =>
                            gItem.SpectrumInfo.Peaks[i].ObservedMz == nearestCurve.Points[nearestIndex].X);
                        peakRmi = gItem.SpectrumInfo.Peaks[spectrumIndex];
                    }
                }
            }

            if (peakRmi != null)
            {
                if (_toolTip == null)
                    _toolTip = new NodeTip(this) { Parent = graphControl };
                _toolTip.SetTipProvider(new ToolTipImplementation(peakRmi), new Rectangle(e.Location, new Size()), e.Location);
                return;
            }
            _toolTip?.HideTip();
            _toolTip = null;
            graphControl.Invalidate();
        }

        public void GraphControl_MouseMove(object sender, MouseEventArgs e)
        {
            DisplayTooltip(e);
        }
        public Rectangle RectToScreen(Rectangle r)
        {
            return RectangleToScreen(r);
        }

        public Rectangle ScreenRect => Screen.GetBounds(this);

        public bool AllowDisplayTip { get; private set; }


        #region Test support

        public ToolStripButton PropertyButton => propertiesButton;
        public MsGraphExtension MsGraphExtension => msGraphExtension;
        public ToolStripComboBox SpectrumCombo => comboSpectrum;
        public NodeTip ToolTip => _toolTip;

        #endregion

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

        public Dictionary<IonType, bool> GetShowIonTypeSettings()
        {
            return new Dictionary<IonType, bool>(){{IonType.a, ShowAIons}, { IonType.b, ShowBIons }, { IonType.c, ShowCIons },
                { IonType.x, ShowXIons }, { IonType.y, ShowYIons }, { IonType.z, ShowZIons },{IonType.zh, ShowZHIons}, {IonType.zhh, ShowZHHIons} };
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
        public bool ShowSpecialIons
        {
            get { return Set.ShowSpecialIons; }
            set { ActAndUpdate(() => Set.ShowSpecialIons = value); }
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

        public ICollection<string> ShowLosses
        {
            get
            {
                return Set.ShowLosses.IsNullOrEmpty() ? new string[] {} : Set.ShowLosses.Split(',');
            }
            set
            {
                ActAndUpdate(() => Set.ShowLosses = value.ToList().ToString(@","));
            }
        }

        public bool Koina
        {
            get { return Set.Koina; }
            set { ActAndUpdate(() => Set.Koina = value);}
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
                AddItem(types, IonType.custom, Set.ShowSpecialIons);
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

    // SpectrumDisplayInfo moved to pwiz.Skyline.Model.Lib.SpectrumDisplayInfo

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
