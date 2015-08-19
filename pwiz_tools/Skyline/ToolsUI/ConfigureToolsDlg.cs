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
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    public partial class ConfigureToolsDlg : FormEx
    {
        private readonly ComboBoxDriverWrapper _driverReportSpec;

        public ConfigureToolsDlg(SkylineWindow parent)
        {
            InitializeComponent();
            SkylineWindowParent = parent;
            PopulateMacroList();
            // Initialize the report comboBox.
            // CONSIDER: Settings list editing is currently disabled, because
            //           it had problems with the empty element added to the list.
            //           Might be nice to allow report spec editing in this form
            //           some day, though.
            _driverReportSpec = new ComboBoxDriverWrapper(components, comboReport);

            if (ToolStoreUtil.ToolStoreClient != null)
                fromWebAddContextMenuItem.Visible = fromWebAddContextMenuItem.Enabled = true;

            Removelist = new List<ToolDescription>();
            Init(false);
        }

        private void Init(bool selectEnd)
        {
            // Initialize the report comboBox.
            _driverReportSpec.LoadList(string.Empty);
            comboReport.Items.Insert(0, string.Empty);
            comboReport.SelectedItem = string.Empty;

            // Value for keeping track of the previously selected tool 
            // Used to check if the tool meets requirements before allowing you to navigate away from it.
            PreviouslySelectedIndex = -1;

            // Reload the tool list
            Removelist.Clear();
            ToolList = Settings.Default.ToolList
                               .Select(t => new ToolDescription(t))
                               .ToList();
            RefreshListBox();
            if (ToolList.Count == 0)
            {
                listTools.SelectedIndex = -1;
                btnRemove.Enabled = false;
                btnMoveUp.Enabled = false;
                btnMoveDown.Enabled = false;
            }
            else
            {
                listTools.SelectedIndex = selectEnd ? ToolList.Count - 1 : 0;
                btnRemove.Enabled = true;
            }
            Unsaved = false;
        }

        private IList<ToolDescription> Removelist { get; set; } 

        private SkylineWindow SkylineWindowParent { get; set; }

        public int PreviouslySelectedIndex { get; private set; }

        public void RefreshListBox()
        {
            listTools.Items.Clear();
            foreach (var toolDescription in ToolList)
                listTools.Items.Add(toolDescription.Title);
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
            contextMenuAdd.Show(btnAdd.Parent, btnAdd.Left, btnAdd.Bottom + 1);
        }

        public void Add()
        {
            if (CheckPassTool(PreviouslySelectedIndex))
            {
                AddDialog(GetTitle(), string.Empty, string.Empty, string.Empty);
            }
            if (ToolList.Count == 1)
            {
                PreviouslySelectedIndex = 0;
            }
        }

        /// <summary>
        /// Return a unique title for a New Tool. (eg. [New Tool1])
        /// </summary>
        private string GetTitle()
        {
            return ToolInstaller.GetUniqueFormat(Resources.ConfigureToolsDlg_GetTitle__New_Tool_0__, value => !ToolList.Any(item => Equals(item.Title, value)));            
        }

        private string GetTitle(string title)
        {
            return ToolList.Any(item => Equals(item.Title, title))
                       ? ToolInstaller.GetUniqueName(title, value => !ToolList.Any(item => Equals(item.Title, value)))
                       : title;
        }

        private bool IsUniqueTitle(string title)
        {
            int count = ToolList.Count(tool => tool.Title == title);
            return count <= 1;
        }


        /// <summary>
        /// Default to values cbOutputImmediateWindow = false, selectedReport=string.Empty
        /// </summary>
        public void AddDialog(string title, string command, string arguments, string initialDirectory )
        {
            AddDialog(title, command, arguments, initialDirectory, false, string.Empty);
        }

        public void AddDialog(string title, string command, string arguments, string initialDirectory,
                              bool isImmediateOutput, string selectedReport)
        {
            AddDialog(title, command, arguments, initialDirectory, isImmediateOutput, selectedReport,
                      string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        }

        public void AddDialog(string title,
                              string command, 
                              string arguments,
                              string initialDirectory,
                              bool isImmediateOutput,
                              string selectedReport,
                              string argsCollectorDllPath,
                              string argsCollectorType,
                              string toolDirPath,
                              string packageVersion,
                              string packageIdentifier,
                              string packageName)        
        {
            ToolDescription newTool;
            if (ToolDescription.IsWebPageCommand(command))
            {
                newTool = new ToolDescription(GetTitle(title), command, arguments, string.Empty, false,
                    selectedReport, argsCollectorDllPath, argsCollectorType, toolDirPath, null, packageVersion, packageIdentifier, packageName);
            }
            else
            {
                newTool = new ToolDescription(GetTitle(title), command, arguments, initialDirectory, isImmediateOutput,
                    selectedReport, argsCollectorDllPath, argsCollectorType, toolDirPath, null, packageVersion, packageIdentifier, packageName);
            }
            ToolList.Add(newTool);
            RefreshListBox();
            PreviouslySelectedIndex = -1;
            listTools.SelectedIndex = ToolList.Count - 1;
            btnRemove.Enabled = true;            
        }

        public void listTools_SelectedIndexChanged(object sender, EventArgs e)
        {                        
            if (listTools.SelectedIndex != -1)
            {
                if (listTools.SelectedIndex != PreviouslySelectedIndex)
                {                    
                     if (PreviouslySelectedIndex != -1 && !CheckPassTool(PreviouslySelectedIndex))
                    {
                        listTools.SelectedIndex = PreviouslySelectedIndex;
                        return;
                    }
                    PreviouslySelectedIndex = listTools.SelectedIndex;
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
        public static readonly string[] EXTENSIONS = {".exe", ".com", ".pif", ".cmd", ".bat", ".py", ".pl"}; // Not L10N

        public static bool CheckExtension(string path)
        {
            // Avoid Path.GetExtension() because it throws an exception for an invalid path
            return EXTENSIONS.Any(extension => PathEx.HasExtension(path, extension));
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
            //Ensure the tool Title is unique.
            if (!IsUniqueTitle(tool.Title))
            {
                MessageDlg.Show(this, Resources.ConfigureToolsDlg_CheckPassToolInternal_Tool_titles_must_be_unique__please_enter_a_unique_title_for_this_tool_);
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
// ReSharper disable ObjectCreationAsStatement
                    new Uri(tool.Command);
// ReSharper restore ObjectCreationAsStatement
                }
                catch (Exception)
                {
                    MessageDlg.Show(this, Resources.ConfigureToolsDlg_CheckPassToolInternal_Please_specify_a_valid_URL_);
                    textCommand.Focus();
                    return false;
                }

                return true;
            }
            //If it is not a $(ProgramPath()) macro then do other checks.
            if (ToolMacros.GetProgramPathContainer(tool.Command) == null)
            {
                string supportedTypes = String.Join("; ", EXTENSIONS); // Not L10N
                supportedTypes = supportedTypes.Replace(".", "*."); // Not L10N
                if (!CheckExtension(tool.Command))
                {
                    MessageDlg.Show(this, string.Format(TextUtil.LineSeparate(
                                Resources.ConfigureToolsDlg_CheckPassTool_The_command_for__0__must_be_of_a_supported_type,
                                Resources.ConfigureToolsDlg_CheckPassTool_Supported_Types___1_,
                                Resources.ConfigureToolsDlg_CheckPassTool_if_you_would_like_the_command_to_launch_a_link__make_sure_to_include_http____or_https___),
                                tool.Title, supportedTypes));
                    textCommand.Focus();
                    return false;
                }
                string adjustedCommand = tool.Command;
                if (adjustedCommand.Contains(ToolMacros.TOOL_DIR))
                {
                    if (String.IsNullOrEmpty(tool.ToolDirPath))
                    {
                        MessageDlg.Show(this,
                                        Resources.ConfigureToolsDlg_CheckPassToolInternal__ToolDir__is_not_a_valid_macro_for_a_tool_that_was_not_installed_and_therefore_does_not_have_a_Tool_Directory_);
                        textCommand.Focus();
                        return false;
                    }
                    else
                    {
                        adjustedCommand = adjustedCommand.Replace(ToolMacros.TOOL_DIR, tool.ToolDirPath);
                    }
                }
                if (!File.Exists(adjustedCommand))
                {
                    if (DialogResult.Yes == MultiButtonMsgDlg.Show(
                        this,
                        string.Format(TextUtil.LineSeparate(
                            Resources.ConfigureToolsDlg_CheckPassTool__The_command_for__0__may_not_exist_in_that_location__Would_you_like_to_edit_it__,
                            Resources.ConfigureToolsDlg_CheckPassTool__Note__if_you_would_like_the_command_to_launch_a_link__make_sure_to_include_http____or_https___),
                            tool.Title), 
                        MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false))
                    {
                        textCommand.Focus();
                        return false;
                    }
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
                if (textTitle.Text == ToolList[spot].Title)
                {
                    return;
                }
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
                if (ToolList[spot].Command != textCommand.Text)
                {
                    Unsaved = true;
                    ToolList[spot].Command = textCommand.Text;
                }

                if (ToolDescription.IsWebPageCommand(textCommand.Text))
                {
                    labelCommand.Text = Resources.ConfigureToolsDlg_textCommand_TextChanged_U_RL_;
                    labelArguments.Text = Resources.ConfigureToolsDlg_textCommand_TextChanged__Query_params_;
                    if (textInitialDirectory.Enabled)
                    {
                        textInitialDirectory.Enabled = false;
                        textInitialDirectory.Text = string.Empty;
                        cbOutputImmediateWindow.Enabled = false;
                        btnFindCommand.Enabled = false;
                        btnInitialDirectory.Enabled = false;
                        btnInitialDirectoryMacros.Enabled = false;
                    }
                }
                else
                {
                    labelCommand.Text = Resources.ConfigureToolsDlg_textCommand_TextChanged__Command_;
                    labelArguments.Text = Resources.ConfigureToolsDlg_textCommand_TextChanged_A_rguments_;
                    if (!textInitialDirectory.Enabled)
                    {
                        textInitialDirectory.Enabled = true;
                        textInitialDirectory.Text = ToolList[spot].InitialDirectory;
                        cbOutputImmediateWindow.Enabled = true;
                        btnFindCommand.Enabled = true;
                        btnInitialDirectory.Enabled = true;
                        btnInitialDirectoryMacros.Enabled = true;                    
                    }
                }
            }
        }

        private void textArguments_TextChanged(object sender, EventArgs e)
        {
            int spot = listTools.SelectedIndex;
            if (spot != -1)
            {
                if (ToolList[spot].Arguments != textArguments.Text)
                {
                    Unsaved = true;
                    ToolList[spot].Arguments = textArguments.Text;
                }

            }
        }

        private void textInitialDirectory_TextChanged(object sender, EventArgs e)
        {
            int spot = listTools.SelectedIndex;
            if (spot != -1)
            {
                if (ToolList[spot].InitialDirectory != textInitialDirectory.Text)
                {
                    Unsaved = true;
                    ToolList[spot].InitialDirectory = textInitialDirectory.Text;
                }
                
            }
        }

        private void cbOutputImmediateWindow_CheckedChanged(object sender, EventArgs e)
        {
            int spot = listTools.SelectedIndex;
            if (spot != -1)
            {
                if (ToolList[spot].OutputToImmediateWindow != (cbOutputImmediateWindow.CheckState == CheckState.Checked))
                {
                    Unsaved = true;
                    ToolList[spot].OutputToImmediateWindow = (cbOutputImmediateWindow.CheckState == CheckState.Checked);                    
                }
            }
        }

        private void comboReport_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_driverReportSpec.SelectedIndexChangedEvent(sender, e))
                return;

            int spot = listTools.SelectedIndex;
            if (spot != -1)
            {
                if (ToolList[spot].ReportTitle != comboReport.Text && !(ToolList[spot].ReportTitle == null && comboReport.Text == string.Empty))
                {
                    Unsaved = true;
                    ToolList[spot].ReportTitle = comboReport.Text;
                }
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
            if (spot >= Settings.Default.ToolList.Count && !Unsaved)
            {
                //Removing a newly added tool
            }
            else
            {
                Unsaved = true;
            }
            Removelist.Add(ToolList[spot]);
            ToolList.RemoveAt(spot);
            RefreshListBox();
            if (ToolList.Count == 0)
            {
                textTitle.Text = string.Empty;
                textCommand.Text = string.Empty;
                textArguments.Text = string.Empty;
                textInitialDirectory.Text = string.Empty;
                btnRemove.Enabled = false;
                PreviouslySelectedIndex = -1;
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
                PreviouslySelectedIndex = PreviouslySelectedIndex - 1;
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
                PreviouslySelectedIndex = PreviouslySelectedIndex + 1;
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
                DialogResult result = MultiButtonMsgDlg.Show(
                    this,
                    Resources.ConfigureToolsDlg_Cancel_Do_you_wish_to_Save_changes_,
                    MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, true);
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
            if (CheckPassTool(PreviouslySelectedIndex))
            {
                // Figure out which tools have been removed and if a directory in the tools folder can be removed.
                // Check if we can delete any toolDirs based on the tools on the remove list. 
                if (Removelist.Count > 0)
                {
                    var referencedPaths = ToolList.Where(t => !string.IsNullOrEmpty(t.ToolDirPath))
                                                  .Select(t => t.ToolDirPath).ToArray();
                    foreach (var removeTool in Removelist)
                    {
                        if (!string.IsNullOrEmpty(removeTool.ToolDirPath) &&
                            Directory.Exists(removeTool.ToolDirPath) &&
                            !referencedPaths.Contains(removeTool.ToolDirPath))
                        {
                            DirectoryEx.SafeDelete(removeTool.ToolDirPath);                                                                            
                        }
                    }    
                }

                Settings.Default.ToolList = Properties.ToolList.CopyTools(ToolList); 
                Unsaved = false;
                Removelist = new List<ToolDescription>();
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

        // Cannot test methods below because of common dialogs

        private void btnFindCommand_Click(object sender, EventArgs e)
        {
            ProgramPathContainer pcc = ToolMacros.GetProgramPathContainer(textCommand.Text);
            if (pcc != null)
            {
                contextMenuCommand.Show(btnFindCommand.Parent, btnFindCommand.Left, btnFindCommand.Bottom + 1);
            }
            else
            {
                CommandBtnClick();
            }
            
        }

        private void browseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CommandBtnClick();
        }

        private void editMacroToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EditMacro();
        }

        public void EditMacro()
        {
            ProgramPathContainer pcc = ToolMacros.GetProgramPathContainer(textCommand.Text);
            using (var dlg = new LocateFileDlg(pcc))
            {
                dlg.ShowDialog();
            }
        }

        public void CommandBtnClick()
        {
            int i = 0;
            using (var dlg = new OpenFileDialog
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
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    textCommand.Text = dlg.FileName;
                }
            }
        }

        private void btnInitialDirectory_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    textInitialDirectory.Text = dlg.SelectedPath;
                }
            }
        }

        #region Macros

        public readonly List<MacroMenuItem> MacroListArguments = new List<MacroMenuItem>();
        public readonly List<MacroMenuItem> MacroListInitialDirectory = new List<MacroMenuItem>();

        private void PopulateMacroList()
        {
            // Populate _macroListArguments.
            foreach (Macro macro in ToolMacros.LIST_ARGUMENTS)
            {
                MacroListArguments.Add(new MacroMenuItem(macro, textArguments, true));
            }
            // Populate _macroListInitialDirectory.
            foreach (Macro macro in ToolMacros.LIST_INITIAL_DIRECTORY)
            {
                MacroListInitialDirectory.Add(new MacroMenuItem(macro, textInitialDirectory, false));
            }            
        }
        
        public class MacroMenuItem : ToolStripMenuItem  
        { 
            private readonly Macro _macro;
            private readonly TextBox _targetTextBox;
            private readonly bool _multiMacro;

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
            public bool IsWebApplicable { get { return _macro.IsWebApplicable; }}

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
            public string GetContents(ToolMacroInfo tmf)
            {
                return _macro.GetContents(tmf);
            }
        }

        // Used in automated testing.
        public void ClickMacro(List<MacroMenuItem> list, int index)
        {
            list[index].DoClick();
        }

        private void btnArguments_Click(object sender, EventArgs e)
        {
            ShowArgumentsOpen();
        }

        /// <summary>
        /// Show the ContextMenu full of macros next to btnArguments.
        /// </summary>
        public void ShowArgumentsOpen()
        {
            bool isWebPage = ToolDescription.IsWebPageCommand(textCommand.Text);
            PopulateMacroDropdown(MacroListArguments, contextMenuMacroArguments, isWebPage);
            if (!isWebPage)
            {
                contextMenuMacroArguments.Items.Insert(4, new ToolStripSeparator());
                contextMenuMacroArguments.Items.Insert(9, new ToolStripSeparator());
            }
            contextMenuMacroArguments.Show(btnArguments, new Point(btnArguments.Width, 0));
        }

        //For Functional Testing.
        public void PopulateListMacroArguments()
        {
            bool isWebPage = ToolDescription.IsWebPageCommand(textCommand.Text);
            PopulateMacroDropdown(MacroListArguments, contextMenuMacroArguments, isWebPage);
        }

        public string GetMacroArgumentToolTip(string s)
        {
            foreach (MacroMenuItem menuItem in MacroListArguments)
            {
                if (menuItem != null && menuItem.Text == s)
                    return menuItem.ToolTipText;
            }
            return null;
        }

        private void btnInitialDirectoryMacros_Click(object sender, EventArgs e)
        {
            btnInitialDirectoryOpen();
        }

        /// <summary>
        /// Show the ContextMenu full of macros next to btnInitialDirectory.
        /// </summary>
        public void btnInitialDirectoryOpen()
        {            
            PopulateMacroDropdown(MacroListInitialDirectory, contextMenuMacroInitialDirectory, false);
            contextMenuMacroInitialDirectory.Show(btnInitialDirectoryMacros, new Point(btnInitialDirectoryMacros.Width, 0));
        }

        /// <summary>
        /// Loop through the macroList adding each to the menu and setting its ToolTip to the appropriate value.
        /// </summary>
        /// <param name="macroList">List of macros to add from (eg. _macroListInitialDirectory)</param>
        /// <param name="menu">Menu to add the macros to.</param>
        /// <param name="isWebPage"></param>
        private void PopulateMacroDropdown(IEnumerable<MacroMenuItem> macroList, ToolStrip menu, bool isWebPage)
        {
            while (menu.Items.Count > 0)
                menu.Items.RemoveAt(0);
            
            foreach (MacroMenuItem menuItem in macroList)
            {
                if (string.IsNullOrEmpty(ToolDir) && menuItem.ShortText == ToolMacros.TOOL_DIR)
                    continue;
                if (string.IsNullOrEmpty(ArgsCollectorPath) && menuItem.ShortText == ToolMacros.COLLECTED_ARGS)
                    continue;
                if (isWebPage && !menuItem.IsWebApplicable)
                    continue;

                menu.Items.Add(menuItem);

                if (SkylineWindowParent != null)
                {
                    int spot = listTools.SelectedIndex;
                    ToolDescription td = ToolList[spot];
                    ToolMacroInfo tmi = new ToolMacroInfo(SkylineWindowParent, td);
                    string content;
                    if (menuItem.Text == Resources.ToolMacros__listArguments_Input_Report_Temp_Path)
                    {
                        content = Resources.ConfigureToolsDlg_PopulateMacroDropdown_File_path_to_a_temporary_report;
                    }
                    else if (menuItem.Text == Resources.ToolMacros__listArguments_Collected_Arguments)
                    {
                        content = Resources.ConfigureToolsDlg_PopulateMacroDropdown_Arguments_collected_at_run_time;
                    }
                    else
                    {
                        content = menuItem.GetContents(tmi);
                    }

                    if (string.IsNullOrEmpty(content))
                        content = Resources.ConfigureToolsDlg_PopulateMacroDropdown_N_A;

                    menuItem.ToolTipText = content;
                }
            }
        }

        #endregion // Macros

        private void customAddContextMenuItem_Click(object sender, EventArgs e)
        {
            Add();
        }

        private void fromWebAddContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFromWeb();
        }

        private void fromFileAddContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFromFile();
        }

        public void AddFromWeb()
        {
            AddFromZip(ToolInstallUI.InstallZipFromWeb);
        }

        public void AddFromFile()
        {
            AddFromZip(ToolInstallUI.InstallZipFromFile);
        }

        public void InstallZipTool(string fullpath)
        {
            AddFromZip((p, i) => ToolInstallUI.InstallZipTool(p, fullpath, i));
        }

        private ToolInstallUI.InstallProgram InstallProgramFile
        {
            get { return TestInstallProgram ?? SkylineWindowParent.InstallProgram; }
        }

        private delegate void AddToolFromFile(Control parent, ToolInstallUI.InstallProgram install);
        
        private void AddFromZip(AddToolFromFile addTool)
        {
            if (CheckPassTool(PreviouslySelectedIndex) && PromptForSave())
            {
                addTool(this, InstallProgramFile);

                // Re-initialize the form from the ToolsList
                Init(true);

                if (ToolList.Count == 1)
                {
                    PreviouslySelectedIndex = 0;
                }
            }
        }

        /// <summary>
        /// Returns true if the tools are saved
        /// </summary>
        private bool PromptForSave()
        {
            if (Unsaved)
            {
                DialogResult toSave = MultiButtonMsgDlg.Show(
                    this,
                    string.Format(Resources.ConfigureToolsDlg_AddFromFile_You_must_save_changes_before_installing_tools__Would_you_like_to_save_changes_),
                    MultiButtonMsgDlg.BUTTON_YES, Resources.ConfigureToolsDlg_AddFromFile_Cancel, false);
                switch (toSave)
                {
                    case (DialogResult.Yes):
                        SaveTools();
                        return true;
                    case (DialogResult.No):
                        return false;
                }
            }
            return true;
        }
        private class ComboBoxDriverWrapper : Component
        {
            private readonly SettingsListComboDriver<ReportOrViewSpec> _liveReportDriver;

            public ComboBoxDriverWrapper(IContainer container, ComboBox comboBox)
            {
                container.Add(this);
                Settings.Default.PersistedViews.Changed += ViewSettingsOnSettingsChange;
                // Initialize the report comboBox.
                // CONSIDER: Settings list editing is currently disabled, because
                //           it had problems with the empty element added to the list.
                //           Also, for live reports, would need to add code to take the
                //           modified list and persist it in ViewSettings.
                //           Might be nice to allow report spec editing in this form
                //           some day, though.
                _liveReportDriver = new SettingsListComboDriver<ReportOrViewSpec>(comboBox, new ReportOrViewSpecList(), false);
                RepopulateLiveReportList();
            }

            private void ViewSettingsOnSettingsChange()
            {
                if (null != _liveReportDriver)
                {
                    RepopulateLiveReportList();
                }
            }

            private void RepopulateLiveReportList()
            {
                _liveReportDriver.List.Clear();
                var documentGridViewContext = DocumentGridViewContext.CreateDocumentGridViewContext(null, DataSchemaLocalizer.INVARIANT);
                _liveReportDriver.List.AddRange(documentGridViewContext.GetViewSpecList(PersistedViews.ExternalToolsGroup.Id).ViewSpecs.Select(view => new ReportOrViewSpec(view)));
            }

            public void LoadList(string selectedItemLast)
            {
                if (null != _liveReportDriver)
                {
                    _liveReportDriver.LoadList(selectedItemLast);
                }
            }

            public bool SelectedIndexChangedEvent(object sender, EventArgs e)
            {
                if (null != _liveReportDriver)
                {
                    return _liveReportDriver.SelectedIndexChangedEvent(sender, e);
                }
                throw new InvalidOperationException();
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing)
                {
                    Settings.Default.PersistedViews.Changed -= ViewSettingsOnSettingsChange;
                }
            }
        }

        #region Functional test support

        public void TestHelperIndexChange(int i)
        {
            listTools.SelectedIndex = i;
        }

        public string GetComboReportText(int i)
        {
            return (string)comboReport.Items[i];
        }

        public string ArgsCollectorPath
        {
            get { return SelectedTool.ArgsCollectorDllPath; }
        }

        public string ArgsCollectorType
        {
            get { return SelectedTool.ArgsCollectorClassName; }
        }
        
        public string ToolDir
        {
            get { return SelectedTool.ToolDirPath; }
        }

        private ToolDescription SelectedTool
        {
            get { return ToolList[listTools.SelectedIndex]; }
        }

        public void RemoveAllTools()
        {
            // Remove one at a time to be sure necessary events fire.
            while (ToolList.Count > 0)
                Remove();
            RefreshListBox();
        }

        public ToolInstallUI.InstallProgram TestInstallProgram { get; set; }

        #endregion

    }
}
