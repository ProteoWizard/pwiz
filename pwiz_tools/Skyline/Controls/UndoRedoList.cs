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
using System.Runtime.InteropServices;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    /// <summary>
    /// Window which pops up containing a list of undoable/redoable items.
    /// </summary>
    public partial class UndoRedoList : FormEx
    {
        const int MAX_DISPLAY_ITEMS = 16;
        const int TOTAL_BORDER_WIDTH = 2;

        private bool _undo;
        private UndoManager _undoManager;
        private int _selectedIndex;
        private int _lastScrollPosition;

        public UndoRedoList()
        {
            InitializeComponent();
            // Execute the undo/redo actions when the mouse is released
            listBox.MouseUp += listBox_MouseUp;
            
            // Update the selected items whenever the mouse moves or is clicked
            listBox.MouseMove += listBox_MouseMove;
            listBox.MouseDown += listBox_MouseMove;
            listBox.Scroll += listBox_Scroll;
            listBox.KeyDown += listBox_KeyDown;

            // Dismiss this form when it loses focus
            Deactivate += (s, e) => Cancel();
        }

        /// <summary>
        /// Populates the list box with the list of undo/redo actions, and displays the form
        /// directly below the tool strip button.
        /// </summary>
        /// <param name="dropDownButton">Tool strip button under which this form should be displayed</param>
        /// <param name="undo">true if this is "undo", false for "redo"</param>
        /// <param name="undoManager">the UndoManager</param>
        public void ShowList(ToolStripDropDownItem dropDownButton, bool undo, UndoManager undoManager)
        {
            Point location =
                dropDownButton.Owner.PointToScreen(new Point(dropDownButton.Bounds.Left, dropDownButton.Bounds.Bottom));
            Left = location.X;
            Top = location.Y;
            _undo = undo;
            _undoManager = undoManager;
            listBox.Items.Clear();
            IEnumerable<String> descriptions = undo ? undoManager.UndoDescriptions : undoManager.RedoDescriptions;
            foreach (String description in descriptions)
            {
                listBox.Items.Add(description);
            }
            UpdateSelectedIndex(0);

            Height = listBox.ItemHeight * Math.Min(MAX_DISPLAY_ITEMS, listBox.Items.Count) + label.Height + TOTAL_BORDER_WIDTH;
            Show(dropDownButton.Owner);
            listBox.Focus();
        }

        void ExecuteActions()
        {
            Close();
            if (_undo)
            {
                _undoManager.UndoRestore(_selectedIndex);
            }
            else
            {
                _undoManager.RedoRestore(_selectedIndex);
            }
        }

        void Cancel()
        {
            Close();
        }

        void listBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Cancel();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                ExecuteActions();
            }
        }

        /// <summary>
        /// Update the selected actions when the scroll position of the listbox changes.
        /// If the user scrolls downwards, then select the last displayed item.  If the user
        /// scrolls upwards, then select the first displayed item.
        /// </summary>
        void listBox_Scroll(object sender, ScrollEventArgs scrollEventArgs)
        {
            if (_lastScrollPosition == scrollEventArgs.ScrollPosition)
            {
                return;
            }
            int newSelectedIndex;
            if (scrollEventArgs.ScrollPosition > _lastScrollPosition)
            {
                newSelectedIndex = scrollEventArgs.ScrollPosition + MAX_DISPLAY_ITEMS - 1;
            }
            else
            {
                newSelectedIndex = scrollEventArgs.ScrollPosition;
            }
            _lastScrollPosition = scrollEventArgs.ScrollPosition;
            newSelectedIndex = Math.Min(newSelectedIndex, listBox.Items.Count - 1);
            UpdateSelectedIndex(newSelectedIndex);
        }

        /// <summary>
        /// Performs the multi-level undo or redo when the user clicks on the list box.
        /// </summary>
        void listBox_MouseUp(object sender, EventArgs e)
        {
            ExecuteActions();
        }

        /// <summary>
        /// Update the number of undo/redo actions that are selected based on the position of the mouse.
        /// </summary>
        void listBox_MouseMove(object sender, MouseEventArgs e)
        {
            int index = listBox.IndexFromPoint(e.Location);
            if (index == ListBox.NoMatches)
            {
                return;
            }
            UpdateSelectedIndex(index);
        }

        private String GetLabelText(int index)
        {
            if (index == 0)
            {
                return _undo
                           ? Resources.UndoRedoList_GetLabelText_Undo_1_Action
                           : Resources.UndoRedoList_GetLabelText_Redo_1_Action;
            }
            return string.Format(_undo 
                ? Resources.UndoRedoList_GetLabelText_Undo__0__Actions
                : Resources.UndoRedoList_GetLabelText_Redo__0__Actions,
                (index + 1));
        }

        private void UpdateSelectedIndex(int index)
        {
            _selectedIndex = index;
            for (int i = 0; i < listBox.Items.Count; i++)
            {
                bool selected = i <= index;
                if (selected != listBox.SelectedIndices.Contains(i))
                {
                    listBox.SetSelected(i, i <= index);
                }
            }
            label.Text = GetLabelText(index);
        }

        /// <summary>
        /// ListBox class which sends events when the listbox is scrolled.
        /// </summary>
        class ListBoxEx : ListBox
        {
            private const int WM_VSCROLL = 0x0115;
            private const int SB_VERT = 1;

            [DllImport("user32.dll")]
            private static extern int GetScrollPos(IntPtr hWnd, int nBar);

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg == WM_VSCROLL)
                {
                    if (Scroll != null)
                    {
                        ScrollEventArgs scrollEventArgs = 
                            new ScrollEventArgs{ScrollPosition = GetScrollPos(m.HWnd, SB_VERT)};
                        Scroll.Invoke(this, scrollEventArgs);
                    }
                }
            }
            public event EventHandler<ScrollEventArgs> Scroll;
        }
        class ScrollEventArgs : EventArgs
        {
            public int ScrollPosition { get; set; }
        }
    }
}
