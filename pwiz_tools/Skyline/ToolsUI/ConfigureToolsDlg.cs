/*
 * Original author: Daniel Broudy <daniel.broudy .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.ToolsUI
{
    public partial class ConfigureToolsDlg : FormEx
    {
        public ConfigureToolsDlg(List<ToolDescription> inList)
        {
            InitializeComponent();

            _previouslySelectedIndex = -1;
            
            ToolList = new List<ToolDescription>();
            LoadTools(inList);
            RefreshListBox();
            Unsaved = false;
            if (ToolList.Count == 0)           
            {
                Add();
            }
            else
            {
                listTools.SelectedIndex = 0;
            }
            textTitle.Enabled = btnDelete.Enabled;
        }

        public int _previouslySelectedIndex;

        public void RefreshListBox()
        {
            listTools.DataSource = null;     
            listTools.DataSource = ToolList;
            listTools.DisplayMember = "Title";            
        }

        private void LoadTools(List<ToolDescription> items )
        {
            ToolList = items;
        }

        public List<ToolDescription> ToolList { get; private set; }

        private bool _unsaved;

        public bool Unsaved
        { 
            
            get { return _unsaved; }
            set
            {
                _unsaved = value;
                btnApply.Enabled = _unsaved;
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            Add();
        }

        public void Add()
        {
            if (CheckPassTool(_previouslySelectedIndex))
            {
                AddDialog(GetTitle(), "", "", "");
            }
            if (ToolList.Count == 1)
            {
                _previouslySelectedIndex = 0;
            }
        }
        // Return a unique title for a New Tool. (eg. [New Tool1])
        private string GetTitle()
        {
            int i = 1;
            do
            {
                if (ToolList.All(item => item.Title != (string.Format("[New Tool{0}]", i))))
                {
                    return string.Format("[New Tool{0}]", i);
                }
                i++;
            } while (true);
        }

        public void AddDialog(string title, string command, string arguments, string initialDirectory )
        {
            ToolDescription newTool = new ToolDescription(title, command, arguments, initialDirectory);
            ToolList.Add(newTool);
            RefreshListBox();
            _previouslySelectedIndex = -1;
            listTools.SelectedIndex = ToolList.Count - 1;
            btnDelete.Enabled = true;
        }

        public void listTools_SelectedIndexChanged(object sender, EventArgs e)
        {                        
            if (listTools.SelectedIndex != -1)
            {
                if (listTools.SelectedIndex != _previouslySelectedIndex)
                {                    
                     if (_previouslySelectedIndex != -1 && !CheckPassTool(_previouslySelectedIndex))
                    {
                        listTools.SelectedIndex = _previouslySelectedIndex;
                        return;
                    }
                    _previouslySelectedIndex = listTools.SelectedIndex;
                    ToolDescription highlighted = ToolList[listTools.SelectedIndex];
                    textTitle.Text = highlighted.Title;
                    textCommand.Text = highlighted.Command;
                    textArguments.Text = highlighted.Arguments;
                    textInitialDirectory.Text = highlighted.InitialDirectory;
                  
                }
                btnMoveUp.Enabled = (listTools.SelectedIndex != 0);
                btnMoveDown.Enabled = (listTools.SelectedIndex != ToolList.Count - 1);
            }            
        }

        private static readonly string[] EXTENSIONS = new[]{".exe", ".com", ".pif", ".cmd", ".bat"};

        public static bool checkExtension(string path)
        {
            return EXTENSIONS.Any(extension => extension == System.IO.Path.GetExtension(path));
        }

        public bool CheckPassTool(int toolIndex)
        {
            ToolDescription tool;
            if (toolIndex < ToolList.Count && toolIndex >= 0)
            {
                tool = ToolList[toolIndex];
            }
            else
            {
                return true;
            }
            if (tool.Title == "") 
            {
                MessageDlg.Show(this, "You must enter a valid title for the tool");
                listTools.SelectedIndex = toolIndex;
                return false;
            }
            if (tool.Command == "")
            {
                MessageDlg.Show(this, string.Format("The command cannot be blank, please enter a valid command for {0}", tool.Title));
                listTools.SelectedIndex = toolIndex;
                return false;
            }
            string supportedTypes = String.Join("; ", EXTENSIONS);
            supportedTypes = supportedTypes.Replace(".", "*.");
            if (!checkExtension(tool.Command))
            {
                MessageDlg.Show(this, string.Format("The command for {0} must be of a supported type \n\n Supported Types: {1}", tool.Title, supportedTypes)); 
                listTools.SelectedIndex = toolIndex;
                return false;                
            }            
            if (!System.IO.File.Exists(tool.Command))
            {
                var dlg =
                new MultiButtonMsgDlg(
                    string.Format("Warning: \n The command for {0} may not exist in that location. Would you like to edit it?",
                                    tool.Title), "Yes", "No", false);
                DialogResult result = dlg.ShowDialog(this);
                if (result == DialogResult.Yes)
                {
                    listTools.SelectedIndex = toolIndex;
                    return false;
                }
            }
            return true;            
        }

        private void textTitle_TextChanged(object sender, EventArgs e)
        {
            int spot = listTools.SelectedIndex;
            if (spot != -1)
            {
                ToolList[spot].Title = textTitle.Text;
                RefreshListBox();
                listTools.SelectedIndex = spot;
                Unsaved = true;
            }       
        }

        private void textCommand_TextChanged(object sender, EventArgs e)
        {
            int spot = listTools.SelectedIndex;
            if (spot != -1)
            {
                ToolList[spot].Command = textCommand.Text;
                Unsaved = true;
            }
        }

        private void textArguments_TextChanged(object sender, EventArgs e)
        {
            int spot = listTools.SelectedIndex;
            if (spot != -1)
            {
                ToolList[spot].Arguments = textArguments.Text;
                Unsaved = true;
            }
        }

        private void textInitialDirectory_TextChanged(object sender, EventArgs e)
        {
            int spot = listTools.SelectedIndex;
            if (spot != -1)
            {
                ToolList[spot].InitialDirectory = textInitialDirectory.Text;
                Unsaved = true;
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
           Delete();
        }

        public void Delete()
        {
            // Delete current value.
            int spot = listTools.SelectedIndex;
            ToolList.RemoveAt(spot);
            RefreshListBox();
            if (ToolList.Count == 0)
            {
                textTitle.Text = "";
                textCommand.Text = "";
                textArguments.Text = "";
                textInitialDirectory.Text = "";
                btnDelete.Enabled = false;
                _previouslySelectedIndex = -1;
            }
            else if (spot == ToolList.Count)
            {
                // If the deleted Index was the last in the list, the selected index is the new last element.
                listTools.SelectedIndex = spot - 1;
            }
            else
            {
                listTools.SelectedIndex = spot;
                //In this case the selected index doesn't actually change so the textBoxes still need to be updated.
                ToolDescription highlighted = ToolList[listTools.SelectedIndex];
                textTitle.Text = highlighted.Title;
                textCommand.Text = highlighted.Command;
                textArguments.Text = highlighted.Arguments;
                textInitialDirectory.Text = highlighted.InitialDirectory;
            }
            Unsaved = true;
        }

        private void btnMoveUp_Click(object sender, EventArgs e)
        {
            MoveUp();
        }

        public void MoveUp()
        {
            // If there is a value above selected index, swap places.
            int spot = listTools.SelectedIndex;
            if (spot > 0)
            {
                // Swap
                ToolDescription temp = ToolList[spot];
                ToolList[spot] = ToolList[spot - 1];
                ToolList[spot - 1] = temp;
                RefreshListBox();
                _previouslySelectedIndex = _previouslySelectedIndex - 1;
                listTools.SelectedIndex = spot - 1;
                Unsaved = true;
            }
        }

        private void btnMoveDown_Click(object sender, EventArgs e)
        {
            MoveDown();
        }

        public void MoveDown()
        {
            // If there is a value below selected index, swap places. 
            int spot = listTools.SelectedIndex;
            int max = ToolList.Count - 1;
            if (spot < max)
            {
                ToolDescription temp = ToolList[spot];
                ToolList[spot] = ToolList[spot + 1];
                ToolList[spot + 1] = temp;
                RefreshListBox();
                _previouslySelectedIndex = _previouslySelectedIndex + 1;
                listTools.SelectedIndex = spot + 1;
                Unsaved = true;
            }
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            SaveTools();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {        
            OkDialog();
        }

        public void OkDialog()
        {
            if (SaveTools())            
                DialogResult = DialogResult.OK;            
        }

        private void btnCancel_Click(object sender, EventArgs eventArgs)
        {
            Cancel();
        }       
        
        public void Cancel()
        {
            if (!Unsaved)
            {
                DialogResult = DialogResult.Cancel;
            }
            else
            {
                var dlg = new MultiButtonMsgDlg("Do you wish to Save changes?", "Yes", "No", true);
                DialogResult result = dlg.ShowDialog(this);
                switch (result)
                {
                    case DialogResult.Yes:
                        if (SaveTools())
                            DialogResult = DialogResult.OK;
                        break;
                    case DialogResult.No:
                        DialogResult = DialogResult.Cancel;
                        break;
                }
            }
        }

        public bool SaveTools()
        {
            if (CheckPassTool(_previouslySelectedIndex))
            {
                SkylineWindow.SenderArgs args = new SkylineWindow.SenderArgs(SkylineWindow.Copier(ToolList));
                SkylineWindow.SaveEvent(this, args);
                Unsaved = false;
                return true;
            }
            else
            {
                return false;
            }
        }

        private void ConfigureToolsDlg_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                btnCancel.PerformClick();
        }

        private void btnDelete_EnabledChanged(object sender, EventArgs e)
        {
            if (!btnDelete.Enabled)
            {
                textTitle.Enabled = false;
                textCommand.Enabled = false;
                textArguments.Enabled = false;
                textInitialDirectory.Enabled = false;
                btnFindCommand.Enabled = false;
                btnInitialDirectory.Enabled = false;
            }
            else
            {
                textTitle.Enabled = true;
                textCommand.Enabled = true;
                textArguments.Enabled = true;
                textInitialDirectory.Enabled = true;
                btnFindCommand.Enabled = true;
                btnInitialDirectory.Enabled = true;
            }
        }

        #region Functional testing support

        public void TestHelperIndexChange(int i)
        {
            listTools.SelectedIndex = i;
        }

        #endregion

        // Cannot test methods below because of common dialogs

        private void btnFindCommand_Click(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter =
                    "All Executables (*.exe, *.com, *.pif, *.bat, *.cmd)|*.exe;*.com;*.pif;*.bat;*.cmd|Command Files (*.com)|*.com|Information Files (*.pif)|*.pif|Batch Files (*.bat,*.cmd)|*.bat;*.cmd|All Files (*.*)|*.*",
                FilterIndex = 1,
                Multiselect = false
            };
            DialogResult result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                textCommand.Text = dlg.FileName;
            }
        }

        private void btnInitialDirectory_Click(object sender, EventArgs e)
        {
            var dlg = new FolderBrowserDialog();
            DialogResult result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                textInitialDirectory.Text = dlg.SelectedPath;
            }
        }
    }
}
