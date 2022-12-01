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
        public NoSrmDataException(MsDataFileUri importPath)
            : base(Resources.NoSrmDataException_NoSrmDataException_No_SRM_MRM_data_found_in__0__, importPath)
        {
        }
    }

    internal class NoFullScanDataException : MissingDataException
    {
        public NoFullScanDataException(MsDataFileUri importPath)
            : base(Resources.NoFullScanDataException_NoFullScanDataException_No_scans_in__0__match_the_current_filter_settings_, importPath)
        {
        }
    }

    internal class NoFullScanFilteringException : MissingDataException
    {
        public NoFullScanFilteringException(MsDataFileUri importPath)
            : base(Resources.NoFullScanFilteringException_NoFullScanFilteringException_The_file__0__does_not_contain_SRM_MRM_chromatograms__To_extract_chromatograms_from_its_spectra__go_to_Settings___Transition_Settings___Full_Scan_and_choose_options_appropriate_to_the_acquisition_method_used_, importPath)
        {
        }
    }

    internal class NoCentroidedDataException : MissingDataException
    {
        public NoCentroidedDataException(MsDataFileUri importPath, Exception innerException)
            : base(Resources.NoCentroidedDataException_NoCentroidedDataException_No_centroided_data_available_for_file___0_____Adjust_your_Full_Scan_settings_, importPath, innerException)
        {
        }
    }

    internal class MissingDataException : DataFileException
    {
        public MissingDataException(string messageFormat, MsDataFileUri importPath)
            : base(string.Format(messageFormat, importPath), importPath)
        {
            MessageFormat = messageFormat;
        }

        public MissingDataException(string messageFormat, MsDataFileUri importPath, Exception innerException)
            : base(string.Format(messageFormat, importPath.GetFilePath()), importPath, innerException)
        {
            MessageFormat = messageFormat;
        }

        public string MessageFormat { get; private set; }
    }

    internal class LoadCanceledException : IOException
    {
        public LoadCanceledException(IProgressStatus status)
            : base(Resources.LoadCanceledException_LoadCanceledException_Data_import_canceled)
        {
            Status = status;
        }

        public IProgressStatus Status { get; private set; }
    }

    internal class ChromCacheBuildException : DataFileException
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
            : base(GetMessage(importPath, innerException), importPath, innerException)
        {
        }
    }

    internal class DataFileException : IOException
    {
        public DataFileException(string message, MsDataFileUri importPath) : base(message)
        {
            ImportPath = importPath;
        }

        public DataFileException(string message, MsDataFileUri importPath, Exception innerException) : base(message, innerException)
        {
            ImportPath = importPath;
        }

        public MsDataFileUri ImportPath { get; private set; }
    }
}
