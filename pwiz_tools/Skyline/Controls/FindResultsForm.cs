/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    /// <summary>
    /// Displays the results of a "Find All" operation in a list that allows the user to
    /// double-click and navigate to the location in the document.
    /// </summary>
    public partial class FindResultsForm : DockableFormEx
    {
        public FindResultsForm(SkylineWindow skylineWindow, IEnumerable<FindResult> findResults)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
            foreach (var findResult in findResults)
            {
                listView.Items.Add(MakeListViewItem(findResult));
            }
            // Width=-1 means auto-adjust to the length of the longest item
            colHdrDisplayText.Width = -1;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (SkylineWindow == null)
            {
                return;
            }
            DocumentUiContainer.ListenUI(DocumentChanged);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            if (DocumentUiContainer == null)
            {
                return;
            }
            DocumentUiContainer.UnlistenUI(DocumentChanged);
        }

        public SkylineWindow SkylineWindow { get; private set; }
        public IDocumentUIContainer DocumentUiContainer { get { return SkylineWindow; } }

        public void ChangeResults(IEnumerable<FindResult> findResults)
        {
            listView.Items.Clear();
            listView.Items.AddRange(findResults.Select(MakeListViewItem).ToArray());
        }
        ListViewItem MakeListViewItem(FindResult findResult)
        {
            var listViewItem = new ListViewItem(findResult.LocationName) { Tag = findResult };
            listViewItem.SubItems.AddRange(new [] {
                                         findResult.LocationType, 
                                         findResult.FindMatch.DisplayText,
                });
            if (!findResult.IsValid)
            {
                listViewItem.Font = new Font(listView.Font, FontStyle.Strikeout);
            }
            return listViewItem;
        }

        private void DocumentChanged(object sender, DocumentChangedEventArgs args)
        {
            var newDocument = DocumentUiContainer.DocumentUI;
            foreach (ListViewItem listViewItem in listView.Items)
            {
                var oldFindResult = ((FindResult)listViewItem.Tag);
                var newFindResult = oldFindResult.ChangeDocument(newDocument);
                if (newFindResult.Equals(oldFindResult))
                {
                    continue;
                }
                listViewItem.SubItems[colHdrDisplayText.Index].Text = newFindResult.FindMatch.DisplayText;
                listViewItem.Font = newFindResult.IsValid ? listView.Font : new Font(listView.Font, FontStyle.Strikeout);
                listViewItem.Tag = newFindResult;
            }
        }

        private void listView_ItemActivate(object sender, EventArgs e)
        {
            ActivateSelectedItem();
        }

        public int ItemCount
        {
            get { return listView.Items.Count; }
        }

        public void ActivateItem(int iItem)
        {
            for (int i = 0; i < listView.Items.Count; i++)
                listView.Items[i].Selected = (i == iItem);
            listView.Items[iItem].Focused = true;
            listView.Select();
            ActivateSelectedItem();
        }

        private void ActivateSelectedItem()
        {
            var activeItem = listView.SelectedItems.Cast<ListViewItem>().FirstOrDefault();
            if (activeItem == null)
            {
                return;
            }
            var findResult = activeItem.Tag as FindResult;
            if (findResult == null)
            {
                return;
            }
            SkylineWindow.DisplayFindResult(listView, findResult);
        }

        private void listView_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            Brush brush;
            Color backColor;
            Color foreColor;
            if (e.Item.Selected)
            {
                if (listView.Focused)
                {
                    brush = SystemBrushes.Highlight;
                    backColor = SystemColors.Highlight;
                    foreColor = SystemColors.HighlightText;
                }
                else
                {
                    // We use the same colors here as TreeViewMS does for
                    // drawing unfocused selected rows.
                    brush = Brushes.LightGray;
                    backColor = Color.LightGray;
                    foreColor = SystemColors.WindowText;
                }
            }
            else
            {
                brush = SystemBrushes.Window;
                backColor = SystemColors.Window;
                foreColor = SystemColors.WindowText;
            }
            if (e.ColumnIndex == 0)
            {
                // Erase the entire background when drawing the first sub-item
                e.Graphics.FillRectangle(brush, e.Item.Bounds);
            }
            if (e.ColumnIndex == colHdrDisplayText.Index)
            {
                var findResult = (FindResult) e.Item.Tag;
                var textRendererHelper = new TextRendererHelper
                                             {
                                                 ForeColor = foreColor,
                                                 BackColor = backColor,
                                                 Font = e.Item.Font,
                                                 HighlightFont = new Font(e.Item.Font, FontStyle.Bold),
                                             };
                textRendererHelper.DrawHighlightedText(e.Graphics, e.SubItem.Bounds, findResult.FindMatch);
            }
            else
            {
                e.SubItem.ForeColor = foreColor;
                e.DrawText();
            }
            if (e.ColumnIndex == 0)
            {
                e.DrawFocusRectangle(e.Bounds);
            }
        }

// ReSharper disable MemberCanBeMadeStatic.Local
        private void listView_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }
// ReSharper restore MemberCanBeMadeStatic.Local

        private void FindResultsForm_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    SkylineWindow.FocusDocument();
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// When the ListView changes size, try to make the DisplayText column take up all of the available space
        /// after the first two columns.
        /// If there is no room for the DisplayText column, make the DisplayText column as wide as the ListView
        /// itself and so the user will be able to scroll and see it.
        /// </summary>
        private void listView_Resize(object sender, EventArgs e)
        {
            if (listView.Items.Count == 0 || !listView.IsHandleCreated)
            {
                return;
            }
            BeginInvoke(new Action(ResizeListViewColumns));
        }

        private void ResizeListViewColumns()
        {
            int height = listView.Items[listView.Items.Count - 1].Bounds.Bottom;
            int dxAvailable = listView.ClientRectangle.Width;
            if (height > listView.ClientRectangle.Height)
            {
                dxAvailable -= SystemInformation.VerticalScrollBarWidth;
            }
            dxAvailable -= listView.Columns[0].Width + listView.Columns[1].Width;
            const int minDisplayTextWidth = 10;
            if (dxAvailable >= minDisplayTextWidth)
            {
                listView.Columns[2].Width = dxAvailable;
            }
            else
            {
                listView.Columns[2].Width = listView.ClientRectangle.Width -
                                            SystemInformation.VerticalScrollBarWidth;
            }
        }
    }
}
