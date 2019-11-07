﻿/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls
{
    public class StatementCompletionTextBox
    {
        // The last query which returned the statement completion results currently displayed
        private ProteinMatchQuery _proteinMatcherLast;
        // The query that is currently executing
        private ProteinMatchQuery _proteinMatcher;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly IDocumentUIContainer _documentUiContainer;
        private readonly ImageList _imageList = new ImageList() {TransparentColor = Color.Magenta};
        private ProteomeDb _proteomeDb;

        // Don't let the name take more than half the space for item display
        private const int MAX_NAME_LENGTH = 40;

        public StatementCompletionTextBox(IDocumentUIContainer documentUiContainer)
        {
            MatchTypes = ProteinMatchTypes.ALL;
            _documentUiContainer = documentUiContainer;
            _imageList.Images.Add(Resources.Protein);
            _imageList.Images.Add(Resources.Peptide);
        }

        public void Attach(TextBox textBox)
        {
            Detach();
            TextBox = textBox;
            if (TextBox == null)
            {
                return;
            }                
            TextBox.KeyDown += TextBox_KeyDown;
            TextBox.TextChanged += TextBox_TextChanged;
            TextBox.GotFocus += TextBox_GotFocus;
            TextBox.LostFocus += TextBox_LostFocus;
            TextBox.LocationChanged += TextBox_LocationChanged;
        }

        public void Detach()
        {
            if (TextBox == null)
            {
                return;
            }
            HideStatementCompletionForm();
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource = null;
            }
            _proteinMatcher = null;
            if (null != _proteomeDb)
            {
                _proteomeDb.Dispose();
                _proteomeDb = null;
            }
            TextBox.KeyDown -= TextBox_KeyDown;
            TextBox.TextChanged -= TextBox_TextChanged;
            TextBox.GotFocus -= TextBox_GotFocus;
            TextBox.LostFocus -= TextBox_LostFocus;
            TextBox.LocationChanged -= TextBox_LocationChanged;
            TextBox = null;
        }

        public TextBox TextBox { get; private set; }

        void HideStatementCompletionFormIfLostFocus()
        {
            if (TextBox == null || TextBox.Focused)
            {
                return;
            }
            if (StatementCompletionForm != null)
            {
                if (StatementCompletionForm.Focused || StatementCompletionForm.ListView.Focused)
                {
                    return;
                }
                StatementCompletionForm.Hide();
            }
            if (TextBoxLoseFocus != null)
            {
                TextBoxLoseFocus.Invoke();
            }
        }

        protected void TextBox_GotFocus(object sender, EventArgs e)
        {
            if (StatementCompletionForm != null && !StatementCompletionForm.Visible)
            {
                StatementCompletionForm.Show(FormEx.GetParentForm(TextBox));
                StatementCompletionForm.ResizeToIdealSize(ScreenRect);
            }
            else
            {
                DoStatementCompletion();   
            }
        }

        void TextBox_LostFocus(Object sender, EventArgs e)
        {
            TextBox.BeginInvoke(new Action(HideStatementCompletionFormIfLostFocus));
        }

        public StatementCompletionForm StatementCompletionForm { get; private set; }
        void TextBox_LocationChanged(object sender, EventArgs e)
        {
            if (StatementCompletionForm != null)
            {
                StatementCompletionForm.ResizeToIdealSize(ScreenRect);
            }
        }

        void TextBox_KeyDown(Object sender, KeyEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }
            switch (e.KeyCode)
            {
                case Keys.Tab:
                case Keys.Enter:
                    if (StatementCompletionForm != null)
                    {
                        var currentItem = StatementCompletionForm.ListView.FocusedItem;
                        if (currentItem != null)
                        {
                            if (e.KeyCode == Keys.Enter)
                            {
                                e.Handled = true;
                                e.SuppressKeyPress = true;
                            }
                            OnSelectionMade((StatementCompletionItem) currentItem.Tag);
                        }
                    }
                    break;
                case Keys.Down:
                    if (StatementCompletionForm != null)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        StatementCompletionForm.SelectNextItem(true);
                    }
                    break;
                case Keys.Up:
                    if (StatementCompletionForm != null)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        StatementCompletionForm.SelectNextItem(false);
                    }
                    break;
                case Keys.Escape:
                    if (StatementCompletionForm != null)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        HideStatementCompletionForm();
                    }
                    break;
                case Keys.V:
                    if (e.Control)
                    {
                        TextBox.Paste();
                        e.Handled = e.SuppressKeyPress = true;
                    }
                    break;
                case Keys.C:
                    if (e.Control)
                    {
                        TextBox.Copy();
                        e.Handled = e.SuppressKeyPress = true;
                    }
                    break;
                case Keys.X:
                    if (e.Control)
                    {
                        TextBox.Cut();
                        e.Handled = e.SuppressKeyPress = true;
                    }
                    break;
                case Keys.Z:
                    if (e.Control)
                    {
                        TextBox.Undo();
                        e.Handled = e.SuppressKeyPress = true;
                    }
                    break;
            }
        }

        public void OnKeyPreview(object sender, KeyEventArgs keyEventArgs)
        {
            if (TextBox != null && TextBox.Focused 
                || StatementCompletionForm != null && StatementCompletionForm.Focused)
            {
                TextBox_KeyDown(sender, keyEventArgs);
            }
        }

        void TextBox_TextChanged(Object sender, EventArgs e)
        {
            DoStatementCompletion();
            if (AutoSizeWidth)
            {
                AutoResize();
            }
        }

        public int MinimumWidth { get; set; }
        public int MaximumWidth { get; set; }

        public void AutoResize()
        {
            int dx = TextRenderer.MeasureText(TextBox.Text, TextBox.Font, new Size(TextBox.Height, MaximumWidth)).Width + 8;
            dx = Math.Max(dx, MinimumWidth);
            dx = Math.Min(dx, MaximumWidth);
            TextBox.Width = dx;
        }

        public Rectangle ScreenRect
        {
            get
            {
                return TextBox.Parent.RectangleToScreen(TextBox.Bounds);
            }
        }

        public ProteinMatchTypes MatchTypes { get; set; }
        public bool AutoSizeWidth { get; set; }
        public bool DisableCompletion { get; set; }

        public void HideStatementCompletionForm()
        {
            if (StatementCompletionForm != null)
            {
                StatementCompletionForm.Close();
                StatementCompletionForm = null;
            }
        }
        /// <summary>
        /// Show the statement completion popup with the list of items.
        /// </summary>
        public void ShowStatementCompletionForm(IList<ListViewItem> items)
        {
            if (items.Count == 0)
            {
                HideStatementCompletionForm();
                return;
            }
            bool show = false;
            if (StatementCompletionForm == null)
            {
                StatementCompletionForm = new StatementCompletionForm { TextBox = TextBox };
                StatementCompletionForm.ListView.MouseDown += ListView_MouseDown;
                StatementCompletionForm.ListView.SmallImageList = _imageList;
                show = true;
            }
            StatementCompletionForm.SetListItems(items);
            StatementCompletionForm.ResizeToIdealSize(ScreenRect);
            if (show)
                StatementCompletionForm.Show(TextBox);
        }

        public void ListView_MouseDown(object sender, MouseEventArgs e)
        {
            // The list view doesn't believe items extend all across the ListView, so we use
            // 17 as the x-coordinate, since Windows XP doesn't count the icon as part of
            // the item either.
            var item = StatementCompletionForm.ListView.GetItemAt(17, e.Location.Y);
            if (item == null)
            {
                return;
            }
            OnSelectionMade((StatementCompletionItem) item.Tag);
        }

        /// <summary>
        /// When the text in the editTextBox changes, fire off the query to populate the statement completion popup.
        /// </summary>
        private void DoStatementCompletion()
        {
            if (DisableCompletion)
                return;

            String searchText = TextBox.Text;
            if (searchText.Length == 0)
            {
                HideStatementCompletionForm();
                return;
            }
            if (_proteinMatcherLast != null)
            {
                //if (_proteinMatcherLast.Settings.SearchText == searchText)
                //{
                //    // Query is already executing for the text that the user typed: no work to do now.
                //    return;
                //}
                if (searchText.StartsWith(_proteinMatcherLast.Settings.SearchText))
                {
                    // If the new text is just longer than the previous text, see if we can just refine the results
                    // of the previous query.
                    var results = _proteinMatcherLast.GetMatches();
                    var newResults = new List<ProteinMatch>();
                    if (results != null)
                    {
                        var oldSettings = _proteinMatcherLast.Settings;
                        var newSettings = new ProteinMatchSettings(oldSettings.ProteomeDbPath, MatchTypes, searchText);
                        newResults.AddRange(RefineMatches(results, newSettings));
                    }
                    if (newResults.Count == 0)
                    {
                        // Nothing from the previous query matches: hide the statement completion form.
                        HideStatementCompletionForm();
                        _proteinMatcherLast = null;
                    }
                    else
                    {
                        // Display the refined results: we'll still fire off another query to see if we get better results.
                        DisplayResultsNow(newResults, _proteinMatcherLast.MaxResults);
                    }
                }
            }

            if (_cancellationTokenSource != null)
            {
                _proteinMatcher = null;
                _cancellationTokenSource.Cancel();
            }
            _cancellationTokenSource = new CancellationTokenSource();
            var settings = CreateProteinMatchSettings(_documentUiContainer.DocumentUI, MatchTypes, searchText);
            if (settings != null)
            {
                _proteinMatcher = new ProteinMatchQuery(settings, _cancellationTokenSource.Token);
                _proteinMatcher.BeginExecute(DisplayResults);
            }
        }

        /// <summary>
        /// Called on a background thread when the statement completion query has some results to display.
        /// </summary>
        private void DisplayResults(ProteinMatchQuery proteinMatchQuery)
        {
            if (TextBox == null || TextBox.IsDisposed)
            {
                return;
            }
            TextBox.BeginInvoke(new Action<ProteinMatchQuery>(DisplayResultsNow), proteinMatchQuery);
        }

        /// <summary>
        /// Updates the StatementCompletion popup to show the list of matching results.
        /// </summary>
        private void DisplayResultsNow(ProteinMatchQuery proteinMatchQuery)
        {
            if (proteinMatchQuery != _proteinMatcher)
            {
                return;
            }
            DisplayResultsNow(proteinMatchQuery.GetMatches(), proteinMatchQuery.MaxResults);
            _proteinMatcherLast = proteinMatchQuery;
        }

        private void DisplayResultsNow(IList<ProteinMatch> matches, int maxResults)
        {
            if (TextBox == null || TextBox.Parent == null)
            {
                return;
            }
            var listItems = CreateListViewItems(matches, TextBox.Text, MatchTypes, 
                _documentUiContainer.DocumentUI.Settings.PeptideSettings, maxResults);
            ShowStatementCompletionForm(listItems);
        }

        protected void TextBox_HandleDestroyed(EventArgs e)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource = null;
            }
        }
        public static readonly ImageList IMAGE_LIST = new ImageList();
        private enum ImageId
        {
            protein,
            peptide,
        }

        /// <summary>
        /// Create the ordered list of dropdown items, with a rational sort order:
        ///   First present any sequence matches, sorted by sequence
        ///   Then present any name, accession, gene, preferredname, or species metadata matches, sorted by the matched metadata field
        ///   Then present any matches against the description field, sorted by the description text starting at the match location
        /// </summary>
        [MethodImpl(MethodImplOptions.NoOptimization)] // TODO(nicksh): reenable optimizations after we track down a NullReferenceException
        public static IList<ListViewItem> CreateListViewItems(IList<ProteinMatch> matches, String searchText, ProteinMatchTypes matchTypes, PeptideSettings peptideSettings, int maxCount)
        {
            var listItems = new SortedList<string,ListViewItem>();
            var setUsedMatches = new HashSet<string>();

            // First check for matching by sequence
            foreach (var match in matches)
            {
                if (matchTypes.Contains(ProteinMatchType.sequence))
                {
                    HashSet<String> addedPeptideSequences = new HashSet<string>();
                    FastaSequence fastaSequence;
                    try
                    {
                        fastaSequence = new FastaSequence(@"name", @"description", new ProteinMetadata[0], match.Protein.Sequence);
                    }
                    catch (InvalidDataException)
                    {
                        // It's possible that the peptide sequence in the fasta file was bogus, in which case we just don't digest it.
                        continue;
                    }
                    foreach (Peptide peptide in peptideSettings.Enzyme.Digest(fastaSequence, peptideSettings.DigestSettings))
                    {
                        if (!peptide.Sequence.StartsWith(searchText))
                        {
                            continue;
                        }
                        if (!addedPeptideSequences.Add(peptide.Sequence))
                        {
                            continue;
                        }
                        var listItem = new ListViewItem
                        {
                            Text = peptide.Sequence,
                            Tag = new StatementCompletionItem
                            {
                                Peptide = peptide.Sequence,
                                ProteinInfo = match.Protein.ProteinMetadata,
                                SearchText = searchText
                            },
                        };
                        StatementCompletionForm.AddDescription(listItem,
                                                               match.Protein.ProteinMetadata.TextForMatchTypes(matchTypes),
                                                               null);
                        setUsedMatches.Add(match.Protein.Name);
                        listItem.ImageIndex = (int) ImageId.peptide;
                        var tooltip = new StringBuilder();
                        tooltip.AppendLine(Resources.StatementCompletionTextBox_CreateListViewItems_Descriptions)
                               .Append(match.Protein.ProteinMetadata.TextForMatchTypes(matchTypes));
                        foreach (var name in match.Protein.AlternativeNames)
                        {
                            tooltip.AppendLine().Append(name.TextForMatchTypes(matchTypes));
                        }
                        listItem.ToolTipText = StripTabs(tooltip.ToString());
                        // Note the leading space in this sort key - we'd like to list sequence matches first
                        var key = TextUtil.SpaceSeparate(@" ", listItem.Text, listItem.ToolTipText);
                        if (!listItems.ContainsKey(key))
                            listItems.Add(key, listItem);
                    }
                }
            }
            if (listItems.Count >= maxCount)
            {
                return new List<ListViewItem>(listItems.Values);  // We used to exit here if we had any matches - but that's frustrating when you're not actually trying to match by sequence
            }

            // Decide which field not to display on righthand side, based on what's already showing on the left due to View|Targets|By* menu
            ProteinMatchTypes displayMatchTypes = ProteinMatchTypes.ALL;
            switch (SequenceTree.ProteinsDisplayMode)
            {
                case ProteinMetadataManager.ProteinDisplayMode.ByName:
                    displayMatchTypes = displayMatchTypes.Except(ProteinMatchType.name);
                    break;
                case ProteinMetadataManager.ProteinDisplayMode.ByAccession:
                    displayMatchTypes = displayMatchTypes.Except(ProteinMatchType.accession);
                    break;
                case ProteinMetadataManager.ProteinDisplayMode.ByGene:
                    displayMatchTypes = displayMatchTypes.Except(ProteinMatchType.gene);
                    break;
                case ProteinMetadataManager.ProteinDisplayMode.ByPreferredName:
                    displayMatchTypes = displayMatchTypes.Except(ProteinMatchType.preferredName);
                    break;
            }

            ProteinMatchTypes secondPassMatchTypes = matchTypes.Except(
                ProteinMatchType.sequence, // We already did sequence 
                ProteinMatchType.description); // And aren't ready for description
            foreach (var match in matches)
            {
                if (setUsedMatches.Contains(match.Protein.Name))
                {
                    continue;
                }
                // Try matching on name, accession etc - cycle through name, accession, preferredName, gene
                foreach (ProteinMatchType tryType in secondPassMatchTypes)
                {
                    if (match.MatchTypes.Contains(tryType))
                    {
                        var listItem = new ListViewItem();
                        // Show description, and any other fields we were searching on
                        if (match.AlternativeName != null)
                        {
                            listItem.Text = ProteinMetadataManager.ProteinModalDisplayText(match.AlternativeName, Settings.Default.ShowPeptidesDisplayMode);
                            listItem.Tag = new StatementCompletionItem {ProteinInfo = match.AlternativeName, SearchText = searchText};
                            StatementCompletionForm.AddDescription(listItem,
                                match.AlternativeName.TextForMatchTypes(displayMatchTypes.Except(ProteinMatchType.name)), searchText);
                        }
                        else
                        {
                            listItem.Text = ProteinMetadataManager.ProteinModalDisplayText(match.Protein.ProteinMetadata, Settings.Default.ShowPeptidesDisplayMode);
                            listItem.Tag = new StatementCompletionItem { ProteinInfo = match.Protein.ProteinMetadata, SearchText = searchText };
                            StatementCompletionForm.AddDescription(listItem,
                                match.Protein.ProteinMetadata.TextForMatchTypes(displayMatchTypes), searchText);
                        }
                        setUsedMatches.Add(match.Protein.Name);
                        listItem.ImageIndex = (int) ImageId.protein;
                        var tooltip = new StringBuilder();
                        tooltip.AppendLine(Resources.StatementCompletionTextBox_CreateListViewItems_Descriptions)
                            .Append(match.Protein.ProteinMetadata.TextForMatchTypes(displayMatchTypes));
                        foreach (var altName in match.Protein.AlternativeNames)
                        {
                            tooltip.AppendLine().Append(altName.TextForMatchTypes(displayMatchTypes));
                        }
                        listItem.ToolTipText = StripTabs(tooltip.ToString());
                        // We want the sort to be on the particular bit of metadata that we matched
                        var key = TextUtil.SpaceSeparate(match.Protein.ProteinMetadata.TextForMatchTypes(ProteinMatchTypes.Singleton(tryType)), 
                            listItem.Text, listItem.ToolTipText);
                        if (!listItems.ContainsKey(key))
                            listItems.Add(key, listItem);
                        break;  
                    }
                }
            }
            if (listItems.Count >= maxCount)
            {
                return new List<ListViewItem>(listItems.Values);  // We used to exit here if we had any matches - but that's frustrating when you're not actually trying to match by sequence
            }

            // Any matches by description?
            foreach (var match in matches)
            {
                if (setUsedMatches.Contains(match.Protein.Name))
                {
                    continue;
                }
                if (match.MatchTypes.Contains(ProteinMatchType.description))
                {
                    ProteinMetadata mainName = match.AlternativeDescription;
                    string matchName = match.Protein.Name;
                    var proteinInfo = match.Protein.ProteinMetadata;
                    if (matchName != null && matchName.Length > MAX_NAME_LENGTH)
                        proteinInfo = proteinInfo.ChangeName(matchName.Substring(0, MAX_NAME_LENGTH) + @"...");
                    var alternativeNames = new List<ProteinMetadata>();
                    if (mainName == null)
                    {
                        mainName = proteinInfo;
                    }
                    else
                    {
                        alternativeNames.Add(proteinInfo);
                    }
                    var listItem = new ListViewItem
                                       {
                                           Text = ProteinMetadataManager.ProteinModalDisplayText(mainName, Settings.Default.ShowPeptidesDisplayMode),
                                           ImageIndex = (int) ImageId.protein,
                                           Tag = new StatementCompletionItem { ProteinInfo = proteinInfo, SearchText = searchText }
                                       };

                    StatementCompletionForm.AddDescription(listItem, mainName.TextForMatchTypes(displayMatchTypes), searchText);
                    if (match.Protein.AlternativeNames.Count > 0)
                    {
                        alternativeNames.AddRange(match.Protein.AlternativeNames);
                        StringBuilder tooltip = new StringBuilder(Resources.StatementCompletionTextBox_CreateListViewItems_Alternative_Names);
                        foreach (var altName in alternativeNames)
                        {
                            if (altName.Name == mainName.Name)
                            {
                                continue;
                            }

                            tooltip.AppendLine().Append(altName.TextForMatchTypes(displayMatchTypes.Union(ProteinMatchType.name)));
                        }
                        listItem.ToolTipText = StripTabs(tooltip.ToString());
                    }
                    // We want the sort to be on what we matched in the description, and what follows.
                    var remains = match.Protein.ProteinMetadata.Description ?? string.Empty;
                    int pos = remains.ToLower().IndexOf(searchText.ToLower(), StringComparison.Ordinal);
                    if (pos > 0)
                        remains = remains.Substring(pos);
                    var key = TextUtil.SpaceSeparate(remains, listItem.Text, listItem.ToolTipText);
                    if (!listItems.ContainsKey(key))
                        listItems.Add(key, listItem);
                }

            }
            return new List<ListViewItem>(listItems.Values);
        }
        public static IList<ProteinMatch> RefineMatches(IEnumerable<ProteinMatch> matches, ProteinMatchSettings settings)
        {
            var newMatches = new List<ProteinMatch>();
            foreach (var match in matches)
            {
                var newMatch = new ProteinMatch(settings, match.Protein);
                if (!newMatch.MatchTypes.IsEmpty)
                {
                    newMatches.Add(newMatch);
                }
            }
            return newMatches;
        }

        private ProteinMatchSettings CreateProteinMatchSettings(SrmDocument srmDocument, ProteinMatchTypes matchTypes, String searchText)
        {
            var peptideSettings = srmDocument.Settings.PeptideSettings;
            var backgroundProteome = peptideSettings.BackgroundProteome;
            if (backgroundProteome.IsNone)
            {
                return null;
            }
            try
            {
                if (_proteomeDb != null && _proteomeDb.Path != backgroundProteome.DatabasePath)
                {
                    _proteomeDb.Dispose();
                    _proteomeDb = null;
                }
                if (_proteomeDb == null)
                {
                    _proteomeDb = backgroundProteome.OpenProteomeDb(_cancellationTokenSource.Token);
                }
                return new ProteinMatchSettings(_proteomeDb.ProteomeDbPath,
                                            matchTypes,
                                            searchText);
            }
            catch (SQLiteException)
            {
                // CONSIDER: Silent failure could be confusing.  Show a message box
                //           about failing to open the database.
                return null;
            }
        }

        private static String StripTabs(String str)
        {
            return str.Replace(TextUtil.SEPARATOR_TSV, TextUtil.SEPARATOR_SPACE);
        }

        public void OnSelectionMade(StatementCompletionItem statementCompletionItem)
        {
            if (SelectionMade != null)
            {
                SelectionMade.Invoke(statementCompletionItem);
            }
        }

        public event Action<StatementCompletionItem> SelectionMade;
        public event Action TextBoxLoseFocus;
    }

    public class StatementCompletionItem
    {
        public ProteinMetadata ProteinInfo { get; set; }
        public String Peptide { get; set; }
        public String SearchText { get; set; }
        public override string ToString()
        {
            if (Peptide == null)
            {
                return ProteinInfo.Name;
            }
            return Peptide + FastaSequence.PEPTIDE_SEQUENCE_SEPARATOR + ProteinInfo.Name;
        }
    }
}
