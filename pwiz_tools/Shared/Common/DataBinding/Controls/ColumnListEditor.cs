/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Controls.Editor;

namespace pwiz.Common.DataBinding.Controls
{
    public partial class ColumnListEditor : UserControl
    {
        private bool _inChange;
        public ColumnListEditor()
        {
            InitializeComponent();
            UpdateButtons();
        }

        internal ColumnListView ListView { get { return listView; } }
        public ToolStrip ToolStrip { get { return toolStrip1; } }

        public void ReplaceItems(IEnumerable<ListViewItem> newItems)
        {
            bool wasInChange = _inChange;
            try
            {
                _inChange = true;
                ListViewHelper.ReplaceItems(ListView, newItems.ToArray());
            }
            finally
            {
                _inChange = wasInChange;
            }
            UpdateButtons();
        }

        private void listView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChange)
            {
                return;
            }
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            btnUp.Enabled = ListViewHelper.IsMoveUpEnabled(listView);
            btnDown.Enabled = ListViewHelper.IsMoveDownEnabled(listView);
            btnRemove.Enabled = listView.SelectedIndices.Count > 0;
        }

        public event Action<IList<int>> ColumnsMoved;

        public void OnColumnsMoved(IEnumerable<int> newIndexes)
        {
            if (ColumnsMoved != null)
            {
                var list = ImmutableList.ValueOf(newIndexes);
                ColumnsMoved(list);
            }
        }

        private void MoveColumns(bool upwards)
        {
            var selectedIndexes = listView.SelectedIndices.Cast<int>().ToArray();
            var newIndexes = ListViewHelper.MoveItems(
                Enumerable.Range(0, listView.Items.Count), selectedIndexes, upwards);
            var newSelection = ListViewHelper.MoveSelectedIndexes(listView.Items.Count, selectedIndexes, upwards);
            OnColumnsMoved(newIndexes);
            ListViewHelper.SelectIndexes(listView, newSelection);
        }

        private void BtnUpOnClick(object sender, EventArgs e)
        {
            MoveColumns(true);
        }

        private void BtnDownOnClick(object sender, EventArgs e)
        {
            MoveColumns(false);
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            var selectedIndexes = ListView.SelectedIndices.OfType<int>().ToArray();
            if (selectedIndexes.Length == 0)
            {
                return;
            }
            var newIndexes = Enumerable.Range(0, ListView.Items.Count).Except(selectedIndexes);
            var newSelection = selectedIndexes.Max() + 1 - selectedIndexes.Length;
            OnColumnsMoved(newIndexes);
            newSelection = Math.Min(newSelection, ListView.Items.Count - 1);
            if (newSelection > 0)
            {
                ListViewHelper.SelectIndex(ListView, newSelection);
            }
        }
    }
}
