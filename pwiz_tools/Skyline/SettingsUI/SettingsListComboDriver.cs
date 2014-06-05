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
    /// <typeparam name="TItem">Type of the items in the settings list</typeparam>
    public class SettingsListComboDriver<TItem>
        where TItem : IKeyContainer<string>, IXmlSerializable
    {
        private int _selectedIndexLast;

        public SettingsListComboDriver(ComboBox combo, SettingsListBase<TItem> list)
            : this(combo, list, true)
        {            
        }

        public SettingsListComboDriver(ComboBox combo, SettingsListBase<TItem> list, bool isVisibleEditting)
        {
            IsVisibleEditting = isVisibleEditting;
            Combo = combo;
            List = list;
        }

        public bool IsVisibleEditting { get; private set; }
        public bool IsInSelectedIndexChangedEvent { get; private set; }
        public ComboBox Combo { get; private set; }
        public SettingsListBase<TItem> List { get; private set; }

        public void LoadList(string selectedItemLast)
        {
            try
            {
                Combo.BeginUpdate();
                Combo.Items.Clear();
                foreach (TItem item in List.ToArrayStd())
                {
                    string name = List.GetDisplayName(item);
                    int i = Combo.Items.Add(name);
                    if (Equals(Combo.Items[i].ToString(), selectedItemLast))
                        Combo.SelectedIndex = i;
                }
                // If nothing was added, add a blank to avoid starting with "Add..." selected.
                if (Combo.Items.Count == 0)
                    Combo.Items.Add(string.Empty);
                if (IsVisibleEditting)
                {
                    if (List is IItemEditor<TItem>)
                    {
                        Combo.Items.Add(Resources.SettingsListComboDriver_Add);
                        Combo.Items.Add(Resources.SettingsListComboDriver_Edit_current);
                    }
                    Combo.Items.Add(Resources.SettingsListComboDriver_Edit_list);
                }
                if (Combo.SelectedIndex < 0)
                    Combo.SelectedIndex = 0;

                ComboHelper.AutoSizeDropDown(Combo);
            }
            finally
            {
                Combo.EndUpdate();
            }
        }

        public TItem SelectedItem
        {
            get
            {
                int selectedIndex = Combo.SelectedIndex;
                if (0 > selectedIndex || selectedIndex >= List.Count)
                    return default(TItem);
                return List[selectedIndex];
            }
        }

        private string SelectedString
        {
            get { return Combo.SelectedItem != null ? Combo.SelectedItem.ToString() : string.Empty; }
        }

        public bool AddItemSelected()
        {
            return Equals(Resources.SettingsListComboDriver_Add, SelectedString);
        }

        public bool EditCurrentSelected()
        {
            return Equals(Resources.SettingsListComboDriver_Edit_current, SelectedString);
        }

        public bool EditListSelected()
        {
            return Equals(Resources.SettingsListComboDriver_Edit_list, SelectedString);
        }

        public bool SelectedIndexChangedEvent(object sender, EventArgs e)
        {
            IsInSelectedIndexChangedEvent = true;
            try
            {
                bool handled = false;
                if (AddItemSelected())
                {
                    AddItem();
                    handled = true;
                }
                else if (EditCurrentSelected())
                {
                    EditCurrent();
                    handled = true;
                }
                else if (EditListSelected())
                {
                    EditList();
                    handled = true;
                }
                _selectedIndexLast = Combo.SelectedIndex;
                return handled;
            }
            finally
            {
                IsInSelectedIndexChangedEvent = false;
            }
        }

        public void AddItem()
        {
            var itemEditor = List as IItemEditor<TItem>;
            if (itemEditor == null)
                return;

            TItem itemNew = itemEditor.NewItem(Combo.TopLevelControl, null, null);
            if (!Equals(itemNew, default(TItem)))
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

        public void EditCurrent()
        {
            var itemEditor = List as IItemEditor<TItem>;
            if (itemEditor == null)
                return;

            int i = _selectedIndexLast;
            TItem itemNew = default(TItem);
            if ((i >= List.ExcludeDefaults) && (i < List.Count))
                itemNew = itemEditor.EditItem(Combo.TopLevelControl, List[i], List, null);
            if (!Equals(itemNew, default(TItem)) && !Equals(itemNew, List[i]))
            {
                List[i] = itemNew;
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
            IEnumerable<TItem> listNew = List.EditList(Combo.TopLevelControl, null);
            if (listNew != null)
            {
                string selectedItemLast;
                if (_selectedIndexLast < 0 || _selectedIndexLast > Combo.Items.Count - 1)
                    selectedItemLast = null;
                else
                    selectedItemLast = Combo.Items[_selectedIndexLast].ToString();
                if (List.ExcludeDefaults < 1)
                    List.Clear();
                else
                {
                    // If default items were excluded from editing,
                    // then make sure they are preserved as the first items.
                    List<TItem> tmpList = new List<TItem>();
                    int countExclude = List.ExcludeDefaults;
                    for (int i = 0; i < countExclude; i++)
                        tmpList.Add(List[i]);
                    List.Clear();
                    List.AddRange(tmpList);
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