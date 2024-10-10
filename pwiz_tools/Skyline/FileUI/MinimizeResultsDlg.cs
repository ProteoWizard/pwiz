
/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    /// <summary>
    /// Displays UI for choosing settings for reducing the size of the chromatogram cache file
    /// by removing unused transition and limiting the length along the time axis.
    /// </summary>
    public partial class MinimizeResultsDlg : FormEx
    {
        private readonly BindingList<GridRowItem> _rowItems;
        private MinimizeResults _minimizeResults;
        private ChromCacheMinimizer.MinStatistics _minStatistics;
        private bool _updatePending;

        private ILongWaitBroker _longWaitBroker;

        public MinimizeResultsDlg(IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            _minimizeResults = new MinimizeResults(documentUIContainer, OnProgress);
            Settings = _minimizeResults.Settings
                .ChangeDiscardUnmatchedChromatograms(true);
            DocumentUIContainer = documentUIContainer;
            bindingSource1.DataSource = _rowItems = new BindingList<GridRowItem>();
            _minimizeResults.StartStatisticsCollection();
        }

        public IDocumentUIContainer DocumentUIContainer { get; private set; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (DocumentUIContainer != null)
            {
                DocumentUIContainer.ListenUI(OnDocumentChanged);
                UpdateMinimizeButtons();
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            if (DocumentUIContainer != null)
            {
                DocumentUIContainer.UnlistenUI(OnDocumentChanged);
            }
        }

        private void OnLoad(object sender, EventArgs e)
        {
            // If you set this in the Designer, DataGridView has a defect that causes it to throw an
            // exception if the the cursor is positioned over the record selector column during loading.
            dataGridViewSizes.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private void OnDocumentChanged(object sender, DocumentChangedEventArgs args)
        {
            if (!_minimizeResults.BlockStatisticsCollection)
            {
                _minimizeResults.SetDocument(DocumentUIContainer.DocumentUI);
                UpdateMinimizeButtons();
            }
        }


        private bool _changingOptimizeSettings;
        public ChromCacheMinimizer.Settings Settings
        {
            get
            {
                return _minimizeResults.Settings;
            }
            set
            {
                if (Equals(value, Settings))
                {
                    return;
                }
                if (_changingOptimizeSettings)
                {
                    return;
                }
                try
                {
                    _changingOptimizeSettings = true;
                    _minimizeResults.Settings = value;
                    // ReSharper disable once PossibleNullReferenceException
                    cbxDiscardUnmatchedChromatograms.Checked = Settings.DiscardUnmatchedChromatograms;
                    if (Settings.NoiseTimeRange.HasValue)
                    {
                        tbxNoiseTimeRange.Text = Settings.NoiseTimeRange.Value.ToString(LocalizationHelper.CurrentCulture);
                        tbxNoiseTimeRange.Enabled = true;
                        cbxLimitNoiseTime.Checked = true;
                    }
                    else
                    {
                        tbxNoiseTimeRange.Enabled = false;
                        cbxLimitNoiseTime.Checked = false;
                    }
                }
                finally
                {
                    _changingOptimizeSettings = false;
                }
            }
        }

        private void UpdateMinimizeButtons()
        {
            if (_minimizeResults.ChromCacheMinimizer != null)
            {
                btnMinimize.Enabled = btnMinimizeAs.Enabled = true;
            }
            else
            {
                btnMinimize.Enabled = btnMinimizeAs.Enabled = false;
                lblCurrentCacheFileSize.Text = FileUIResources.MinimizeResultsDlg_ChromCacheMinimizer_The_cache_file_has_not_been_loaded_yet;
                lblSpaceSavings.Text = string.Empty;
            }
        }

        private void cbxDiscardUnmatchedChromatograms_CheckedChanged(object sender, EventArgs e)
        {
            Settings = Settings.ChangeDiscardUnmatchedChromatograms(cbxDiscardUnmatchedChromatograms.Checked);
        }

        private void tbxNoiseTimeRange_Leave(object sender, EventArgs e)
        {
            if (DialogResult != DialogResult.None)
                return;

            double noiseTime;
            string errorMessage = null;
            if (!double.TryParse(tbxNoiseTimeRange.Text, out noiseTime))
                errorMessage = FileUIResources.MinimizeResultsDlg_tbxNoiseTimeRange_Leave_The_noise_time_limit_must_be_a_valid_decimal_number;
            if (noiseTime < 0)
                errorMessage = FileUIResources.MinimizeResultsDlg_tbxNoiseTimeRange_Leave_The_noise_time_limit_must_be_a_positive_decimal_number;
            if (errorMessage != null)
            {
                MessageDlg.Show(this, errorMessage);
                tbxNoiseTimeRange.Focus();
                tbxNoiseTimeRange.SelectAll();
                return;
            }

            Settings = Settings.ChangeNoiseTimeRange(noiseTime);
        }

        private void cbxLimitNoiseTime_CheckedChanged(object sender, EventArgs e)
        {
            Settings = cbxLimitNoiseTime.Checked
                           ? Settings.ChangeNoiseTimeRange(double.Parse(tbxNoiseTimeRange.Text))
                           : Settings.ChangeNoiseTimeRange(null);
        }

        private void btnMinimize_Click(object sender, EventArgs e)
        {
            Minimize(false);
        }

        private void btnMinimizeAs_Click(object sender, EventArgs e)
        {
            Minimize(true);
        }

        public void Minimize(bool asNewFile)
        {
            var document = DocumentUIContainer.DocumentUI;
            if (!document.Settings.MeasuredResults.IsLoaded)
            {
                MessageDlg.Show(this, FileUIResources.MinimizeResultsDlg_Minimize_All_results_must_be_completely_imported_before_any_can_be_minimized);
                return;
            }
            if (!Settings.DiscardUnmatchedChromatograms && !Settings.NoiseTimeRange.HasValue)
            {
                if (MultiButtonMsgDlg.Show(this, 
                    FileUIResources.MinimizeResultsDlg_Minimize_You_have_not_chosen_any_options_to_minimize_your_cache_file_Are_you_sure_you_want_to_continue, 
                    MessageBoxButtons.OKCancel) != DialogResult.OK)
                {
                    return;
                }
            }
            string targetFile = DocumentUIContainer.DocumentFilePath;

            if (asNewFile || string.IsNullOrEmpty(targetFile))
            {
                using (var saveFileDialog =
                    new SaveFileDialog())
                {
                    saveFileDialog.InitialDirectory = Properties.Settings.Default.ActiveDirectory;
                    saveFileDialog.OverwritePrompt = true;
                    saveFileDialog.DefaultExt = SrmDocument.EXT;
                    saveFileDialog.Filter = TextUtil.FileDialogFiltersAll(SrmDocument.FILTER_DOC);
                    saveFileDialog.FileName = Path.GetFileName(targetFile);
                    if (saveFileDialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }
                    targetFile = saveFileDialog.FileName;
                }
            }
            MinimizeToFile(targetFile);
        }

        public void MinimizeToFile(string targetFile)
        {
            // First dispose of statistics collection to avoid it consuming disk reads
            // while actual minimizing is happening.
            _minimizeResults.PauseStatisticsCollection();
            if (!TryMinimizeToFile(targetFile))
            {
                // Restore statistics calculation if minimizing didn't happen and the
                // form is to remain open
                _minimizeResults.StartStatisticsCollection();
                return;
            }
            DialogResult = DialogResult.OK;
        }

        public bool TryMinimizeToFile(string targetFile)
        {
            using (var longWaitDlg = new LongWaitDlg(DocumentUIContainer))
            {
                longWaitDlg.PerformWork(this, 1000,
                    longWaitBroker =>
                    {
                        _longWaitBroker = longWaitBroker;
                        longWaitBroker.Message = FileUIResources.MinimizeResultsDlg_MinimizeToFile_Saving_new_cache_file;
                        _minimizeResults.MinimizeCacheFile(targetFile);
                    });

                if (longWaitDlg.IsCanceled)
                {
                    return false;
                }
            }

            var skylineWindow = (SkylineWindow)DocumentUIContainer;
            if (!skylineWindow.SaveDocument(targetFile, false))
            {
                return false;
            }

            try
            {
                _minimizeResults.SetMeasuredResults(targetFile);
            }
            catch (Exception e)
            {
                var message = TextUtil.LineSeparate(
                    string.Format(
                        FileUIResources
                            .MinimizeResultsDlg_MinimizeToFile_An_unexpected_error_occurred_while_saving_the_data_cache_file__0__,
                        targetFile),
                    e.Message);
                MessageDlg.ShowWithException(this, message, e);
                return false;
            }
            skylineWindow.InvalidateChromatogramGraphs();
            
            return true;
        }

        public void OnProgress(ChromCacheMinimizer.MinStatistics minStatistics, MinimizeResults.BackgroundWorker worker)
        {
            lock (worker)
            {
                CheckDisposed();
                bool updateUi = _minStatistics == null || 
                                _minStatistics.PercentComplete != minStatistics.PercentComplete ||
                                _minStatistics.MinimizedRatio != minStatistics.MinimizedRatio;
                _minStatistics = minStatistics;
                var _this = this;
                if (ReferenceEquals(_minimizeResults.StatisticsCollector, worker))
                {
                    if (updateUi && !_updatePending)
                    {
                        //_updatePending = true;
                        try
                        {
                            BeginInvoke(new Action(() => UpdateStatistics(worker)));
                        }
                        catch (Exception x)
                        {
                            throw new ObjectDisposedException(_this.GetType().FullName, x);
                        }
                    }
                }
            }
            if (worker.IsMinimizingFile)
            {
                _longWaitBroker.ProgressValue = minStatistics.PercentComplete;
                if (_longWaitBroker.IsCanceled)
                {
                    worker.Cancel();
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public void UpdateStatistics(MinimizeResults.BackgroundWorker worker)
        {
            ChromCacheMinimizer.MinStatistics minStatistics;
            lock (worker)
            {
                _updatePending = false;
                if (!ReferenceEquals(_minimizeResults.StatisticsCollector, worker))
                {
                    return;
                }

                Debug.Assert(!InvokeRequired);
                minStatistics = _minStatistics;
            }

            lblCurrentCacheFileSize.Text = string.Format(FileSize.FormatProvider,
                FileUIResources.BackgroundWorker_UpdateStatistics_The_current_size_of_the_cache_file_is__0__fs,
                minStatistics.OriginalFileSize);
            if (minStatistics.PercentComplete == 100)
            {
                lblSpaceSavings.Text = string.Format(
                    FileUIResources
                        .BackgroundWorker_UpdateStatistics_After_minimizing_the_cache_file_will_be_reduced_to__0__its_current_size,
                    minStatistics.MinimizedRatio);
            }
            else
            {
                lblSpaceSavings.Text = string.Format(
                    FileUIResources.BackgroundWorker_UpdateStatistics_Computing_space_savings__0__complete,
                    minStatistics.PercentComplete);
            }

            var newGridRowItems = minStatistics.Replicates.Select(r => new GridRowItem(r))
                .ToArray();
            if (_rowItems.Count != newGridRowItems.Length)
            {
                _rowItems.Clear();
            }

            for (int i = 0; i < newGridRowItems.Length; i++)
            {
                if (i >= _rowItems.Count)
                {
                    _rowItems.Add(newGridRowItems[i]);
                }
                else
                {
                    if (!Equals(_rowItems[i], newGridRowItems[i]))
                    {
                        _rowItems[i] = newGridRowItems[i];
                    }
                }
            }
        }

        #region Functional Test Support

        public bool LimitNoiseTime
        {
            get { return cbxLimitNoiseTime.Checked; }
            set { cbxLimitNoiseTime.Checked = value; }
        }

        public double NoiseTimeRange
        {
            get { return double.Parse(tbxNoiseTimeRange.Text); }
            set { tbxNoiseTimeRange.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        #endregion

        /// <summary>
        /// Data items which are displayed in the DataGridView.
        /// </summary>
        public struct GridRowItem
        {
            public GridRowItem(ChromCacheMinimizer.MinStatistics.Replicate replicateStats)
                : this()
            {
                Name = replicateStats.Name;
                Size = new FileSize(replicateStats.OriginalFileSize);
                if (replicateStats.MinimizedRatio.HasValue)
                {
                    MinimizedSize = Math.Round(replicateStats.MinimizedRatio.Value, 2);
                }
            }
            public string Name { get; set; }
            public FileSize Size { get; set; }
            public double? MinimizedSize { get; set; }
        }
    }
}
