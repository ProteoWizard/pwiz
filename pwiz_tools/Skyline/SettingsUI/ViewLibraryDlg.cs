/*
 * Original author: Tahmina Jahan <tabaker .at. u.washington.edu>,
 *                  Alana Killeen <killea .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.MSGraph;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib.Midas;
using pwiz.Skyline.Model.Proteome;
using ZedGraph;
using pwiz.Skyline.Util.Extensions;
using Label = System.Windows.Forms.Label;


namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// Dialog to view the contents of the libraries in the Peptide Settings 
    /// dialog's Library tab. It allows you to select one of the libraries 
    /// from a drop-down, view and search the list of peptides, and view the
    /// spectrum for peptide selected in the list.
    /// </summary>
    public partial class ViewLibraryDlg : FormEx, IGraphContainer, ITipDisplayer
    {
        // Used to parse the modification string in a given sequence
        private const string COLON_SEP = ": ";  // Not L10N

        protected internal const int PADDING = 3;
        private const TextFormatFlags FORMAT_PLAIN = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
        private const TextFormatFlags FORMAT_CUSTOM = FORMAT_PLAIN | TextFormatFlags.NoPadding;
        
        private readonly LibraryManager _libraryManager;
        private bool _libraryListChanged;
        private string _selectedLibName;
        private Library _selectedLibrary;
        private LibrarySpec _selectedSpec;
        private readonly PageInfo _pageInfo;
        private readonly IDocumentUIContainer _documentUiContainer;
        private SrmDocument Document { get { return _documentUiContainer.Document; }  }
        private readonly Bitmap _peptideImg;
        private readonly Bitmap _moleculeImg;
        public ViewLibSpectrumGraphItem GraphItem { get; private set; }
        public GraphSpectrumSettings GraphSettings { get; private set; }
        public int LineWidth { get; set; }
        public float FontSize { get; set; }

        private readonly SettingsListComboDriver<LibrarySpec> _driverLibraries;

        private ViewLibraryPepInfoList _peptides;
        private IList<int> _currentRange;

        private LibKeyModificationMatcher _matcher;

        private bool _activated;

        private readonly NodeTip _nodeTip;
        private readonly MoveThreshold _moveThreshold = new MoveThreshold(5, 5);
        private ViewLibraryPepInfo _lastTipNode;
        private ITipProvider _lastTipProvider;
        private bool _showChromatograms;
        private readonly GraphHelper _graphHelper;

        private ModFontHolder ModFonts { get; set; }

        private string SelectedLibraryName
        {
            get
            {
                if (!string.IsNullOrEmpty(_selectedLibName))
                    return _selectedLibName;
                if (comboLibrary.Items.Count > 0)
                    return _selectedLibName = (string)comboLibrary.Items[0];
                return null;
            }
        }

        public int PeptidesCount { get { return _peptides.Count(); } }

        public bool HasSmallMolecules { get; private set; }
        public bool HasPeptides { get; private set;  }

        public int PeptideDisplayCount { get { return listPeptide.Items.Count; } }

        /// <summary>
        /// Constructor for the View Library dialog.
        /// </summary>
        /// <param name="libMgr"> A library manager is needed to load the
        /// chosen peptide library. </param>
        /// <param name="libName"> Name of the library to select. </param>
        /// <param name="documentContainer"> The current document. </param>
        public ViewLibraryDlg(LibraryManager libMgr, String libName, IDocumentUIContainer documentContainer)
        {
            InitializeComponent();
            _graphHelper = GraphHelper.Attach(graphControl);
            _libraryManager = libMgr;
            _selectedLibName = libName;
            if (string.IsNullOrEmpty(_selectedLibName) && Settings.Default.SpectralLibraryList.Count > 0)
                _selectedLibName = Settings.Default.SpectralLibraryList[0].Name;

            _pageInfo = new PageInfo(100, 0, new IndexedSubList<ViewLibraryPepInfo>(_peptides, _currentRange));
            _documentUiContainer = documentContainer;
            _documentUiContainer.ListenUI(OnDocumentChange);

            _driverLibraries = new SettingsListComboDriver<LibrarySpec>(comboLibrary, Settings.Default.SpectralLibraryList);
            Settings.Default.SpectralLibraryList.ListChanged += SpectralLibraryList_ListChanged;

            GraphSettings = new GraphSpectrumSettings(UpdateUI);

            Icon = Resources.Skyline;
            ModFonts = new ModFontHolder(listPeptide);
            _peptideImg = Resources.PeptideLib;
            _moleculeImg = Resources.MoleculeLib;

            // Tip for peptides in list
            _nodeTip = new NodeTip(this) {Parent = this};

            // Restore window placement.
            Size size = Settings.Default.ViewLibrarySize;
            if (!size.IsEmpty)
                Size = size;
            Point location = Settings.Default.ViewLibraryLocation;
            if (!location.IsEmpty)
            {
                StartPosition = FormStartPosition.Manual;

                // Make sure the window is entirely on screen
                Location = location;
                ForceOnScreen();
            }

            _matcher = new LibKeyModificationMatcher();
            _showChromatograms = Settings.Default.ShowLibraryChromatograms;
        }

        private void SpectralLibraryList_ListChanged(object sender, EventArgs e)
        {
            // Not necessary to remember this change, if it is the combo box doing the changing.
            _libraryListChanged = !_driverLibraries.IsInSelectedIndexChangedEvent;
        }

        private void graphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip,
            Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            BuildSpectrumMenu(sender, menuStrip);
        }

        /// <summary>
        /// Gets names of libraries in the Peptide Settings --> Library tab,
        /// displays them in the Library combobox, and selects the first one
        /// as the default library.
        /// </summary>
        private void InitializeLibrariesComboBox()
        {
            _driverLibraries.LoadList(_selectedLibName);
        }

        public bool AssociateMatchingProteins
        {
            get { return cbAssociateProteins.Checked; }
            set { cbAssociateProteins.Checked = value; }
        }

        public void ChangeSelectedPeptide(string text)
        {
            foreach(ViewLibraryPepInfo pepInfo in listPeptide.Items)
            {
                if (pepInfo.AnnotatedDisplayText.Contains(text))
                {
                    listPeptide.SelectedItem = pepInfo;
                    return;
                }
            }
        }

        #region Dialog Events

        private void ViewLibraryDlg_Load(object sender, EventArgs e)
        {
            // The combobox is the control that tells us which peptide library
            // we need to load, so as soon as the dialog loads, we need to
            // populate it and set a default selection for the library.
            InitializeLibrariesComboBox();
            UpdateViewLibraryDlg();
        }

        private void ViewLibraryDlg_Shown(object sender, EventArgs e)
        {
            textPeptide.Focus();
            if (_selectedLibrary is MidasLibrary || MatchModifications())
                UpdateListPeptide(0);
        }

        private void ViewLibraryDlg_Activated(object sender, EventArgs e)
        {
            // Used to set correct highlight color for selected item in the listPeptide.
            _activated = true;
            if (listPeptide.SelectedIndex != -1)
                listPeptide.Invalidate(listPeptide.GetItemRectangle(listPeptide.SelectedIndex));

            // Check to see if the library list has changed.
            if (_libraryListChanged)
            {
                _libraryListChanged = false;
                // If the current library spec is no longer available in the global list of library specs, 
                // ask if user wants to reload the explorer. Otherwise, simply update the LibrariesComboBox.
                if (Settings.Default.SpectralLibraryList.Contains(_selectedSpec))
                {
                    InitializeLibrariesComboBox();
                }
                else
                {
                    Program.MainWindow.FocusDocument();
                    var result = MultiButtonMsgDlg.Show(
                        this,
                        string.Format(Resources.ViewLibraryDlg_ViewLibraryDlg_Activated_The_library__0__is_no_longer_available_in_the_Skyline_settings__Reload_the_library_explorer_, _selectedLibName),
                        Resources.ViewLibraryDlg_MatchModifications_Yes, Resources.ViewLibraryDlg_MatchModifications_No, true);
                    if (result == DialogResult.Yes)
                    {
                        if (Settings.Default.SpectralLibraryList.Count == 0)
                        {
                            MessageDlg.Show(this, Resources.ViewLibraryDlg_ViewLibraryDlg_Activated_There_are_no_libraries_in_the_current_settings);
                            Close();
                            return;
                        }
                        InitializeLibrariesComboBox();
                        comboLibrary.SelectedIndex = 0;
                        Activate();
                    }
                }
            }
        }

        private void ViewLibraryDlg_Deactivate(object sender, EventArgs e)
        {
            _activated = false;
            if (listPeptide.SelectedIndex != -1)
                listPeptide.Invalidate(listPeptide.GetItemRectangle(listPeptide.SelectedIndex));
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_selectedLibrary != null && _selectedLibrary.ReadStream != null)
                _selectedLibrary.ReadStream.CloseStream();
            _documentUiContainer.UnlistenUI(OnDocumentChange);

            Settings.Default.SpectralLibraryList.ListChanged -= SpectralLibraryList_ListChanged;

            base.OnHandleDestroyed(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            Settings.Default.ViewLibraryLocation = Location;
            Settings.Default.ViewLibrarySize = Size;

            base.OnClosing(e);
        }

        #endregion

        #region Update ViewLibraryDlg

        private void UpdateViewLibraryDlg()
        {
            // Order matters!!
            LoadLibrary();
            InitializePeptides();
            _currentRange = new RangeList(0, _peptides.Count);
            UpdatePageInfo();
            UpdateStatusArea();
            cbShowModMasses.Checked = Settings.Default.ShowModMassesInExplorer;
            UpdateListPeptide(0);
            textPeptide.Select();
            UpdateUI();
        }

        /// <summary>
        /// Loads the library selected in the Library combobox. First, we check
        /// to see if the library is already loaded in the main window. If yes,
        /// great, we can just use that. If no, we need to use the LongWaitDlg
        /// as the IProgressMonitor to load the library from the LibrarySpec.
        /// </summary>
        private void LoadLibrary()
        {
            LibrarySpec selectedLibrarySpec = _selectedSpec = _driverLibraries.SelectedItem;
            if (_selectedLibrary != null && _selectedLibrary.ReadStream != null)
                _selectedLibrary.ReadStream.CloseStream();
            _selectedLibrary = selectedLibrarySpec != null ? _libraryManager.TryGetLibrary(selectedLibrarySpec) : null;
            if (_selectedLibrary == null)
            {
                btnAddAll.Enabled = false;
                btnAdd.Enabled = false;
                if (selectedLibrarySpec == null)
                    return;

                using (var longWait = new LongWaitDlg { Text = Resources.ViewLibraryDlg_LoadLibrary_Loading_Library })
                {
                    try
                    {
                        var status = longWait.PerformWork(this, 800, monitor =>
                        {
                            _selectedLibrary = selectedLibrarySpec.LoadLibrary(new DefaultFileLoadMonitor(monitor));
                        });
                        if (status.IsError)
                        {
                            MessageDlg.ShowException(this, status.ErrorException);
                            return;
                        }
                    }
                    catch (Exception x)
                    {
                        var message = TextUtil.LineSeparate(string.Format(Resources.ViewLibraryDlg_LoadLibrary_An_error_occurred_attempting_to_import_the__0__library, selectedLibrarySpec.Name),
                                        x.Message);
                        MessageDlg.Show(this, message);
                    }
                }
            }
            btnAddAll.Enabled = true;
            btnAdd.Enabled = true;
        }

        /// <summary>
        /// Loads the entire list of peptides from the library selected into an
        /// array of PeptideInfo objects and sorts them alphabetically, with a
        /// few small quirks related to the modification. Please see comments
        /// for the PeptideInfoComparer above.
        /// </summary>
        private void InitializePeptides()
        {
            IEnumerable<ViewLibraryPepInfo> pepInfos;
            if (_selectedLibrary == null)
            {
                pepInfos = new ViewLibraryPepInfo[0];
            }
            else
            {
                pepInfos = _selectedLibrary.Keys.Select(
                    key => new ViewLibraryPepInfo(key));
            }
            _peptides = new ViewLibraryPepInfoList(pepInfos);
            bool allPeptides = _peptides.All(key => key.Key.IsProteomicKey);
            MoleculeLabel.Left = PeptideLabel.Left;
            PeptideLabel.Visible = HasPeptides = allPeptides;
            MoleculeLabel.Visible = HasSmallMolecules = !allPeptides;
            _currentRange = _peptides.Filter(null);
        }

        public bool MatchModifications()
        {
            if (_selectedLibrary == null)
                return false;

            var matcher = new LibKeyModificationMatcher();
            matcher.CreateMatches(Document.Settings, _selectedLibrary.Keys, Settings.Default.StaticModList, Settings.Default.HeavyModList);
            if (string.IsNullOrEmpty(matcher.FoundMatches) && !matcher.UnmatchedSequences.Any())
            {
                _matcher = matcher;
                return true;
            }

            using (var addModificationsDlg = new AddModificationsDlg(Document.Settings, _selectedLibrary))
            {
                if ((addModificationsDlg.NumMatched == 0 && addModificationsDlg.NumUnmatched == 0) || addModificationsDlg.ShowDialog(this) != DialogResult.OK)
                {
                    _matcher.ClearMatches();
                    return false;
                }
                _matcher = addModificationsDlg.Matcher;
                if (addModificationsDlg.NewDocumentModsStatic.Any() || addModificationsDlg.NewDocumentModsHeavy.Any())
                {
                    Program.MainWindow.ModifyDocument(Resources.ViewLibraryDlg_MatchModifications_Add_modifications, doc =>
                    {
                        var mods = doc.Settings.PeptideSettings.Modifications;
                        mods = mods.ChangeStaticModifications(mods.StaticModifications.Concat(addModificationsDlg.NewDocumentModsStatic).ToList())
                                   .AddHeavyModifications(addModificationsDlg.NewDocumentModsHeavy);
                        doc = doc.ChangeSettings(doc.Settings.ChangePeptideSettings(doc.Settings.PeptideSettings.ChangeModifications(mods)));
                        doc.Settings.UpdateDefaultModifications(false);
                        return doc;
                    }, SkylineWindow.SettingsLogFunction);
                }
                return true;
            }
        }

        /// <summary>
        /// Used to update page info when something changes: e.g. library
        /// selection changes, or search results change.
        /// </summary>
        private void UpdatePageInfo()
        {
            // We need to update the number of total items
            _pageInfo.ItemIndexRange = new IndexedSubList<ViewLibraryPepInfo>(_peptides, _currentRange);

            // Restart on page 1 (or 0 if we have no items)
            PreviousLink.Enabled = false;
            _pageInfo.Page = 0;
            if (_pageInfo.Items > 0)
            {
                _pageInfo.Page = 1;
            }

            // Enable the Next link if we have more than one page
            NextLink.Enabled = _pageInfo.Items > _pageInfo.PageSize;
        }

        /// <summary>
        /// Updates the status area showing which peptides are being shown.
        /// </summary>
        private void UpdateStatusArea()
        {
            int showStart = 0;
            int showEnd = 0;
            if (_currentRange.Count > 0)
            {
                showStart = _pageInfo.StartIndex + 1;
                showEnd = _pageInfo.EndIndex;
            }

            const string numFormat = "#,0"; // Not L10N
            var peptideCountFormat = HasSmallMolecules
                ? Resources.ViewLibraryDlg_UpdateStatusArea_Molecules__0__through__1__of__2__total_
                : Resources.ViewLibraryDlg_UpdateStatusArea_Peptides__0__through__1__of__2__total;

            PeptideCount.Text =
                string.Format(peptideCountFormat,
                              showStart.ToString(numFormat),
                              showEnd.ToString(numFormat),
                                _peptides.Count.ToString(numFormat));

            PageCount.Text = string.Format(Resources.ViewLibraryDlg_UpdateStatusArea_Page__0__of__1__, _pageInfo.Page,
                                           _pageInfo.Pages);
        }

        /// <summary>
        /// Used to update the peptides list when something changes: e.g. 
        /// library selection changes, or search results change.
        /// </summary>
        private void UpdateListPeptide(int selectPeptideIndex)
        {
            var pepMatcher = new ViewLibraryPepMatching(Document,
                _selectedLibrary, _selectedSpec, _matcher, _peptides);
            listPeptide.BeginUpdate();
            listPeptide.Items.Clear();
            if (_currentRange.Count > 0)
            {
                try
                {
                    int start = _pageInfo.StartIndex;
                    int end = _pageInfo.EndIndex;
                    new LongOperationRunner
                    {
                        JobTitle = Resources.ViewLibraryDlg_UpdateListPeptide_Updating_list_of_peptides
                    }.Run(longWaitBroker =>
                    {
                        for (int i = start; i < end; i++)
                        {
                            if (IsUpdateCanceled)   // Allows tests to get out of this loop and fail
                                break;
                            longWaitBroker.SetProgressCheckCancel(i - start, end - start);
                            ViewLibraryPepInfo pepInfo = _peptides[_currentRange[i]];
                            var adduct = pepInfo.Key.Adduct;
                            var diff = new SrmSettingsDiff(true, false, true, false, false, false);
                            listPeptide.Items.Add(pepMatcher.AssociateMatchingPeptide(pepInfo, adduct, diff));
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                }

                listPeptide.SelectedIndex = Math.Min(selectPeptideIndex, listPeptide.Items.Count - 1);
            }
            listPeptide.EndUpdate();
            IsUpdateComplete = true;
        }

        public static void GetPeptideInfo(ViewLibraryPepInfo pepInfo, 
                                        LibKeyModificationMatcher matcher, 
                                        out SrmSettings settings, out TransitionGroupDocNode transitionGroup, out ExplicitMods mods)
        {
            settings = Program.ActiveDocumentUI.Settings;

            if (matcher.HasMatches)
                settings = settings.ChangePeptideModifications(modifications => matcher.MatcherPepMods);

//            var pepInfo = (ViewLibraryPepInfo)lPeptide.SelectedItem;
            var nodePep = pepInfo.PeptideNode;
            if (nodePep != null)
            {
                mods = nodePep.ExplicitMods;
                // Should always be just one child.  The child that matched this spectrum.
                transitionGroup = nodePep.TransitionGroups.First();
            }
            else if (null != pepInfo.Key.LibraryKey.Target)
            {
                var peptide = new Peptide(pepInfo.Key.LibraryKey.StripModifications().Target);
                transitionGroup = new TransitionGroupDocNode(new TransitionGroup(peptide, pepInfo.Adduct,
                                                      IsotopeLabelType.light, true, null), null);
                if (pepInfo.Key.IsSmallMoleculeKey)
                {
                    mods = null;
                    return;
                }

                // Because the document modifications do not explain this peptide, a set of
                // explicit modifications must be constructed, even if they are empty.
                IList<ExplicitMod> staticModList = new List<ExplicitMod>();
                IEnumerable<ModificationInfo> modList = GetModifications(pepInfo);
                foreach (var modInfo in modList)
                {
                    var smod = new StaticMod("temp", // Not L10N
                                             modInfo.ModifiedAminoAcid.ToString(CultureInfo.InvariantCulture),
                                             null,
                                             null,
                                             LabelAtoms.None,
                                             modInfo.ModifiedMass,
                                             modInfo.ModifiedMass);
                    var exmod = new ExplicitMod(modInfo.IndexMod, smod);
                    staticModList.Add(exmod);
                }

                mods = new ExplicitMods(peptide, staticModList, new TypedExplicitModifications[0]);
            }
            else
            {
                // Create custom ion node for midas library
                var precursor = pepInfo.Key.PrecursorMz.GetValueOrDefault();
                var precursorMono = new TypedMass(precursor, MassType.Monoisotopic);
                var precursorAverage = new TypedMass(precursor, MassType.Average);
                var peptide = new Peptide(new CustomMolecule(precursorMono, precursorAverage, precursor.ToString(CultureInfo.CurrentCulture)));
                transitionGroup = new TransitionGroupDocNode(new TransitionGroup(peptide, Adduct.EMPTY, IsotopeLabelType.light, true, null), null);
                mods = new ExplicitMods(peptide, new ExplicitMod[0], new TypedExplicitModifications[0]);
            }
        }

        /// <summary>
        /// Updates the spectrum graph using the currently selected peptide.
        /// </summary>
        public void UpdateUI(bool selectionChanged = true)
        {
            // Only worry about updates, if the graph is visible
            // And make sure it is not disposed, since rendering happens on a timer
            if (!Visible || IsDisposed)
                return;

            // Clear existing data from the graph pane
            var graphPane = (MSGraphPane)graphControl.MasterPane[0];
            graphPane.CurveList.Clear();
            graphPane.GraphObjList.Clear();

            labelRT.Text = string.Empty;
            labelFilename.Text = string.Empty;

            // Check for appropriate spectrum to load
            bool available = false;
            try
            {
                int index = GetIndexOfSelectedPeptide();
                bool isSequenceLibrary = !(_selectedLibrary is MidasLibrary);
                bool isSmallMoleculeItem = -1 != index && _peptides[index].Key.IsSmallMoleculeKey;
                if (isSequenceLibrary)
                {
                    btnAdd.Enabled = -1 != index;
                    cbAssociateProteins.Enabled = !isSmallMoleculeItem;
                    cbAssociateProteins.Visible = HasPeptides; // Don't show the control at all if library is all small mol
                }
                else
                {
                    btnAdd.Enabled = btnAddAll.Enabled = cbAssociateProteins.Enabled = false;
                }

                btnAIons.Enabled = btnAIons.Visible =
                    btnBIons.Enabled = btnBIons.Visible =
                    btnCIons.Enabled = btnCIons.Visible =
                    btnXIons.Enabled = btnXIons.Visible =
                    btnYIons.Enabled = btnYIons.Visible =
                    btnZIons.Enabled = btnZIons.Visible =
                    aionsContextMenuItem.Enabled = aionsContextMenuItem.Visible =
                    bionsContextMenuItem.Enabled = bionsContextMenuItem.Visible =
                    cionsContextMenuItem.Enabled = cionsContextMenuItem.Visible =
                    xionsContextMenuItem.Enabled = xionsContextMenuItem.Visible =
                    yionsContextMenuItem.Enabled = yionsContextMenuItem.Visible =
                    zionsContextMenuItem.Enabled = zionsContextMenuItem.Visible
                    = isSequenceLibrary && !isSmallMoleculeItem;
                btnFragmentIons.Enabled = btnFragmentIons.Visible =
                    fragmentionsContextMenuItem.Enabled = fragmentionsContextMenuItem.Visible =
                    isSequenceLibrary && isSmallMoleculeItem;
                precursorIonContextMenuItem.Enabled = chargesContextMenuItem.Enabled
                    = toolStripSeparator1.Enabled = toolStripSeparator2.Enabled = charge1Button.Enabled = charge2Button.Enabled
                    = toolStripSeparator11.Enabled = toolStripSeparator12.Enabled = toolStripSeparator13.Enabled
                    = ranksContextMenuItem.Enabled = ionMzValuesContextMenuItem.Enabled
                    = observedMzValuesContextMenuItem.Enabled = duplicatesContextMenuItem.Enabled
                    = isSequenceLibrary;


                if (-1 != index)
                {
                    SpectrumPeaksInfo spectrum;
                    if (_selectedLibrary.TryLoadSpectrum(_peptides[index].Key, out spectrum))
                    {
                        SrmSettings settings = Program.ActiveDocumentUI.Settings;

                        if (_matcher.HasMatches)
                            settings = settings.ChangePeptideModifications(modifications => _matcher.MatcherPepMods);

                        TransitionGroupDocNode transitionGroupDocNode;

                        var types = ShowIonTypes(!isSmallMoleculeItem);
                        var rankTypes = isSmallMoleculeItem ?
                            settings.TransitionSettings.Filter.SmallMoleculeIonTypes :
                            settings.TransitionSettings.Filter.PeptideIonTypes;
                        var rankCharges = isSmallMoleculeItem ?
                            settings.TransitionSettings.Filter.SmallMoleculeFragmentAdducts :
                            settings.TransitionSettings.Filter.PeptideProductCharges;

                        ExplicitMods mods;
                        var pepInfo = (ViewLibraryPepInfo)listPeptide.SelectedItem;
                        GetPeptideInfo(pepInfo, _matcher, out settings, out transitionGroupDocNode, out mods);
                        var showAdducts = (isSmallMoleculeItem ?  transitionGroupDocNode.InUseAdducts : Transition.DEFAULT_PEPTIDE_LIBRARY_CHARGES).ToList();
                        var charges = ShowIonCharges(showAdducts);

                        // Make sure the types and charges in the settings are at the head
                        // of these lists to give them top priority, and get rankings correct.
                        int i = 0;
                        foreach (IonType type in rankTypes)
                        {
                            if (types.Remove(type))
                                types.Insert(i++, type);
                        }
                        i = 0;
                        var adducts = new List<Adduct>();
                        var absoluteChargeValues = Adduct.OrderedAbsoluteChargeValues(rankCharges);
                        foreach (var charge in absoluteChargeValues)
                        {
                            if (charges.Remove(charge))
                                charges.Insert(i++, charge);
                            adducts.AddRange(showAdducts.Where(a => charge == Math.Abs(a.AdductCharge)));
                        }
                        adducts.AddRange(showAdducts.Where(a => charges.Contains(Math.Abs(a.AdductCharge)) && !adducts.Contains(a))); // And the unranked charges as well

                        var spectrumInfoR = new LibraryRankedSpectrumInfo(spectrum,
                                                                          transitionGroupDocNode.TransitionGroup.LabelType,
                                                                          transitionGroupDocNode,
                                                                          settings,
                                                                          transitionGroupDocNode.Peptide.Target,
                                                                          mods,
                                                                          adducts,
                                                                          types,
                                                                          rankCharges,
                                                                          rankTypes);
                        LibraryChromGroup libraryChromGroup = null;
                        if (_showChromatograms)
                        {
                            var spectrumInfo = _selectedLibrary.GetSpectra(_peptides[index].Key, null, LibraryRedundancy.best).FirstOrDefault();
                            if (null != spectrumInfo)
                            {
                                libraryChromGroup = _selectedLibrary.LoadChromatogramData(spectrumInfo.SpectrumKey);
                            }
                        }
                        GraphItem = new ViewLibSpectrumGraphItem(spectrumInfoR, transitionGroupDocNode.TransitionGroup, _selectedLibrary, pepInfo.Key)
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
                        // Update file and retention time indicators
                        var bestSpectrum = _selectedLibrary.GetSpectra(_peptides[index].Key,
                            IsotopeLabelType.light, LibraryRedundancy.best).FirstOrDefault();
                        if (bestSpectrum != null)
                        {
                            double? rt = bestSpectrum.RetentionTime;
                            string filename = bestSpectrum.FileName;

                            if (!string.IsNullOrEmpty(filename))
                            {
                                labelFilename.Text = Resources.ViewLibraryDlg_UpdateUI_File + COLON_SEP + filename;
                            }
                            if (rt.HasValue)
                            {
                                labelRT.Text = Resources.ViewLibraryDlg_UpdateUI_RT + COLON_SEP + rt;
                            }
                            IonMobilityAndCCS dt = bestSpectrum.IonMobilityInfo;
                            if (dt != null && !dt.IsEmpty)
                            {
                                var dtText = string.Empty;
                                if (dt.HasCollisionalCrossSection)
                                    dtText = "CCS" + COLON_SEP + string.Format("{0:F4} ", dt.CollisionalCrossSectionSqA.Value); // Not L10N
                                if (dt.HasIonMobilityValue)
                                    dtText += "IM" + COLON_SEP + string.Format("{0:F4}{1}", dt.IonMobility.Mobility, dt.IonMobility.UnitsString); // Not L10N
                                if (dt.HighEnergyIonMobilityValueOffset != 0) // Show the high energy value (as in Waters MSe) if different
                                    dtText += String.Format("({0:F4})", dt.HighEnergyIonMobilityValueOffset); // Not L10N
                                labelRT.Text = TextUtil.SpaceSeparate(labelRT.Text, dtText);
                            }
                        }
                        if (null == libraryChromGroup)
                        {
                            _graphHelper.ResetForSpectrum(null);
                            _graphHelper.AddSpectrum(GraphItem);
                            _graphHelper.ZoomSpectrumToSettings(Program.ActiveDocumentUI, null);
                        }
                        else
                        {
                            _graphHelper.ResetForChromatograms(new[]{transitionGroupDocNode.TransitionGroup});
                            double maxHeight = libraryChromGroup.ChromDatas.Max(chromData => chromData.Height);
                            int iChromDataPrimary = libraryChromGroup.ChromDatas.IndexOf(chromData => maxHeight == chromData.Height);
                            for (int iChromData = 0; iChromData < libraryChromGroup.ChromDatas.Count; iChromData++)
                            {
                                ChromatogramInfo chromatogramInfo;
                                TransitionChromInfo transitionChromInfo;
                                var chromData = libraryChromGroup.ChromDatas[iChromData];
                                GraphSpectrum.MakeChromatogramInfo(SignedMz.ZERO, libraryChromGroup, chromData, out chromatogramInfo, out transitionChromInfo);
                                var nodeGroup = new TransitionGroupDocNode(transitionGroupDocNode.TransitionGroup, new TransitionDocNode[0]);
                                var color =
                                    GraphChromatogram.COLORS_LIBRARY[iChromData%GraphChromatogram.COLORS_LIBRARY.Count];
                                var graphItem = new ChromGraphItem(nodeGroup, null, chromatogramInfo, iChromData == iChromDataPrimary ? transitionChromInfo : null, null,
                                   new[] { iChromData == iChromDataPrimary }, null, 0, false, false, null, 0,
                                   color, Settings.Default.ChromatogramFontSize, 1);
                                LineItem curve = (LineItem) _graphHelper.AddChromatogram(PaneKey.DEFAULT, graphItem);
                                var pointAnnotation = GraphItem.AnnotatePoint(new PointPair(chromData.Mz, 1.0));
                                if (null != pointAnnotation)
                                {
                                    curve.Label.Text = pointAnnotation.Label;
                                }
                                else
                                {
                                    curve.Label.Text = chromData.Mz.ToString("0.####"); // Not L10N? international # formats
                                }
                                curve.Line.Width = Settings.Default.ChromatogramLineWidth;
                                curve.Color = color;
                            }
                            _graphHelper.FinishedAddingChromatograms(libraryChromGroup.StartTime, libraryChromGroup.EndTime, false);
                        }

                        available = true;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                SetGraphItem(new NoDataMSGraphItem(Resources.ViewLibraryDlg_UpdateUI_Unauthorized_access_attempting_to_read_from_library_));
                return;
            }
            catch (IOException)
            {
                SetGraphItem(new NoDataMSGraphItem(Resources.ViewLibraryDlg_UpdateUI_Failure_loading_spectrum_Library_may_be_corrupted));
                return;
            }

            // Show unavailable message, if no spectrum loaded
            if (!available)
            {
                SetGraphItem(new UnavailableMSGraphItem());
            }

            btnAIons.Checked = btnAIons.Enabled && Settings.Default.ShowAIons;
            btnBIons.Checked = btnBIons.Enabled && Settings.Default.ShowBIons;
            btnCIons.Checked = btnCIons.Enabled && Settings.Default.ShowCIons;
            btnXIons.Checked = btnXIons.Enabled && Settings.Default.ShowXIons;
            btnYIons.Checked = btnYIons.Enabled && Settings.Default.ShowYIons;
            btnZIons.Checked = btnZIons.Enabled && Settings.Default.ShowZIons;
            btnFragmentIons.Checked = btnFragmentIons.Enabled && Settings.Default.ShowFragmentIons;
            charge1Button.Checked = charge1Button.Enabled && Settings.Default.ShowCharge1;
            charge2Button.Checked = charge2Button.Enabled && Settings.Default.ShowCharge2;
        }

        private void SetGraphItem(IMSGraphItemInfo item)
        {
            var curveItem = _graphHelper.SetErrorGraphItem(item);
            graphControl.GraphPane.Title.Text = item.Title;
            curveItem.Label.IsVisible = false;
            graphControl.Refresh();
        }

        /// <summary>
        /// Gets the index of the peptide in the peptides array for the selected
        /// peptide in the peptides listbox. Returns -1 if none is found.
        /// </summary>
        private int GetIndexOfSelectedPeptide()
        {
            var selPepInfo = listPeptide.SelectedItem as ViewLibraryPepInfo;
            if (selPepInfo == null)
            {
                return -1;
            }
            int indexInFullList = _peptides.IndexOf(selPepInfo.Key.LibraryKey);
            if (indexInFullList < 0)
            {
                return -1;
            }
            if (!_currentRange.Contains(indexInFullList))
            {
                return -1;
            }
            return indexInFullList;
        }

        #endregion

        #region IStateProvider Implementation

        public IList<IonType> ShowIonTypes(bool isProteomic)
        {
            return GraphSettings.ShowIonTypes(isProteomic); 
        }

        // N.B. we're interested in the absolute value of charge here, so output list may be shorter than input list
        // CONSIDER(bspratt): will we want finer grained (full adduct sense) control for small molecule libs?
        public IList<int> ShowIonCharges(IEnumerable<Adduct> chargePriority)
        {
            return GraphSettings.ShowIonCharges(chargePriority); 
        }

        public void BuildSpectrumMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip)
        {
            // Store original menuitems in an array, and insert a separator
            var items = new ToolStripItem[menuStrip.Items.Count];
            int iUnzoom = -1;
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = menuStrip.Items[i];
                var tag = (string)items[i].Tag;
                if (tag == "unzoom") // Not L10N
                    iUnzoom = i;
            }

            if (iUnzoom != -1)
                menuStrip.Items.Insert(iUnzoom, toolStripSeparator27);

            // Insert skyline specific menus
            var set = Settings.Default;
            int iInsert = 0;
            aionsContextMenuItem.Checked = set.ShowAIons;
            menuStrip.Items.Insert(iInsert++, aionsContextMenuItem);
            bionsContextMenuItem.Checked = set.ShowBIons;
            menuStrip.Items.Insert(iInsert++, bionsContextMenuItem);
            cionsContextMenuItem.Checked = set.ShowCIons;
            menuStrip.Items.Insert(iInsert++, cionsContextMenuItem);
            xionsContextMenuItem.Checked = set.ShowXIons;
            menuStrip.Items.Insert(iInsert++, xionsContextMenuItem);
            yionsContextMenuItem.Checked = set.ShowYIons;
            menuStrip.Items.Insert(iInsert++, yionsContextMenuItem);
            zionsContextMenuItem.Checked = set.ShowZIons;
            menuStrip.Items.Insert(iInsert++, zionsContextMenuItem);
            fragmentionsContextMenuItem.Checked = set.ShowFragmentIons;
            menuStrip.Items.Insert(iInsert++, fragmentionsContextMenuItem);
            precursorIonContextMenuItem.Checked = set.ShowPrecursorIon;
            menuStrip.Items.Insert(iInsert++, precursorIonContextMenuItem);
            menuStrip.Items.Insert(iInsert++, toolStripSeparator11);
            menuStrip.Items.Insert(iInsert++, chargesContextMenuItem);
            if (chargesContextMenuItem.DropDownItems.Count == 0)
            {
                chargesContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        charge1ContextMenuItem,
                        charge2ContextMenuItem,
                        charge3ContextMenuItem,
                        charge4ContextMenuItem,
                    });
            }
            menuStrip.Items.Insert(iInsert++, toolStripSeparator12);
            ranksContextMenuItem.Checked = set.ShowRanks;
            menuStrip.Items.Insert(iInsert++, ranksContextMenuItem);
            ionMzValuesContextMenuItem.Checked = set.ShowIonMz;
            menuStrip.Items.Insert(iInsert++, ionMzValuesContextMenuItem);
            observedMzValuesContextMenuItem.Checked = set.ShowObservedMz;
            menuStrip.Items.Insert(iInsert++, observedMzValuesContextMenuItem);
            duplicatesContextMenuItem.Checked = set.ShowDuplicateIons;
            menuStrip.Items.Insert(iInsert++, duplicatesContextMenuItem);
            menuStrip.Items.Insert(iInsert++, toolStripSeparator13);
            lockYaxisContextMenuItem.Checked = set.LockYAxis;
            menuStrip.Items.Insert(iInsert++, lockYaxisContextMenuItem);
            menuStrip.Items.Insert(iInsert++, toolStripSeparator14);
            menuStrip.Items.Insert(iInsert++, spectrumPropsContextMenuItem);
            showChromatogramsContextMenuItem.Checked = _showChromatograms;
            menuStrip.Items.Insert(iInsert++, showChromatogramsContextMenuItem);
            menuStrip.Items.Insert(iInsert, toolStripSeparator15);

            // Remove some ZedGraph menu items not of interest
            foreach (var item in items)
            {
                var tag = (string)item.Tag;
                if (tag == "set_default" || tag == "show_val") // Not L10N
                    menuStrip.Items.Remove(item);
            }
            CopyEmfToolStripMenuItem.AddToContextMenu(graphControl, menuStrip);
        }

        public void LockYAxis(bool lockY)
        {
            graphControl.IsEnableVPan = graphControl.IsEnableVZoom = !lockY;
            graphControl.Refresh();
        }

        #endregion

        #region Draw Peptide List Box

        private IEnumerable<TextSequence> GetTextSequences(DrawItemEventArgs e)
        {
            ViewLibraryPepInfo pepInfo = ((ViewLibraryPepInfo)listPeptide.Items[e.Index]);
            IList<TextSequence> textSequences = new List<TextSequence>();
            // If a matching peptide exists, use the text sequences for that peptide.
            if (pepInfo.HasPeptide)
            {
                var settings = Document.Settings;
                if (_matcher.HasMatches)
                    settings = settings.ChangePeptideModifications(mods => _matcher.MatcherPepMods);
                textSequences = PeptideTreeNode.CreateTextSequences(pepInfo.PeptideNode, settings,
                    pepInfo.GetPlainDisplayString(), null, new ModFontHolder(this));
            }
            // If no modifications, use a single plain test sequence
            else if (!pepInfo.IsModified)
            {
                textSequences.Add(CreateTextSequence(pepInfo.AnnotatedDisplayText, false));
            }
            // Otherwise bold-underline all modifications
            else
            {
                var sb = new StringBuilder(128);
                bool inMod = false;

                string sequence = pepInfo.AnnotatedDisplayText;
                for (int i = 0; i < sequence.Length; i++)
                {
                    bool isMod = i < sequence.Length - 1 && sequence[i + 1] == '[';
                    if (isMod != inMod)
                    {
                        textSequences.Add(CreateTextSequence(sb.ToString(), inMod));
                        sb.Remove(0, sb.Length);
                    }
                    sb.Append(sequence[i]);
                    inMod = isMod;
                    if (isMod)
                    {
                        i = sequence.IndexOf(']', i);
                        if (i == -1)
                            i = sequence.Length;
                    }
                }
                textSequences.Add(CreateTextSequence(sb.ToString(), inMod));
            }

            // Calculate placement for each text sequence
            int textRectWidth = 0;
            foreach (var textSequence in textSequences)
            {
                Size sizeMax = new Size(int.MaxValue, int.MaxValue);
                textSequence.Position = textRectWidth;
                textSequence.Width = TextRenderer.MeasureText(e.Graphics, textSequence.Text,
                    textSequence.Font, sizeMax, FORMAT_CUSTOM).Width;
                textRectWidth += textSequence.Width;
            }

            return textSequences;
        }
        
        private TextSequence CreateTextSequence(string text, bool modified)
        {
            var font = (modified ? ModFonts.Light : ModFonts.Plain);
            return new TextSequence { Text = text, Font = font, Color = Color.Black };
        }

        private void listPeptide_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index == -1)
                return;

            // Draw the background of the ListBox control for each item.
            bool selected = e.Index == listPeptide.SelectedIndex;
            Color backColor = !_activated && selected ? Color.LightGray : e.BackColor;
            e.Graphics.FillRectangle(new SolidBrush(backColor), e.Bounds);

            if (cbShowModMasses.Visible && Settings.Default.ShowModMassesInExplorer)
            {
                // Draw the current item text based on the current Font 
                // and a black brush.
                TextRenderer.DrawText(e.Graphics, ((ViewLibraryPepInfo)listPeptide.Items[e.Index]).AnnotatedDisplayText,
                                      e.Font, e.Bounds, e.ForeColor, backColor,
                                      FORMAT_PLAIN);
            }
            else
            {
                // Draw formatted text and the peptide image, if a peptide has been successfully
                // associated with this item
                ViewLibraryPepInfo pepInfo = ((ViewLibraryPepInfo)listPeptide.Items[e.Index]);
                Rectangle bounds = e.Bounds;
                var imgWidth = _peptideImg.Width;
                if (pepInfo.PeptideNode != null)
                    e.Graphics.DrawImage(pepInfo.PeptideNode.IsProteomic ? _peptideImg : _moleculeImg, bounds.Left, bounds.Top, imgWidth, bounds.Height);
                Rectangle rectDraw = new Rectangle(0, bounds.Y, 0, bounds.Height);
                foreach (var textSequence in GetTextSequences(e))
                {
                    rectDraw.X = textSequence.Position + bounds.X + PADDING + imgWidth;
                    rectDraw.Width = textSequence.Width;
                    var textColor = Equals(e.ForeColor, SystemColors.HighlightText) ? e.ForeColor : textSequence.Color;
                    TextRenderer.DrawText(e.Graphics, textSequence.Text,
                                          textSequence.Font, rectDraw, textColor, backColor,
                                          FORMAT_CUSTOM);
                }
            }
        }

        #endregion
        
        #region Change Events

        private void textPeptide_TextChanged(object sender, EventArgs e)
        {
            _currentRange = _peptides.Filter(textPeptide.Text);
            UpdatePageInfo();
            UpdateStatusArea();
            UpdateListPeptide(0);
            UpdateUI();
        }

        private void listPeptide_SelectedIndexChanged(object sender, EventArgs e)
        {
            // We need to update the spectrum graph when the peptide
            // selected in the listbox changes.
            UpdateUI();
        }

        private void LibraryComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_driverLibraries.SelectedIndexChangedEvent(sender, e))
                ChangeSelectedLibrary(comboLibrary.SelectedItem.ToString());
        }

        public void ChangeSelectedLibrary(string libName)
        {
            // The user has selected a different library. We need to reload 
            // everything in the dialog. 
            if (libName != _selectedLibName)
            {
                comboLibrary.SelectedItem = libName;
                _selectedLibName = libName;
                _matcher = new LibKeyModificationMatcher();
                UpdateViewLibraryDlg();

                _matcher.ClearMatches();
                if (_selectedLibrary is MidasLibrary || MatchModifications())
                    UpdateListPeptide(0);
            }
        }

        private void cbShowModMasses_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.ShowModMassesInExplorer = cbShowModMasses.Checked;
            int selPepIndex = listPeptide.SelectedIndex > 0 ? listPeptide.SelectedIndex : 0;
            UpdateListPeptide(selPepIndex);
        }

        private void OnDocumentChange(object sender, DocumentChangedEventArgs e)
        {
            var prevDocSet = e.DocumentPrevious.Settings;
            var curDocSet = Document.Settings;
            // Only need to update if ViewLibraryDlg is already loaded and transition settings have changed.
            if (_peptides != null
                && !Equals(prevDocSet.TransitionSettings, curDocSet.TransitionSettings))
                    UpdateListPeptide(listPeptide.SelectedIndex);
            if ( !Equals(prevDocSet.PeptideSettings.Modifications, curDocSet.PeptideSettings.Modifications))
            {
                if(_matcher.HasMatches)
                    _matcher.UpdateMatches(prevDocSet.PeptideSettings.Modifications, 
                        curDocSet.PeptideSettings.Modifications);
                UpdateListPeptide(listPeptide.SelectedIndex);
            }    
        }
        
        #endregion
        
        #region Mouse Click Events

        private void aionsContextMenuItem_Click(object sender, EventArgs e)
        {
            GraphSettings.ShowAIons = !GraphSettings.ShowAIons;
        }

        private void bionsContextMenuItem_Click(object sender, EventArgs e)
        {
            GraphSettings.ShowBIons = !GraphSettings.ShowBIons;
        }

        private void cionsContextMenuItem_Click(object sender, EventArgs e)
        {
            GraphSettings.ShowCIons = !GraphSettings.ShowCIons;
        }

        private void xionsContextMenuItem_Click(object sender, EventArgs e)
        {
            GraphSettings.ShowXIons = !GraphSettings.ShowXIons;
        }

        private void yionsContextMenuItem_Click(object sender, EventArgs e)
        {
            GraphSettings.ShowYIons = !GraphSettings.ShowYIons;
        }

        private void zionsContextMenuItem_Click(object sender, EventArgs e)
        {
            GraphSettings.ShowZIons = !GraphSettings.ShowZIons;
        }

        private void fragmentionsContextMenuItem_Click(object sender, EventArgs e)
        {
            GraphSettings.ShowFragmentIons = !GraphSettings.ShowFragmentIons;
        }

        private void precursorIonContextMenuItem_Click(object sender, EventArgs e)
        {
            GraphSettings.ShowPrecursorIon = !GraphSettings.ShowPrecursorIon;
        }

        private void charge1ContextMenuItem_Click(object sender, EventArgs e)
        {
            GraphSettings.ShowCharge1 = !GraphSettings.ShowCharge1;
        }

        private void charge2ContextMenuItem_Click(object sender, EventArgs e)
        {
            GraphSettings.ShowCharge2 = !GraphSettings.ShowCharge2;
        }

        private void charge3ContextMenuItem_Click(object sender, EventArgs e)
        {
            GraphSettings.ShowCharge3 = !GraphSettings.ShowCharge3;
        }

        private void charge4ContextMenuItem_Click(object sender, EventArgs e)
        {
            GraphSettings.ShowCharge4 = !GraphSettings.ShowCharge4;
        }

        private void chargesMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var set = GraphSettings;
            charge1ContextMenuItem.Checked = set.ShowCharge1;
            charge2ContextMenuItem.Checked = set.ShowCharge2;
            charge3ContextMenuItem.Checked = set.ShowCharge3;
            charge4ContextMenuItem.Checked = set.ShowCharge4;
        }

        private void ranksContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowRanks = !Settings.Default.ShowRanks;
            UpdateUI();
        }

        private void ionMzValuesContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowIonMz = !Settings.Default.ShowIonMz;
            UpdateUI();
        }

        private void observedMzValuesContextMenuItem_Click(object sender, EventArgs e)
        {
            SetObservedMzValues(!Settings.Default.ShowObservedMz);
        }

        public void SetObservedMzValues(bool on)
        {
            Settings.Default.ShowObservedMz = on;
            UpdateUI();
        }

        private void duplicatesContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowDuplicateIons = duplicatesContextMenuItem.Checked;
            UpdateUI();
        }

        private void lockYaxisContextMenuItem_Click(object sender, EventArgs e)
        {
            // Avoid updating the rest of the graph just to change the y-axis lock state
            LockYAxis(Settings.Default.LockYAxis = lockYaxisContextMenuItem.Checked);
        }

        private void spectrumPropsContextMenuItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new SpectrumChartPropertyDlg())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    UpdateUI();
            }
        }

        private void zoomSpectrumContextMenuItem_Click(object sender, EventArgs e)
        {
            ZoomSpectrumToSettings();
        }
        
        public void ZoomSpectrumToSettings()
        {
            _graphHelper.ZoomSpectrumToSettings(Program.ActiveDocumentUI, null);
        }

        private void copyMetafileButton_Click(object sender, EventArgs e)
        {
            CopyEmfToolStripMenuItem.CopyEmf(graphControl);
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            graphControl.Copy(false);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            graphControl.SaveAs();
        }

        private void btnPrint_Click(object sender, EventArgs e)
        {
            graphControl.DoPrint();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            CancelDialog();
        }

        public override void CancelDialog()
        {
            DialogResult = DialogResult.Cancel;
            Application.DoEvents();
            Close();
        }

        // Adds a single library peptide to the document.
        private void btnAdd_Click(object sender, EventArgs e)
        {
            AddPeptide();

            // Set focus back to the peptide edit box to allow further navigation
            // of the peptide list, and possibly more adding of peptides.
            textPeptide.Focus();
        }

        public void AddPeptide()
        {
            if (CheckLibraryInSettings() == DialogResult.Cancel || listPeptide.SelectedItem == null)
                return;

            var startingDocument = Document;
            // Open m/z limitations without allowing doc node tree to change, since that can take
            // seconds to achieve in a large document.
            var broadRangeDocument = startingDocument.ChangeSettingsNoDiff(startingDocument.Settings
                .ChangeTransitionInstrument(i => i.ChangeMinMz(TransitionInstrument.MIN_MEASUREABLE_MZ).ChangeMaxMz(TransitionInstrument.MAX_MEASURABLE_MZ))
                .ChangeTransitionFullScan(fs => fs.ChangeAcquisitionMethod(FullScanAcquisitionMethod.None, null)));
                
            var pepMatcher = new ViewLibraryPepMatching(broadRangeDocument,
                                                        _selectedLibrary,
                                                        _selectedSpec,
                                                        _matcher,
                                                        _peptides);

            if (!EnsureBackgroundProteome(startingDocument, pepMatcher, false))
                return;

            var pepInfo = (ViewLibraryPepInfo)listPeptide.SelectedItem;
            var nodePepMatched = pepMatcher.MatchSinglePeptide(pepInfo);
            if (nodePepMatched == null || nodePepMatched.Children.Count == 0)
            {
                MessageDlg.Show(this, Resources.ViewLibraryDlg_AddPeptide_Modifications_for_this_peptide_do_not_match_current_document_settings);
                return;
            }
            double precursorMz = nodePepMatched.TransitionGroups.First().PrecursorMz;
            double minMz = startingDocument.Settings.TransitionSettings.Instrument.MinMz;
            double maxMz = startingDocument.Settings.TransitionSettings.Instrument.MaxMz;
            if (minMz > precursorMz || precursorMz > maxMz)
            {
                MessageDlg.Show(this, string.Format(Resources.ViewLibraryDlg_AddPeptide_The_precursor_m_z__0_F04__is_outside_the_instrument_range__1__to__2__,
                                                    precursorMz, minMz, maxMz));
                return;
            }
            var isolationScheme = startingDocument.Settings.TransitionSettings.FullScan.IsolationScheme;
            if (isolationScheme != null && !isolationScheme.IsInRangeMz(precursorMz))
            {
                MessageDlg.Show(this, string.Format(Resources.ViewLibraryDlg_AddPeptide_The_precursor_m_z__0_F04__is_not_measured_by_the_current_DIA_isolation_scheme_,
                                                    precursorMz));
                return;
            }

            if (nodePepMatched.Children.Count > 0)
            {
                var keyMatched = nodePepMatched.SequenceKey;
                var chargeMatched = ((TransitionGroupDocNode)nodePepMatched.Children[0]).TransitionGroup.PrecursorAdduct;
                if (!cbAssociateProteins.Checked && Document.Peptides.Contains(nodePep => Equals(nodePep.SequenceKey, keyMatched) && nodePep.HasChildCharge(chargeMatched)))
                {
                    MessageDlg.Show(this, string.Format(Resources.ViewLibraryDlg_AddPeptide_The_peptide__0__already_exists_with_charge__1__in_the_current_document,
                                                        nodePepMatched.Peptide, chargeMatched));
                    return;
                }
            }

            if (pepMatcher.EnsureDuplicateProteinFilter(this, true) == DialogResult.Cancel)
                return;

            IdentityPath toPath = Program.MainWindow.SelectedPath;
            IdentityPath selectedPath = toPath;

            string message = string.Format(Resources.ViewLibraryDlg_AddPeptide_Add_library_peptide__0__,
                                           nodePepMatched.Peptide.Target);
            Program.MainWindow.ModifyDocument(message, doc =>
                {
                    var newDoc = doc;
                    if (_matcher.HasMatches)
                    {
                        var matchingDocument = newDoc;
                        newDoc = newDoc.ChangeSettings(
                            newDoc.Settings.ChangePeptideModifications(mods =>
                                _matcher.SafeMergeImplicitMods(matchingDocument)));
                    }

                    newDoc = pepMatcher.AddPeptides(newDoc, null, toPath,
                                      out selectedPath);
                    if (newDoc.MoleculeTransitionGroupCount == doc.MoleculeTransitionGroupCount)
                        return doc;
                    if(!_matcher.HasMatches)
                            return newDoc;
                    var modsNew = _matcher.GetDocModifications(newDoc); 
                    return newDoc.ChangeSettings(newDoc.Settings.ChangePeptideModifications(mods => modsNew));
                }, SkylineWindow.SettingsLogFunction);
            
            Program.MainWindow.SelectedPath = selectedPath;
            Document.Settings.UpdateDefaultModifications(true, true);
        }

        private bool EnsureBackgroundProteome(SrmDocument document, ViewLibraryPepMatching pepMatcher, bool ensureDigested)
        {
            if (cbAssociateProteins.Checked)
            {
                var backgroundProteome = document.Settings.PeptideSettings.BackgroundProteome;
                if (backgroundProteome.BackgroundProteomeSpec.IsNone)
                {
                    MessageDlg.Show(this,
                                    Resources.
                                        ViewLibraryDlg_EnsureBackgroundProteome_A_background_proteome_is_required_to_associate_proteins);
                    return false;
                }
                if (ensureDigested)
                {
                    if (!EnsureDigested(this, backgroundProteome))
                    {
                        return false;
                    }
                }
                pepMatcher.SetBackgroundProteome(backgroundProteome);
            }
            return true;
        }

        public static bool EnsureDigested(Control owner, BackgroundProteome backgroundProteome)
        {
            try
            {
                using (var proteomeDb = backgroundProteome.OpenProteomeDb())
                {
                    if (proteomeDb.IsDigested())
                    {
                        return true;
                    }
                }
                String message = string.Format(
                    Resources.ViewLibraryDlg_EnsureDigested_The_background_proteome___0___is_in_an_older_format___In_order_to_be_able_to_efficiently_find_peptide_sequences__the_background_proteome_should_be_upgraded_to_the_latest_version___Do_you_want_to_upgrade_the_background_proteome_now_,
                    backgroundProteome.Name);
                using (var alertDlg = new AlertDlg(message, MessageBoxButtons.YesNoCancel))
                {
                    switch (alertDlg.ShowDialog(owner))
                    {
                        case DialogResult.Cancel:
                            return false;
                        case DialogResult.No:
                            return true;
                    }
                }
                using (var longWaitDlg = new LongWaitDlg())
                {
                    bool finished = false;
                    longWaitDlg.PerformWork(owner, 1000, progressMonitor =>
                    {
                        using (var fileSaver = new FileSaver(backgroundProteome.DatabasePath))
                        {
                            var progressStatus =
                                new ProgressStatus().ChangeMessage(Resources
                                    .ViewLibraryDlg_EnsureDigested_Copying_database);
                            progressMonitor.UpdateProgress(progressStatus);
                            File.Copy(backgroundProteome.DatabasePath, fileSaver.SafeName, true);

                            using (var newProteomeDb =
                                ProteomeDb.OpenProteomeDb(fileSaver.SafeName, longWaitDlg.CancellationToken))
                            {
                                newProteomeDb.Digest(progressMonitor, ref progressStatus);
                            }
                            if (progressMonitor.IsCanceled)
                            {
                                return;
                            }
                            fileSaver.Commit();
                            finished = true;
                        }
                    });
                    return finished;
                }
            }
            catch (Exception e)
            {
                string errorMessage = TextUtil.LineSeparate(
                    string.Format(Resources.ViewLibraryDlg_EnsureDigested_An_error_occurred_while_trying_to_process_the_file__0__,
                        backgroundProteome.DatabasePath), e.Message);
                MessageDlg.ShowWithException(owner, errorMessage, e);
                return false;
            }
        }

        public DialogResult CheckLibraryInSettings()
        {
            // Check to see if the library is part of the settings. If not, prompt the user to add it.
            var docLibraries = Document.Settings.PeptideSettings.Libraries;
            if (docLibraries.GetLibrary(_selectedLibName) == null)
            {
                var message = TextUtil.LineSeparate(string.Format(
                    Resources.ViewLibraryDlg_CheckLibraryInSettings_The_library__0__is_not_currently_added_to_your_document, _selectedLibName),
                    Resources.ViewLibraryDlg_CheckLibraryInSettings_Would_you_like_to_add_it);
                var result = MultiButtonMsgDlg.Show(
                    this, message, Resources.ViewLibraryDlg_MatchModifications_Yes,
                    Resources.ViewLibraryDlg_MatchModifications_No, true);
                if (result == DialogResult.No)
                    return result;
                if (result == DialogResult.Yes)
                    Program.MainWindow.ModifyDocument(Resources.ViewLibraryDlg_CheckLibraryInSettings_Add_Library, doc =>
                        doc.ChangeSettings(doc.Settings.ChangePeptideLibraries(pepLibraries =>
                            pepLibraries.ChangeLibraries(new List<LibrarySpec>(docLibraries.LibrarySpecs) { _selectedSpec },
                            new List<Library>(docLibraries.Libraries) { _selectedLibrary }))), SkylineWindow.SettingsLogFunction);
            }
            return DialogResult.OK;
        }

        /// <summary>
        /// Adds all matched library peptides to the current document.
        /// </summary>
        private void btnAddAll_Click(object sender, EventArgs e)
        {
            AddAllPeptides();
        }

        public void AddAllPeptides()
        {
            CheckDisposed();

            if (CheckLibraryInSettings() == DialogResult.Cancel)
                return;
            
            var startingDocument = Document;

            SrmDocument startingDocumentImplicitMods = startingDocument;
            if(_matcher.HasMatches)
                startingDocumentImplicitMods = startingDocumentImplicitMods.ChangeSettings(
                    startingDocument.Settings.ChangePeptideModifications(
                        mods => _matcher.SafeMergeImplicitMods(startingDocument)));

            var pepMatcher = new ViewLibraryPepMatching(startingDocumentImplicitMods,
                                                        _selectedLibrary,
                                                        _selectedSpec,
                                                        _matcher,
                                                        _peptides);

            if (!EnsureBackgroundProteome(startingDocument, pepMatcher, true))
                return;
            pepMatcher.AddAllPeptidesSelectedPath = Program.MainWindow.SelectedPath;

            SrmDocument newDocument;
            var hasSmallMolecules = HasSmallMolecules;
            using (var longWaitDlg = new LongWaitDlg
                {
                    Text = hasSmallMolecules ? Resources.ViewLibraryDlg_AddAllPeptides_Matching_Molecules : Resources.ViewLibraryDlg_AddAllPeptides_Matching_Peptides,
                    Message = hasSmallMolecules ? Resources.ViewLibraryDlg_AddAllPeptides_Matching_molecules_to_the_current_document_settings : Resources.ViewLibraryDlg_AddAllPeptides_Matching_peptides_to_the_current_document_settings
                })
            {
                longWaitDlg.PerformWork(this, 1000, pepMatcher.AddAllPeptidesToDocument);
                newDocument = pepMatcher.DocAllPeptides;
                if (longWaitDlg.IsCanceled || newDocument == null)
                    return;
            }

            var selectedPath = pepMatcher.AddAllPeptidesSelectedPath;
            var numMatchedPeptides = pepMatcher.MatchedPeptideCount;
            if (numMatchedPeptides == 0)
            {
                MessageDlg.Show(this, Resources.ViewLibraryDlg_AddAllPeptides_No_peptides_match_the_current_document_settings);
                return;
            }

            // Calculate changes that will occur.
            var peptideCountDiff = newDocument.MoleculeCount - startingDocument.MoleculeCount;
            var groupCountDiff = newDocument.MoleculeTransitionGroupCount - startingDocument.MoleculeTransitionGroupCount;
            if (peptideCountDiff + groupCountDiff == 0)
            {
                MessageDlg.Show(this, Resources.ViewLibraryDlg_AddAllPeptides_All_library_peptides_already_exist_in_the_current_document);
                return;
            }
            var pepGroupCountDiff = newDocument.MoleculeGroupCount - startingDocument.MoleculeGroupCount;
            string proteins = cbAssociateProteins.Checked ? string.Format(Resources.ViewLibraryDlg_AddAllPeptides__0__proteins, pepGroupCountDiff)
                : string.Empty;
            var format = HasSmallMolecules
                ? Resources.ViewLibraryDlg_AddAllPeptides_This_operation_will_add__0__1__molecules__2__precursors_and__3__transitions_to_the_document
                : Resources.ViewLibraryDlg_AddAllPeptides_This_operation_will_add__0__1__peptides__2__precursors_and__3__transitions_to_the_document;

            string msg = string.Format(format,
                                        proteins, peptideCountDiff, groupCountDiff,
                                        newDocument.MoleculeTransitionCount - startingDocument.MoleculeTransitionCount);
            var numSkipped = pepMatcher.SkippedPeptideCount;          
            var hasSkipped = numSkipped > 0;
            var numUnmatchedPeptides = PeptidesCount - numMatchedPeptides;
            var hasUnmatched = numUnmatchedPeptides > 0;
            if (hasSkipped || hasUnmatched)
            {
                string duplicatePeptides = hasSkipped
                                               ? string.Format(Resources.ViewLibraryDlg_AddAllPeptides__0__existing,numSkipped)
                                               : string.Empty;
                string unmatchedPeptides = hasUnmatched
                                               ? string.Format(Resources.ViewLibraryDlg_AddAllPeptides__0__unmatched,numUnmatchedPeptides)
                                               : string.Empty;
                string entrySuffix = (numSkipped + numUnmatchedPeptides > 1
                                          ? Resources.ViewLibraryDlg_AddAllPeptides_entries
                                          : Resources.ViewLibraryDlg_AddAllPeptides_entry);
                msg = TextUtil.LineSeparate(msg, string.Empty, string.Empty,
                    string.Format((hasSkipped && hasUnmatched)
                                        ? Resources.ViewLibraryDlg_AddAllPeptides__0__and__1__library__2__will_be_ignored
                                        : Resources.ViewLibraryDlg_AddAllPeptides__0__1__library__2__will_be_ignored,
                                    duplicatePeptides, unmatchedPeptides, entrySuffix));
            }
            var dlg = new MultiButtonMsgDlg(msg, Resources.ViewLibraryDlg_AddAllPeptides_Add_All)
            {
                Tag = numUnmatchedPeptides
            };
            if (DialogResult.Cancel == dlg.ShowAndDispose(this))
            {
                return;
            }
            PeptideModifications modsNew = null;
            if (_matcher.HasMatches)
            {
                modsNew = _matcher.GetDocModifications(newDocument);
                newDocument = 
                    newDocument.ChangeSettings(newDocument.Settings.ChangePeptideModifications(mods => modsNew));
            }

            // If the user chooses to continue with the operation, call AddPeptides again in case the document has changed.
            var toPath = Program.MainWindow.SelectedPath;
            Program.MainWindow.ModifyDocument(string.Format(Resources.ViewLibraryDlg_AddAllPeptides_Add_all_peptides_from__0__library, SelectedLibraryName), 
                doc =>
                {
                    if (ReferenceEquals(doc, startingDocument))
                        return newDocument;
                    if (!Equals(doc.Settings.PeptideSettings.Modifications, startingDocument.Settings.PeptideSettings.Modifications))
                    {
                        selectedPath = toPath;
                        var message = TextUtil.LineSeparate(Resources.ViewLibraryDlg_AddAllPeptides_The_document_changed_during_processing,
                            Resources.ViewLibraryDlg_AddAllPeptides_Please_retry_this_operation);
                        throw new InvalidDataException(message);
                    }
                    var newDoc = doc;
                    newDoc = pepMatcher.AddPeptides(newDoc, null, toPath, out selectedPath);
                    if (newDoc.MoleculeTransitionGroupCount == doc.MoleculeTransitionGroupCount)
                        return doc;
                    if (!_matcher.HasMatches)
                        return newDoc;
                    return newDoc.ChangeSettings(newDoc.Settings.ChangePeptideModifications(mods => modsNew));
                }, SkylineWindow.SettingsLogFunction);

            Program.MainWindow.SelectedPath = selectedPath;
            Document.Settings.UpdateDefaultModifications(true, true);
        }

        // User wants to go to the previous page. Update the page info.
        private void PreviousLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PreviousPage();
        }

        public void PreviousPage()
        {
            _pageInfo.Page--;
            NextLink.Enabled = true;
            PreviousLink.Enabled = _pageInfo.Page > 1;
            UpdateListPeptide(0);
            UpdateStatusArea();
            UpdateUI();
            textPeptide.Focus();
        }

        // User wants to go to the next page. Update the page info.
        private void NextLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            NextPage();
        }

        public void NextPage()
        {
            _pageInfo.Page++;
            PreviousLink.Enabled = true;
            NextLink.Enabled = (_currentRange.Count - (_pageInfo.Page * _pageInfo.PageSize)) > 0;
            UpdateListPeptide(0);
            UpdateStatusArea();
            UpdateUI();
            textPeptide.Focus();
        }

        private void btnLibDetails_Click(object sender, EventArgs e)
        {
            ShowLibDetails();
        }

        public void ShowLibDetails()
        {
            if (_selectedLibrary != null)
            {
                LibraryDetails libInfo = _selectedLibrary.LibraryDetails;
                using (var dlg = new SpectrumLibraryInfoDlg(libInfo))
                {
                    // dlg.SetLinks(links);
                    dlg.ShowDialog(this);
                }
            }
        }

        # endregion
               
        #region Splitter events

        // Temp variable to store a previously focused control
        private Control _focused;

        private void splitMain_MouseDown(object sender, MouseEventArgs e)
        {
            // Get the focused control before the splitter is focused
            _focused = GetFocused(Controls);
        }

        private void splitMain_MouseUp(object sender, MouseEventArgs e)
        {
            // If a previous control had focus
            if (_focused != null)
            {
                // Return focus and clear the temp variable
                _focused.Focus();
                _focused = null;
            }
        }

        private static Control GetFocused(Control.ControlCollection controls)
        {
            foreach (Control c in controls)
            {
                if (c.Focused)
                {
                    // Return the focused control
                    return c;
                }
                if (c.ContainsFocus)
                {
                    // If the focus is contained inside a control's children
                    // return the child
                    return GetFocused(c.Controls);
                }
            }
            // No control on the form has focus
            return null;
        }

        #endregion

        #region Key Down Events

        private void PeptideTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up)
            {
                e.Handled = true;
                if (listPeptide.SelectedIndex > 0)
                {
                    listPeptide.SelectedIndex--;
                }
            }
            else if (e.KeyCode == Keys.Down)
            {
                e.Handled = true;
                if ((listPeptide.SelectedIndex + 1) < listPeptide.Items.Count)
                {
                    listPeptide.SelectedIndex++;
                }
            }
        }

// ReSharper disable MemberCanBeMadeStatic.Local
        private void ViewLibraryDlg_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                if (e.KeyCode == Keys.Z)
                    Program.MainWindow.Undo();
                if (e.KeyCode == Keys.Y)
                    Program.MainWindow.Redo();
            }
        }
// ReSharper restore MemberCanBeMadeStatic.Local

        #endregion

        #region Node Tip

        private void listPeptide_MouseMove(object sender, MouseEventArgs e)
        {
            Point pt = e.Location;
            if (!_moveThreshold.Moved(pt))
                return;
            _moveThreshold.Location = null;

            ITipProvider tipProvider = null;
            int i = GetIndexAtPoint(pt);

            if (i == -1)
            {
                _lastTipNode = null;
                _lastTipProvider = null;
            }
            else
            {
                var pepInfo = ((ViewLibraryPepInfo)listPeptide.Items[i]);
                if (!Equals(pepInfo, _lastTipNode))
                {
                    _lastTipNode = pepInfo;
                    _lastTipProvider = new PeptideTipProvider(pepInfo, _matcher);
                }
                tipProvider = _lastTipProvider;
            }

            if (tipProvider == null || !tipProvider.HasTip)
                _nodeTip.HideTip();
            else
            {
                var itemRect = listPeptide.GetItemRectangle(i);
                _nodeTip.SetTipProvider(tipProvider, 
                    new Rectangle(itemRect.X + _peptideImg.Width, itemRect.Y, itemRect.Width - _peptideImg.Width, itemRect.Height), 
                    pt);
            }
        }

        private void listPeptide_MouseLeave(object sender, EventArgs e)
        {
            _nodeTip.HideTip();
        }

        private int GetIndexAtPoint(Point pt)
        {
            for (int i = listPeptide.TopIndex; 0 <= i && i < listPeptide.Items.Count; i++)
            {
                var rectItem = listPeptide.GetItemRectangle(i);
                if (rectItem.Top > ClientRectangle.Bottom)
                    break;

                if (rectItem.Contains(pt))
                    return i;
            }
            return -1;
        }

        public Rectangle ScreenRect
        {
            get { return Screen.GetBounds(listPeptide); }
        }

        public bool AllowDisplayTip
        {
            get { return true; }
        }

        public Rectangle RectToScreen(Rectangle r)
        {
            return listPeptide.RectangleToScreen(r);
        }

        #endregion

        # region ViewLibraryPepInfo Helpers

        // Computes each ModificationInfo for the given peptide and returns a 
        // list of all modifications.
        private static IEnumerable<ModificationInfo> GetModifications(ViewLibraryPepInfo pep)
        {
            IList<ModificationInfo> modList = new List<ModificationInfo>();
            if (!pep.Target.IsProteomic)
                return modList;
            string sequence = pep.Target.Sequence;
            int iMod = -1;
            for (int i = 0; i < sequence.Length; i++)
            {
                if (sequence[i] != '[')
                {
                    iMod++;
                    continue;
                }
                int iAa = i - 1;
                // Invalid format. Should never happen.
                if (iAa < 0)
                    break;
                i++;
                int signVal = 1;
                if (sequence[i] == '+')
                    i++;
                else if (sequence[i] == '-')
                {
                    i++;
                    signVal = -1;
                }
                int iEnd = sequence.IndexOf(']', i);
                // Invalid format. Should never happen.
                if (iEnd == -1)
                    break;
                // NIST libraries sometimes use [?] when modification cannot be identified
                double massDiff;
                if (double.TryParse(sequence.Substring(i, iEnd - i), NumberStyles.Number, CultureInfo.InvariantCulture, out massDiff))
                {
                    modList.Add(new ModificationInfo(iMod, sequence[iAa], massDiff * signVal));
                }
                i = iEnd;
            }
            return modList;
        }
        #endregion

        #region Test helpers

        public string FilterString
        {
            get { return textPeptide.Text; }
            set { textPeptide.Text = value; }            
        }

        public int GetSelectedIndex()
        {
            return SelectedIndex;
        }

        public int SelectedIndex
        {
            get { return listPeptide.SelectedIndex; }
            set { listPeptide.SelectedIndex = value; }
        }

        public int SelectedLibIndex
        {
            get { return comboLibrary.SelectedIndex; }
            set { listPeptide.SelectedIndex = value; }
        }

        public string SourceFile
        {
            get { return GetLabelValue(labelFilename); }
        }

        public double RetentionTime
        {
            get
            {
                double rt;
                if (double.TryParse(GetLabelValue(labelRT), out rt))
                    return rt;
                return 0;
            }
        }

        private string GetLabelValue(Label label)
        {
            // Expecting "RT: 12.345" or "RT: 12.345 DT: 67.89" or "File: foo bar.baz"
            if (string.IsNullOrEmpty(label.Text))
               return string.Empty;
            var split = label.Text.Split(new[] {COLON_SEP}, StringSplitOptions.None);
            if (split.Length == 2)
                return split[1];
            // Trickier case of ""RT: 12.345 DT: 67.89"
            return split[1].Substring(0, split[1].LastIndexOf(' ')); // Not L10N
        }

        public PeptideTipProvider GetTipProvider(int i)
        {
            return new PeptideTipProvider(GetPepInfo(i), _matcher);
        }

        private ViewLibraryPepInfo GetPepInfo(int i)
        {
            if (listPeptide.Items.Count <= i || i < 0)
                return null;
            return (ViewLibraryPepInfo)listPeptide.Items[i];
        }

        public bool HasMatches
        {
            get { return _matcher.HasMatches; }
        }

        public bool HasUnmatchedPeptides
        {
            get { return listPeptide.Items.Cast<ViewLibraryPepInfo>().Any(info => !info.HasPeptide); }
        }

        public int UnmatchedPeptidesCount
        {
            get { return listPeptide.Items.Cast<ViewLibraryPepInfo>().Count(info => !info.HasPeptide); }
        }

        /// <summary>
        /// Set to false and wait for set to true to track updates
        /// </summary>
        public bool IsUpdateComplete { get; set; }

        /// <summary>
        /// Set to avoid waiting on extremely long update. Since it executes on the
        /// UI thread, it is very difficult to cancel otherwise during a test.
        /// </summary>
        public bool IsUpdateCanceled { get; set; }

        #endregion

        /// <summary>
        /// Data structure to store information on a modification for a given
        /// peptide sequence.
        /// </summary>
        private class ModificationInfo
        {
            public int IndexMod { get; private set; }
            public char ModifiedAminoAcid { get; private set; }
            public double ModifiedMass { get; private set; }

            public ModificationInfo(int indexMod, char modifiedAminoAcid, double modifiedMass)
            {
                IndexMod = indexMod;
                ModifiedAminoAcid = modifiedAminoAcid;
                ModifiedMass = modifiedMass;
            }
        }

        /// <summary>
        /// If the library has a HUGE number of peptides, it may not be
        /// performant to show them all at once in the peptides list box.
        /// We use paging and show 100 peptides at a time. This data structure
        /// keeps track of information pertaining to the pages.
        /// </summary>
        private class PageInfo
        {
            // The current page we are on
            public int Page { get; set; }

            // The size of each page
            private readonly int _pageSize;
            public int PageSize { get { return _pageSize; } }

            // Range of indices of items to be shown from the peptides array
            private IReadOnlyList<ViewLibraryPepInfo> _itemIndexRange;
            public IReadOnlyList<ViewLibraryPepInfo> ItemIndexRange { set { _itemIndexRange = value; } }

            // Total number of items to be show from the peptides array
            public int Items { get { return _itemIndexRange.Count; } }

            public PageInfo(int pageSize, int currentPage, IReadOnlyList<ViewLibraryPepInfo> itemIndexRange)
            {
                _pageSize = pageSize;
                Page = currentPage;
                ItemIndexRange = itemIndexRange;
            }

            // The total number of pages we need to show all the items
            public int Pages
            {
                get
                {
                    int temp = Items;
                    int totalPages = 0;
                    while (temp > 0)
                    {
                        totalPages++;
                        temp -= PageSize;
                    }
                    return totalPages;
                }
            }

            // The start index in the peptides array for the current page
            public int StartIndex
            {
                get
                {
                    int start = -1;
                    if (Items > 0)
                    {
                        if (Page > 0)
                        {
                            start = (Page - 1) * PageSize;
                        }
                        else
                        {
                            start = 0;
                        }
                    }
                    return start;
                }
            }

            // The end index in the peptides array for the current page
            public int EndIndex
            {
                get
                {
                    int end = -1;
                    if (Items > 0)
                    {
                        if (Page == Pages)
                        {
                            end = _itemIndexRange.Count;
                        }
                        else
                        {
                            end = 0 + (Page * PageSize);
                        }
                    }
                    return end;
                }
            }

        }

        /// <summary>
        /// Represents the spectrum graph for the selected peptide.
        /// </summary>
        public class ViewLibSpectrumGraphItem : AbstractSpectrumGraphItem
        {
            private readonly Library _library;
            private readonly LibKey _key;
            private string LibraryName { get { return _library.Name; } }
            private TransitionGroup TransitionGroup { get; set; }

            public ViewLibSpectrumGraphItem(LibraryRankedSpectrumInfo spectrumInfo, TransitionGroup group, Library lib, LibKey key)
                : base(spectrumInfo)
            {
                TransitionGroup = group;
                _library = lib;
                _key = key;
            }

            protected override bool IsMatch(double predictedMz)
            {
                return false;
            }

            public override string Title
            {
                get
                {
                    string libraryNamePrefix = LibraryName;
                    if (!string.IsNullOrEmpty(libraryNamePrefix))
                        libraryNamePrefix += " - "; // Not L10N
                    if (_key.IsPrecursorKey)
                    {
                        return string.Format(Resources.ViewLibSpectrumGraphItem_Title__0__1_, libraryNamePrefix, _key.PrecursorMz.GetValueOrDefault());
                    }
                    if (_key.IsSmallMoleculeKey)
                    {
                        return string.Format("{0}{1}{2}", libraryNamePrefix, TransitionGroup.Peptide.CustomMolecule.DisplayName, TransitionGroup.PrecursorAdduct); // Not L10N
                    }
                    return string.Format(Resources.ViewLibSpectrumGraphItem_Title__0__1__Charge__2__, libraryNamePrefix, TransitionGroup.Peptide.Target, TransitionGroup.PrecursorAdduct);
                }
            }
        }

        public class PeptideTipProvider : ITipProvider
        {
            private ViewLibraryPepInfo _pepInfo;
            private readonly LibKeyModificationMatcher _matcher;
            private readonly List<TextColor> _seqPartsToDraw;
            private readonly List<TextColor> _mzRangePartsToDraw;
            private readonly List<KeyValuePair<string, string>> _smallMoleculePartsToDraw;
            private readonly SrmSettings _settings;
            private readonly double _mz;

            public PeptideTipProvider(ViewLibraryPepInfo pepInfo, LibKeyModificationMatcher matcher)
            {
                ExplicitMods mods;
                TransitionGroupDocNode transitionGroup;
                _pepInfo = pepInfo;
                _matcher = matcher;
                GetPeptideInfo(_pepInfo, _matcher, out _settings, out transitionGroup, out mods);
                // build seq parts to draw
                _seqPartsToDraw = GetSequencePartsToDraw(mods);
                // Get small molecule info if any
                _smallMoleculePartsToDraw = null;
                var smallMolInfo = _pepInfo.GetSmallMoleculeLibraryAttributes();
                if (smallMolInfo != null && !smallMolInfo.IsEmpty)
                {
                    // Get a list of things like Name:caffeine, Formula:C8H10N4O2, InChIKey:RYYVLZVUVIJVGH-UHFFFAOYSA-N MoleculeIds: CAS:58-08-2\tKEGG:D00528
                    _smallMoleculePartsToDraw = smallMolInfo.LocalizedKeyValuePairs;
                }
                if (_pepInfo.Target != null)
                {
                    // build mz range parts to draw
                    var massH = _settings.GetPrecursorCalc(transitionGroup.TransitionGroup.LabelType, mods)
                        .GetPrecursorMass(_pepInfo.Target);
                    _mz = SequenceMassCalc.PersistentMZ(SequenceMassCalc.GetMZ(massH, transitionGroup.PrecursorAdduct));
                    _mzRangePartsToDraw = GetMzRangeItemsToDraw(_mz);
                }
                else
                {
                    _mzRangePartsToDraw = new List<TextColor>();
                    var precursorKey = _pepInfo.Key.LibraryKey as PrecursorLibraryKey;
                    if (precursorKey != null)
                    {
                        _mz = precursorKey.Mz;
                    }
                }
            }

            public bool HasTip
            {
                get { return true; }
            }

            public Size RenderTip(Graphics g, Size sizeMax, bool draw)
            {
                var table = new TableDesc();
                var tableMz = new TableDesc();
                SizeF sizeSeq;
                SizeF sizeMz;

                using (RenderTools rt = new RenderTools())
                {
                    // Draw sequence
                    sizeSeq = DrawTextParts(g, 0, 0, _seqPartsToDraw, rt);

                    // Draw small mol info
                    if (_smallMoleculePartsToDraw != null)
                    {
                        foreach (var item in _smallMoleculePartsToDraw)
                        {
                            tableMz.AddDetailRow(item.Key, item.Value, rt);
                        }
                    }
                    var heightSmallMol = tableMz.CalcDimensions(g).Height;
                    
                    // Draw mz
                    tableMz.AddDetailRow(Resources.PeptideTipProvider_RenderTip_Precursor_m_z, string.Format("{0:F04}", _mz), rt); // Not L10N
                    sizeMz = tableMz.CalcDimensions(g);
                    sizeSeq.Height += 2;    // Spacing between details and fragments

                    // Draw mz range out of bounds
                    if (_mzRangePartsToDraw.Count > 0)
                        sizeMz.Width = DrawTextParts(g, sizeMz.Width, sizeSeq.Height + heightSmallMol, _mzRangePartsToDraw, rt).Width;

                    if (draw)
                    {
                        table.Draw(g);
                        g.TranslateTransform(0, sizeSeq.Height);
                        tableMz.Draw(g);
                        g.TranslateTransform(0, -sizeSeq.Height);
                    }
                }

                int width = (int)Math.Round(Math.Max(sizeMz.Width, sizeSeq.Width));
                int height = (int)Math.Round(sizeMz.Height + sizeSeq.Height);
                return new Size(width + 8, height + 4); // +8 width, +4 height padding
            }

            private List<TextColor> GetMzRangeItemsToDraw(double mz)
            {
                var toDrawItems = new List<TextColor>();
                var maxMz = _settings.TransitionSettings.Instrument.MaxMz;
                var minMz = _settings.TransitionSettings.Instrument.MinMz;
                if (mz > maxMz)
                {
                    toDrawItems.Add(new TextColor(" > [" + minMz + "-")); // Not L10N
                    toDrawItems.Add(new TextColor(maxMz.ToString(), Brushes.Red));
                    toDrawItems.Add(new TextColor("]")); // Not L10N
                }
                else if (mz < minMz)
                {
                    toDrawItems.Add(new TextColor(" < [")); // Not L10N
                    toDrawItems.Add(new TextColor(minMz.ToString(), Brushes.Red));
                    toDrawItems.Add(new TextColor("-" + maxMz + "]")); // Not L10N
                }
                return toDrawItems;
            }

            private List<TextColor> GetSequencePartsToDraw(ExplicitMods mods)
            {
                var toDrawParts = new List<TextColor>();
                if (!_pepInfo.Key.HasModifications)
                {
                    toDrawParts.Add(new TextColor(_pepInfo.Key.Sequence));
                }
                else
                {
                    var splitMods = SplitModifications(_pepInfo.Key.Sequence);
                    for (var i = 0; i < splitMods.Count; i++)
                    {
                        var piece = splitMods[i];
                        string drawStr = piece.Item1.ToString();
                        var drawColor = Brushes.Black;
                        if (piece.Item2 != null) // if is modified AA
                        {
                            drawStr += piece.Item2;
                            var currentMod = GetCurrentMod(mods, i, piece);
                            if (!IsMatched(currentMod, piece)) // not match if color is red
                            {
                                drawStr = drawStr.Replace("]", "?]"); // Not L10N
                                drawColor = Brushes.Red;
                            }
                        }
                        toDrawParts.Add(new TextColor(drawStr, drawColor));
                    }
                }
                return toDrawParts;
            }

            private bool IsMatched(ExplicitMod currentMod, Tuple<char, string> piece)
            {
                if (currentMod == null)
                    return false;
                if (_matcher.MatcherPepMods == null)
                    return false;

                foreach (var mod in _matcher.MatcherPepMods.StaticModifications)
                {
                    if (mod.Terminus == ModTerminus.N && currentMod.IndexAA != 0)
                    {
                        continue;
                    }
                    if (mod.Terminus == ModTerminus.C &&
                        _pepInfo.PeptideNode != null &&
                        currentMod.IndexAA != _pepInfo.PeptideNode.Peptide.Sequence.Length - 1)
                    {
                        continue;
                    }
                    if (!MatchesAAsAndMass(piece.Item1.ToString(), currentMod.Modification.MonoisotopicMass, mod))
                        continue;

                    return true;
                }
                return false;
            }

            private ExplicitMod GetCurrentMod(ExplicitMods mods, int staticModIndex, Tuple<char, string> piece)
            {
                ExplicitMod currentMod = null;
                if (mods != null && mods.StaticModifications != null)
                {
                    currentMod = mods.StaticModifications.FirstOrDefault(m => staticModIndex == m.IndexAA);
                }
                if (currentMod == null) // If not in docnode.staticmodifications then check implicit mods in settings
                {
                    double mass;
                    if (double.TryParse(piece.Item2.Replace("[", string.Empty).Replace("]", string.Empty),  // Not L10N
                        NumberStyles.Float, CultureInfo.InvariantCulture, out mass))
                    {
                        foreach (var mod in _settings.PeptideSettings.Modifications.StaticModifications)
                        {
                            if (MatchesAAsAndMass(piece.Item1.ToString(), mass, mod))
                                currentMod = new ExplicitMod(staticModIndex, mod);
                        }
                    }
                }
                return currentMod;
            }

            private static bool MatchesAAsAndMass(string aa, double? mass, StaticMod mod)
            {
                if (aa != null && mod.AAs != null)
                {
                    var AAs = mod.AAs.Split(',').Select(p => p.Trim()).ToArray();
                    if (Array.IndexOf(AAs, aa) == -1)
                        return false;
                }
                if (mass.HasValue && mod.MonoisotopicMass.HasValue)
                {
                    // CONSIDER: Hard coded tolerance may need to be reconsidered
                    if (Math.Abs(mass.Value - mod.MonoisotopicMass.Value) > .05)
                    {
                        return false;
                    }
                }
                return true;
            }

            private List<Tuple<char, string>> SplitModifications(string modifiedSequence)
            {
                var result = new List<Tuple<char, string>>();
                for (int ich = 0; ich < modifiedSequence.Length; ich++)
                {
                    char ch = modifiedSequence[ich];
                    if (ich == modifiedSequence.Length - 1 || modifiedSequence[ich + 1] != '[')
                    {
                        result.Add(Tuple.Create(ch, (string) null));
                    }
                    else
                    {
                        int ichEndBracket = modifiedSequence.IndexOf(']', ich + 1);
                        if (ichEndBracket == -1)
                            ichEndBracket = modifiedSequence.Length;
                        int ichStart = ich + 1;
                        string modText = ichStart < ichEndBracket
                            ? modifiedSequence.Substring(ich + 1, ichEndBracket - ich)
                            : null;
                        result.Add(Tuple.Create(ch, modText));
                        ich = ichEndBracket;
                    }
                }
                return result;
            }

            // draws text at a start (x,y) and returns the end (x,y) of the drawn text
            // takes a list of <string, color> so that we can draw segments of different colors
            private SizeF DrawTextParts(Graphics g, float startX, float startY, List<TextColor> parts, RenderTools rt)
            {
                var size = new SizeF(startX, startY);
                float height = 0;
                foreach (var part in parts)
                {
                    g.DrawString(part.Text, rt.FontNormal, part.Color, new PointF(size.Width, size.Height)); // Not L10N
                    size.Width += g.MeasureString(part.Text, rt.FontNormal).Width - 3;
                    height = g.MeasureString(part.Text, rt.FontNormal).Height;
                }
                size.Height = height;
                return size;
            }

            #region Test support

            public List<TextColor> GetSeqParts() { return _seqPartsToDraw; }
            public List<TextColor> GetMzParts() { return _mzRangePartsToDraw; }

            #endregion

            public struct TextColor
            {
                public TextColor(string text, Brush color = null) : this()
                {
                    Text = text;
                    Color = color ?? Brushes.Black;
                }

                public string Text { get; private set; }
                public Brush Color { get; private set; }
            }
        }

        private void showChromatogramsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _showChromatograms = !_showChromatograms;
            UpdateUI();
        }
    }
}
