/*
 * Original author: John Chilton <jchilton .at. uw.edu>,
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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditIrtCalcDlg : Form
    {
        private const string STANDARD_TABLE_NAME = "standard";
        private const string LIBRARY_TABLE_NAME = "library";

        private readonly IEnumerable<RetentionScoreCalculatorSpec> _existingCalcs;

        public RetentionScoreCalculatorSpec Calculator { get; private set; }
        //These properties are for records that have been changed, but could not be persisted
        //at the time of change. If any of them contain values, the user should be notified
        //when the dialog closes.
        public List<MeasuredPeptide> DirtyLibraryAdd { get; private set; }
        public List<MeasuredPeptide> DirtyLibraryRemove { get; private set; }
        public List<MeasuredPeptide> DirtyStandardUpdate { get; private set; }

        private readonly string _databaseStartPath = "";
        //Used to determine whether we are creating a new calculator, trying to overwrite
        //an old one, or editing an old one
        private readonly string _editingName = "";

        public EditIrtCalcDlg(RetentionScoreCalculatorSpec calc, IEnumerable<RetentionScoreCalculatorSpec> existingCalcs)
        {
            InitializeComponent();

            _existingCalcs = existingCalcs;

            DirtyLibraryAdd = new List<MeasuredPeptide>();
            DirtyLibraryRemove = new List<MeasuredPeptide>();
            DirtyStandardUpdate = new List<MeasuredPeptide>();

            if (calc != null)
            {
                textCalculatorName.Text = calc.Name;
                RCalcIrt c = (RCalcIrt) calc;
                textDatabase.Text = c.DatabasePath;

                _databaseStartPath = c.DatabasePath;
                _editingName = calc.Name;

                OpenDatabase(true);
            }

            UpdateNumPeptides();
        }

        public void DisableEditStandard()
        {
            gridViewStandard.AllowUserToAddRows = false;
            gridViewStandard.AllowUserToDeleteRows = false;
            btnCalibrate.Enabled = false;
        }

        /// <summary>
        /// This function will open a database file and fill in the standard and library tables. If
        /// initialOpening is true, it will not add the library to the remove buffer. This functionality
        /// is needed because of the case when a user may open an existing database file and then add
        /// peptides to the library.
        /// 
        /// If the database path is different from what it was when the dialog was opened, then everything
        /// in both tables must be added to the new file. But if the path is different because a new file
        /// was opened, the old peptides must be deleted first in order to avoid duplicates in the database.
        /// 
        /// initialOpening means that this function is getting called when the dialog is first opened with
        /// an initial path. In that case, we do not want to delete all the peptides.
        /// </summary>
        /// <param name="initialOpening"></param>
        private void OpenDatabase(bool initialOpening)
        {
            var file = textDatabase.Text;
            if (!File.Exists(file))
            {
                MessageDlg.Show(this, String.Format("The file {0} does not exist. Click the Create button to create a new database or the Browse button to find the missing file.", file));
                return;
            }

            try
            {
                IrtDb db = IrtDb.GetIrtDb(textDatabase.Text, null); // CONSIDER: LongWaitDlg?
                LoadStandard(db.StandardPeptides);

                LoadLibrary(db.LibraryPeptides);

                DisableEditStandard();
                
                List<MeasuredPeptide> libraryPeps;
                if (!ValidatePeptideTable(gridViewLibrary, LIBRARY_TABLE_NAME, out libraryPeps))
                {
                    //This should never happen. It is such an edge case that it really should present
                    //the uncaught exception dialog to the user.
                    throw new InvalidDataException();
                }

                if(!initialOpening)
                    DirtyLibraryRemove = libraryPeps;
            }
            catch (DatabaseOpeningException e)
            {
                MessageDlg.Show(this, e.Message);
            }
        }

        public void OkDialog()
        {
            if(textCalculatorName.Text.Length < 1)
            {
                MessageDlg.Show(this, "Please enter a name for the iRT Standard.");
                return;
            }

            if (_existingCalcs != null)
            {
                foreach (var existingCalc in _existingCalcs)
                {
                    if (Equals(existingCalc.Name, textCalculatorName.Text) && !Equals(existingCalc.Name, _editingName))
                    {
                        if (MessageBox.Show(this, String.Format("A calculator with the name {0} already exists. Do you want to overwrite it?", textCalculatorName.Text),
                                            Program.Name, MessageBoxButtons.YesNo) != DialogResult.Yes)
                            return;
                    }
                }
            }

            //This function MessageBox.Show's error messages
            List<MeasuredPeptide> standardTable;
            List<MeasuredPeptide> libraryTable;
            if(!ValidatePeptideTable(gridViewStandard, STANDARD_TABLE_NAME, out standardTable))
                return;
            if(!ValidatePeptideTable(gridViewLibrary, LIBRARY_TABLE_NAME, out libraryTable))
                return;

            if (standardTable.Count < CalibrateIrtDlg.MIN_STANDARD_PEPTIDES)
            {
                MessageDlg.Show(this, string.Format("Please enter at least {0} peptides.", CalibrateIrtDlg.MIN_STANDARD_PEPTIDES));
                return;
            }

            if (standardTable.Count < CalibrateIrtDlg.MIN_SUGGESTED_STANDARD_PEPTIDES)
            {
                DialogResult result = MessageBox.Show(this, string.Format("Using fewer than {0} standard peptides is not recommended. Are you sure you want to continue with only {1}?", CalibrateIrtDlg.MIN_SUGGESTED_STANDARD_PEPTIDES, standardTable.Count),
                    Program.Name, MessageBoxButtons.YesNo);
                if (result != DialogResult.Yes)
                    return;
            }

            try
            {
                var calculator = new RCalcIrt(textCalculatorName.Text, textDatabase.Text);

                IrtDb db;
                if (File.Exists(textDatabase.Text))
                    db = IrtDb.GetIrtDb(textDatabase.Text, null);   // CONSIDER: LongWaitDlg?
                else
                    db = IrtDb.CreateIrtDb(textDatabase.Text);
                
                if(_databaseStartPath != textDatabase.Text)
                {
                    DirtyLibraryAdd = libraryTable;
                    DirtyStandardUpdate = standardTable;
                }

                if (DirtyStandardUpdate.Count > 0)
                    db = db.UpdateStandard(ConvertFromMeasuredPeptide(DirtyStandardUpdate, true));
                if (DirtyLibraryRemove.Count > 0)
                    db = db.DeletePeptides(DirtyLibraryRemove.Select(pep => pep.Sequence));
                if (DirtyLibraryAdd.Count > 0)
                    db = db.AddPeptides(ConvertFromMeasuredPeptide(DirtyLibraryAdd, false));

                Calculator = calculator.ChangeDatabase(db);
            }
            catch(DatabaseOpeningException x)
            {
                MessageDlg.Show(this, x.Message);
                return;
            }

            DialogResult = DialogResult.OK;
        }

        private bool ValidatePeptideTable(DataGridView table, string tableName, out List<MeasuredPeptide> tableData)
        {
            tableData = new List<MeasuredPeptide>();
            var sequenceSet = new HashSet<string>();
            foreach(DataGridViewRow row in table.Rows)
            {
                object pep = row.Cells[0].Value;
                object iRT = row.Cells[1].Value;
                if(!ValidateRow(new[] { pep, iRT }))
                {
                    //Empty row
                    if(pep == null && iRT == null)
                    {
                        continue;
                    }

                    MessageDlg.Show(this, string.Format("Please ensure that each {0} entry has a peptide and a numerical iRT value.", tableName));
                    return false;
                }

                string seqModified = pep.ToString();
                if (sequenceSet.Contains(seqModified))
                {
                    MessageDlg.Show(this, string.Format("The peptide {0} appears in the {1} table more than once.", seqModified, tableName));
                    return false;
                }

                tableData.Add(new MeasuredPeptide(pep.ToString(), double.Parse(iRT.ToString())));
            }

            return true;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private static IEnumerable<MeasuredPeptide> ConvertFromDbIrtPeptide(IEnumerable<DbIrtPeptide> source)
        {
            return source.Select(dbpep => new MeasuredPeptide(dbpep.PeptideModSeq, dbpep.Irt)).ToList();
        }
        private static IEnumerable<DbIrtPeptide> ConvertFromMeasuredPeptide(IEnumerable<MeasuredPeptide> source, bool standard)
        {
            return source.Select(mpep => new DbIrtPeptide(mpep.Sequence, mpep.RetentionTimeOrIrt, standard, true));
        }

        public void btnCalibrate_Click(object sender, EventArgs e)
        {
            Calibrate();
        }

        public int UpdateNumPeptides()
        {
            int count = gridViewLibrary.Rows.Count;
            labelNumPeptides.Text = string.Format("{0} Peptides", count);
            return count;
        }
        
        private void LoadStandard(IEnumerable<DbIrtPeptide> standard)
        {
            gridViewStandard.Rows.Clear();
            foreach (DbIrtPeptide pep in standard.OrderBy(pep => pep.Irt))
            {
                int n = gridViewStandard.Rows.Add();
                gridViewStandard.Rows[n].Cells[0].Value = pep.PeptideModSeq;
                gridViewStandard.Rows[n].Cells[1].Value = string.Format("{0:F04}", pep.Irt.ToString());
            }
        }

        private void LoadLibrary(IEnumerable<DbIrtPeptide> library)
        {
            gridViewLibrary.Rows.Clear();
            foreach (DbIrtPeptide pep in library.OrderBy(pep => pep.Irt))
            {
                int n = gridViewLibrary.Rows.Add();
                gridViewLibrary.Rows[n].Cells[0].Value = pep.PeptideModSeq;
                gridViewLibrary.Rows[n].Cells[1].Value = string.Format("{0:F04}", pep.Irt.ToString());
            }

            UpdateNumPeptides();
        }

        private void AddToAddBuffer(MeasuredPeptide pep)
        {
            foreach(var addPep in DirtyLibraryAdd)
            {
                if (Equals(pep.Sequence, addPep.Sequence))
                {
                    DirtyLibraryAdd.Remove(addPep);
                    break;
                }
            }

            DirtyLibraryAdd.Add(pep);
        }

        private void AddToLibrary(IEnumerable<MeasuredPeptide> peptides)
        {
            foreach (var pep in peptides)
            {
                bool inLibrary = false;
                foreach (DataGridViewRow row in gridViewLibrary.Rows)
                {
                    if (row.Cells[0].Value != null && Equals(row.Cells[0].Value.ToString(), pep.Sequence))
                    {
                        gridViewLibrary.Rows.Remove(row);
                        DirtyLibraryRemove.Add(pep);
                        inLibrary = true;
                        break;
                    }
                }

                if(inLibrary)
                    AddToAddBuffer(pep);
                else //save the trouble of another foreach
                    DirtyLibraryAdd.Add(pep);

                int n = gridViewLibrary.Rows.Add();
                gridViewLibrary.Rows[n].Cells[0].Value = pep.Sequence;
                gridViewLibrary.Rows[n].Cells[1].Value = string.Format("{0:F04}", pep.RetentionTimeOrIrt);
            }
        }

        /// <summary>
        /// If the document contains the standard, this function does a regression of the document standard vs. the
        /// calculator standard and calculates iRTs for all the non-standard peptides
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAddResults_Click(object sender, EventArgs e)
        {
            AddResults();
        }

        /// <summary>
        /// Gets and sets a list of MeasuredPeptides that are in a given list of MeasuredPeptides representing a standard.
        /// Returns the number of matched peptides.
        /// </summary>
        private static int GetStandardFromDocument(List<MeasuredPeptide> docPeps, IEnumerable<MeasuredPeptide> standard, out List<MeasuredPeptide> docStandard)
        {
            docStandard = new List<MeasuredPeptide>();

            int standardCount = 0;
            int standardCopyCount = 0;
            foreach(var pep in standard)
            {
                string pep1 = pep.Sequence;
                MeasuredPeptide node = docPeps.Find(peptide => Equals(peptide.Sequence, pep1));

                standardCount++;
                if(node != default(MeasuredPeptide))
                {
                    standardCopyCount++;
                    docStandard.Add(node);
                }
            }
            if(standardCopyCount != standardCount)
                docStandard = null;
            return standardCopyCount;
        }

        private void gridViewStandard_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle Ctrl + V for paste
            if (e.KeyCode == Keys.V && e.Control)
            {
                DoPaste();
            }
            
            //If we delete from the standard, the library is invalid
            //else if (e.KeyCode == Keys.Delete)
            //    gridViewStandard.DoDelete();
        }

        public static bool ValidateRow(object[] columns)
        {
            double x;
            if(columns.Length != 2)
                return false;
            string seq = columns[0] as string;
            string iRT = columns[0] as string;
            return (!string.IsNullOrEmpty(seq) && !string.IsNullOrEmpty(iRT) && double.TryParse((string)columns[1], out x));
        }

        private void btnCreateDb_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog dlg = new SaveFileDialog
                        {
                            Title = "Create iRT Database",
                            InitialDirectory = Settings.Default.ActiveDirectory,
                            OverwritePrompt = true,
                            DefaultExt = IrtDb.EXT_IRTDB,
                            Filter = string.Join("|", new[]
                                {
                                    "iRT Database Files (*" + IrtDb.EXT_IRTDB + ")|*" + IrtDb.EXT_IRTDB,
                                    "All Files (*.*)|*.*"
                                })
                        })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);

                    //The file that was just created does not have a schema, so SQLite won't touch it.
                    //The file must have a schema or not exist for use with SQLite, so we'll delete
                    //it and install a schema

                    //Create file, initialize db
                    try
                    {
                        File.Delete(dlg.FileName);
                        IrtDb.CreateIrtDb(dlg.FileName);
                    }
                    catch (DatabaseOpeningException x)
                    {
                        MessageDlg.Show(this, x.Message);
                    }
                    catch (Exception)
                    {
                        MessageDlg.Show(this, String.Format("The file {0} could not be created.", dlg.FileName));
                    }
                    textDatabase.Text = dlg.FileName;
                }
            }
        }

        private void btnBrowseDb_Click(object sender, EventArgs e)
        {
            if (DirtyLibraryAdd.Count > 0 || DirtyStandardUpdate.Count > 0)
            {
                var result = MessageBox.Show(
                    "Are you sure you want to open a new database file? Any changes to the current calculator will be lost.",
                    Program.Name, MessageBoxButtons.YesNo);

                if (result != DialogResult.Yes)
                    return;
            }

            using (OpenFileDialog dlg = new OpenFileDialog
                        {
                            Title = "Browse for iRT Database",
                            InitialDirectory = Settings.Default.ActiveDirectory,
                            DefaultExt = IrtDb.EXT_IRTDB,
                            Filter = string.Join("|", new[]
                                {
                                    "iRT Database Files (*" + IrtDb.EXT_IRTDB + ")|*" + IrtDb.EXT_IRTDB,
                                    "All Files (*.*)|*.*"
                                })
                        })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);

                    textDatabase.Text = dlg.FileName;

                    OpenDatabase(false);
                }
            }
        }

        #region Functional Test Support

        //This one is a little bit of a departure from most FT support because I couldn't think of
        //a great way to do this based on the existing code. I think reusing this excerpt is
        //appropriate, at least at the time of writing
        public void CreateDatabase(string path)
        {
            IrtDb.CreateIrtDb(path);
            textDatabase.Text = path;
        }

        public void OpenDatabase()
        {
            OpenDatabase(false);
        }

        public void SetDatabasePath(string path)
        {
            textDatabase.Text = path;
        }

        public void SetStandardName(string name)
        {
            textCalculatorName.Text = name;
        }

        public void Calibrate()
        {
            using (var calibrateDlg = new CalibrateIrtDlg())
            {
                if (calibrateDlg.ShowDialog(this) == DialogResult.OK)
                {
                    LoadStandard(calibrateDlg.CalibrationPeptides);

                    //RecalculateLibrary(calibrateDlg.CalibrationPeptides);

                    DirtyStandardUpdate = ConvertFromDbIrtPeptide(calibrateDlg.CalibrationPeptides).ToList();
                }
            }
        }

        public bool GetStandard(out List<MeasuredPeptide> tableData)
        {
            return ValidatePeptideTable(gridViewStandard, STANDARD_TABLE_NAME, out tableData);
        }

        public void DoPaste()
        {
            gridViewStandard.DoPaste(this, ValidateRow);

            List<MeasuredPeptide> dirtyStandard;
            ValidatePeptideTable(gridViewStandard, STANDARD_TABLE_NAME, out dirtyStandard);
            DirtyStandardUpdate = dirtyStandard;
        }

        public void LoadStandard(List<MeasuredPeptide> standard)
        {
            LoadStandard(ConvertFromMeasuredPeptide(standard, true));
        }

        public int GetNumStandardPeptides()
        {
            //If the grid is editable, there is a "New Row" for new entries
            return gridViewStandard.AllowUserToAddRows ? gridViewStandard.RowCount - 1 : gridViewStandard.RowCount;
        }

        public void Cancel()
        {
            DialogResult = DialogResult.Cancel;
        }

        public void AddResults()
        {
            var document = Program.ActiveDocumentUI;
            var settings = document.Settings;

            // Get all peptides with usable retention times
            var allDocPeptides = new List<MeasuredPeptide>();
            foreach (var nodePep in document.Peptides)
            {
                if (nodePep.SchedulingTime.HasValue)
                {
                    string modSeq = settings.GetModifiedSequence(nodePep, IsotopeLabelType.light);
                    allDocPeptides.Add(new MeasuredPeptide(modSeq, nodePep.SchedulingTime.Value));
                }
            }

            List<MeasuredPeptide> standard;
            //This function raises MessageDlgs
            if(!ValidatePeptideTable(gridViewStandard, STANDARD_TABLE_NAME, out standard))
                return;

            List<MeasuredPeptide> standardDocPeptides;
            int docStandardCount = GetStandardFromDocument(allDocPeptides, standard, out standardDocPeptides);
            if (docStandardCount != standard.Count)
            {
                MessageDlg.Show(this, String.Format(
                        "The active document must contain the entire standard in order to calculate iRT values. Of {0} there were {1}.",
                        standard.Count, docStandardCount));
                return;
            }

            //These have to be sorted by sequence because they are K,V pairs and sequence is the key.
            //They have to be sorted in the first place to calculate the m and b for y=mx+b
            standard.Sort((one, two) => one.Sequence.CompareTo(two.Sequence));
            standardDocPeptides.Sort((one, two) => one.Sequence.CompareTo(two.Sequence));

            Statistics iRtY = new Statistics(standard.Select(pep => pep.RetentionTimeOrIrt));
            Statistics rtX = new Statistics(standardDocPeptides.Select(pep => pep.RetentionTimeOrIrt));
            double slope = Statistics.Slope(iRtY, rtX);
            double intercept = Statistics.Intercept(iRtY, rtX);

            List<MeasuredPeptide> libraryPeptides = new List<MeasuredPeptide>();
            foreach (var pep in allDocPeptides)
            {
                //only calculate new iRTs for peptides not in the standard
                string sequence = pep.Sequence;
                if (!standardDocPeptides.Any(pepStandard => Equals(sequence, pepStandard.Sequence)))
                {
                    double newIrt = slope * pep.RetentionTimeOrIrt + intercept;
                    libraryPeptides.Add(new MeasuredPeptide(sequence, newIrt));
                }
            }

            AddToLibrary(libraryPeptides);

            UpdateNumPeptides();
        }

        #endregion
    }
}
