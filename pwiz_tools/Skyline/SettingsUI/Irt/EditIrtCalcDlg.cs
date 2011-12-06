/*
 * Original author: John Chilton <jchilton .at. uw.edu>,
 *                  Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public partial class EditIrtCalcDlg : Form
    {
        private const string STANDARD_TABLE_NAME = "standard";
        private const string LIBRARY_TABLE_NAME = "library";

        private readonly IEnumerable<RetentionScoreCalculatorSpec> _existingCalcs;

        public RetentionScoreCalculatorSpec Calculator { get; private set; }

        private DbIrtPeptide[] _originalPeptides;
        private readonly StandardGridViewDriver _gridViewStandardDriver;
        private readonly LibraryGridViewDriver _gridViewLibraryDriver;

        private readonly string _databaseStartPath = "";
        
        //Used to determine whether we are creating a new calculator, trying to overwrite
        //an old one, or editing an old one
        private readonly string _editingName = "";

        public EditIrtCalcDlg(RetentionScoreCalculatorSpec calc, IEnumerable<RetentionScoreCalculatorSpec> existingCalcs)
        {
            _existingCalcs = existingCalcs;

            InitializeComponent();

            Icon = Resources.Skyline;

            _gridViewStandardDriver = new StandardGridViewDriver(gridViewStandard, bindingSourceStandard,
                                                                 new SortableBindingList<DbIrtPeptide>());
            _gridViewLibraryDriver = new LibraryGridViewDriver(gridViewLibrary, bindingSourceLibrary,
                                                               new SortableBindingList<DbIrtPeptide>());
            _gridViewStandardDriver.LibraryPeptideList = _gridViewLibraryDriver.Items;
            _gridViewLibraryDriver.StandardPeptideList = _gridViewStandardDriver.Items;

            if (calc != null)
            {
                textCalculatorName.Text = _editingName = calc.Name;
                _databaseStartPath =((RCalcIrt) calc).DatabasePath;

                OpenDatabase(_databaseStartPath);
            }

            DatabaseChanged = false;
        }

        private bool DatabaseChanged { get; set; }

        private BindingList<DbIrtPeptide> StandardPeptideList { get { return _gridViewStandardDriver.Items; } }

        private BindingList<DbIrtPeptide> LibraryPeptideList { get { return _gridViewLibraryDriver.Items; } }

        public IEnumerable<DbIrtPeptide> StandardPeptides
        {
            get { return StandardPeptideList; }
        }

        public IEnumerable<DbIrtPeptide> LibraryPeptides
        {
            get { return LibraryPeptideList; }
        }

        public IEnumerable<DbIrtPeptide> AllPeptides
        {
            get { return new[] {StandardPeptideList, LibraryPeptideList}.SelectMany(list => list); }
        }

        public int StandardPeptideCount { get { return StandardPeptideList.Count; } }
        public int LibraryPeptideCount { get { return LibraryPeptideList.Count; } }

        public void ClearStandardPeptides()
        {
            StandardPeptideList.Clear();
        }

        public void ClearLibraryPeptides()
        {
            LibraryPeptideList.Clear();
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

                    CreateDatabase(dlg.FileName);
                }
            }
        }

        public void CreateDatabase(string path)
        {
            //The file that was just created does not have a schema, so SQLite won't touch it.
            //The file must have a schema or not exist for use with SQLite, so we'll delete
            //it and install a schema

            //Create file, initialize db
            try
            {
                File.Delete(path);
                IrtDb.CreateIrtDb(path);

                textDatabase.Text = path;
            }
            catch (DatabaseOpeningException x)
            {
                MessageDlg.Show(this, x.Message);
            }
            catch (Exception x)
            {
                MessageDlg.Show(this, String.Format("The file {0} could not be created.", path, x));
            }
        }

        private void btnBrowseDb_Click(object sender, EventArgs e)
        {
            if (DatabaseChanged)
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

                    OpenDatabase(dlg.FileName);
                }
            }
        }

        public void OpenDatabase(string path)
        {
            if (!File.Exists(path))
            {
                MessageDlg.Show(this, String.Format("The file {0} does not exist. Click the Create button to create a new database or the Browse button to find the missing file.", path));
                return;
            }

            try
            {
                IrtDb db = IrtDb.GetIrtDb(path, null); // TODO: LongWaitDlg
                var dbPeptides = db.GetPeptides();

                LoadStandard(dbPeptides);
                LoadLibrary(dbPeptides);

                // Clone all of the peptides to use for comparison in OkDialog
                _originalPeptides = dbPeptides.Select(p => new DbIrtPeptide(p)).ToArray();

                textDatabase.Text = path;
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
            if(!ValidatePeptideList(StandardPeptideList, STANDARD_TABLE_NAME))
                return;
            if(!ValidatePeptideList(LibraryPeptideList, LIBRARY_TABLE_NAME))
                return;

            if (StandardPeptideList.Count < CalibrateIrtDlg.MIN_STANDARD_PEPTIDES)
            {
                MessageDlg.Show(this, string.Format("Please enter at least {0} peptides.", CalibrateIrtDlg.MIN_STANDARD_PEPTIDES));
                return;
            }

            if (StandardPeptideList.Count < CalibrateIrtDlg.MIN_SUGGESTED_STANDARD_PEPTIDES)
            {
                DialogResult result = MessageBox.Show(this, string.Format("Using fewer than {0} standard peptides is not recommended. Are you sure you want to continue with only {1}?", CalibrateIrtDlg.MIN_SUGGESTED_STANDARD_PEPTIDES, StandardPeptideList.Count),
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

                db = db.UpdatePeptides(AllPeptides, _originalPeptides ?? new DbIrtPeptide[0]);

                Calculator = calculator.ChangeDatabase(db);
            }
            catch(DatabaseOpeningException x)
            {
                MessageDlg.Show(this, x.Message);
                return;
            }

            DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// At this point a failure in this function probably means the iRT database was used
        /// </summary>
        private bool ValidatePeptideList(IEnumerable<DbIrtPeptide> peptideList, string tableName)
        {
            var sequenceSet = new HashSet<string>();
            foreach(DbIrtPeptide peptide in peptideList)
            {
                string seqModified = peptide.PeptideModSeq;
                // CONSIDER: Select the peptide row
                if (!FastaSequence.IsExSequence(seqModified))
                {
                    MessageDlg.Show(this, string.Format("The value {0} is not a valid modified peptide sequence.", seqModified, tableName));
                    return false;
                }

                if (sequenceSet.Contains(seqModified))
                {
                    MessageDlg.Show(this, string.Format("The peptide {0} appears in the {1} table more than once.", seqModified, tableName));
                    return false;
                }
                sequenceSet.Add(seqModified);
            }

            return true;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void btnCalibrate_Click(object sender, EventArgs e)
        {
            if (LibraryPeptideList.Count == 0)
                Calibrate();
            else
                Recalibrate();
        }

        public void Calibrate()
        {
            using (var calibrateDlg = new CalibrateIrtDlg())
            {
                if (calibrateDlg.ShowDialog(this) == DialogResult.OK)
                {
                    LoadStandard(calibrateDlg.CalibrationPeptides);
                }
            }
        }

        public void Recalibrate()
        {
            using (var recalibrateDlg = new RecalibrateIrtDlg(AllPeptides))
            {
                if (recalibrateDlg.ShowDialog(this) == DialogResult.OK)
                {
                    StandardPeptideList.ResetBindings();
                    LibraryPeptideList.ResetBindings();
                }
            }
        }

        private void LoadStandard(IEnumerable<DbIrtPeptide> standard)
        {
            StandardPeptideList.Clear();
            foreach (var peptide in standard.Where(pep => pep.Standard))
                StandardPeptideList.Add(peptide);
        }

        private void LoadLibrary(IEnumerable<DbIrtPeptide> library)
        {
            LibraryPeptideList.Clear();
            foreach (var peptide in library.Where(pep => !pep.Standard))
                LibraryPeptideList.Add(peptide);
        }

        /// <summary>
        /// If the document contains the standard, this function does a regression of the document standard vs. the
        /// calculator standard and calculates iRTs for all the non-standard peptides
        /// </summary>
        private void btnAddResults_Click(object sender, EventArgs e)
        {
            AddResults();
        }

        private class StandardGridViewDriver : PeptideGridViewDriver<DbIrtPeptide>
        {
            public StandardGridViewDriver(DataGridViewEx gridView, BindingSource bindingSource,
                                          SortableBindingList<DbIrtPeptide> items)
                : base(gridView, bindingSource, items)
            {
                AllowNegativeTime = true;
            }

            public BindingList<DbIrtPeptide> LibraryPeptideList { private get; set; }

            protected override void DoPaste()
            {
                var standardPeptidesNew = new List<DbIrtPeptide>();
                GridView.DoPaste(MessageParent, ValidateRow, values =>
                    standardPeptidesNew.Add(new DbIrtPeptide(values[0], double.Parse(values[1]), true, TimeSource.peak)));

                // Make sure the newly pasted peptides are not present in the library list
                for (int i = 0; i < standardPeptidesNew.Count; i++)
                {
                    var peptide = standardPeptidesNew[i];
                    string sequence = peptide.PeptideModSeq;
                    int iLib = LibraryPeptideList.IndexOf(p => Equals(p.PeptideModSeq, sequence));
                    if (iLib == -1)
                        continue;
                    // Keep the existing peptide, but use the new values
                    var peptideExist = LibraryPeptideList[iLib];
                    LibraryPeptideList.RemoveAt(i);
                    peptideExist.Irt = peptide.Irt;
                    peptideExist.TimeSource = peptide.TimeSource;
                    peptideExist.Standard = true;
                    standardPeptidesNew[i] = peptideExist;
                }

                // Add all standard peptides not included in the new list to the general library list
                foreach (var peptide in from standardPeptide in Items
                                        let sequence = standardPeptide.PeptideModSeq
                                        where !standardPeptidesNew.Any(p => Equals(p.PeptideModSeq, sequence))
                                        select standardPeptide)
                {
                    peptide.Standard = false;
                    LibraryPeptideList.Add(peptide);
                }

                Items.Clear();
                foreach (var peptide in standardPeptidesNew)
                    Items.Add(peptide);
            }
        }

        private class LibraryGridViewDriver : PeptideGridViewDriver<DbIrtPeptide>
        {
            public LibraryGridViewDriver(DataGridViewEx gridView, BindingSource bindingSource,
                                         SortableBindingList<DbIrtPeptide> items)
                : base(gridView, bindingSource, items)
            {
                AllowNegativeTime = true;
            }

            public BindingList<DbIrtPeptide> StandardPeptideList { private get; set; }

            protected override void DoPaste()
            {
                var libraryPeptidesNew = new List<DbIrtPeptide>();
                GridView.DoPaste(MessageParent, ValidateRow, values =>
                    libraryPeptidesNew.Add(new DbIrtPeptide(values[0], double.Parse(values[1]), false, TimeSource.peak)));

                foreach (var peptide in libraryPeptidesNew)
                {
                    string sequence = peptide.PeptideModSeq;
                    if (StandardPeptideList.Any(p => Equals(p.PeptideModSeq, sequence)))
                    {
                        MessageDlg.Show(MessageParent, string.Format("The peptide {0} is already present in the {1} table, and may not be pasted into the {2} table.",
                                                                     sequence, STANDARD_TABLE_NAME, LIBRARY_TABLE_NAME));
                        return;
                    }
                }

                AddToLibrary(libraryPeptidesNew);
            }

            public void AddResults()
            {
                var document = Program.ActiveDocumentUI;
                var settings = document.Settings;
                if (!settings.HasResults)
                {
                    MessageDlg.Show(MessageParent, "The active document must contain results in order to add iRT values.");
                    return;
                }

                // Get retention times for standard in each file in which they have times
                var setStandard = new HashSet<string>(StandardPeptideList.Select(pep => pep.PeptideModSeq));
                var dictStandardTimes = new Dictionary<int, List<MeasuredPeptide>>();
                foreach (var fileInfo in settings.MeasuredResults.MSDataFileInfos)
                {
                    var docPeptides = new List<MeasuredPeptide>();
                    foreach (var nodePep in document.Peptides)
                    {
                        string modSeq = settings.GetModifiedSequence(nodePep, IsotopeLabelType.light);
                        if (!setStandard.Contains(modSeq))
                            continue;
                        float? centerTime = nodePep.GetPeakCenterTime(fileInfo);
                        if (!centerTime.HasValue)
                            break;
                        docPeptides.Add(new MeasuredPeptide(modSeq, centerTime.Value));
                    }
                    if (docPeptides.Count == setStandard.Count)
                    {
                        docPeptides.Sort((p1, p2) => Comparer.Default.Compare(p1.Sequence, p2.Sequence));
                        dictStandardTimes.Add(fileInfo.Id.GlobalIndex, docPeptides);
                    }
                }

                if (dictStandardTimes.Count == 0)
                {
                    MessageDlg.Show(MessageParent, String.Format("The active document must contain the entire standard measured in at least one file in order to calculate iRT values."));
                    return;
                }

                Statistics iRtY = new Statistics(StandardPeptideList.OrderBy(pep => pep.PeptideModSeq).Select(pep => pep.Irt));
                var dictLinearEqs = new Dictionary<int, RegressionLine>();
                foreach (var standardTimes in dictStandardTimes)
                {
                    Statistics rtX = new Statistics(standardTimes.Value.Select(pep => pep.RetentionTime));
                    dictLinearEqs.Add(standardTimes.Key, new RegressionLine(iRtY.Slope(rtX), iRtY.Intercept(rtX)));
                }

                var libraryPeptidesNew = new List<DbIrtPeptide>();
                foreach (var nodePep in document.Peptides)
                {
                    //only calculate new iRTs for peptides not in the standard
                    string modSeq = settings.GetModifiedSequence(nodePep, IsotopeLabelType.light);
                    if (setStandard.Contains(modSeq))
                        continue;
                    double totalIrt = 0;
                    int countIrt = 0;
                    foreach (var fileInfo in settings.MeasuredResults.MSDataFileInfos)
                    {
                        RegressionLine linearEq;
                        if (!dictLinearEqs.TryGetValue(fileInfo.Id.GlobalIndex, out linearEq))
                            continue;
                        float? centerTime = nodePep.GetPeakCenterTime(fileInfo);
                        if (!centerTime.HasValue)
                            continue;

                        totalIrt += linearEq.GetY(centerTime.Value);
                        countIrt++;
                    }

                    if (countIrt > 0)
                        libraryPeptidesNew.Add(new DbIrtPeptide(modSeq, totalIrt / countIrt, false, TimeSource.peak));
                }

                AddToLibrary(libraryPeptidesNew);
            }

            private void AddToLibrary(IEnumerable<DbIrtPeptide> libraryPeptidesNew)
            {
                var dictLibraryIndices = new Dictionary<string, int>();
                for (int i = 0; i < Items.Count; i++)
                    dictLibraryIndices.Add(Items[i].PeptideModSeq, i);

                var listChangedPeptides = new List<string>();
                var listOverwritePeptides = new List<string>();

                // Check for existing matching peptides
                foreach (var peptide in libraryPeptidesNew)
                {
                    int peptideIndex;
                    if (!dictLibraryIndices.TryGetValue(peptide.PeptideModSeq, out peptideIndex))
                        continue;
                    var peptideExist = Items[peptideIndex];
                    if (Equals(peptide, peptideExist))
                        continue;

                    if (peptide.TimeSource != peptideExist.TimeSource &&
                            peptideExist.TimeSource.HasValue &&
                            peptideExist.TimeSource.Value == (int)TimeSource.scan)
                        listOverwritePeptides.Add(peptide.PeptideModSeq);
                    else
                        listChangedPeptides.Add(peptide.PeptideModSeq);
                }

                // If there were any matches, get user feedback
                AddIrtPeptidesAction action = AddIrtPeptidesAction.skip;
                if (listChangedPeptides.Count > 0 || listOverwritePeptides.Count > 0)
                {
                    using (var dlg = new AddIrtPeptidesDlg(listChangedPeptides, listOverwritePeptides))
                    {
                        if (dlg.ShowDialog(MessageParent) != DialogResult.OK)
                            return;
                        action = dlg.Action;
                    }
                }

                // Add the new peptides to the library list
                foreach (var peptide in libraryPeptidesNew)
                {
                    int peptideIndex;
                    if (!dictLibraryIndices.TryGetValue(peptide.PeptideModSeq, out peptideIndex))
                    {
                        Items.Add(peptide);
                        continue;
                    }
                    var peptideExist = Items[peptideIndex];
                    if (action == AddIrtPeptidesAction.skip || Equals(peptide, peptideExist))
                        continue;

                    if (action == AddIrtPeptidesAction.replace ||
                            (peptide.TimeSource != peptideExist.TimeSource &&
                             peptideExist.TimeSource.HasValue &&
                             peptideExist.TimeSource.Value == (int)TimeSource.scan))
                    {
                        peptideExist.Irt = peptide.Irt;
                        peptideExist.TimeSource = peptide.TimeSource;
                    }
                    else // if (action == AddIrtPeptidesAction.average)
                    {
                        peptideExist.Irt = (peptide.Irt + peptideExist.Irt) / 2;
                    }
                    Items.ResetItem(peptideIndex);
                }
            }
        }

        private void gridViewLibrary_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            UpdateNumPeptides();
        }

        private void gridViewLibrary_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            UpdateNumPeptides();
        }

        private void UpdateNumPeptides()
        {
            btnCalibrate.Text = (LibraryPeptideList.Count == 0 ? "Cali&brate..." : "Recali&brate...");

            labelNumPeptides.Text = string.Format("{0} Peptides", LibraryPeptideList.Count);
        }

        #region Functional Test Support

        public void SetCalcName(string name)
        {
            textCalculatorName.Text = name;
        }

        public void DoPasteStandard()
        {
            _gridViewStandardDriver.OnPaste();
        }

        public void AddResults()
        {
            _gridViewLibraryDriver.AddResults();
        }

        #endregion
    }
}