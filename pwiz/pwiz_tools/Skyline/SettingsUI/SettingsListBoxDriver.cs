/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public class SettingsListBoxDriver<TItem>
        where TItem : IKeyContainer<string>, IXmlSerializable
    {
        public SettingsListBoxDriver(ListBox listBox, SettingsList<TItem> list)
        {
            ListBox = listBox;
            List = list;
        }

        public ListBox ListBox { get; private set; }
        public CheckedListBox CheckedListBox { get { return ListBox as CheckedListBox; } }
        public SettingsList<TItem> List { get; private set; }

        public TItem[] Chosen { get { return GetChosen(null); } }

        public virtual TItem[] GetChosen(ItemCheckEventArgs e)
        {
            if (CheckedListBox == null)
                return new TItem[0];

            List<TItem> listChosen = new List<TItem>();
            for (int i = 0; i < CheckedListBox.Items.Count; i++)
            {
                TItem item;
                bool checkItem = CheckedListBox.GetItemChecked(i);

                // If event refers to this item, then use the check state in the event.
                if (e != null && e.Index == i)
                    checkItem = (e.NewValue == CheckState.Checked);

                if (checkItem && List.TryGetValue(CheckedListBox.Items[i].ToString(), out item))
                    listChosen.Add(item);
            }
            return listChosen.ToArray();                            
        }

        public void LoadList()
        {
            LoadList(Chosen);
        }

        public void LoadList(IList<TItem> chosen)
        {
            string selectedItemLast = null;
            if (ListBox.SelectedItem != null)
                selectedItemLast = ListBox.SelectedItem.ToString();
            LoadList(selectedItemLast, chosen);
        }

        public void LoadList(string selectedItemLast, IList<TItem> chosen)
        {
            ListBox.BeginUpdate();
            ListBox.Items.Clear();

            foreach (TItem item in List)
            {
                string name = item.GetKey();

                // "GetChosen(ItemCheckEventArgs)" assumes that the strings in the
                // ListBox are the same as the Keys of the items.
                // Assert that the List does not want to use a different string as the 
                // display name for an item: that is not supported by this SettingsListBoxDriver.
                Debug.Assert(name == List.GetDisplayName(item));
                
                int i = ListBox.Items.Add(name);

                SetCheckedBoxes(chosen, i, item);
                // Select the previous selection if it is seen.
                if (ListBox.Items[i].ToString() == selectedItemLast)
                    ListBox.SelectedIndex = i;
            }
            ListBox.EndUpdate();
        }

        protected virtual void SetCheckedBoxes(IList<TItem> chosen, int i, TItem item)
        {
            if (CheckedListBox != null)
            {
                // Set checkbox state from chosen list.
                CheckedListBox.SetItemChecked(i, chosen.Contains(item));
            }
        }

        public void EditList()
        {
            EditList(null);
        }

        public void EditList(object tag)
        {
            IEnumerable<TItem> listNew = List.EditList(ListBox.TopLevelControl, tag);
            if (listNew != null)
            {
                List.Clear();
                List.AddRange(listNew);

                // Reload from the edited list.
                LoadList();
            }
        }

        #region Functional test support
        
        public string[] CheckedNames
        {
            get
            {
                if (CheckedListBox == null)
                    return new string[0];
                var checkedNames = new List<string>();
                for (int i = 0; i < CheckedListBox.Items.Count; i++)
                {
                    if (CheckedListBox.GetItemChecked(i))
                        checkedNames.Add(CheckedListBox.Items[i].ToString());
                }
                return checkedNames.ToArray();                
            }

            set
            {
                if (CheckedListBox != null)
                {
                    for (int i = 0; i < ListBox.Items.Count; i++)
                    {
                        CheckedListBox.SetItemChecked(i,
                                               value.Contains(ListBox.Items[i].ToString()));
                    }
                }
            }
        }

        #endregion
    }
}
