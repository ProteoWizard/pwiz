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

namespace pwiz.Skyline.FileUI
{
    /// <summary>
    /// Displays UI for choosing settings for reducing the size of the chromatogram cache file
    /// by removing unused transition and limiting the length along the time axis.
    /// </summary>
    public partial class MinimizeResultsDlg : FormEx
    {
        private readonly BindingList<GridRowItem> _rowItems;
        private ChromCacheMinimizer.Settings _settings;
        private ChromCacheMinimizer _chromCacheMinimizer;
        private BackgroundWorker _statisticsCollector;

        public MinimizeResultsDlg(IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            Settings = new ChromCacheMinimizer.Settings()
                .SetDiscardUnmatchedChromatograms(true)
                .SetNoiseTimeRange(null);
            DocumentUIContainer = documentUIContainer;
            bindingSource1.DataSource = _rowItems = new BindingList<GridRowItem>();
        }

        public IDocumentUIContainer DocumentUIContainer { get; private set; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (DocumentUIContainer != null)
            {
                DocumentUIContainer.ListenUI(OnDocumentChanged);
                SetDocument(DocumentUIContainer.Document);
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

        private void OnDocumentChanged(object sender, DocumentChangedEventArgs args)
        {
            SetDocument(DocumentUIContainer.DocumentUI);
        }

        private void SetDocument(SrmDocument document)
        {
            ChromCacheMinimizer = document.Settings.HasResults
                                      ? document.Settings.MeasuredResults.GetChromCacheMinimizer(document)
                                      : null;
        }

        private bool _changingOptimizeSettings;
        public ChromCacheMinimizer.Settings Settings
        {
            get
            {
                return _settings;
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
                    _settings = value;
                    cbxDiscardUnmatchedChromatograms.Checked = Settings.DiscardUnmatchedChromatograms;
                    if (Settings.NoiseTimeRange.HasValue)
                    {
                        tbxNoiseTimeRange.Text = Settings.NoiseTimeRange.Value.ToString(CultureInfo.CurrentCulture);
                        tbxNoiseTimeRange.Enabled = true;
                        cbxLimitNoiseTime.Checked = true;
                    }
                    else
                    {
                        tbxNoiseTimeRange.Enabled = false;
                        cbxLimitNoiseTime.Checked = false;
                    }
                    if (ChromCacheMinimizer != null)
                    {
                        StatisticsCollector = new BackgroundWorker(this, null);
                    }
                }
                finally
                {
                    _changingOptimizeSettings = false;
                }
            }
        }

        private ChromCacheMinimizer ChromCacheMinimizer
        {
            get { return _chromCacheMinimizer; }
            set
            {
                if (ReferenceEquals(value, _chromCacheMinimizer))
                {
                    return;
                }
                if (ChromCacheMinimizer != null)
                {
                    StatisticsCollector = null;
                }
                _chromCacheMinimizer = value;
                if (ChromCacheMinimizer != null)
                {
                    StatisticsCollector = new BackgroundWorker(this, null);
                    btnMinimize.Enabled = btnMinimizeAs.Enabled = true;
                }
                else
                {
                    btnMinimize.Enabled = btnMinimizeAs.Enabled = false;
                    lblCurrentCacheFileSize.Text = "The cache file has not been loaded yet.";
                    lblSpaceSavings.Text = "";
                }
            }
        }

        private BackgroundWorker StatisticsCollector
        {
            get { return _statisticsCollector; }
            set
            {
                if (ReferenceEquals(value, _statisticsCollector))
                {
                    return;
                }
                if (StatisticsCollector != null)
                {
                    StatisticsCollector.Dispose();
                }
                _statisticsCollector = value;
                if (StatisticsCollector != null)
                {
                    new Action(StatisticsCollector.CollectStatistics)
                        .BeginInvoke(null, null);
                }
            }
        }

        private void cbxDiscardUnmatchedChromatograms_CheckedChanged(object sender, EventArgs e)
        {
            Settings = Settings.SetDiscardUnmatchedChromatograms(cbxDiscardUnmatchedChromatograms.Checked);
        }

        private void tbxNoiseTimeRange_Leave(object sender, EventArgs e)
        {
            if (DialogResult != DialogResult.None)
                return;

            double noiseTime;
            string errorMessage = null;
            if (!double.TryParse(tbxNoiseTimeRange.Text, out noiseTime))
                errorMessage = "The noise time limit must be a valid decimal number.";
            if (noiseTime < 0)
                errorMessage = "The noise time limit must be a positive decimal number.";
            if (errorMessage != null)
            {
                MessageDlg.Show(this, errorMessage);
                tbxNoiseTimeRange.Focus();
                tbxNoiseTimeRange.SelectAll();
                return;
            }

            Settings = Settings.SetNoiseTimeRange(noiseTime);
        }

        private void cbxLimitNoiseTime_CheckedChanged(object sender, EventArgs e)
        {
            Settings = cbxLimitNoiseTime.Checked
                           ? Settings.SetNoiseTimeRange(double.Parse(tbxNoiseTimeRange.Text))
                           : Settings.SetNoiseTimeRange(null);
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
                MessageDlg.Show(this, "All results must be completely imported before any can be minimized.");
                return;
            }
            if (!Settings.DiscardUnmatchedChromatograms && !Settings.NoiseTimeRange.HasValue)
            {
                if (MessageBox.Show(this, "You have not chosen any options to minimize your cache file.  Are you sure you want to continue?", 
                    Program.Name, MessageBoxButtons.OKCancel) != DialogResult.OK)
                {
                    return;
                }
            }
            string targetFile = DocumentUIContainer.DocumentFilePath;

            if (asNewFile || string.IsNullOrEmpty(targetFile))
            {
                using (var saveFileDialog =
                    new SaveFileDialog
                    {
                        InitialDirectory = Properties.Settings.Default.ActiveDirectory,
                        OverwritePrompt = true,
                        DefaultExt = SrmDocument.EXT,
                        Filter = string.Join("|", new[]
                        {
                            "Skyline Documents (*." + SrmDocument.EXT + ")|*." + SrmDocument.EXT,
                            "All Files (*.*)|*.*"
                        }),
                        FileName = Path.GetFileName(targetFile),
                    })
                {
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
            var targetSkydFile = ChromatogramCache.FinalPathForName(targetFile, null);
            using (var skydSaver = new FileSaver(targetSkydFile))
            {
                using (var stream = File.OpenWrite(skydSaver.SafeName))
                using (var longWaitDlg = new LongWaitDlg(DocumentUIContainer))
                {
                    longWaitDlg.PerformWork(this, 1000,
                                            longWaitBroker =>
                                                {
                                                    longWaitBroker.Message = "Saving new cache file";
                                                    try
                                                    {
                                                        using (var backgroundWorker =
                                                            new BackgroundWorker(this, longWaitBroker))
                                                        {
                                                            backgroundWorker.RunBackground(stream);
                                                        }
                                                    }
                                                    catch (ObjectDisposedException)
                                                    {
                                                        if (!longWaitBroker.IsCanceled)
                                                        {
                                                            throw;
                                                        }
                                                    }
                                                });

                    if (longWaitDlg.IsCanceled)
                    {
                        return;
                    }
                }

                var skylineWindow = (SkylineWindow) DocumentUIContainer;
                if (!skylineWindow.SaveDocument(targetFile, false))
                {
                    return;
                }
                try
                {
                    var measuredResults = DocumentUIContainer.Document.Settings.MeasuredResults.CommitCacheFile(skydSaver);
                    SrmDocument docOrig, docNew;
                    do
                    {
                        docOrig = DocumentUIContainer.Document;
                        docNew = docOrig.ChangeMeasuredResults(measuredResults);
                    } while (!DocumentUIContainer.SetDocument(docNew, docOrig));
                }
                catch (Exception x)
                {
                    MessageDlg.Show(this,
                                    string.Format("An unexpected error occurred while saving the data cache file {0}.\n{1}",
                                                  targetFile, x.Message));
                    return;
                }
                skylineWindow.InvalidateChromatogramGraphs();
            }
            DialogResult = DialogResult.OK;
        }

        #region Functional Test Support
        public bool LimitNoiseTime
        {
            get { return cbxLimitNoiseTime.Checked; }
            set { cbxLimitNoiseTime.Checked = value; }
        }

        public string NoiseTimeRange
        {
            get { return tbxNoiseTimeRange.Text; }
            set { tbxNoiseTimeRange.Text = value; }
        }
        #endregion

        /// <summary>
        /// Handles the task of either estimating the space savings the user will achieve
        /// when they minimize the cache file, and actually doing to the work to minimize
        /// the file.
        /// The work that this class is doing can be cancelled by calling <see cref="IDisposable.Dispose"/>.
        /// </summary>
        private class BackgroundWorker : MustDispose
        {
            private readonly MinimizeResultsDlg _dlg;
            private readonly ILongWaitBroker _longWaitBroker;
            private ChromCacheMinimizer.Statistics _statistics;
            private bool _updatePending;

            public BackgroundWorker(MinimizeResultsDlg dlg, ILongWaitBroker longWaitBroker)
            {
                _dlg = dlg;
                _longWaitBroker = longWaitBroker;
            }


            void OnProgress(ChromCacheMinimizer.Statistics statistics)
            {
                lock(this)
                {
                    CheckDisposed();
                    bool updateUi = _statistics == null || _statistics.PercentComplete != statistics.PercentComplete;
                    _statistics = statistics;
                    if (ReferenceEquals(_dlg.StatisticsCollector, this))
                    {
                        if (updateUi && !_updatePending)
                        {
                            //_updatePending = true;
                            try
                            {
                                _dlg.BeginInvoke(new Action(UpdateStatistics));
                            }
                            catch (Exception x)
                            {                                
                                throw new ObjectDisposedException(_dlg.GetType().FullName, x);
                            }
                        }
                    }
                }
                if (_longWaitBroker != null)
                {
                    _longWaitBroker.ProgressValue = statistics.PercentComplete;
                    if (_longWaitBroker.IsCanceled)
                    {
                        throw new ObjectDisposedException(GetType().FullName);
                    }
                }
            }

            public void RunBackground(Stream outputStream)
            {
                _dlg.ChromCacheMinimizer.Minimize(_dlg.Settings, OnProgress, outputStream);
            }

            public void CollectStatistics()
            {
                try
                {
                    RunBackground(null);
                }
                catch (ObjectDisposedException)
                {
                    
                }
            }

            private void UpdateStatistics()
            {
                ChromCacheMinimizer.Statistics statistics;
                lock(this)
                {
                    _updatePending = false;
                    if (!ReferenceEquals(_dlg.StatisticsCollector, this))
                    {
                        return;
                    }
                    Debug.Assert(!_dlg.InvokeRequired);
                    statistics = _statistics;
                }
                _dlg.lblCurrentCacheFileSize.Text = string.Format(
                    FileSize.FormatProvider,
                    "The current size of the cache file is {0:fs}", statistics.OriginalFileSize);
                if (statistics.PercentComplete == 100)
                {
                    _dlg.lblSpaceSavings.Text =
                        string.Format("After minimizing, the cache file will be reduced to {0:0%} its current size",
                                      statistics.MinimizedRatio);
                }
                else
                {
                    _dlg.lblSpaceSavings.Text = string.Format("Computing space savings ({0}% complete)", statistics.PercentComplete);
                }
                var newGridRowItems = statistics.Replicates.Select(r => new GridRowItem(r)).ToArray();
                if (_dlg._rowItems.Count != newGridRowItems.Length)
                {
                    _dlg._rowItems.Clear();
                }
                for (int i = 0; i < newGridRowItems.Length; i++)
                {
                    if (i >= _dlg._rowItems.Count)
                    {
                        _dlg._rowItems.Add(newGridRowItems[i]);
                    }
                    else
                    {
                        if (!Equals(_dlg._rowItems[i], newGridRowItems[i]))
                        {
                            _dlg._rowItems[i] = newGridRowItems[i];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Data items which are displayed in the DataGridView.
        /// </summary>
        public struct GridRowItem
        {
            public GridRowItem(ChromCacheMinimizer.Statistics.Replicate replicateStats)
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
