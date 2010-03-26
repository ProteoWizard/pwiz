/*
 * Original author: Tahmina Baker <tabaker .at. u.washington.edu>,
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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using pwiz.MSGraph;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model.DocSettings;
using ZedGraph;


namespace pwiz.Skyline.SettingsUI
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

    /// <summary>
    /// Dialog to view the contents of the libraries in the Peptide Settings 
    /// dialog's Library tab. It allows you to select one of the libraries 
    /// from a drop-down, view and search the list of peptides, and view the
    /// spectrum for peptide selected in the list.
    /// </summary>
    public partial class ViewLibraryDlg : Form, IGraphContainer
    {
        /// <summary>
        /// Data structure used to store information about a given peptide
        /// in the peptides list.
        /// </summary>
        private struct PeptideInfo
        {
            // Used to parse the modification string in a given sequence
            private const string RegexModificationPattern = @"\[.*?\]";
            private const string RegexValidModification = @"[+-]*\d+(\.\d+)*";

            // The peptide sequence, including the modification
            private string _sequence;
            public string Sequence
            {
                get
                {
                    return _sequence;
                }
                set
                {
                    _sequence = value;
                    
                    // We want to extract a few pieces of information from the
                    // sequence and store the information.

                    // First, strip the modification string from the sequence.
                    _unmodifiedSequence = Regex.Replace(_sequence, RegexModificationPattern, string.Empty);
                    
                    // Next, create the list of modifications, if any
                    CreateModificationsList();
                }
            }

            // List of modifications for this peptide
            private IList<ModificationInfo> _modificationList;
            public IList<ModificationInfo> Modifications { get { return _modificationList; } }
            private void CreateModificationsList()
            {
                _modificationList = new List<ModificationInfo>();
                foreach (Match m in Regex.Matches(Sequence, RegexModificationPattern))
                {
                    String str = m.ToString();

                    // Strip the "[" and "]" characters 
                    str = str.Substring(1, str.Length - 2);

                    // Look up the modified amino acid preceding the "["
                    var aminoAcid = new char[1];
                    Sequence.CopyTo(m.Index - 1, aminoAcid, 0, 1);

                    // Make sure we have a valid modification pattern.
                    // It should be a "+" or "-" followed by a number.
                    if (Regex.IsMatch(str, RegexValidModification))
                    {
                        _modificationList.Add(new ModificationInfo(aminoAcid[0], Double.Parse(str)));
                    }
                }
            }

            // The peptide sequence MINUS the modification string.
            private string _unmodifiedSequence;
            public string UnmodifiedSequence { get { return _unmodifiedSequence; } }

            // This is the display string that will be shown in the list. In
            // addition to the sequence, including modification, it will also
            // contain plus signs to indicate the charge. Example: AC[+59]D++
            private string _displayString;
            public string DisplayString
            {
                get
                {
                    return _displayString;
                }
                set
                {
                    _displayString = value;
                    _unmodifiedDisplayString = Regex.Replace(_displayString, RegexModificationPattern, string.Empty);
                }
            }

            // This is the display string from above MINUS the modification.
            // Example: ACD++
            private string _unmodifiedDisplayString;
            public string UnmodifiedDisplayString { get { return _unmodifiedDisplayString; } }

            public int Charge { get; set; }
        }

        /// <summary>
        /// Data structure to store information on a modification for a given
        /// peptide sequence.
        /// </summary>
        private class ModificationInfo
        {
            public char ModifiedAminoAcid { get; private set; }
            public double ModifiedMass { get; private set; }
           
            public ModificationInfo(char modifiedAminoAcid, double modifiedMass)
            {
                ModifiedAminoAcid = modifiedAminoAcid;
                ModifiedMass = modifiedMass;
            }
        }

        /// <summary>
        /// IComparer implementation to compare two PeptideInfo objects. This
        /// is used to sort the array of PeptideInfo objects we load from each
        /// library so that we can show it in alphabetical order in the list,
        /// but also show the modified peptides right below the unmodified 
        /// versions. Here's an example:
        /// DEYACR+
        /// DEYAC[+57.0]R+
        /// DEYACR++
        /// DEYAC[+57.0]R++
        /// </summary>
        private class PeptideInfoComparer : IComparer<PeptideInfo>
        {
            private readonly CompareInfo _compInfo;
            private readonly CompareOptions _compOptions = CompareOptions.None;

            // Constructs a comparer using the specified CompareOptions.
            public PeptideInfoComparer(CompareInfo cmpi, CompareOptions options)
            {
                _compInfo = cmpi;
                _compOptions = options;
            }

            // Compares peptides in PeptideInfo according to the CompareOptions
            // specified in the constructor.
            public int Compare(PeptideInfo a, PeptideInfo b)
            {
                int ret = _compInfo.Compare(a.UnmodifiedDisplayString, b.UnmodifiedDisplayString, _compOptions);
                if (0 == ret)
                {
                    // If the unmodified sequences AND the number of 
                    // charges are the same, it gets a little more 
                    // complicated:
                    if( a.Modifications.Count > b.Modifications.Count )
                    {
                        // If a contains more modifications than b, we want
                        // b to show up ahead of a in the list
                        return 1;                       
                    }
                    if ( a.Modifications.Count < b.Modifications.Count)
                    {
                        // If a contains less modifications than b, we want
                        // a to show up ahead of b in the list
                        return -1;
                    }

                    // a and b have the same unmodified sequence, same number
                    // of charges, and same number of modifications. Just 
                    // compare their display strings directly in this case.
                    return _compInfo.Compare(a.DisplayString, b.DisplayString, _compOptions);
                }
                    
                return ret;
            }
        }

        /// <summary>
        /// Data structure to store a range of indices of peptides found in 
        /// the peptides array matching the sequence typed in by the user.
        /// </summary>
        private class Range
        {
            public Range(int start, int end)
            {
                StartIndex = start;
                EndIndex = end;
            }

            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
            public int Count
            {
                get
                {
                    if ((StartIndex != -1) && (EndIndex != -1))
                    {
                        return (EndIndex + 1) - StartIndex;
                    }
                    return 0;
                }
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
            private Range _itemIndexRange;
            public Range ItemIndexRange { set { _itemIndexRange = value; } }
            
            // Total number of items to be show from the peptides array
            public int Items { get { return _itemIndexRange.Count; } }

            public PageInfo(int pageSize, int currentPage, Range itemIndexRange)
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
                            start = _itemIndexRange.StartIndex + (Page - 1)*PageSize;
                        }
                        else
                        {
                            start = _itemIndexRange.StartIndex;
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
                            end = _itemIndexRange.EndIndex + 1;
                        }
                        else
                        {
                            end = _itemIndexRange.StartIndex + (Page*PageSize);
                        }
                    }
                    return end;
                }
            }
        }

        /// <summary>
        /// Represents the spectrum graph for the selected peptide.
        /// </summary>
        private class ViewLibSpectrumGraphItem : AbstractMSGraphItem
        {
            private const string FontFace = "Arial";
            private static readonly Color ColorA = Color.YellowGreen;
            private static readonly Color ColorX = Color.Green;
            private static readonly Color ColorB = Color.BlueViolet;
            private static readonly Color ColorY = Color.Blue;
            private static readonly Color ColorC = Color.Orange;
            private static readonly Color ColorZ = Color.OrangeRed;
            private static readonly Color ColorPrecursor = Color.DarkCyan;
            private static readonly Color ColorNone = Color.Gray;

            private readonly Library _library;
            private string LibraryName { get { return _library.Name; } }
            private TransitionGroup TransitionGroup { get; set; }
            private LibraryRankedSpectrumInfo SpectrumInfo { get; set; }
            private readonly Dictionary<double, LibraryRankedSpectrumInfo.RankedMI> _ionMatches;
            public ICollection<IonType> ShowTypes { private get; set; }
            public ICollection<int> ShowCharges { private get; set; }
            public bool ShowRanks { private get; set; }
            public bool ShowDuplicates { private get; set; }
            public int LineWidth { private get; set; }
            public float FontSize { private get; set; }

            public ViewLibSpectrumGraphItem(LibraryRankedSpectrumInfo spectrumInfo, TransitionGroup group, Library lib)
            {
                SpectrumInfo = spectrumInfo;
                TransitionGroup = group;
                _library = lib;
                
                _ionMatches = spectrumInfo.PeaksMatched.ToDictionary(rmi => rmi.ObservedMz);

                // Default values
                FontSize = 10;
                LineWidth = 1;
            }

            // ReSharper disable InconsistentNaming
            private FontSpec _fontSpecA;
            private FontSpec FONT_SPEC_A { get { return GetFontSpec(ColorA, ref _fontSpecA); } }
            private FontSpec _fontSpecX;
            private FontSpec FONT_SPEC_X { get { return GetFontSpec(ColorX, ref _fontSpecX); } }
            private FontSpec _fontSpecB;
            private FontSpec FONT_SPEC_B { get { return GetFontSpec(ColorB, ref _fontSpecB); } }
            private FontSpec _fontSpecY;
            private FontSpec FONT_SPEC_Y { get { return GetFontSpec(ColorY, ref _fontSpecY); } }
            private FontSpec _fontSpecC;
            private FontSpec FONT_SPEC_C { get { return GetFontSpec(ColorC, ref _fontSpecC); } }
            private FontSpec _fontSpecZ;
            private FontSpec FONT_SPEC_PRECURSOR { get { return GetFontSpec(ColorPrecursor, ref _fontSpecPrecursor); } }
            private FontSpec _fontSpecPrecursor;
            private FontSpec FONT_SPEC_Z { get { return GetFontSpec(ColorZ, ref _fontSpecZ); } }
            private FontSpec _fontSpecNone;
            private FontSpec FONT_SPEC_NONE { get { return GetFontSpec(ColorNone, ref _fontSpecNone); } }
            // ReSharper restore InconsistentNaming

            private static FontSpec CreateFontSpec(Color color, float size)
            {
                return new FontSpec(FontFace, size, color, false, false, false) { Border = { IsVisible = false } };
            }

            private FontSpec GetFontSpec(Color color, ref FontSpec fontSpec)
            {
                return fontSpec ?? (fontSpec = CreateFontSpec(color, FontSize));
            }

            public override void CustomizeCurve(CurveItem curveItem)
            {
                ((LineItem)curveItem).Line.Width = LineWidth;
            }

            public override string Title
            {
                get
                {
                    string libraryNamePrefix = LibraryName;
                    if (!string.IsNullOrEmpty(libraryNamePrefix))
                        libraryNamePrefix += " - ";

                    string sequence = TransitionGroup.Peptide.Sequence;
                    int charge = TransitionGroup.PrecursorCharge;

                    return string.Format("{0}{1}, Charge {2}", libraryNamePrefix, sequence, charge);
                }
            }

            public override IPointList Points
            {
                get
                {
                    return new PointPairList(SpectrumInfo.MZs.ToArray(),
                                             SpectrumInfo.Intensities.ToArray());
                }
            }

            public override void AddAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
            {
                // ReSharper disable UseObjectOrCollectionInitializer
                foreach (var rmi in SpectrumInfo.PeaksMatched)
                {
                    if (!IsVisibleIon(rmi))
                        continue;

                    IonType type = IsVisibleIon(rmi.IonType, rmi.Ordinal, rmi.Charge) ?
                                                                                          rmi.IonType : rmi.IonType2;

                    Color color;
                    switch (type)
                    {
                        default: color = ColorNone; break;
                        case IonType.a: color = ColorA; break;
                        case IonType.x: color = ColorX; break;
                        case IonType.b: color = ColorB; break;
                        case IonType.y: color = ColorY; break;
                        case IonType.c: color = ColorC; break;
                        case IonType.z: color = ColorZ; break;
                        case IonType.precursor: color = ColorPrecursor; break;
                    }

                    double mz = rmi.ObservedMz;
                    var stick = new LineObj(color, mz, rmi.Intensity, mz, 0);
                    stick.IsClippedToChartRect = true;
                    stick.Location.CoordinateFrame = CoordType.AxisXYScale;
                    stick.Line.Width = LineWidth + 1;
                    annotations.Add(stick);
                }
                //ReSharper restore UseObjectOrCollectionInitializer
            }

            public override PointAnnotation AnnotatePoint(PointPair point)
            {
                LibraryRankedSpectrumInfo.RankedMI rmi;
                if (!_ionMatches.TryGetValue(point.X, out rmi) || !IsVisibleIon(rmi))
                    return null;

                var parts = new string[2];
                int i = 0;
                if (IsVisibleIon(rmi.IonType, rmi.Ordinal, rmi.Charge))
                    parts[i++] = GetLabel(rmi.IonType, rmi.Ordinal, rmi.Charge, rmi.Rank);
                if (IsVisibleIon(rmi.IonType2, rmi.Ordinal2, rmi.Charge2))
                    parts[i] = GetLabel(rmi.IonType2, rmi.Ordinal2, rmi.Charge2, 0);
                var sb = new StringBuilder();
                foreach (string part in parts)
                {
                    if (part == null)
                        continue;
                    if (sb.Length > 0)
                        sb.Append(", ");
                    sb.Append(part);
                }
                FontSpec fontSpec;
                switch (rmi.IonType)
                {
                    default: fontSpec = FONT_SPEC_NONE; break;
                    case IonType.a: fontSpec = FONT_SPEC_A; break;
                    case IonType.x: fontSpec = FONT_SPEC_X; break;
                    case IonType.b: fontSpec = FONT_SPEC_B; break;
                    case IonType.y: fontSpec = FONT_SPEC_Y; break;
                    case IonType.c: fontSpec = FONT_SPEC_C; break;
                    case IonType.z: fontSpec = FONT_SPEC_Z; break;
                    case IonType.precursor: fontSpec = FONT_SPEC_PRECURSOR; break;
                }
                
                return new PointAnnotation(sb.ToString(), fontSpec);
            }

            private string GetLabel(IonType type, int ordinal, int charge, int rank)
            {
                string chargeIndicator = (charge == 1 ? "" : Transition.GetChargeIndicator(charge));
                string label = type.ToString();
                if (!Transition.IsPrecursor(type))
                    label = label + ordinal + chargeIndicator;
                if (rank > 0 && ShowRanks)
                    label = string.Format("{0} (rank {1})", label, rank);
                return label;
            }

            private bool IsVisibleIon(LibraryRankedSpectrumInfo.RankedMI rmi)
            {
                bool singleIon = (rmi.Ordinal2 == 0);
                if (ShowDuplicates && singleIon)
                    return false;
                return IsVisibleIon(rmi.IonType, rmi.Ordinal, rmi.Charge) ||
                       IsVisibleIon(rmi.IonType2, rmi.Ordinal2, rmi.Charge2);
            }

            private bool IsVisibleIon(IonType type, int ordinal, int charge)
            {
                return ordinal > 0 && ShowTypes.Contains(type) && ShowCharges.Contains(charge);
            }
        }

        /// <summary>
        /// Needed by the graph object.
        /// </summary>
        public interface IStateProvider
        {
            TreeNode SelectedNode { get; }
            IList<IonType> ShowIonTypes { get; }
            IList<int> ShowIonCharges { get; }
        }

        /// <summary>
        /// IStateProvider implementation for the View Library spectrum graph.
        /// </summary>
        private class ViewLibStateProvider : IStateProvider
        {
            public TreeNode SelectedNode { get { return null; } }
            
            public IList<IonType> ShowIonTypes
            {
                get
                {
                    // Priority ordered
                    var types = new List<IonType>();
                    if (Settings.Default.ShowYIons)
                        types.Add(IonType.y);
                    if (Settings.Default.ShowBIons)
                        types.Add(IonType.b);
                    if (Settings.Default.ShowZIons)
                        types.Add(IonType.z);
                    if (Settings.Default.ShowCIons)
                        types.Add(IonType.c);
                    if (Settings.Default.ShowXIons)
                        types.Add(IonType.x);
                    if (Settings.Default.ShowAIons)
                        types.Add(IonType.a);
                    if (Settings.Default.ShowPrecursorIon)
                        types.Add(IonType.precursor);
                    return types;
                }
            }

            public IList<int> ShowIonCharges
            {
                get
                {
                    // Priority ordered
                    var charges = new List<int>();
                    if (Settings.Default.ShowCharge1)
                        charges.Add(1);
                    if (Settings.Default.ShowCharge2)
                        charges.Add(2);
                    return charges;
                }
            }
        }

        /// <summary>
        /// ILoadMonitor implementation needed for loading the library in the 
        /// case where the library isn't already loaded in the main window.
        /// </summary>
        private sealed class ViewLibLoadMonitor : ILoadMonitor
        {
            private readonly IProgressMonitor _monitor;

            public ViewLibLoadMonitor(IProgressMonitor monitor)
            {
                _monitor = monitor;
            }

            public bool IsCanceled
            {
                get { return _monitor.IsCanceled; }
            }

            public void UpdateProgress(ProgressStatus status)
            {
                _monitor.UpdateProgress(status);
            }

            public IStreamManager StreamManager
            {
                get
                {
                    return FileStreamManager.Default;
                }
            }
        }

        private readonly LibraryManager _libraryManager;
        private readonly SettingsListBoxDriver<LibrarySpec> _driverLibrary;
        private Library _selectedLibrary;
        private Range _currentRange;
        private PeptideInfo[] _peptides;
        private readonly PageInfo _pageInfo;

        // To improve the performance of searching for peptides, when the
        // user types in a sequence in the edit box, if there are matches,
        // we store the range for the matches in this table. Later, if the
        // user types in the same sequence, or a substring of the sequence,
        // we use the table to narrow down the search. We can do tis 
        // because the peptides are stored in the array in alphabetical
        // order.
        //
        // For example, say we have the following peptides in the list:
        // AACD
        // ACDA
        // CADA
        // DCAA
        // 
        // The user types in "A" in the edit box. We find the two matches
        // starting with the letter A and store the range in the range 
        // table. The user then types in "AA". Now we look up the range for
        // peptides starting with "A" and search only within that range.
        readonly Dictionary<string, Range> _rangeTable;
        
        private readonly IStateProvider _stateProvider;
        private MSGraphPane GraphPane { get { return (MSGraphPane)graphControl.MasterPane[0]; } }
        private ViewLibSpectrumGraphItem GraphItem { get; set; }
        public int LineWidth { get; set; }
        public float FontSize { get; set; }

        /// <summary>
        /// Constructor for the View Library dialog.
        /// </summary>
        /// <param name="libMgr"> A library manager is needed to load the
        /// chosen peptide library. </param>
        /// <param name="driverLibrary"> This is needed to get the names of the
        /// libraries currently in the Library tab of Peptide Settings. </param>
        public ViewLibraryDlg(LibraryManager libMgr, SettingsListBoxDriver<LibrarySpec> driverLibrary)
        {
            InitializeComponent();

            _libraryManager = libMgr;
            _driverLibrary = driverLibrary;
            _pageInfo = new PageInfo(100, 0, _currentRange);
            _rangeTable = new Dictionary<string, Range>();
            _stateProvider = new ViewLibStateProvider();

            graphControl.MasterPane.Border.IsVisible = false;
            var graphPane = GraphPane;
            graphPane.Border.IsVisible = false;
            graphPane.Title.IsVisible = true;
            graphPane.AllowCurveOverlap = true;

            Icon = Resources.Skyline;
        }

        private void ViewLibraryDlg_Load(object sender, EventArgs e)
        {
            // The combobox is the control that tells us which peptide library
            // we need to load, so as soon as the dialog loads, we need to
            // populate it and set a default selection for the library.
            InitializeLibrariesComboBox();
        }

        // Gets names of libraries in the Peptide Settings --> Library tab,
        // displays them in the Library combobox, and selects the first one
        // as the default library.
        private void InitializeLibrariesComboBox()
        {
            LibraryComboBox.BeginUpdate();

            // Go through all the libraries currently in the Peptide Settings
            // --> Library tab and show them in the combobox. 
            int numLibs = _driverLibrary.List.Count;
            for (int i = 0; i < numLibs; i++)
            {
                LibraryComboBox.Items.Add(_driverLibrary.List[i].Name);
            }

            // Set the selection to the very first library in the combobox.
            // The "View Libraries" button is not enabled unless we have at 
            // least ONE library, so if we are here, we must have at least 
            // one to select.
            LibraryComboBox.SelectedIndex = 0;

            LibraryComboBox.EndUpdate();
        }

        private void UpdateViewLibraryDlg()
        {
            // Order matters!!
            _rangeTable.Clear();
            LoadLibrary();
            InitializePeptides();
            UpdatePageInfo();
            UpdateStatusArea();
            UpdatePeptideListBox();
            PeptideTextBox.Select();
        }

        // Loads the library selected in the Library combobox. First, we check
        // to see if the library is already loaded in the main window. If yes,
        // great, we can just use that. If no, we need to use the LongWaitDlg
        // as the IProgressMonitor to load the library from the LibrarySpec.
        private void LoadLibrary()
        {
            LibrarySpec selectedLibrarySpec = _driverLibrary.List[LibraryComboBox.SelectedIndex];
            _selectedLibrary = _libraryManager.TryGetLibrary(selectedLibrarySpec);
            if (null == _selectedLibrary)
            {
                var longWait = new LongWaitDlg { Text = "Loading Library" };
                try
                {
                    var status = longWait.PerformWork(this, 
                                                      800, 
                                                      monitor =>
                                                      {
                                                          _selectedLibrary = selectedLibrarySpec.LoadLibrary(new ViewLibLoadMonitor(monitor));
                                                      }
                                                     );
                    if (status.IsError)
                    {
                        MessageBox.Show(this, status.ErrorException.Message);
                    }
                }
                catch (Exception x)
                {
                    MessageBox.Show(this, string.Format("An error occurred attempting to import the {0} library.\n{1}", selectedLibrarySpec.Name, x.Message), Program.Name);
                }
            }
        }

        // Loads the entire list of peptides from the library selected into an
        // array of PeptideInfo objects and sorts them alphabetically, with a
        // few small quirks related to the modification. Please see comments
        // for the PeptideInfoComparer above.
        private void InitializePeptides()
        {
            // Load the entire list of peptides from the library
            _peptides = new PeptideInfo[_selectedLibrary.Count];
            int index = 0;
            foreach (var libKey in _selectedLibrary.Keys)
            {
                _peptides[index].Sequence = libKey.Sequence;
                _peptides[index].Charge = libKey.Charge;
                _peptides[index].DisplayString = libKey.ToString();
                index++;
            }

            Array.Sort(_peptides, new PeptideInfoComparer(CompareInfo.GetCompareInfo("en-US"), CompareOptions.Ordinal & CompareOptions.IgnoreCase));
            _currentRange = new Range(0, _peptides.Length - 1);
        }

        // Updates the status area showing which peptides are being shown.
        private void UpdateStatusArea()
        {
            int showStart = 0;
            int showEnd = 0;
            if (_currentRange.Count > 0)
            {
                showStart = _pageInfo.StartIndex + 1;
                showEnd = _pageInfo.EndIndex;
            }

            PeptideCount.Text = string.Format("Peptides {0} through {1} of {2} total.", showStart, showEnd, _peptides.Length);
            PageCount.Text = string.Format("Page {0} of {1}", _pageInfo.Page, _pageInfo.Pages);
        }
            
        // Used to update page info when something changes: e.g. library
        // selection changes, or search results change.
        private void UpdatePageInfo()
        {
            // We need to update the number of total items
            _pageInfo.ItemIndexRange = _currentRange;

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

        // Used to update the peptides list when something changes: e.g. 
        // library selection changes, or search results change.
        private void UpdatePeptideListBox()
        {
            PeptideListBox.BeginUpdate();
            PeptideListBox.Items.Clear();
            if (_currentRange.Count > 0)
            {
                int start = _pageInfo.StartIndex;
                int end = _pageInfo.EndIndex;
                for (int i = start; i < end; i++)
                {
                    PeptideListBox.Items.Add(_peptides[i].DisplayString);
                }

                PeptideListBox.SelectedIndex = 0;
            }

            PeptideListBox.EndUpdate();
        }

        // Used to get the range of peptide indices in the peptides array that
        // match (by match, we mean "starts with") the given string.
        private Range GetRange(string s)
        {
            Range rOut;
            if (s.Length == 0)
            {
                // If the string contains nothing, this means we need to show
                // all the peptides in the entire list.
                rOut = new Range(0, _peptides.Length - 1);
            }
            else
            {
                String temp = s;
                Range rTemp;
                bool addToRangeTable = false;
                do
                {
                    // Try to look up the range in the range table.
                    if (!_rangeTable.TryGetValue(temp, out rTemp))
                    {
                        // The string wasn't found in the range table, so we
                        // would like to add it to the table once we have
                        // calculated the range.
                        addToRangeTable = true;

                        // We keep chopping the string by the last character,
                        // then trying to look it up in the range table until 
                        // we are left with just a single character. 
                        if (temp.Length >= 2)
                        {
                            temp = temp.Remove(temp.Length - 1); // remove rightmost char
                        }
                        else
                        {
                            // If we are only left with a single character, and
                            // we haven't found any of the substrings in the
                            // range table yet, unfortunately, we need to
                            // search the entire peptides array.
                            rTemp = new Range(0, _peptides.Length - 1);
                        }
                    }
                } while (rTemp == null);

                // If we need to add the string to the range table, calculate
                // the range of peptides in matches in the array and then add
                // this information to the range table.
                if (addToRangeTable)
                {
                    rOut = CalculateRange(s, rTemp);
                    if (rOut.Count > 0)
                    {
                        _rangeTable.Add(s, rOut);
                    }
                }
                else
                {
                    rOut = rTemp;
                }
            }

            return rOut;
        }

        // Calculates the range of peptides in the peptides array that match
        // the string passed in. If nothing is found, a range of (-1, -1) is 
        // returned. 
        private Range CalculateRange(string s, Range r)
        {
            var substrRange = new Range(-1, -1);
            int index;
            bool firstMatch = true;
            for (index = r.StartIndex; index <= r.EndIndex; index++)
            {
                // See if either the modified/unmodified sequence starts with
                // the string passed in.
                if (_peptides[index].DisplayString.StartsWith(s, StringComparison.InvariantCultureIgnoreCase) ||
                    _peptides[index].UnmodifiedDisplayString.StartsWith(s, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (firstMatch)
                    {
                        substrRange.StartIndex = index;
                        substrRange.EndIndex = index;
                        firstMatch = false;
                    }
                    else
                    {
                        substrRange.EndIndex = index;
                    }
                }
            }

            return substrRange;
        }

        // Gets the index of the peptide in the peptides array for the selected
        // peptide in the peptides listbox. Returns -1 if none is found.
        private int GetIndexOfSelectedPeptide()
        {
            if (_currentRange.Count > 0)
            {
                string selPeptide = PeptideListBox.SelectedItem.ToString();
                Range selPeptideRange = GetRange(selPeptide);
                if (selPeptideRange.Count > 0)
                {
                    int start = selPeptideRange.StartIndex;
                    int end = selPeptideRange.EndIndex;
                    for (int i = start; i <= end; i++)
                    {
                        if (selPeptide.Equals(_peptides[i].DisplayString))
                        {
                            return i;
                        }
                    }
                }
            }

            return -1;
        }

        private void PeptideTextBox_TextChanged(object sender, EventArgs e)
        {
            _currentRange = GetRange(PeptideTextBox.Text);
            UpdatePageInfo();
            UpdateStatusArea();
            UpdatePeptideListBox();
            UpdateUI();
        }

        private void PeptideListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // We need to update the spectrum graph when the peptide
            // selected in the listbox changes.
            UpdateUI();
        }

        // User wants to go to the previous page. Update the page info.
        private void PreviousLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            _pageInfo.Page--;
            NextLink.Enabled = true;
            PreviousLink.Enabled = _pageInfo.Page > 1;
            UpdatePeptideListBox();
            UpdateStatusArea();
            UpdateUI();
        }

        // User wants to go to the next page. Update the page info.
        private void NextLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            _pageInfo.Page++;
            PreviousLink.Enabled = true;
            NextLink.Enabled = (_currentRange.Count - (_pageInfo.Page * _pageInfo.PageSize)) > 0;
            UpdatePeptideListBox();
            UpdateStatusArea();
            UpdateUI();
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

        /// <summary>
        /// Updates the spectrum graph using the currently selected peptide.
        /// </summary>
        public void UpdateUI()
        {
            // Clear existing data from the graph pane
            var graphPane = (MSGraphPane)graphControl.MasterPane[0];
            graphPane.CurveList.Clear();
            graphPane.GraphObjList.Clear();

            // Check for appropriate spectrum to load
            bool available = false;
            try
            {
                int index = GetIndexOfSelectedPeptide();
                if (-1 != index)
                {
                    var lkey = new LibKey(_peptides[index].Sequence, _peptides[index].Charge);
                    SpectrumPeaksInfo spectrum;
                    if (_selectedLibrary.TryLoadSpectrum(lkey, out spectrum))
                    {
                        SrmSettings settings =  Program.ActiveDocumentUI.Settings;
                        var peptide = new Peptide(null, _peptides[index].UnmodifiedSequence, null, null, 0);
                        const IsotopeLabelType isotopeLabelType = IsotopeLabelType.light;
                        var group = new TransitionGroup(peptide, _peptides[index].Charge, isotopeLabelType);

                        var types = _stateProvider.ShowIonTypes;
                        var charges = _stateProvider.ShowIonCharges;
                        var rankTypes = settings.TransitionSettings.Filter.IonTypes;
                        var rankCharges = settings.TransitionSettings.Filter.ProductCharges;

                        ExplicitMods mods = null;
                        if (lkey.IsModified)
                        {
                            IList<ExplicitMod> staticModList = new List<ExplicitMod>();
                            IList<ExplicitMod> heavyModList = new List<ExplicitMod>();
                            IList<ModificationInfo> modList = _peptides[index].Modifications;
                            int numMods = modList.Count;
                            for (int idx = 0; idx < numMods; idx++)
                            {
                                var smod = new StaticMod("temp",
                                                               modList[idx].ModifiedAminoAcid,
                                                               null,
                                                               null,
                                                               LabelAtoms.None,
                                                               RelativeRT.Unknown,
                                                               modList[idx].ModifiedMass,
                                                               modList[idx].ModifiedMass,
                                                               null,
                                                               null,
                                                               null);

                                var exmod = new ExplicitMod(idx, smod);
                                staticModList.Add(exmod);
                            }

                            mods = new ExplicitMods(peptide, staticModList, heavyModList);
                        }

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

                        var spectrumInfoR = new LibraryRankedSpectrumInfo(spectrum,
                                                                          isotopeLabelType,
                                                                          group,
                                                                          settings,
                                                                          mods,
                                                                          charges,
                                                                          types,
                                                                          rankCharges,
                                                                          rankTypes);

                        GraphItem = new ViewLibSpectrumGraphItem(spectrumInfoR, group, _selectedLibrary)
                                        {   ShowTypes = types,
                                            ShowCharges = charges,
                                            ShowRanks = Settings.Default.ShowRanks,
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
            }
            catch (IOException)
            {
                AddGraphItem(graphPane, new NoDataMSGraphItem("Failure loading spectrum. Library may be corrupted."));
                return;
            }
            
            // Show unavailable message, if no spectrum loaded
            if (!available)
            {
                AddGraphItem(graphPane, new UnavailableMSGraphItem());
            }
        }

        private void LibraryComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // The user has selected a different library. We need to reload 
            // everything in the dialog. 
            UpdateViewLibraryDlg();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            Application.DoEvents();
        }
    }
}
