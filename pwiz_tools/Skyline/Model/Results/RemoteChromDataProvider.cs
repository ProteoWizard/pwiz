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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Results.RemoteApi.Chorus;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results
{
    internal class RemoteChromDataProvider : ChromDataProvider
    {
        private ChorusAccount _chorusAccount;
        private SrmDocument _document;
        private ChromatogramRequestProvider[] _chromatogramRequestProviders;
        private ChromTaskList[] _chromTaskLists;

        private bool _sourceHasPositivePolarityData;
        private bool _sourceHasNegativePolarityData;

        public RemoteChromDataProvider(SrmDocument document, IRetentionTimePredictor retentionTimePredictor, ChromFileInfo chromFileInfo, IProgressStatus progressStatus, int startPercent,
            int endPercent, ILoadMonitor loader)
            : base(chromFileInfo, progressStatus, startPercent, endPercent, loader)
        {
            _document = document;
            ChorusUrl chorusUrl = (ChorusUrl)chromFileInfo.FilePath;
            _chorusAccount = chorusUrl.FindChorusAccount(Settings.Default.RemoteAccountList);
            var chromatogramRequestProviders = new List<ChromatogramRequestProvider>();
            foreach (bool firstPass in new[] {true, false})
            {
                if (null == retentionTimePredictor && !firstPass)
                {
                    continue;
                }
                var chromatogramRequestProvider = new ChromatogramRequestProvider(document, chorusUrl,
                    retentionTimePredictor, firstPass);
                if (0 == chromatogramRequestProvider.ChromKeys.Count)
                {
                    continue;
                }
                chromatogramRequestProviders.Add(chromatogramRequestProvider);
            }
            _chromatogramRequestProviders = chromatogramRequestProviders.ToArray();
            _chromTaskLists = new ChromTaskList[_chromatogramRequestProviders.Length];
        }

        public override IEnumerable<ChromKeyProviderIdPair> ChromIds
        {
            get
            {
                return _chromatogramRequestProviders.SelectMany(chromRequestProvider => chromRequestProvider.ChromKeys)
                    .Select((key, index) => new ChromKeyProviderIdPair(key, index));
            }
        }

        public override bool GetChromatogram(
            int id, 
            Target modifiedSequence,
            Color peptideColor,
            out ChromExtra extra,
            out TimeIntensities timeIntensities)
        {
            bool loaded = false;
            extra = null;
            timeIntensities = null;
            int idRemain = id;
            for (int iTaskList = 0; iTaskList < _chromatogramRequestProviders.Length; iTaskList++)
            {
                ChromatogramRequestProvider requestProvider = _chromatogramRequestProviders[iTaskList];
                if (requestProvider.ChromKeys.Count <= idRemain)
                {
                    idRemain -= requestProvider.ChromKeys.Count;
                    continue;
                }
                ChromTaskList chromTaskList = _chromTaskLists[iTaskList];
                if (null == chromTaskList)
                {
                    chromTaskList = _chromTaskLists[iTaskList] = new ChromTaskList(CheckCancelled, _document, _chorusAccount, requestProvider.ChorusUrl, ChromTaskList.ChunkChromatogramRequest(requestProvider.GetChromatogramRequest(), 1000));
                    chromTaskList.SetMinimumSimultaneousTasks(3);
                }
                ChromKey chromKey = requestProvider.ChromKeys[idRemain];
                loaded = chromTaskList.GetChromatogram(chromKey, out timeIntensities);
                if (loaded)
                {
                    extra = new ChromExtra(id, chromKey.Precursor == 0 ? 0 : -1);
                    if (chromKey.Precursor.IsNegative)
                    {
                        _sourceHasNegativePolarityData = true;
                    }
                    else
                    {
                        _sourceHasPositivePolarityData = true;
                    }
                    if (timeIntensities.NumPoints > 0 && Status is ChromatogramLoadingStatus)
                    {
                        ((ChromatogramLoadingStatus)Status).Transitions.AddTransition(
                                modifiedSequence,
                                peptideColor,
                                extra.StatusId,
                                extra.StatusRank,
                                timeIntensities.Times,
                                timeIntensities.Intensities);
                    }
                }
                break;
            }
            int percentComplete = _chromTaskLists.Sum(taskList => taskList == null ? 0 : taskList.PercentComplete)/
                                  _chromTaskLists.Length;

            percentComplete = Math.Min(percentComplete, 99);
            SetPercentComplete(percentComplete);
            return loaded;
        }

        public override double? MaxRetentionTime
        {
            get { return null; }
        }

        public override MsDataFileImpl.eIonMobilityUnits IonMobilityUnits { get { return MsDataFileImpl.eIonMobilityUnits.none; } }

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

        public override bool SourceHasPositivePolarityData
        {
            get { return _sourceHasPositivePolarityData; }
        }

        public override bool SourceHasNegativePolarityData
        {
            get { return _sourceHasNegativePolarityData; }
        }

        public override void ReleaseMemory()
        {
            _chromTaskLists = null;
        }

        public override void Dispose()
        {
            ReleaseMemory();
        }


        public static bool IsRemoteChromFile(MsDataFileUri msDataFileUri)
        {
            return msDataFileUri is ChorusUrl || ChorusResponseChromDataProvider.IsChorusResponse(msDataFileUri);
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
