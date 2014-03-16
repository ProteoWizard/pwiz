/*
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
using System.Drawing;
using System.Windows.Forms;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    public partial class StatementCompletionForm : FormEx
    {
        public StatementCompletionForm()
        {
            InitializeComponent();
            MaxDisplayItems = 11;
            MaxWidth = 1000;
        }
        public ListView ListView { get { return listView; } }
        protected override bool ShowWithoutActivation
        {
            get
            {
                return true;
            }
        }

        public TextBox TextBox { get; set; }

        public int MaxWidth { get; set; }
        public int MaxDisplayItems { get; set; }
        public int TitleWidth { get; set; }

        /// <summary>
        /// Resizes the form so that it is just wide enough to show the list items.
        /// </summary>
        public void ResizeToIdealSize(Rectangle parentRectangle)
        {
            const int dyPadding = 2;
            var screenRect = Screen.FromRectangle(parentRectangle).WorkingArea;
            int dxRequired = 0;
            const int dxIcon = 32;
            int dxAvailable = screenRect.Width;
            int dxTitle = 0;
            using (var titleFont = CreateListViewFont())
            {
                foreach (ListViewItem item in ListView.Items)
                {
                    int dxItem = TextRenderer.MeasureText(item.Text, titleFont).Width;
                    // Add 16 pixels to the width of the item so that the title and description are separated.
                    dxItem += 16;
                    dxTitle = Math.Max(dxTitle, dxItem);
                    dxItem += dxIcon;
                    var description = GetDescription(item) ?? string.Empty;
                    dxItem += TextRenderer.MeasureText(description, ListView.Font).Width;
                    dxRequired = Math.Max(dxRequired, dxItem);
                }
            }
            TitleWidth = dxTitle;
            bool displayAbove = false;
            int dyAvailable = screenRect.Bottom - parentRectangle.Bottom;
            int itemHeight = ListView.Items[0].Bounds.Height;
            if (dyAvailable < MaxDisplayItems * itemHeight + dyPadding)
            {
                if (parentRectangle.Top - screenRect.Top > dyAvailable)
                {
                    displayAbove = true;
                    dyAvailable = parentRectangle.Top - screenRect.Top;
                }
            }

            int dyRequired = itemHeight * Math.Min(MaxDisplayItems, ListView.Items.Count) + dyPadding;
            int columnDelta = 0;
            if (dyRequired > dyAvailable || MaxDisplayItems < ListView.Items.Count)
            {
                columnDelta = SystemInformation.VerticalScrollBarWidth;
                dxRequired += columnDelta;
            }
            Size = new Size(Math.Min(dxRequired, dxAvailable), Math.Min(dyRequired, dyAvailable));
            Location = displayAbove
                ? new Point(Math.Min(parentRectangle.Left, screenRect.Right - Size.Width), parentRectangle.Top - Size.Height)
                : new Point(Math.Min(parentRectangle.Left, screenRect.Right - Size.Width), parentRectangle.Bottom);
            // Set the width of the ListView's name column-- this is the width that listView_DrawItem
            // draws into.
            columnName.Width = Size.Width - 2 - columnDelta;
        }

        private Font CreateListViewFont()
        {
            return new Font(ListView.Font, FontStyle.Regular);
        }

        /// <summary>
        /// Event handler for the ListView's DrawItem event.
        /// Draws the list item.  This had to be owner draw because a ListView does not
        /// highlight the currently selected item if it does not have the focus.
        /// </summary>
        private void listView_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            DrawItem(e.Graphics, e.Item);
        }

        private void DrawItem(Graphics graphics, ListViewItem item)
        {
            var bounds = item.Bounds;
            graphics.FillRectangle(SystemBrushes.Window, bounds);
            int dxIcon = bounds.Height;
            graphics.DrawImageUnscaled(item.ImageList.Images[item.ImageIndex], bounds.Left, bounds.Top, dxIcon,
                                         bounds.Height);
            var textBounds = new Rectangle(bounds.Left + dxIcon, bounds.Top,
                bounds.Width - dxIcon, bounds.Height);
            Color textColor = ListView.ForeColor;
            Color backColor = ListView.BackColor;
            if (item.Focused)
            {
                graphics.FillRectangle(SystemBrushes.Highlight, textBounds);
                textColor = SystemColors.HighlightText;
                backColor = SystemColors.Highlight;
            }

            var titleBounds = new Rectangle(textBounds.Left, textBounds.Top, TitleWidth, textBounds.Height);
            DrawWithHighlighting(item.Text, GetHighlightedText(item) ?? string.Empty,
                    graphics, titleBounds, textColor, backColor);
            String description = GetDescription(item);
            if (description != null)
            {
                var descriptionBounds = new Rectangle(
                    titleBounds.Right, textBounds.Top,
                    textBounds.Right - titleBounds.Right, textBounds.Height);
                DrawWithHighlighting(description, GetDescriptionHighlightedText(item) ?? string.Empty,
                    graphics, descriptionBounds, textColor, backColor);
            }
        }

        private void DrawWithHighlighting(String description, String textToHighlight, Graphics graphics,
            Rectangle descriptionBounds, Color textColor, Color backColor)
        {
            var findMatch = new FindMatch(description);
            int ichHighlightBegin = description.ToLower().IndexOf(textToHighlight.ToLower(), StringComparison.Ordinal);
            // A very short search only matches at the front of a word
            if ((textToHighlight.Length < ProteinMatchQuery.MIN_FREE_SEARCH_LENGTH) && (ichHighlightBegin > 0))
            {
                // Insist on a leading space for match
                ichHighlightBegin = description.ToLower().IndexOf(" "+textToHighlight.ToLower(), StringComparison.Ordinal); // Not L10N
                if (ichHighlightBegin > 0)
                    ichHighlightBegin++; // Don't really want to highlight the space
            }
            if (ichHighlightBegin >= 0)
            {
                findMatch = findMatch.ChangeRange(ichHighlightBegin, ichHighlightBegin + textToHighlight.Length);
            }
            else
            {
                findMatch = findMatch.ChangeRange(0,0); // No highlighting
            }
            var textRendererHelper = new TextRendererHelper
            {
                Font = ListView.Font,
                HighlightFont = new Font(ListView.Font, FontStyle.Bold),
                ForeColor = textColor,
                BackColor = backColor,
            };
            textRendererHelper.DrawHighlightedText(graphics, descriptionBounds, findMatch);
        }

        /// <summary>
        /// Select the list item that is either before/after the currently selected list item.
        /// </summary>
        public void SelectNextItem(bool down)
        {
            int index;
            if (ListView.FocusedItem == null)
            {
                index = down ? 0 : ListView.Items.Count - 1;
            }
            else
            {
                index = ListView.FocusedItem.Index + (down ? 1 : -1);
            }
            if (index < 0 || index >= ListView.Items.Count)
            {
                return;
            }
            ListView.FocusedItem = ListView.Items[index];
            ListView.FocusedItem.EnsureVisible();
        }

        /// <summary>
        /// Updates the tooltip text to reflect the list item under the mouse.
        /// </summary>
        private void listView_MouseMove(object sender, MouseEventArgs e)
        {
            var item = ListView.GetItemAt(e.X, e.Y);
            if (item == null)
            {
                return;
            }
            String tooltipText = item.ToolTipText;
            if (tooltipText != toolTip.GetToolTip(ListView))
            {
                toolTip.SetToolTip(ListView, tooltipText);
            }
        }
        /// <summary>
        /// Changes the items displayed in the ListView with a minimum of repainting.
        /// </summary>
        public void SetListItems(IList<ListViewItem> listItems)
        {
            for (int i = 0; i < listItems.Count; i++)
            {
                SetListItem(i, listItems[i]);
            }
            while (ListView.Items.Count > listItems.Count)
            {
                ListView.Items.RemoveAt(ListView.Items.Count - 1);
            }
            // Select the item if it is the only choice, so that the user
            // can just press enter or tab to accept a single entry.
            if (listItems.Count == 1)
                ListView.FocusedItem = ListView.Items[0];
        }
        /// <summary>
        /// Returns the description associated with the ListViewItem.
        /// The description is defined as the text of the first ListViewSubItem.
        /// (The first ListViewSubItem corresponds to the text of the ListViewItem itself).
        /// </summary>
        public static String GetDescription(ListViewItem listViewItem)
        {
            if (listViewItem.SubItems.Count < 2)
            {
                return null;
            }
            return listViewItem.SubItems[1].Text;
        }
        /// <summary>
        /// Returns the portion of the completion item text which should be bolded
        /// in the ListViewItem.  This is stored on the SearchView property of the ListViewItem.
        /// </summary>
        public static String GetHighlightedText(ListViewItem listViewItem)
        {
            var completionItem = listViewItem.Tag as StatementCompletionItem;
            if (completionItem != null)
            {
                return completionItem.SearchText;
            }
            return null;
        }
        /// <summary>
        /// Returns the portion of the description text which should be bolded
        /// in the ListViewItem.  This is stored on the "Tag" property of the ListViewSubItem.
        /// </summary>
        public static String GetDescriptionHighlightedText(ListViewItem listViewItem)
        {
            if (listViewItem.SubItems.Count < 2)
            {
                return null;
            }
            return listViewItem.SubItems[1].Tag as String;
        }
        /// <summary>
        /// Adds a description to the ListViewSubItem.
        /// </summary>
        /// <param name="listViewItem"></param>
        /// <param name="description"></param>
        /// <param name="highlightedText">The portion of the description that should be underlined (or null).
        /// The first occurence of the highlightedText in the string description will be underlined.  Also, 
        /// if the description is too long to be completely displayed, it will be trimmed in such a way
        /// that the highlightedText is still visible.
        /// </param>
        public static void AddDescription(ListViewItem listViewItem, String description, String highlightedText)
        {
            if (listViewItem.SubItems.Count != 1)
            {
                throw new InvalidOperationException(
                    Resources.StatementCompletionForm_AddDescription_List_view_item_already_has_a_description);
            }
            listViewItem.SubItems.Add(new ListViewItem.ListViewSubItem(listViewItem, description)
                                          {
                                              Tag = highlightedText
                                          });
        }

        public ListViewItem SetListItem(int index, ListViewItem src)
        {
            if (index == ListView.Items.Count)
            {
                ListView.Items.Add(src);
                return src;
            }
            var dest = ListView.Items[index];
            if (dest.SubItems.Count != src.SubItems.Count)
            {
                ListView.Items[index] = src;
                return src;
            }
            // Setting the Text property of a ListViewItem causes significant flicker 
            // (even if the value is the same) so we only set it if it's different.
// ReSharper disable RedundantCheckBeforeAssignment
            if (dest.Text != src.Text)
                dest.Text = src.Text;
// ReSharper restore RedundantCheckBeforeAssignment
            dest.ImageIndex = src.ImageIndex;
            dest.ToolTipText = src.ToolTipText;
            dest.Tag = src.Tag;
            for (int i = 0; i < dest.SubItems.Count; i++)
            {
                dest.SubItems[i].Text = src.SubItems[i].Text;
                dest.SubItems[i].Tag = src.SubItems[i].Tag;
            }
            DrawItem(ListView.CreateGraphics(), dest);
            return dest;
        }
    }
}
