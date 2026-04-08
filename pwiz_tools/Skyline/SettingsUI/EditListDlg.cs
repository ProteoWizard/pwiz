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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.GUI;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditListDlg<TList, TItem> : FormEx
        where TList : ICollection<TItem>, IListDefaults<TItem>, IListEditorSupport
        where TItem : IKeyContainer<string>
    {
        private readonly TList _model;
        private readonly IItemEditor<TItem> _editor;

        private readonly List<TItem> _list;
        private readonly Panel _spacerAfterEdit;
        private readonly Panel _spacerAfterDown;

        public EditListDlg(TList model, object tag)
        {
            _model = model;
            _list = model.ToList();
            _editor = model as IItemEditor<TItem>;

            TagEx = tag;

            InitializeComponent();

            // Add spacers between button groups in the FlowLayoutPanel:
            // Group 1 (CRUD): Add, Remove, Rename, Edit
            // Group 2 (Reorder): Up, Down
            // Group 3 (Manage): Reset, Copy, Import, Share
            _spacerAfterEdit = CreateButtonGroupSpacer();
            pnlButtons.Controls.Add(_spacerAfterEdit);
            pnlButtons.Controls.SetChildIndex(_spacerAfterEdit, pnlButtons.Controls.IndexOf(btnUp));

            _spacerAfterDown = CreateButtonGroupSpacer();
            pnlButtons.Controls.Add(_spacerAfterDown);
            pnlButtons.Controls.SetChildIndex(_spacerAfterDown, pnlButtons.Controls.IndexOf(btnReset));

            Icon = Resources.Skyline;
            Text = model.Title;
            labelListName.Text = model.Label;

            if (_editor == null)
            {
                // Hide the Add and edit buttons.
                pnlButtons.Controls.Remove(btnAdd);
                pnlButtons.Controls.Remove(btnCopy);
                pnlButtons.Controls.Remove(btnEdit);
            }
            if (!model.AllowReset)
                pnlButtons.Controls.Remove(btnReset);

            // Rename is available when items implement IRenameable<T>
            bool canRename = typeof(IRenameable<TItem>).IsAssignableFrom(typeof(TItem));
            if (!canRename)
                pnlButtons.Controls.Remove(btnRename);
            else
                btnRename.Enabled = false;

            // Import/Export available when list supports serialization
            bool canSerialize = model is IListSerializer<TItem>;
            if (!canSerialize)
            {
                pnlButtons.Controls.Remove(btnImport);
                pnlButtons.Controls.Remove(btnShare);
            }

            ReloadList();
        }

        public object TagEx { get; private set; }

        public int ListCount => listBox.Items.Count;

        private static Panel CreateButtonGroupSpacer()
        {
            return new Panel { Width = 75, Height = 17, Margin = new Padding(0) };
        }

        private void ReloadList()
        {
            // Remove the default settings items before reloading.
            // The list may have fewer items than ExcludeDefaults if some defaults were removed.
            int countExclude = Math.Min(_model.ExcludeDefaults, _list.Count);
            for (int i = 0; i < countExclude; i++)
                _list.RemoveAt(0);

            listBox.BeginUpdate();
            listBox.SelectedIndex = -1;
            listBox.Items.Clear();
            foreach (TItem item in _list)
            {
                string name = _model.GetDisplayName(item);
                listBox.Items.Add(name);
            }
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
            btnRename.Enabled = enable;
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

        private void btnRename_Click(object sender, EventArgs e)
        {
            RenameItem();
        }

        public void RenameItem()
        {
            int i = listBox.SelectedIndex;
            if (i < 0)
                return;

            var item = _list[i];
            string currentName = item.GetKey();

            using (var form = new Form())
            {
                form.Text = SettingsUIResources.EditListDlg_Rename.Replace(@"&", string.Empty);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.Width = 350;
                form.Height = 130;

                var lblName = new Label { Left = 10, Top = 15, Text = SettingsUIResources.EditListDlg_RenameItem_Enter_new_name, AutoSize = true };
                var textBox = new TextBox { Left = 10, Top = 35, Width = 310, Text = currentName };
                textBox.SelectionStart = textBox.Text.Length;
                var okButton = new Button { Text = CommonAlertDlg.GetDefaultButtonText(DialogResult.OK), Left = 150, Width = 80, Top = 65, DialogResult = DialogResult.OK };
                var cancelButton = new Button { Text = CommonAlertDlg.GetDefaultButtonText(DialogResult.Cancel), Left = 240, Width = 80, Top = 65, DialogResult = DialogResult.Cancel };

                form.Controls.AddRange(new Control[] { lblName, textBox, okButton, cancelButton });
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                if (form.ShowDialog(this) != DialogResult.OK)
                    return;

                string newName = textBox.Text?.Trim();
                if (string.IsNullOrEmpty(newName) || newName == currentName)
                    return;

                // Check for duplicate names
                if (_list.Any(existing => existing.GetKey() == newName))
                {
                    MessageDlg.Show(this,
                        string.Format(SettingsUIResources.EditListDlg_RenameItem_An_item_with_the_name__0__already_exists, newName));
                    return;
                }

                var renameable = (IRenameable<TItem>)item;
                _list[i] = renameable.ChangeName(newName);
                listBox.Items[i] = newName;
            }
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            ImportItems();
        }

        public void ImportItems()
        {
            var serializer = _model as IListSerializer<TItem>;
            if (serializer == null)
                return;

            using (var openDlg = new OpenFileDialog())
            {
                openDlg.Filter = TextUtil.FileDialogFilterAll(_model.Title, serializer.FileExtension);
                if (openDlg.ShowDialog(this) != DialogResult.OK)
                    return;

                ImportItemsFromFile(openDlg.FileName);
            }
        }

        public void ImportItemsFromFile(string filePath)
        {
            var serializer = _model as IListSerializer<TItem>;
            if (serializer == null)
                return;

            IList<TItem> loadedItems;
            try
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var xmlSerializer = new XmlSerializer(serializer.DeserialType);
                    loadedItems = ((ICollection<TItem>)xmlSerializer.Deserialize(stream)).ToList();
                }
            }
            catch (Exception ex)
            {
                MessageDlg.ShowWithException(this,
                    string.Format(Resources.SerializableSettingsList_ImportFile_Failure_loading__0__, filePath), ex);
                return;
            }

            foreach (var item in loadedItems)
            {
                string key = item.GetKey();
                int existingIndex = _list.FindIndex(x => x.GetKey() == key);
                if (existingIndex >= 0)
                {
                    _list[existingIndex] = item;
                    listBox.Items[existingIndex] = _model.GetDisplayName(item);
                }
                else
                {
                    _list.Add(item);
                    listBox.Items.Add(_model.GetDisplayName(item));
                }
            }

            if (listBox.Items.Count > 0 && listBox.SelectedIndex < 0)
                listBox.SelectedIndex = 0;
        }

        private void btnShare_Click(object sender, EventArgs e)
        {
            ShareItems();
        }

        public void ShareItems()
        {
            var serializer = _model as IListSerializer<TItem>;
            if (serializer == null)
                return;

            // Let user select which items to share
            using (var selectDlg = new ShareListDlg<TItem>(_list, _model.Title))
            {
                if (selectDlg.ShowDialog(this) != DialogResult.OK)
                    return;

                var selectedNames = selectDlg.CheckedItemNames;
                if (selectedNames.Count == 0)
                    return;

                using (var saveDlg = new SaveFileDialog())
                {
                    saveDlg.Filter = TextUtil.FileDialogFilterAll(_model.Title, serializer.FileExtension);
                    saveDlg.DefaultExt = serializer.FileExtension;
                    if (saveDlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    ShareItemsToFile(saveDlg.FileName, selectedNames);
                }
            }
        }

        public void ShareItemsToFile(string filePath, IList<string> itemNames = null)
        {
            var serializer = _model as IListSerializer<TItem>;
            if (serializer == null)
                return;

            try
            {
                var exportList = serializer.CreateEmptyList();
                foreach (var item in _list)
                {
                    if (itemNames == null || itemNames.Contains(item.GetKey()))
                        exportList.Add(item);
                }

                using (var stream = File.Create(filePath))
                {
                    var xmlSerializer = new XmlSerializer(serializer.SerialType);
                    xmlSerializer.Serialize(stream, exportList);
                }
            }
            catch (Exception ex)
            {
                MessageDlg.ShowWithException(this,
                    string.Format(Resources.SerializableSettingsList_ImportFile_Failure_loading__0__, filePath), ex);
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            DialogResult result = MultiButtonMsgDlg.Show(this, SettingsUIResources.EditListDlg_btnReset_Click_This_will_reset_the_list_to_its_default_values_Continue,
                MessageBoxButtons.YesNo);

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

        private void listBox_MouseMove(object sender, MouseEventArgs e)
        {
            string newToolTip = null;
            var itemIndex = listBox.IndexFromPoint(e.Location);
            if (itemIndex >= 0 && itemIndex < _list.Count)
            {
                newToolTip = (_list[itemIndex] as IHasItemDescription)?.ItemDescription.ToString();
            }

            if (newToolTip != helpTip.GetToolTip(listBox))
            {
                helpTip.SetToolTip(listBox, newToolTip);
            }
        }
    }
}
