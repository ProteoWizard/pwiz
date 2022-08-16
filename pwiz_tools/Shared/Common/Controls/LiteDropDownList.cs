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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Properties;

namespace pwiz.Common.Controls
{
    /// <summary>
    /// Substitute for ComboBox which avoids performance issues by only
    /// creating its dropdown list when user action demands it.
    ///
    /// We'd call it DropDownList but there's one of those in the .net WebUI namespace, which might be confusing
    /// </summary>
    public class LiteDropDownList : Button
    {

        private int _selectedIndex;
        private Color _backColorInactive;
        private Color _backColorActive;
        private Font _normalFont;
        private Font _specialFont;


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
            _normalFont = new Font(base.Font, FontStyle.Regular);
            _specialFont = new Font(base.Font, FontStyle.Italic);
        }


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
            get => SelectedIndex >= 0 ? Items[SelectedIndex] : null;
            set => SelectedIndex = Items.IndexOf(value);
        }

        public override string Text
        {
            get => base.Text;
            set => SelectedIndex = FindStringExact(value);
        }

        public int FindStringExact(string str)
        {
            return Items.IndexOf(str);
        }

        private void RestoreBackgroundColor(object sender, EventArgs e)
        {
            // Tidy up background color if focus moves to a sibling etc, but losing focus to our listbox is a different story
            base.BackColor = _backColorInactive;
        }

        protected override void OnClick(EventArgs e)
        {
            ShowDropdown();
        }

        private void ShowDropdown()
        {
            // Show the dropdown menu
            var contextMenuStrip = new ContextMenuStrip();
            contextMenuStrip.ShowImageMargin =
                contextMenuStrip.ShowCheckMargin = false; // We only want to see the text, no image area needed
            contextMenuStrip.Items.AddRange(Items.Select(CreateMenuItem).ToArray());
            contextMenuStrip.ItemClicked += contextMenuStrip_ItemClicked; // Update the button selection when use clicks on menu
            contextMenuStrip.KeyDown += contextMenuStrip_KeyDown; // Handle keys in the manner of a standard combobox
            contextMenuStrip.Show(this, new Point(0, this.Height)); // Show menu just below our button
            base.BackColor = _backColorActive;
        }

        protected virtual ToolStripMenuItem CreateMenuItem(object value)
        {
            return new ToolStripMenuItem(value.ToString())
            {
                Padding = Padding.Empty
            };
        }

        // Update the button selection when use clicks on menu
        private void contextMenuStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            // Note the user selection, menu will close itself
            var item = e.ClickedItem;
            Text = item.Text;
            base.BackColor = _backColorInactive;
        }

        // Handle keys in the manner of a standard combobox
        private void contextMenuStrip_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyData)
            {
                case Keys.F4:
                case Keys.Space:
                {
                    // Close the menu
                    ((ContextMenuStrip)sender).Close();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    base.BackColor = _backColorInactive;
                    break;
                }
            }
        }

        // Mimic the keyboard handling of standard ComboBox
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Alt | Keys.Down) || keyData == (Keys.Alt | Keys.Up) || keyData == Keys.F4)
            {
                ShowDropdown();
                return true;
            }

            keyData &= ~(Keys.Control | Keys.Shift); // Ignore control and shift keys, as standard comboBox does

            // Arrow keys change the selection rather than leaving the control
            if (keyData == Keys.Up || keyData == Keys.Left)
            {
                SelectedIndex = Math.Max(0, SelectedIndex - 1);
                return true;
            }
            if (keyData == Keys.Down || keyData == Keys.Right)
            {
                SelectedIndex = Math.Min(Items.Count - 1, SelectedIndex + 1);
                return true;
            }
            if (keyData == Keys.PageDown || keyData == Keys.End)
            {
                SelectedIndex = Items.Count - 1;
                return true;
            }
            if (keyData == Keys.PageUp || keyData == Keys.Home)
            {
                SelectedIndex = 0;
                return true;
            }
            var kc = new KeysConverter();
            var keyChar = kc.ConvertToString(keyData);
            if (keyChar != null)
            {
                // Find the next item that starts with this key, if any
                for (var i = 1; i <= Items.Count; i++)
                {
                    var proposedIndex = (SelectedIndex + i) % Items.Count;
                    if (Items[proposedIndex].ToString().StartsWith(keyChar, StringComparison.CurrentCultureIgnoreCase))
                    {
                        SelectedIndex = proposedIndex;
                        return true;
                    }
                }
            }

            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _normalFont?.Dispose();
                _specialFont?.Dispose();
            }

            base.Dispose(disposing);
        }

    }

}