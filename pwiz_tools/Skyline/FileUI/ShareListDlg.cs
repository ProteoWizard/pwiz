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
using pwiz.Skyline.Controls;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public partial class ShareListDlg<TList, TItem> : FormEx
        where TList : IList<TItem>, IListSerializer<TItem>, IListDefaults<TItem>, IListEditorSupport
        where TItem : IKeyContainer<string>, IXmlSerializable
    {
        private const string SETTINGS_DEFINITION_FILTER = "Skyline Settings (*.skys)|*.skys|All Files|*.*";
        private string _label;

        public ShareListDlg(TList list)
        {
            InitializeComponent();

            List = list;
            Filter = SETTINGS_DEFINITION_FILTER;
            
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

                Text = string.Format("Save {0}", _label);
                labelMessage.Text = string.Format("Select the {0} you want to save to a file.", _label.ToLower());
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
            int countDefaults = List.ExcludeDefaults ? List.GetDefaults(List.RevisionIndexCurrent).Count() : 0;
            foreach (TItem item in List)
            {
                if (index < countDefaults)
                    continue;
                ListBox.Items.Add(item.GetKey());
                index++;
            }
            ListBox.EndUpdate();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = Settings.Default.ActiveDirectory,
                CheckPathExists = true,
                Filter = Filter
            };
            saveFileDialog.ShowDialog(this);
            if (!string.IsNullOrEmpty(saveFileDialog.FileName))
                OkDialog(saveFileDialog.FileName);
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
                    if (!fs.CanSave(true))
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
                MessageBox.Show(this, "An error occurred: " + exception.Message, Program.Name);
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
            bool checkAll = cbSelectAll.Checked;
            for (int i = 0; i < listboxListItems.Items.Count; i++)
                listboxListItems.SetItemChecked(i, checkAll);
        }

        public static bool Import(Form parent, TList listDest)
        {
            return ImportFile(parent, listDest, GetImportFileName(parent, SETTINGS_DEFINITION_FILTER));
        }

        public static string GetImportFileName(Form parent, string filter)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                InitialDirectory = Settings.Default.ActiveDirectory,
                CheckPathExists = true,
                Filter = filter
            };
            if (dialog.ShowDialog(parent) == DialogResult.Cancel)
                return null;
            return dialog.FileName;
        }

        public static bool ImportFile(Form parent, TList listDest, string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            XmlSerializer xmlSerializer = new XmlSerializer(listDest.SerialType);
            TList loadedItems;
            try
            {
                using (var stream = File.OpenRead(fileName))
                {
                    loadedItems = (TList)xmlSerializer.Deserialize(stream);
                }
            }
            catch (Exception exception)
            {
                new MessageBoxHelper(parent).ShowXmlParsingError(string.Format("Failure loading {0}.", fileName),
                                                                 fileName, exception);
                return false;
            }

            // Check for and warn about existing reports.
            List<string> existing = new List<string>(from reportSpec in loadedItems
                                                     where listDest.ContainsKey(reportSpec.GetKey())
                                                     select reportSpec.GetKey());

            if (existing.Count > 0)
            {
                string messageFormat = existing.Count == 1 ?
                   "The name '{0}' already exists. Do you want to replace it?" :
                   "The following names already exist:\n\n{0}\n\nDo you want to replace them?";
                var result = MessageBox.Show(string.Format(messageFormat, string.Join("\n", existing.ToArray())),
                                             Program.Name, MessageBoxButtons.YesNoCancel, MessageBoxIcon.None,
                                             MessageBoxDefaultButton.Button2);
                switch (result)
                {
                    case DialogResult.Cancel:
                        return false;
                    case DialogResult.Yes:
                        // Overwrite everything
                        existing.Clear();
                        break;
                }
            }

            foreach (TItem reportSpec in loadedItems)
            {
                // Skip anything still in the existing list
                if (existing.Contains(reportSpec.GetKey()))
                    continue;
                RemoveReport(listDest, reportSpec.GetKey());
                listDest.Add(reportSpec);
            }
            return true;
        }

        // Another implementation of the above ImportFile used for importing a skyr file in a command line scenario 
        // Maybe move this to a more sensable location.
        public static bool ImportFile(TextWriter _out, TList listDest, string fileName, bool? resolveSkyrConflictsBySkipping)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            XmlSerializer xmlSerializer = new XmlSerializer(listDest.SerialType);
            TList loadedItems;
            try
            {
                using (var stream = File.OpenRead(fileName))
                {
                    loadedItems = (TList)xmlSerializer.Deserialize(stream);
                }
            }

            catch (Exception exception)
            {
                _out.WriteLine(string.Format("Failure loading {0}. \n {1}", fileName, exception));
                return false;
            }

            // Check for and warn about existing reports.
            List<string> existing = new List<string>(from reportSpec in loadedItems
                                                     where listDest.ContainsKey(reportSpec.GetKey())
                                                     select reportSpec.GetKey());

            if (existing.Count > 0)
            {
                string messageFormat = existing.Count == 1 ?
                   "The name '{0}' already exists." :
                   "The following names already exist:\n\n{0}\n\n";
                _out.WriteLine(string.Format(messageFormat, string.Join("\n", existing.ToArray())));
                if (resolveSkyrConflictsBySkipping == null)
                {
                    _out.WriteLine("Error: Please specify a way to resolve conflicts.");
                    _out.WriteLine("       Use command --resolve-skyr-conflicts=< overwrite | skip >");
                    return false;
                }
                if (resolveSkyrConflictsBySkipping == true)
                {
                    _out.WriteLine("Resolving conflicts by skipping.");
                    // The objects are skipped below for being in the list called existing
                }
                if (resolveSkyrConflictsBySkipping == false)
                {
                    _out.WriteLine("Resolving conflicts by overwriting.");
                    existing.Clear();
                    // All conflicts are overwritten because existing is empty. 
                }
            }

            foreach (TItem reportSpec in loadedItems)
            {
                // Skip anything still in the existing list
                if (existing.Contains(reportSpec.GetKey()))
                    continue;
                RemoveReport(listDest, reportSpec.GetKey());
                listDest.Add(reportSpec);
            }
            return true;
        }

        private static void RemoveReport(IList<TItem> reportSpecList, String name)
        {
            for (int i = 0; i < reportSpecList.Count; i++)
            {
                if (Equals(name, reportSpecList[i].GetKey()))
                {
                    reportSpecList.RemoveAt(i);
                    break;
                }
            }
        }
    }
}
