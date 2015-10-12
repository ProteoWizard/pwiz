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
using System.Drawing;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Reads chromatograms out of .chorusresponse files, which are just .skyd files with the peaks etc. stripped out, and only
    /// containing chromatograms from a single MS data file.  .chorusresponse files are only used for testing the Chorus API.
    /// </summary>
    internal class ChorusResponseChromDataProvider : ChromDataProvider
    {
        private readonly CachedChromatogramDataProvider _cachedChromatogramDataProvider;
        public ChorusResponseChromDataProvider(SrmDocument document, ChromFileInfo chromFileInfo, ProgressStatus progressStatus, int startPercent,
            int endPercent, ILoadMonitor loader) : base(chromFileInfo, progressStatus, startPercent, endPercent, loader)
        {
            ChromatogramCache.RawData rawData;
            MsDataFilePath msDataFilePath = (MsDataFilePath) chromFileInfo.FilePath;
            IPooledStream stream = loader.StreamManager.CreatePooledStream(msDataFilePath.FilePath, false);
            ChromatogramCache.LoadStructs(stream.Stream, out rawData);
            var chromCacheFile = rawData.ChromCacheFiles[0];
            rawData.ChromCacheFiles = new[]
            {
                new ChromCachedFile(chromFileInfo.FilePath, chromCacheFile.Flags, chromCacheFile.FileWriteTime,
                    chromCacheFile.RunStartTime, chromCacheFile.MaxRetentionTime, chromCacheFile.MaxIntensity,
                    chromCacheFile.InstrumentInfoList),
            };
            var cache = new ChromatogramCache("cachePath", rawData, stream); // Not L10N
            _cachedChromatogramDataProvider = new CachedChromatogramDataProvider(cache, document,
                chromFileInfo.FilePath, chromFileInfo, null, progressStatus, startPercent, endPercent, loader);
        }

        public override IEnumerable<KeyValuePair<ChromKey, int>> ChromIds
        {
            get { return _cachedChromatogramDataProvider.ChromIds; }
        }

        public override bool GetChromatogram(
            int id, string modifiedSequence, Color peptideColor,
            out ChromExtra extra, out float[] times, out int[] scanIndexes, out float[] intensities, out float[] massErrors)
        {
            return _cachedChromatogramDataProvider.GetChromatogram(
                id, modifiedSequence, peptideColor,
                out extra, out times, out scanIndexes, out intensities, out massErrors);
        }

        public override double? MaxRetentionTime
        {
            get { return _cachedChromatogramDataProvider.MaxRetentionTime; }
        }

        public override double? MaxIntensity
        {
            get { return _cachedChromatogramDataProvider.MaxIntensity; }
        }

        public override bool IsProcessedScans
        {
            get { return _cachedChromatogramDataProvider.IsProcessedScans; }
        }

        public override bool IsSingleMzMatch
        {
            get { return _cachedChromatogramDataProvider.IsSingleMzMatch; }
        }

        public override void ReleaseMemory()
        {
            _cachedChromatogramDataProvider.ReleaseMemory();
        }

        public override void Dispose()
        {
            _cachedChromatogramDataProvider.Dispose();
        }

        public static bool IsChorusResponse(MsDataFileUri msDataFileUri)
        {
            MsDataFilePath msDataFilePath = msDataFileUri as MsDataFilePath;
            return null != msDataFilePath && msDataFilePath.GetExtension() == DataSourceUtil.EXT_CHORUSRESPONSE;
        }
    }
}
