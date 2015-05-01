/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Windows.Forms;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// Dialog box to create a background proteome database and add one or more FASTA
    /// files to it.
    /// </summary>
    public partial class BuildBackgroundProteomeDlg : FormEx
    {
        private readonly IEnumerable<BackgroundProteomeSpec> _existing;
        private String _databasePath;
        private String _name;
        private BackgroundProteomeSpec _backgroundProteomeSpec;
        private readonly MessageBoxHelper _messageBoxHelper;
        public BuildBackgroundProteomeDlg(IEnumerable<BackgroundProteomeSpec> existing)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _existing = existing;
            _messageBoxHelper = new MessageBoxHelper(this);
        }

        public string StatusText
        {
            get { return tbxStatus.Text; }
        }

        /// <summary>
        /// BackgroundProteomeSpec that this dialog is editing.  The property
        /// value will be null if this is for a new BackgroundProteomeSpec.
        /// </summary>
        public BackgroundProteomeSpec BackgroundProteomeSpec
        { 
            get { return _backgroundProteomeSpec; } 
            set 
            { 
                _backgroundProteomeSpec = value;
                if (_backgroundProteomeSpec == null)
                {
                    textPath.Text = string.Empty;
                    textName.Text = string.Empty;
                }
                else
                {
                    textPath.Text = _backgroundProteomeSpec.DatabasePath;
                    textName.Text = _backgroundProteomeSpec.Name;
                }
                RefreshStatus();
            }
        }

        public static string FILTER_PROTDB
        {
            get { return TextUtil.FileDialogFilter(Resources.BuildBackgroundProteomeDlg_FILTER_PROTDB_Proteome_File, ProteomeDb.EXT_PROTDB); }
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            string filterProtDb = TextUtil.FileDialogFiltersAll(FILTER_PROTDB);
            
            string fileName;
            using (var openFileDialog = new OpenFileDialog
            {
                Filter = filterProtDb,
                InitialDirectory = Settings.Default.ProteomeDbDirectory,
                Title = Resources.BuildBackgroundProteomeDlg_btnOpen_Click_Open_Background_Protoeme,
                CheckFileExists = true,
            })
            {
                if (openFileDialog.ShowDialog(this) == DialogResult.Cancel)
                    return;

                fileName = openFileDialog.FileName;
            }
            Settings.Default.ProteomeDbDirectory = Path.GetDirectoryName(fileName);

            textPath.Text = fileName;
            if (textName.Text.Length == 0)
            {
                textName.Text = Path.GetFileNameWithoutExtension(fileName);
            }
            RefreshStatus();
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            string filterProtDb = TextUtil.FileDialogFiltersAll(FILTER_PROTDB);

            string fileName;
            using (var saveFileDialog = new SaveFileDialog
            {
                Filter = filterProtDb,
                InitialDirectory = Settings.Default.ProteomeDbDirectory,
                Title = Resources.BuildBackgroundProteomeDlg_btnCreate_Click_Create_Background_Proteome,
                OverwritePrompt = true,
            })
            {
                if (saveFileDialog.ShowDialog(this) == DialogResult.Cancel)
                    return;

                fileName = saveFileDialog.FileName;
            }

            // If the file exists, then the user chose to overwrite,
            // so delete the existing file.
            try
            {
                FileEx.SafeDelete(fileName);
            }
            catch (IOException x)
            {
                MessageDlg.ShowException(this, x);
                return;
            }

            Settings.Default.ProteomeDbDirectory = Path.GetDirectoryName(fileName);

            textPath.Text = fileName;
            if (textName.Text.Length == 0)
            {
                textName.Text = Path.GetFileNameWithoutExtension(fileName);
            }

            try
            {
                ProteomeDb.CreateProteomeDb(fileName);
            }
            catch (Exception x)
            {
                var message = TextUtil.LineSeparate(string.Format(Resources.BuildBackgroundProteomeDlg_btnCreate_Click_An_error_occurred_attempting_to_create_the_proteome_file__0__,
                                                                  fileName), x.Message);
                MessageDlg.ShowWithException(this, message, x);
            }

            RefreshStatus();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if (!_messageBoxHelper.ValidateNameTextBox(textName, out _name))
            {
                return;
            }
            if (_backgroundProteomeSpec == null || _name != _backgroundProteomeSpec.Name)
            {
                foreach (BackgroundProteomeSpec backgroundProteomeSpec in _existing)
                {
                    if (_name == backgroundProteomeSpec.Name)
                    {
                        _messageBoxHelper.ShowTextBoxError(textName, Resources.BuildBackgroundProteomeDlg_OkDialog_The_background_proteome__0__already_exists, _name);
                        return;
                    }
                }
            }
            if (string.IsNullOrEmpty(textPath.Text))
            {
                _messageBoxHelper.ShowTextBoxError(textPath, Resources.BuildBackgroundProteomeDlg_OkDialog_You_must_specify_a_proteome_file);
                return;
            }
            try
            {
                if (textPath.Text != Path.GetFullPath(textPath.Text))
                {
                    _messageBoxHelper.ShowTextBoxError(textPath, Resources.BuildBackgroundProteomeDlg_OkDialog_Please_specify_a_full_path_to_the_proteome_file);
                    return;                    
                }
                else if (!File.Exists(textPath.Text))
                {
                    _messageBoxHelper.ShowTextBoxError(textPath,
                               string.Format(Resources.BuildBackgroundProteomeDlg_OkDialog_The_proteome_file__0__does_not_exist, textPath.Text));
                    return; 
                }

                ProteomeDb.OpenProteomeDb(textPath.Text);
            }
            catch (Exception x)
            {
                // In case exception is thrown opening protdb
                string message = TextUtil.LineSeparate(Resources.BuildBackgroundProteomeDlg_OkDialog_The_proteome_file_is_not_valid,
                                                       Resources.BuildBackgroundProteomeDlg_OkDialog_Choose_a_valid_proteome_file__or_click_the__Create__button_to_create_a_new_one_from_FASTA_files);
                MessageDlg.ShowWithException(this, message, x);
                return;
            }

            _databasePath = textPath.Text;
            _name = textName.Text;
            _backgroundProteomeSpec = new BackgroundProteomeSpec(_name, _databasePath);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnAddFastaFile_Click(object sender, EventArgs e)
        {
            using (var openFileDialog = new OpenFileDialog
            {
                Title = Resources.BuildBackgroundProteomeDlg_btnAddFastaFile_Click_Add_FASTA_File,
                InitialDirectory = Settings.Default.FastaDirectory,
                CheckPathExists = true
                // FASTA files often have no extension as well as .fasta and others
            })
            {
                if (openFileDialog.ShowDialog(this) == DialogResult.Cancel)
                {
                    return;
                }
                String fastaFilePath = openFileDialog.FileName;
                Settings.Default.LibraryDirectory = Path.GetDirectoryName(fastaFilePath);
                AddFastaFile(fastaFilePath);
            }
        }

        public void AddFastaFile(string fastaFilePath)
        {
            String databasePath = textPath.Text;
            Settings.Default.FastaDirectory = Path.GetDirectoryName(fastaFilePath);
            using (var longWaitDlg = new LongWaitDlg { ProgressValue = 0 })
            {
                var progressMonitor = new ProgressMonitor(longWaitDlg);
                try
                {
                    longWaitDlg.PerformWork(this, 0, () =>
                    {
                        ProteomeDb proteomeDb = File.Exists(databasePath)
                            ? ProteomeDb.OpenProteomeDb(databasePath)
                            : ProteomeDb.CreateProteomeDb(databasePath);
                        using (proteomeDb)
                        {
                            using (var reader = File.OpenText(fastaFilePath))
                            {
                                proteomeDb.AddFastaFile(reader, progressMonitor.UpdateProgress);
                            }
                        }
                    });
                }
                catch (Exception x)
                {
                    var message = TextUtil.LineSeparate(string.Format(Resources.BuildBackgroundProteomeDlg_AddFastaFile_An_error_occurred_attempting_to_add_the_FASTA_file__0__,
                                                                      fastaFilePath), x.Message);
                    MessageDlg.ShowWithException(this, message, x);
                    return;
                }
            }

            string path = Path.GetFileName(fastaFilePath);
            if (path != null)
                listboxFasta.Items.Add(path);
            RefreshStatus();
        }

        private class ProgressMonitor
        {
            private readonly LongWaitDlg _longWaitDlg;

            public ProgressMonitor(LongWaitDlg longWaitDlg)
            {
                _longWaitDlg = longWaitDlg;
            }

            public bool UpdateProgress(string message, int progress)
            {
                _longWaitDlg.ProgressValue = progress;
                _longWaitDlg.Message = message;
                return !_longWaitDlg.IsCanceled;
            }
        }

        private void textName_TextChanged(object sender, EventArgs e)
        {
            string name = textName.Text;
            string outputPath = textPath.Text;
            if (!File.Exists(outputPath))
            {
                if (outputPath.Length > 0 && !Directory.Exists(outputPath))
                {
                    try
                    {
                        outputPath = Path.GetDirectoryName(outputPath);
                    }
                    catch (Exception)
                    {
                        outputPath = string.Empty;
                    }
                }
                string id = (name.Length == 0 ? string.Empty : Helpers.MakeId(textName.Text));
                if (id.Length == 0)
                    textPath.Text = outputPath;
                else if (!string.IsNullOrEmpty(outputPath))
                    textPath.Text = Path.Combine(outputPath, id + ProteomeDb.EXT_PROTDB);
            }
        }

        private void textPath_TextChanged(object sender, EventArgs e)
        {
            listboxFasta.Items.Clear();
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (File.Exists(textPath.Text))
            {
                btnAddFastaFile.Enabled = true;
                ProteomeDb proteomeDb = null;
                try
                {
                    using (var longWaitDlg = new LongWaitDlg
                    {
                        Text = Resources.BuildBackgroundProteomeDlg_RefreshStatus_Loading_Proteome_File,
                        Message =
                            string.Format(
                                Resources.BuildBackgroundProteomeDlg_RefreshStatus_Loading_protein_information_from__0__,
                                textPath.Text)
                    })
                    {
                        longWaitDlg.PerformWork(this, 1000, () => proteomeDb = ProteomeDb.OpenProteomeDb(textPath.Text));
                    }
                    if (proteomeDb == null)
                        throw new Exception();

                    int proteinCount = proteomeDb.GetProteinCount();
                    var digestions = proteomeDb.ListDigestions();
                    tbxStatus.Text =
                        string.Format(
                            Resources.BuildBackgroundProteomeDlg_RefreshStatus_The_proteome_file_contains__0__proteins,
                            proteinCount);
                    if (proteinCount != 0 && digestions.Count > 0)
                    {
                        tbxStatus.Text = TextUtil.LineSeparate(tbxStatus.Text,
                            Resources.BuildBackgroundProteomeDlg_RefreshStatus_The_proteome_has_already_been_digested);
                    }
                }
                catch (Exception)
                {
                    tbxStatus.Text = Resources.BuildBackgroundProteomeDlg_OkDialog_The_proteome_file_is_not_valid;
                    btnAddFastaFile.Enabled = false;
                }
                finally
                {
                    if (null != proteomeDb)
                    {
                        proteomeDb.Dispose();
                    }
                }
            }
            else
            {
                btnAddFastaFile.Enabled = false;
                tbxStatus.Text = Resources.BuildBackgroundProteomeDlg_RefreshStatus_Click_the_Open_button_to_choose_an_existing_proteome_file_or_click_the_Create_button_to_create_a_new_proteome_file;
            }
        }

        #region Functional test support

        public String BackgroundProteomeName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public String BackgroundProteomePath
        {
            get { return textPath.Text; }
            set { textPath.Text = value; }
        }

        #endregion
    }
}
