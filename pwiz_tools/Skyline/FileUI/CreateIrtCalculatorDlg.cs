/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class CreateIrtCalculatorDlg : FormEx
    {
        public List<SpectrumMzInfo> LibrarySpectra { get { return _librarySpectra; } } 
        public List<DbIrtPeptide> DbIrtPeptides { get { return _dbIrtPeptides; } } 
        public string IrtFile { get; private set; }

        private List<SpectrumMzInfo> _librarySpectra;
        private List<DbIrtPeptide> _dbIrtPeptides; 

        public CreateIrtCalculatorDlg(SrmDocument document, IList<RetentionScoreCalculatorSpec> existing)
        {
            _existing = existing;
            Document = document;
            InitializeComponent();
            _librarySpectra = new List<SpectrumMzInfo>();
            _dbIrtPeptides = new List<DbIrtPeptide>();
            UpdateSelection(true);
        }

        public SrmDocument Document { get; private set; }

        private readonly IList<RetentionScoreCalculatorSpec> _existing;

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            bool useExisting = radioUseExisting.Checked;
            if (textCalculatorName.Text.Length == 0)
            {
                MessageDlg.Show(this, Resources.CreateIrtCalculatorDlg_OkDialog_Calculator_name_cannot_be_empty);
                return;
            }
            if (_existing.Select(spec => spec.Name).Contains(textCalculatorName.Text))
            {
                var replaceResult = MultiButtonMsgDlg.Show(this, Resources.CreateIrtCalculatorDlg_OkDialog_A_calculator_with_that_name_already_exists___Do_you_want_to_replace_it_,
                                       MultiButtonMsgDlg.BUTTON_YES,           
                                       MultiButtonMsgDlg.BUTTON_NO, 
                                       false);
                if (replaceResult == DialogResult.No)
                    return;
            }
            if (useExisting)
            {
                try
                {
                    if (!File.Exists(textOpenDatabase.Text))
                    {
                        MessageDlg.Show(this, Resources.CreateIrtCalculatorDlg_OkDialog_iRT_database_field_must_contain_a_path_to_a_valid_file_);
                        return;
                    }
                    var db = IrtDb.GetIrtDb(textOpenDatabase.Text, null);
                    if (db == null)
                    {
                        throw new DatabaseOpeningException(string.Format(Resources.CreateIrtCalculatorDlg_OkDialog_Cannot_read_the_database_file__0_, textOpenDatabase.Text));
                    }
                }
                catch (Exception x)
                {
                    MessageDlg.Show(this, string.Format(Resources.CreateIrtCalculatorDlg_OkDialog_Failed_to_open_the_database_file___0_, x.Message));
                    return;
                }
            }
            else
            {
                if (textNewDatabase.Text.Length == 0)
                {
                    MessageDlg.Show(this, Resources.CreateIrtCalculatorDlg_OkDialog_iRT_database_field_must_not_be_empty_);
                    return;
                }
                if (!CreateDatabase(textNewDatabase.Text))
                    return;
            }
            // Make a version of the document with the new calculator in it
            var calculator = new RCalcIrt(textCalculatorName.Text, useExisting ? textOpenDatabase.Text : textNewDatabase.Text);
            // CONSIDER: Probably can't use just a static default like 10 below
            var retentionTimeRegression = new RetentionTimeRegression(calculator.Name, calculator, null, null, 10, new List<MeasuredRetentionTime>());
            var docNew = Document.ChangeSettings(Document.Settings.ChangePeptidePrediction(prediction =>
                prediction.ChangeRetentionTime(retentionTimeRegression)));
            // Import transition list of standards, if applicable
            if (!useExisting)
            {
                try
                {
                    if (!File.Exists(textImportText.Text))
                    {
                        MessageDlg.Show(this, Resources.CreateIrtCalculatorDlg_OkDialog_Transition_list_field_must_contain_a_path_to_a_valid_file_);
                        return;
                    }
                    IFormatProvider provider;
                    char sep;
                    using (var readerLine = new StreamReader(textImportText.Text))
                    {
                        Type[] columnTypes;
                        string line = readerLine.ReadLine();
                        if (!MassListImporter.IsColumnar(line, out provider, out sep, out columnTypes))
                            throw new IOException(Resources.SkylineWindow_importMassListMenuItem_Click_Data_columns_not_found_in_first_line);
                    }
                    using (var readerList = new StreamReader(textImportText.Text))
                    {
                        IdentityPath selectPath;
                        List<KeyValuePair<string, double>> irtPeptides;
                        docNew = docNew.ImportMassList(readerList, provider, sep, null, out selectPath, out irtPeptides, out _librarySpectra);
                        _dbIrtPeptides = irtPeptides.Select(pair => new DbIrtPeptide(pair.Key, pair.Value, true, TimeSource.scan)).ToList();
                    }
                    IrtFile = textImportText.Text;
                }
                catch (Exception x)
                {
                    MessageDlg.Show(this, string.Format(Resources.CreateIrtCalculatorDlg_OkDialog_Error_reading_iRT_standards_transition_list___0_, x.Message));
                    return;
                }
            }
            Document = docNew;
            DialogResult = DialogResult.OK;
        }

        public bool CreateDatabase(string path)
        {
            try
            {
                FileEx.SafeDelete(path);
            }
            catch (IOException x)
            {
                MessageDlg.Show(this, x.Message);
                return false;
            }

            //Create file, initialize db
            try
            {
                IrtDb.CreateIrtDb(path);
            }
            catch (DatabaseOpeningException x)
            {
                MessageDlg.Show(this, x.Message);
                return false;
            }
            catch (Exception x)
            {
                var message = TextUtil.LineSeparate(string.Format(Resources.EditIrtCalcDlg_CreateDatabase_The_file__0__could_not_be_created, path),
                                                    x.Message);
                MessageDlg.Show(this, message);
                return false;
            }
            return true;
        }

        private void btnBrowseDb_Click(object sender, EventArgs e)
        {
            BrowseDb();
        }

        public void BrowseDb()
        {
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Resources.EditIrtCalcDlg_btnBrowseDb_Click_Open_iRT_Database,
                InitialDirectory = Settings.Default.ActiveDirectory,
                DefaultExt = IrtDb.EXT,
                Filter = TextUtil.FileDialogFiltersAll(IrtDb.FILTER_IRTDB)
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);
                    textOpenDatabase.Text = dlg.FileName;
                    textOpenDatabase.Focus();
                }
            }
        }

        private void btnCreateDb_Click(object sender, EventArgs e)
        {
            CreateDb();
        }

        public void CreateDb()
        {
            using (var dlg = new SaveFileDialog
            {
                Title = Resources.EditIrtCalcDlg_btnCreateDb_Click_Create_iRT_Database,
                InitialDirectory = Settings.Default.ActiveDirectory,
                OverwritePrompt = true,
                DefaultExt = IrtDb.EXT,
                Filter = TextUtil.FileDialogFiltersAll(IrtDb.FILTER_IRTDB)
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);
                    textNewDatabase.Text = dlg.FileName;
                    textNewDatabase.Focus();
                }
            }
        }

        private void btnBrowseText_Click(object sender, EventArgs e)
        {
            ImportTextFile();
        }

        public void ImportTextFile()
        {
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Resources.CreateIrtCalculatorDlg_ImportTextFile_Import_Transition_List__iRT_standards_,
                InitialDirectory = Settings.Default.ActiveDirectory,
                DefaultExt = TextUtil.EXT_CSV,
                Filter = TextUtil.FileDialogFiltersAll(TextUtil.FileDialogFilter(
                    Resources.SkylineWindow_importMassListMenuItem_Click_Transition_List, TextUtil.EXT_CSV, TextUtil.EXT_TSV))
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);
                    textImportText.Text = dlg.FileName;
                    textImportText.Focus();
                }
            }
        }

        private void radioUseExisting_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSelection(radioUseExisting.Checked);
        }

        private void radioCreateNew_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSelection(radioUseExisting.Checked);
        }

        public void UpdateSelection(bool useExisting)
        {
            textImportText.Enabled = !useExisting;
            textNewDatabase.Enabled = !useExisting;
            btnBrowseText.Enabled = !useExisting;
            btnCreateDb.Enabled = !useExisting;

            textOpenDatabase.Enabled = useExisting;
            btnBrowseDb.Enabled = useExisting;
        }

        #region test helpers

        public string CalculatorName
        {
            get { return textCalculatorName.Text; }
            set { textCalculatorName.Text = value; }
        }

        public string ExistingDatabaseName
        {
            get { return textOpenDatabase.Text; }
            set { textOpenDatabase.Text = value; }
        }

        public string NewDatabaseName
        {
            get { return textNewDatabase.Text; }
            set { textNewDatabase.Text = value; }
        }

        public string TextFilename
        {
            get { return textImportText.Text; }
            set { textImportText.Text = value; }
        }

        public bool UseExisting
        {
            get { return radioUseExisting.Checked; }
            set
            {
                radioUseExisting.Checked = value;
                radioCreateNew.Checked = !value;
                UpdateSelection(value);
            }
        }

        #endregion
    }
}
