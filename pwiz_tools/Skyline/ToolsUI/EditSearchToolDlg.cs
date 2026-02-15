/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

using pwiz.Common.Collections;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace pwiz.Skyline.ToolsUI
{
    public partial class EditSearchToolDlg : FormEx
    {
        private SearchTool _searchTool;
        private readonly IEnumerable<SearchTool> _existing;

        public EditSearchToolDlg(IEnumerable<SearchTool> existing)
        {
            _existing = existing;
            Icon = Resources.Skyline;
            InitializeComponent();

            comboToolName.Items.AddRange(Enum.GetNames(typeof(SearchToolType)));
        }

        public SearchTool SearchTool
        {
            get { return _searchTool; }
            set
            {
                _searchTool = value;
                if (_searchTool == null)
                {
                    comboToolName.SelectedIndex = 0;
                    tbPath.Text = string.Empty;
                    tbExtraArgs.Text = string.Empty;
                }
                else
                {
                    comboToolName.SelectedItem = _searchTool.Name.ToString();
                    tbPath.Text = _searchTool.Path;
                    tbExtraArgs.Text = _searchTool.ExtraCommandlineArgs;
                }
            }
        }

        public SearchToolType ToolName
        {
            get => (SearchToolType) Enum.Parse(typeof(SearchToolType), comboToolName.SelectedItem.ToString());
            set => comboToolName.SelectedItem = value.ToString();
        }

        public string ToolPath
        {
            get => tbPath.Text;
            set { tbPath.Text = value; }
        }

        public string ExtraCommandlineArgs
        {
            get => tbExtraArgs.Text;
            set { tbExtraArgs.Text = value; }
        }


        public void OkDialog()
        {
            MessageBoxHelper helper = new MessageBoxHelper(this);

            if (_existing.Contains(tool => !ReferenceEquals(_searchTool, tool) && Equals(ToolName, tool.Name)))
            {
                helper.ShowTextBoxError(comboToolName, ToolsUIResources.EditSearchToolDlg_OkDialog_The_tool__0__is_already_configured_, ToolName.ToString());
                return;
            }

            if (ToolPath.IsNullOrEmpty())
            {
                helper.ShowTextBoxError(tbPath, Resources.AddPeakCompareDlg_OkDialog_File_path_cannot_be_empty_);
                return;
            }
            
            if (!File.Exists(ToolPath))
            {
                helper.ShowTextBoxError(tbPath, ToolsUIResources.EditSearchToolDlg_OkDialog_The_file__0__does_not_exist_, ToolPath);
                return;
            }

            _searchTool = new SearchTool(ToolName, ToolPath, ExtraCommandlineArgs, Path.GetDirectoryName(ToolPath), false);
            DialogResult = DialogResult.OK;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = ConfigureToolsDlg.EXECUTABLE_FILES_FILTER;
                dlg.FilterIndex = 1;
                dlg.Multiselect = false;
                dlg.SupportMultiDottedExtensions = true;
                if (!ToolPath.IsNullOrEmpty() && Directory.Exists(Path.GetDirectoryName(ToolPath)))
                    dlg.InitialDirectory = Path.GetDirectoryName(ToolPath);
                if (dlg.ShowDialog(Parent) == DialogResult.OK)
                {
                    tbPath.Text = dlg.FileName;
                }
            }
        }

        private void tbPath_TextChanged(object sender, EventArgs e)
        {
            if (File.Exists(tbPath.Text))
                tbPath.ForeColor = Color.Black;
            else
                tbPath.ForeColor = Color.Red;
        }
    }
}
