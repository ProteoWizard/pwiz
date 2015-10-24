/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.IO;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    internal class NoSrmDataException : MissingDataException
    {
        public NoSrmDataException(string filePath)
            : base(Resources.NoSrmDataException_NoSrmDataException_No_SRM_MRM_data_found_in__0__, filePath)
        {
        }
    }

    internal class NoFullScanDataException : MissingDataException
    {
        public NoFullScanDataException(string filePath)
            : base(Resources.NoFullScanDataException_NoFullScanDataException_No_scans_in__0__match_the_current_filter_settings_, filePath)
        {
        }
    }

    internal class NoFullScanFilteringException : MissingDataException
    {
        public NoFullScanFilteringException(string fileName)
            : base(Resources.NoFullScanFilteringException_NoFullScanFilteringException_To_extract_chromatograms_from__0__full_scan_settings_must_be_enabled_, fileName)
        {
        }
    }

    internal class NoCentroidedDataException : IOException
    {
        public NoCentroidedDataException(string fileName, Exception innerException)
            : base(string.Format(Resources.NoCentroidedDataException_NoCentroidedDataException_No_centroided_data_available_for_file___0_____Adjust_your_Full_Scan_settings_, fileName), innerException)
        {
        }
    }

    internal class MissingDataException : IOException
    {
        public MissingDataException(string messageFormat, string fileName)
            : base(string.Format(messageFormat, fileName))
        {
            MessageFormat = messageFormat;
        }

        public MissingDataException(string messageFormat, string fileName, Exception innerException)
            : base(string.Format(messageFormat, fileName), innerException)
        {
            MessageFormat = messageFormat;
        }

        public string MessageFormat { get; private set; }
    }

    internal class LoadCanceledException : IOException
    {
        public LoadCanceledException(ProgressStatus status)
            : base(Resources.LoadCanceledException_LoadCanceledException_Data_import_canceled)
        {
            Status = status;
        }

        public ProgressStatus Status { get; private set; }
    }

    internal class ChromCacheBuildException : IOException
    {
        private static string GetMessage(MsDataFileUri importPath, Exception x)
        {
            string message = importPath.GetSampleName() == null
                ? string.Format(Resources.ChromCacheBuildException_GetMessage_Failed_importing_results_file___0___, importPath.GetFilePath())
                : string.Format(Resources.ChromCacheBuildException_GetMessage_Failed_importing_results_file___0____sample__1__,
                    importPath.GetFilePath(), importPath.GetSampleName());
            return TextUtil.LineSeparate(message, x.Message);
        }

        public ChromCacheBuildException(MsDataFileUri importPath, Exception innerException)
            : base(GetMessage(importPath, innerException), innerException)
        {
            ImportPath = importPath;
        }

        public MsDataFileUri ImportPath { get; private set; }
    }
}
