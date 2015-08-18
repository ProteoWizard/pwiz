/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using NHibernate;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI.Optimization;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditOptimizationLibraryDlg : FormEx
    {
        private readonly IEnumerable<OptimizationLibrary> _existing;
        private readonly SrmDocument _document;

        public OptimizationLibrary Library { get; private set; }

        private DbOptimization[] _original;
        private readonly LibraryGridViewDriver _gridViewLibraryDriver;

        //Used to determine whether we are creating a new library, trying to overwrite
        //an old one, or editing an old one
        private readonly string _editingName = string.Empty;

        public EditOptimizationLibraryDlg(SrmDocument document, OptimizationLibrary lib, IEnumerable<OptimizationLibrary> existing)
        {
            _existing = existing;
            _document = document;

            InitializeComponent();

            Icon = Resources.Skyline;

            _gridViewLibraryDriver = new LibraryGridViewDriver(gridViewLibrary, bindingSourceLibrary,
                                                               new SortableBindingList<DbOptimization>(), this, document);

            if (lib != null)
            {
                textName.Text = _editingName = lib.Name;
                OpenDatabase(lib.DatabasePath);
            }

            comboType.Items.Add(ExportOptimize.CE);
            comboType.Items.Add(ExportOptimize.COV);
            comboType.SelectedIndex = 0;
        }

        public string ViewType
        {
            get { return comboType.SelectedItem != null ? comboType.SelectedItem.ToString() : null; }
            set { comboType.SelectedItem = value; }
        }

        public OptimizationType ViewDbType
        {
            get
            {
                if (Equals(ViewType, ExportOptimize.CE))
                    return OptimizationType.collision_energy;
                if (Equals(ViewType, ExportOptimize.DP))
                    return OptimizationType.declustering_potential;
                if (Equals(ViewType, ExportOptimize.COV))
                    return OptimizationType.compensation_voltage_fine;
                return OptimizationType.unknown;
            }
            set
            {
                if (Equals(value, OptimizationType.collision_energy))
                    ViewType = ExportOptimize.CE;
                else if (Equals(value, OptimizationType.declustering_potential))
                    ViewType = ExportOptimize.DP;
                else if (Equals(value, OptimizationType.compensation_voltage_fine))
                    ViewType = ExportOptimize.COV;
            }
        }

        private void OnLoad(object sender, EventArgs e)
        {
            // If you set this in the Designer, DataGridView has a defect that causes it to throw an
            // exception if the the cursor is positioned over the record selector column during loading.
            gridViewLibrary.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private bool DatabaseChanged
        {
            get
            {
                if (_original == null)
                    return LibraryOptimizations.Any();

                var dictOriginalOptimizations = _original.ToDictionary(opt => opt.Id);
                long countOptimizations = 0;
                foreach (var optimization in LibraryOptimizations)
                {
                    countOptimizations++;

                    // Any new optimization implies a change
                    if (!optimization.Id.HasValue)
                        return true;
                    // Any optimization that was not in the original set, or that has changed
                    DbOptimization originalOptimization;
                    if (!dictOriginalOptimizations.TryGetValue(optimization.Id, out originalOptimization) ||
                            !Equals(optimization, originalOptimization))
                        return true;
                }
                // Finally, check for optimizations removed
                return countOptimizations != _original.Length;
            }
        }

        public BindingList<DbOptimization> LibraryOptimizations { get { return _gridViewLibraryDriver.Optimizations; } }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            if (DatabaseChanged)
            {
                var result = MessageBox.Show(this, Resources.EditOptimizationLibraryDlg_btnOpen_Click_Are_you_sure_you_want_to_open_a_new_optimization_library_file__Any_changes_to_the_current_library_will_be_lost_,
                    Program.Name, MessageBoxButtons.YesNo);

                if (result != DialogResult.Yes)
                    return;
            }

            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Resources.EditOptimizationLibraryDlg_btnOpen_Click_Open_Optimization_Library,
                InitialDirectory = Settings.Default.ActiveDirectory,
                DefaultExt = OptimizationDb.EXT,
                Filter = TextUtil.FileDialogFiltersAll(OptimizationDb.FILTER_OPTDB)
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
                MessageDlg.Show(this, String.Format(Resources.EditOptimizationLibraryDlg_OpenDatabase_The_file__0__does_not_exist__Click_the_Create_button_to_create_a_new_library_or_the_Open_button_to_find_the_missing_file_,
                                                    path));
                return;
            }

            try
            {
                OptimizationDb db = OptimizationDb.GetOptimizationDb(path, null, _document);
                var dbOptimizations = db.GetOptimizations().ToArray();

                SetOptimizations(dbOptimizations);

                // Clone all of the optimizations to use for comparison in OkDialog
                _original = dbOptimizations.Select(p => new DbOptimization(p)).ToArray();

                textDatabase.Text = path;
            }
            catch (OptimizationsOpeningException e)
            {
                MessageDlg.ShowException(this, e);
            }
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            if (DatabaseChanged)
            {
                var result = MessageBox.Show(this, Resources.EditOptimizationLibraryDlg_btnCreate_Click_Are_you_sure_you_want_to_create_a_new_optimization_library_file__Any_changes_to_the_current_library_will_be_lost_,
                    Program.Name, MessageBoxButtons.YesNo);

                if (result != DialogResult.Yes)
                    return;
            }

            using (var dlg = new SaveFileDialog
            {
                Title = Resources.EditOptimizationLibraryDlg_btnCreate_Click_Create_Optimization_Library,
                InitialDirectory = Settings.Default.ActiveDirectory,
                OverwritePrompt = true,
                DefaultExt = OptimizationDb.EXT,
                Filter = TextUtil.FileDialogFiltersAll(OptimizationDb.FILTER_OPTDB)
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);

                    CreateDatabase(dlg.FileName);
                    textDatabase.Focus();
                    SetOptimizations(new DbOptimization[0]);
                    _original = null;
                }
            }
        }

        public void SetOptimizations(IEnumerable<DbOptimization> optimizations)
        {
            _gridViewLibraryDriver.SetOptimizations(optimizations);
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
                MessageDlg.ShowException(this, x);
                return;
            }

            //Create file, initialize db
            try
            {
                OptimizationDb.CreateOptimizationDb(path);

                textDatabase.Text = path;
            }
            catch (Exception x)
            {
                var message = TextUtil.LineSeparate(string.Format(Resources.EditOptimizationLibraryDlg_CreateDatabase_The_file__0__could_not_be_created_, path),
                                                    x.Message);
                MessageDlg.ShowWithException(this, message, x);
            }
        }

        public void OkDialog()
        {
            if (string.IsNullOrEmpty(textName.Text))
            {
                MessageDlg.Show(this, Resources.EditOptimizationLibraryDlg_OkDialog_Please_enter_a_name_for_the_optimization_library_);
                textName.Focus();
                return;
            }

            if (_existing != null)
            {
                foreach (var existing in _existing)
                {
                    if (Equals(existing.Name, textName.Text) && !Equals(existing.Name, _editingName))
                    {
                        if (MessageBox.Show(this, string.Format(Resources.EditOptimizationLibraryDlg_OkDialog_A_library_with_the_name__0__already_exists__Do_you_want_to_overwrite_it_,
                                                                textName.Text),
                                            Program.Name, MessageBoxButtons.YesNo) != DialogResult.Yes)
                        {
                            textName.Focus();
                            return;
                        }
                    }
                }
            }

            string message;
            if (string.IsNullOrEmpty(textDatabase.Text))
            {
                message = TextUtil.LineSeparate(Resources.EditOptimizationLibraryDlg_OkDialog_Please_choose_a_library_file_for_the_optimization_library_,
                                                Resources.EditOptimizationLibraryDlg_OkDialog_Click_the_Create_button_to_create_a_new_library_or_the_Open_button_to_open_an_existing_library_file_);
                MessageDlg.Show(this, message);
                textDatabase.Focus();
                return;
            }
            string path = Path.GetFullPath(textDatabase.Text);
            if (!Equals(path, textDatabase.Text))
            {
                message = TextUtil.LineSeparate(Resources.EditOptimizationLibraryDlg_OkDialog_Please_use_a_full_path_to_a_library_file_for_the_optimization_library_,
                                                Resources.EditOptimizationLibraryDlg_OkDialog_Click_the_Create_button_to_create_a_new_library_or_the_Open_button_to_open_an_existing_library_file_);
                MessageDlg.Show(this, message);
                textDatabase.Focus();
                return;
            }
            if (!string.Equals(Path.GetExtension(path), OptimizationDb.EXT))
                path += OptimizationDb.EXT;

            if (!ValidateOptimizationList(LibraryOptimizations, Resources.EditOptimizationLibraryDlg_OkDialog_library))
            {
                gridViewLibrary.Focus();
                return;
            }

            try
            {
                var library = new OptimizationLibrary(textName.Text, path);

                OptimizationDb db = File.Exists(path)
                               ? OptimizationDb.GetOptimizationDb(path, null, _document)
                               : OptimizationDb.CreateOptimizationDb(path);

                db = db.UpdateOptimizations(LibraryOptimizations.ToArray(), _original ?? new DbOptimization[0]);

                Library = library.ChangeDatabase(db);
            }
            catch (OptimizationsOpeningException x)
            {
                MessageDlg.ShowException(this, x);
                textDatabase.Focus();
                return;
            }
            catch (StaleStateException)
            {
                // CONSIDER: Not sure if this is the right thing to do.  It would
                //           be nice to solve whatever is causing this, but this is
                //           better than showing an unexpected error form with stack trace.
                MessageDlg.Show(this, Resources.EditOptimizationLibraryDlg_OkDialog_Failure_updating_optimizations_in_the_optimization_library__The_database_may_be_out_of_synch_);
                return;
            }

            DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// At this point a failure in this function probably means the optimization database was used
        /// </summary>
        private bool ValidateOptimizationList(IEnumerable<DbOptimization> optimizationList, string tableName)
        {
            var keySet = new HashSet<OptimizationKey>();
            foreach (DbOptimization optimization in optimizationList)
            {
                string seqModified = optimization.PeptideModSeq;
                if (LibraryGridViewDriver.ValidateSequence(seqModified) != null)
                {
                    MessageDlg.Show(this, string.Format(Resources.EditOptimizationLibraryDlg_ValidateOptimizationList_The_value__0__is_not_a_valid_modified_peptide_sequence_,
                                                        seqModified));
                    return false;
                }

                if (keySet.Contains(optimization.Key))
                {
                    MessageDlg.Show(this, string.Format(Resources.EditOptimizationLibraryDlg_ValidateOptimizationList_The_optimization_with_sequence__0___charge__1___fragment_ion__2__and_product_charge__3__appears_in_the__4__table_more_than_once_,
                                                        seqModified, optimization.Charge, optimization.FragmentIon, optimization.ProductCharge, tableName));
                    return false;
                }
                keySet.Add(optimization.Key);
            }

            return true;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public class LibraryGridViewDriver : PeptideGridViewDriver<DbOptimization>
        {
            private const int COLUMN_PRODUCT_ION = 1;
            private const int COLUMN_VALUE = 2;
            private const int COLUMN_TYPE = 3;

            private readonly EditOptimizationLibraryDlg _form;
            private readonly SrmDocument _document;

            public BindingList<DbOptimization> Optimizations { get; private set; }

            private static readonly Regex RGX_PRODUCT_ION = new Regex(@"^[\s]*(?<ion>precursor|[abcxyz][\d]+)[\s]*(?:-[\s]*(?<loss>[\d]+(?:[\.,][\d]+)?)[\s]*)?$", RegexOptions.IgnoreCase); // Not L10N

            public LibraryGridViewDriver(DataGridViewEx gridView, BindingSource bindingSource,
                                         SortableBindingList<DbOptimization> items, EditOptimizationLibraryDlg form, SrmDocument document)
                : base(gridView, bindingSource, items)
            {
                gridView.UserDeletingRow += gridView_UserDeletingRow;
                gridView.UserDeletedRow += gridView_UserDeletedRow;
                gridView.CellFormatting += gridView_CellFormatting;
                gridView.CellParsing += gridView_CellParsing;
                _form = form;
                _document = document;
                Optimizations = new BindingList<DbOptimization>();
                Optimizations.ListChanged += OptimizationsChanged;
            }

            private string ViewType { get { return _form.ViewType; } }
            private OptimizationType ViewDbType { get { return _form.ViewDbType; } }

            private void OptimizationsChanged(object sender, ListChangedEventArgs e)
            {
                var list = sender as BindingList<DbOptimization>;
                if (list == null)
                    return;

                switch (e.ListChangedType)
                {
                    case ListChangedType.ItemAdded:
                        var newItem = list[e.NewIndex];
                        if (newItem.Type.Equals((int)ViewDbType))
                        {
                            Items.Add(newItem);
                            _form.UpdateNumOptimizations();
                        }
                        break;
                    case ListChangedType.Reset:
                        UpdateView();
                        break;
                }
            }

            public void SetOptimizations(IEnumerable<DbOptimization> optimizations)
            {
                Optimizations.RaiseListChangedEvents = false;
                Optimizations.Clear();
                Array.ForEach(optimizations.ToArray(), opt => Optimizations.Add(opt));
                Optimizations.RaiseListChangedEvents = true;
                Optimizations.ResetBindings();
            }

            public void UpdateView()
            {
                // Update the grid view to show the appropriate headers and optimizations based on the combobox
                GridView.Columns[COLUMN_VALUE].HeaderText = ViewType;
                GridView.Columns[COLUMN_PRODUCT_ION].Visible = !Equals(ViewType, ExportOptimize.COV);

                Items.Clear();
                Items.RaiseListChangedEvents = false;
                Array.ForEach(Optimizations.Where(opt => opt.Type.Equals((int)ViewDbType)).ToArray(), opt => Items.Add(opt));
                Items.RaiseListChangedEvents = true;
                Items.ResetBindings();

                _form.UpdateNumOptimizations();
            }

            private void gridView_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
            {
                var optimization = e.Row.DataBoundItem as DbOptimization;
                if (optimization == null)
                    return;

                Optimizations.Remove(optimization);
            }

            private void gridView_UserDeletedRow(object sender, DataGridViewRowEventArgs e)
            {
                _form.UpdateNumOptimizations();
            }

            private void gridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
            {
                DbOptimization optimization = Items[e.RowIndex];
                switch (e.ColumnIndex)
                {
                    case COLUMN_SEQUENCE:
                        {
                            if (!string.IsNullOrWhiteSpace(optimization.PeptideModSeq))
                            {
                                e.Value = (optimization.Charge > 1)
                                    ? optimization.PeptideModSeq + Transition.GetChargeIndicator(optimization.Charge)
                                    : optimization.PeptideModSeq;
                                e.FormattingApplied = true;
                            }
                        }
                        break;
                    case COLUMN_PRODUCT_ION:
                        {
                            if (!string.IsNullOrWhiteSpace(optimization.FragmentIon))
                            {
                                e.Value = (optimization.ProductCharge > 1)
                                    ? (optimization.FragmentIon + Transition.GetChargeIndicator(optimization.ProductCharge))
                                    : optimization.FragmentIon;
                                e.FormattingApplied = true;
                            }
                        }
                        break;
                }
            }

            private void gridView_CellParsing(object sender, DataGridViewCellParsingEventArgs e)
            {
                string value = e.Value.ToString();
                if (string.IsNullOrWhiteSpace(value))
                    return;
                value = value.Trim();

                switch (e.ColumnIndex)
                {
                    case COLUMN_SEQUENCE:
                        {
                            e.Value = Transition.StripChargeIndicators(value, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE);
                            Items[e.RowIndex].Charge = Transition.GetChargeFromIndicator(value, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE).GetValueOrDefault(1);
                            e.ParsingApplied = true;
                        }
                        break;
                    case COLUMN_PRODUCT_ION:
                        {
                            e.Value = NormalizeProductIon(Transition.StripChargeIndicators(value, Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE));
                            Items[e.RowIndex].ProductCharge = Transition.GetChargeFromIndicator(value, Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE).GetValueOrDefault(1);
                            e.ParsingApplied = true;
                        }
                        break;
                }
            }

            public DbOptimization GetOptimization(OptimizationType type, string sequence, int charge, string fragmentIon, int productCharge)
            {
                DbOptimization[] optimizations =
                    Optimizations.Where(opt => Equals(opt.Type, (int)type) && Equals(opt.PeptideModSeq, sequence) &&
                        Equals(opt.Charge, charge) && Equals(opt.FragmentIon, fragmentIon) && Equals(opt.ProductCharge, productCharge)).ToArray();
                return optimizations.Count() == 1 ? optimizations.First() : null;
            }

            protected override void DoPaste()
            {
                var libraryOptimizationsNew = new List<DbOptimization>();
                bool add = false;

                if (Equals(ViewType, ExportOptimize.CE))
                {
                    add = GridView.DoPaste(MessageParent, ValidateOptimizationRow,
                        values => libraryOptimizationsNew.Add(new DbOptimization(ViewDbType,
                            Transition.StripChargeIndicators(values[0], TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE),
                            Transition.GetChargeFromIndicator(values[0], TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE).GetValueOrDefault(1),
                            NormalizeProductIon(Transition.StripChargeIndicators(values[1], Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE)),
                            Transition.GetChargeFromIndicator(values[1], Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE).GetValueOrDefault(1),
                            double.Parse(values[2]))));
                }
                else if (Equals(ViewType, ExportOptimize.COV))
                {
                    add = GridView.DoPaste(MessageParent, ValidateOptimizationRow,
                        values => libraryOptimizationsNew.Add(new DbOptimization(ViewDbType,
                            Transition.StripChargeIndicators(values[0], TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE),
                            Transition.GetChargeFromIndicator(values[0], TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE).GetValueOrDefault(1),
                            null, 0, double.Parse(values[1]))));
                }

                if (add)
                {
                    AddToLibrary(libraryOptimizationsNew);
                }
            }

            public static string NormalizeProductIon(string ion)
            {
                MatchCollection matches = RGX_PRODUCT_ION.Matches(ion);
                if (matches.Count != 1)
                    return ion;

                string normalizedIon = matches[0].Groups["ion"].Value.ToLower(); // Not L10N
                IFormatProvider culture = LocalizationHelper.CurrentCulture;
                return (string.IsNullOrEmpty(matches[0].Groups["loss"].Value)) // Not L10N
                    ? normalizedIon
                    : string.Format(culture, "{0} -{1}", normalizedIon, Math.Round(double.Parse(matches[0].Groups["loss"].Value, culture), 1)); // Not L10N
            }

            private bool ValidateOptimizationRow(object[] columns, IWin32Window parent, int lineNumber)
            {
                if (columns.Length != GridView.Columns.Cast<DataGridViewColumn>().Count(column => column.Visible))
                {
                    MessageDlg.Show(parent, Resources.LibraryGridViewDriver_ValidateOptimizationRow_The_pasted_text_must_contain_the_same_number_of_columns_as_the_table_);
                    return false;
                }

                bool prodVisible = GridView.Columns[COLUMN_PRODUCT_ION].Visible;

                string seq = columns[0] as string;
                string prod = prodVisible ? columns[1] as string : null;
                string val = columns.Last() as string;
                string message;
                if (string.IsNullOrWhiteSpace(seq))
                {
                    message = string.Format(Resources.PeptideGridViewDriver_ValidateRow_Missing_peptide_sequence_on_line__0_, lineNumber);
                }
                else if (ValidateSequence(seq) != null)
                {
                    message = string.Format(Resources.PeptideGridViewDriver_ValidateRow_The_text__0__is_not_a_valid_peptide_sequence_on_line__1_, seq, lineNumber);
                }
                else if (prodVisible && string.IsNullOrWhiteSpace(prod))
                {
                    message = string.Format(Resources.LibraryGridViewDriver_ValidateOptimizationRow_Missing_product_ion_on_line__1_, prod, lineNumber);
                }
                else if (prodVisible && ValidateProductIon(prod) != null)
                {
                    message = string.Format(Resources.LibraryGridViewDriver_ValidateRow_Invalid_product_ion_format__0__on_line__1__, prod, lineNumber);
                }
                else
                {
                    double dVal;
                    if (string.IsNullOrWhiteSpace(val))
                    {
                        message = string.Format(Resources.PeptideGridViewDriver_ValidateRow_Missing_value_on_line__0_, lineNumber);
                    }
                    else if (!double.TryParse(val, out dVal))
                    {
                        message = string.Format(Resources.PeptideGridViewDriver_ValidateRow_Invalid_decimal_number_format__0__on_line__1_, val, lineNumber);
                    }
                    else
                    {
                        return true;
                    }
                }

                MessageDlg.Show(parent, message);
                return false;
            }

            protected override bool DoCellValidating(int rowIndex, int columnIndex, string value)
            {
                string errorText = null;
                if (columnIndex == COLUMN_SEQUENCE && GridView.IsCurrentCellInEditMode)
                {
                    string sequence = value;
                    errorText = ValidateSequence(sequence);
                }
                else if (columnIndex == COLUMN_PRODUCT_ION && GridView.IsCurrentCellInEditMode)
                {
                    string chargeText = value;
                    errorText = ValidateProductIon(chargeText);
                }
                else if (columnIndex == COLUMN_VALUE && GridView.IsCurrentCellInEditMode)
                {
                    string optimizedText = value;
                    errorText = ValidateOptimizedValue(optimizedText);
                }
                if (errorText == null && GridView.IsCurrentCellInEditMode &&
                    (columnIndex == COLUMN_SEQUENCE || columnIndex == COLUMN_PRODUCT_ION))
                {
                    var curRow = GridView.Rows[rowIndex].DataBoundItem as DbOptimization;
                    OptimizationKey curKey = curRow != null ? new OptimizationKey(curRow.Key) : new OptimizationKey();
                    switch (columnIndex)
                    {
                        case COLUMN_SEQUENCE:
                            curKey.PeptideModSeq = Transition.StripChargeIndicators(value, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE);
                            curKey.Charge = Transition.GetChargeFromIndicator(value, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE).GetValueOrDefault(1);
                            break;
                        case COLUMN_PRODUCT_ION:
                            curKey.FragmentIon = Transition.StripChargeIndicators(value, Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE);
                            curKey.ProductCharge = Transition.GetChargeFromIndicator(value, Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE).GetValueOrDefault(1);
                            break;
                    }
                    int iExist = Optimizations.ToArray().IndexOf(item => Equals(item.Key, curKey));
                    if (iExist != -1 && iExist != rowIndex)
                    {
                        errorText = string.Format(
                            Resources.LibraryGridViewDriver_DoCellValidating_There_is_already_an_optimization_with_sequence___0___and_product_ion___2___in_the_list_,
                            curKey.PeptideModSeq, Transition.GetChargeIndicator(curKey.Charge), curKey.FragmentIon, Transition.GetChargeIndicator(curKey.ProductCharge));
                    }
                }
                if (errorText != null)
                {
                    MessageDlg.Show(MessageParent, errorText);
                    return false;
                }

                return true;
            }

            public static string ValidateSequence(string sequenceText)
            {
                if (string.IsNullOrWhiteSpace(sequenceText))
                    return Resources.LibraryGridViewDriver_ValidateSequence_Sequence_cannot_be_empty_;
                if (!FastaSequence.IsExSequence(Transition.StripChargeIndicators(sequenceText, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE)))
                    return string.Format(Resources.EditOptimizationLibraryDlg_ValidateOptimizationList_The_value__0__is_not_a_valid_modified_peptide_sequence_, sequenceText);

                return null;
            }

            private static string ValidateProductIon(string productIonText)
            {
                if (string.IsNullOrWhiteSpace(productIonText))
                    return Resources.LibraryGridViewDriver_ValidateProductIon_Product_ion_cannot_be_empty_;

                string stripped = Transition.StripChargeIndicators(productIonText, Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE);
                if (!RGX_PRODUCT_ION.IsMatch(stripped))
                    return string.Format(Resources.LibraryGridViewDriver_ValidateProductIon_Product_ion__0__is_invalid_, stripped);

                return null;
            }

            private static string ValidateOptimizedValue(string optimizedText)
            {
                double optimizedValue;
                if (optimizedText == null || !double.TryParse(optimizedText, out optimizedValue))
                    return Resources.LibraryGridViewDriver_ValidateOptimizedValue_Optimized_values_must_be_valid_decimal_numbers_;
                return optimizedValue <= 0 ? Resources.LibraryGridViewDriver_ValidateOptimizedValue_Optimized_values_must_be_greater_than_zero_ : null;
            }

            protected override bool DoRowValidating(int rowIndex)
            {
                var row = GridView.Rows[rowIndex];
                if (row.IsNewRow)
                    return true;
                var cell = row.Cells[COLUMN_SEQUENCE];
                string errorText = ValidateSequence(cell.FormattedValue != null ? cell.FormattedValue.ToString() : null);
                if (errorText == null && !row.Cells[COLUMN_TYPE].Value.Equals((int) OptimizationType.compensation_voltage_fine))
                {
                    cell = row.Cells[COLUMN_PRODUCT_ION];
                    errorText = ValidateProductIon(cell.FormattedValue != null ? cell.FormattedValue.ToString() : null);
                }
                if (errorText == null)
                {
                    cell = row.Cells[COLUMN_VALUE];
                    errorText = ValidateOptimizedValue(cell.FormattedValue != null ? cell.FormattedValue.ToString() : null);
                }
                if (errorText != null)
                {
                    bool messageShown = false;
                    try
                    {
                        GridView.CurrentCell = cell;
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

            private static double GetCollisionEnergy(SrmSettings settings, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, CollisionEnergyRegression regression, int step)
            {
                int charge = nodeGroup.TransitionGroup.PrecursorCharge;
                double mz = settings.GetRegressionMz(nodePep, nodeGroup);
                return regression.GetCollisionEnergy(charge, mz) + regression.StepSize * step;
            }

            public void AddResults()
            {
                var settings = _document.Settings;
                if (!settings.HasResults)
                {
                    MessageDlg.Show(MessageParent, Resources.LibraryGridViewDriver_AddResults_The_active_document_must_contain_results_in_order_to_add_optimized_values_);
                    return;
                }

                var newOptimizations = new HashSet<DbOptimization>();

                foreach (PeptideGroupDocNode seq in _document.MoleculeGroups.Where(seq => seq.TransitionCount > 0 && !seq.IsDecoy))
                {
                    foreach (PeptideDocNode peptide in seq.Children)
                    {
                        foreach (TransitionGroupDocNode nodeGroup in peptide.Children)
                        {
                            string sequence = _document.Settings.GetSourceTextId(peptide);
                            int charge = nodeGroup.TransitionGroup.PrecursorCharge;
                            foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                            {
                                OptimizationKey key = null;
                                double? value = null;
                                if (Equals(ViewType, ExportOptimize.CE))
                                {
                                    key = new OptimizationKey(ViewDbType, sequence, charge, nodeTran.GetFragmentIonName(CultureInfo.InvariantCulture), nodeTran.Transition.Charge);
                                    value = OptimizationStep<CollisionEnergyRegression>.FindOptimizedValueFromResults(
                                            settings, peptide, nodeGroup, nodeTran, OptimizedMethodType.Transition, GetCollisionEnergy);
                                }
                                else if (Equals(ViewType, ExportOptimize.COV))
                                {
                                    key = new OptimizationKey(ViewDbType, sequence, charge, null, 0);
                                    double? cov = OptimizationStep<CompensationVoltageRegressionFine>.FindOptimizedValueFromResults(_document.Settings,
                                        peptide, nodeGroup, null, OptimizedMethodType.Precursor, SrmDocument.GetCompensationVoltageFine);
                                    value = cov.HasValue ? cov.Value : 0;
                                }
                                if (value.HasValue && value > 0)
                                {
                                    newOptimizations.Add(new DbOptimization(key, value.Value));
                                }
                            }
                        }
                    }
                }
                AddToLibrary(newOptimizations);
            }

            public void AddOptimizationLibrary(SrmDocument document)
            {
                string txtName = _form.textName.Text;
                string txtPath = _form.textDatabase.Text;
                var optLibs = Settings.Default.OptimizationLibraryList.Where(
                        lib => !lib.IsNone && !(Equals(txtName, lib.Name) && Equals(txtPath, lib.DatabasePath)));
                using (var addOptimizationLibraryDlg = new AddOptimizationLibraryDlg(optLibs))
                {
                    if (addOptimizationLibraryDlg.ShowDialog(MessageParent) == DialogResult.OK)
                    {
                        AddOptimizationLibrary(addOptimizationLibraryDlg.Library, document);
                    }
                }
            }

            private void AddOptimizationLibrary(OptimizationLibrary optLibrary, SrmDocument document)
            {
                IEnumerable<DbOptimization> optimizations = null;
                using (var longWait = new LongWaitDlg
                {
                    Text = Resources.LibraryGridViewDriver_AddOptimizationLibrary_Adding_optimization_library,
                    Message = string.Format(Resources.LibraryGridViewDriver_AddOptimizationLibrary_Adding_optimization_values_from__0_, optLibrary.DatabasePath),
                    FormBorderStyle = FormBorderStyle.Sizable
                })
                {
                    try
                    {
                        var status = longWait.PerformWork(MessageParent, 800, monitor =>
                        {
                            var optDb = OptimizationDb.GetOptimizationDb(optLibrary.DatabasePath, monitor, document);
                            if (optDb != null)
                            {
                                optimizations = optDb.GetOptimizations().Where(opt => Equals(opt.Type, (int)ViewDbType));
                            }
                        });
                        if (status.IsError)
                        {
                            MessageDlg.ShowException(MessageParent, status.ErrorException);
                        }
                    }
                    catch (Exception x)
                    {
                        var message = TextUtil.LineSeparate(string.Format(Resources.LibraryGridViewDriver_AddOptimizationLibrary_An_error_occurred_attempting_to_load_the_optimization_library_file__0__,
                                                                          optLibrary.DatabasePath),
                                                            x.Message);
                        MessageDlg.ShowWithException(MessageParent, message, x);
                    }
                }
                AddToLibrary(optimizations);
            }

            private void AddToLibrary(IEnumerable<DbOptimization> libraryOptimizationsNew)
            {
                if (libraryOptimizationsNew == null)
                {
                    return;
                }

                var dictLibraryIndices = new Dictionary<OptimizationKey, int>();
                for (int i = 0; i < Optimizations.Count; i++)
                {
                    // Sometimes the last item can be empty with no sequence.
                    if (Optimizations[i].Key != null)
                        dictLibraryIndices.Add(Optimizations[i].Key, i);
                }

                var listOptimizationsNew = libraryOptimizationsNew.ToList();
                var listChangedOptimizations = new List<OptimizationKey>();

                // Check for existing matching optimizations
                for (int i = listOptimizationsNew.Count - 1; i >= 0; i--)
                {
                    int optimizationIndex;
                    if (!dictLibraryIndices.TryGetValue(listOptimizationsNew[i].Key, out optimizationIndex))
                        continue;

                    if (Equals(listOptimizationsNew[i], Optimizations[optimizationIndex]))
                    {
                        listOptimizationsNew.RemoveAt(i);
                        continue;
                    }

                    listChangedOptimizations.Add(listOptimizationsNew[i].Key);
                }

                listChangedOptimizations.Sort();

                // If there were any matches, get user feedback
                AddOptimizationsAction action;
                using (var dlg = new AddOptimizationsDlg(listOptimizationsNew.Count -
                                                         listChangedOptimizations.Count,
                                                         listChangedOptimizations))
                {
                    if (dlg.ShowDialog(MessageParent) != DialogResult.OK)
                        return;
                    action = dlg.Action;
                }

                Optimizations.RaiseListChangedEvents = false;
                try
                {
                    // Add the new optimizations to the library list
                    foreach (var optimization in listOptimizationsNew)
                    {
                        OptimizationKey key = optimization.Key;
                        int optimizationIndex;
                        // Add any optimizations not yet in the library
                        if (!dictLibraryIndices.TryGetValue(key, out optimizationIndex))
                        {
                            optimization.Id = null;
                            Optimizations.Add(optimization);
                            continue;
                        }

                        var optimizationExist = Optimizations[optimizationIndex];
                        // Replace optimizations if the user said to
                        if (action == AddOptimizationsAction.replace)
                        {
                            optimizationExist.Value = optimization.Value;
                        }
                        // Skip optimizations if the user said to, or no change has occurred.
                        else if (action == AddOptimizationsAction.skip || Equals(optimization, optimizationExist))
                        {
                        }
                        // Average existing and new if that is what the user specified.
                        else if (action == AddOptimizationsAction.average)
                        {
                            optimizationExist.Value = (optimization.Value + optimizationExist.Value) / 2;
                        }
                    }
                }
                finally
                {
                    Optimizations.RaiseListChangedEvents = true;
                }
                Optimizations.ResetBindings();
            }
        }

        private void UpdateNumOptimizations()
        {
            int optCount = _gridViewLibraryDriver.Optimizations.Count(opt => opt.Type.Equals((int) ViewDbType));
            if (Equals(ViewType, ExportOptimize.CE))
            {
                labelNumOptimizations.Text = string.Format(optCount == 1
                    ? Resources.EditOptimizationLibraryDlg_UpdateNumOptimizations__0__optimized_collision_energy
                    : Resources.EditOptimizationLibraryDlg_UpdateNumOptimizations__0__optimized_collision_energies,
                    optCount);
            }
            else if (Equals(ViewType, ExportOptimize.DP))
            {
                labelNumOptimizations.Text = string.Format(optCount == 1
                    ? Resources.EditOptimizationLibraryDlg_UpdateNumOptimizations__0__optimized_declustering_potential
                    : Resources.EditOptimizationLibraryDlg_UpdateNumOptimizations__0__optimized_declustering_potentials,
                    optCount);
            }
            else if (Equals(ViewType, ExportOptimize.COV))
            {
                labelNumOptimizations.Text = string.Format(optCount == 1
                    ? Resources.EditOptimizationLibraryDlg_UpdateNumOptimizations__0__optimized_compensation_voltage
                    : Resources.EditOptimizationLibraryDlg_UpdateNumOptimizations__0__optimized_compensation_voltages,
                    optCount);
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            contextMenuAdd.Show(btnAdd, 0, btnAdd.Height + 2);
        }

        private void addFromResultsMenuItem_Click(object sender, EventArgs e)
        {
            AddResults();
        }

        public void AddResults()
        {
            _gridViewLibraryDriver.AddResults();
        }

        private void addFromFileMenuItem_Click(object sender, EventArgs e)
        {
            AddOptimizationDatabase(_document);
        }

        public void AddOptimizationDatabase()
        {
            AddOptimizationDatabase(null);
        }

        public void AddOptimizationDatabase(SrmDocument document)
        {
            _gridViewLibraryDriver.AddOptimizationLibrary(document);
        }

        private void comboType_SelectedIndexChanged(object sender, EventArgs e)
        {
            _gridViewLibraryDriver.UpdateView();
        }

        #region Functional Test Support
        public string LibName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public void DoPasteLibrary()
        {
            _gridViewLibraryDriver.OnPaste();
        }

        public DbOptimization GetCEOptimization(string sequence, int charge, string fragmentIon, int productCharge)
        {
            return _gridViewLibraryDriver.GetOptimization(OptimizationType.collision_energy, sequence, charge, fragmentIon, productCharge);
        }
        #endregion
    }
}
