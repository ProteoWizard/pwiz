/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2022 University of Washington - Seattle, WA
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

using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public class DDASearchControl : SearchControl
    {
        private ImportPeptideSearch ImportPeptideSearch;
        public DDASearchControl(ImportPeptideSearch importPeptideSearch)
        {
            ImportPeptideSearch = importPeptideSearch;
        }

        public override void RunSearch()
        {
            ImportPeptideSearch.SearchEngine.SearchProgressChanged += SearchEngine_MessageNotificationEvent;
            txtSearchProgress.Text = string.Empty;
            _progressTextItems.Clear();
            btnCancel.Enabled = progressBar.Visible = true;

            _cancelToken = new CancellationTokenSource();

            ActionUtil.RunAsync(RunSearchAsync, @"DDA Search thread");
        }

        private void RunSearchAsync()
        {
            var filesCount = ImportPeptideSearch.SearchEngine.SpectrumFileNames.Length;

            IProgressStatus status = new ProgressStatus().ChangeSegments(0, filesCount);
            bool convertSuccess = true;

            if (ImportPeptideSearch.DdaConverter != null)
            {
                status = status.ChangeSegments(0, filesCount * 2);
                convertSuccess = ImportPeptideSearch.DdaConverter.Run(this, status) && !_cancelToken.IsCancellationRequested;

                Invoke(new MethodInvoker(() => status = UpdateSearchEngineProgressMilestone(status, convertSuccess, filesCount,
                    Resources.DDASearchControl_RunSearch_Conversion_cancelled_,
                    Resources.DDASearchControl_RunSearch_Conversion_failed_,
                    Resources.DDASearchControl_SearchProgress_Starting_search)));
            }

            bool searchSuccess = convertSuccess;
            if (convertSuccess)
            {
                searchSuccess = ImportPeptideSearch.SearchEngine.Run(_cancelToken, status) && !_cancelToken.IsCancellationRequested;

                Invoke(new MethodInvoker(() => UpdateSearchEngineProgressMilestone(status, searchSuccess, status.SegmentCount,
                    Resources.DDASearchControl_SearchProgress_Search_canceled,
                    Resources.DDASearchControl_SearchProgress_Search_failed,
                    Resources.DDASearchControl_SearchProgress_Search_done)));
            }

            Invoke(new MethodInvoker(() =>
            {
                UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.NoProgress, 0);
                btnCancel.Enabled = false;
                OnSearchFinished(searchSuccess);
                ImportPeptideSearch.SearchEngine.SearchProgressChanged -= SearchEngine_MessageNotificationEvent;
            }));
        }
    }
}
