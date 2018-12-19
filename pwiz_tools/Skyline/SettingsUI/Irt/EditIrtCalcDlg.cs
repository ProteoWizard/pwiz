﻿/*
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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using NHibernate;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.ChromLib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public partial class EditIrtCalcDlg : FormEx
    {
        //private const string STANDARD_TABLE_NAME = "standard";
        //private const string LIBRARY_TABLE_NAME = "library";

        private const double IRT_TOLERANCE = 0.01;

        private readonly IEnumerable<RetentionScoreCalculatorSpec> _existingCalcs;

        public RetentionScoreCalculatorSpec Calculator { get; private set; }

        private DbIrtPeptide[] _originalPeptides;
        private DbIrtPeptide[] _originalKnownPeptides;
        private readonly StandardGridViewDriver _gridViewStandardDriver;
        private readonly LibraryGridViewDriver _gridViewLibraryDriver;

        //Used to determine whether we are creating a new calculator, trying to overwrite
        //an old one, or editing an old one
        private readonly string _editingName = string.Empty;

        public EditIrtCalcDlg(RCalcIrt calc, IEnumerable<RetentionScoreCalculatorSpec> existingCalcs)
        {
            _existingCalcs = existingCalcs;

            InitializeComponent();

            Icon = Resources.Skyline;

            _gridViewStandardDriver = new StandardGridViewDriver(this, gridViewStandard, bindingSourceStandard,
                                                                 new SortableBindingList<DbIrtPeptide>());
            _gridViewLibraryDriver = new LibraryGridViewDriver(gridViewLibrary, bindingSourceLibrary,
                                                               new SortableBindingList<DbIrtPeptide>());
            _gridViewStandardDriver.GridLibrary = gridViewLibrary;
            _gridViewStandardDriver.LibraryPeptideList = _gridViewLibraryDriver.Items;
            _gridViewLibraryDriver.StandardPeptideList = _gridViewStandardDriver.Items;

            foreach (var standard in IrtStandard.ALL)
                comboStandards.Items.Add(standard);

            if (calc != null)
            {
                textCalculatorName.Text = _editingName = calc.Name;
                string databaseStartPath = calc.DatabasePath;

                OpenDatabase(databaseStartPath);
            }
        }

        private void OnLoad(object sender, EventArgs e)
        {
            // If you set this in the Designer, DataGridView has a defect that causes it to throw an
            // exception if the the cursor is positioned over the record selector column during loading.
            gridViewStandard.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridViewLibrary.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private bool DatabaseChanged
        {
            get
            {
                if (_originalPeptides == null)
                    return AllPeptides.Any();

                var dictOriginalPeptides = _originalPeptides.ToDictionary(pep => pep.Id);
                long countPeptides = 0;
                foreach (var peptide in AllPeptides)
                {
                    countPeptides++;

                    // Any new peptide implies a change
                    if (!peptide.Id.HasValue)
                        return true;
                    // Any peptide that was not in the original set, or that has changed
                    DbIrtPeptide originalPeptide;
                    if (!dictOriginalPeptides.TryGetValue(peptide.Id, out originalPeptide) ||
                            !Equals(peptide, originalPeptide))
                        return true;
                }
                // Finally, check for peptides removed
                return countPeptides != _originalPeptides.Length;
            }
        }

        private BindingList<DbIrtPeptide> StandardPeptideList { get { return _gridViewStandardDriver.Items; } }

        private BindingList<DbIrtPeptide> LibraryPeptideList { get { return _gridViewLibraryDriver.Items; } }

        private IrtStandard CurrentStandard
        {
            get
            {
                return comboStandards.Items.Cast<IrtStandard>().FirstOrDefault(standard => standard.IsMatch(StandardPeptideList, IRT_TOLERANCE))
                    ?? IrtStandard.EMPTY;
            }
        }

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
            if (DatabaseChanged)
            {
                var result = MessageBox.Show(this, Resources.EditIrtCalcDlg_btnCreateDb_Click_Are_you_sure_you_want_to_create_a_new_database_file_Any_changes_to_the_current_calculator_will_be_lost,
                    Program.Name, MessageBoxButtons.YesNo);

                if (result != DialogResult.Yes)
                    return;
            }

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

                    CreateDatabase(dlg.FileName);
                    textDatabase.Focus();
                }
            }
        }

        public void CreateDatabase(string path)
        {
            //The file that was just created does not have a schema, so SQLite won't touch it.
            //The file must have a schema or not exist for use with SQLite, so we'll delete
            //it and install a schema

            try
            {
                FileEx.SafeDelete(path);
            }
            catch (IOException x)
            {
                MessageDlg.Show(this, x.Message);
                return;
            }

            //Create file, initialize db
            try
            {
                IrtDb.CreateIrtDb(path);

                textDatabase.Text = path;
            }
            catch (DatabaseOpeningException x)
            {
                MessageDlg.Show(this, x.Message);
            }
            catch (Exception x)
            {
                var message = TextUtil.LineSeparate(string.Format(Resources.EditIrtCalcDlg_CreateDatabase_The_file__0__could_not_be_created, path),
                                                    x.Message);
                MessageDlg.Show(this, message);
            }
        }

        private void btnBrowseDb_Click(object sender, EventArgs e)
        {
            if (DatabaseChanged)
            {
                var result = MessageBox.Show(this, Resources.EditIrtCalcDlg_btnBrowseDb_Click_Are_you_sure_you_want_to_open_a_new_database_file_Any_changes_to_the_current_calculator_will_be_lost,
                    Program.Name, MessageBoxButtons.YesNo);

                if (result != DialogResult.Yes)
                    return;
            }

            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Resources.EditIrtCalcDlg_btnBrowseDb_Click_Open_iRT_Database,
                InitialDirectory = Settings.Default.ActiveDirectory,
                DefaultExt = IrtDb.EXT,
                Filter = TextUtil.FileDialogFiltersAll(IrtDb.FILTER_IRTDB, BiblioSpecLiteSpec.FILTER_BLIB, ChromatogramLibrarySpec.FILTER_CLIB)
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);

                    OpenDatabase(dlg.FileName);
                    textDatabase.Focus();
                }
            }
        }

        public void OpenDatabase(string path)
        {
            if (!File.Exists(path))
            {
                MessageDlg.Show(this, String.Format(Resources.EditIrtCalcDlg_OpenDatabase_The_file__0__does_not_exist__Click_the_Create_button_to_create_a_new_database_or_the_Open_button_to_find_the_missing_file_,
                                                    path));
                return;
            }

            try
            {
                IList<DbIrtPeptide> dbPeptides;
                IrtDb.GetIrtDb(path, null, out dbPeptides); // TODO: LongWaitDlg

                LoadStandard(dbPeptides);
                LoadLibrary(dbPeptides);

                // Clone all of the peptides to use for comparison in OkDialog
                _originalPeptides = dbPeptides.Select(p => new DbIrtPeptide(p)).ToArray();
                _originalKnownPeptides = _originalPeptides.Where(p => IrtStandard.AnyContains(p, IRT_TOLERANCE)).ToArray();

                textDatabase.Text = path;
            }
            catch (DatabaseOpeningException e)
            {
                MessageDlg.Show(this, e.Message);
            }
        }

        public void OkDialog()
        {
            if(string.IsNullOrEmpty(textCalculatorName.Text))
            {
                MessageDlg.Show(this, Resources.EditIrtCalcDlg_OkDialog_Please_enter_a_name_for_the_iRT_calculator);
                textCalculatorName.Focus();
                return;
            }

            if (_existingCalcs != null)
            {
                foreach (var existingCalc in _existingCalcs)
                {
                    if (Equals(existingCalc.Name, textCalculatorName.Text) && !Equals(existingCalc.Name, _editingName))
                    {
                        if (MessageBox.Show(this, string.Format(Resources.EditIrtCalcDlg_OkDialog_A_calculator_with_the_name__0__already_exists_Do_you_want_to_overwrite_it,
                                                                textCalculatorName.Text),
                                            Program.Name, MessageBoxButtons.YesNo) != DialogResult.Yes)
                        {
                            textCalculatorName.Focus();
                            return;
                        }
                    }
                }
            }

            string message;
            if (string.IsNullOrEmpty(textDatabase.Text))
            {
                message = TextUtil.LineSeparate(Resources.EditIrtCalcDlg_OkDialog_Please_choose_a_database_file_for_the_iRT_calculator,
                                                Resources.EditIrtCalcDlg_OkDialog_Click_the_Create_button_to_create_a_new_database_or_the_Open_button_to_open_an_existing_database_file);
                MessageDlg.Show(this, message);
                textDatabase.Focus();
                return;
            }
            string path = Path.GetFullPath(textDatabase.Text);
            if (!Equals(path, textDatabase.Text))
            {
                message = TextUtil.LineSeparate(Resources.EditIrtCalcDlg_OkDialog_Please_use_a_full_path_to_a_database_file_for_the_iRT_calculator,
                                                Resources.EditIrtCalcDlg_OkDialog_Click_the_Create_button_to_create_a_new_database_or_the_Open_button_to_open_an_existing_database_file);
                MessageDlg.Show(this, message);
                textDatabase.Focus();
                return;
            }
            string ext = Path.GetExtension(path);
            bool chromLib = string.Equals(ext, ChromatogramLibrarySpec.EXT);
            bool specLib = string.Equals(ext, BiblioSpecLiteSpec.EXT);
            if ((chromLib || specLib) && DatabaseChanged)
            {
                string pathNew;
                do
                {
                    MessageDlg.Show(this, chromLib
                        ? Resources.EditIrtCalcDlg_OkDialog_Chromatogram_libraries_cannot_be_modified__You_must_save_this_iRT_calculator_as_a_new_file_
                        : Resources.EditIrtCalcDlg_OkDialog_Spectral_libraries_cannot_be_modified__You_must_save_this_iRT_calculator_as_a_new_file_);
                    using (var saveDlg = new SaveFileDialog {Filter = IrtDb.FILTER_IRTDB})
                    {
                        if (saveDlg.ShowDialog(this) == DialogResult.Cancel)
                        {
                            return;
                        }
                        pathNew = saveDlg.FileName;
                    }
                } while (string.Equals(path, pathNew));
                path = pathNew;
            }
            if (!string.Equals(Path.GetExtension(path), IrtDb.EXT) && !((chromLib || specLib) && !DatabaseChanged))
                path += IrtDb.EXT;

            //This function MessageBox.Show's error messages
            if (!ValidatePeptideList(StandardPeptideList, Resources.EditIrtCalcDlg_OkDialog_standard_table_name))
            {
                gridViewStandard.Focus();
                return;
            }
            if(!ValidatePeptideList(LibraryPeptideList, Resources.EditIrtCalcDlg_OkDialog_library_table_name))
            {
                gridViewLibrary.Focus();
                return;                
            }

            if (StandardPeptideList.Count < CalibrateIrtDlg.MIN_STANDARD_PEPTIDES)
            {
                MessageDlg.Show(this, string.Format(Resources.EditIrtCalcDlg_OkDialog_Please_enter_at_least__0__standard_peptides,
                                                    CalibrateIrtDlg.MIN_STANDARD_PEPTIDES));
                gridViewStandard.Focus();
                return;
            }

            if (StandardPeptideList.Count < CalibrateIrtDlg.MIN_SUGGESTED_STANDARD_PEPTIDES)
            {
                string messageTooFewPeptides = string.Format(Resources
                    .EditIrtCalcDlg_OkDialog_Using_fewer_than__0__standard_peptides_is_not_recommended_Are_you_sure_you_want_to_continue_with_only__1__,
                    CalibrateIrtDlg.MIN_SUGGESTED_STANDARD_PEPTIDES, StandardPeptideList.Count);

                DialogResult result = MultiButtonMsgDlg.Show(this, messageTooFewPeptides, MultiButtonMsgDlg.BUTTON_YES,
                    MultiButtonMsgDlg.BUTTON_NO, false);
                if (result != DialogResult.Yes)
                {
                    gridViewStandard.Focus();
                    return;
                }
            }

            try
            {
                if (DatabaseChanged)
                {
                    using (FileSaver fileSaver = new FileSaver(path))
                    {
                        IrtDb db = IrtDb.CreateIrtDb(fileSaver.SafeName);
                        db.AddPeptides(null, AllPeptides.ToArray());
                        fileSaver.Commit();
                    }
                }
                Calculator = new RCalcIrt(textCalculatorName.Text, path).ChangeDatabase(IrtDb.GetIrtDb(path, null));
            }
            catch (DatabaseOpeningException x)
            {
                MessageDlg.Show(this, x.Message);
                textDatabase.Focus();
                return;
            }
            catch (StaleStateException)
            {
                // CONSIDER: Not sure if this is the right thing to do.  It would
                //           be nice to solve whatever is causing this, but this is
                //           better than showing an unexpected error form with stack trace.
                MessageDlg.Show(this, Resources.EditIrtCalcDlg_OkDialog_Failure_updating_peptides_in_the_iRT_database___The_database_may_be_out_of_synch_);
                return;
            }

            DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// At this point a failure in this function probably means the iRT database was used
        /// </summary>
        private bool ValidatePeptideList(IEnumerable<DbIrtPeptide> peptideList, string tableName)
        {
            var sequenceSet = new HashSet<Target>();
            foreach(DbIrtPeptide peptide in peptideList)
            {
                var seqModified = peptide.ModifiedTarget;
                // CONSIDER: Select the peptide row
                if (seqModified.IsProteomic && !FastaSequence.IsExSequence(seqModified.Sequence))
                {
                    MessageDlg.Show(this, string.Format(Resources.EditIrtCalcDlg_ValidatePeptideList_The_value__0__is_not_a_valid_modified_peptide_sequence,
                                                        seqModified));
                    return false;
                }

                if (sequenceSet.Contains(seqModified))
                {
                    MessageDlg.Show(this, string.Format(Resources.EditIrtCalcDlg_ValidatePeptideList_The_peptide__0__appears_in_the__1__table_more_than_once,
                                                        seqModified, tableName));
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

        private void btnCalibrate_Click(object sender, EventArgs e)
        {
            Calibrate();
        }

        public void Calibrate()
        {
            CheckDisposed();
            if (LibraryPeptideList.Count == 0)
                CalibrateOnce();
            else
                Recalibrate();
        }

        private void CalibrateOnce()
        {
            using (var calibrateDlg = new CalibrateIrtDlg())
            {
                if (calibrateDlg.ShowDialog(this) == DialogResult.OK)
                {
                    LoadStandard(calibrateDlg.CalibrationPeptides);
                }
            }
        }

        private void Recalibrate()
        {
            using (var recalibrateDlg = new RecalibrateIrtDlg(AllPeptides.ToArray()))
            {
                if (recalibrateDlg.ShowDialog(this) == DialogResult.OK)
                {
                    StandardPeptideList.ResetBindings();
                    LibraryPeptideList.ResetBindings();
                }
            }
        }

        private void btnPeptides_Click(object sender, EventArgs e)
        {
            ChangeStandardPeptides();
        }

        public void ChangeStandardPeptides()
        {
            using (var changeDlg = new ChangeIrtPeptidesDlg(AllPeptides.ToArray()))
            {
                if (changeDlg.ShowDialog(this) == DialogResult.OK)
                {
                    _gridViewStandardDriver.Reset(changeDlg.Peptides.OrderBy(peptide => peptide.Irt).ToArray());
                }
            }
        }

        private void LoadStandard(IEnumerable<DbIrtPeptide> standard)
        {
            ReplaceItems(StandardPeptideList, standard.Where(pep=>pep.Standard).Select(pep=>new DbIrtPeptide(pep)));
        }

        private void LoadLibrary(IEnumerable<DbIrtPeptide> library)
        {
            ReplaceItems(LibraryPeptideList, library.Where(pep=>!pep.Standard).Select(pep=>new DbIrtPeptide(pep)));
        }

        /// <summary>
        /// If the document contains the standard, this function does a regression of the document standard vs. the
        /// calculator standard and calculates iRTs for all the non-standard peptides
        /// </summary>
        private void btnAddResults_Click(object sender, EventArgs e)
        {
            if (StandardPeptideCount < CalibrateIrtDlg.MIN_STANDARD_PEPTIDES)
            {
                MessageDlg.Show(this, string.Format(Resources.EditIrtCalcDlg_OkDialog_Please_enter_at_least__0__standard_peptides,
                                                    CalibrateIrtDlg.MIN_STANDARD_PEPTIDES));
                return;
            }
            contextMenuAdd.Show(btnAddResults, 0, btnAddResults.Height + 2);
        }

        private void addResultsContextMenuItem_Click(object sender, EventArgs e)
        {
            AddResults();
        }

        public void AddResults()
        {
            _gridViewLibraryDriver.AddResults();
        }

        private void addSpectralLibraryContextMenuItem_Click(object sender, EventArgs e)
        {
            AddLibrary();
        }

        public void AddLibrary()
        {
            CheckDisposed();
            _gridViewLibraryDriver.AddSpectralLibrary();
        }

        private void addIRTDatabaseContextMenuItem_Click(object sender, EventArgs e)
        {
            AddIrtDatabase();
        }

        public void AddIrtDatabase()
        {
            _gridViewLibraryDriver.AddIrtDatabase();
        }

        private void ReplaceItems<T>(BindingList<T> bindingList, IEnumerable<T> newItems)
        {
            bool raiseEventsOld = bindingList.RaiseListChangedEvents;
            try
            {
                bindingList.RaiseListChangedEvents = false;
                bindingList.Clear();
                foreach (var item in newItems)
                {
                    bindingList.Add(item);
                }
            }
            finally
            {
                bindingList.RaiseListChangedEvents = raiseEventsOld;
            }
            bindingList.ResetBindings();
        }

        private class StandardGridViewDriver : PeptideGridViewDriver<DbIrtPeptide>
        {
            public StandardGridViewDriver(EditIrtCalcDlg parent, DataGridViewEx gridView, BindingSource bindingSource,
                SortableBindingList<DbIrtPeptide> items)
                : base(gridView, bindingSource, items)
            {
                AllowNegativeTime = true;
                GridView.CellValueChanged += parent.HandleStandardsChanged;
                Items.ListChanged += parent.HandleStandardsChanged;
            }

            public DataGridView GridLibrary { private get; set; }

            /// <summary>
            /// The associated library peptide list, set in the dialog constructor
            /// </summary>
            public BindingList<DbIrtPeptide> LibraryPeptideList { private get; set; }

            protected override void DoPaste()
            {
                var standardPeptidesNew = new List<DbIrtPeptide>();
                GridView.DoPaste(MessageParent, ValidateRowWithIrt, values =>
                    standardPeptidesNew.Add(new DbIrtPeptide(new Target(values[0]), double.Parse(values[1]), true, TimeSource.peak)));

                Reset(standardPeptidesNew);
            }

            public void Reset(IList<DbIrtPeptide> standardPeptidesNew)
            {
                // Selection in the library grid can cause exceptions
                if (LibraryPeptideList.Count > 0)
                {
                    GridLibrary.ClearSelection();
                    GridLibrary.CurrentCell = GridLibrary.Rows[0].Cells[0];
                }

                // Make sure to use existing peptides where possible
                for (int i = 0; i < standardPeptidesNew.Count; i++)
                {
                    var peptide = standardPeptidesNew[i];
                    var sequence = peptide.ModifiedTarget;
                    DbIrtPeptide peptideExist;
                    int iPep;
                    if ((iPep = LibraryPeptideList.IndexOf(p => Equals(p.ModifiedTarget, sequence))) != -1)
                    {
                        peptideExist = new DbIrtPeptide(LibraryPeptideList[iPep]);
                        // Remove from the library list, so that it is in only one list
                        LibraryPeptideList.RemoveAt(iPep);
                    }
                    else if ((iPep = Items.IndexOf(p => Equals(p.ModifiedTarget, sequence))) != -1)
                    {
                        peptideExist = new DbIrtPeptide(Items[iPep]);
                    }
                    else
                    {
                        continue;
                    }

                    // Keep the existing peptide, but use the new values
                    peptideExist.Irt = peptide.Irt;
                    peptideExist.TimeSource = peptide.TimeSource;
                    peptideExist.Standard = true;
                    standardPeptidesNew[i] = peptideExist;
                }

                // Add all standard peptides not included in the new list to the general library list
                foreach (var peptide in from standardPeptide in Items
                                        let sequence = standardPeptide.ModifiedTarget
                                        where sequence != null &&
                                            !standardPeptidesNew.Any(p => Equals(p.ModifiedTarget, sequence))
                                        select standardPeptide)
                {
                    peptide.Standard = false;
                    LibraryPeptideList.Add(new DbIrtPeptide(peptide));
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

            /// <summary>
            /// The associated standard peptide list, set in the dialog constructor
            /// </summary>
            public BindingList<DbIrtPeptide> StandardPeptideList { private get; set; }

            protected override void DoPaste()
            {
                var libraryPeptidesNew = new List<DbIrtPeptide>();
                if (!GridView.DoPaste(MessageParent, ValidateRowWithIrt, values => libraryPeptidesNew.Add(new DbIrtPeptide(new Target(values[0]), double.Parse(values[1]), false, TimeSource.peak))))
                    return;

                foreach (var peptide in libraryPeptidesNew)
                {
                    var sequence = peptide.ModifiedTarget;
                    if (StandardPeptideList.Any(p => Equals(p.ModifiedTarget, sequence)))
                    {
                        MessageDlg.Show(MessageParent,
                                        string.Format(Resources.LibraryGridViewDriver_DoPaste_The_peptide__0__is_already_present_in_the__1__table__and_may_not_be_pasted_into_the__2__table,
                                                      sequence,
                                                      Resources.EditIrtCalcDlg_OkDialog_standard_table_name,
                                                      Resources.EditIrtCalcDlg_OkDialog_library_table_name));
                        return;
                    }
                }

                AddToLibrary(new ProcessedIrtAverages(
                    libraryPeptidesNew.ToDictionary(pep => pep.ModifiedTarget, pep => new IrtPeptideAverages(pep.ModifiedTarget, pep.Irt, TimeSource.peak)),
                    new KeyValuePair<string, RetentionTimeProviderData>[0]));
            }

            public void AddResults()
            {
                var document = Program.ActiveDocumentUI;
                var settings = document.Settings;
                if (!settings.HasResults)
                {
                    MessageDlg.Show(MessageParent, Resources.LibraryGridViewDriver_AddResults_The_active_document_must_contain_results_in_order_to_add_iRT_values);
                    return;
                }

                ProcessedIrtAverages irtAverages = null;
                using (var longWait = new LongWaitDlg
                {
                    Text = Resources.LibraryGridViewDriver_AddResults_Adding_Results,
                    Message = Resources.LibraryGridViewDriver_AddResults_Adding_retention_times_from_imported_results,
                    FormBorderStyle = FormBorderStyle.Sizable
                })
                {
                    try
                    {
                        var status = longWait.PerformWork(MessageParent, 800, monitor =>
                            irtAverages = ProcessRetentionTimes(monitor,
                                              GetRetentionTimeProviders(document),
                                              document.Settings.MeasuredResults.MSDataFileInfos.Count()));
                        if (status.IsError)
                        {
                            MessageBox.Show(MessageParent, status.ErrorException.Message, Program.Name);
                            return;
                        }
                    }
                    catch (Exception x)
                    {
                        var message = TextUtil.LineSeparate(Resources.LibraryGridViewDriver_AddResults_An_error_occurred_attempting_to_add_results_from_current_document,
                                                            x.Message);
                        MessageDlg.Show(MessageParent, message);
                        return;
                    }
                }

                AddToLibrary(irtAverages);
            }

            private static IEnumerable<IRetentionTimeProvider> GetRetentionTimeProviders(SrmDocument document)
            {
                return document.Settings.MeasuredResults.MSDataFileInfos.Select(fileInfo =>
                            new DocumentRetentionTimeProvider(document, fileInfo));
            }

            private sealed class DocumentRetentionTimeProvider : IRetentionTimeProvider
            {
                private readonly Dictionary<Target, double> _dictPeptideRetentionTime;

                public DocumentRetentionTimeProvider(SrmDocument document, ChromFileInfo fileInfo)
                {
                    Name = fileInfo.FilePath.ToString();

                    _dictPeptideRetentionTime = new Dictionary<Target, double>();
                    foreach (var nodePep in document.Peptides)
                    {
                        var modSeq = document.Settings.GetModifiedSequence(nodePep);
                        if (_dictPeptideRetentionTime.ContainsKey(modSeq))
                            continue;
                        float? centerTime = nodePep.GetSchedulingTime(fileInfo.FileId);
                        if (!centerTime.HasValue)
                            continue;
                        _dictPeptideRetentionTime.Add(modSeq, centerTime.Value);
                    }

                }

                public string Name { get; private set; }

                public double? GetRetentionTime(Target sequence)
                {
                    double time;
                    if (_dictPeptideRetentionTime.TryGetValue(sequence, out time))
                        return time;
                    return null;
                }

                public TimeSource? GetTimeSource(Target sequence)
                {
                    return TimeSource.peak;
                }

                public IEnumerable<MeasuredRetentionTime> PeptideRetentionTimes
                {
                    get
                    {
                        return _dictPeptideRetentionTime.Select(pepTime =>
                            new MeasuredRetentionTime(pepTime.Key, pepTime.Value));
                    }
                }
            }

            public void AddSpectralLibrary()
            {
                using (var addLibraryDlg = new AddIrtSpectralLibrary(Settings.Default.SpectralLibraryList))
                {
                    if (addLibraryDlg.ShowDialog(MessageParent) == DialogResult.OK)
                    {
                        AddSpectralLibrary(addLibraryDlg.Library);
                    }
                }
            }

            private void AddSpectralLibrary(LibrarySpec librarySpec)
            {
                var libraryManager = ((ILibraryBuildNotificationContainer)Program.MainWindow).LibraryManager;
                Library library = null;
                ProcessedIrtAverages irtAverages = null;
                try
                {
                    library = libraryManager.TryGetLibrary(librarySpec);
                    using (var longWait = new LongWaitDlg
                    {
                        Text = Resources.LibraryGridViewDriver_AddSpectralLibrary_Adding_Spectral_Library,
                        Message = string.Format(Resources.LibraryGridViewDriver_AddSpectralLibrary_Adding_retention_times_from__0__, librarySpec.FilePath),
                        FormBorderStyle = FormBorderStyle.Sizable
                    })
                    {
                        try
                        {
                            var status = longWait.PerformWork(MessageParent, 800, monitor =>
                            {
                                if (library == null)
                                    library = librarySpec.LoadLibrary(new DefaultFileLoadMonitor(monitor));

                                var irtProvider = library.RetentionTimeProvidersIrt.ToArray();
                                if (irtProvider.Any())
                                {
                                    irtAverages = ProcessRetentionTimes(monitor, irtProvider, 1);
                                }
                                else
                                {
                                    int fileCount = library.FileCount ?? 0;
                                    if (fileCount == 0)
                                    {
                                        string message = string.Format(Resources.LibraryGridViewDriver_AddSpectralLibrary_The_library__0__does_not_contain_retention_time_information,
                                                                       librarySpec.FilePath);
                                        monitor.UpdateProgress(new ProgressStatus(string.Empty).ChangeErrorException(new IOException(message)));
                                        return;
                                    }

                                    irtAverages = ProcessRetentionTimes(monitor, library.RetentionTimeProviders, fileCount);
                                }
                            });
                            if (status.IsError)
                            {
                                MessageBox.Show(MessageParent, status.ErrorException.Message, Program.Name);
                                return;
                            }
                        }
                        catch (Exception x)
                        {
                            var message = TextUtil.LineSeparate(string.Format(Resources.LibraryGridViewDriver_AddSpectralLibrary_An_error_occurred_attempting_to_load_the_library_file__0__,
                                                                              librarySpec.FilePath),
                                                                x.Message);
                            MessageDlg.Show(MessageParent, message);
                            return;
                        }
                    }
                }
                finally
                {
                    if (library != null)
                    {
                        foreach (var pooledStream in library.ReadStreams)
                            pooledStream.CloseStream();
                    }
                }

                AddToLibrary(irtAverages);
            }

            public void AddIrtDatabase()
            {
                var irtCalcs = Settings.Default.RTScoreCalculatorList.Where(calc => calc is RCalcIrt).Cast<RCalcIrt>();
                using (var addIrtCalculatorDlg = new AddIrtCalculatorDlg(irtCalcs))
                {
                    if (addIrtCalculatorDlg.ShowDialog(MessageParent) == DialogResult.OK)
                    {
                        AddIrtDatabase(addIrtCalculatorDlg.Calculator);
                    }
                }                
            }

            private void AddIrtDatabase(RCalcIrt irtCalc)
            {
                ProcessedIrtAverages irtAverages = null;
                using (var longWait = new LongWaitDlg
                {
                    Text = Resources.LibraryGridViewDriver_AddIrtDatabase_Adding_iRT_Database,
                    Message = string.Format(Resources.LibraryGridViewDriver_AddSpectralLibrary_Adding_retention_times_from__0__, irtCalc.DatabasePath),
                    FormBorderStyle = FormBorderStyle.Sizable
                })
                {
                    try
                    {
                        var status = longWait.PerformWork(MessageParent, 800, monitor =>
                        {
                            var irtDb = IrtDb.GetIrtDb(irtCalc.DatabasePath, monitor);

                            irtAverages = ProcessRetentionTimes(monitor,
                                new[] { new IrtRetentionTimeProvider(!irtCalc.Name.Equals(AddIrtCalculatorDlg.DEFAULT_NAME) ? irtCalc.Name : Path.GetFileName(irtCalc.DatabasePath), irtDb) }, 1);
                        });
                        if (status.IsError)
                        {
                            MessageBox.Show(MessageParent, status.ErrorException.Message, Program.Name);
                            return;
                        }
                    }
                    catch (Exception x)
                    {
                        var message = TextUtil.LineSeparate(string.Format(Resources.LibraryGridViewDriver_AddIrtDatabase_An_error_occurred_attempting_to_load_the_iRT_database_file__0__,
                                                                          irtCalc.DatabasePath),
                                                            x.Message);
                        MessageDlg.Show(MessageParent, message);
                        return;
                    }
                }

                AddToLibrary(irtAverages);
            }

            private sealed class IrtRetentionTimeProvider : IRetentionTimeProvider
            {
                private readonly string _name;
                private readonly Dictionary<Target, DbIrtPeptide> _dictSequenceToPeptide;

                public IrtRetentionTimeProvider(string name, IrtDb irtDb)
                {
                    _name = name;
                    _dictSequenceToPeptide = irtDb.GetPeptides().ToDictionary(peptide => peptide.ModifiedTarget);
                }

                public string Name
                {
                    get { return _name; }
                }

                public double? GetRetentionTime(Target sequence)
                {
                    DbIrtPeptide peptide;
                    if (_dictSequenceToPeptide.TryGetValue(sequence, out peptide))
                        return peptide.Irt;
                    return null;
                }

                public TimeSource? GetTimeSource(Target sequence)
                {
                    DbIrtPeptide peptide;
                    if (_dictSequenceToPeptide.TryGetValue(sequence, out peptide))
                        return (TimeSource?) peptide.TimeSource;
                    return null;
                }

                public IEnumerable<MeasuredRetentionTime> PeptideRetentionTimes
                {
                    get { return _dictSequenceToPeptide.Select(p => new MeasuredRetentionTime(p.Key, p.Value.Irt, true)); }
                }
            }

            private ProcessedIrtAverages ProcessRetentionTimes(IProgressMonitor monitor,
                                          IEnumerable<IRetentionTimeProvider> providers,
                                          int countProviders)
            {
                return RCalcIrt.ProcessRetentionTimes(monitor, providers, countProviders, StandardPeptideList.ToArray(), Items.ToArray());
            }

            private void AddToLibrary(ProcessedIrtAverages irtAverages)
            {
                if (irtAverages == null)
                    return; // Canceled

                var listPeptidesNew = irtAverages.DbIrtPeptides.ToList();
                LibKeyMap<int> dictLibraryIndices;
                List<Target> listChangedPeptides, listOverwritePeptides, listKeepPeptides;
                GetPeptideLists(listPeptidesNew, out dictLibraryIndices, out listChangedPeptides, out listOverwritePeptides, out listKeepPeptides);

                // If there were any matches, get user feedback
                AddIrtPeptidesAction action;
                using (var dlg = new AddIrtPeptidesDlg(AddIrtPeptidesLocation.irt_database,
                                                       irtAverages,
                                                       listChangedPeptides,
                                                       listOverwritePeptides,
                                                       listKeepPeptides))
                {
                    if (dlg.ShowDialog(MessageParent) != DialogResult.OK)
                        return;
                    action = dlg.Action;
                }

                List<DbIrtPeptide> newStandards = null;
                if (irtAverages.CanRecalibrateStandards(StandardPeptideList))
                {
                    using (var dlg = new MultiButtonMsgDlg(
                        TextUtil.LineSeparate(Resources.LibraryGridViewDriver_AddToLibrary_Do_you_want_to_recalibrate_the_iRT_standard_values_relative_to_the_peptides_being_added_,
                            Resources.LibraryGridViewDriver_AddToLibrary_This_can_improve_retention_time_alignment_under_stable_chromatographic_conditions_),
                        MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, true))
                    {
                        switch (dlg.ShowDialog(MessageParent))
                        {
                            case DialogResult.Cancel:
                                return;
                            case DialogResult.Yes:
                                using (var longWait = new LongWaitDlg
                                {
                                    Text = Resources.LibraryGridViewDriver_AddToLibrary_Recalibrate_iRT_Standard_Peptides,
                                    Message = Resources.LibraryGridViewDriver_AddToLibrary_Recalibrating_iRT_standard_peptides_and_reprocessing_iRT_values
                                })
                                {
                                    try
                                    {
                                        newStandards = irtAverages.RecalibrateStandards(StandardPeptideList);
                                        var status = longWait.PerformWork(MessageParent, 800, monitor => irtAverages = RCalcIrt.ProcessRetentionTimes(
                                            monitor, irtAverages.ProviderData.Select(data => data.Value.RetentionTimeProvider),
                                            irtAverages.ProviderData.Count, newStandards.ToArray(), Items.ToArray()));
                                        if (status.IsError)
                                        {
                                            MessageDlg.ShowWithException(MessageParent, Resources.LibraryGridViewDriver_AddToLibrary_An_error_occurred_while_recalibrating_, status.ErrorException);
                                            return;
                                        }
                                    }
                                    catch (Exception x)
                                    {
                                        var message = TextUtil.LineSeparate(Resources.LibraryGridViewDriver_AddToLibrary_An_error_occurred_while_recalibrating_, x.Message);
                                        MessageDlg.Show(MessageParent, message);
                                        return;
                                    }
                                }
                                break;
                        }
                    }
                }

                if (newStandards != null)
                {
                    listPeptidesNew = irtAverages.DbIrtPeptides.ToList();
                    GetPeptideLists(listPeptidesNew, out dictLibraryIndices, out listChangedPeptides, out listOverwritePeptides, out listKeepPeptides);

                    StandardPeptideList.RaiseListChangedEvents = false;
                    StandardPeptideList.Clear();
                    foreach (var newStandard in newStandards)
                        StandardPeptideList.Add(newStandard);
                    StandardPeptideList.RaiseListChangedEvents = true;
                    StandardPeptideList.ResetBindings();
                }

                Items.RaiseListChangedEvents = false;
                try
                {
                    // Add the new peptides to the library list
                    var setOverwritePeptides = new HashSet<Target>(listOverwritePeptides);
                    var setKeepPeptides = new HashSet<Target>(listKeepPeptides);
                    foreach (var peptide in listPeptidesNew)
                    {
                        var seq = peptide.ModifiedTarget;
                        int peptideIndex;
                        // Add any peptides not yet in the library
                        if (!dictLibraryIndices.TryGetValue(seq, out peptideIndex))
                        {
                            Items.Add(peptide);
                            continue;
                        }
                        // Skip any peptides where the peak type is less accurate than that
                        // of the existing iRT value.
                        if (setKeepPeptides.Contains(seq))
                            continue;

                        var peptideExist = Items[peptideIndex];
                        // Replace peptides if the user said to, or if the peak type is more accurate
                        // than that of the existing iRT value.
                        if (action == AddIrtPeptidesAction.replace || setOverwritePeptides.Contains(seq))
                        {
                            peptideExist.Irt = peptide.Irt;
                            peptideExist.TimeSource = peptide.TimeSource;
                        }
                        // Skip peptides if the user said to, or no change has occurred.
                        else if (action == AddIrtPeptidesAction.skip || Equals(peptide, peptideExist))
                        {
                            continue;
                        }
                        // Average existing and new if that is what the user specified.
                        else if (action == AddIrtPeptidesAction.average)
                        {
                            peptideExist.Irt = (peptide.Irt + peptideExist.Irt) / 2;
                        }
                        
                        Items.ResetItem(peptideIndex);
                    }
                }
                finally
                {
                    Items.RaiseListChangedEvents = true;
                }
                Items.ResetBindings();
            }

            private void GetPeptideLists(IEnumerable<DbIrtPeptide> peptidesNew,
                out LibKeyMap<int> libraryIndices,
                out List<Target> changed, out List<Target> overwrite, out List<Target> keep)
            {
                var targetIndexes = ImmutableList.ValueOf(Enumerable.Range(0, Items.Count)
                    .Where(index => null != Items[index].ModifiedTarget));
                libraryIndices = new LibKeyMap<int>(targetIndexes, targetIndexes.Select(i=>Items[i].ModifiedTarget.GetLibKey(Adduct.EMPTY).LibraryKey));

                changed = new List<Target>();
                overwrite = new List<Target>();
                keep = new List<Target>();

                // Check for existing matching peptides
                foreach (var peptide in peptidesNew)
                {
                    int peptideIndex;
                    if (!libraryIndices.TryGetValue(peptide.ModifiedTarget, out peptideIndex))
                        continue;
                    var peptideExist = Items[peptideIndex];
                    if (Equals(peptide, peptideExist))
                        continue;

                    if (peptide.TimeSource != peptideExist.TimeSource &&
                        peptideExist.TimeSource.HasValue)
                    {
                        switch (peptideExist.TimeSource.Value)
                        {
                            case (int)TimeSource.scan:
                                overwrite.Add(peptide.ModifiedTarget);
                                continue;
                            case (int)TimeSource.peak:
                                keep.Add(peptide.ModifiedTarget);
                                continue;
                        }
                    }

                    changed.Add(peptide.ModifiedTarget);
                }

                changed.Sort();
                overwrite.Sort();
                keep.Sort();
            }
        }

        private void gridViewStandard_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            UpdateNumStandards();
        }

        private void gridViewStandard_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            UpdateNumStandards();
        }

        private void gridViewLibrary_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            UpdateNumPeptides();
        }

        private void gridViewLibrary_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            UpdateNumPeptides();
        }

        private void UpdateNumStandards()
        {
            labelNumStandards.Text = string.Format(StandardPeptideCount == 1
                                                       ? Resources.EditIrtCalcDlg_UpdateNumStandards__0__Standard_peptide___1__required_
                                                       : Resources.EditIrtCalcDlg_UpdateNumStandards__0__Standard_peptides___1__required_,
                                                   StandardPeptideCount, RCalcIrt.MinStandardCount(StandardPeptideCount));
        }

        private void UpdateNumPeptides()
        {
            bool hasLibraryPeptides = LibraryPeptideList.Count != 0;
            btnCalibrate.Text = (hasLibraryPeptides
                                     ? Resources.EditIrtCalcDlg_UpdateNumPeptides_Recalibrate
                                     : Resources.EditIrtCalcDlg_UpdateNumPeptides_Calibrate);
            btnPeptides.Visible = hasLibraryPeptides;

            labelNumPeptides.Text = string.Format(LibraryPeptideList.Count == 1
                                                      ? Resources.EditIrtCalcDlg_UpdateNumPeptides__0__Peptide
                                                      : Resources.EditIrtCalcDlg_UpdateNumPeptides__0__Peptides,
                                                  LibraryPeptideList.Count);
        }

        #region Functional Test Support

        public string CalcName
        {
            get { return textCalculatorName.Text; }
            set { textCalculatorName.Text = value; }
        }

        public void DoPasteStandard()
        {
            _gridViewStandardDriver.OnPaste();
        }

        public void DoPasteLibrary()
        {
            _gridViewLibraryDriver.OnPaste();
        }

        public IrtStandard IrtStandards
        {
            get { return comboStandards.SelectedItem as IrtStandard; }
            set
            {
                if (value == null)
                    comboStandards.SelectedIndex = 0;

                for (var i = 0; i < comboStandards.Items.Count; i++)
                {
                    if (comboStandards.Items[i] == value)
                    {
                        comboStandards.SelectedIndex = i;
                        return;
                    }
                }
                comboStandards.SelectedIndex = 0;
            }
        }

        #endregion

        private void comboStandards_SelectedIndexChanged(object sender, EventArgs e)
        {
            var current = CurrentStandard;
            var selected = (IrtStandard) comboStandards.SelectedItem;

            if (current == selected)
            {
                return;
            }

            if (!IrtStandard.AllStandards(StandardPeptideList, IRT_TOLERANCE))
            {
                comboStandards.SelectedItem = IrtStandard.EMPTY;
                MessageDlg.Show(this,
                    Resources.EditIrtCalcDlg_comboStandards_SelectedIndexChanged_The_list_of_standard_peptides_must_contain_only_recognized_iRT_C18_standards_to_switch_to_a_predefined_set_of_iRT_C18_standards_);
                return;
            }

            if (_originalPeptides != null)
            {
                foreach (var original in _originalKnownPeptides.Where(peptide =>
                    !selected.Contains(peptide, IRT_TOLERANCE) &&
                    IrtStandard.ContainsMatch(StandardPeptides, peptide, IRT_TOLERANCE) &&
                    !IrtStandard.ContainsMatch(LibraryPeptides, peptide, IRT_TOLERANCE)))
                {
                    LibraryPeptideList.Add(new DbIrtPeptide(original) {Standard = false});
                }
            }

            LoadStandard(selected.Peptides);
        }

        private void HandleStandardsChanged(object sender, EventArgs eventArgs)
        {
            comboStandards.SelectedItem = CurrentStandard;
            // Use a dictionary to avoid this becoming O(n^2)
            var dictLibraryPeptides = new Dictionary<Target, DbIrtPeptide>();
            foreach (var libraryPeptide in LibraryPeptides)
            {
                var key = libraryPeptide.ModifiedTarget;
                if (!dictLibraryPeptides.ContainsKey(key))
                    dictLibraryPeptides.Add(key, libraryPeptide);
            }
            // Remove any matching peptides from the list of library peptides
            foreach (var standard in StandardPeptides)
            {
                DbIrtPeptide irtPeptide = standard;
                DbIrtPeptide libraryPeptide;
                if (dictLibraryPeptides.TryGetValue(irtPeptide.ModifiedTarget, out libraryPeptide) &&
                    IrtStandard.Match(irtPeptide, libraryPeptide, IRT_TOLERANCE))
                {
                    LibraryPeptideList.Remove(libraryPeptide);
                }
            }
        }
    }
}