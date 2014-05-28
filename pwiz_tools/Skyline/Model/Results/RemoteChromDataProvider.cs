/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Model.Results.RemoteApi.GeneratedCode;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    internal class RemoteChromDataProvider : ChromDataProvider
    {
        private ChromTaskList _chromTaskList;
        public RemoteChromDataProvider(SrmDocument document, ChromFileInfo chromFileInfo, ProgressStatus progressStatus, int startPercent,
            int endPercent, ILoadMonitor loader)
            : base(chromFileInfo, progressStatus, startPercent, endPercent, loader)
        {
            ChromatogramRequestDocument fullChromatogramRequest = new SpectrumFilter(document, chromFileInfo.FilePath, null).ToChromatogramRequestDocument();
            ChorusUrl chorusUrl = (ChorusUrl) chromFileInfo.FilePath;
            ChorusAccount chorusAccount = chorusUrl.FindChorusAccount(Settings.Default.ChorusAccountList);
            _chromTaskList = new ChromTaskList(CheckCancelled, document, chorusAccount, chorusUrl, ChromTaskList.ChunkChromatogramRequest(fullChromatogramRequest, 100));
            _chromTaskList.SetMinimumSimultaneousTasks(2);
        }

        public override IEnumerable<KeyValuePair<ChromKey, int>> ChromIds
        {
            get { return _chromTaskList == null ? null : _chromTaskList.ChromIds; }
        }

        public override bool GetChromatogram(
            int id, 
            out ChromExtra extra,
            out float[] times, 
            out int[] scanIds, 
            out float[] intensities,
            out float[] massErrors)
        {
            bool loaded = _chromTaskList.GetChromatogram(id, out extra, out times, out intensities, out massErrors);
            if (loaded)
            {
                LoadingStatus.Transitions.AddTransition(
                        extra.StatusId,
                        extra.StatusRank,
                        times,
                        intensities);
            }

            SetPercentComplete(_chromTaskList.PercentComplete * 99/100);
            scanIds = null; // TODO
            return loaded;
        }

        public override double? MaxRetentionTime
        {
            get { return null; }
        }

        public override double? MaxIntensity
        {
            get { return null; }
        }

        public override bool IsProcessedScans
        {
            get { return false; }
        }

        public override bool IsSingleMzMatch
        {
            get { return true; }
        }

        public override void ReleaseMemory()
        {
            _chromTaskList = null;
        }

        public override void Dispose()
        {
            ReleaseMemory();
        }


        public static bool IsRemoteChromFile(MsDataFileUri msDataFileUri)
        {
            return msDataFileUri is ChorusUrl || DataSourceUtil.EXT_CHORUSRESPONSE == msDataFileUri.GetExtension();
        }

        private void CheckCancelled()
        {
            if (_loader.IsCanceled)
            {
                throw new LoadCanceledException(Status.Cancel());
            }
        }
    }
}
