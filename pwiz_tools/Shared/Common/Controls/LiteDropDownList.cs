/*
 * Original author: Brian Pratt <bspratt .at. proteinms dot net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Common.Properties;

namespace pwiz.Common.Controls
{
    /// <summary>
    /// Substitute for ComboBox which avoids performance issues by only
    /// creating its dropdown list when user action demands it.
    ///
    /// We'd call it DropDownList but there's one of those in the .net WebUI space, which might be confusing
    /// </summary>
    public class LiteDropDownList : Button
    {
        private int _selectedIndex;
        private Color _backColorInactive;
        private Color _backColorActive;
        private Font _normalFont;
        private Font _specialFont;

        public List<object> Items;
        public List<object> SpecialItems; // Items which also appear in this list will be formatted in italics

        public event EventHandler SelectedIndexChanged;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                var oldValue = _selectedIndex;
                _selectedIndex = value;
                var text = _selectedIndex < 0 || _selectedIndex >= Items.Count ? string.Empty : Items[_selectedIndex].ToString();
                base.Font = SpecialItems.Contains(text) ? _specialFont : _normalFont;
                base.Text = text;
                if (value != oldValue)
                {
                    SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public object SelectedItem
        {
            get
            {
                return SelectedIndex >= 0 ? Items[SelectedIndex] : null;
            }
            set
            {
                SelectedIndex = Items.IndexOf(value);
            }
        }

        public override string Text
        {
            get => base.Text;
            set => SelectedIndex = FindStringExact(value);
        }

        public LiteDropDownList()
        {
            Items = new List<object>();
            SpecialItems = new List<object>();
            base.TextAlign = ContentAlignment.MiddleLeft;
            SelectedIndex = -1;
            Padding = Padding.Empty;
            Margin = new Padding(-1, 0, -1, 0); // Let these butt up to each other, and overlap on left/right edges
            AutoEllipsis = true; // e.g. If test is too wide, show "Explicit Coll..." rather than breaking at space and showing "Explicit"
            base.BackColor = _backColorInactive = SystemColors.ControlLight;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.MouseOverBackColor = _backColorActive = SystemColors.GradientInactiveCaption; // Very light system color
            FlatAppearance.BorderColor = SystemColors.ControlDark;
            FlatAppearance.BorderSize = 1;
            // Show a dropdown hint down arrow in the same color as the text
            var scrBitmap = Resources.DropImageNoBackground;
            var newBitmap = new Bitmap(scrBitmap.Width, scrBitmap.Height);
            for (var i = 0; i < scrBitmap.Width; i++)
            {
                for (var j = 0; j < scrBitmap.Height; j++)
                {
                    var pixel = scrBitmap.GetPixel(i, j);
                    newBitmap.SetPixel(i, j, pixel.A != 0 ? base.ForeColor : pixel); // Leave 0-alpha pixels alone
                }
            }
            Image = newBitmap;
            ImageAlign = ContentAlignment.MiddleRight;
            TextImageRelation = TextImageRelation.Overlay;
            LostFocus += RestoreBackgroundColor;
            Leave += RestoreBackgroundColor;
            _normalFont = base.Font;
            _specialFont = new Font(base.Font, FontStyle.Italic);
        }

        private void RestoreBackgroundColor(object sender, EventArgs e)
        {
            // Tidy up background color if focus moves to a sibling etc, but losing focus to our listbox is a different story
            base.BackColor = _backColorInactive; 
        }

        public int FindStringExact(string str)
        {
            return Items.IndexOf(str);
        }

        protected override void OnClick(EventArgs e)
        {
            // Show the dropdown menu
            var contextMenuStrip = new ContextMenuStrip();
            contextMenuStrip.ShowImageMargin = contextMenuStrip.ShowCheckMargin = false; // We only want to see the text, no image area needed
            for (var i = 0; i < Items.Count; i++)
            {
                contextMenuStrip.Items.Add(Items[i].ToString());
                if (i == SelectedIndex)
                {
                    contextMenuStrip.Items[i].Select(); // Set the initial selection
                }
            }
            contextMenuStrip.ItemClicked += contextMenuStrip_ItemClicked; // Update the button selection when use clicks on menu
            contextMenuStrip.Show(this, new Point(0, this.Height)); // Show menu just below our button
            base.BackColor = _backColorActive;
        }

        void contextMenuStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            // Note the user selection, menu will close itself
            var item = e.ClickedItem;
            Text = item.Text;
            base.BackColor = _backColorInactive;
        }
    }

}