/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public enum IrtCalculatorSource { settings, file }

    public partial class AddIrtCalculatorDlg : FormEx
    {
        public AddIrtCalculatorDlg(IEnumerable<RCalcIrt> calculators)
        {
            InitializeComponent();

            comboLibrary.Items.AddRange(calculators.Cast<object>().ToArray());
            ComboHelper.AutoSizeDropDown(comboLibrary);
        }

        public IrtCalculatorSource Source
        {
            get { return radioSettings.Checked ? IrtCalculatorSource.settings : IrtCalculatorSource.file; }

            set
            {
                if (value == IrtCalculatorSource.settings)
                    radioSettings.Checked = true;
                else
                    radioFile.Checked = true;
            }
        }

        public RCalcIrt Calculator
        {
            get
            {
                if (Source == IrtCalculatorSource.settings)
                    return (RCalcIrt)comboLibrary.SelectedItem;
                return new RCalcIrt("Add", textFilePath.Text);  // Not L10N
            }
        }

        public void OkDialog()
        {
            if (Source == IrtCalculatorSource.file)
            {
                string path = textFilePath.Text;
                string message = null;
                if (string.IsNullOrEmpty(path))
                    message = Resources.AddIrtCalculatorDlg_OkDialog_Please_specify_a_path_to_an_existing_iRT_database;
                else if (!path.EndsWith(IrtDb.EXT))
                    message = string.Format(Resources.AddIrtCalculatorDlg_OkDialog_The_file__0__is_not_an_iRT_database, path);
                else if (!File.Exists(path))
                {
                    message = TextUtil.LineSeparate(string.Format(Resources.AddIrtCalculatorDlgOkDialogThe_file__0__does_not_exist, path),
                                                    Resources.AddIrtCalculatorDlg_OkDialog_Please_specify_a_path_to_an_existing_iRT_database);
                }
                if (message != null)
                {
                    MessageDlg.Show(this, message);
                    textFilePath.Focus();
                    return;                    
                }
            }
            var calculator = Calculator;
            if (calculator == null)
            {
                MessageDlg.Show(this, Resources.AddIrtCalculatorDlg_OkDialog_Please_choose_the_iRT_calculator_you_would_like_to_add);
                return;
            }

            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void radioSettings_CheckedChanged(object sender, EventArgs e)
        {
            SourceChanged();
        }

        private void radioFile_CheckedChanged(object sender, EventArgs e)
        {
            SourceChanged();
        }

        private void SourceChanged()
        {
            if (Source == IrtCalculatorSource.settings)
            {
                comboLibrary.Enabled = true;
                textFilePath.Enabled = false;
                textFilePath.Text = string.Empty;
                btnBrowseFile.Enabled = false;
            }
            else
            {
                comboLibrary.SelectedIndex = -1;
                comboLibrary.Enabled = false;
                textFilePath.Enabled = true;
                btnBrowseFile.Enabled = true;
            }
        }

        private void btnBrowseFile_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
                                 {
                                     CheckPathExists = true,
                                     DefaultExt = BiblioSpecLibSpec.EXT,
                                     Filter = TextUtil.FileDialogFiltersAll(IrtDb.FILTER_IRTDB)
                                 })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                textFilePath.Text = dlg.FileName;
            }
        }

        #region Functional test support

        public string FilePath
        {
            get { return textFilePath.Text; }
            set
            {
                Source = IrtCalculatorSource.file;
                textFilePath.Text = value;
            }
        }

        public string CalculatorName
        {
            get { return ((RCalcIrt) comboLibrary.SelectedItem).Name; }
            set
            {
                Source = IrtCalculatorSource.settings;
                comboLibrary.SelectedIndex = GetCalculatorNameIndex(comboLibrary, value);
            }
        }

        private static int GetCalculatorNameIndex(ComboBox comboBox, string name)
        {
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (Equals(name, ((RCalcIrt)comboBox.Items[i]).Name))
                    return i;
            }
            return -1;
        }

        #endregion
    }
}