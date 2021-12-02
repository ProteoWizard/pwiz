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
    /// We'd call it DropDownList but there's one of those in the .net
    /// </summary>
    public class LiteDropDownList : Button
    {
        private NonClippingListBox _listbox;
        private int _selectedIndex;
        private Control _listboxLostFocusToControl;
        private bool _listboxClosedByUnknownKeystroke;
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
            // Show a dropdown hint
            Image = Resources.DropImageNoBackground;
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
            if (_listboxLostFocusToControl == this && !_listboxClosedByUnknownKeystroke)
            {
                // We were showing a listbox, user clicked on its button - don't reopen the listbox on this click
                _listboxLostFocusToControl = null;
                return;
            }

            if (_listbox == null)
            {
                // Position the listbox within the parent form, just below our button
                var form = this.Parent;
                var location = new Point(Location.X, Location.Y + Height);
                while (!(form is Form) && (form.Parent != null))
                {
                    location.Offset(form.Location.X, form.Location.Y);
                    form = form.Parent;
                }
                _listbox = new NonClippingListBox(form) {Location = location, AutoSize = true};
                _listbox.SelectedIndexChanged += ListBox_SelectedValueChanged;
                _listbox.LostFocus += ListBox_LostFocus;
                _listbox.UnknownKeystroke += ListBox_UnknownKeystroke;
            }

            _listbox.Items.Clear();
            _listbox.Items.AddRange(Items.ToArray());
            // Set size wide and tall enough to not require scrollbars
            using (var g = _listbox.CreateGraphics())
            {
                var w = (from object item in _listbox.Items select TextRenderer.MeasureText(g, item.ToString(), _listbox.Font).Width).Prepend(0).Max();
                _listbox.Size = new Size(w + 5, _listbox.ItemHeight * (Items.Count +1 ) );
            }

            _listboxClosedByUnknownKeystroke = false;
            _listboxLostFocusToControl = null;
            _listbox.SelectedIndex = _selectedIndex;
            _listbox.BringToFront();
            _listbox.Visible = true;
            _listbox.Show();
            _listbox.Focus();
            base.BackColor = _backColorActive;
        }

        // Mimic the keyboard handling of standard ComboBox
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Up || keyData == Keys.Left)
            {
                SelectedIndex = Math.Max(0, SelectedIndex - 1);
                return true;
            }
            else if (keyData == Keys.Down || keyData == Keys.Right)
            {
                SelectedIndex = Math.Min(Items.Count - 1, SelectedIndex + 1);
                return true;
            }
            return false;
        }

        private void ListBox_SelectedValueChanged(object sender, EventArgs e)
        {
            if (_listbox != null && !_listbox.Disposing &&  _listbox.SelectedIndex != -1)
            {
                SelectedIndex = _listbox.SelectedIndex;
                if (_listbox.SelectionChangedByArrowKey)
                {
                    _listbox.SelectionChangedByArrowKey = false;
                    return;
                }
                ListBox_Leave(); // Dropdown list goes away on selection (standard combo box behavior)
                _listboxLostFocusToControl = null; // Next click on parent button should fire
            }
        }

        private void ListBox_LostFocus(object sender, EventArgs e)
        {
            _listboxLostFocusToControl = (_listbox.mParent as ContainerControl)?.ActiveControl; // So we know if lost focus due to click on own parent button
            ListBox_Leave();
        }

        private void ListBox_UnknownKeystroke(object sender, EventArgs e)
        {
            _listboxClosedByUnknownKeystroke = true;
            ListBox_Leave();
        }

        private void ListBox_Leave()
        {
            if (_listbox != null && !_listbox.Disposing)
            {
                _listbox.SelectionChangedByArrowKey = false;
                _listbox.Visible = false;
                _listbox.Hide();
            }
            base.BackColor = _backColorInactive;
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

        // A ListBox that doesn't get clipped by its parent container
        // Credit to https://stackoverflow.com/users/17034/hans-passant
        // https://stackoverflow.com/questions/353561/how-to-create-a-c-sharp-winforms-control-that-hovers#354326
        private class NonClippingListBox : ListBox
        {
            internal Control mParent;
            private Point mPos;
            private Point relLocation;
            private bool mInitialized;

            public bool SelectionChangedByArrowKey;
            public event EventHandler UnknownKeystroke;

            public NonClippingListBox(Control parent)
            {
                mParent = parent;
                mInitialized = true;
                this.SetTopLevel(true);
                parent.LocationChanged += parent_LocationChanged;
                mPos = mParent.Location;
            }

            public new Point Location
            {
                get { return mParent.PointToClient(relLocation); }
                set
                {
                    relLocation = value;
                    Point zero = mParent.PointToScreen(Point.Empty);
                    base.Location = new Point(zero.X + value.X, zero.Y + value.Y);
                }
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (keyData == Keys.Up)
                {
                    SelectionChangedByArrowKey = true;
                    SelectedIndex = Math.Max(0, SelectedIndex - 1);
                    return true;
                }
                else if (keyData == Keys.Down)
                {
                    SelectionChangedByArrowKey = true;
                    SelectedIndex = Math.Min(Items.Count - 1, SelectedIndex + 1);
                    return true;
                }
                // Any other key should close the listbox
                UnknownKeystroke?.Invoke(this, EventArgs.Empty);
                return false;
            }


            protected override Size DefaultSize
            {
                get
                {
                    return mInitialized ? base.DefaultSize : Size.Empty;
                }
            }

            protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
            {
                if (this.mInitialized)
                    base.SetBoundsCore(x, y, width, height, specified);
            }

            void parent_LocationChanged(object sender, EventArgs e)
            {
                base.Location = new Point(Left + mParent.Left - mPos.X, Top + mParent.Top - mPos.Y);
                mPos = mParent.Location;
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams cp = base.CreateParams;
                    if (mParent != null && !DesignMode)
                    {
                        cp.Style = (int)(((long)cp.Style & 0xffff) | 0x90200000);
                        cp.Parent = mParent.Handle;
                        Point pos = mParent.PointToScreen(Point.Empty);
                        cp.X = pos.X;
                        cp.Y = pos.Y;
                        cp.Width = base.DefaultSize.Width;
                        cp.Height = base.DefaultSize.Height;
                    }
                    return cp;
                }
            }
        }
    }

}