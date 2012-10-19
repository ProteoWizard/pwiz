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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    public partial class ConfigureToolsDlg : FormEx
    {
        private readonly SettingsListComboDriver<ReportSpec> _driverReportSpec;

        public ConfigureToolsDlg()
        {
            InitializeComponent();

            PopulateMacroList();

            // Initialize the report comboBox.
            // CONSIDER: Settings list editing is currently disabled, because
            //           it had problems with the empty element added to the list.
            //           Might be nice to allow report spec editing in this form
            //           some day, though.
            _driverReportSpec = new SettingsListComboDriver<ReportSpec>(comboReport,
                Settings.Default.ReportSpecList, false);
            _driverReportSpec.LoadList(string.Empty);
            comboReport.Items.Insert(0, string.Empty);
            comboReport.SelectedItem = string.Empty;
            
            // Value for keeping track of the previously selected tool 
            // Used to check if the tool meets requirements before allowing you to navigate away from it.
            _previouslySelectedIndex = -1;

            ToolList = Settings.Default.ToolList
                       .Select(t => new ToolDescription(t))
                       .ToList();

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
        }

        public int _previouslySelectedIndex;

        public void RefreshListBox()
        {
            listTools.DataSource = null;     
            listTools.DataSource = ToolList;
            listTools.DisplayMember = "Title"; // Not L10N     
        }

        public List<ToolDescription> ToolList { get; private set; }

        private static ToolList CopyTools(IEnumerable<ToolDescription> list)
        {
            var listCopy = new ToolList();
            listCopy.AddRange(from t in list
                              where !Equals(t, ToolDescription.EMPTY)
                              select new ToolDescription(t));
            return listCopy;
        }

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
                AddDialog(GetTitle(), string.Empty, string.Empty, string.Empty);
            }
            if (ToolList.Count == 1)
            {
                _previouslySelectedIndex = 0;
            }
        }
        
        /// <summary>
        /// Return a unique title for a New Tool. (eg. [New Tool1])
        /// </summary>
        private string GetTitle()
        {
            int i = 1;
            do
            {
                if (ToolList.All(item => item.Title != (string.Format(Resources.ConfigureToolsDlg_GetTitle__New_Tool_0__, i))))
                {
                    return string.Format(Resources.ConfigureToolsDlg_GetTitle__New_Tool_0__, i);
                }
                i++;
            } while (true);
        }


        /// <summary>
        /// Default to values cbOutputImmediateWindow = false, selectedReport=string.Empty
        /// </summary>
        public void AddDialog(string title, string command, string arguments, string initialDirectory )
        {
            AddDialog(title, command, arguments, initialDirectory, false, string.Empty);
        }

        public void AddDialog(string title, string command, string arguments, string initialDirectory, bool cbOutputImmediate, string selectedReport)
        {
            ToolDescription newTool;
            if (ToolDescription.IsWebPageCommand(command))
            {
                newTool = new ToolDescription(title, command, string.Empty, string.Empty, false, selectedReport);
            }
            else
            {
                newTool = new ToolDescription(title, command, arguments, initialDirectory, cbOutputImmediate, selectedReport);    
            }
            ToolList.Add(newTool);
            RefreshListBox();
            _previouslySelectedIndex = -1;
            listTools.SelectedIndex = ToolList.Count - 1;
            btnRemove.Enabled = true;            
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
                    cbOutputImmediateWindow.CheckState = highlighted.OutputToImmediateWindow
                                                   ? CheckState.Checked
                                                   : CheckState.Unchecked;
                    comboReport.SelectedItem = ComboContainsTitle(highlighted.ReportTitle)
                                                   ? highlighted.ReportTitle
                                                   : string.Empty;
                }
                btnMoveUp.Enabled = (listTools.SelectedIndex != 0);
                btnMoveDown.Enabled = (listTools.SelectedIndex != ToolList.Count - 1);
            }            
        }

        /// <summary>
        /// Returns a bool representing if there is a Report with the specified title.        
        /// </summary>
        private bool ComboContainsTitle(string reportTitle)
        {
            return comboReport.Items.Cast<string>().Any(item => item == reportTitle);
        }

        /// <summary>
        /// Supported extensions
        /// <para>Changes to this array require corresponding changes to the FileDialogFiltersAll call below</para>
        /// </summary>
        public static readonly string[] EXTENSIONS = new[]{".exe", ".com", ".pif", ".cmd", ".bat", ".py", ".pl"};

        public static bool CheckExtension(string path)
        {
            // Avoid Path.GetExtension() because it throws an exception for an invalid path
            path = path.ToLower();
            return EXTENSIONS.Any(extension => path.EndsWith(extension));
        }

        public bool CheckPassTool(int toolIndex)
        {
            bool pass = CheckPassToolInternal(toolIndex);
            if (!pass)
                listTools.SelectedIndex = toolIndex;
            return pass;
        }

        private bool CheckPassToolInternal(int toolIndex)
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
            if (tool.Title == string.Empty) 
            {
                MessageDlg.Show(this, Resources.ConfigureToolsDlg_CheckPassTool_You_must_enter_a_valid_title_for_the_tool);
                textTitle.Focus();
                return false;
            }
            if (tool.Command == string.Empty)
            {
                MessageDlg.Show(this, string.Format(Resources.ConfigureToolsDlg_CheckPassTool_The_command_cannot_be_blank__please_enter_a_valid_command_for__0_, tool.Title));
                textCommand.Focus();
                return false;
            }
            if (tool.IsWebPage)
            {
                try
                {
                    new Uri(tool.Command);
                }
                catch (Exception)
                {
                    MessageDlg.Show(this, Resources.ConfigureToolsDlg_CheckPassToolInternal_Please_specify_a_valid_URL_);
                    textCommand.Focus();
                    return false;
                }

                return true;
            }
            string supportedTypes = String.Join("; ", EXTENSIONS);
            supportedTypes = supportedTypes.Replace(".", "*.");
            if (!CheckExtension(tool.Command))
            {
                MessageDlg.Show(this, string.Format(TextUtil.LineSeparate(
                            Resources.ConfigureToolsDlg_CheckPassTool_The_command_for__0__must_be_of_a_supported_type,
                            Resources.ConfigureToolsDlg_CheckPassTool_Supported_Types___1_ , 
                            Resources.ConfigureToolsDlg_CheckPassTool_if_you_would_like_the_command_to_launch_a_link__make_sure_to_include_http____or_https___),
                            tool.Title, supportedTypes));
                textCommand.Focus();
                return false;                
            }  
           
            if (!File.Exists(tool.Command))
            {
                var dlg =
                new MultiButtonMsgDlg(
                    string.Format(TextUtil.LineSeparate(
                            Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__,                            
                            Resources.ConfigureToolsDlg_CheckPassTool__Note__if_you_would_like_the_command_to_launch_a_link__make_sure_to_include_http____or_https___),
                                    tool.Title), Resources.MultiButtonMsgDlg_BUTTON_YES__Yes, Resources.MultiButtonMsgDlg_BUTTON_NO__No, false);
                DialogResult result = dlg.ShowDialog(this);
                if (result == DialogResult.Yes)
                {
                    textCommand.Focus();
                    return false;
                }
            }
            if (tool.Arguments.Contains(ToolMacros.INPUT_REPORT_TEMP_PATH) && string.IsNullOrEmpty(tool.ReportTitle))
            {
                MessageDlg.Show(this, TextUtil.LineSeparate(
                            string.Format(Resources.ConfigureToolsDlg_CheckPassToolInternal_You_have_provided__0__as_an_argument_but_have_not_selected_a_report_, ToolMacros.INPUT_REPORT_TEMP_PATH),
                            string.Format(Resources.ConfigureToolsDlg_CheckPassToolInternal_Please_select_a_report_or_remove__0__from_arguments_, ToolMacros.INPUT_REPORT_TEMP_PATH)));
                comboReport.Focus();
                return false;                  
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


                if (ToolDescription.IsWebPageCommand(textCommand.Text) && textArguments.Enabled)
                {                    
                    textArguments.Enabled = false;
                    textArguments.Text = string.Empty;
                    textInitialDirectory.Enabled = false;
                    textInitialDirectory.Text = string.Empty;
                    cbOutputImmediateWindow.Enabled = false;
                    btnArguments.Enabled = false;
                    btnFindCommand.Enabled = false;
                    btnInitialDirectory.Enabled = false;
                    btnInitialDirectoryMacros.Enabled = false;
                }
                else if (!ToolDescription.IsWebPageCommand(textCommand.Text) && !textArguments.Enabled)
                {
                    textArguments.Enabled = true;
                    textArguments.Text = ToolList[spot].Arguments;
                    textInitialDirectory.Enabled = true;
                    textInitialDirectory.Text = ToolList[spot].InitialDirectory;
                    cbOutputImmediateWindow.Enabled = true;
                    btnArguments.Enabled = true;
                    btnFindCommand.Enabled = true;
                    btnInitialDirectory.Enabled = true;
                    btnInitialDirectoryMacros.Enabled = true;
                }
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

        private void cbOutputImmediateWindow_CheckedChanged(object sender, EventArgs e)
        {
            int spot = listTools.SelectedIndex;
            if (spot != -1)
            {
                ToolList[spot].OutputToImmediateWindow = (cbOutputImmediateWindow.CheckState == CheckState.Checked);                 
                Unsaved = true;
            }
        }

        private void comboReport_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_driverReportSpec.SelectedIndexChangedEvent(sender, e))
                return;

            int spot = listTools.SelectedIndex;
            if (spot != -1)
            {
                ToolList[spot].ReportTitle = comboReport.Text;
                Unsaved = true;
            }
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
           Remove();
        }    

        /// <summary>
        /// Remove the currently selected value.
        /// </summary>
        public void Remove()
        {
            
            int spot = listTools.SelectedIndex;
            ToolList.RemoveAt(spot);
            RefreshListBox();
            if (ToolList.Count == 0)
            {
                textTitle.Text = string.Empty;
                textCommand.Text = string.Empty;
                textArguments.Text = string.Empty;
                textInitialDirectory.Text = string.Empty;
                btnRemove.Enabled = false;
                _previouslySelectedIndex = -1;
                cbOutputImmediateWindow.CheckState = CheckState.Unchecked;
                comboReport.SelectedItem = string.Empty;
            }
            // If the removed Index was the last in the list, the selected index is the new last element.
            else if (spot == ToolList.Count)
            {
                // If the removed Index was the last in the list, the selected index is the new last element.
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
                cbOutputImmediateWindow.CheckState = highlighted.OutputToImmediateWindow
                                                   ? CheckState.Checked
                                                   : CheckState.Unchecked;
                comboReport.SelectedItem = ComboContainsTitle(highlighted.ReportTitle)
                               ? highlighted.ReportTitle
                               : string.Empty;
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
                var dlg = new MultiButtonMsgDlg(Resources.ConfigureToolsDlg_Cancel_Do_you_wish_to_Save_changes_, Resources.MultiButtonMsgDlg_BUTTON_YES__Yes, Resources.MultiButtonMsgDlg_BUTTON_NO__No, true);
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
                Settings.Default.ToolList = CopyTools(ToolList);
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

        private void btnRemove_EnabledChanged(object sender, EventArgs e)
        {
            textTitle.Enabled =
                textCommand.Enabled =
                textArguments.Enabled =
                textInitialDirectory.Enabled =
                btnFindCommand.Enabled =
                btnInitialDirectory.Enabled =
                btnArguments.Enabled =
                btnInitialDirectoryMacros.Enabled =
                cbOutputImmediateWindow.Enabled =
                comboReport.Enabled = btnRemove.Enabled;
        }

        #region Functional testing support

        public void TestHelperIndexChange(int i)
        {
            listTools.SelectedIndex = i;
        }

        public void RemoveAllTools()
        {
            // Remove one at a time to be sure necessary events fire.
            while (ToolList.Count > 0)
            {
                Remove();
            }
            RefreshListBox();
        }

        public string GetComboReportText(int i)
        {
            return (string) comboReport.Items[i];
        }

        #endregion

        // Cannot test methods below because of common dialogs

        private void btnFindCommand_Click(object sender, EventArgs e)
        {
            int i = 0;
            var dlg = new OpenFileDialog
            {
                Filter = TextUtil.FileDialogFiltersAll(
                               TextUtil.FileDialogFilter(Resources.ConfigureToolsDlg_btnFindCommand_Click_All_Executables, EXTENSIONS[i++]),
                               TextUtil.FileDialogFilter(Resources.ConfigureToolsDlg_btnFindCommand_Click_Command_Files, EXTENSIONS[i++]),
                               TextUtil.FileDialogFilter(Resources.ConfigureToolsDlg_btnFindCommand_Click_Information_Files, EXTENSIONS[i++]),
                               TextUtil.FileDialogFilter(Resources.ConfigureToolsDlg_btnFindCommand_Click_Batch_Files, EXTENSIONS[i++], EXTENSIONS[i++]),
                               TextUtil.FileDialogFilter(Resources.ConfigureToolsDlg_btnFindCommand_Click_Python_Scripts, EXTENSIONS[i++]),
                               TextUtil.FileDialogFilter(Resources.ConfigureToolsDlg_btnFindCommand_Click_Perl_Scripts, EXTENSIONS[i])
                               ),
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

        #region Macros

        public List<MacroMenuItem> _macroListArguments = new List<MacroMenuItem>();
        public List<MacroMenuItem> _macroListInitialDirectory = new List<MacroMenuItem>();

        private void PopulateMacroList()
        {
            // Populate _macroListArguments.            
            foreach (Macro macro in ToolMacros._listArguments)
            {
                _macroListArguments.Add(new MacroMenuItem(macro, textArguments, true));
            }
            // Populate _macroListInitialDirectory.
            foreach (Macro macro in ToolMacros._listInitialDirectory)
            {
                _macroListInitialDirectory.Add(new MacroMenuItem(macro, textInitialDirectory, false));
            }            
        }
        
        public class MacroMenuItem : MenuItem
        { 
            private readonly Macro _macro; 

            /// <summary>
            /// Initiates a new MacroMenuItem
            /// </summary>
            /// <param name="macro"> The macro the item represents </param>
            /// <param name="targetTextBox"> The text box the macro should be written to. </param>
            /// <param name="multiMacro"> A bool that says if multiple macros are accepted.
            /// initialDirectory only makes sense to have one argument
            /// when true: append the macro to the txtBox,
            /// when false: replace the text in the txtbox with the macro. </param>
            public MacroMenuItem(Macro macro, TextBox targetTextBox, bool multiMacro)
            {
                _macro = macro;
                Click += HandleClick;
                Text = macro.PlainText;
                _targetTextBox = targetTextBox;
                _multiMacro = multiMacro;
            }

            public string ShortText { get { return _macro.ShortText; } }
            private readonly TextBox _targetTextBox;
            private readonly bool _multiMacro;

            private void HandleClick(object sender, EventArgs e)
            {
                DoClick();
            }

            public void DoClick()
            {
                if (_multiMacro) 
                    // If multiple macros are allowed, append the macro ShortText.
                    _targetTextBox.Text += ShortText;
                else
                {               
                    // If Multiple Macros are NOT allowed, replace with macro ShortText.
                    _targetTextBox.Text = ShortText;
                }
            }
        }

        // Used in automated testing.
        public void ClickMacro (List<MacroMenuItem> list, int index)
        {
            list[index].DoClick();
        }

        private void btnArguments_Click(object sender, EventArgs e)
        {
            btnArgumentsOpen();
        }

        /// <summary>
        /// Show the ContextMenu full of macros next to btnArguments.
        /// </summary>
        public void btnArgumentsOpen()
        {            
            MacroMenuArguments.Show(btnArguments, new Point(btnArguments.Width, 0));
        }

        /// <summary>
        /// Populate the macroMenu on popup. (Arguments)
        /// </summary>        
        private void MacroMenuArguments_Popup(object sender, EventArgs e)
        {
            foreach (MacroMenuItem menuItem in _macroListArguments)
                MacroMenuArguments.MenuItems.Add(menuItem);
        }

        /// <summary>
        /// Populate the macroMenu on popup. (initialDirectory)
        /// </summary>        
        private void MacroMenuInitialDirectory_Popup(object sender, EventArgs e)
        {                            
            foreach (MacroMenuItem menuItem in _macroListInitialDirectory)
                MacroMenuInitialDirectory.MenuItems.Add(menuItem);                            
        }

        private void btnInitialDirectoryMacros_Click(object sender, EventArgs e)
        {
            btnInitialDirectoryOpen();
        }
       
        public void btnInitialDirectoryOpen()
        {
            // Show the ContextMenu full of macros next to btnInitialDirectory.
            MacroMenuInitialDirectory.Show(btnInitialDirectoryMacros, new Point(btnInitialDirectoryMacros.Width, 0));
        }

        #endregion // Macros
    }
}
