/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.CarafeSharp.Proteome;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// Adds or edits one model in the Carafe model list: its name and its file, with what the file
    /// says it was trained on shown below them as it is chosen, so the user sees what they are adding.
    /// </summary>
    public partial class EditCarafeModelDlg : FormEx
    {
        private CarafeModelSpec _modelSpec;
        private readonly IEnumerable<CarafeModelSpec> _existing;

        public EditCarafeModelDlg(IEnumerable<CarafeModelSpec> existing)
        {
            _existing = existing;

            InitializeComponent();

            Icon = Resources.Skyline;
            textName.Focus();
        }

        public CarafeModelSpec ModelSpec
        {
            get { return _modelSpec; }
            set
            {
                _modelSpec = value;
                textName.Text = _modelSpec?.Name ?? string.Empty;
                textPath.Text = _modelSpec?.FilePath ?? string.Empty;
            }
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            if (!helper.ValidateNameTextBox(textName, out string name))
                return;

            // Allow keeping the original name, but not any other existing one
            if ((ModelSpec == null || !Equals(name, ModelSpec.Name)) && _existing.Any(spec => Equals(name, spec.Name)))
            {
                helper.ShowTextBoxError(textName, SettingsUIResources.EditCarafeModelDlg_OkDialog_The_model__0__already_exists_, name);
                return;
            }

            string path = textPath.Text.Trim();
            if (!ValidateModelPath(this, path))
            {
                textPath.Focus();
                return;
            }

            _modelSpec = new CarafeModelSpec(name, path);
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// Checks that <paramref name="path"/> is a fine-tuned Carafe model file whose every entry
        /// matches its hash, saying why not: a missing or damaged file is reported here, before a
        /// build starts.
        /// </summary>
        public static bool ValidateModelPath(Control owner, string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                MessageDlg.Show(owner, string.Format(SettingsUIResources.EditCarafeModelDlg_ValidateModelPath_The_file__0__does_not_exist_, path));
                return false;
            }
            try
            {
                CarafeModelFile.Open(path);
                return true;
            }
            catch (Exception x)
            {
                if (ExceptionUtil.IsProgrammingDefect(x))
                    throw;
                MessageDlg.ShowWithException(owner,
                    string.Format(SettingsUIResources.EditCarafeModelDlg_ValidateModelPath_The_file__0__is_not_a_valid_fine_tuned_Carafe_model_, path), x);
                return false;
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            string fileName = GetModelPath(this, textPath.Text.Trim());
            if (fileName != null)
                textPath.Text = fileName;
        }

        /// <summary>
        /// Asks for a model file, starting in the folder the last one came from, and remembers
        /// the folder it came from.
        /// </summary>
        public static string GetModelPath(IWin32Window parent, string fileName)
        {
            using (var dlg = new OpenFileDialog())
            {
                string directory = Settings.Default.CarafeModelDirectory;
                dlg.InitialDirectory = !string.IsNullOrEmpty(directory) && Directory.Exists(directory)
                    ? directory
                    : Settings.Default.LibraryDirectory;
                dlg.CheckPathExists = true;
                dlg.SupportMultiDottedExtensions = true;
                dlg.DefaultExt = CarafeModelFile.EXTENSION;
                dlg.Filter = TextUtil.FileDialogFiltersAll(TextUtil.FileDialogFilter(
                    SettingsUIResources.EditCarafeModelDlg_GetModelPath_Fine_tuned_Carafe_models,
                    CarafeModelFile.EXTENSION, CarafeModelFile.LEGACY_EXTENSION));
                if (!string.IsNullOrEmpty(fileName))
                    dlg.FileName = fileName;

                if (dlg.ShowDialog(parent) != DialogResult.OK)
                    return null;

                Settings.Default.CarafeModelDirectory = Path.GetDirectoryName(dlg.FileName);
                return dlg.FileName;
            }
        }

        private void textPath_TextChanged(object sender, EventArgs e)
        {
            UpdateDetails();
        }

        /// <summary>
        /// Shows what the file says about itself and its training, read without checking the
        /// models it holds (that is done once, on OK), or why it cannot be read.
        /// </summary>
        private void UpdateDetails()
        {
            string path = textPath.Text.Trim();
            bool exists = File.Exists(path);
            textPath.ForeColor = exists ? Color.Black : Color.Red;
            string details = string.Empty;
            if (exists)
            {
                try
                {
                    details = CarafeModelFile.ReadDescription(path).FormatInfo();
                }
                catch (Exception x)
                {
                    if (ExceptionUtil.IsProgrammingDefect(x))
                        throw;
                    details = x.Message;
                }
            }
            textDetails.Text = details;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        #region Functional test support

        public string ModelName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public string ModelPath
        {
            get { return textPath.Text; }
            set { textPath.Text = value; }
        }

        public string Details
        {
            get { return textDetails.Text; }
        }

        #endregion
    }
}
