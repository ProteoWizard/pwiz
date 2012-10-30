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
using System.ComponentModel;
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
        private bool _buildNew = true;  // Design mode with build UI showing
        private LongWaitDlg _longWaitDlg;
        private BackgroundProteomeSpec _backgroundProteomeSpec;
        private readonly MessageBoxHelper _messageBoxHelper;
        public BuildBackgroundProteomeDlg(IEnumerable<BackgroundProteomeSpec> existing)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _existing = existing;
            _messageBoxHelper = new MessageBoxHelper(this);

            // Fix dialog layout from design positioning
            labelFileNew.Left = labelFile.Left;
            labelFile.Visible = false;

            // Close the building section of the dialog.            
            BuildNew = false;
        }

        public bool BuildNew
        {
            get { return _buildNew; }            
            set
            {
                if (BuildNew == value)
                    return;

                _buildNew = value;

                // Update UI
                labelFileNew.Visible =
                    labelFasta.Visible = 
                    listboxFasta.Visible = 
                    btnAddFastaFile.Visible = _buildNew;
                labelFile.Visible = !labelFileNew.Visible;

                string btnText = btnBuild.Text;
                btnBuild.Text = btnText.Substring(0, btnText.Length - 2) +
                    (_buildNew ? "<<" : ">>"); // Not L10N

                if (_buildNew && string.IsNullOrEmpty(textPath.Text))
                {
                    textPath.Text = Settings.Default.ProteomeDbDirectory;
                    textName_TextChanged(this, new EventArgs());
                }

                ResizeForBuild();
                RefreshStatus();
            }
        }

        private void ResizeForBuild()
        {
            int delta = listboxFasta.Bottom - tbxStatus.Bottom;
            if (BuildNew)
                delta -= ClientSize.Height - labelFasta.Top;
            listboxFasta.Anchor &= ~AnchorStyles.Bottom;
            Height += (BuildNew ? delta : -delta);
            listboxFasta.Anchor |= AnchorStyles.Bottom;
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

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            string filterProtDb = TextUtil.FileDialogFiltersAll(FILTER_PROTDB);
            
            string fileName;
            if (BuildNew)
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = filterProtDb,
                    InitialDirectory = Settings.Default.ProteomeDbDirectory,
                    Title = Resources.BuildBackgroundProteomeDlg_btnBrowse_Click_Create_Background_Proteome,
                    OverwritePrompt = true,
                };
                if (saveFileDialog.ShowDialog() == DialogResult.Cancel)
                    return;

                fileName = saveFileDialog.FileName;

                // If the file exists, then the user chose to overwrite,
                // so delete the existing file.
                try
                {
                    FileEx.SafeDelete(fileName);
                }
                catch (IOException x)
                {
                    MessageDlg.Show(this, x.Message);
                    return;
                }
            }
            else
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = filterProtDb,
                    InitialDirectory = Settings.Default.ProteomeDbDirectory,
                    Title = Resources.BuildBackgroundProteomeDlg_btnBrowse_Click_Open_Background_Protoeme,
                    CheckFileExists = true,
                };
                if (openFileDialog.ShowDialog() == DialogResult.Cancel)
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

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if (!_messageBoxHelper.ValidateNameTextBox(new CancelEventArgs(), textName, out _name))
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

                int proteinCount = 0;
                if (File.Exists(textPath.Text))
                {
                    proteinCount = ProteomeDb.OpenProteomeDb(textPath.Text).GetProteinCount();
                }
                if (proteinCount == 0)
                {
                    if (!BuildNew)
                    {
                        _messageBoxHelper.ShowTextBoxError(textPath,
                            string.Format(Resources.BuildBackgroundProteomeDlg_OkDialog_The_proteome_file__0__does_not_exist, textPath.Text));
                        return;                        
                    }
                    btnAddFastaFile_Click(btnAddFastaFile, new EventArgs());
                    if (File.Exists(textPath.Text))
                        proteinCount = ProteomeDb.OpenProteomeDb(textPath.Text).GetProteinCount();
                }
                if (proteinCount == 0)
                {
                    return;
                }
            }
            catch (Exception)
            {
                string message;
                // In case exception is thrown opening protdb
                if (BuildNew)
                {
                    message = TextUtil.LineSeparate(Resources.BuildBackgroundProteomeDlg_OkDialog_The_proteome_file_is_not_valid,
                                                    Resources.BuildBackgroundProteomeDlg_OkDialog_Click_the__Browse__button_to_choose_a_valid_path_for_your_new_proteome_file);
                    MessageDlg.Show(this,message);
                }
                else
                {
                    message = TextUtil.LineSeparate(Resources.BuildBackgroundProteomeDlg_OkDialog_The_proteome_file_is_not_valid,
                                                    Resources.BuildBackgroundProteomeDlg_OkDialog_Choose_a_valid_proteome_file__or_click_the__Build__button_to_create_a_new_one_from_FASTA_files);
                    MessageDlg.Show(this,message);
                }
                return;
            }

            _databasePath = textPath.Text;
            _name = textName.Text;
            _backgroundProteomeSpec = new BackgroundProteomeSpec(_name, _databasePath);
            DialogResult = DialogResult.OK;
            Close();
        }

        private bool UpdateProgress(String message, int progress)
        {
            _longWaitDlg.ProgressValue = progress;
            _longWaitDlg.Message = message;
            return !_longWaitDlg.IsCanceled;
        }

        private void btnBuild_Click(object sender, EventArgs e)
        {
            BuildNew = !BuildNew;
            if (string.IsNullOrEmpty(textName.Text))
                textName.Focus();
            else
                textPath.Focus();
        }

        private void btnAddFastaFile_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = Resources.BuildBackgroundProteomeDlg_btnAddFastaFile_Click_Add_FASTA_File,
                InitialDirectory = Settings.Default.FastaDirectory,
                CheckPathExists = true
                // FASTA files often have no extension as well as .fasta and others
            };
            if (openFileDialog.ShowDialog(this) == DialogResult.Cancel)
            {
                return;
            }
            String fastaFilePath = openFileDialog.FileName;
            Settings.Default.LibraryDirectory = Path.GetDirectoryName(fastaFilePath);
            AddFastaFile(fastaFilePath);
        }

        public void AddFastaFile(string fastaFilePath)
        {
            String databasePath = textPath.Text;
            Settings.Default.FastaDirectory = Path.GetDirectoryName(fastaFilePath);
            _longWaitDlg = new LongWaitDlg
                               {
                                   ProgressValue = 0
                               };

            try
            {
                _longWaitDlg.PerformWork(this, 0, () =>
                {
                    if (!File.Exists(databasePath))
                    {
                        ProteomeDb.CreateProteomeDb(databasePath);
                    }
                    var proteomeDb = ProteomeDb.OpenProteomeDb(databasePath);
                    using (var reader = File.OpenText(fastaFilePath))
                    {
                        proteomeDb.AddFastaFile(reader, UpdateProgress);
                    }
                });
            }
            catch (Exception x)
            {
                var message = TextUtil.LineSeparate(string.Format(Resources.BuildBackgroundProteomeDlg_AddFastaFile_An_error_occurred_attempting_to_add_the_FASTA_file__0__,
                                                                  fastaFilePath),
                                                    x.Message);
                MessageDlg.Show(this, message);
                return;
            }
            string path = Path.GetFileName(fastaFilePath);
            if (path != null)
                listboxFasta.Items.Add(path);
            RefreshStatus();
        }

        private void textName_TextChanged(object sender, EventArgs e)
        {
            if (BuildNew)
            {
                string name = textName.Text;
                string outputPath = textPath.Text;
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
            if (BuildNew)
            {
                string path = textPath.Text;
                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(path);
                }
                catch (Exception)
                {
                    fullPath = null;
                }
                bool browse = string.IsNullOrEmpty(path) ||
                              !Equals(path, fullPath) ||
                              Directory.Exists(path);
                if (!browse)
                {
                    string dirName = Path.GetDirectoryName(textPath.Text);
                    browse = dirName != null && !Directory.Exists(dirName);
                }

                if (browse)
                {
                    tbxStatus.Text = Resources.BuildBackgroundProteomeDlg_RefreshStatus_Click_the_Browse_button_to_choose_a_path_for_a_new_proteome_file;
                    btnAddFastaFile.Enabled = false;
                    return;
                }
                btnAddFastaFile.Enabled = true;                
            }

            // Unless proven otherwise.
            btnBuild.Enabled = true;
            btnAddFastaFile.Enabled = true;

            if (File.Exists(textPath.Text))
            {
                try
                {
                    var longWaitDlg = new LongWaitDlg
                                          {
                                              Text = Resources.BuildBackgroundProteomeDlg_RefreshStatus_Loading_Proteome_File,
                                              Message = string.Format(Resources.BuildBackgroundProteomeDlg_RefreshStatus_Loading_protein_information_from__0__,
                                                      textPath.Text)
                                          };
                    ProteomeDb proteomeDb = null;
                    longWaitDlg.PerformWork(this, 1000, () => proteomeDb = ProteomeDb.OpenProteomeDb(textPath.Text));
                    if (proteomeDb == null)
                        throw new Exception();

                    int proteinCount = proteomeDb.GetProteinCount();
                    var digestions = proteomeDb.ListDigestions();
                    tbxStatus.Text = string.Format(Resources.BuildBackgroundProteomeDlg_RefreshStatus_The_proteome_file_contains__0__proteins,
                            proteinCount);
                    if (proteinCount != 0 && digestions.Count > 0)
                    {
                        tbxStatus.Text = TextUtil.LineSeparate(tbxStatus.Text, 
                            Resources.BuildBackgroundProteomeDlg_RefreshStatus_The_proteome_has_already_been_digested);
                        BuildNew = false;
                        btnBuild.Enabled = false;
                    }
                }
                catch (Exception)
                {
                    tbxStatus.Text = Resources.BuildBackgroundProteomeDlg_OkDialog_The_proteome_file_is_not_valid;
                    btnAddFastaFile.Enabled = false;
                }
            }
            else if (BuildNew)
            {
                tbxStatus.Text = Resources.BuildBackgroundProteomeDlg_RefreshStatus_Click_the_Add_File_button_to_add_a_FASTA_file_and_create_a_new_proteome_file;
                btnAddFastaFile.Enabled = !string.IsNullOrEmpty(textPath.Text);
            }
            else
            {
                tbxStatus.Text = Resources.BuildBackgroundProteomeDlg_RefreshStatus_Click_the_Browse_button_to_choose_an_existing_proteome_file_or_click_the_Build_button_to_create_a_new_proteome_file;
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
