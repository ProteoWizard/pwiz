/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding.Controls
{
    /// <summary>
    /// Assists with some operations on a listView control which displays a list of model objects.
    /// Model objects can be mapped to a Key.
    /// The ListViewHelper tries to maintain the selection when new items are added to the ListView.
    /// </summary>
    /// <typeparam name="TKey">Type of the </typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class ListViewHelper<TKey, TValue>
    {
        private ListView _listView;
        private IList<TValue> _items = new TValue[0];
        private Func<TValue, TKey> _getKeyFunc;
        private Func<TValue, ListViewItem> _makeListItemFunc;
        public ListViewHelper(ListView listView, IList<TValue> items, Func<TValue, TKey> getKeyFunc, Func<TValue, ListViewItem> makeListItemFunc)
        {
            ListView = listView;
            _items = items;
            _getKeyFunc = getKeyFunc;
            _makeListItemFunc = makeListItemFunc;
        }
        public TKey GetKey(TValue item)
        {
            return _getKeyFunc.Invoke(item);
        }
        public ListViewItem MakeListViewItem(TValue item)
        {
            return _makeListItemFunc.Invoke(item);
        }

        public ListView ListView
        {
            get
            {
                return _listView;
            }
            set
            {
                _listView = value;
            }
        }
        public IList<TValue> Items 
        { 
            get
            {
                return _items;
            } 
            set
            {
                if (_items.SequenceEqual(value))
                {
                    return;
                }
                var selection = GetSelection();
                _items = value;
                ListView.Items.Clear();
                ListView.Items.AddRange(Items.Select(item => MakeListViewItem(item)).ToArray());
                RestoreSelection(selection);
            }
        }
        public TValue[] GetSelectedItems()
        {
            return ListView.SelectedIndices.Cast<int>().Select(index => Items[index]).ToArray();
        }
        public bool IsMoveUpEnabled()
        {
            var items = Enumerable.Range(0, Items.Count);
            var newItems = MoveItems(items, ListView.SelectedIndices.Cast<int>(), true);
            return !newItems.SequenceEqual(items);
        }
        public IList<TValue> MoveItemsUp()
        {
            return MoveItems(Items, ListView.SelectedIndices.Cast<int>(), true);
        }
        public bool IsMoveDownEnabled()
        {
            var items = Enumerable.Range(0, Items.Count);
            var newItems = MoveItems(items, ListView.SelectedIndices.Cast<int>(), false);
            return newItems.SequenceEqual(items);
        }
        public IList<TValue> MoveItemsDown()
        {
            return MoveItems(Items, ListView.SelectedIndices.Cast<int>(), false);
        }

        public static T[] MoveItems<T>(IEnumerable<T> items, IEnumerable<int> selectedIndexCollection, bool upwards)
        {
            var newItems = items.ToArray();
            int[] selectedIndices;
            if (upwards)
            {
                selectedIndices = selectedIndexCollection.ToArray();
            }
            else
            {
                Array.Reverse(newItems);
                selectedIndices = selectedIndexCollection.Select(index => newItems.Length - index - 1).ToArray();
            }
            Array.Sort(selectedIndices);
            for (int i = 0; i < selectedIndices.Length; i++ )
            {
                var index = selectedIndices[i];
                if (index <= i)
                {
                    continue;
                }
                var temp = newItems[index - 1];
                newItems[index - 1] = newItems[index];
                newItems[index] = temp;
            }
            if (!upwards)
            {
                Array.Reverse(newItems);
            }
            return newItems;
        }

        protected IDictionary<TKey, ListViewItem> GetListViewItemDict()
        {
            return ListView.Items.Cast<ListViewItem>().ToDictionary(
                listViewItem => GetKey(Items[listViewItem.Index]));
            
        }
        public void RestoreSelection(Selection selection)
        {
            ListView.SelectedIndices.Clear();
            if (ListView.Items.Count == 0)
            {
                return;
            }
            var listViewItemDict = GetListViewItemDict();
            ListViewItem lastSelectedItem = null;
            foreach (var key in selection.SelectedKeys)
            {
                ListViewItem listViewItem;
                if (listViewItemDict.TryGetValue(key, out listViewItem))
                {
                    ListView.SelectedIndices.Add(listViewItem.Index);
                    lastSelectedItem = listViewItem;
                }
            }
            ListViewItem focusedItem;
            if (Equals(null, selection.FocusedKey))
            {
                focusedItem = null;
            }
            else
            {
                if (listViewItemDict.TryGetValue(selection.FocusedKey, out focusedItem))
                {
                    if (focusedItem.Selected)
                    {
                        return;
                    }
                }
            }
            if (lastSelectedItem != null)
            {
                lastSelectedItem.Focused = true;
                return;
            }
            if (focusedItem != null)
            {
                focusedItem.Selected = true;
                return;
            }
            var focusedIndex = Math.Max(0, Math.Min(ListView.Items.Count - 1, selection.FocusedIndex));
            focusedItem = ListView.Items[focusedIndex];
            focusedItem.Selected = focusedItem.Focused = true;
        }
        public void SelectKeys(IEnumerable<TKey> keys)
        {
            RestoreSelection(new Selection
                                 {
                                     SelectedKeys = keys.ToArray(),
                                 });
        }
        public void SelectKey(TKey key)
        {
            SelectKeys(new[]{key});
        }
        public Selection GetSelection()
        {
            var selection = new Selection
                                {
                                    FocusedIndex = ListView.FocusedItem == null ? -1 : ListView.FocusedItem.Index,
                                    SelectedKeys = ListView.SelectedIndices.Cast<int>()
                                        .Select(index=>GetKey(Items[index])).ToArray(),
                                };
            if (selection.FocusedIndex >= 0)
            {
                selection.FocusedKey = GetKey(Items[selection.FocusedIndex]);
            }
            return selection;
        }

        public class Selection
        {
            public Selection()
            {
                FocusedIndex = -1;
            }
            public TKey FocusedKey { get; set; }
            public int FocusedIndex { get; set; }
            public IList<TKey> SelectedKeys { get; set; }
        }
    }
}
