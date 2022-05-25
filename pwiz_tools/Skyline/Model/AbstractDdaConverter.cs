/*
 * Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
 *
 * Copyright 2020
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

using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model
{
    public abstract class AbstractDdaConverter
    {
        public MsDataFileUri[] OriginalSpectrumSources { get; protected set; }
        public MsDataFileUri[] ConvertedSpectrumSources { get; protected set; }
        protected ImportPeptideSearch ImportPeptideSearch { get; private set; }

        public AbstractDdaConverter(ImportPeptideSearch importPeptideSearch)
        {
            ImportPeptideSearch = importPeptideSearch;
        }

        // NB: these must match the enum values in pwiz::msdata::MSDataFile::Format,
        // but also match the letter casing from msconvert options since those options are case-sensitive
        // ReSharper disable InconsistentNaming
        public enum MsdataFileFormat
        {
            mzML = 1,
            mzXML,
            mgf,
            ms1,
            cms1,
            ms2,
            cms2,
            mz5
        }
        // ReSharper restore InconsistentNaming

        /// <summary>
        /// Tells the DDA converter to produce output in the given format. 
        /// </summary>
        public abstract void SetRequiredOutputFormat(MsdataFileFormat format);

        public abstract void SetSpectrumFiles(MsDataFileUri[] spectrumFiles);

        public abstract bool Run(IProgressMonitor progressMonitor, IProgressStatus status);
    }
}
