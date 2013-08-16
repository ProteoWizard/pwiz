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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    public partial class ConfigureToolsDlg : FormEx
    {
        private readonly SettingsListComboDriver<ReportSpec> _driverReportSpec;

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
            _driverReportSpec = new SettingsListComboDriver<ReportSpec>(comboReport,
                Settings.Default.ReportSpecList, false);
            _driverReportSpec.LoadList(string.Empty);
            comboReport.Items.Insert(0, string.Empty);
            comboReport.SelectedItem = string.Empty;
            
            // Value for keeping track of the previously selected tool 
            // Used to check if the tool meets requirements before allowing you to navigate away from it.
            PreviouslySelectedIndex = -1;

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
                listTools.SelectedIndex = 0;
            }
            Unsaved = false;
            Removelist = new List<ToolDescription>();
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
                newTool = new ToolDescription(GetTitle(title), command, string.Empty, string.Empty, false,
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
                string supportedTypes = String.Join("; ", EXTENSIONS);
                supportedTypes = supportedTypes.Replace(".", "*.");
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

                Settings.Default.ToolList = CopyTools(ToolList); 
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
            var dlg = new LocateFileDlg(pcc);
            dlg.ShowDialog();
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
            foreach (Macro macro in ToolMacros._listArguments)
            {
                MacroListArguments.Add(new MacroMenuItem(macro, textArguments, true));
            }
            // Populate _macroListInitialDirectory.
            foreach (Macro macro in ToolMacros._listInitialDirectory)
            {
                MacroListInitialDirectory.Add(new MacroMenuItem(macro, textInitialDirectory, false));
            }            
        }
        
        public class MacroMenuItem : ToolStripMenuItem  
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
            public string GetContents(ToolMacroInfo tmf)
            {
                return _macro.GetContents(tmf);
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
            PopulateMacroDropdown(MacroListArguments, contextMenuMacroArguments);
            contextMenuMacroArguments.Items.Insert(4, new ToolStripSeparator());
            contextMenuMacroArguments.Items.Insert(9, new ToolStripSeparator());
            contextMenuMacroArguments.Show(btnArguments, new Point(btnArguments.Width, 0));
        }

        //For Functional Testing.
        public void PopulateListMacroArguments()
        {
            PopulateMacroDropdown(MacroListArguments, contextMenuMacroArguments);
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
            PopulateMacroDropdown(MacroListInitialDirectory, contextMenuMacroInitialDirectory);
            contextMenuMacroInitialDirectory.Show(btnInitialDirectoryMacros, new Point(btnInitialDirectoryMacros.Width, 0));
        }

        /// <summary>
        /// Loop through the macroList adding each to the menu and setting its ToolTip to the appropriate value.
        /// </summary>
        /// <param name="macroList">List of macros to add from (eg. _macroListInitialDirectory)</param>
        /// <param name="menu">Menu to add the macros to.</param>
        private void PopulateMacroDropdown(IEnumerable<MacroMenuItem> macroList, ToolStrip menu)
        {
            while (menu.Items.Count > 0)
                menu.Items.RemoveAt(0);
            
            foreach (MacroMenuItem menuItem in macroList)
            {
                if (string.IsNullOrEmpty(ToolDir) && menuItem.ShortText == ToolMacros.TOOL_DIR)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(ArgsCollectorPath) && menuItem.ShortText == ToolMacros.COLLECTED_ARGS)
                {
                    continue;
                }

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

        private void fromWebAddContextMenuItem_Click(object sender, EventArgs e)
        {
            //Curently hidden from the menu.
        }

        private void customAddContextMenuItem_Click(object sender, EventArgs e)
        {
            Add();
        }

        private void fromFileAddContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFromFile();
        }

        private void AddFromFile()
        {
            if (CheckPassTool(PreviouslySelectedIndex))
            {
                if (Unsaved)
                {
                    //Prompt them to save first!
                    MultiButtonMsgDlg saveDlg = new MultiButtonMsgDlg(string.Format(Resources.ConfigureToolsDlg_AddFromFile_You_must_save_changes_before_installing_tools__Would_you_like_to_save_changes_),
                               MultiButtonMsgDlg.BUTTON_YES, Resources.ConfigureToolsDlg_AddFromFile_Cancel , false);
                    DialogResult toSave = saveDlg.ShowDialog(this);
                    switch (toSave)
                    {
                        case (DialogResult.Yes):
                            SaveTools();
                            break;
                        case (DialogResult.No):
                            return;
                    }
                }

                using (var dlg = new OpenFileDialog
                    {
                        Filter = TextUtil.FileDialogFiltersAll(TextUtil.FileDialogFilter(
                            Resources.ConfigureToolsDlg_AddFromFile_Zip_Files, ".zip")),
                        Multiselect = false
                    })
                {
                    DialogResult result = dlg.ShowDialog();
                    if (result == DialogResult.OK)
                    {
                        UnpackZipTool(dlg.FileName);
                    }
                }
            }
            if (ToolList.Count == 1)
            {
                PreviouslySelectedIndex = 0;
            }
        }

        public class UnpackZipToolHelper : IUnpackZipToolSupport
        {
            public UnpackZipToolHelper(SkylineWindow parentWindow, Func<ProgramPathContainer, ICollection<string>, string, string> testFindProgramPath)
            {
                parent = parentWindow;
                _testFindProgramPath = testFindProgramPath;
            }
            private SkylineWindow parent { get; set; }
            private Func<ProgramPathContainer, ICollection<string>, string, string> _testFindProgramPath { get; set; }

            public bool? shouldOverwrite(string toolCollectionName, string toolCollectionVersion, List<ReportSpec> reportList, string foundVersion, string newCollectionName)
            {
                return OverwriteOrInParallel(toolCollectionName, toolCollectionVersion, reportList, foundVersion, newCollectionName);
            }

            public string installProgram(ProgramPathContainer programPathContainer, ICollection<string> packages, string pathToInstallScript)
            {
                return _testFindProgramPath == null ? parent.InstallProgram(programPathContainer, packages, pathToInstallScript) : _testFindProgramPath(programPathContainer, packages, pathToInstallScript);
            }

            public bool? shouldOverwriteAnnotations(List<AnnotationDef> annotations)
            {
                return OverwriteAnnotations(annotations);
            }

            public string FindProgramPath(ProgramPathContainer programPathContainer)
            {
                return parent.FindProgramPath(programPathContainer);
            }
        }

        /// <summary>
        /// Copy a zip file's contents to the tools folder and loop through its .properties
        /// files adding the tools to the tools menu.
        /// </summary>
        /// <param name="fullpath"> The full path to the ziped folder containing the tools</param>
        public void UnpackZipTool(string fullpath)
        {
            ToolInstaller.UnzipToolReturnAccumulator result = null;
            try
            {
                result = ToolInstaller.UnpackZipTool(fullpath, new UnpackZipToolHelper(SkylineWindowParent, TestFindProgramPath));
            }
            catch (MessageException x)
            {
                MessageDlg.Show(this, x.Message);
            }
            catch (IOException x)
            {
                MessageDlg.Show(this, TextUtil.LineSeparate(string.Format(Resources.ConfigureToolsDlg_UnpackZipTool_Failed_attempting_to_extract_the_tool_from__0_, Path.GetFileName(fullpath)), x.Message));
            }

            if (result != null)
            {
                foreach (var message in result.MessagesThrown)
                {
                    MessageDlg.Show(this, message);
                }
            }
            else
            {
                // If result is Null than we want to discard changes made to the toolsList
                // SaveTools will overwrite Settings tools list with whatever we have in the Dialog toolList
                SaveTools();
            }
            //Reload the report dropdown menu!
            _driverReportSpec.LoadList(string.Empty);
            comboReport.Items.Insert(0, string.Empty);
            comboReport.SelectedItem = string.Empty;

            //Reload the tool list
            RemoveAllTools();
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
                listTools.SelectedIndex = ToolList.Count - 1;
                btnRemove.Enabled = true;
            }
            Unsaved = false;
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

        public enum RelativeVersion
        {
            upgrade,
            reinstall,
            olderversion,
            unknown
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

        public Func<ProgramPathContainer, ICollection<string>, string, string> TestFindProgramPath { get; set; }

        #endregion

        public static bool? OverwriteOrInParallel(string toolCollectionName, string toolCollectionVersion, List<ReportSpec> reportList, string foundVersion, string newCollectionName)
        {
            string message;
            string buttonText;
            if (toolCollectionName != null)
            {
                RelativeVersion relativeVersion = DetermineRelativeVersion(toolCollectionVersion, foundVersion);
                string toolMessage;
                switch (relativeVersion)
                {
                    case RelativeVersion.upgrade:
                        toolMessage = TextUtil.LineSeparate(Resources.ConfigureToolsDlg_OverwriteOrInParallel_The_tool__0__is_currently_installed_, string.Empty, 
                            string.Format(Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_upgrade_to__0__or_install_in_parallel_, foundVersion));                        
                        buttonText = Resources.ConfigureToolsDlg_OverwriteOrInParallel_Upgrade;
                        break;
                    case RelativeVersion.olderversion:
                        toolMessage =
                            TextUtil.LineSeparate(
                                string.Format(Resources.ConfigureToolsDlg_OverwriteOrInParallel_This_is_an_older_installation_v_0__of_the_tool__1_, foundVersion, "{0}"), //Not L10N
                                string.Empty, 
                                string.Format(Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_overwrite_with_the_older_version__0__or_install_in_parallel_,
                                foundVersion));
                        buttonText = Resources.ConfigureToolsDlg_OverwriteOrInParallel_Overwrite;
                        break;
                    case RelativeVersion.reinstall:
                        toolMessage = TextUtil.LineSeparate(Resources.ConfigureToolsDlg_OverwriteOrInParallel_The_tool__0__is_already_installed_, 
                            string.Empty,
                            Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_reinstall_or_install_in_parallel_);                        
                        buttonText = Resources.ConfigureToolsDlg_OverwriteOrInParallel_Reinstall;
                        break;
                    default:
                        toolMessage =
                            TextUtil.LineSeparate(
                                Resources
                                    .ConfigureToolsDlg_OverwriteOrInParallel_The_tool__0__is_in_conflict_with_the_new_installation,
                                string.Empty,
                                Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_overwrite_or_install_in_parallel_);
                        buttonText = Resources.ConfigureToolsDlg_OverwriteOrInParallel_Overwrite; // Or update?
                        break;
                }
                message = string.Format(toolMessage, toolCollectionName);
            }
            else //Warn about overwritng report.
            {
                List<string> reportTitles = reportList.Select(sp => sp.GetKey()).ToList();
            
                string reportMultiMessage = TextUtil.LineSeparate(Resources.ConfigureToolsDlg_OverwriteOrInParallel_This_installation_would_modify_the_following_reports, string.Empty,
                                                              "{0}", string.Empty); //Not L10N
                string reportSingleMessage = TextUtil.LineSeparate(Resources.ConfigureToolsDlg_OverwriteOrInParallel_This_installation_would_modify_the_report_titled__0_, string.Empty);
                
                string reportMessage = reportList.Count == 1 ? reportSingleMessage : reportMultiMessage;
                string question = Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_overwrite_or_install_in_parallel_;
                buttonText = Resources.ConfigureToolsDlg_OverwriteOrInParallel_Overwrite;
                string reportMessageFormat = TextUtil.LineSeparate(reportMessage, question);
                message = string.Format(reportMessageFormat, TextUtil.LineSeparate(reportTitles));
            }

            MultiButtonMsgDlg dlg = new MultiButtonMsgDlg(message, buttonText, Resources.ConfigureToolsDlg_OverwriteOrInParallel_In_Parallel, true);
            DialogResult result = dlg.ShowDialog();
            switch (result)
            {
                case DialogResult.Cancel:
                    return null;
                case DialogResult.Yes:
                    return true;
                case DialogResult.No:
                    return false;
            }
            return false;
        }


        public static RelativeVersion DetermineRelativeVersion(string versionToCompare, string foundVersion)
        {
            if (!string.IsNullOrEmpty(foundVersion) && !string.IsNullOrEmpty(versionToCompare))
            {
                Version current = new Version(versionToCompare);
                Version found = new Version(foundVersion);
                if (current > found) //Installing an olderversion.
                {
                    return RelativeVersion.olderversion;
                }
                else if (current == found) // Installing the same version.
                {
                    return RelativeVersion.reinstall;
                }
                else if (found > current)
                {
                    return RelativeVersion.upgrade;
                }
            }
            return RelativeVersion.unknown;
        }

        public static bool? OverwriteAnnotations(List<AnnotationDef> annotations)
        {
            List<string> annotationTitles = annotations.Select(annotation => annotation.GetKey()).ToList();
            
            string annotationMultiMessage = TextUtil.LineSeparate(Resources.ConfigureToolsDlg_OverwriteAnnotations_Annotations_with_the_following_names_already_exist_, string.Empty,
                                                    "{0}", string.Empty);

            string annotationSingleMessage =
                TextUtil.LineSeparate(Resources.ConfigureToolsDlg_OverwriteAnnotations_An_annotation_with_the_following_name_already_exists_, string.Empty, "{0}", string.Empty);

            string annotationMessage = annotations.Count == 1 ? annotationSingleMessage : annotationMultiMessage;
            string question = Resources.ConfigureToolsDlg_OverwriteAnnotations_Do_you_want_to_overwrite_or_keep_the_existing_annotations_;

            string messageFormat = TextUtil.LineSeparate(annotationMessage, question);

            MultiButtonMsgDlg dlg = new MultiButtonMsgDlg(string.Format(messageFormat, TextUtil.LineSeparate(annotationTitles)), Resources.ConfigureToolsDlg_OverwriteOrInParallel_Overwrite, Resources.ConfigureToolsDlg_OverwriteAnnotations_Keep_Existing, true);
            DialogResult result = dlg.ShowDialog();
            switch (result)
            {
                    case DialogResult.Cancel:
                        return null;
                    case DialogResult.Yes:
                        return true;
                    case DialogResult.No:
                        return false;
            }
            return false;
        }
    }
}
