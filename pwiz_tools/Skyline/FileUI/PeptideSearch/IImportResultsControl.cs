/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public class ImportResultsSettings
    {
        public static readonly ImportResultsSettings DEFAULT = new ImportResultsSettings(false,
            MultiFileLoader.ImportResultsSimultaneousFileOptions.one_at_a_time, false, null, null);

        public ImportResultsSettings(bool excludeSpectrumSourceFiles, IImportResultsControl control) : this(
            excludeSpectrumSourceFiles,
            (MultiFileLoader.ImportResultsSimultaneousFileOptions) control.SimultaneousFiles, control.DoAutoRetry,
            control.Prefix, control.Suffix)
        {
        }

        public ImportResultsSettings(bool excludeSpectrumSourceFiles,
            MultiFileLoader.ImportResultsSimultaneousFileOptions importFileOptions, bool retryImportAfterFailure,
            string prefix, string suffix)
        {
            ExcludeSpectrumSourceFiles = excludeSpectrumSourceFiles;
            ImportFileOptions = importFileOptions;
            RetryImportAfterFailure = retryImportAfterFailure;
            Prefix = prefix;
            Suffix = suffix;
        }

        [Track]
        public bool ExcludeSpectrumSourceFiles { get; private set; }
        [Track]
        public MultiFileLoader.ImportResultsSimultaneousFileOptions ImportFileOptions { get; private set; }
        [Track]
        public bool RetryImportAfterFailure { get; private set; }
        [Track]
        public string Prefix { get; private set; }
        [Track]
        public string Suffix { get; private set; }
    }

    public interface IImportResultsControl
    {
        List<ImportPeptideSearch.FoundResultsFile> FoundResultsFiles { get; set; } // Name --> Path
        bool ResultsFilesMissing { get; }
        int SimultaneousFiles { get; }
        bool DoAutoRetry { get; }

        string Prefix { get; set; }
        string Suffix { get; set; }

        ImportResultsSettings ImportSettings { get; }

        event EventHandler<ImportResultsControl.ResultsFilesEventArgs> ResultsFilesChanged;
    }
}
