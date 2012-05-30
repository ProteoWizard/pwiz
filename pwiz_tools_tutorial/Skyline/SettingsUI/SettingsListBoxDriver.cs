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
        public SettingsListBoxDriver(CheckedListBox listBox, SettingsList<TItem> list)
        {
            ListBox = listBox;
            List = list;
        }

        public CheckedListBox ListBox { get; private set; }
        public SettingsList<TItem> List { get; private set; }

        public TItem[] Chosen { get { return GetChosen(null); } }

        public TItem[] GetChosen(ItemCheckEventArgs e)
        {
            List<TItem> listChosen = new List<TItem>();
            for (int i = 0; i < ListBox.Items.Count; i++)
            {
                TItem item;
                bool checkItem = ListBox.GetItemChecked(i);

                // If even refers to this item, then use the check state in the event.
                if (e != null && e.Index == i)
                    checkItem = (e.NewValue == CheckState.Checked);

                if (checkItem && List.TryGetValue(ListBox.Items[i].ToString(), out item))
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
                int i = ListBox.Items.Add(name);

                // Set checkbox state from chosen list.
                ListBox.SetItemChecked(i, chosen.Contains(item));

                // Select the previous selection if it is seen.
                if (ListBox.Items[i].ToString() == selectedItemLast)
                    ListBox.SelectedIndex = i;
            }
            ListBox.EndUpdate();
        }

        public void EditList()
        {
            IEnumerable<TItem> listNew = List.EditList(ListBox.TopLevelControl, null);
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
                var checkedNames = new List<string>();
                for (int i = 0; i < ListBox.Items.Count; i++)
                {
                    if (ListBox.GetItemChecked(i))
                        checkedNames.Add(ListBox.Items[i].ToString());
                }
                return checkedNames.ToArray();                
            }

            set
            {
                for (int i = 0; i < ListBox.Items.Count; i++)
                {
                    ListBox.SetItemChecked(i,
                        value.Contains(ListBox.Items[i].ToString()));
                }                                
            }
        }

        #endregion
    }
}
