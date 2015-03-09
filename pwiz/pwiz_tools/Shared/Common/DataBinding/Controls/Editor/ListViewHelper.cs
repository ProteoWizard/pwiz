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
using System.Linq;
using System.Windows.Forms;

namespace pwiz.Common.DataBinding.Controls.Editor
{
    /// <summary>
    /// Assists with some operations on a listView control which displays a list of model objects.
    /// Model objects can be mapped to a Key.
    /// The ListViewHelper tries to maintain the selection when new items are added to the ListView.
    /// </summary>
    public static class ListViewHelper
    {
        public static bool IsMoveUpEnabled(ListView listView)
        {
            return IsMoveEnabled(listView.Items.Count, listView.SelectedIndices.Cast<int>(), true);
        }
        public static bool IsMoveDownEnabled(ListView listView)
        {
            return IsMoveEnabled(listView.Items.Count, listView.SelectedIndices.Cast<int>(), false);
        }
        public static bool IsMoveEnabled(int itemCount, IEnumerable<int> selectedIndexes, bool upwards)
        {
            var items = Enumerable.Range(0, itemCount).ToArray();
            var newItems = MoveItems(items, selectedIndexes, upwards);
            return !newItems.SequenceEqual(items);
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

        public static void SelectIndexes(ListView listView, IEnumerable<int> selectedIndexes)
        {
            var selectedIndexesArray = selectedIndexes.ToArray();
            var unselect = listView.SelectedIndices.Cast<int>().Except(selectedIndexesArray).ToArray();
            var select = selectedIndexesArray.Except(listView.SelectedIndices.Cast<int>()).ToArray();
            if (unselect.Length == 0 && select.Length == 0)
            {
                return;
            }
            
            listView.BeginUpdate();
            try
            {
                foreach (var i in unselect)
                {
                    listView.Items[i].Selected = false;
                }
                foreach (var i in select)
                {
                    listView.Items[i].Selected = true;
                }
            }
            finally
            {
                listView.EndUpdate();
            }
        }
        public static void SelectIndex(ListView listView, int i)
        {
            SelectIndexes(listView, new[]{i});
        }

        public static void ReplaceItems(ListView listView, IList<ListViewItem> newItems)
        {
            listView.BeginUpdate();
            try
            {
                for (int i = 0; i < newItems.Count; i++)
                {
                    var item = newItems[i];
                    if (i >= listView.Items.Count)
                    {
                        listView.Items.Add(item);
                    }
                    else
                    {
                        bool selected = listView.Items[i].Selected;
                        bool focused = listView.Items[i].Focused;
                        listView.Items[i] = item;
                        item.Selected = selected;
                        item.Focused = focused;
                    }
                }
                while (listView.Items.Count > newItems.Count)
                {
                    listView.Items.RemoveAt(listView.Items.Count - 1);
                }
            }
            finally
            {
                listView.EndUpdate();
            }
        }

        public static IEnumerable<int> MoveSelectedIndexes(int itemCount, IEnumerable<int> selectedIndexes, bool upwards)
        {
            var selectedIndexesArray = selectedIndexes.ToArray();
            var items = Enumerable.Range(0, itemCount).ToArray();
            var newItems = MoveItems(items, selectedIndexesArray, upwards);
            var reverseMap = new int[items.Length];
            for (int i = 0; i < items.Length; i++ )
            {
                reverseMap[newItems[i]] = i;
            }
            return selectedIndexesArray.Select((item, index) => reverseMap[item]);
        }
    }
}
