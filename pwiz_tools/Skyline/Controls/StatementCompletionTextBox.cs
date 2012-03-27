using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls
{
    public class StatementCompletionTextBox
    {
        // The last query which returned the statement completion results currently displayed
        private ProteinMatchQuery _proteinMatcherLast;
        // The query that is currently executing
        private ProteinMatchQuery _proteinMatcher;
        private readonly IDocumentUIContainer _documentUiContainer;
        private readonly ImageList _imageList = new ImageList();

        // Don't let the name take more than half the space for item display
        private const int MAX_NAME_LENGTH = 40;

        public StatementCompletionTextBox(IDocumentUIContainer documentUiContainer)
        {
            MatchTypes = ProteinMatchType.all;
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
            if (_proteinMatcher != null)
            {
                _proteinMatcher.Cancel();
                _proteinMatcher = null;
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
                StatementCompletionForm.Show();
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

        public ProteinMatchType MatchTypes { get; set; }
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
                        var newSettings = new ProteinMatchSettings(oldSettings.ProteomeDb, oldSettings.Protease, MatchTypes, searchText);
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
                        DisplayResultsNow(newResults);
                    }
                }
            }

            if (_proteinMatcher != null)
            {
                _proteinMatcher.Cancel();
                _proteinMatcher = null;
            }
            var settings = CreateProteinMatchSettings(_documentUiContainer.DocumentUI, MatchTypes, searchText);
            if (settings != null)
            {
                _proteinMatcher = new ProteinMatchQuery(settings, 100);
                _proteinMatcher.BeginExecute(DisplayResults);
            }
        }

        /// <summary>
        /// Called on a background thread when the statement completion query has some results to display.
        /// </summary>
        private void DisplayResults(ProteinMatchQuery proteinMatchQuery)
        {
            if (TextBox.IsDisposed)
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
            DisplayResultsNow(proteinMatchQuery.GetMatches());
            _proteinMatcherLast = proteinMatchQuery;
        }

        private void DisplayResultsNow(IList<ProteinMatch> matches)
        {
            if (TextBox == null || TextBox.Parent == null)
            {
                return;
            }
            var listItems = CreateListViewItems(matches, TextBox.Text, MatchTypes);
            ShowStatementCompletionForm(listItems);
        }

        protected void TextBox_HandleDestroyed(EventArgs e)
        {
            if (_proteinMatcher != null)
            {
                _proteinMatcher.Cancel();
            }
        }
        public static readonly ImageList IMAGE_LIST = new ImageList();
        private enum ImageId
        {
            protein,
            peptide,
        }
        public static IList<ListViewItem> CreateListViewItems(IList<ProteinMatch> matches, String searchText, ProteinMatchType matchTypes)
        {
            var listItems = new List<ListViewItem>();
            foreach (var match in matches)
            {
                if (0 != (matchTypes & match.MatchType & ProteinMatchType.sequence))
                {
                    foreach (DigestedPeptide digestedPeptide in match.DigestedPeptides)
                    {
                        var listItem = new ListViewItem
                                           {
                                               Text = digestedPeptide.Sequence,
                                               Tag = new StatementCompletionItem
                                                         {
                                                             Peptide = digestedPeptide.Sequence,
                                                             ProteinName = match.Protein.Name
                                                         },
                                           };
                        StatementCompletionForm.AddDescription(listItem,
                                                               match.Protein.Name + " " + match.Protein.Description,
                                                               null);
                        listItem.ImageIndex = (int) ImageId.peptide;
                        var tooltip =
                            new StringBuilder("Descriptions:\n" + match.Protein.Name + " " + match.Protein.Description);
                        foreach (var name in match.Protein.AlternativeNames)
                        {
                            tooltip.Append("\n" + name.Name + " " + name.Description);
                        }
                        listItem.ToolTipText = StripTabs(tooltip.ToString());
                        listItems.Add(listItem);
                    }
                }
            }
            if (listItems.Count > 0)
            {
                return listItems;
            }
            foreach (var match in matches)
            {
                if (0 != (matchTypes & match.MatchType & ProteinMatchType.name))
                {
                    var listItem = new ListViewItem();
                    if (match.AlternativeName != null)
                    {
                        listItem.Text = match.AlternativeName.Name;
                        listItem.Tag = new StatementCompletionItem {ProteinName = match.AlternativeName.Name};
                        StatementCompletionForm.AddDescription(listItem, match.AlternativeName.Description, null);
                    }
                    else
                    {
                        listItem.Text = match.Protein.Name;
                        listItem.Tag = new StatementCompletionItem {ProteinName = match.Protein.Name};
                        StatementCompletionForm.AddDescription(listItem, match.Protein.Description, null);
                    }
                    listItem.ImageIndex = (int) ImageId.protein;
                    var tooltip =
                        new StringBuilder("Descriptions:\n" + match.Protein.Name + " "
                                          + match.Protein.Description);
                    foreach (var altName in match.Protein.AlternativeNames)
                    {
                        tooltip.Append("\n" + altName.Name + " " + altName.Description);
                    }
                    listItem.ToolTipText = StripTabs(tooltip.ToString());
                    listItems.Add(listItem);
                }
            }
            if (listItems.Count > 0)
            {
                return listItems;
            }
            foreach (var match in matches)
            {
                if (0 != (match.MatchType & ProteinMatchType.description))
                {
                    AlternativeName mainName = match.AlternativeDescription;
                    string matchName = match.Protein.Name;
                    if (matchName.Length > MAX_NAME_LENGTH)
                        matchName = matchName.Substring(0, MAX_NAME_LENGTH) + "...";
                    var proteinName = new AlternativeName
                                                      {
                                                          Name = matchName,
                                                          Description = match.Protein.Description
                                                      };
                    var alternativeNames = new List<AlternativeName>();
                    if (mainName == null)
                    {
                        mainName = proteinName;
                    }
                    else
                    {
                        alternativeNames.Add(proteinName);
                    }
                    var listItem = new ListViewItem
                                       {
                                           Text = mainName.Name,
                                           ImageIndex = (int) ImageId.protein,
                                           Tag = new StatementCompletionItem { ProteinName = proteinName.Name }
                                       };

                    StatementCompletionForm.AddDescription(listItem, mainName.Description, searchText);
                    if (match.Protein.AlternativeNames.Count > 0)
                    {
                        alternativeNames.AddRange(match.Protein.AlternativeNames);
                        StringBuilder tooltip = new StringBuilder("Alternative Names:");
                        foreach (var altName in alternativeNames)
                        {
                            if (altName.Name == mainName.Name)
                            {
                                continue;
                            }
                            tooltip.Append("\n" + altName.Name + " " + altName.Description);
                        }
                        listItem.ToolTipText = StripTabs(tooltip.ToString());
                    }
                    listItems.Add(listItem);
                }

            }
            return listItems;
        }
        public static IList<ProteinMatch> RefineMatches(IEnumerable<ProteinMatch> matches, ProteinMatchSettings settings)
        {
            var newMatches = new List<ProteinMatch>();
            foreach (var match in matches)
            {
                var newMatch = new ProteinMatch(settings, match.Protein);
                if (newMatch.MatchType != 0)
                {
                    newMatches.Add(newMatch);
                }
            }
            return newMatches;
        }

        public static ProteinMatchSettings CreateProteinMatchSettings(SrmDocument srmDocument, ProteinMatchType matchTypes, String searchText)
        {
            var peptideSettings = srmDocument.Settings.PeptideSettings;
            var backgroundProteome = peptideSettings.BackgroundProteome;
            if (backgroundProteome.IsNone)
            {
                return null;
            }
            try
            {
                return new ProteinMatchSettings(backgroundProteome.OpenProteomeDb(),
                                            new ProteaseImpl(peptideSettings.Enzyme),
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
            return str.Replace('\t', ' ');
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
        public String ProteinName { get; set; }
        public String Peptide { get; set; }
        public override string ToString()
        {
            if (Peptide == null)
            {
                return ProteinName;
            }
            return Peptide + FastaSequence.PEPTIDE_SEQUENCE_SEPARATOR + ProteinName;
        }
    }
}
