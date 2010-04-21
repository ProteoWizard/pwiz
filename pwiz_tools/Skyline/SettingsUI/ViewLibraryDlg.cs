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
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using pwiz.MSGraph;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
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
    /// Needed by the graph object.
    /// </summary>
    public interface IStateProvider
    {
        IList<IonType> ShowIonTypes { get; }
        IList<int> ShowIonCharges { get; }

        void BuildSpectrumMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip);
    }

    /// <summary>
    /// Dialog to view the contents of the libraries in the Peptide Settings 
    /// dialog's Library tab. It allows you to select one of the libraries 
    /// from a drop-down, view and search the list of peptides, and view the
    /// spectrum for peptide selected in the list.
    /// </summary>
    public partial class ViewLibraryDlg : Form, IGraphContainer, IStateProvider
    {
        // Used to parse the modification string in a given sequence
        private const string REGEX_MODIFICATION_PATTERN = @"\[.*?\]";
        private const string REGEX_VALID_MODIFICATION = @"[+-]*\d+(\.\d+)*";

        /// <summary>
        /// Data structure containing information on a single peptide. It is
        /// basically a very lightweight wrapper for the LibKey object.
        /// </summary>
        private struct PepInfo
        {
            private const int PEPTIDE_CHARGE_OFFSET = 3;
//            private const int PEPTIDE_MODIFICATION_OFFSET_LOWER = 2;
//            private const int PEPTIDE_MODIFICATION_OFFSET_UPPER = 1;

            public PepInfo(LibKey key, ICollection<byte> lookupPool)
                : this()
            {
                Key = key;
                IndexLookup = lookupPool.Count;
                foreach (char aa in key.AminoAcids)
                    lookupPool.Add((byte)aa);
                
                // Order extra bytes so that a byte-by-byte comparison of the
                // lookup bytes will order correctly.
                lookupPool.Add((byte)key.Charge);
                int countMods = key.ModificationCount;
                lookupPool.Add((byte)(countMods & 0xFF));
                lookupPool.Add((byte)((countMods >> 8) & 0xFF)); // probably never non-zero, but to be safe
                LengthLookup = lookupPool.Count - IndexLookup;
            }

            public LibKey Key { get; private set; }
            private int IndexLookup { get; set; }
            private int LengthLookup { get; set; }

            public string DisplayString { get { return Key.ToString(); } }
            public int Charge { get { return Key.Charge; } }
            public string Sequence { get { return Key.Sequence; } }
            public bool IsModified { get { return Key.IsModified; } }

            private int SequenceLength { get { return LengthLookup - PEPTIDE_CHARGE_OFFSET; } }

            public string GetAASequence(byte[] lookupPool)
            {
                return Encoding.Default.GetString(lookupPool, IndexLookup, SequenceLength);
            }

            public static int Compare(PepInfo p1, PepInfo p2, IList<byte> lookupPool)
            {
                // If they point to the same look-up index, then they are equal.
                if (p1.IndexLookup == p2.IndexLookup)
                    return 0;

                int lenP1 = p1.SequenceLength;
                int lenP2 = p2.SequenceLength;
                int lenCompare;
                // If sequences are equal length compare charge and mod count also
                if (lenP1 == lenP2)
                    lenCompare = p1.LengthLookup;
                // Otherwise, just compare the length of the shorter sequence
                else
                    lenCompare = Math.Min(lenP1, lenP2);
                // Compare bytes in the lookup pool
                for (int i = 0; i < lenCompare; i++)
                {
                    byte b1 = lookupPool[p1.IndexLookup + i];
                    byte b2 = lookupPool[p2.IndexLookup + i];
                    // If unequal bytes are found, compare the bytes
                    if (b1 != b2)
                        return b1 - b2;
                }

                // If sequence length is not equal, the shorter should be first.
                if (lenP1 != lenP2)
                    return lenP1 - lenP2;

                // p1 and p2 have the same unmodified sequence, same number
                // of charges, and same number of modifications. Just 
                // compare their display strings directly in this case.
                return Comparer.Default.Compare(p1.DisplayString, p2.DisplayString);
            }
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
        /// IComparer implementation to compare two PepInfo objects. It is used
        /// to sort the array of PepInfo objects we load from each library so 
        /// that we can show it in alphabetical order in the list, and also 
        /// show the modified peptides right below the unmodified versions. 
        /// Here's an example:
        /// DEYACR+
        /// DEYAC[+57.0]R+
        /// DEYACR++
        /// DEYAC[+57.0]R++
        /// </summary>
        private class PepInfoComparer : IComparer<PepInfo>
        {
            private readonly List<byte> _lookupPool;

            // Constructs a comparer using the specified CompareOptions.
            public PepInfoComparer(List<byte> lookupPool)
            {
                _lookupPool = lookupPool;
            }

            // Compares peptides in PeptideInfo according to the CompareOptions
            // specified in the constructor.
            public int Compare(PepInfo p1, PepInfo p2)
            {
                return PepInfo.Compare(p1, p2, _lookupPool);
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

            public Range(Range rangeIn)
            {
                StartIndex = rangeIn.StartIndex;
                EndIndex = rangeIn.EndIndex;
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
        private class ViewLibSpectrumGraphItem : AbstractSpectrumGraphItem
        {
            private readonly Library _library;
            private string LibraryName { get { return _library.Name; } }
            private TransitionGroup TransitionGroup { get; set; }
            
            public ViewLibSpectrumGraphItem(LibraryRankedSpectrumInfo spectrumInfo, TransitionGroup group, Library lib)
                : base(spectrumInfo)
            {
                TransitionGroup = group;
                _library = lib;
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
                        libraryNamePrefix += " - ";

                    string sequence = TransitionGroup.Peptide.Sequence;
                    int charge = TransitionGroup.PrecursorCharge;

                    return string.Format("{0}{1}, Charge {2}", libraryNamePrefix, sequence, charge);
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
        //private PeptideInfo[] _peptides;
        private readonly PageInfo _pageInfo;
        private MSGraphPane GraphPane { get { return (MSGraphPane)graphControl.MasterPane[0]; } }
        private ViewLibSpectrumGraphItem GraphItem { get; set; }
        public int LineWidth { get; set; }
        public float FontSize { get; set; }

        private PepInfo[] _peptides;
        private byte[] _lookupPool;

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

            graphControl.MasterPane.Border.IsVisible = false;
            var graphPane = GraphPane;
            graphPane.Border.IsVisible = false;
            graphPane.Title.IsVisible = true;
            graphPane.AllowCurveOverlap = true;

            Icon = Resources.Skyline;

            PeptideTextBox.Focus();
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

            // If anything is checked, start on the first checked item.
            string[] checkedNames = _driverLibrary.CheckedNames;
            if (checkedNames.Length > 0)
                LibraryComboBox.SelectedItem = checkedNames[0];
            else
            {
                // Set the selection to the very first library in the combobox.
                // The "View Libraries" button is not enabled unless we have at 
                // least ONE library, so if we are here, we must have at least 
                // one to select.

                LibraryComboBox.SelectedIndex = 0;                
            }

            LibraryComboBox.EndUpdate();
        }

        private void UpdateViewLibraryDlg()
        {
            // Order matters!!
            LoadLibrary();
            InitializePeptides();
            _currentRange = BinaryRangeSearch(PeptideTextBox.Text, new Range(0, _peptides.Length - 1));
            UpdatePageInfo();
            UpdateStatusArea();
            UpdatePeptideListBox();
            PeptideTextBox.Select();
            UpdateUI();
        }

        // Loads the library selected in the Library combobox. First, we check
        // to see if the library is already loaded in the main window. If yes,
        // great, we can just use that. If no, we need to use the LongWaitDlg
        // as the IProgressMonitor to load the library from the LibrarySpec.
        private void LoadLibrary()
        {
            LibrarySpec selectedLibrarySpec = _driverLibrary.List[LibraryComboBox.SelectedIndex];
            if (_selectedLibrary != null)
                _selectedLibrary.ReadStream.CloseStream();
            _selectedLibrary = _libraryManager.TryGetLibrary(selectedLibrarySpec);
            if (_selectedLibrary == null)
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
            var lookupPool = new List<byte>();
            _peptides = new PepInfo[_selectedLibrary != null ? _selectedLibrary.Count : 0];
            if (_selectedLibrary != null)
            {
                int index = 0;
                foreach (var libKey in _selectedLibrary.Keys)
                {
                    _peptides[index] = new PepInfo(libKey, lookupPool);
                    index++;
                }
                Array.Sort(_peptides, new PepInfoComparer(lookupPool));
            }

            _lookupPool = lookupPool.ToArray();
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

        // Gets the display string for the given PepInfo object, minus the
        // modification characters.
        private string GetUnmodifiedDisplayString(PepInfo pep)
        {
            return pep.GetAASequence(_lookupPool) + Transition.GetChargeIndicator(pep.Charge);
        }

        // Computes each ModificationInfo for the given peptide and returns a 
        // list of all modifications.
        private static IList<ModificationInfo> GetModifications(PepInfo pep)
        {
            IList<ModificationInfo> modList = new List<ModificationInfo>();
            string sequence = pep.Sequence;
            foreach (Match m in Regex.Matches(sequence, REGEX_MODIFICATION_PATTERN))
            {
                String str = m.ToString();

                // Strip the "[" and "]" characters 
                str = str.Substring(1, str.Length - 2);

                // Look up the modified amino acid preceding the "["
                var aminoAcid = new char[1];
                sequence.CopyTo(m.Index - 1, aminoAcid, 0, 1);

                // Make sure we have a valid modification pattern.
                // It should be a "+" or "-" followed by a number.
                if (Regex.IsMatch(str, REGEX_VALID_MODIFICATION))
                {
                    modList.Add(new ModificationInfo(aminoAcid[0], Double.Parse(str)));
                }
            }
            return modList;
        }
 
        // Compares the display string minus the modification characters for 
        // the given peptide with the string passed in.
        private int Compare(PepInfo pep, string s)
        {
            return string.Compare(GetUnmodifiedDisplayString(pep), 0, s, 0, s.Length, true);
        }

        // Checks to see if the display string minus the modification
        // characters starts with the string passed in. 
        private bool IsMatch(PepInfo pep, string s)
        {
            return GetUnmodifiedDisplayString(pep).StartsWith(s, StringComparison.InvariantCultureIgnoreCase);
        }

        // Uses binary search to find the range of peptides in the list
        // that match the string passed in. If none is found, a range of
        // (-1, -1) is returned.
        private Range BinaryRangeSearch(string s, Range rangeIn)
        {
            // First try to find a single match for s within the peptides list
            var searchRange = new Range(rangeIn);
            if (s.Length > 0)
            {
                bool found = false;
                int mid = 0;
                while (searchRange.StartIndex <= searchRange.EndIndex)
                {
                    mid = (searchRange.StartIndex + searchRange.EndIndex) / 2;
                    int compResult = Compare(_peptides[mid], s);
                    if (compResult < 0)
                    {
                        searchRange.StartIndex = mid + 1;
                    }
                    else if (compResult > 0)
                    {
                        searchRange.EndIndex = mid - 1;
                    }
                    else
                    {
                        // We've found a single match
                        found = true;
                        break;
                    }
                }

                // We'll return (-1, -1) if nothing was found
                searchRange.StartIndex = -1;
                searchRange.EndIndex = -1;

                if (found)
                {
                    // Now that we've found a single match, we first need to
                    // walk up the array and find each matching peptide above
                    // it in the peptides list.
                    searchRange.StartIndex = mid;
                    int previousIndex = searchRange.StartIndex - 1;
                    while ((previousIndex >= rangeIn.StartIndex) &&
                            IsMatch(_peptides[previousIndex],s))
                    {
                        searchRange.StartIndex = previousIndex;
                        previousIndex--;
                    }

                    // Next, we walk down the array and find each matching 
                    // peptide below it in the peptides list.
                    searchRange.EndIndex = mid;
                    int nextIndex = searchRange.EndIndex + 1;
                    while ((nextIndex <= rangeIn.EndIndex) &&
                            IsMatch(_peptides[nextIndex],s))
                    {
                        searchRange.EndIndex = nextIndex;
                        nextIndex++;
                    }
                }
            }

            return searchRange;
        }

        // Gets the index of the peptide in the peptides array for the selected
        // peptide in the peptides listbox. Returns -1 if none is found.
        private int GetIndexOfSelectedPeptide()
        {
            if (_currentRange.Count > 0)
            {
                string selPeptide = PeptideListBox.SelectedItem.ToString();
                string selPeptideAASequence = Regex.Replace(selPeptide, REGEX_MODIFICATION_PATTERN, string.Empty);

                Range selPeptideRange = BinaryRangeSearch(selPeptideAASequence, new Range(_currentRange));
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
            _currentRange = BinaryRangeSearch(PeptideTextBox.Text, new Range(0, _peptides.Length - 1));
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

        public void BuildSpectrumMenu(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip)
        {
            // Store original menuitems in an array, and insert a separator
            var items = new ToolStripItem[menuStrip.Items.Count];
            int iUnzoom = -1;
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = menuStrip.Items[i];
                var tag = (string)items[i].Tag;
                if (tag == "unzoom")
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
            precursorIonContextMenuItem.Checked = set.ShowPrecursorIon;
            menuStrip.Items.Insert(iInsert++, precursorIonContextMenuItem);
            menuStrip.Items.Insert(iInsert++, toolStripSeparator11);
            charge1ContextMenuItem.Checked = set.ShowCharge1;
            menuStrip.Items.Insert(iInsert++, charge1ContextMenuItem);
            charge2ContextMenuItem.Checked = set.ShowCharge2;
            menuStrip.Items.Insert(iInsert++, charge2ContextMenuItem);
            menuStrip.Items.Insert(iInsert++, toolStripSeparator12);
            ranksContextMenuItem.Checked = set.ShowRanks;
            menuStrip.Items.Insert(iInsert++, ranksContextMenuItem);
            duplicatesContextMenuItem.Checked = set.ShowDuplicateIons;
            menuStrip.Items.Insert(iInsert++, duplicatesContextMenuItem);
            menuStrip.Items.Insert(iInsert++, toolStripSeparator13);
            lockYaxisContextMenuItem.Checked = set.LockYAxis;
            menuStrip.Items.Insert(iInsert++, lockYaxisContextMenuItem);
            menuStrip.Items.Insert(iInsert++, toolStripSeparator14);
            menuStrip.Items.Insert(iInsert++, spectrumPropsContextMenuItem);
            menuStrip.Items.Insert(iInsert, toolStripSeparator15);

            // Remove some ZedGraph menu items not of interest
            foreach (var item in items)
            {
                var tag = (string)item.Tag;
                if (tag == "set_default" || tag == "show_val")
                    menuStrip.Items.Remove(item);
            }
            CopyEmfToolStripMenuItem.AddToContextMenu(graphControl, menuStrip);
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

        public void ZoomSpectrumToSettings()
        {
            var axis = GraphPane.XAxis;
            var instrument = Program.ActiveDocumentUI.Settings.TransitionSettings.Instrument;
            axis.Scale.Min = instrument.MinMz;
            axis.Scale.MinAuto = false;
            axis.Scale.Max = instrument.MaxMz;
            axis.Scale.MaxAuto = false;
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
                    SpectrumPeaksInfo spectrum;
                    if (_selectedLibrary.TryLoadSpectrum(_peptides[index].Key, out spectrum))
                    {
                        SrmSettings settings =  Program.ActiveDocumentUI.Settings;
                        var peptide = new Peptide(null, _peptides[index].GetAASequence(_lookupPool), null, null, 0);
                        const IsotopeLabelType isotopeLabelType = IsotopeLabelType.light;
                        var group = new TransitionGroup(peptide, _peptides[index].Charge, isotopeLabelType);

                        var types = ShowIonTypes;
                        var charges = ShowIonCharges;
                        var rankTypes = settings.TransitionSettings.Filter.IonTypes;
                        var rankCharges = settings.TransitionSettings.Filter.ProductCharges;

                        ExplicitMods mods = null;
                        if (_peptides[index].IsModified)
                        {
                            IList<ExplicitMod> staticModList = new List<ExplicitMod>();
                            IList<ExplicitMod> heavyModList = new List<ExplicitMod>();
                            IList<ModificationInfo> modList = GetModifications(_peptides[index]);
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

            aionsButton.Checked = Settings.Default.ShowAIons;
            bionsButton.Checked = Settings.Default.ShowBIons;
            cionsButton.Checked = Settings.Default.ShowCIons;
            xionsButton.Checked = Settings.Default.ShowXIons;
            yionsButton.Checked = Settings.Default.ShowYIons;
            zionsButton.Checked = Settings.Default.ShowZIons;
            charge1Button.Checked = Settings.Default.ShowCharge1;
            charge2Button.Checked = Settings.Default.ShowCharge2;
        }

        private void LibraryComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // The user has selected a different library. We need to reload 
            // everything in the dialog. 
            UpdateViewLibraryDlg();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            CancelDialog();
        }

        public void CancelDialog()
        {
            DialogResult = DialogResult.Cancel;
            Application.DoEvents();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_selectedLibrary != null)
                _selectedLibrary.ReadStream.CloseStream();
            base.OnClosed(e);
        }

        private void graphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            BuildSpectrumMenu(sender, menuStrip);
        }

        private void PeptideTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up)
            {
                e.Handled = true;
                if (PeptideListBox.SelectedIndex > 0)
                {
                    PeptideListBox.SelectedIndex--;
                }
            }
            else if (e.KeyCode == Keys.Down)
            {
                e.Handled = true;
                if ((PeptideListBox.SelectedIndex + 1) < PeptideListBox.Items.Count)
                {
                    PeptideListBox.SelectedIndex++;
                }
            }
        }

        private void aionsContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowAIons = !Settings.Default.ShowAIons;
            UpdateUI();
        }

        private void bionsContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowBIons = !Settings.Default.ShowBIons;
            UpdateUI();
        }

        private void cionsContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowCIons = !Settings.Default.ShowCIons;
            UpdateUI();
        }

        private void xionsContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowXIons = !Settings.Default.ShowXIons;
            UpdateUI();
        }

        private void yionsContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowYIons = !Settings.Default.ShowYIons;
            UpdateUI();
        }

        private void zionsContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowZIons = !Settings.Default.ShowZIons;
            UpdateUI();
        }

        private void precursorIonContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowPrecursorIon = !Settings.Default.ShowPrecursorIon;
            UpdateUI();
        }

        private void charge1ContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowCharge1 = !Settings.Default.ShowCharge1;
            UpdateUI();
        }

        private void charge2ContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowCharge2 = !Settings.Default.ShowCharge2;
            UpdateUI();
        }

        private void ranksContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowRanks = !Settings.Default.ShowRanks;
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
            var dlg = new SpectrumChartPropertyDlg();
            if (dlg.ShowDialog(this) == DialogResult.OK)
                UpdateUI();
        }

        private void zoomSpectrumContextMenuItem_Click(object sender, EventArgs e)
        {
            ZoomSpectrumToSettings();
        }

        private void copyMetafileButton_Click(object sender, EventArgs e)
        {
            CopyEmfToolStripMenuItem.CopyEmf(graphControl);
        }

        private void copyButton_Click(object sender, EventArgs e)
        {
            graphControl.Copy(false);
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            graphControl.SaveAs();
        }

        private void printButton_Click(object sender, EventArgs e)
        {
            graphControl.DoPrint();
        }

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
                else if (c.ContainsFocus)
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
    }
}
