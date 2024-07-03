/*
 * Original author: Brian Pratt <bspratt .at. proteinms dot net >
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.Skyline.Model.DdaSearch;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public class HardklorSearchControl : SearchControl
    {
        private ImportPeptideSearch ImportPeptideSearch;
        private ImportPeptideSearch.HardklorSettings _settings;
        private int _totalSteps;
        private int _currentStep;
        HardklorSearchEngine FeatureFinder => ImportPeptideSearch.SearchEngine as HardklorSearchEngine;


        public HardklorSearchControl(ImportPeptideSearch importPeptideSearch)
        {
            ImportPeptideSearch = importPeptideSearch;
        }

        // Steps, in parallel, for n files:
        //    Threads 0 through (n-1): per file, convert to mzML, then do HardKlor then Bullseye
        //    Thread n: Perform alignments on all mzMLs as they become available
        // Finally combine the results across replicates

        private bool Search(ImportPeptideSearch.HardklorSettings settings, CancellationTokenSource token)
        {
            _settings = settings;
            ParallelRunnerProgressControl multiProgressControl = null;
            try
            {
                Invoke(new MethodInvoker(() =>
                {
                    multiProgressControl = new ParallelRunnerProgressControl(this);
                    multiProgressControl.Dock = DockStyle.Fill;
                    progressSplitContainer.Panel1.Controls.Add(multiProgressControl);
                }));

                FeatureFinder.Generate(multiProgressControl, this, token.Token);
            }
            catch (OperationCanceledException e)
            {
                UpdateProgress(_status.ChangeWarningMessage(e.InnerException?.Message ?? e.Message));
                return false;
            }
            catch (Exception e)
            {
                UpdateProgress(_status.ChangeErrorException(e));
                return false;
            }
            finally
            {
                Invoke(new MethodInvoker(() =>
                {
                    progressSplitContainer.Panel1Collapsed = true;
                    progressSplitContainer.Panel1.Controls.Clear();
                    multiProgressControl?.Dispose();
                }));
            }

            return !token.IsCancellationRequested;
        }

        public override UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            if (IsCanceled)
            {
                return UpdateProgressResponse.cancel;
            }

            lock (this)
            {
                _status = _status.ChangeMessage(status.Message).ChangePercentComplete((100 * _currentStep++) / _totalSteps);
            }

            BeginInvoke(new MethodInvoker(() => UpdateSearchEngineProgress(_status)));

            return UpdateProgressResponse.normal;
        }




        public override void RunSearch()
        {
            // ImportPeptideSearch.SearchEngine.SearchProgressChanged += SearchEngine_MessageNotificationEvent;
            txtSearchProgress.Text = string.Empty;
            _progressTextItems.Clear();
            btnCancel.Enabled = progressBar.Visible = true;

            _cancelToken = new CancellationTokenSource();
            FeatureFinder.SetCancelToken(_cancelToken);

            ActionUtil.RunAsync(RunSearchAsync, @"Feature Finding Search thread");
        }

        private IProgressStatus _status;
        private void RunSearchAsync()
        {
            _totalSteps = (ImportPeptideSearch.SearchEngine.SpectrumFileNames.Length * 4) + 2; //  Per-file: msconvert, Hardklor, Bullseye, RTAlign prep.  All-files: RT alignment, combine features
            _currentStep = 0;

            _status = new ProgressStatus();

            bool success = true;

            if (!_cancelToken.IsCancellationRequested)
            {
                UpdateProgress(_status = _status.ChangeMessage(PeptideSearchResources.DDASearchControl_SearchProgress_Starting_search));

                success = Search(_settings, _cancelToken);

                Invoke(new MethodInvoker(() => UpdateSearchEngineProgressMilestone(_status, success, _status.SegmentCount,
                    Resources.DDASearchControl_SearchProgress_Search_canceled,
                    PeptideSearchResources.DDASearchControl_SearchProgress_Search_failed,
                    Resources.DDASearchControl_SearchProgress_Search_done)));
            }


            Invoke(new MethodInvoker(() =>
            {
                UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.NoProgress, 0);
                btnCancel.Enabled = false;
                OnSearchFinished(success);
                // ImportPeptideSearch.SearchEngine.SearchProgressChanged -= SearchEngine_MessageNotificationEvent;
            }));
        }


    }
}
