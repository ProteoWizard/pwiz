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
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// Drives a combo box with values from a <see cref="SettingsList{T}"/>.
    /// Initially this class derived from <see cref="ComboBox"/>, but that
    /// proved very problematic for the form designer.
    /// </summary>
    /// <typeparam name="T">Type of the items in the settings list</typeparam>
    public class SettingsListComboDriver<T>
        where T : IKeyContainer<string>, IXmlSerializable
    {
        private const string ADD_ITEM = "<Add...>";
        private const string EDIT_LIST_ITEM = "<Edit list...>";

        private int _selectedIndexLast;

        public SettingsListComboDriver(ComboBox combo, SettingsList<T> list)
        {
            Combo = combo;
            List = list;
        }

        public ComboBox Combo { get; private set; }
        public SettingsList<T> List { get; private set; }

        public void LoadList(string selectedItemLast)
        {
            try
            {
                Combo.BeginUpdate();
                Combo.Items.Clear();
                foreach (T item in List)
                {
                    string name = List.GetKey(item);
                    int i = Combo.Items.Add(name);
                    if (Combo.Items[i].ToString() == selectedItemLast)
                        Combo.SelectedIndex = i;
                }
                // If nothing was added, add a blank to avoid starting with "Add..." selected.
                if (Combo.Items.Count == 0)
                    Combo.Items.Add("");
                Combo.Items.Add(ADD_ITEM);
                Combo.Items.Add(EDIT_LIST_ITEM);
                if (Combo.SelectedIndex < 0)
                    Combo.SelectedIndex = 0;
            }
            finally
            {
                Combo.EndUpdate();
            }
        }

        public bool AddItemSelected()
        {
            return (ADD_ITEM == Combo.SelectedItem.ToString());
        }

        public bool EditListSelected()
        {
            return (EDIT_LIST_ITEM == Combo.SelectedItem.ToString());
        }

        public void SelectedIndexChangedEvent(object sender, EventArgs e)
        {
            if (AddItemSelected())
            {
                AddItem();
            }
            else if (EditListSelected())
            {
                EditList();
            }
            _selectedIndexLast = Combo.SelectedIndex;
        }

        public void AddItem()
        {
            T itemNew = List.NewItem(Combo.TopLevelControl, null, null);
            if (!Equals(itemNew, default(T)))
            {
                List.Add(itemNew);
                LoadList(itemNew.GetKey());
            }
            else
            {
                // Reset the selected index before edit was chosen.
                Combo.SelectedIndex = _selectedIndexLast;
            }
        }

        public void EditList()
        {
            IEnumerable<T> listNew = List.EditList(Combo.TopLevelControl, null);
            if (listNew != null)
            {
                string selectedItemLast = Combo.Items[_selectedIndexLast].ToString();
                if (!List.ExcludeDefault)
                    List.Clear();
                else
                {
                    // If the default item was excluded from editing,
                    // then make sure it is preserved as the first item.
                    T itemDefault = List[0];
                    List.Clear();
                    List.Add(itemDefault);
                }
                List.AddRange(listNew);
                LoadList(selectedItemLast);
            }
            else
            {
                // Reset the selected index before edit was chosen.
                Combo.SelectedIndex = _selectedIndexLast;
            }
        }
    }
}