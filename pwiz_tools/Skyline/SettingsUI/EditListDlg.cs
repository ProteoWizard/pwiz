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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// For naming of the resource file
    /// </summary>
    public class EditListDlg
    {        
    }

    public partial class EditListDlg<TList, TItem> : FormEx
        where TList : ICollection<TItem>, IListDefaults<TItem>, IListEditorSupport
        where TItem : IKeyContainer<string>
    {
        private readonly TList _model;
        private readonly IItemEditor<TItem> _editor;

        private readonly List<TItem> _list;

        public EditListDlg(TList model, object tag)
        {
            _model = model;
            _list = model.ToList();
            _editor = model as IItemEditor<TItem>;

            TagEx = tag;

            InitializeComponent();

            Icon = Resources.Skyline;
            Text = model.Title;
            labelListName.Text = model.Label;

            if (_editor == null)
            {
                // Hide the Add and edit buttons.
                btnAdd.Visible = false;
                btnCopy.Visible = false;
                btnEdit.Visible = false;

                // Move other vertically aligned buttons up.
                int delta = btnRemove.Top - btnAdd.Top;
                btnRemove.Top -= delta;
                btnUp.Top -= delta;
                btnDown.Top -= delta;
                btnReset.Top -= delta;
            }
            if (!model.AllowReset)
                btnReset.Visible = false;

            ReloadList();
        }

        public object TagEx { get; private set; }

        private void ReloadList()
        {
            // Remove the default settings item before reloading.
            int countExclude = _model.ExcludeDefaults;
            for (int i = 0; i < countExclude; i++)
                _list.RemoveAt(0);

            listBox.BeginUpdate();
            listBox.SelectedIndex = -1;
            listBox.Items.Clear();
            foreach (TItem item in _list)
                listBox.Items.Add(item.GetKey());
            if (listBox.Items.Count > 0)
                listBox.SelectedIndex = 0;
            listBox.EndUpdate();
        }

        /// <summary>
        /// The following 2 functions had to be split up because _list does not include the defaults,
        /// so the user can overwrite any of the defaults of a list. But _model does not include the
        /// modified values, so just returning _model will never update the list.
        /// </summary>
        public IEnumerable<TItem> GetAll()
        {
            var arrayDefaults = _model.GetDefaults(_model.RevisionIndexCurrent).ToArray();
            for (int i = 0; i < _model.ExcludeDefaults; i++)
                yield return arrayDefaults[i];

            foreach (var item in _list)
                yield return item;
        }

        public IEnumerable<TItem> GetAllEdited()
        {
            return _list;
        }

        private void listBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool enable = (listBox.SelectedIndex != -1);
            btnCopy.Enabled = enable;
            btnEdit.Enabled = enable;
            btnRemove.Enabled = enable;
            btnUp.Enabled = enable;
            btnDown.Enabled = enable;
        }

        public void SelectItem(string name)
        {
            listBox.SelectedItem = name;
        }

        public void SelectLastItem()
        {
            listBox.SelectedIndex = listBox.Items.Count - 1;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            AddItem();
        }

        public void AddItem()
        {
            CheckDisposed();
            TItem item = _editor.NewItem(this, GetAll(), TagEx);
            if (!Equals(item, default(TItem)))
            {
                // Insert after current selection.
                int i = listBox.SelectedIndex + 1;
                _list.Insert(i, item);
                listBox.Items.Insert(i, item.GetKey());
                listBox.SelectedIndex = i;
            }
        }

        public void AddItem(TItem item)
        {
            CheckDisposed();
            if (!Equals(item, default(TItem)))
            {
                int i = listBox.SelectedIndex + 1;
                _list.Insert(i, item);
            }
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            CopyItem();
        }

        public void CopyItem()
        {
            CheckDisposed();
            int i = listBox.SelectedIndex;
            TItem item = _editor.EditItem(this, _editor.CopyItem(_list[i]), GetAll(), TagEx);
            if (!Equals(item, default(TItem)))
            {
                // Insert after current selection.
                i++;
                _list.Insert(i, item);
                listBox.Items.Insert(i, item.GetKey());
                listBox.SelectedIndex = i;
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            EditItem();
        }

        public void EditItem()
        {
            if (_editor == null)
                return;
            CheckDisposed();
            int i = listBox.SelectedIndex;
            if (i == -1)
                return;
            TItem item = _editor.EditItem(this, _list[i], GetAll(), TagEx);
            if (!Equals(item, default(TItem)))
            {
                _list[i] = item;
                listBox.Items[i] = item.GetKey();
            }
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            RemoveItem();
        }

        public void RemoveItem()
        {
            int i = listBox.SelectedIndex;
            _list.RemoveAt(i);
            listBox.Items.RemoveAt(i);
            listBox.SelectedIndex = Math.Min(i, listBox.Items.Count - 1);
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            MoveItemUp();
        }

        public void MoveItemUp()
        {
            int i = listBox.SelectedIndex;
            if (i > 0)
            {
                // Swap with item at index - 1
                TItem item = _list[i];
                object itemListBox = listBox.Items[i];
                _list[i] = _list[i - 1];
                listBox.Items[i] = listBox.Items[i - 1];
                i--;
                _list[i] = item;
                listBox.Items[i] = itemListBox;

                listBox.SelectedIndex = i;
            }
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            MoveItemDown();
        }

        public void MoveItemDown()
        {
            int i = listBox.SelectedIndex;
            if (i < listBox.Items.Count - 1)
            {
                // Swap with item at index + 1
                TItem item = _list[i];
                object itemListBox = listBox.Items[i];
                _list[i] = _list[i + 1];
                listBox.Items[i] = listBox.Items[i + 1];
                i++;
                _list[i] = item;
                listBox.Items[i] = itemListBox;

                listBox.SelectedIndex = i;
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(this, Resources.EditListDlg_btnReset_Click_This_will_reset_the_list_to_its_default_values_Continue,
                                                  Program.Name, MessageBoxButtons.YesNo);

            if (result == DialogResult.Yes)
            {
                ResetList();
            }
        }

        public void ResetList()
        {
            _list.Clear();
            _list.AddRange(_model.GetDefaults(_model.RevisionIndexCurrent));
            ReloadList();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}