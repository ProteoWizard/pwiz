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
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
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
            _gridViewStandardDriver.GridLibrary = gridViewLibrary;
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
                    textDatabase.Focus();
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
                Title = "Open iRT Database",
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
                    textDatabase.Focus();
                }
            }
        }

        public void OpenDatabase(string path)
        {
            if (!File.Exists(path))
            {
                MessageDlg.Show(this, String.Format("The file {0} does not exist. Click the Create button to create a new database or the Open button to find the missing file.", path));
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
            if(string.IsNullOrEmpty(textCalculatorName.Text))
            {
                MessageDlg.Show(this, "Please enter a name for the iRT calculator.");
                textCalculatorName.Focus();
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
                        {
                            textCalculatorName.Focus();
                            return;                            
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(textDatabase.Text))
            {
                MessageDlg.Show(this, "Please choose a database file for the iRT calculator.\nClick the Create button to create a new database or the Open button to open an existing database file.");
                textDatabase.Focus();
                return;
            }
            string path = Path.GetFullPath(textDatabase.Text);
            if (!Equals(path, textDatabase.Text))
            {
                MessageDlg.Show(this, "Please use a full path to a database file for the iRT calculator.\nClick the Create button to create a new database or the Open button to open an existing database file.");
                textDatabase.Focus();
                return;
            }
            if (!string.Equals(Path.GetExtension(path), IrtDb.EXT_IRTDB))
                path += IrtDb.EXT_IRTDB;

            //This function MessageBox.Show's error messages
            if(!ValidatePeptideList(StandardPeptideList, STANDARD_TABLE_NAME))
            {
                gridViewStandard.Focus();
                return;
            }
            if(!ValidatePeptideList(LibraryPeptideList, LIBRARY_TABLE_NAME))
            {
                gridViewLibrary.Focus();
                return;                
            }

            if (StandardPeptideList.Count < CalibrateIrtDlg.MIN_STANDARD_PEPTIDES)
            {
                MessageDlg.Show(this, string.Format("Please enter at least {0} standard peptides.", CalibrateIrtDlg.MIN_STANDARD_PEPTIDES));
                gridViewStandard.Focus();
                return;
            }

            if (StandardPeptideList.Count < CalibrateIrtDlg.MIN_SUGGESTED_STANDARD_PEPTIDES)
            {
                DialogResult result = MessageBox.Show(this, string.Format("Using fewer than {0} standard peptides is not recommended. Are you sure you want to continue with only {1}?", CalibrateIrtDlg.MIN_SUGGESTED_STANDARD_PEPTIDES, StandardPeptideList.Count),
                                                      Program.Name, MessageBoxButtons.YesNo);
                if (result != DialogResult.Yes)
                {
                    gridViewStandard.Focus();
                    return;
                }
            }

            try
            {
                var calculator = new RCalcIrt(textCalculatorName.Text, path);

                IrtDb db;
                if (File.Exists(path))
                    db = IrtDb.GetIrtDb(path, null);   // CONSIDER: LongWaitDlg?
                else
                    db = IrtDb.CreateIrtDb(path);

                db = db.UpdatePeptides(AllPeptides, _originalPeptides ?? new DbIrtPeptide[0]);

                Calculator = calculator.ChangeDatabase(db);
            }
            catch(DatabaseOpeningException x)
            {
                MessageDlg.Show(this, x.Message);
                textDatabase.Focus();
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

        private void btnPeptides_Click(object sender, EventArgs e)
        {
            using (var changeDlg = new ChangeIrtPeptidesDlg(AllPeptides))
            {
                if (changeDlg.ShowDialog(this) == DialogResult.OK)
                {
                    _gridViewStandardDriver.Reset(changeDlg.Peptides.OrderBy(peptide => peptide.Irt).ToArray());
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
            if (StandardPeptideCount < CalibrateIrtDlg.MIN_STANDARD_PEPTIDES)
            {
                MessageDlg.Show(this, string.Format("Please enter at least {0} standard peptides.", CalibrateIrtDlg.MIN_STANDARD_PEPTIDES));
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

        private void AddLibrary()
        {
            _gridViewLibraryDriver.AddSpectralLibrary();
        }

        private void addIRTDatabaseContextMenuItem_Click(object sender, EventArgs e)
        {
            AddIrtDatabase();
        }

        public void AddIrtDatabase()
        {
            throw new NotImplementedException();
        }

        private class StandardGridViewDriver : PeptideGridViewDriver<DbIrtPeptide>
        {
            public StandardGridViewDriver(DataGridViewEx gridView, BindingSource bindingSource,
                                          SortableBindingList<DbIrtPeptide> items)
                : base(gridView, bindingSource, items)
            {
                AllowNegativeTime = true;
            }

            public DataGridView GridLibrary { private get; set; }

            /// <summary>
            /// The associated library peptide list, set in the dialog constructor
            /// </summary>
            public BindingList<DbIrtPeptide> LibraryPeptideList { private get; set; }

            protected override void DoPaste()
            {
                var standardPeptidesNew = new List<DbIrtPeptide>();
                GridView.DoPaste(MessageParent, ValidateRow, values =>
                    standardPeptidesNew.Add(new DbIrtPeptide(values[0], double.Parse(values[1]), true, TimeSource.peak)));

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
                    string sequence = peptide.PeptideModSeq;
                    DbIrtPeptide peptideExist;
                    int iPep;
                    if ((iPep = LibraryPeptideList.IndexOf(p => Equals(p.PeptideModSeq, sequence))) != -1)
                    {
                        peptideExist = LibraryPeptideList[iPep];                        
                        // Remove from the library list, so that it is in only one list
                        LibraryPeptideList.RemoveAt(iPep);
                    }
                    else if ((iPep = Items.IndexOf(p => Equals(p.PeptideModSeq, sequence))) != -1)
                    {
                        peptideExist = Items[iPep];
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
                                        let sequence = standardPeptide.PeptideModSeq
                                        where sequence != null &&
                                            !standardPeptidesNew.Any(p => Equals(p.PeptideModSeq, sequence))
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

            /// <summary>
            /// The associated standard peptide list, set in the dialog constructor
            /// </summary>
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

                AddToLibrary(libraryPeptidesNew, null, null);
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

                AddRetetionTimes(GetRetentionTimeProviders(document));
            }

            private static IEnumerable<IRetentionTimeProvider> GetRetentionTimeProviders(SrmDocument document)
            {
                return document.Settings.MeasuredResults.MSDataFileInfos.Select(fileInfo =>
                    new DocumentRetentionTimeProvider(document, fileInfo)).Cast<IRetentionTimeProvider>();
            }

            private sealed class DocumentRetentionTimeProvider : IRetentionTimeProvider
            {
                private readonly Dictionary<string, double> _dictPeptideRetentionTime;

                public DocumentRetentionTimeProvider(SrmDocument document, ChromFileInfo fileInfo)
                {
                    _dictPeptideRetentionTime = new Dictionary<string, double>();
                    foreach (var nodePep in document.Peptides)
                    {
                        string modSeq = document.Settings.GetModifiedSequence(nodePep, IsotopeLabelType.light);
                        float? centerTime = nodePep.GetPeakCenterTime(fileInfo);
                        if (centerTime.HasValue)
                            _dictPeptideRetentionTime.Add(modSeq, centerTime.Value);
                    }

                }

                public double? GetRetentionTime(string sequence)
                {
                    double time;
                    if (_dictPeptideRetentionTime.TryGetValue(sequence, out time))
                        return time;
                    return null;
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
                var library = libraryManager.TryGetLibrary(librarySpec);
                if (library == null)
                {
                    using (var longWait = new LongWaitDlg
                        {
                            Text = "Loading Library",
                            Message = string.Format("Loading library from {0}", librarySpec.FilePath)
                        })
                    {
                        try
                        {
                            var status = longWait.PerformWork(MessageParent, 800, monitor =>
                            {
                                library = librarySpec.LoadLibrary(new ViewLibraryDlg.ViewLibLoadMonitor(monitor));
                            });
                            if (status.IsError)
                            {
                                MessageBox.Show(MessageParent, status.ErrorException.Message, Program.Name);
                                return;
                            }
                        }
                        catch (Exception x)
                        {
                            MessageDlg.Show(MessageParent, string.Format("An error occurred attempting to load the {0} library.\n{1}", librarySpec.Name, x.Message));
                            return;
                        }
                    }
                }

                var retentionTimeProviders = GetRetentionTimeProviders(library).ToArray();
                if (retentionTimeProviders.Length == 0)
                {
                    MessageDlg.Show(MessageParent, string.Format("The library {0} does not contain retention time information.", librarySpec.Name));
                    return;
                }

                // TODO: This also needs a LongWaitDlg.  Should share same dialog with above
                AddRetetionTimes(retentionTimeProviders);
            }

            private static IEnumerable<IRetentionTimeProvider> GetRetentionTimeProviders(Library library)
            {
                int? fileCount = library.FileCount;
                if (!fileCount.HasValue)
                    yield break;

                for (int i = 0; i < fileCount.Value; i++)
                {
                    LibraryRetentionTimes retentionTimes;
                    if (library.TryGetRetentionTimes(i, out retentionTimes))
                        yield return retentionTimes;
                }
            }

            private void AddRetetionTimes(IEnumerable<IRetentionTimeProvider> providers)
            {
                var dictPeptideAverages = new Dictionary<string, IrtPeptideAverages>();
                int runCount = 0;
                int regressionLineCount = 0;
                foreach (var retentionTimeProvider in providers)
                {
                    runCount++;
                    var regressionLine = CalcRegressionLine(retentionTimeProvider);
                    if (regressionLine == null)
                        continue;
                    regressionLineCount++;
                    AddRetentionTimesToDict(retentionTimeProvider, regressionLine, dictPeptideAverages);
                }

                if (regressionLineCount == 0)
                {
                    if (runCount == 1)
                        MessageDlg.Show(MessageParent, "A single run does not have high enough correlation to the existing iRT values to allow retention time conversion.");
                    else
                        MessageDlg.Show(MessageParent, string.Format("None of {0} runs were found with high enough correlation to the existing iRT values to allow retention time conversion.", runCount));
                    return;
                }
                AddToLibrary(from pepAverage in dictPeptideAverages.Values
                             orderby pepAverage.IrtAverage
                             select new DbIrtPeptide(pepAverage.PeptideModSeq, pepAverage.IrtAverage, false, TimeSource.scan),
                             runCount,
                             regressionLineCount);
            }

            private const double MIN_IRT_TO_TIME_CORRELATION = 0.99;
            private const int MIN_IRT_TO_TIME_POINT_COUNT = 20;

            private IRegressionFunction CalcRegressionLine(IRetentionTimeProvider retentionTimes)
            {
                var listPeptides = StandardPeptideList.OrderBy(peptide => peptide.Irt).ToArray();

                var listTimes = new List<double>();
                foreach (var standardPeptide in listPeptides)
                {
                    double? time = retentionTimes.GetRetentionTime(standardPeptide.PeptideModSeq);
                    if (!time.HasValue)
                        continue;
                    listTimes.Add(time.Value);
                }
                var listIrts = listPeptides.Select(peptide => peptide.Irt).ToList();
                if (listTimes.Count == StandardPeptideList.Count)
                {
                    var statTimes = new Statistics(listTimes);
                    var statIrts = new Statistics(listIrts);
                    double correlation = statIrts.R(statTimes);
                    // If the correlation is not good enough, try removing one value to
                    // fix the problem.)
                    if (correlation < MIN_IRT_TO_TIME_CORRELATION)
                    {
                        double? time = null, irt = null;
                        for (int i = 0; i < listTimes.Count; i++)
                        {
                            statTimes = GetTrial(listTimes, i, ref time);
                            statIrts = GetTrial(listIrts, i, ref irt);
                            correlation = statIrts.R(statTimes);
                            if (correlation >= MIN_IRT_TO_TIME_CORRELATION)
                                break;
                        }
                    }
                    if (correlation >= MIN_IRT_TO_TIME_CORRELATION)
                        return new RegressionLine(statIrts.Slope(statTimes), statIrts.Intercept(statTimes));
                }

                // TODO: Attempt to find a usable regression among non-standard peptides
                return null;
            }

            private static Statistics GetTrial(IList<double> listValues, int i, ref double? valueReplace)
            {
                if (valueReplace.HasValue)
                    listValues.Insert(i-1, valueReplace.Value);
                valueReplace = listValues[i];
                listValues.RemoveAt(i);
                return new Statistics(listValues);
            }

            private void AddRetentionTimesToDict(IRetentionTimeProvider retentionTimes,
                                                          IRegressionFunction regressionLine,
                                                          IDictionary<string, IrtPeptideAverages> dictPeptideAverages)
            {
                var setStandards = new HashSet<string>(StandardPeptideList.Select(peptide => peptide.PeptideModSeq));
                foreach (var pepTime in retentionTimes.PeptideRetentionTimes.Where(p => !setStandards.Contains(p.PeptideSequence)))
                {
                    string peptideModSeq = pepTime.PeptideSequence;
                    double irt = regressionLine.GetY(pepTime.RetentionTime);
                    IrtPeptideAverages pepAverage;
                    if (!dictPeptideAverages.TryGetValue(peptideModSeq, out pepAverage))
                        dictPeptideAverages.Add(peptideModSeq, new IrtPeptideAverages(peptideModSeq, irt));
                    else
                        pepAverage.AddIrt(irt);
                }
            }

            private class IrtPeptideAverages
            {
                public IrtPeptideAverages(string peptideModSeq, double irtAverage)
                {
                    PeptideModSeq = peptideModSeq;
                    IrtAverage = irtAverage;
                    RunCount = 1;
                }

                public string PeptideModSeq { get; private set; }
                public double IrtAverage { get; private set; }
                private int RunCount { get; set; }

                public void AddIrt(double irt)
                {
                    RunCount++;
                    IrtAverage += (irt - IrtAverage) / RunCount;
                }
            }

            private void AddToLibrary(IEnumerable<DbIrtPeptide> libraryPeptidesNew, int? runCount, int? includedCouunt)
            {
                var dictLibraryIndices = new Dictionary<string, int>();
                for (int i = 0; i < Items.Count; i++)
                {
                    // Sometimes the last item can be empty with no sequence.
                    if (Items[i].PeptideModSeq != null)
                        dictLibraryIndices.Add(Items[i].PeptideModSeq, i);
                }

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
            bool hasLibraryPeptides = LibraryPeptideList.Count != 0;
            btnCalibrate.Text = (hasLibraryPeptides ? "Recali&brate..." : "Cali&brate...");
            btnPeptides.Visible = hasLibraryPeptides;

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

        #endregion
    }
}