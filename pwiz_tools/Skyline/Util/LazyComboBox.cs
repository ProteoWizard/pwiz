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
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Substitute for ComboBox which avoids performance issues by only
    /// creating its dropdown list when user action demands it
    /// </summary>
    public class LazyComboBox : Button
    {
        public List<object> Items;
        private ListBox _listbox;
        private int _selectedIndex;

        public event EventHandler SelectedIndexChanged;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                var oldValue = _selectedIndex;
                _selectedIndex = value;
                base.Text = _selectedIndex < 0 ? string.Empty : Items[_selectedIndex].ToString();
                if (value != oldValue)
                {
                    SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public override string Text
        {
            get => base.Text;
            set => SelectedIndex = FindStringExact(value);
        }

        public LazyComboBox()
        {
            Items = new List<object>();
            base.TextAlign = ContentAlignment.MiddleLeft;
            FlatStyle = FlatStyle.Flat;
            SelectedIndex = -1;
            AutoEllipsis = true; // e.g. If test is too wide, show "Explicit Coll..." rather than breaking at space and showing "Explicit"
            base.BackColor = SystemColors.ControlLight;
            FlatAppearance.BorderSize = Math.Max(1, FlatAppearance.BorderSize/2); // Allow text closer to edge than in a normal button, like standard ComboBox does
            // Show a dropdown hint
            Image = Resources.DropImageNoBackground;
            ImageAlign = ContentAlignment.MiddleRight;
            TextImageRelation = TextImageRelation.Overlay;
        }

        public int FindStringExact(string str)
        {
            return Items.IndexOf(str);
        }

        protected override void OnClick(EventArgs e)
        {
            if (_listbox == null)
            {
                _listbox = new ListBox();
                // Position the listbox within the parent form, just below our button
                var form = this.Parent;
                var location = new Point(Location.X, Location.Y + Height);
                while (!(form is Form) && (form.Parent != null))
                {
                    location.Offset(form.Location.X, form.Location.Y);
                    form = form.Parent;
                }
                _listbox.Parent = form;
                _listbox.Location = location;
                _listbox.AutoSize = true;
                _listbox.SelectedIndexChanged += ListBox_SelectedValueChanged;
                _listbox.LostFocus += ListBox_Leave;
            }
            _listbox.Items.Clear();
            _listbox.Items.AddRange(Items.ToArray());
            _listbox.SelectedIndex = _selectedIndex;
            _listbox.BringToFront();
            _listbox.Visible = true;
            _listbox.Show();
            _listbox.Focus();
        }

        private void ListBox_SelectedValueChanged(object sender, EventArgs e)
        {
            if (_listbox != null && !_listbox.Disposing &&  _listbox.SelectedIndex != -1)
            {
                SelectedIndex = _listbox.SelectedIndex;
                ListBox_Leave(null, null); // Dropdown list goes away on selection (standard combo box behavior)
            }
        }

        private void ListBox_Leave(object sender, EventArgs e)
        {
            if (_listbox != null && !_listbox.Disposing)
            {
                _listbox.Visible = false;
                _listbox.Hide();
            }
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (_listbox != null))
            {
                _listbox.Dispose();
                _listbox = null;
            }
            base.Dispose(disposing);
        }
    }
}