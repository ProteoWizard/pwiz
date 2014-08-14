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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    /// <summary>
    /// For naming of the resource file
    /// </summary>
    public class ShareListDlg
    {
    }

    public partial class ShareListDlg<TList, TItem> : FormEx
        where TList : SerializableSettingsList<TItem>
        where TItem : IKeyContainer<string>, IXmlSerializable
    {
        private string _label;

        public ShareListDlg(TList list)
        {
            InitializeComponent();

            List = list;
            Filter = TextUtil.FileDialogFilterAll(Resources.ShareListDlg_ShareListDlg_Skyline_Settings, SrmSettingsList.EXT_SETTINGS);
            
            LoadList();
        }

        public string Filter { get; set; }

        public TList List { get; private set; }

        public string Label
        {
            get { return _label; }
            set
            {
                _label = value;

                Text = string.Format(Resources.ShareListDlg_Label_Save__0__, _label);
                labelMessage.Text = string.Format(Resources.ShareListDlg_Label_Select_the__0__you_want_to_save_to_a_file, 
                    _label.ToLower());
            }
        }

        public IEnumerable<string> ChosenNames
        {
            get
            {
                foreach (var item in listboxListItems.CheckedItems)
                    yield return item.ToString();
            }

            set
            {
                for (int i = 0; i < listboxListItems.Items.Count; i++)
                {
                    listboxListItems.SetItemChecked(i,
                        value.Contains(listboxListItems.Items[i].ToString()));
                }
            }
        }

        private ListBox ListBox { get { return listboxListItems; } }

        public void LoadList()
        {
            ListBox.BeginUpdate();
            ListBox.Items.Clear();
            int index = 0;
            int countExclude = List.ExcludeDefaults;
            foreach (TItem item in List)
            {
                if (index++ < countExclude)
                    continue;
                ListBox.Items.Add(item.GetKey());
            }
            ListBox.EndUpdate();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            using (var saveFileDialog = new SaveFileDialog
                {
                    InitialDirectory = Settings.Default.ActiveDirectory,
                    CheckPathExists = true,
                    Filter = Filter
                })
            {
                saveFileDialog.ShowDialog(this);
                if (!string.IsNullOrEmpty(saveFileDialog.FileName))
                    OkDialog(saveFileDialog.FileName);
            }
        }

        public void OkDialog(string fileName)
        {
            ICollection<TItem> listSave = List.CreateEmptyList();
            foreach (string itemName in listboxListItems.CheckedItems)
            {
                TItem item = GetItem(itemName);
                if (!Equals(item, default(TItem)))
                    listSave.Add(item);
            }
            try
            {
                XmlSerializer xmlSerializer = new XmlSerializer(List.SerialType);
                using (FileSaver fs = new FileSaver(fileName))
                {
                    if (!fs.CanSave(this))
                        return;

                    using (FileStream stream = File.OpenWrite(fs.SafeName))
                    {
                        xmlSerializer.Serialize(stream, listSave);
                        stream.Close();
                        fs.Commit();
                    }
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, TextUtil.LineSeparate(Resources.ShareListDlg_OkDialog_An_error_occurred, exception.Message), Program.Name);
            }
            Close();            
        }

        private TItem GetItem(string name)
        {
            foreach (TItem item in List)
            {
                if (Equals(name, item.GetKey()))
                    return item;
            }
            return default(TItem);
        }

        private void listboxListItems_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            // listboxReports.CheckedItems is not updated until after this event returns.
            if (e.NewValue == CheckState.Checked || listboxListItems.CheckedItems.Count > 1)
            {
                btnOk.Enabled = true;
            }
            else
            {
                btnOk.Enabled = false;
            }
        }

        private void cbSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            SelectAll(cbSelectAll.Checked);
        }

        public void SelectAll(bool selectAllChecked)
        {
            for (int i = 0; i < listboxListItems.Items.Count; i++)
                listboxListItems.SetItemChecked(i, selectAllChecked);
        }

        //IListSerilizer<TItem> XmlMappedList<string, TItem>, IListDefaults<TItem>, IListEditor<TItem>, IListEditorSupport
        public static bool Import(Form parent, TList listDest)
        {
            return ImportFile(parent, listDest, GetImportFileName(parent,
                TextUtil.FileDialogFilterAll(Resources.ShareListDlg_ShareListDlg_Skyline_Settings, SrmSettingsList.EXT_SETTINGS)));
        }

        public static string GetImportFileName(Form parent, string filter)
        {
            using (var dialog = new OpenFileDialog
                {
                    InitialDirectory = Settings.Default.ActiveDirectory,
                    CheckPathExists = true,
                    Filter = filter
                })
            {
                if (dialog.ShowDialog(parent) == DialogResult.Cancel)
                    return null;
                return dialog.FileName;
            }
        }

        public static bool ImportFile(Form parent, TList listDest, string fileName)
        {
            bool sucess;
            try
            {
                sucess = listDest.ImportFile(fileName, ResolveImportConflicts);
            }
            catch (Exception x)
            {
                new MessageBoxHelper(parent).ShowXmlParsingError(string.Format(Resources.ShareListDlg_ImportFile_Failure_loading__0__, fileName),
                                                                 fileName, x.InnerException);
                return false;
            }
            return sucess;
        }

        private static IList<string> ResolveImportConflicts(IList<string> existing)
        {
            var multipleMessage = TextUtil.LineSeparate(Resources.ShareListDlg_ImportFile_The_following_names_already_exist, string.Empty,
                                                    "{0}", string.Empty, Resources.ShareListDlg_ImportFile_Do_you_want_to_replace_them); // Not L10N
            string messageFormat = existing.Count == 1 ?
               Resources.ShareListDlg_ImportFile_The_name__0__already_exists_Do_you_want_to_replace_it :
               multipleMessage;
            var result = MultiButtonMsgDlg.Show(null, string.Format(messageFormat, TextUtil.LineSeparate(existing)),
                MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, true);
            if (result == DialogResult.Yes)
            {
                // Overwrite everything
                existing.Clear();
            }
            else if (result == DialogResult.Cancel)
            {
                return null;
            }
            return existing;
        }
    }
}
