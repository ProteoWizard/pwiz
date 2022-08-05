/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using NHibernate;
using pwiz.Common.Chemistry;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using DatabaseOpeningException = pwiz.Skyline.Model.IonMobility.DatabaseOpeningException;

namespace pwiz.Skyline.SettingsUI.IonMobility
{
    public partial class EditIonMobilityLibraryDlg : FormEx
    {
        private readonly IEnumerable<IonMobilityLibrarySpec> _existingLibs;

        public IonMobilityLibrary IonMobilityLibrary { get; private set; }

        private ValidatingIonMobilityPrecursor[] _originalMobilitiesFlat;
        private readonly CollisionalCrossSectionGridViewDriver _gridViewLibraryDriver;

        //Used to determine whether we are creating a new calculator, trying to overwrite
        //an old one, or editing an old one
        private readonly string _editingName = string.Empty;

        public const int COLUMN_TARGET = 0;
        public const int COLUMN_ADDUCT = 1;
        public const int COLUMN_CCS = 2;
        public const int COLUMN_ION_MOBILITY = 3;
        public const int COLUMN_ION_MOBILITY_UNITS = 4;
        public const int COLUMN_HIGH_ENERGY_OFFSET = 5;


        public EditIonMobilityLibraryDlg(IonMobilityLibrarySpec library, IEnumerable<IonMobilityLibrarySpec> existingLibs)
        {
            _existingLibs = existingLibs;

            InitializeComponent();

            Icon = Resources.Skyline;
            var smallMoleculeUI = Program.MainWindow.Document.HasSmallMolecules || Program.MainWindow.ModeUI != SrmDocument.DOCUMENT_TYPE.proteomic;

            _gridViewLibraryDriver = new CollisionalCrossSectionGridViewDriver(gridViewIonMobilities,
                bindingSourceLibrary,
                new SortableBindingList<ValidatingIonMobilityPrecursor>());

            // Show window width caclulation types in L10N, watch out for special type "unknown" which does not display
            object[] namesL10n = Enumerable.Range(0, Enum.GetNames(typeof(eIonMobilityUnits)).Length)
                .Select(n => IonMobilityFilter.IonMobilityUnitsL10NString((eIonMobilityUnits) n)).Where(s => s != null)
                .ToArray();
            columnIonMobilityUnits.MaxDropDownItems = namesL10n.Length;
            columnIonMobilityUnits.Items.AddRange(namesL10n);

            if (smallMoleculeUI)
            {
                gridViewIonMobilities.Columns[COLUMN_TARGET].HeaderText = Resources.EditIonMobilityLibraryDlg_EditIonMobilityLibraryDlg_Molecule;
                gridViewIonMobilities.Columns[COLUMN_ADDUCT].HeaderText = Resources.EditIonMobilityLibraryDlg_EditIonMobilityLibraryDlg_Adduct;
            }

            if (library != null)
            {
                textLibraryName.Text = _editingName = library.Name;
                string databaseStartPath = library.FilePath;

                OpenDatabase(databaseStartPath);
            }

            UpdateControls();

            // If there are high energy IM offsets be sure to show them
            if (LibraryMobilitiesFlat.Any(item => item.HighEnergyIonMobilityOffset != 0))
                cbOffsetHighEnergySpectra.Checked = true;
        }

        private void OnLoad(object sender, EventArgs e)
        {
            // If you set this in the Designer, DataGridView has a defect that causes it to throw an
            // exception if the the cursor is positioned over the record selector column during loading.
            gridViewIonMobilities.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private bool DatabaseChanged
        {
            get
            {
                if (_originalMobilitiesFlat == null)
                    return LibraryMobilitiesFlat.Any();

                if (_originalMobilitiesFlat.Length != LibraryMobilitiesFlat.Count)
                    return true;

                var ionMobilities = IonMobilityLibrary.FlatListToLibKeyIndex(LibraryMobilitiesFlat);
                foreach (var item in _originalMobilitiesFlat)
                {
                    if (!ionMobilities.ItemsMatching(item.Precursor, LibKeyIndex.LibraryMatchType.details).Any())
                    {
                        return true; // Original mobility value for this ion is not present in current list
                    }
                }
                return false;
            }
        }

        // Flat (multiple conformers occupy multiple lines) representation
        public IList<ValidatingIonMobilityPrecursor> LibraryMobilitiesFlat 
        {
            get { return _gridViewLibraryDriver.Items; }
            set { _gridViewLibraryDriver.SetTablePrecursors(value);}
        }

        public int LibraryMobilitiesFlatCount { get { return LibraryMobilitiesFlat.Count; } }

        public void ClearLibraryPeptides()
        {
            LibraryMobilitiesFlat.Clear();
        }

        private void btnCreateDb_Click(object sender, EventArgs e)
        {
            if (DatabaseChanged)
            {
                var result = MultiButtonMsgDlg.Show(this, Resources.EditIonMobilityLibraryDlg_btnCreateDb_Click_Are_you_sure_you_want_to_create_a_new_ion_mobility_library_file___Any_changes_to_the_current_library_will_be_lost_,
                     MessageBoxButtons.YesNo);

                if (result != DialogResult.Yes)
                    return;
            }

            using (var dlg = new SaveFileDialog
            {
                Title = Resources.EditIonMobilityLibraryDlg_btnCreateDb_Click_Create_Ion_Mobility_Library,
                InitialDirectory = Settings.Default.ActiveDirectory,
                OverwritePrompt = true,
                DefaultExt = IonMobilityDb.EXT,
                Filter = TextUtil.FileDialogFiltersAll(IonMobilityLibrarySpec.FILTER_IONMOBILITYLIBRARY) 
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var fileName = dlg.FileName;
                    CreateDatabaseFile(fileName);
                }
            }
        }

        public void CreateDatabaseFile(string fileName)
        {
            Settings.Default.ActiveDirectory = Path.GetDirectoryName(fileName);
            if (string.IsNullOrEmpty(LibraryName))
            {
                // User has not provided a library name - use file name as a reasonable guess
                LibraryName = Path.GetFileNameWithoutExtension(fileName);
            }
            CreateDatabase(fileName, LibraryName);
            textDatabase.Focus();
        }

        private void CreateDatabase(string path, string libraryName)
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
                MessageDlg.ShowException(this, x);
                return;
            }

            //Create file, initialize db
            try
            {
                IonMobilityLibrary = new IonMobilityLibrary(libraryName, path, 
                    IonMobilityDb.CreateIonMobilityDb(path, libraryName, false));

                textDatabase.Text = path;

                UpdateControls();
            }
            catch (DatabaseOpeningException x)
            {
                MessageDlg.ShowException(this, x);
            }
            catch (Exception x)
            {
                var message = TextUtil.LineSeparate(string.Format(Resources.EditIonMobilityLibraryDlg_CreateDatabase_The_ion_mobility_library_file__0__could_not_be_created, path),
                                                    x.Message);
                MessageDlg.ShowWithException(this, message, x);
            }
        }

        private void btnBrowseDb_Click(object sender, EventArgs e)
        {
            if (DatabaseChanged)
            {
                var result = MultiButtonMsgDlg.Show(this, Resources.EditIonMobilityLibraryDlg_btnBrowseDb_Click_Are_you_sure_you_want_to_open_a_new_ion_mobility_library_file___Any_changes_to_the_current_library_will_be_lost_,
                    MessageBoxButtons.YesNo);

                if (result != DialogResult.Yes)
                    return;
            }

            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Resources.EditIonMobilityLibraryDlg_btnBrowseDb_Click_Open_Ion_Mobility_Library,
                InitialDirectory = Settings.Default.ActiveDirectory,
                DefaultExt = IonMobilityDb.EXT,
                Filter = TextUtil.FileDialogFiltersAll(IonMobilityLibrarySpec.FILTER_IONMOBILITYLIBRARY)
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
                MessageDlg.Show(this, String.Format(Resources.EditIonMobilityLibraryDlg_OpenDatabase_The_file__0__does_not_exist__Click_the_Create_button_to_create_a_new_ion_mobility_library_or_click_the_Open_button_to_find_the_missing_file_,
                                                    path));
                return;
            }

            try
            {
                // Infer library name from file name if user has not specified otherwise
                if (string.IsNullOrEmpty(LibraryName) && !string.IsNullOrEmpty(path))
                {
                    LibraryName = Path.GetFileNameWithoutExtension(path);
                }
                var db = IonMobilityDb.GetIonMobilityDb(path, null); // TODO: (copied from iRT code) LongWaitDlg
                var dbIonMobilities = db.GetIonMobilities().ToArray(); // Avoid multiple enumeration

                LoadLibrary(dbIonMobilities);

                // Clone all of the peptides to use for comparison in OkDialog
                _originalMobilitiesFlat = dbIonMobilities.Select(p => new ValidatingIonMobilityPrecursor(p)).ToArray();

                textDatabase.Text = path;
                IonMobilityLibrary = new IonMobilityLibrary(LibraryName, path, db);
            }
            catch (DatabaseOpeningException e)
            {
                MessageDlg.ShowException(this, e);
            }
        }

        public void OkDialog()
        {
            if(string.IsNullOrEmpty(textLibraryName.Text))
            {
                MessageDlg.Show(this, Resources.EditIonMobilityLibraryDlg_OkDialog_Please_enter_a_name_for_the_ion_mobility_library_);
                textLibraryName.Focus();
                return;
            }

            if (_existingLibs != null)
            {
                foreach (var existingLib in _existingLibs)
                {
                    if (Equals(existingLib.Name, textLibraryName.Text) && !Equals(existingLib.Name, _editingName))
                    {
                        if (MultiButtonMsgDlg.Show(this, string.Format(Resources.EditIonMobilityLibraryDlg_OkDialog_An_ion_mobility_library_with_the_name__0__already_exists__Do_you_want_to_overwrite_it_,
                                    textLibraryName.Text),
                                MessageBoxButtons.YesNo) != DialogResult.Yes)
                        {
                            textLibraryName.Focus();
                            return;
                        }
                    }
                }
            }

            string message;
            if (string.IsNullOrEmpty(textDatabase.Text))
            {
                message = TextUtil.LineSeparate(Resources.EditIonMobilityLibraryDlg_OkDialog_Please_choose_a_file_for_the_ion_mobility_library,
                                                Resources.EditIonMobilityLibraryDlg_OkDialog_Click_the_Create_button_to_create_a_new_library_or_the_Open_button_to_open_an_existing_library_file_);
                MessageDlg.Show(this, message);
                textDatabase.Focus();
                return;
            }
            string path = Path.GetFullPath(textDatabase.Text);
            if (!Equals(path, textDatabase.Text))
            {
                message = TextUtil.LineSeparate(Resources.EditIonMobilityLibraryDlg_OkDialog_Please_use_a_full_path_to_a_file_for_the_ion_mobility_library_,
                                                Resources.EditIonMobilityLibraryDlg_OkDialog_Click_the_Create_button_to_create_a_new_library_or_the_Open_button_to_open_an_existing_library_file_);
                MessageDlg.Show(this, message);
                textDatabase.Focus();
                return;
            }
            if (!string.Equals(Path.GetExtension(path), IonMobilityDb.EXT))
                path += IonMobilityDb.EXT;

            // This function MessageDlg.Show's error messages
            if (!ValidateIonMobilitiesList(LibraryMobilitiesFlat, textLibraryName.Text ?? string.Empty))
            {
                gridViewIonMobilities.Focus();
                return;                
            }

            try
            {
                IonMobilityLibrary =
                    IonMobilityLibrary.CreateFromList(textLibraryName.Text, path, LibraryMobilitiesFlat);
            }
            catch (DatabaseOpeningException x)
            {
                MessageDlg.Show(this, x.Message);
                textDatabase.Focus();
                return;
            }
            catch (StaleStateException staleStateException)
            {
                // CONSIDER: (copied from iRT code) Not sure if this is the right thing to do.  It would
                //           be nice to solve whatever is causing this, but this is
                //           better than showing an unexpected error form with stack trace.
                MessageDlg.ShowWithException(this,
                    Resources
                        .EditIonMobilityLibraryDlg_OkDialog_Failure_updating_peptides_in_the_ion_mobility_library__The_library_may_be_out_of_synch_,
                    staleStateException);
                return;
            }
            catch (IOException x)
            {
                // DB file open in an external viewer etc
                MessageDlg.Show(this, x.Message);
                textDatabase.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
        }

        private bool ValidateIonMobilitiesList(IEnumerable<ValidatingIonMobilityPrecursor> imList, string tableName)
        {
            foreach(var ion in imList)
            {
                var seqModified = ion.Precursor;
                // CONSIDER: Select the peptide row
                if (seqModified.Target.IsProteomic && !FastaSequence.IsValidPeptideSequence(seqModified.Sequence))
                {
                    MessageDlg.Show(this, 
                        string.Format(Resources.EditIonMobilityLibraryDlg_ValidatePeptideList_The_value__0__is_not_a_valid_modified_peptide_sequence_, seqModified));
                    return false;
                }
            }
            return true;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void LoadLibrary(IEnumerable<DbPrecursorAndIonMobility> dbList)
        {
            LibraryMobilitiesFlat.Clear();
            foreach (var item in dbList)
            {
                var val = new ValidatingIonMobilityPrecursor(item);
                if (!LibraryMobilitiesFlat.Any(p => p.Equals(val)))
                    LibraryMobilitiesFlat.Add(val);
            }
        }

        private void addIonMobilityLibraryContextMenuItem_Click(object sender, EventArgs e)
        {
            AddIonMobilityLibrary();
        }

        /// <summary>
        /// import ion mobility data from an external file format.
        /// </summary>
        public void AddIonMobilityLibrary()
        {
            // _gridViewLibraryDriver.AddIonMobilityDatabase(); TODO once we have some examples of external files
        }

        private void RemoveRedundantEntries()
        {
            // remove redundant rows if any
            var existingLines = new Dictionary<string, int>();
            int redundantRows = 0;
            int rownum = 0;
            foreach (DataGridViewRow row in gridViewIonMobilities.Rows)
            {
                if (!row.IsNewRow)
                {
                    const int COLUMN_COUNT = 4;
                    var valstrs = new string[COLUMN_COUNT];
                    double value;
                    if ((row.Cells[COLUMN_CCS].FormattedValue == null) ||
                        !double.TryParse(row.Cells[COLUMN_CCS].FormattedValue.ToString(),
                            out value))
                    {
                        value = -1;
                    }
                    valstrs[COLUMN_CCS] = value.ToString(CultureInfo.InvariantCulture);
                    if ((row.Cells[COLUMN_HIGH_ENERGY_OFFSET].FormattedValue == null) ||
                        !double.TryParse(row.Cells[COLUMN_HIGH_ENERGY_OFFSET].FormattedValue.ToString(),
                            out value))
                    {
                        value = -1;
                    }
                    valstrs[COLUMN_HIGH_ENERGY_OFFSET] = value.ToString(CultureInfo.InvariantCulture);
                    valstrs[COLUMN_TARGET] = (row.Cells[COLUMN_TARGET].FormattedValue ?? string.Empty).ToString();
                    string valstr = TextUtil.SpaceSeparate(valstrs);
                    int oldrow;
                    if (existingLines.TryGetValue(valstr, out oldrow))
                    {
                        for (int col = 0; col < COLUMN_COUNT; col++)
                        {
                            row.Cells[col].Selected = true; // mark line for deletion
                        }
                        redundantRows++;
                    }
                    else
                    {
                        existingLines.Add(valstr, rownum);
                    }
                    rownum++;
                }
            }
            if (redundantRows > 0)
                gridViewIonMobilities.DoDelete(); // delete rows with selected columns
            UpdateNumPrecursorIons(); // update the count display
        }

        private void gridViewLibrary_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            UpdateNumPrecursorIons(); // update the count display
        }

        private void gridViewLibrary_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            UpdateNumPrecursorIons();
        }

        private void UpdateNumPrecursorIons()
        {
            labelNumPrecursorIons.Text = ModeUIAwareStringFormat(LibraryMobilitiesFlat.Count <= 1
                    ? Resources.EditIonMobilityLibraryDlg_UpdateNumPrecursorIons__0__Precursor_Ion
                    : Resources.EditIonMobilityLibraryDlg_UpdateNumPrecursorIons__0__Precursor_Ions,
                LibraryMobilitiesFlat.Count);
        }

        public static string ValidateUniquePrecursors(IEnumerable<ValidatingIonMobilityPrecursor> precursorIonMobilities, out List<ValidatingIonMobilityPrecursor> minimalSet)
        {
            var libKeyIndex = IonMobilityLibrary.FlatListToLibKeyIndex(precursorIonMobilities);
            minimalSet = IonMobilityLibrary.MultiConformerDictionaryToFlatList(libKeyIndex); // The conversion to dict removed any duplicates
            var multiConformers = new HashSet<LibKey>();
            foreach (var item in libKeyIndex)
            {
                if (libKeyIndex.ItemsMatching(item.LibraryKey, LibKeyIndex.LibraryMatchType.ion).ToList().Count > 1)
                {
                    multiConformers.Add(new LibKey(item.LibraryKey.Target, item.LibraryKey.Adduct));
                }
            }

            var multiConformersCount = multiConformers.Count;

            if (multiConformersCount > 0)
            {
                if (multiConformersCount == 1)
                {
                    return string.Format(Resources.EditIonMobilityLibraryDlg_ValidateUniquePrecursors_The_ion__0__has_multiple_ion_mobility_values__Skyline_supports_multiple_conformers__so_this_may_be_intentional_,
                        multiConformers.First());
                }
                if (multiConformersCount < 15)
                {
                    return TextUtil.LineSeparate(Resources.EditIonMobilityLibraryDlg_ValidateUniquePrecursors_The_following_ions_have_multiple_ion_mobility_values__Skyline_supports_multiple_conformers__so_this_may_be_intentional_,
                        string.Empty,
                        TextUtil.LineSeparate(multiConformers.Select(c => c.ToString())));
                }
                return string.Format(Resources.EditIonMobilityLibraryDlg_ValidateUniquePrecursors_This_list_contains__0__ions_with_multiple_ion_mobility_values__Skyline_supports_multiple_conformers__so_this_may_be_intentional_,
                    multiConformersCount);
            }
            return null;
        }

        private void btnImportFromLibrary_Click(object sender, EventArgs e)
        {
            ImportFromSpectralLibrary();
        }

        public void ImportFromSpectralLibrary()
        {
            CheckDisposed();
            _gridViewLibraryDriver.ImportFromSpectralLibrary();
        }



        #region Functional Test Support

        public string LibraryName
        {
            get { return (textLibraryName.Text ?? string.Empty).Trim(); }
            set { textLibraryName.Text = (value ?? string.Empty).Trim(); }
        }

        public void DoPasteLibrary()
        {
            _gridViewLibraryDriver.OnPaste();
        }

        public void SetOffsetHighEnergySpectraCheckbox(bool enable)
        {
            cbOffsetHighEnergySpectra.Checked = enable;
            UpdateControls();
        }

        public bool GetOffsetHighEnergySpectraCheckbox()
        {
            return cbOffsetHighEnergySpectra.Checked;
        }

        #endregion

        private void btnUseResults_Click(object sender, EventArgs e)
        {
            GetIonMobilitiesFromResults();
        }
        public void GetIonMobilitiesFromResults()
        {
            try
            {
                var document = Program.MainWindow.Document;
                var documentFilePath = Program.MainWindow.DocumentFilePath;
                bool useHighEnergyOffset = cbOffsetHighEnergySpectra.Checked;
                using (var longWaitDlg = new LongWaitDlg
                {
                    Text = Resources.EditIonMobilityLibraryDlg_GetDriftTimesFromResults_Finding_ion_mobility_values_for_peaks,
                    Message = string.Empty,
                    ProgressValue = 0
                })
                {
                    Dictionary<LibKey, IonMobilityAndCCS> dict = null;
                    longWaitDlg.PerformWork(this, 100, broker =>
                    {
                        dict = IonMobilityLibrary.CreateFromResults(
                            document, documentFilePath, useHighEnergyOffset,
                            broker);
                    });
                    if (!longWaitDlg.IsCanceled && dict != null)
                    {
                        // Preserve existing values that weren't updated by this search
                        dict = MergeWithExisting(LibraryMobilitiesFlat, dict);
                        // And update display
                        UpdateMeasuredDriftTimesControl(dict);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageDlg.ShowException(this, ex);
            }
        }

        public static Dictionary<LibKey, IonMobilityAndCCS> MergeWithExisting(IList<ValidatingIonMobilityPrecursor> previous, Dictionary<LibKey, IonMobilityAndCCS> dict)
        {
            if (previous.Any()) // Any existing entries?
            {
                // Add any existing values that weren't updated to this dictionary
                // Also check for equivalent (varying precision modifications) precursors in the document, prefer the existing version 
                var found = LibKeyMap<IonMobilityAndCCS>.FromDictionary(dict); // LibKeyMap can match modifications written at varying precisions
                foreach (var existing in previous)
                {
                    // Determine whether the document uses a different representation of the same precursor
                    var match = found.KeyPairsMatching(existing.Precursor, LibKeyIndex.LibraryMatchType.ion).ToArray();
                    if (match.Length == 0)
                    {
                        // Document did not contain the library precursor, retain it
                        dict.Add(existing.Precursor, existing.GetIonMobilityAndCCS());
                    }
                    else
                    {
                        // Document contained the library precursor, but is it using same modification precision etc?
                        if (!dict.ContainsKey(existing.Precursor))
                        {
                            // Document and library use different representation of precursor
                            // Prefer the one already in the library
                            foreach (var kvp in match)
                            {
                                dict.Remove(kvp.Key);
                                dict.Add(existing.Precursor, kvp.Value);
                            }
                        }
                    }
                }
            }

            return dict;
        }

        private void UpdateMeasuredDriftTimesControl(Dictionary<LibKey, IonMobilityAndCCS> ionMobilities)
        {

            // List any measured ion mobility values
            _gridViewLibraryDriver.SetTablePrecursors(ionMobilities.Select(item => 
                new ValidatingIonMobilityPrecursor(item.Key.Target, item.Key.Adduct, item.Value)));

            cbOffsetHighEnergySpectra.Checked = ionMobilities.Any(item => item.Value.HighEnergyIonMobilityValueOffset != 0);

            UpdateControls();
        }


        private void UpdateControls()
        {
            textDatabase.Enabled = !string.IsNullOrEmpty(LibraryName); // Need a name before we can create a database

            cbOffsetHighEnergySpectra.Enabled = btnUseResults.Enabled = btnImportFromLibrary.Enabled = gridViewIonMobilities.Enabled =
                (IonMobilityLibrary != null); // Need a database before we can populate it

            _gridViewLibraryDriver.SetColumnVisibility(COLUMN_HIGH_ENERGY_OFFSET, cbOffsetHighEnergySpectra.Checked);
        }

        private void cbOffsetHighEnergySpectra_CheckedChanged(object sender, EventArgs e)
        {
            UpdateControls();
        }
    }

    public class CollisionalCrossSectionGridViewDriver : CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPrecursor>
    {
        public CollisionalCrossSectionGridViewDriver(DataGridViewEx gridView,
                                         BindingSource bindingSource,
                                         SortableBindingList<ValidatingIonMobilityPrecursor> items)
            : base(gridView, bindingSource, items)
        {
        }

        protected override void DoPaste()
        {
            var mMeasuredCollisionalCrossSectionsNew = new List<ValidatingIonMobilityPrecursor>();
            var targetResolver = TargetResolver.MakeTargetResolver(Program.ActiveDocumentUI);
            GridView.DoPaste(MessageParent, ValidateRow,
                values =>
                {
                    var columnCount = values.Length;
                    var target = targetResolver.TryResolveTarget(values[EditIonMobilityLibraryDlg.COLUMN_TARGET], out _)  ?? new Target(values[EditIonMobilityLibraryDlg.COLUMN_TARGET]);
                    var precursorAdduct = columnCount <= EditIonMobilityLibraryDlg.COLUMN_ADDUCT ? Adduct.EMPTY : 
                        target.IsProteomic
                            ? Adduct.FromStringAssumeProtonated(values[EditIonMobilityLibraryDlg.COLUMN_ADDUCT]) // e.g. "1" -> M+H
                            : Adduct.FromStringAssumeChargeOnly(values[EditIonMobilityLibraryDlg.COLUMN_ADDUCT]); // e.g. "1" -> M+
                    var ccs = columnCount <= EditIonMobilityLibraryDlg.COLUMN_CCS || string.IsNullOrEmpty(values[EditIonMobilityLibraryDlg.COLUMN_CCS])
                        ? 0
                        : double.Parse(values[EditIonMobilityLibraryDlg.COLUMN_CCS]);
                    var im = columnCount <= EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY || string.IsNullOrEmpty(values[EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY])
                        ? 0
                        : double.Parse(values[EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY]);
                    var units = columnCount > EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY_UNITS ?
                        IonMobilityFilter.IonMobilityUnitsFromL10NString(values[EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY_UNITS]) :
                        eIonMobilityUnits.none;
                    var offset = columnCount <= EditIonMobilityLibraryDlg.COLUMN_HIGH_ENERGY_OFFSET || string.IsNullOrEmpty(values[EditIonMobilityLibraryDlg.COLUMN_HIGH_ENERGY_OFFSET])
                        ? 0
                        : double.Parse(values[EditIonMobilityLibraryDlg.COLUMN_HIGH_ENERGY_OFFSET]);
                    mMeasuredCollisionalCrossSectionsNew.Add(new ValidatingIonMobilityPrecursor(
                        target,
                        precursorAdduct,
                        ccs,
                        im,
                        offset,
                        units));
                }
            );
            SetTablePrecursors(mMeasuredCollisionalCrossSectionsNew);
        }

        public void SetColumnVisibility(int col, bool visible)
        {
            this.GridView.Columns[col].Visible = visible;
        }

        public void SetTablePrecursors(IEnumerable<ValidatingIonMobilityPrecursor> tableIons)
        {
            string message = EditIonMobilityLibraryDlg.ValidateUniquePrecursors(tableIons, out var minimalIons); // check for conflicts, strip out dupes
            Populate(minimalIons);
            if (message != null)
            {
                MessageDlg.Show(MessageParent, message);
            }
        }

        public void ImportFromSpectralLibrary()
        {
            using (var importFromLibraryDlg = new ImportIonMobilityFromSpectralLibraryDlg(Settings.Default.SpectralLibraryList, Items, this))
            {
                importFromLibraryDlg.ShowDialog(MessageParent);
            }
        }

        public string ImportFromSpectralLibrary(LibrarySpec librarySpec, IList<ValidatingIonMobilityPrecursor> existing)
        {
            var libraryManager = ((ILibraryBuildNotificationContainer)Program.MainWindow).LibraryManager;
            Library library = null;
            IEnumerable<ValidatingIonMobilityPrecursor> peptideCollisionalCrossSections = null;
            try
            {
                library = libraryManager.TryGetLibrary(librarySpec);
                using (var longWait = new LongWaitDlg  
                {
                    Text = Resources.CollisionalCrossSectionGridViewDriver_AddSpectralLibrary_Adding_Spectral_Library,
                    Message = string.Format(Resources.CollisionalCrossSectionGridViewDriver_AddSpectralLibrary_Adding_ion_mobility_data_from__0_, librarySpec.FilePath),
                    FormBorderStyle = FormBorderStyle.Sizable
                })
                {
                    try
                    {
                        var status = longWait.PerformWork(MessageParent, 800, monitor =>
                        {
                            var success = false;
                            if (library == null)
                                library = librarySpec.LoadLibrary(new DefaultFileLoadMonitor(monitor));

                            int fileCount = library.FileCount ?? 0;
                            if (fileCount != 0)
                            {
                                peptideCollisionalCrossSections = CollectIonMobilitiesAndCollisionalCrossSections(monitor, library);
                                if (peptideCollisionalCrossSections != null)
                                {
                                    var found = peptideCollisionalCrossSections.ToArray();
                                    success = found.Any();
                                    if (success && existing.Any())
                                    {
                                        // Combine with any existing entries
                                        var dict = new Dictionary<LibKey, IonMobilityAndCCS>();
                                        foreach (var item in found)
                                        {
                                            var libkey = item.Precursor;
                                            var value = item.GetIonMobilityAndCCS();
                                            if (!dict.ContainsKey(libkey)) // Not expecting multiple conformers from a spectral library
                                                dict.Add(libkey, value);
                                        }
                                        dict = EditIonMobilityLibraryDlg.MergeWithExisting(existing, dict);
                                        var updated = new List<ValidatingIonMobilityPrecursor>();
                                        foreach (var kvp in dict)
                                        {
                                            updated.Add(new ValidatingIonMobilityPrecursor(kvp.Key.Target, kvp.Key.Adduct, kvp.Value));
                                        }
                                        peptideCollisionalCrossSections = updated;
                                    }
                                }
                            }
                            if (!success)
                            {
                                string message = string.Format(Resources.CollisionalCrossSectionGridViewDriver_AddSpectralLibrary_The_library__0__does_not_contain_ion_mobility_information_,
                                                               librarySpec.FilePath);
                                monitor.UpdateProgress(new ProgressStatus(string.Empty).ChangeErrorException(new IOException(message)));
                            }

                        });
                        if (status.IsError)
                        {
                            return status.ErrorException.Message;
                        }
                    }
                    catch (Exception x)
                    {
                        var message = TextUtil.LineSeparate(string.Format(Resources.CollisionalCrossSectionGridViewDriver_AddSpectralLibrary_An_error_occurred_attempting_to_load_the_library_file__0__,
                                                                          librarySpec.FilePath),
                                                            x.Message);
                        return message;
                    }
                }
            }
            finally
            {
                if (library != null)
                {
                    // (ReSharper 2019.1 seems not to notice the check that's already here)
                    // ReSharper disable PossibleNullReferenceException
                    foreach (var pooledStream in library.ReadStreams)
                    // ReSharper restore PossibleNullReferenceException
                        pooledStream.CloseStream();
                }
            }

            if (peptideCollisionalCrossSections == null)
                return null;
            SetTablePrecursors(peptideCollisionalCrossSections);
            return null;
        }

        public static IEnumerable<ValidatingIonMobilityPrecursor> CollectIonMobilitiesAndCollisionalCrossSections(IProgressMonitor monitor, Library library)
        {
            IProgressStatus status = new ProgressStatus(Resources.CollisionalCrossSectionGridViewDriver_ProcessIonMobilityValues_Reading_ion_mobility_information);
            List<ValidatingIonMobilityPrecursor> peptideIonMobilities = null;
            if ((monitor != null ) && monitor.IsCanceled)
                return null;
            if (monitor != null)
            {
                var message = string.Format(Resources.CollisionalCrossSectionGridViewDriver_ProcessDriftTimes_Reading_ion_mobility_data_from__0__, library.Name);
                monitor.UpdateProgress(status = status.ChangeMessage(message));
            }

            var keys = library.Keys.ToArray();
            if (library.TryGetPrecursorFilter(keys, out var ionMobilities))
            {
                status = CollectIonMobilitiesAndCollisionalCrossSections(monitor, ionMobilities, status, out peptideIonMobilities);
            }

            if (monitor != null)
                monitor.UpdateProgress(status.Complete());

            return peptideIonMobilities;
        }

        public static IProgressStatus CollectIonMobilitiesAndCollisionalCrossSections(IProgressMonitor monitor,
            LibraryPrecursorFiltersInfo precursorFilters, IProgressStatus status, out List<ValidatingIonMobilityPrecursor> peptideIonMobilities)
        {
            int runCount = 0;
            peptideIonMobilities = new List<ValidatingIonMobilityPrecursor>();
            var dict = precursorFilters.GetPrecursorFiltersDict();
            foreach (var kvp in dict)
            {
                var key = kvp.Key;
                foreach (var precursorFilter in kvp.Value)
                {
                    var ionMobility = precursorFilter.IonMobilityAndCCS;
                    if (key.IsSmallMoleculeKey)
                        peptideIonMobilities.Add(new ValidatingIonMobilityPrecursor(key.SmallMoleculeLibraryAttributes,
                            key.Adduct,
                            ionMobility.CollisionalCrossSectionSqA ?? 0,
                            ionMobility.IonMobility.Mobility ?? 0,
                            ionMobility.HighEnergyIonMobilityValueOffset ?? 0,
                            ionMobility.IonMobility.Units));
                    else
                        peptideIonMobilities.Add(new ValidatingIonMobilityPrecursor(key.Target,
                            key.Adduct,
                            ionMobility.CollisionalCrossSectionSqA ?? 0,
                            ionMobility.IonMobility.Mobility ?? 0,
                            ionMobility.HighEnergyIonMobilityValueOffset ?? 0,
                            ionMobility.IonMobility.Units));
                }

                if (monitor != null)
                    monitor.UpdateProgress(status = status.ChangePercentComplete(runCount++ * 100 / (dict.Count + 1)));
            }

            return status;
        }
    }



    public abstract class CollisionalCrossSectionGridViewDriverBase<TItem> : SimpleGridViewDriver<TItem>
        where TItem : ValidatingIonMobilityPrecursor
    {

        protected CollisionalCrossSectionGridViewDriverBase(DataGridViewEx gridView, BindingSource bindingSource, SortableBindingList<TItem> items)
            : base(gridView, bindingSource, items)
        {
            GridView.RowValidating += gridView_RowValidating;
        }

        public static string ValidateRow(object[] columns, int lineNumber, TargetResolver targetResolver, out int badCell)
        {
            badCell = 0;
            if (columns.Length < 3)
            {
                return Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_The_pasted_text_must_at_a_minimum_contain_columns_for_peptide_and_adduct__along_with_collisional_cross_section_and_or_ion_mobility_;
            }

            var targetObj = columns[EditIonMobilityLibraryDlg.COLUMN_TARGET] as Target;
            var target = columns[EditIonMobilityLibraryDlg.COLUMN_TARGET] == null
                ? String.Empty
                : columns[EditIonMobilityLibraryDlg.COLUMN_TARGET].ToString();
            var isPeptide = FastaSequence.IsValidPeptideSequence(target);
            var adduct = columns[EditIonMobilityLibraryDlg.COLUMN_ADDUCT] == null
                ? String.Empty
                : columns[EditIonMobilityLibraryDlg.COLUMN_ADDUCT].ToString();
            var collisionalCrossSection = columns[EditIonMobilityLibraryDlg.COLUMN_CCS] == null
                ? null
                : columns[EditIonMobilityLibraryDlg.COLUMN_CCS].ToString();
            var ionMobility = (columns.Length > EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY) &&
                              columns[EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY] != null
                ? columns[EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY].ToString()
                : string.Empty;
            var highEnergyOffset = (columns.Length > EditIonMobilityLibraryDlg.COLUMN_HIGH_ENERGY_OFFSET) &&
                                   columns[EditIonMobilityLibraryDlg.COLUMN_HIGH_ENERGY_OFFSET] != null
                ? columns[EditIonMobilityLibraryDlg.COLUMN_HIGH_ENERGY_OFFSET].ToString()
                : string.Empty;
            var units = (columns.Length > EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY_UNITS) &&
                        columns[EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY_UNITS] != null
                ? columns[EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY_UNITS].ToString()
                : string.Empty;

            var messages = new List<string>();

            if (string.IsNullOrWhiteSpace(target))
            {
                messages.Add(string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Missing_peptide_sequence_on_line__0__, lineNumber));
                badCell = EditIonMobilityLibraryDlg.COLUMN_TARGET;
            }
            else if (!isPeptide && 
                     targetResolver.TryResolveTarget(target, out _) == null && 
                     targetResolver.TryResolveTarget(targetObj?.ToSerializableString(), out _) == null)
            {
                messages.Add(string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_The_text__0__is_not_a_valid_peptide_sequence_on_line__1__, target, lineNumber));
                badCell = EditIonMobilityLibraryDlg.COLUMN_TARGET;
            }
            else if (isPeptide)
            {
                try
                {
                    columns[EditIonMobilityLibraryDlg.COLUMN_TARGET] =
                        SequenceMassCalc.NormalizeModifiedSequence(target);
                }
                catch (Exception x)
                {
                    messages.Add(x.Message);
                    badCell = EditIonMobilityLibraryDlg.COLUMN_TARGET;
                }
            }

            if (string.IsNullOrWhiteSpace(adduct))
            {
                messages.Add(string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Missing_adduct_description_on_line__0_, lineNumber));
                badCell = EditIonMobilityLibraryDlg.COLUMN_ADDUCT;
            }
            else if (!Adduct.TryParse(adduct, out var parsedAdduct))
            {
                messages.Add(string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Could_not_parse_adduct_description___0___on_line__1_, adduct, lineNumber));
                badCell = EditIonMobilityLibraryDlg.COLUMN_ADDUCT;
            }
            else if (Math.Abs(parsedAdduct.AdductCharge) > TransitionGroup.MAX_PRECURSOR_CHARGE || parsedAdduct.AdductCharge == 0)
            {
                messages.Add(string.Format(
                    Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow__0__is_not_a_valid_charge__Precursor_charges_must_be_integers_with_absolute_value_between_1_and__1__,
                    parsedAdduct.AdductCharge, TransitionGroup.MAX_PRECURSOR_CHARGE));
            }

            if (string.IsNullOrWhiteSpace(collisionalCrossSection) && string.IsNullOrWhiteSpace(ionMobility))
            {
                messages.Add(string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Missing_collisional_cross_section_value_on_line__0__, lineNumber));
                badCell = EditIonMobilityLibraryDlg.COLUMN_CCS;
            }
            else if (!string.IsNullOrWhiteSpace(collisionalCrossSection))
            {
                double dCollisionalCrossSection;
                if (!double.TryParse(collisionalCrossSection, out dCollisionalCrossSection))
                {
                    messages.Add(string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Invalid_number_format__0__for_collisional_cross_section_on_line__1__,
                        collisionalCrossSection,
                        lineNumber));
                    badCell = EditIonMobilityLibraryDlg.COLUMN_CCS;
                }
                else if (dCollisionalCrossSection < 0)
                {
                    messages.Add(string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_The_collisional_cross_section__0__must_be_greater_than_zero_on_line__1__,
                        dCollisionalCrossSection,
                        lineNumber));
                    badCell = EditIonMobilityLibraryDlg.COLUMN_CCS;
                }
            }

            if (string.IsNullOrWhiteSpace(ionMobility) && string.IsNullOrWhiteSpace(collisionalCrossSection))
            {
                messages.Add(string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Missing_ion_mobility_value_on_line__0__, lineNumber));
                badCell = EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY;
            }
            else if (!string.IsNullOrWhiteSpace(ionMobility))
            {
                double dIonMobility;
                if (!double.TryParse(ionMobility, out dIonMobility))
                {
                    messages.Add(string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Invalid_number_format__0__for_ion_mobility_on_line__1__,
                        ionMobility,
                        lineNumber));
                    badCell = EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY;
                }
                else if (dIonMobility <= 0)
                {
                    if (dIonMobility == 0 || 
                        !IonMobilityFilter.TryParseIonMobilityUnits(units, out var unitsType) || // No units declared
                        !IonMobilityFilter.AcceptNegativeMobilityValues(unitsType)) // Negative values inappropriate for these units
                    {
                        messages.Add(string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_The_ion_mobility_value___0___on_line__1__must_be_greater_than_zero,
                            dIonMobility,
                            lineNumber));
                    }
                }
            }

            double dHighEnergyDriftTimeOffsetMsec;
            if (!string.IsNullOrWhiteSpace(highEnergyOffset) && !double.TryParse(highEnergyOffset, out dHighEnergyDriftTimeOffsetMsec))
            {
                messages.Add(string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Cannot_read_high_energy_ion_mobility_offset_value___0___on_line__1__,
                    highEnergyOffset,
                    lineNumber));
                badCell = EditIonMobilityLibraryDlg.COLUMN_HIGH_ENERGY_OFFSET;
            }

            if (!string.IsNullOrEmpty(units) && !IonMobilityFilter.TryParseIonMobilityUnits(units, out var _))
            {
                messages.Add(string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Unrecognized_ion_mobility_units___0___on_line__1_,
                    units,
                    lineNumber));
                // Inform the user of the strings we will accept
                messages.Add(string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Supported_units_include___0_,
                    string.Join(@",", IonMobilityFilter.KnownIonMobilityTypes)));
                badCell = EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY_UNITS;
            }

            return messages.Any() ? TextUtil.LineSeparate(messages) : null;
        }

        public static bool ValidateRow(object[] columns, IWin32Window parent, int lineNumber)
        {
            var targetResolver = TargetResolver.MakeTargetResolver(Program.ActiveDocumentUI);
            string message = ValidateRow(columns, lineNumber, targetResolver, out _);
            if (message == null)
                return true;
            MessageDlg.Show(parent, message);
            return false;
        }

        private void gridView_RowValidating(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (!DoRowValidating(e.RowIndex))
                e.Cancel = true;
        }

        protected virtual bool DoRowValidating(int rowIndex)
        {
            var row = GridView.Rows[rowIndex];
            if (row.IsNewRow)
                return true;
            var targetResolver = TargetResolver.MakeTargetResolver(Program.ActiveDocumentUI);
            var cells = new List<object>();
            for (var i = 0; i < row.Cells.Count; i++)
            {
                cells.Add(row.Cells[i].Value);
            }
            var errorText = ValidateRow(cells.ToArray(), rowIndex, targetResolver, out var badCol);
            if (errorText != null)
            {
                bool messageShown = false;
                try
                {
                    GridView.CurrentCell = row.Cells[badCol];
                    MessageDlg.Show(MessageParent, errorText);
                    messageShown = true;
                    GridView.BeginEdit(true);
                }
                catch (Exception)
                {
                    // Exception may be thrown if current cell is changed in the wrong context.
                    if (!messageShown)
                        MessageDlg.Show(MessageParent, errorText);
                }
                return false;
            }
            return true;
        }
    }
}