/*
 * Original author: John Chilton <jchilton .at. u.washington.edu>,
 *                  Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011-2019 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Xml.Serialization;
using pwiz.Common.DataBinding.Documentation;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline
{
    public class CommandArgs
    {
        // Argument value descriptions
        private static readonly Func<string> PATH_TO_FILE = () => CommandArgUsage.CommandArgs_PATH_TO_FILE_path_to_file;

        private static string GetPathToFile(string ext)
        {
            return PATH_TO_FILE() + ext;
        }

        private static readonly Func<string> PATH_TO_DOCUMENT = () => GetPathToFile(SrmDocument.EXT);
        private static readonly Func<string> PATH_TO_FOLDER = () => CommandArgUsage.CommandArgs_PATH_TO_FOLDER;
        private static readonly Func<string> DATE_VALUE = () => CommandArgUsage.CommandArgs_DATE_VALUE;
        private static readonly Func<string> INT_VALUE = () => CommandArgUsage.CommandArgs_INT_VALUE;
        private static readonly Func<string> NUM_VALUE = () => CommandArgUsage.CommandArgs_NUM_VALUE;
        private static readonly Func<string> NUM_LIST_VALUE = () => CommandArgUsage.CommandArgs_NUM_LIST_VALUE;
        private static readonly Func<string> NAME_VALUE = () => CommandArgUsage.CommandArgs_NAME_VALUE;
        private static readonly Func<string> FEATURE_NAME_VALUE = () => CommandArgUsage.CommandArgs_FEATURE_NAME_VALUE;
        private static readonly Func<string> REPORT_NAME_VALUE = () => CommandArgUsage.CommandArgs_REPORT_NAME_VALUE;
        private static readonly Func<string> PIPE_NAME_VALUE = () => CommandArgUsage.CommandArgs_PIPE_NAME_VALUE;
        private static readonly Func<string> REGEX_VALUE = () => CommandArgUsage.CommandArgs_REGEX_VALUE;
        private static readonly Func<string> RP_VALUE = () => CommandArgUsage.CommandArgs_RP_VALUE;
        private static readonly Func<string> MZ_VALUE = () => CommandArgUsage.CommandArgs_MZ_VALUE;
        private static readonly Func<string> MINUTES_VALUE = () => CommandArgUsage.CommandArgs_MINUTES_VALUE;
        private static readonly Func<string> MILLIS_VALE = () => CommandArgUsage.CommandArgs_MILLIS_VALE;
        private static readonly Func<string> SERVER_URL_VALUE = () => CommandArgUsage.CommandArgs_SERVER_URL_VALUE;
        private static readonly Func<string> USERNAME_VALUE = () => CommandArgUsage.CommandArgs_USERNAME_VALUE;
        private static readonly Func<string> PASSWORD_VALUE = () => CommandArgUsage.CommandArgs_PASSWORD_VALUE;
        private static readonly Func<string> COMMAND_VALUE = () => CommandArgUsage.CommandArgs_COMMAND_VALUE;
        private static readonly Func<string> COMMAND_ARGUMENTS_VALUE = () => CommandArgUsage.CommandArgs_COMMAND_ARGUMENTS_VALUE;
        private static readonly Func<string> PROGRAM_MACRO_VALUE = () => CommandArgUsage.CommandArgs_PROGRAM_MACRO_VALUE;
        private static readonly Func<string> LABEL_VALUE = () => CommandArgUsage.CommandArgs_LABEL_VALUE;
        // ReSharper disable LocalizableElement
        private static readonly Func<string> INT_LIST_VALUE = () => "\"1, 2, 3...\"";  // Not L10N
        private static readonly Func<string> ION_TYPE_LIST_VALUE = () => "\"a, b, c, x, y, z, p\"";    // Not L10N
        // ReSharper restore LocalizableElement

        // Internal use arguments
        public static readonly Argument ARG_INTERNAL_SCREEN_WIDTH = new Argument(@"sw", INT_VALUE,
            (c, p) => c._usageWidth = p.ValueInt) {InternalUse = true};
        public static readonly Argument ARG_INTERNAL_CULTURE = new Argument(@"culture", () => @"en|fr|ja|zh-CHS...",
            (c, p) => SetCulture(p.Value)) { InternalUse = true };

        private static void SetCulture(string cultureName)
        {
            LocalizationHelper.CurrentCulture = LocalizationHelper.CurrentUICulture = new CultureInfo(cultureName);
            LocalizationHelper.InitThread(Thread.CurrentThread);
        }
        // Multi process import
        public static readonly Argument ARG_INTERNAL_IMPORT_FILE_CACHE = new DocArgument(@"import-file-cache", PATH_TO_FILE,
            (c, p) => Program.ReplicateCachePath = p.Value) {InternalUse = true};
        public static readonly Argument ARG_INTERNAL_IMPORT_PROGRESS_PIPE = new DocArgument(@"import-progress-pipe", PIPE_NAME_VALUE,
            (c, p) => Program.ImportProgressPipe = p.Value) {InternalUse = true};
        public static readonly Argument ARG_TEST_UI = new Argument(@"ui",
            (c, p) => /* Handled by Program */ true) {InternalUse = true};
        public static readonly Argument ARG_TEST_HIDEACG = new Argument(@"hideacg",
            (c, p) => c.HideAllChromatogramsGraph = true) {InternalUse = true};
        public static readonly Argument ARG_TEST_NOACG = new Argument(@"noacg",
            (c, p) => c.NoAllChromatogramsGraph = true) {InternalUse = true};

        private static readonly ArgumentGroup GROUP_INTERNAL = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_INTERNAL, false,
            ARG_INTERNAL_SCREEN_WIDTH, ARG_INTERNAL_CULTURE, ARG_INTERNAL_IMPORT_FILE_CACHE, ARG_INTERNAL_IMPORT_PROGRESS_PIPE,
            ARG_TEST_UI, ARG_TEST_HIDEACG, ARG_TEST_NOACG);

        public bool HideAllChromatogramsGraph { get; private set; }
        public bool NoAllChromatogramsGraph { get; private set; }

        // Conflict resolution values
        public const string ARG_VALUE_OVERWRITE = "overwrite";
        public const string ARG_VALUE_SKIP = "skip";
        public const string ARG_VALUE_PARALLEL = "parallel";

        public static readonly Argument ARG_IN = new DocArgument(@"in", PATH_TO_DOCUMENT,
            (c, p) => c.SkylineFile = p.ValueFullPath);
        public static readonly Argument ARG_SAVE = new DocArgument(@"save", (c, p) => { c.Saving = true; });
        public static readonly Argument ARG_SAVE_SETTINGS = new DocArgument(@"save-settings", (c, p) => c.SaveSettings = true);
        public static readonly Argument ARG_OUT = new DocArgument(@"out", PATH_TO_DOCUMENT,
            (c, p) => { c.SaveFile = p.ValueFullPath; });
        public static readonly Argument ARG_SHARE_ZIP = new DocArgument(@"share-zip", () => GetPathToFile(SrmDocumentSharing.EXT_SKY_ZIP),
            (c, p) =>
            {
                c.SharingZipFile = true;
                if (!string.IsNullOrEmpty(p.Value))
                    c.SharedFile = p.Value;
            })
            { OptionalValue = true};
        public static readonly Argument ARG_SHARE_TYPE = new Argument(@"share-type",
            new[] {ARG_VALUE_SHARE_TYPE_MINIMAL, ARG_VALUE_SHARE_TYPE_COMPLETE},
            (c, p) => c.SharedFileType = p.IsValue(ARG_VALUE_SHARE_TYPE_MINIMAL) ? ShareType.MINIMAL : ShareType.COMPLETE);
        public const string ARG_VALUE_SHARE_TYPE_MINIMAL = "minimal";
        public const string ARG_VALUE_SHARE_TYPE_COMPLETE = "complete";
        public static readonly Argument ARG_BATCH = new Argument(@"batch-commands", PATH_TO_FILE, // Run each line of a text file like a command
            (c, p) =>
            {
                c.BatchCommandsPath = p.ValueFullPath;
                c.RunningBatchCommands = true;
            });
        public static readonly Argument ARG_DIR = new Argument(@"dir", PATH_TO_FOLDER,
            (c, p) =>
            {
                if (!Directory.Exists(p.Value))
                {
                    c.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__The_specified_working_directory__0__does_not_exist_, p.Value);
                    return false;
                }
                Directory.SetCurrentDirectory(p.Value);
                return true;
            });
        public static readonly Argument ARG_TIMESTAMP = new Argument(@"timestamp", (c, p) => c._out.IsTimeStamped = true);
        public static readonly Argument ARG_MEMSTAMP = new Argument(@"memstamp", (c, p) => c._out.IsMemStamped = true);
        public static readonly Argument ARG_LOG_FILE = new Argument(@"log-file", PATH_TO_FILE, (c, p) => c.LogFile = p.Value);
        public static readonly Argument ARG_HELP = new Argument(@"help",
            new[] { ARG_VALUE_ASCII, ARG_VALUE_NO_BORDERS },
            (c, p) => c.Usage(p.Value)) {OptionalValue = true};
        public const string ARG_VALUE_ASCII = "ascii";
        public const string ARG_VALUE_NO_BORDERS = "no-borders";
        public static readonly Argument ARG_VERSION = new Argument(@"version", (c, p) => c.Version());

        private static readonly ArgumentGroup GROUP_GENERAL_IO = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_GENERAL_IO_General_input_output, true,
            ARG_IN, ARG_SAVE, ARG_SAVE_SETTINGS, ARG_OUT, ARG_SHARE_ZIP, ARG_SHARE_TYPE, ARG_BATCH, ARG_DIR, ARG_TIMESTAMP, ARG_MEMSTAMP,
            ARG_LOG_FILE, ARG_HELP, ARG_VERSION)
        {
            Validate = c => c.ValidateGeneralArgs()
        };

        private void Version()
        {
            UsageShown = true;  // Keep from showing the full usage table
            _out.WriteLine(Install.ProgramNameAndVersion);
            VersionPwiz();
        }
        private void VersionPwiz()
        {
            UsageShown = true;  // Keep from showing the full usage table
            _out.WriteLine(@"    ProteoWizard MSData {0}", MsDataFileImpl.InstalledVersion);
        }

        private bool ValidateGeneralArgs()
        {
            // If SkylineFile isn't set and one of the commands that requires --in is called, complain.
            if (string.IsNullOrEmpty(SkylineFile) && RequiresSkylineDocument && !_isDocumentLoaded)
            {
                WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Use___in_to_specify_a_Skyline_document_to_open_);
                return false;
            }

            // Use the original file as the output file, if not told otherwise.
            if (Saving && string.IsNullOrEmpty(SaveFile))
            {
                SaveFile = SkylineFile;
            }

            return true;
        }

        public string LogFile { get; private set; }
        public string SkylineFile { get; private set; }
        public string SaveFile { get; private set; }
        private bool _saving;
        public bool Saving
        {
            get { return !String.IsNullOrEmpty(SaveFile) || _saving; }
            set { _saving = value; }
        }
        public bool SaveSettings { get; private set; }

        // For sharing zip file
        public bool SharingZipFile { get; private set; }
        public string SharedFile { get; private set; }
        public ShareType SharedFileType { get; private set; }

        public static readonly Argument ARG_IMPORT_FILE = new DocArgument(@"import-file", PATH_TO_FILE,
            (c, p) => c.ParseImportFile(p));
        public static readonly Argument ARG_IMPORT_REPLICATE_NAME = new DocArgument(@"import-replicate-name", NAME_VALUE,
            (c, p) => c.ReplicateName = p.Value);
        public static readonly Argument ARG_IMPORT_OPTIMIZING = new DocArgument(@"import-optimizing", new[] {OPT_CE, OPT_DP},
            (c, p) => c.ImportOptimizeType = p.Value);
        public static readonly Argument ARG_IMPORT_APPEND = new DocArgument(@"import-append", (c, p) => c.ImportAppend = true);
        public static readonly Argument ARG_IMPORT_ALL = new DocArgument(@"import-all", PATH_TO_FOLDER,
            (c, p) =>
            {
                c.ImportSourceDirectory = p.ValueFullPath;
                c.ImportRecursive = true;
            });
        public static readonly Argument ARG_IMPORT_ALL_FILES = new DocArgument(@"import-all-files", PATH_TO_FOLDER,
            (c, p) => c.ImportSourceDirectory = p.ValueFullPath);
        public static readonly Argument ARG_IMPORT_NAMING_PATTERN = new DocArgument(@"import-naming-pattern", REGEX_VALUE,
            (c, p) => c.ParseImportNamingPattern(p));
        public static readonly Argument ARG_IMPORT_FILENAME_PATTERN = new DocArgument(@"import-filename-pattern", REGEX_VALUE,
            (c, p) => c.ParseImportFileNamePattern(p));
        public static readonly Argument ARG_IMPORT_SAMPLENAME_PATTERN = new DocArgument(@"import-samplename-pattern", REGEX_VALUE,
            (c, p) => c.ParseImportSampleNamePattern(p));
        public static readonly Argument ARG_IMPORT_BEFORE = new DocArgument(@"import-before", DATE_VALUE,
            (c, p) => c.ImportBeforeDate = p.ValueDate);
        public static readonly Argument ARG_IMPORT_ON_OR_AFTER = new DocArgument(@"import-on-or-after", DATE_VALUE,
            (c, p) => c.ImportOnOrAfterDate = p.ValueDate);
        public static readonly Argument ARG_IMPORT_WARN_ON_FAILURE = new DocArgument(@"import-warn-on-failure",
            (c, p) => c.ImportWarnOnFailure = true);
        public static readonly Argument ARG_IMPORT_NO_JOIN = new DocArgument(@"import-no-join",
            (c, p) => c.ImportDisableJoining = true);
        public static readonly Argument ARG_IMPORT_PROCESS_COUNT = new Argument(@"import-process-count", INT_VALUE,
            (c, p) =>
            {
                c.ImportThreads = p.ValueInt;
                if (c.ImportThreads > 0)
                    Program.MultiProcImport = true;
            });
        public static readonly Argument ARG_IMPORT_THREADS = new Argument(@"import-threads", INT_VALUE,
            (c, p) => c.ImportThreads = p.ValueInt);
        public static readonly Argument ARG_IMPORT_LOCKMASS_POSITIVE = new DocArgument(@"import-lockmass-positive", NUM_VALUE,
            (c, p) => c.LockmassPositive = p.ValueDouble);
        public static readonly Argument ARG_IMPORT_LOCKMASS_NEGATIVE = new DocArgument(@"import-lockmass-negative", NUM_VALUE,
            (c, p) => c.LockmassNegative = p.ValueDouble);
        public static readonly Argument ARG_IMPORT_LOCKMASS_TOLERANCE = new DocArgument(@"import-lockmass-tolerance", NUM_VALUE,
            (c, p) => c.LockmassTolerance = p.ValueDouble);
        public static readonly Argument ARG_IMPORT_PEAK_BOUNDARIES = new DocArgument(@"import-peak-boundaries", PATH_TO_FILE,
            (c, p) => c.ImportPeakBoundariesPath = p.ValueFullPath);

        private static readonly ArgumentGroup GROUP_IMPORT = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_IMPORT_Importing_results_replicates, false,
            ARG_IMPORT_FILE, ARG_IMPORT_REPLICATE_NAME, ARG_IMPORT_OPTIMIZING, ARG_IMPORT_APPEND, ARG_IMPORT_ALL,
            ARG_IMPORT_ALL_FILES, ARG_IMPORT_NAMING_PATTERN, ARG_IMPORT_FILENAME_PATTERN, ARG_IMPORT_SAMPLENAME_PATTERN,
            ARG_IMPORT_BEFORE, ARG_IMPORT_ON_OR_AFTER, ARG_IMPORT_NO_JOIN, ARG_IMPORT_PROCESS_COUNT, ARG_IMPORT_THREADS,
            ARG_IMPORT_WARN_ON_FAILURE, ARG_IMPORT_LOCKMASS_POSITIVE, ARG_IMPORT_LOCKMASS_NEGATIVE, ARG_IMPORT_LOCKMASS_TOLERANCE, 
            ARG_IMPORT_PEAK_BOUNDARIES);

        public static readonly Argument ARG_REMOVE_BEFORE = new DocArgument(@"remove-before", DATE_VALUE,
            (c, p) => c.SetRemoveBefore(p.ValueDate));
        public static readonly Argument ARG_REMOVE_ALL = new DocArgument(@"remove-all",
            (c, p) => c.SetRemoveBefore(null));

        private void SetRemoveBefore(DateTime? date)
        {
            RemovingResults = true;
            RemoveBeforeDate = date;
        }

        private static readonly ArgumentGroup GROUP_REMOVE = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_REMOVE_Removing_results_replicates, false,
            ARG_REMOVE_BEFORE, ARG_REMOVE_ALL)
        {
            Validate = c => c.ValidateImportResultsArgs()
        };

        private bool ValidateImportResultsArgs()
        {
            // CONSIDER: Add declarative Exclusive arguments? So far only these two
            if (ImportingReplicateFile && ImportingSourceDirectory)
            {
                ErrorArgsExclusive(ARG_IMPORT_FILE, ARG_IMPORT_ALL);
                return false;
            }

            if (ImportingReplicateFile && ImportNamingPattern != null)
            {
                ErrorArgsExclusive(ARG_IMPORT_NAMING_PATTERN, ARG_IMPORT_FILE);
                return false;
            }

            return true;
        }

        public static readonly Argument ARG_CHROMATOGRAMS_LIMIT_NOISE = new DocArgument(@"chromatograms-limit-noise", NUM_VALUE,
            (c, p) => c.LimitNoise = p.ValueDouble);

        public static readonly Argument ARG_CHROMATOGRAMS_DISCARD_UNUSED = new DocArgument(@"chromatograms-discard-unused",
            (c, p) => c.ChromatogramsDiscard = true );

        private static readonly ArgumentGroup GROUP_MINIMIZE_RESULTS = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_MINIMIZE_RESULTS_Minimizing_results_file_size, false,
            ARG_CHROMATOGRAMS_LIMIT_NOISE, ARG_CHROMATOGRAMS_DISCARD_UNUSED)
        {
            Validate = c => c.ValidateMinimizeResultsArgs()
        };
        
        private bool ValidateMinimizeResultsArgs()
        {
            if (Minimizing)
            {
                if (!_seenArguments.Contains(ARG_SAVE) && !_seenArguments.Contains(ARG_OUT))
                {
                    // Has minimize argument(s), but no --save or --out command
                    if (ChromatogramsDiscard)
                    {
                        WarnArgRequirement(ARG_CHROMATOGRAMS_DISCARD_UNUSED, ARG_SAVE, ARG_OUT);
                    }
                    if (LimitNoise.HasValue)
                    {
                        WarnArgRequirement(ARG_CHROMATOGRAMS_LIMIT_NOISE, ARG_SAVE, ARG_OUT);
                    }
                    return false;
                }
            }
            return true;
        }


        public List<MsDataFileUri> ReplicateFile { get; private set; }
        public string ReplicateName { get; private set; }
        public int ImportThreads { get; private set; }
        public bool ImportAppend { get; private set; }
        public bool ImportDisableJoining { get; private set; }
        public bool ImportRecursive { get; private set; }
        public string ImportSourceDirectory { get; private set; }
        public Regex ImportNamingPattern { get; private set; }
        public Regex ImportFileNamePattern { get; private set; }
        public Regex ImportSampleNamePattern { get; private set; }
        public bool ImportWarnOnFailure { get; private set; }
        public string ImportPeakBoundariesPath { get; private set; }
        public bool RemovingResults { get; private set; }
        public DateTime? RemoveBeforeDate { get; private set; }
        public bool ChromatogramsDiscard{ get; private set; }
        public double? LimitNoise { get; private set; }
        public DateTime? ImportBeforeDate { get; private set; }
        public DateTime? ImportOnOrAfterDate { get; private set; }
        // Waters lockmass correction
        public double? LockmassPositive { get; private set; }
        public double? LockmassNegative { get; private set; }
        public double? LockmassTolerance { get; private set; }
        public LockMassParameters LockMassParameters { get { return new LockMassParameters(LockmassPositive, LockmassNegative, LockmassTolerance); } }

        private void ParseImportFile(NameValuePair pair)
        {
            ReplicateFile.Add(new MsDataFilePath(pair.ValueFullPath));
        }

        private bool ParseImportNamingPattern(NameValuePair pair)
        {
            var importNamingPatternVal = pair.Value;
            try
            {
                ImportNamingPattern = new Regex(importNamingPatternVal);
            }
            catch (Exception e)
            {
                WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Regular_expression__0__cannot_be_parsed_,
                    importNamingPatternVal);
                WriteLine(e.Message);
                return false;
            }

            // ReSharper disable LocalizableElement
            Match match = Regex.Match(importNamingPatternVal, @".*\(.+\).*");
            // ReSharper restore LocalizableElement
            if (!match.Success)
            {
                WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Regular_expression___0___does_not_have_any_groups___String,
                    importNamingPatternVal);
                return false;
            }

            return true;
        }

        private bool ParseImportFileNamePattern(NameValuePair pair)
        {
            return ParseRegexArgument(pair, r => ImportFileNamePattern = r);
        }

        private bool ParseImportSampleNamePattern(NameValuePair pair)
        {
            return ParseRegexArgument(pair, r => ImportSampleNamePattern = r);
        }

        private bool ParseRegexArgument(NameValuePair pair, Action<Regex> assign)
        {
            var regexText = pair.Value;
            try
            {
                assign(new Regex(regexText));
            }
            catch (Exception e)
            {
                WriteLine(Resources.CommandArgs_ParseRegexArgument_Error__Regular_expression___0___for__1__cannot_be_parsed_, regexText, pair.Match.ArgumentText);
                WriteLine(e.Message);
                return false;
            }
            return true;
        }

        // Document import
        public static readonly Argument ARG_IMPORT_DOCUMENT = new DocArgument(@"import-document", PATH_TO_DOCUMENT,
            (c, p) =>
            {
                c.DocImportPaths.Add(p.ValueFullPath);
                c.DocImportResultsMerge = c.DocImportResultsMerge ?? MeasuredResults.MergeAction.remove;
            });
        public static readonly Argument ARG_IMPORT_DOCUMENT_RESULTS = new DocArgument(@"import-document-results",
            Helpers.GetEnumValues<MeasuredResults.MergeAction>().Select(p => p.ToString()).ToArray(),
            (c, p) => c.DocImportResultsMerge = (MeasuredResults.MergeAction)Enum.Parse(typeof(MeasuredResults.MergeAction), p.Value, true))
            { WrapValue = true};
        public static readonly Argument ARG_IMPORT_DOCUMENT_MERGE_PEPTIDES = new DocArgument(@"import-document-merge-peptides",
            (c, p) => c.DocImportMergePeptides = true);

        private static readonly ArgumentGroup GROUP_IMPORT_DOC = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_IMPORT_DOC_Importing_other_Skyline_documents, false,
            ARG_IMPORT_DOCUMENT, ARG_IMPORT_DOCUMENT_RESULTS, ARG_IMPORT_DOCUMENT_MERGE_PEPTIDES)
        {
            LeftColumnWidth = 36,
            Dependencies =
            {
                { ARG_IMPORT_DOCUMENT_RESULTS, ARG_IMPORT_DOCUMENT },
                { ARG_IMPORT_DOCUMENT_MERGE_PEPTIDES, ARG_IMPORT_DOCUMENT }
            }
        };

        public bool ImportingDocuments { get { return DocImportPaths.Any(); } }
        public List<string> DocImportPaths { get; private set; }
        public MeasuredResults.MergeAction? DocImportResultsMerge { get; private set; }
        public bool DocImportMergePeptides { get; private set; }

        // Importing FASTA
        public static readonly Argument ARG_IMPORT_FASTA = new DocArgument(@"import-fasta", PATH_TO_FILE,
            (c, p) => c.FastaPath = p.ValueFullPath);
        public static readonly Argument ARG_KEEP_EMPTY_PROTEINS = new DocArgument(@"keep-empty-proteins",
            (c, p) => c.KeepEmptyProteins = true);

        private static readonly ArgumentGroup GROUP_FASTA = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_FASTA_Importing_FASTA_files, false,
            ARG_IMPORT_FASTA, ARG_KEEP_EMPTY_PROTEINS);

        public string FastaPath { get; private set; }
        public bool KeepEmptyProteins { get; private set; }

        // Transition list and assay library import
        public static readonly Argument ARG_IMPORT_TRANSITION_LIST = new DocArgument(@"import-transition-list", PATH_TO_FILE,
            (c, p) => c.ParseListPath(p, false));
        public static readonly Argument ARG_IMPORT_ASSAY_LIBRARY = new DocArgument(@"import-assay-library", PATH_TO_FILE,
            (c, p) => c.ParseListPath(p, true));
        public static readonly Argument ARG_IGNORE_TRANSITION_ERRORS = new DocArgument(@"ignore-transition-errors",
            (c, p) => c.IsIgnoreTransitionErrors = true);
        public static readonly Argument ARG_IRT_STANDARDS_GROUP_NAME = new DocArgument(@"irt-standards-group-name", NAME_VALUE,
            (c, p) => c.IrtGroupName = p.Value);
        public static readonly Argument ARG_IRT_STANDARDS_FILE = new DocArgument(@"irt-standards-file", PATH_TO_FILE,
            (c, p) => c.IrtStandardsPath = p.ValueFullPath);
        public static readonly Argument ARG_IRT_DATABASE_PATH = new DocArgument(@"irt-database-path", () => GetPathToFile(IrtDb.EXT),
            (c, p) => c.IrtDatabasePath = p.ValueFullPath);
        public static readonly Argument ARG_IRT_CALC_NAME = new DocArgument(@"irt-calc-name", NAME_VALUE,
            (c, p) => c.IrtCalcName = p.Value);

        private static readonly ArgumentGroup GROUP_IMPORT_LIST = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_IMPORT_LIST_Importing_transition_lists_and_assay_libraries, false,
            ARG_IMPORT_TRANSITION_LIST, ARG_IMPORT_ASSAY_LIBRARY, ARG_IGNORE_TRANSITION_ERRORS, ARG_IRT_STANDARDS_GROUP_NAME,
            ARG_IRT_STANDARDS_FILE, ARG_IRT_DATABASE_PATH, ARG_IRT_CALC_NAME)
        {
            Dependencies =
            {
                { ARG_IRT_STANDARDS_GROUP_NAME, ARG_IMPORT_ASSAY_LIBRARY },
                { ARG_IRT_STANDARDS_FILE, ARG_IMPORT_ASSAY_LIBRARY },
            },
            Validate = (c) =>
            {
                if (!c.ImportingTransitionList)   // Either --import-transition-list or --import-assay-library
                {
                    if (c.IsIgnoreTransitionErrors)
                       c. WarnArgRequirement(ARG_IGNORE_TRANSITION_ERRORS, ARG_IMPORT_TRANSITION_LIST);
                }
                return true;
            }
        };

        public string TransitionListPath { get; private set; }
        public bool IsTransitionListAssayLibrary { get; private set; }
        public bool IsIgnoreTransitionErrors { get; private set; }
        public string IrtGroupName { get; private set; }
        public string IrtStandardsPath { get; private set; }
        public string IrtDatabasePath { get; private set; }
        public string IrtCalcName { get; private set; }

        private void ParseListPath(NameValuePair pair, bool isAssayLib)
        {
            TransitionListPath = pair.ValueFullPath;
            IsTransitionListAssayLibrary = isAssayLib;
        }

        // Add a library
        public static readonly Argument ARG_ADD_LIBRARY_NAME = new DocArgument(@"add-library-name", NAME_VALUE,
            (c, p) => c.LibraryName = p.Value);
        public static readonly Argument ARG_ADD_LIBRARY_PATH = new DocArgument(@"add-library-path", PATH_TO_FILE,
            (c, p) => c.LibraryPath = p.ValueFullPath);

        private static readonly ArgumentGroup GROUP_ADD_LIBRARY = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_ADD_LIBRARY_Adding_spectral_libraries, false,
            ARG_ADD_LIBRARY_PATH, ARG_ADD_LIBRARY_NAME);

        public string LibraryName { get; private set; }
        public string LibraryPath { get; private set; }

        // Decoys
        public static readonly Argument ARG_DECOYS_ADD = new DocArgument(@"decoys-add",
            new[] {ARG_VALUE_DECOYS_ADD_REVERSE, ARG_VALUE_DECOYS_ADD_SHUFFLE},
            (c, p) => c.AddDecoysType = p.IsNameOnly || p.IsValue(ARG_VALUE_DECOYS_ADD_REVERSE)
                    ? DecoyGeneration.REVERSE_SEQUENCE
                    : DecoyGeneration.SHUFFLE_SEQUENCE)
            { OptionalValue = true};
        public const string ARG_VALUE_DECOYS_ADD_SHUFFLE = "shuffle";
        public const string ARG_VALUE_DECOYS_ADD_REVERSE = "reverse";
        public static readonly Argument ARG_DECOYS_ADD_COUNT = new DocArgument(@"decoys-add-count", INT_VALUE,
            (c, p) => c.AddDecoysCount = p.ValueInt);
        public static readonly Argument ARG_DECOYS_DISCARD = new DocArgument(@"decoys-discard",
            (c, p) => c.DiscardDecoys = true);

        private static readonly ArgumentGroup GROUP_DECOYS = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_DECOYS, false,
            ARG_DECOYS_ADD, ARG_DECOYS_ADD_COUNT, ARG_DECOYS_DISCARD)
        {
            Dependencies =
            {
                { ARG_DECOYS_ADD_COUNT, ARG_DECOYS_ADD },
            }
        };

        public string AddDecoysType { get; private set; }
        public int? AddDecoysCount { get; private set; }
        public bool DiscardDecoys { get; private set; }

        public bool AddDecoys
        {
            get { return !string.IsNullOrEmpty(AddDecoysType); }
        }
        public bool ImportingResults
        {
            get { return ImportingReplicateFile || ImportingSourceDirectory; }
        }
        public bool ImportingReplicateFile
        {
            get { return ReplicateFile.Count > 0; }
        }
        public bool ImportingSourceDirectory
        {
            get { return !string.IsNullOrEmpty(ImportSourceDirectory); }
        }

        public bool ImportingFasta
        {
            get { return !string.IsNullOrWhiteSpace(FastaPath); }
        }

        public bool ImportingTransitionList
        {
            get { return !string.IsNullOrWhiteSpace(TransitionListPath); }
        }

        public bool SettingLibraryPath
        {
            get { return !string.IsNullOrWhiteSpace(LibraryName) || !string.IsNullOrWhiteSpace(LibraryPath); }
        }

        // Annotations
        private static readonly Argument ARG_IMPORT_ANNOTATIONS = new DocArgument(@"import-annotations", () => GetPathToFile(TextUtil.EXT_CSV),
            (c, p) => c.ImportAnnotations = p.ValueFullPath);

        private static readonly ArgumentGroup GROUP_ANNOTATIONS = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_ANNOTATIONS_Importing_annotations, false,
            ARG_IMPORT_ANNOTATIONS);

        public string ImportAnnotations { get; private set; }

        // For reintegration
        private static readonly Argument ARG_REINTEGRATE_MODEL_NAME = new DocArgument(@"reintegrate-model-name", NAME_VALUE,
            (c, p) => c.ReintegrateModelName = p.Value);
        private static readonly Argument ARG_REINTEGRATE_CREATE_MODEL = new DocArgument(@"reintegrate-create-model",
            (c, p) =>
            {
                c.IsCreateScoringModel = true;
                if (!c.IsSecondBestModel)
                    c.IsDecoyModel = true;
            });
        private static readonly Argument ARG_REINTEGRATE_MODEL_TYPE = new DocArgument(@"reintegrate-model-type",
            Helpers.GetEnumValues<ScoringModelType>().Select(p => p.ToString()).ToArray(),
            (c, p) => c.ReintegrateModelType = (ScoringModelType)Enum.Parse(typeof(ScoringModelType), p.Value, true)) {WrapValue = true};
        private static readonly Argument ARG_REINTEGRATE_MODEL_CUTOFFS = new DocArgument(@"reintegrate-model-cutoffs", NUM_LIST_VALUE,
                (c, p) => c.ReintegrateModelCutoffs = c.ParseNumberList(p))
            { InternalUse = true };
        private static readonly Argument ARG_REINTEGRATE_MODEL_ITERATION_COUNT = new DocArgument(@"reintegrate-model-iteration-count", INT_VALUE,
            (c, p) => c.ReintegrateModelIterationCount = p.ValueInt) {InternalUse = true};
        private static readonly Argument ARG_REINTEGRATE_MODEL_SECOND_BEST = new DocArgument(@"reintegrate-model-second-best",
            (c, p) =>
            {
                c.IsSecondBestModel = true;
                c.IsDecoyModel = false;
            });
        private static readonly Argument ARG_REINTEGRATE_MODEL_BOTH = new DocArgument(@"reintegrate-model-both",
            (c, p) => c.IsDecoyModel = c.IsSecondBestModel = true);
        private static readonly Argument ARG_REINTEGRATE_OVERWRITE_PEAKS = new DocArgument(@"reintegrate-overwrite-peaks",
            (c, p) => c.IsOverwritePeaks = true);
        private static readonly Argument ARG_REINTEGRATE_LOG_TRAINING = new DocArgument(@"reintegrate-log-training",
            (c, p) => c.IsLogTraining = true) {InternalUse = true};
        private static readonly Argument ARG_REINTEGRATE_EXCLUDE_FEATURE = new DocArgument(@"reintegrate-exclude-feature", FEATURE_NAME_VALUE,
                (c, p) => c.ParseReintegrateExcludeFeature(p))
            { WrapValue = true };

        private static readonly ArgumentGroup GROUP_REINTEGRATE = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_REINTEGRATE_Reintegrate_with_advanced_peak_picking_models, false,
            ARG_REINTEGRATE_MODEL_NAME, ARG_REINTEGRATE_CREATE_MODEL, ARG_REINTEGRATE_MODEL_TYPE, ARG_REINTEGRATE_MODEL_ITERATION_COUNT,
            ARG_REINTEGRATE_MODEL_CUTOFFS, ARG_REINTEGRATE_MODEL_SECOND_BEST, ARG_REINTEGRATE_MODEL_BOTH, ARG_REINTEGRATE_OVERWRITE_PEAKS,
            ARG_REINTEGRATE_LOG_TRAINING, ARG_REINTEGRATE_EXCLUDE_FEATURE)
        {
            Dependencies =
            {
                { ARG_REINTEGRATE_CREATE_MODEL , ARG_REINTEGRATE_MODEL_NAME },
                { ARG_REINTEGRATE_MODEL_TYPE , ARG_REINTEGRATE_CREATE_MODEL },
                { ARG_REINTEGRATE_MODEL_CUTOFFS , ARG_REINTEGRATE_CREATE_MODEL },
                { ARG_REINTEGRATE_OVERWRITE_PEAKS , ARG_REINTEGRATE_MODEL_NAME },
                { ARG_REINTEGRATE_MODEL_SECOND_BEST, ARG_REINTEGRATE_CREATE_MODEL},
                { ARG_REINTEGRATE_MODEL_BOTH, ARG_REINTEGRATE_CREATE_MODEL},
                { ARG_REINTEGRATE_EXCLUDE_FEATURE, ARG_REINTEGRATE_CREATE_MODEL },
            },
            Validate = c => c.ValidateReintegrateArgs()
        };

        private bool ValidateReintegrateArgs()
        {
            if (ReintegrateModelType != ScoringModelType.mProphet && ExcludeFeatures.Count > 0)
            {
                WriteLine(Resources.CommandLine_CreateUntrainedScoringModel_Error__Excluding_feature_scores_is_not_permitted_with_the_default_Skyline_model_);
                return false;
            }

            if (ReintegrateModelCutoffs != null)
            {
                if (ReintegrateModelType == ScoringModelType.Skyline)
                {
                    WriteLine(Resources.CommandArgs_ValidateReintegrateArgs_Error__Model_cutoffs_cannot_be_applied_in_calibrating_the_Skyline_default_model_);
                    return false;
                }

                double maxCutoff = MProphetPeakScoringModel.DEFAULT_CUTOFFS[0];
                if (MonotonicallyDecreasing(ReintegrateModelCutoffs, maxCutoff))
                {
                    WriteLine(Resources.CommandArgs_ValidateReintegrateArgs_Error__Model_cutoffs___0___must_be_in_decreasing_order_greater_than_zero_and_less_than__1__, string.Join(
                        CultureInfo.CurrentCulture.TextInfo.ListSeparator, ReintegrateModelCutoffs.Select(c => c.ToString(CultureInfo.CurrentCulture))), maxCutoff);
                }
            }

            return true;
        }

        private bool MonotonicallyDecreasing(List<double> values, double maxValue)
        {
            double? lastValue = null;
            foreach (var value in values)
            {
                if (value > maxValue || (lastValue.HasValue && value >= lastValue.Value))
                    return false;
                lastValue = value;
            }

            return true;
        }

        public enum ScoringModelType
        {
            mProphet, // Full mProphet model (default)
            Skyline,  // Skyline default model with coefficients scaled to estimate unit normal distribution from decoys
            SkylineML // Skyline Machine Learning - essentially mProphet model with default set of features
        }

        public string ReintegrateModelName { get; private set; }
        public List<double> ReintegrateModelCutoffs { get; private set; }
        public int? ReintegrateModelIterationCount { get; private set; }
        public bool IsOverwritePeaks { get; private set; }
        public bool IsCreateScoringModel { get; private set; }
        public bool IsSecondBestModel { get; private set; }
        public bool IsDecoyModel { get; private set; }
        public bool IsLogTraining { get; private set; }
        public ScoringModelType ReintegrateModelType { get; private set; }
        public List<IPeakFeatureCalculator> ExcludeFeatures { get; private set; }

        public bool Reintegrating { get { return !string.IsNullOrEmpty(ReintegrateModelName); } }
        public bool Minimizing { get { return ChromatogramsDiscard || LimitNoise.HasValue; } }


        private List<double> ParseNumberList(NameValuePair pair)
        {
            try
            {
                return pair.Value.Split(new[] {CultureInfo.CurrentCulture.TextInfo.ListSeparator},
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(double.Parse).ToList();
            }
            catch (Exception)
            {
                throw new ValueInvalidNumberListException(pair.Match, pair.Value);
            }
        }

        private bool ParseReintegrateExcludeFeature(NameValuePair pair)
        {
            string featureName = pair.Value;
            var calc = PeakFeatureCalculator.Calculators.FirstOrDefault(c =>
                Equals(featureName, c.HeaderName) || Equals(featureName, c.Name));
            if (calc == null)
            {
                WriteLine(
                    Resources
                        .CommandArgs_ParseArgsInternal_Error__Attempting_to_exclude_an_unknown_feature_name___0____Try_one_of_the_following_,
                    featureName);
                foreach (var featureCalculator in PeakFeatureCalculator.Calculators)
                {
                    if (Equals(featureCalculator.HeaderName, featureCalculator.Name))
                        WriteLine(@"    {0}", featureCalculator.HeaderName);
                    else
                        WriteLine(Resources.CommandArgs_ParseArgsInternal______0__or___1__, featureCalculator.HeaderName,
                            featureCalculator.Name);
                }

                return false;
            }

            ExcludeFeatures.Add(calc);
            return true;
        }

        // Refinement
        public static readonly Argument ARG_REFINE_MIN_PEPTIDES = new RefineArgument(@"refine-min-peptides", INT_VALUE,
            (c, p) => c.Refinement.MinPeptidesPerProtein = p.ValueInt);
        public static readonly Argument ARG_REFINE_REMOVE_REPEATS = new RefineArgument(@"refine-remove-repeats",
            (c, p) => c.Refinement.RemoveRepeatedPeptides = true);
        public static readonly Argument ARG_REFINE_REMOVE_DUPLICATES = new RefineArgument(@"refine-remove-duplicates",
            (c, p) => c.Refinement.RemoveDuplicatePeptides = true);
        public static readonly Argument ARG_REFINE_MISSING_LIBRARY = new RefineArgument(@"refine-missing-library",
            (c, p) => c.Refinement.RemoveMissingLibrary = true);
        public static readonly Argument ARG_REFINE_MIN_TRANSITIONS = new RefineArgument(@"refine-min-transitions", INT_VALUE,
            (c, p) => c.Refinement.MinTransitionsPepPrecursor = p.ValueInt);
        public static readonly Argument ARG_REFINE_LABEL_TYPE = new RefineArgument(@"refine-label-type", LABEL_VALUE,
            (c, p) => c.RefinementLabelTypeName = p.Value);
        public static readonly Argument ARG_REFINE_ADD_LABEL_TYPE = new RefineArgument(@"refine-add-label-type", 
            (c, p) => c.Refinement.AddLabelType = true);
        public static readonly Argument ARG_REFINE_AUTOSEL_PEPTIDES = new RefineArgument(@"refine-auto-select-peptides",
            (c, p) => c.Refinement.AutoPickChildrenAll = c.Refinement.AutoPickChildrenAll | PickLevel.peptides);
        public static readonly Argument ARG_REFINE_AUTOSEL_PRECURSORS = new RefineArgument(@"refine-auto-select-precursors",
            (c, p) => c.Refinement.AutoPickChildrenAll = c.Refinement.AutoPickChildrenAll | PickLevel.precursors);
        public static readonly Argument ARG_REFINE_AUTOSEL_TRANSITIONS = new RefineArgument(@"refine-auto-select-transitions",
            (c, p) => c.Refinement.AutoPickChildrenAll = c.Refinement.AutoPickChildrenAll | PickLevel.transitions);
        // Refinement requiring imported results
        public static readonly Argument ARG_REFINE_MIN_PEAK_FOUND_RATIO = new RefineArgument(@"refine-min-peak-found-ratio", NUM_VALUE,
            (c, p) => c.Refinement.MinPeakFoundRatio = p.ValueDouble) { WrapValue = true };
        public static readonly Argument ARG_REFINE_MAX_PEAK_FOUND_RATIO = new RefineArgument(@"refine-max-peak-found-ratio", NUM_VALUE,
            (c, p) => c.Refinement.MaxPeakFoundRatio = p.ValueDouble) { WrapValue = true };
        public static readonly Argument ARG_REFINE_MAX_PEPTIDE_PEAK_RANK = new RefineArgument(@"refine-max-peptide-peak-rank", INT_VALUE,
            (c, p) => c.Refinement.MaxPepPeakRank = p.ValueInt) { WrapValue = true };
        public static readonly Argument ARG_REFINE_MAX_PEAK_RANK = new RefineArgument(@"refine-max-transition-peak-rank", INT_VALUE,
            (c, p) => c.Refinement.MaxPeakRank = p.ValueInt) { WrapValue = true };
        public static readonly Argument ARG_REFINE_MAX_PRECURSOR_PEAK_ONLY = new RefineArgument(@"refine-max-precursor-only", 
            (c, p) => c.Refinement.MaxPrecursorPeakOnly = true);
        public static readonly Argument ARG_REFINE_PREFER_LARGER_PRODUCTS = new RefineArgument(@"refine-prefer-larger-products",
            (c, p) => c.Refinement.PreferLargeIons = true);
        public static readonly Argument ARG_REFINE_MISSING_RESULTS = new RefineArgument(@"refine-missing-results",
            (c, p) => c.Refinement.RemoveMissingResults = true);
        public static readonly Argument ARG_REFINE_MIN_TIME_CORRELATION = new RefineArgument(@"refine-min-time-correlation", NUM_VALUE,
            (c, p) => c.Refinement.RTRegressionThreshold = p.ValueDouble) { WrapValue = true };
        public static readonly Argument ARG_REFINE_MIN_DOTP = new RefineArgument(@"refine-min-dotp", NUM_VALUE,
            (c, p) => c.Refinement.DotProductThreshold = p.ValueDouble);
        public static readonly Argument ARG_REFINE_MIN_IDOTP = new RefineArgument(@"refine-min-idotp", NUM_VALUE,
            (c, p) => c.Refinement.IdotProductThreshold = p.ValueDouble);
        public static readonly Argument ARG_REFINE_USE_BEST_RESULT = new RefineArgument(@"refine-use-best-result",
            (c, p) => c.Refinement.UseBestResult = true);
        // Refinement consistency tab
        public static readonly Argument ARG_REFINE_CV_REMOVE_ABOVE_CUTOFF = new RefineArgument(@"refine-cv-remove-above-cutoff", NUM_VALUE,
            (c,p) => c.Refinement.CVCutoff = p.ValueDouble >= 1 ? p.ValueDouble : p.ValueDouble * 100);  // If a value like 0.2, interpret as 20%
        public static readonly Argument ARG_REFINE_CV_GLOBAL_NORMALIZE = new RefineArgument(@"refine-cv-global-normalize",
            new[] { NormalizationMethod.GLOBAL_STANDARDS.Name, NormalizationMethod.EQUALIZE_MEDIANS.Name, NormalizationMethod.TIC.Name },
            (c, p) =>
            {
                if (p.Value == NormalizationMethod.GLOBAL_STANDARDS.Name)
                {
                    c.Refinement.NormalizationMethod = NormalizeOption.FromNormalizationMethod(NormalizationMethod.GLOBAL_STANDARDS);
                }
                else if (p.Value == NormalizationMethod.TIC.Name)
                {
                    c.Refinement.NormalizationMethod = NormalizeOption.FromNormalizationMethod(NormalizationMethod.TIC);
                }
                else
                {
                    c.Refinement.NormalizationMethod = NormalizeOption.FromNormalizationMethod(NormalizationMethod.EQUALIZE_MEDIANS);
                }
            }) { WrapValue = true };
        public static readonly Argument ARG_REFINE_CV_REFERENCE_NORMALIZE = new RefineArgument(@"refine-cv-reference-normalize", LABEL_VALUE,
            (c, p) =>
            {
                c.Refinement.NormalizationMethod = NormalizeOption.FromNormalizationMethod(NormalizationMethod.FromIsotopeLabelTypeName(p.Value));
            }) { WrapValue = true };
        public static readonly Argument ARG_REFINE_CV_TRANSITIONS = new RefineArgument(@"refine-cv-transitions",
            new[] { AreaCVTransitions.all.ToString(), AreaCVTransitions.best.ToString() },
            (c, p) =>
            {
                c.Refinement.Transitions = (AreaCVTransitions)Enum.Parse(typeof(AreaCVTransitions), p.Value, false);
                c.Refinement.CountTransitions = -1;
            });
        public static readonly Argument ARG_REFINE_CV_TRANSITIONS_COUNT = new RefineArgument(@"refine-cv-transitions-count", INT_VALUE,
            (c, p) =>
            {
                c.Refinement.Transitions = AreaCVTransitions.count;
                c.Refinement.CountTransitions = p.ValueInt;
            });
        public static readonly Argument ARG_REFINE_CV_MS_LEVEL = new RefineArgument(@"refine-cv-ms-level",
            Helpers.GetEnumValues<AreaCVMsLevel>().Select(e => e.ToString()).ToArray(),
            (c, p) => c.Refinement.MSLevel = (AreaCVMsLevel) Enum.Parse(typeof(AreaCVMsLevel), p.Value, true));
        public static readonly Argument ARG_REFINE_QVALUE_CUTOFF = new RefineArgument(@"refine-qvalue-cutoff", NUM_VALUE,
            (c, p) => c.Refinement.QValueCutoff = p.ValueDouble);
        public static readonly Argument ARG_REFINE_MINIMUM_DETECTIONS = new RefineArgument(@"refine-minimum-detections", INT_VALUE,
            (c, p) => c.Refinement.MinimumDetections = p.ValueInt);
        // Refinement Group Comparison Tab
        public static readonly Argument ARG_REFINE_GC_P_VALUE_CUTOFF = new RefineArgument(
            @"refine-gc-p-value-cutoff", NUM_VALUE,
            (c, p) => c.Refinement.AdjustedPValueCutoff = p.ValueDouble);
        public static readonly Argument ARG_REFINE_GC_FOLD_CHANGE_CUTOFF = new RefineArgument(@"refine-gc-fold-change-cutoff",
            NUM_VALUE,
            (c, p) => c.Refinement.FoldChangeCutoff = Math.Log(p.ValueDouble, 2));
        public static readonly Argument ARG_REFINE_GC_MS_LEVEL = new RefineArgument(@"refine-gc-ms-level", NUM_VALUE,
            (c, p) => c.Refinement.MSLevelGroupComparison = p.ValueInt);
        public static readonly Argument ARG_REFINE_GROUP_NAME = new RefineArgument(@"refine-gc-name", LABEL_VALUE,
            (c, p) => c.Refinement.GroupComparisonNames.Add(p.Value));

        private static readonly ArgumentGroup GROUP_REFINEMENT = new ArgumentGroup(
            () => CommandArgUsage.CommandArgs_GROUP_REFINEMENT, false,
            ARG_REFINE_MIN_PEPTIDES, ARG_REFINE_REMOVE_REPEATS, ARG_REFINE_REMOVE_DUPLICATES,
            ARG_REFINE_MISSING_LIBRARY, ARG_REFINE_MIN_TRANSITIONS, ARG_REFINE_LABEL_TYPE,
            ARG_REFINE_ADD_LABEL_TYPE, ARG_REFINE_AUTOSEL_PEPTIDES, ARG_REFINE_AUTOSEL_PRECURSORS,
            ARG_REFINE_AUTOSEL_TRANSITIONS);

        private static readonly ArgumentGroup GROUP_REFINEMENT_W_RESULTS = new ArgumentGroup(
            () => CommandArgUsage.CommandArgs_GROUP_REFINEMENT_W_RESULTS, false,
            ARG_REFINE_MIN_PEAK_FOUND_RATIO, ARG_REFINE_MAX_PEAK_FOUND_RATIO, ARG_REFINE_MAX_PEPTIDE_PEAK_RANK,
            ARG_REFINE_MAX_PEAK_RANK, ARG_REFINE_MAX_PRECURSOR_PEAK_ONLY,
            ARG_REFINE_PREFER_LARGER_PRODUCTS, ARG_REFINE_MISSING_RESULTS,
            ARG_REFINE_MIN_TIME_CORRELATION, ARG_REFINE_MIN_DOTP, ARG_REFINE_MIN_IDOTP,
            ARG_REFINE_USE_BEST_RESULT,
            ARG_REFINE_CV_REMOVE_ABOVE_CUTOFF, ARG_REFINE_CV_GLOBAL_NORMALIZE, ARG_REFINE_CV_REFERENCE_NORMALIZE,
            ARG_REFINE_CV_TRANSITIONS, ARG_REFINE_CV_TRANSITIONS_COUNT, ARG_REFINE_CV_MS_LEVEL,
            ARG_REFINE_QVALUE_CUTOFF, ARG_REFINE_MINIMUM_DETECTIONS,
            ARG_REFINE_GC_P_VALUE_CUTOFF, ARG_REFINE_GC_FOLD_CHANGE_CUTOFF, ARG_REFINE_GC_MS_LEVEL, ARG_REFINE_GROUP_NAME);
        

        public RefinementSettings Refinement { get; private set; }
        public string RefinementLabelTypeName { get; private set; }   // Must store as string until document is instantiated
        public string RefinementCvLabelTypeName { get; private set; }   // Must store as string until document is instantiated


        // For exporting reports
        // Adding reports does not require a document
        public static readonly Argument ARG_REPORT_NAME = new Argument(@"report-name", NAME_VALUE,
            (c, p) => c.ReportName = p.Value);
        public static readonly Argument ARG_REPORT_ADD = new Argument(@"report-add", () => GetPathToFile(ReportSpecList.EXT_REPORTS),
            (c, p) =>
            {
                c.ImportingSkyr = true;
                c.SkyrPath = p.ValueFullPath;
            });
        public static readonly Argument ARG_REPORT_CONFLICT_RESOLUTION = new Argument(@"report-conflict-resolution",
            new []{ARG_VALUE_OVERWRITE, ARG_VALUE_SKIP},
            (c, p) => c.ResolveSkyrConflictsBySkipping = p.IsValue(ARG_VALUE_SKIP)) { WrapValue = true };
        // Exporting reports does require a document
        public static readonly Argument ARG_REPORT_FILE = new DocArgument(@"report-file", () => GetPathToFile(TextUtil.EXT_CSV),
            (c, p) => c.ReportFile = p.ValueFullPath);
        public static readonly Argument ARG_REPORT_FORMAT = new DocArgument(@"report-format",
            new []{ARG_VALUE_CSV, ARG_VALUE_TSV},
            (c, p) => c.ReportColumnSeparator = p.IsValue(ARG_VALUE_TSV)
                    ? TextUtil.SEPARATOR_TSV
                    : TextUtil.CsvSeparator);
        public const string ARG_VALUE_CSV = "csv";
        public const string ARG_VALUE_TSV = "tsv";
        public static readonly Argument ARG_REPORT_INVARIANT = new DocArgument(@"report-invariant",
            (c, p) => c.IsReportInvariant = true);

        private static readonly ArgumentGroup GROUP_REPORT = new ArgumentGroup(
            () => CommandArgUsage.CommandArgs_GROUP_REPORT_Exporting_reports, false,
            ARG_REPORT_NAME, ARG_REPORT_FILE, ARG_REPORT_ADD, ARG_REPORT_CONFLICT_RESOLUTION, ARG_REPORT_FORMAT,
            ARG_REPORT_INVARIANT);

        public string ReportName { get; private set; }
        public char ReportColumnSeparator { get; private set; }
        public string ReportFile { get; private set; }
        public bool IsReportInvariant { get; private set; }
        public bool ExportingReport
        {
            get { return !string.IsNullOrEmpty(ReportName); }
        }
        // For adding a skyr file to user.config
        public string SkyrPath { get; private set; }
        private bool _importingSkyr;
        public bool ImportingSkyr
        {
            get { return !string.IsNullOrEmpty(SkyrPath) || _importingSkyr; }
            set { _importingSkyr = value; }
        }
        public bool? ResolveSkyrConflictsBySkipping { get; private set; }

        // For exporting chromatograms
        private static readonly Argument ARG_CHROMATOGRAM_FILE = new DocArgument(@"chromatogram-file", () => GetPathToFile(TextUtil.EXT_TSV),
            (c, p) => c.ChromatogramsFile = p.ValueFullPath);
        private static readonly Argument ARG_CHROMATOGRAM_PRECURSORS = new DocArgument(@"chromatogram-precursors",
            (c, p) => c.ChromatogramsPrecursors = true);
        private static readonly Argument ARG_CHROMATOGRAM_PRODUCTS = new DocArgument(@"chromatogram-products",
            (c, p) => c.ChromatogramsProducts = true);
        private static readonly Argument ARG_CHROMATOGRAM_BASE_PEAKS = new DocArgument(@"chromatogram-base-peaks",
            (c, p) => c.ChromatogramsBasePeaks = true);
        private static readonly Argument ARG_CHROMATOGRAM_TICS = new DocArgument(@"chromatogram-tics",
            (c, p) => c.ChromatogramsTics = true);

        private static readonly ArgumentGroup GROUP_CHROMATOGRAM = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_CHROMATOGRAM_Exporting_chromatograms, false,
            ARG_CHROMATOGRAM_FILE, ARG_CHROMATOGRAM_PRECURSORS, ARG_CHROMATOGRAM_PRODUCTS, ARG_CHROMATOGRAM_BASE_PEAKS,
            ARG_CHROMATOGRAM_TICS)
        {
            Dependencies =
            {
                { ARG_CHROMATOGRAM_PRECURSORS, ARG_CHROMATOGRAM_FILE },
                { ARG_CHROMATOGRAM_PRODUCTS, ARG_CHROMATOGRAM_FILE },
                { ARG_CHROMATOGRAM_BASE_PEAKS, ARG_CHROMATOGRAM_FILE },
                { ARG_CHROMATOGRAM_TICS, ARG_CHROMATOGRAM_FILE },
            },
            Validate = c => c.ValidateChromatogramArgs()
        };

        private bool ValidateChromatogramArgs()
        {
            if (ExportingChromatograms)
            {
                if (!ChromatogramsPrecursors && !ChromatogramsProducts && !ChromatogramsBasePeaks && !ChromatogramsTics)
                    ChromatogramsPrecursors = ChromatogramsProducts = true;
            }

            return true;
        }

        public string ChromatogramsFile { get; private set; }
        public bool ChromatogramsPrecursors { get; private set; }
        public bool ChromatogramsProducts { get; private set; }
        public bool ChromatogramsBasePeaks { get; private set; }
        public bool ChromatogramsTics { get; private set; }
        public bool ExportingChromatograms { get { return !string.IsNullOrEmpty(ChromatogramsFile); } }


        // For publishing the document to Panorama
        private static readonly Argument ARG_PANORAMA_SERVER = new DocArgument(@"panorama-server", SERVER_URL_VALUE,
            (c, p) => c.PanoramaServerUri = p.Value);
        private static readonly Argument ARG_PANORAMA_USERNAME = new DocArgument(@"panorama-username", USERNAME_VALUE,
            (c, p) => c.PanoramaUserName = p.Value);
        private static readonly Argument ARG_PANORAMA_PASSWORD = new DocArgument(@"panorama-password", PASSWORD_VALUE,
            (c, p) => c.PanoramaPassword = p.Value);
        private static readonly Argument ARG_PANORAMA_FOLDER = new DocArgument(@"panorama-folder", PATH_TO_FOLDER,
            (c, p) => c.PanoramaFolder = p.Value);

        private static readonly ArgumentGroup GROUP_PANORAMA = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_PANORAMA_Publishing_to_Panorama, false,
            ARG_PANORAMA_SERVER, ARG_PANORAMA_USERNAME, ARG_PANORAMA_PASSWORD, ARG_PANORAMA_FOLDER
        )
        {
            Validate = c => c.ValidatePanoramaArgs(),
            Postamble = () => CommandArgUsage.CommandArgs_GROUP_PANORAMA_postamble
        };

        private string PanoramaServerUri { get; set; }
        private string PanoramaUserName { get; set; }
        private string PanoramaPassword { get; set; }
        public string PanoramaFolder { get; private set; }
        public bool PublishingToPanorama { get; private set; }
        public Server PanoramaServer { get; private set; }

        private bool ValidatePanoramaArgs()
        {
            if (!string.IsNullOrEmpty(PanoramaServerUri) || !string.IsNullOrEmpty(PanoramaFolder))
            {
                if (!PanoramaArgsComplete())
                    return false;

                var serverUri = PanoramaUtil.ServerNameToUri(PanoramaServerUri);
                if (serverUri == null)
                {
                    WriteLine(Resources.CommandLine_GeneralException_Error___0_, 
                        string.Format(Resources.EditServerDlg_OkDialog_The_text__0__is_not_a_valid_server_name_, PanoramaServerUri));
                    return false;
                }

                var panoramaClient = new WebPanoramaClient(serverUri);
                var panoramaHelper = new PanoramaHelper(_out); // Helper writes messages for failures below
                PanoramaServer = panoramaHelper.ValidateServer(panoramaClient, PanoramaUserName, PanoramaPassword);
                if (PanoramaServer == null)
                    return false;

                if (!panoramaHelper.ValidateFolder(panoramaClient, PanoramaServer, PanoramaFolder))
                    return false;

                PublishingToPanorama = true;
            }

            return true;
        }

        private bool PanoramaArgsComplete()
        {
            var missingArgs = new List<string>();
            if (string.IsNullOrWhiteSpace(PanoramaServerUri))
            {
                missingArgs.Add(ARG_PANORAMA_SERVER.ArgumentText);
            }
            if (string.IsNullOrWhiteSpace(PanoramaUserName))
            {
                missingArgs.Add(ARG_PANORAMA_USERNAME.ArgumentText);
            }
            if (string.IsNullOrWhiteSpace(PanoramaPassword))
            {
                missingArgs.Add(ARG_PANORAMA_PASSWORD.ArgumentText);
            }
            if (string.IsNullOrWhiteSpace(PanoramaFolder))
            {
                missingArgs.Add(ARG_PANORAMA_FOLDER.ArgumentText);
            }

            if (missingArgs.Count > 0)
            {
                WriteLine(missingArgs.Count > 1
                        ? Resources.CommandArgs_PanoramaArgsComplete_plural_
                        : Resources.CommandArgs_PanoramaArgsComplete_,
                    TextUtil.LineSeparate(missingArgs));
                return false;
            }

            return true;
        }

        public class PanoramaHelper
        {
            private readonly TextWriter _statusWriter;

            public PanoramaHelper(TextWriter statusWriter)
            {
                _statusWriter = statusWriter;
            }

            public Server ValidateServer(IPanoramaClient panoramaClient, string panoramaUsername, string panoramaPassword)
            {
                try
                {
                    PanoramaUtil.VerifyServerInformation(panoramaClient, panoramaUsername, panoramaPassword);
                    return new Server(panoramaClient.ServerUri, panoramaUsername, panoramaPassword);
                }
                catch (PanoramaServerException x)
                {
                    _statusWriter.WriteLine(Resources.PanoramaHelper_ValidateServer_PanoramaServerException_, x.Message);
                }
                catch (Exception x)
                {
                    _statusWriter.WriteLine(Resources.PanoramaHelper_ValidateServer_Exception_, x.Message);
                }

                return null;
            }

            public bool ValidateFolder(IPanoramaClient panoramaClient, Server server, string panoramaFolder)
            {
                try
                {
                    PanoramaUtil.VerifyFolder(panoramaClient, server, panoramaFolder);
                    return true;
                }
                catch (PanoramaServerException x)
                {
                    _statusWriter.WriteLine(Resources.PanoramaHelper_ValidateFolder_PanoramaServerException_, x.Message);
                }
                catch (Exception x)
                {
                    _statusWriter.WriteLine(
                        Resources.PanoramaHelper_ValidateFolder_Exception_,
                        panoramaFolder, panoramaClient.ServerUri,
                        x.Message);
                }
                return false;
            }
        }

        // For importing a tool.
        public string ToolName { get; private set; }
        public string ToolCommand { get; private set; }
        public string ToolArguments { get; private set; }
        public string ToolInitialDirectory { get; private set; }
        public string ToolReportTitle { get; private set; }
        public bool ToolOutputToImmediateWindow { get; private set; }
        private bool _importingTool;
        public bool ImportingTool
        {
            get { return !string.IsNullOrEmpty(ToolName) || _importingTool; }
            set { _importingTool = value; }
        }
        public bool? ResolveToolConflictsBySkipping { get; private set; }

        // For importing a peptide search
        public static readonly Argument ARG_IMPORT_PEPTIDE_SEARCH_FILE = new DocArgument(@"import-search-file", PATH_TO_FILE,
            (c, p) =>
            {
                c.SearchResultsFiles.Add(p.ValueFullPath);
                c.CutoffScore = c.CutoffScore ?? Settings.Default.LibraryResultCutOff;
                c.IrtStandardName = null;
                c.NumCirts = null;
                c.RecalibrateIrts = false;
            });
        public static readonly Argument ARG_IMPORT_PEPTIDE_SEARCH_CUTOFF = new Argument(@"import-search-cutoff-score", NUM_VALUE,
            (c, p) => c.CutoffScore = p.GetValueDouble(0, 1));
        public static readonly Argument ARG_IMPORT_PEPTIDE_SEARCH_IRTS = new Argument(@"import-search-irts", NAME_VALUE,
            (c, p) => c.IrtStandardName = p.Value);
        public static readonly Argument ARG_IMPORT_PEPTIDE_SEARCH_NUM_CIRTS = new Argument(@"import-search-num-cirts", INT_VALUE,
            (c, p) => c.NumCirts = p.ValueInt);
        public static readonly Argument ARG_IMPORT_PEPTIDE_SEARCH_RECALIBRATE_IRTS = new Argument(@"import-search-recalibrate-irts",
            (c, p) => c.RecalibrateIrts = true);
        public static readonly Argument ARG_IMPORT_PEPTIDE_SEARCH_MODS = new Argument(@"import-search-add-mods",
            (c, p) => c.AcceptAllModifications = true);
        public static readonly Argument ARG_IMPORT_PEPTIDE_SEARCH_AMBIGUOUS = new Argument(@"import-search-include-ambiguous",
            (c, p) => c.IncludeAmbiguousMatches = true);
        public static readonly Argument ARG_IMPORT_PEPTIDE_SEARCH_PREFER_EMBEDDED = new Argument(@"import-search-prefer-embedded-spectra",
            (c, p) => c.PreferEmbeddedSpectra = true);

        private static readonly ArgumentGroup GROUP_IMPORT_SEARCH = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_IMPORT_SEARCH_Importing_peptide_searches, false, 
            ARG_IMPORT_PEPTIDE_SEARCH_FILE, ARG_IMPORT_PEPTIDE_SEARCH_CUTOFF, ARG_IMPORT_PEPTIDE_SEARCH_IRTS, ARG_IMPORT_PEPTIDE_SEARCH_NUM_CIRTS,
            ARG_IMPORT_PEPTIDE_SEARCH_RECALIBRATE_IRTS, ARG_IMPORT_PEPTIDE_SEARCH_MODS, ARG_IMPORT_PEPTIDE_SEARCH_AMBIGUOUS, ARG_IMPORT_PEPTIDE_SEARCH_PREFER_EMBEDDED)
        {
            Dependencies =
            {
                { ARG_IMPORT_PEPTIDE_SEARCH_CUTOFF, ARG_IMPORT_PEPTIDE_SEARCH_FILE },
                { ARG_IMPORT_PEPTIDE_SEARCH_IRTS, ARG_IMPORT_PEPTIDE_SEARCH_FILE },
                { ARG_IMPORT_PEPTIDE_SEARCH_NUM_CIRTS, ARG_IMPORT_PEPTIDE_SEARCH_IRTS },
                { ARG_IMPORT_PEPTIDE_SEARCH_RECALIBRATE_IRTS, ARG_IMPORT_PEPTIDE_SEARCH_IRTS },
                { ARG_IMPORT_PEPTIDE_SEARCH_MODS, ARG_IMPORT_PEPTIDE_SEARCH_FILE },
                { ARG_IMPORT_PEPTIDE_SEARCH_AMBIGUOUS, ARG_IMPORT_PEPTIDE_SEARCH_FILE },
                { ARG_IMPORT_PEPTIDE_SEARCH_PREFER_EMBEDDED, ARG_IMPORT_PEPTIDE_SEARCH_FILE },
            }
        };

        public List<string> SearchResultsFiles { get; private set; }
        public double? CutoffScore { get; private set; }
        public string IrtStandardName { get; private set; }
        public int? NumCirts { get; private set; }
        public bool RecalibrateIrts { get; private set; }
        public bool AcceptAllModifications { get; private set; }
        public bool IncludeAmbiguousMatches { get; private set; }
        public bool? PreferEmbeddedSpectra { get; private set; }
        public bool ImportingSearch
        {
            get { return SearchResultsFiles.Count > 0; }
        }

        // For adjusting transition filter and full-scan settings
        public static readonly Argument ARG_TRAN_PRECURSOR_ION_CHARGES = new DocArgument(@"tran-precursor-ion-charges", INT_LIST_VALUE,
                (c, p) => c.FilterPrecursorCharges = ParseIonCharges(p, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE))
            { WrapValue = true };
        public static readonly Argument ARG_TRAN_FRAGMENT_ION_CHARGES = new DocArgument(@"tran-product-ion-charges", INT_LIST_VALUE,
                (c, p) => c.FilterProductCharges = ParseIonCharges(p, Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE))
            { WrapValue = true };
        public static readonly Argument ARG_TRAN_FRAGMENT_ION_TYPES = new DocArgument(@"tran-product-ion-types", ION_TYPE_LIST_VALUE,
            (c, p) => c.FilterProductTypes = ParseIonTypes(p)) { WrapValue = true };
        public static readonly Argument ARG_TRAN_PREDICT_CE = new DocArgument(@"tran-predict-ce", () => GetDisplayNames(Settings.Default.CollisionEnergyList),
            (c, p) => c.PredictCEName = p.Value) { WrapValue = true };
        public static readonly Argument ARG_TRAN_PREDICT_DP = new DocArgument(@"tran-predict-dp", () => GetDisplayNames(Settings.Default.DeclusterPotentialList),
            (c, p) => c.PredictDPName = p.Value) { WrapValue = true };
        public static readonly Argument ARG_TRAN_PREDICT_COV = new DocArgument(@"tran-predict-cov", () => GetDisplayNames(Settings.Default.CompensationVoltageList),
            (c, p) => c.PredictCoVName = p.Value) { WrapValue = true };
        public static readonly Argument ARG_TRAN_PREDICT_OPTDB = new DocArgument(@"tran-predict-optdb", () => GetDisplayNames(Settings.Default.OptimizationLibraryList),
            (c, p) => c.PredictOpimizationLibraryName = p.Value) { WrapValue = true };
        public static readonly Argument ARG_FULL_SCAN_PRECURSOR_RES = new DocArgument(@"full-scan-precursor-res", RP_VALUE,
            (c, p) => c.FullScanPrecursorRes = p.ValueDouble) { WrapValue = true };
        public static readonly Argument ARG_FULL_SCAN_PRECURSOR_RES_MZ = new DocArgument(@"full-scan-precursor-res-mz", MZ_VALUE,
            (c, p) => c.FullScanPrecursorResMz = p.ValueDouble) { WrapValue = true };
        public static readonly Argument ARG_FULL_SCAN_PRODUCT_RES = new DocArgument(@"full-scan-product-res", RP_VALUE,
            (c, p) => c.FullScanProductRes = p.ValueDouble) { WrapValue = true };
        public static readonly Argument ARG_FULL_SCAN_PRODUCT_RES_MZ = new DocArgument(@"full-scan-product-res-mz", MZ_VALUE,
            (c, p) => c.FullScanProductResMz = p.ValueDouble) { WrapValue = true };
        public static readonly Argument ARG_FULL_SCAN_RT_FILTER_TOLERANCE = new DocArgument(@"full-scan-rt-filter-tolerance", MINUTES_VALUE,
            (c, p) => c.FullScanRetentionTimeFilterLength = p.ValueDouble) { WrapValue = true };
        public static readonly Argument ARG_IMS_LIBRARY_RES = new DocArgument(@"ims-library-res", RP_VALUE,
                (c, p) => c.IonMobilityLibraryRes = p.ValueDouble)
            { WrapValue = true };

        private static readonly ArgumentGroup GROUP_SETTINGS = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_SETTINGS_Document_Settings, false,
            ARG_TRAN_PRECURSOR_ION_CHARGES, ARG_TRAN_FRAGMENT_ION_CHARGES, ARG_TRAN_FRAGMENT_ION_TYPES,
            ARG_TRAN_PREDICT_CE, ARG_TRAN_PREDICT_DP, ARG_TRAN_PREDICT_COV, ARG_TRAN_PREDICT_OPTDB,
            ARG_FULL_SCAN_PRECURSOR_RES, ARG_FULL_SCAN_PRECURSOR_RES_MZ,
            ARG_FULL_SCAN_PRODUCT_RES, ARG_FULL_SCAN_PRODUCT_RES_MZ,
            ARG_FULL_SCAN_RT_FILTER_TOLERANCE, ARG_IMS_LIBRARY_RES)
        {            
            LeftColumnWidth = 34,
            Dependencies =
            {
                {ARG_FULL_SCAN_PRECURSOR_RES_MZ, ARG_FULL_SCAN_PRECURSOR_RES},
                {ARG_FULL_SCAN_PRODUCT_RES_MZ, ARG_FULL_SCAN_PRODUCT_RES},
            }
        };

        public static string[] GetDisplayNames<TItem>(SettingsListBase<TItem> list) where TItem : IKeyContainer<string>, IXmlSerializable
        {
            return list.Select(list.GetDisplayName).ToArray();
        }

        private static Adduct[] ParseIonCharges(NameValuePair p, int min, int max)
        {
            Assume.IsNotNull(p.Match); // Must be matched before accessing this
            var charges = ArrayUtil.Parse(p.Value, Adduct.FromStringAssumeProtonated, TextUtil.SEPARATOR_CSV, null);
            if (charges == null)
                throw new ValueInvalidChargeListException(p.Match, p.Value);

            foreach (var charge in charges)
            {
                if (min > charge.AdductCharge || charge.AdductCharge > max)
                    throw new ValueOutOfRangeIntException(p.Match, charge.AdductCharge, min, max);
            }
            return charges;
        }

        private static IonType[] ParseIonTypes(NameValuePair p)
        {
            Assume.IsNotNull(p.Match); // Must be matched before accessing this
            var types =  TransitionFilter.ParseTypes(p.Value, null);
            if (types == null)
                throw new ValueInvalidIonTypeListException(p.Match, p.Value);
            return types;
        }

        public Adduct[] FilterPrecursorCharges { get; private set; }
        public Adduct[] FilterProductCharges { get; private set; }
        public IonType[] FilterProductTypes { get; private set; }

        public bool FilterSettings
        {
            get
            {
                return (FilterPrecursorCharges != null ||
                        FilterProductCharges != null ||
                        FilterProductTypes != null);
            }
        }
        public string PredictCEName { get; private set; }
        public string PredictDPName { get; private set; }
        public string PredictCoVName { get; private set; }
        public string PredictOpimizationLibraryName { get; private set; }

        public bool PredictTranSettings
        {
            get
            {
                return (PredictCEName != null ||
                        PredictDPName != null ||
                        PredictCoVName != null ||
                        PredictOpimizationLibraryName != null);
            }
        }
        public double? FullScanPrecursorRes { get; private set; }
        public double? FullScanPrecursorResMz { get; private set; }
        public double? FullScanProductRes { get; private set; }
        public double? FullScanProductResMz { get; private set; }
        public double? FullScanRetentionTimeFilterLength { get; private set; }

        public bool FullScanSettings
        {
            get
            {
                return (FullScanPrecursorRes
                        ?? FullScanPrecursorResMz
                        ?? FullScanProductRes
                        ?? FullScanProductResMz
                        ?? FullScanRetentionTimeFilterLength).HasValue;
            }
        }

        public double? IonMobilityLibraryRes { get; private set; }

        public bool ImsSettings
        {
            get { return IonMobilityLibraryRes.HasValue; }
        }

        // For importing a tool from a zip file.
        public static readonly Argument ARG_TOOL_ADD = new ToolArgument(@"tool-add", NAME_VALUE,
            (c, p) => c.ToolName = p.Value);
        public static readonly Argument ARG_TOOL_COMMAND = new ToolArgument(@"tool-command", COMMAND_VALUE,
            (c, p) => c.ToolCommand = p.Value);
        public static readonly Argument ARG_TOOL_ARGUMENTS = new ToolArgument(@"tool-arguments", COMMAND_ARGUMENTS_VALUE,
            (c, p) => c.ToolArguments = p.Value);
        public static readonly Argument ARG_TOOL_INITIAL_DIR = new ToolArgument(@"tool-initial-dir", PATH_TO_FOLDER,
            (c, p) => c.ToolInitialDirectory = p.Value);
        public static readonly Argument ARG_TOOL_CONFLICT_RESOLUTION = new Argument(@"tool-conflict-resolution", 
            new[] {ARG_VALUE_OVERWRITE, ARG_VALUE_SKIP},
            (c, p) => c.ResolveToolConflictsBySkipping = p.IsValue(ARG_VALUE_SKIP)) {WrapValue = true};
        public static readonly Argument ARG_TOOL_REPORT = new ToolArgument(@"tool-report", REPORT_NAME_VALUE,
            (c, p) => c.ToolReportTitle = p.Value);
        public static readonly Argument ARG_TOOL_OUTPUT_TO_IMMEDIATE_WINDOW = new ToolArgument(@"tool-output-to-immediate-window",
            (c, p) => c.ToolOutputToImmediateWindow = true);
        public static readonly Argument ARG_TOOL_ADD_ZIP = new Argument(@"tool-add-zip", () => GetPathToFile(ToolDescription.EXT_INSTALL),
            (c, p) =>
            {
                c.InstallingToolsFromZip = true;
                c.ZippedToolsPath = p.Value;
            });
        public static readonly Argument ARG_TOOL_ZIP_CONFLICT_RESOLUTION = new Argument(@"tool-zip-conflict-resolution",
            new [] {ARG_VALUE_OVERWRITE, ARG_VALUE_PARALLEL}, (c, p) =>
            {
                c.ResolveZipToolConflictsBySkipping = p.IsValue(ARG_VALUE_OVERWRITE)
                    ? CommandLine.ResolveZipToolConflicts.overwrite
                    : CommandLine.ResolveZipToolConflicts.in_parallel;
            }) {WrapValue = true};
        public static readonly Argument ARG_TOOL_ZIP_OVERWRITE_ANNOTATIONS = new Argument(@"tool-zip-overwrite-annotations",
            new[] {ARG_VALUE_TRUE, ARG_VALUE_FALSE}, (c, p) => c.ResolveZipToolAnotationConflictsBySkipping = p.IsValue(ARG_VALUE_TRUE))
            { WrapValue = true};
        public const string ARG_VALUE_TRUE = "true";
        public const string ARG_VALUE_FALSE = "false";
        public static readonly Argument ARG_TOOL_PROGRAM_MACRO = new Argument(@"tool-program-macro",
            PROGRAM_MACRO_VALUE, (c, p) => c.ParseToolProgramMacro(p)) {WrapValue = true};
        public static readonly Argument ARG_TOOL_PROGRAM_PATH = new Argument(@"tool-program-path", PATH_TO_FILE,
            (c, p) => c.ZippedToolsProgramPathValue = p.Value);
        public static readonly Argument ARG_TOOL_IGNORE_REQUIRED_PACKAGES = new Argument(@"tool-ignore-required-packages",
            (c, p) => c.ZippedToolsPackagesHandled = true);
        public static readonly Argument ARG_TOOL_LIST_EXPORT = new Argument(@"tool-list-export", PATH_TO_FILE,
            (c, p) => ExportToolList(p)) {InternalUse = true};

        private void ParseToolProgramMacro(NameValuePair pair)
        {
            // example --tool-program-macro=R,2.15.2
            var spliced = pair.Value.Split(',');
            if (spliced.Length > 2)
            {
                WriteLine(Resources.CommandArgs_ParseArgsInternal_Warning__Incorrect_Usage_of_the___tool_program_macro_command_);
            }
            else
            {
                string programName = spliced[0];
                string programVersion = null;
                if (spliced.Length > 1)
                {
                    // Extract the version if specified.
                    programVersion = spliced[1];
                }

                ZippedToolsProgramPathContainer = new ProgramPathContainer(programName, programVersion);
            }
        }

        private static void ExportToolList(NameValuePair pair)
        {
            // A command that exports all the tools to a text file in a SkylineRunner form for --batch-commands
            // Not advertised.
            string pathToOutputFile = pair.ValueFullPath;
            using (StreamWriter sw = new StreamWriter(pathToOutputFile))
            {
                foreach (var tool in Settings.Default.ToolList)
                {
                    // ReSharper disable LocalizableElement
                    string command = "--tool-add=" + "\"" + tool.Title + "\"" +
                                     " --tool-command=" + "\"" + tool.Command + "\"" +
                                     " --tool-arguments=" + "\"" + tool.Arguments + "\"" +
                                     " --tool-initial-dir=" + "\"" + tool.InitialDirectory + "\"" +
                                     " --tool-conflict-resolution=skip" +
                                     " --tool-report=" + "\"" + tool.ReportTitle + "\"";

                    if (tool.OutputToImmediateWindow)
                        command += " --tool-output-to-immediate-window";

                    sw.WriteLine(command);
                    // ReSharper restore LocalizableElement
                }
            }
        }

        private static readonly ArgumentGroup GROUP_TOOLS = new ArgumentGroup(() => Resources.CommandArgs_GROUP_TOOLS_Tools_Installation, false,
            ARG_TOOL_ADD, ARG_TOOL_COMMAND, ARG_TOOL_ARGUMENTS, ARG_TOOL_INITIAL_DIR, ARG_TOOL_CONFLICT_RESOLUTION,
            ARG_TOOL_REPORT, ARG_TOOL_OUTPUT_TO_IMMEDIATE_WINDOW, ARG_TOOL_ADD_ZIP, ARG_TOOL_ZIP_CONFLICT_RESOLUTION,
            ARG_TOOL_ZIP_OVERWRITE_ANNOTATIONS, ARG_TOOL_PROGRAM_MACRO, ARG_TOOL_PROGRAM_PATH,
            ARG_TOOL_IGNORE_REQUIRED_PACKAGES, ARG_TOOL_LIST_EXPORT)
        {
            Preamble = () => Resources.CommandArgs_GROUP_TOOLS_The_arguments_below_can_be_used_to_install_tools_onto_the_Tools_menu_and_do_not_rely_on_the____in__argument_because_they_independent_of_a_specific_Skyline_document_,
        };

        public bool InstallingToolsFromZip { get; private set; }
        public string ZippedToolsPath { get; private set; }
        public CommandLine.ResolveZipToolConflicts? ResolveZipToolConflictsBySkipping { get; private set; }
        public bool? ResolveZipToolAnotationConflictsBySkipping { get; private set; }
        public ProgramPathContainer ZippedToolsProgramPathContainer { get; private set; }
        public string ZippedToolsProgramPathValue { get; private set; }
        public bool ZippedToolsPackagesHandled { get; set; }
        
        // For keeping track of when an in command is required.
        public bool RequiresSkylineDocument { get; private set; }

        // For --batch-commands parameter
        public string BatchCommandsPath { get; private set; }
        private bool _runningBatchCommands;
        public bool RunningBatchCommands
        {
            get { return !string.IsNullOrEmpty(BatchCommandsPath) || _runningBatchCommands; }
            set { _runningBatchCommands = value; }
        }

        // Export isolation / transition list
        public static readonly Argument ARG_EXP_ISOLATION_LIST_INSTRUMENT = new DocArgument(@"exp-isolationlist-instrument",
            ExportInstrumentType.ISOLATION_LIST_TYPES, (c, p) => c.ParseExpIsolationListInstrumentType(p)) {WrapValue = true};
        public static readonly Argument ARG_EXP_TRANSITION_LIST_INSTRUMENT = new DocArgument(@"exp-translist-instrument",
            ExportInstrumentType.TRANSITION_LIST_TYPES, (c, p) => c.ParseExpTransitionListInstrumentType(p)) {WrapValue = true};
        private static readonly ArgumentGroup GROUP_LISTS = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_LISTS_Exporting_isolation_transition_lists, false,
            ARG_EXP_ISOLATION_LIST_INSTRUMENT, ARG_EXP_TRANSITION_LIST_INSTRUMENT) {LeftColumnWidth = 34};

        private string _isolationListInstrumentType;
        public string IsolationListInstrumentType
        {
            get { return _isolationListInstrumentType; }
            set
            {
                if (ExportInstrumentType.ISOLATION_LIST_TYPES.Any(inst => inst.Equals(value)))
                {
                    _isolationListInstrumentType = value;
                }
                else
                {
                    throw new ArgumentException(string.Format(Resources.CommandArgs_IsolationListInstrumentType_The_instrument_type__0__is_not_valid_for_isolation_list_export, value));
                }
            }
        }

        public bool ExportingIsolationList
        {
            get { return !string.IsNullOrEmpty(IsolationListInstrumentType); }
        }

        private string _transListInstrumentType;
        public string TransListInstrumentType
        {
            get { return _transListInstrumentType; }
            set
            {
                if (ExportInstrumentType.TRANSITION_LIST_TYPES.Any(inst => inst.Equals(value)))
                {
                    _transListInstrumentType = value;
                }
                else
                {
                    throw new ArgumentException(string.Format(Resources.CommandArgs_TransListInstrumentType_The_instrument_type__0__is_not_valid_for_transition_list_export, value));
                }
            }
        }

        public bool ExportingTransitionList
        {
            get { return !string.IsNullOrEmpty(TransListInstrumentType);  }
        }

        private void ParseExpIsolationListInstrumentType(NameValuePair pair)
        {
            try
            {
                IsolationListInstrumentType = pair.Value;
            }
            catch (ArgumentException)
            {
                WriteInstrumentValueError(pair, ExportInstrumentType.ISOLATION_LIST_TYPES);
                WriteLine(Resources.CommandArgs_ParseArgsInternal_No_isolation_list_will_be_exported_);
            }
        }

        private void ParseExpTransitionListInstrumentType(NameValuePair pair)
        {
            try
            {
                TransListInstrumentType = pair.Value;
            }
            catch (ArgumentException)
            {
                WriteInstrumentValueError(pair, ExportInstrumentType.TRANSITION_LIST_TYPES);
                WriteLine(Resources.CommandArgs_ParseArgsInternal_No_transition_list_will_be_exported_);
            }
        }

        private void WriteInstrumentValueError(NameValuePair pair, string[] listInstrumentTypes)
        {
            WriteLine(Resources.CommandArgs_ParseArgsInternal_Warning__The_instrument_type__0__is_not_valid__Please_choose_from_,
                pair.Value);
            foreach (string str in listInstrumentTypes)
                WriteLine(str);
        }

        // Export method
        public static readonly Argument ARG_EXP_METHOD_INSTRUMENT = new DocArgument(@"exp-method-instrument",
            ExportInstrumentType.METHOD_TYPES, (c, p) => c.ParseExpMethodInstrumentType(p)) { WrapValue = true };
        public static readonly Argument ARG_EXP_TEMPLATE = new DocArgument(@"exp-template", PATH_TO_FILE,
            (c, p) => c.TemplateFile = p.ValueFullPath);
        private static readonly ArgumentGroup GROUP_METHOD = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_METHOD_Exporting_native_instrument_methods, false,
            ARG_EXP_METHOD_INSTRUMENT, ARG_EXP_TEMPLATE) {LeftColumnWidth = 34};

        private string _methodInstrumentType;
        public string MethodInstrumentType
        {
            get { return _methodInstrumentType; }
            set
            {
                if (ExportInstrumentType.METHOD_TYPES.Any(inst => inst.Equals(value)))
                {
                    _methodInstrumentType = value;
                }
                else
                {
                    throw new ArgumentException(string.Format(Resources.CommandArgs_MethodInstrumentType_The_instrument_type__0__is_not_valid_for_method_export, value));
                }
            }
        }

        public bool ExportingMethod
        {
            get { return !string.IsNullOrEmpty(MethodInstrumentType); }
        }

        public string TemplateFile { get; private set; }

        private void ParseExpMethodInstrumentType(NameValuePair pair)
        {
            try
            {
                MethodInstrumentType = pair.Value;
            }
            catch (ArgumentException)
            {
                WriteInstrumentValueError(pair, ExportInstrumentType.METHOD_TYPES);
                WriteLine(Resources.CommandArgs_ParseArgsInternal_No_method_will_be_exported_);
            }
        }

        // Export list/method arguments
        public static readonly Argument ARG_EXP_FILE = new DocArgument(@"exp-file", PATH_TO_FILE,
            (c, p) => c.ExportPath = p.ValueFullPath);
        public static readonly Argument ARG_EXP_STRATEGY = new DocArgument(@"exp-strategy",
            Helpers.GetEnumValues<ExportStrategy>().Select(p => p.ToString()).ToArray(),
            (c, p) =>
            {
                c.ExportStrategySet = true;
                c.ExportStrategy = (ExportStrategy)Enum.Parse(typeof(ExportStrategy), p.Value, true);
            })
            { WrapValue = true};
        public static readonly Argument ARG_EXP_METHOD_TYPE = new DocArgument(@"exp-method-type",
            Helpers.GetEnumValues<ExportMethodType>().Select(p => p.ToString()).ToArray(),
            (c, p) => c.ExportMethodType = (ExportMethodType)Enum.Parse(typeof(ExportMethodType), p.Value, true))
            { WrapValue = true};
        public static readonly Argument ARG_EXP_MAX_TRANS = new DocArgument(@"exp-max-trans", NAME_VALUE,
            (c, p) => c.MaxTransitionsPerInjection = p.ValueInt);
        public static readonly Argument ARG_EXP_OPTIMIZING = new DocArgument(@"exp-optimizing",  new[] { OPT_CE, OPT_DP},
            (c, p) => c.ExportOptimizeType = p.IsValue(OPT_CE) ? OPT_CE : OPT_DP);
        public static readonly Argument ARG_EXP_SCHEDULING_REPLICATE = new DocArgument(@"exp-scheduling-replicate", NAME_VALUE,
            (c, p) => c.SchedulingReplicate = p.Value);
        public static readonly Argument ARG_EXP_ORDER_BY_MZ = new DocArgument(@"exp-order-by-mz",
            (c, p) => c.SortByMz = true);
        public static readonly Argument ARG_EXP_IGNORE_PROTEINS = new DocArgument(@"exp-ignore-proteins",
            (c, p) => c.IgnoreProteins = true);
        public static readonly Argument ARG_EXP_PRIMARY_COUNT = new DocArgument(@"exp-primary-count", INT_VALUE,
            (c, p) => c.PrimaryTransitionCount = p.GetValueInt(AbstractMassListExporter.PRIMARY_COUNT_MIN, AbstractMassListExporter.PRIMARY_COUNT_MAX));
        public static readonly Argument ARG_EXP_POLARITY = new Argument(@"exp-polarity", 
            Helpers.GetEnumValues<ExportPolarity>().Select(p => p.ToString()).ToArray(),
            (c, p) => c.ExportPolarityFilter = (ExportPolarity)Enum.Parse(typeof(ExportPolarity), p.Value, true))
            { WrapValue = true};

        private static readonly ArgumentGroup GROUP_EXP_GENERAL = new ArgumentGroup(
            () => CommandArgUsage.CommandArgs_GROUP_EXP_GENERAL_Method_and_transition_list_options, false,
            ARG_EXP_FILE, ARG_EXP_STRATEGY, ARG_EXP_METHOD_TYPE, ARG_EXP_MAX_TRANS,
            ARG_EXP_OPTIMIZING, ARG_EXP_SCHEDULING_REPLICATE, ARG_EXP_ORDER_BY_MZ, ARG_EXP_IGNORE_PROTEINS,
            ARG_EXP_PRIMARY_COUNT, ARG_EXP_POLARITY); // {LeftColumnWidth = 34};

        // Instrument specific arguments
        public static readonly Argument ARG_EXP_DWELL_TIME = new DocArgument(@"exp-dwell-time", MILLIS_VALE,
            (c, p) => c.DwellTime = p.GetValueInt(AbstractMassListExporter.DWELL_TIME_MIN, AbstractMassListExporter.DWELL_TIME_MAX))
            { AppliesTo = CommandArgUsage.CommandArgs_ARG_EXP_DWELL_TIME_AppliesTo};
        public static readonly Argument ARG_EXP_ADD_ENERGY_RAMP = new DocArgument(@"exp-add-energy-ramp",
            (c, p) => c.AddEnergyRamp = true)
            { AppliesTo = CommandArgUsage.CommandArgs_ARG_EXP_Thermo};
        public static readonly Argument ARG_EXP_USE_S_LENS = new DocArgument(@"exp-use-s-lens",
            (c, p) => c.UseSlens = true)
            { AppliesTo = CommandArgUsage.CommandArgs_ARG_EXP_Thermo};
        public static readonly Argument ARG_EXP_RUN_LENGTH = new DocArgument(@"exp-run-length", MINUTES_VALUE,
            (c, p) => c.RunLength = p.GetValueDouble(AbstractMassListExporter.RUN_LENGTH_MIN, AbstractMassListExporter.RUN_LENGTH_MAX))
            { AppliesTo = CommandArgUsage.CommandArgs_ARG_EXP_RUN_LENGTH_AppliesTo};

        private static readonly ArgumentGroup GROUP_EXP_INSTRUMENT = new ArgumentGroup(() => CommandArgUsage.CommandArgs_GROUP_EXP_INSTRUMENT_Vendor_specific_method_and_transition_list_options, false,
                ARG_EXP_DWELL_TIME, ARG_EXP_ADD_ENERGY_RAMP, ARG_EXP_USE_S_LENS, ARG_EXP_RUN_LENGTH);

        public string ExportPath { get; private set; }
        public bool ExportStrategySet { get; private set; }
        public ExportStrategy ExportStrategy { get; set; }

        // The min value for this field comes from either MassListExporter.MAX_TRANS_PER_INJ_MIN
        // or MethodExporter.MAX_TRANS_PER_INJ_MIN_TLTQ depending on the instrument. The max value
        // comes from the document. Point being, there is no way to check the value in the accessor.
        public int MaxTransitionsPerInjection { get; set; }

        private string _importOptimizeType;
        public string ImportOptimizeType
        {
            get { return _importOptimizeType; }
            set { _importOptimizeType = ToOptimizeString(value); }
        }

        private string _exportOptimizeType;
        public string ExportOptimizeType
        {
            get { return _exportOptimizeType; }
            set { _exportOptimizeType = ToOptimizeString(value); }
        }

        public const string OPT_NONE = "NONE";
        public const string OPT_CE = "CE";
        public const string OPT_DP = "DP";

        private static string ToOptimizeString(string value)
        {
            if (value == null)
                return null;

            switch (value.ToUpperInvariant())
            {
                case OPT_NONE:
                    return null;
                case OPT_CE:
                    return ExportOptimize.CE;
                case OPT_DP:
                    return ExportOptimize.DP;
                default:
                    throw new ArgumentException(string.Format(Resources.CommandArgs_ToOptimizeString_The_instrument_parameter__0__is_not_valid_for_optimization_, value));
            }
        }

        public ExportMethodType ExportMethodType { get; private set; }

        public ExportSchedulingAlgorithm ExportSchedulingAlgorithm
        {
            get
            {
                return String.IsNullOrEmpty(SchedulingReplicate)
                    ? ExportSchedulingAlgorithm.Average
                    : ExportSchedulingAlgorithm.Single;
            }
        }
        
        public string SchedulingReplicate { get; private set; }

        public bool SortByMz { get; private set; }

        public bool IgnoreProteins { get; private set; }

        private int _primaryTransitionCount;
        public int PrimaryTransitionCount
        {
            get { return _primaryTransitionCount; }
            set
            {
                if (value < AbstractMassListExporter.PRIMARY_COUNT_MIN || value > AbstractMassListExporter.PRIMARY_COUNT_MAX)
                {
                    throw new ArgumentException(string.Format(Resources.CommandArgs_PrimaryTransitionCount_The_primary_transition_count__0__must_be_between__1__and__2__, value, AbstractMassListExporter.PRIMARY_COUNT_MIN, AbstractMassListExporter.PRIMARY_COUNT_MAX));
                }
                _primaryTransitionCount = value;
            }
        }

        private int _dwellTime;
        public int DwellTime
        {
            get { return _dwellTime; }
            set
            {
                if (value < AbstractMassListExporter.DWELL_TIME_MIN || value > AbstractMassListExporter.DWELL_TIME_MAX)
                {
                    throw new ArgumentException(string.Format(Resources.CommandArgs_DwellTime_The_dwell_time__0__must_be_between__1__and__2__, value, AbstractMassListExporter.DWELL_TIME_MIN, AbstractMassListExporter.DWELL_TIME_MAX));
                }
                _dwellTime = value;
            }
        }

        public bool AddEnergyRamp { get; private set; }
        public bool UseSlens { get; private set; }
        private double _runLength;
        public double RunLength
        {
            get { return _runLength; }
            set
            {
                // Not inclusive of minimum, because it was made zero
                if (value <= AbstractMassListExporter.RUN_LENGTH_MIN || value > AbstractMassListExporter.RUN_LENGTH_MAX)
                {
                    throw new ArgumentException(string.Format(Resources.CommandArgs_RunLength_The_run_length__0__must_be_between__1__and__2__, value, AbstractMassListExporter.RUN_LENGTH_MIN, AbstractMassListExporter.RUN_LENGTH_MAX));
                }
                _runLength = value;
            }
        }
        
        public ExportPolarity ExportPolarityFilter { get; private set; }

        public ExportCommandProperties ExportCommandProperties
        {
            get
            {
                return new ExportCommandProperties(_out)
                {

                    AddEnergyRamp = AddEnergyRamp,
                    UseSlens = UseSlens,
                    DwellTime = DwellTime,
                    ExportStrategy = ExportStrategy,
                    SortByMz = SortByMz,
                    IgnoreProteins = IgnoreProteins,
                    MaxTransitions = MaxTransitionsPerInjection,
                    MethodType = ExportMethodType,
                    OptimizeType = ExportOptimizeType,
                    PolarityFilter = ExportPolarityFilter,
                    RunLength = RunLength,
                    SchedulingAlgorithm = ExportSchedulingAlgorithm
                };
            }
        }

        private class ParaUsageBlock : IUsageBlock
        {
            public ParaUsageBlock(string text)
            {
                Text = text;
            }

            public string Text { get; private set; }

            public string ToString(int width, string formatType)
            {
                return ConsoleTable.ParaToString(width, Text, true);
            }

            public string ToHtmlString()
            {
                return @"<p>" + Text + @"</p>";
            }
        }

        public static IEnumerable<IUsageBlock> UsageBlocks
        {
            get
            {
                return new IUsageBlock[]
                {
                    new ParaUsageBlock(CommandArgUsage.CommandArgs_Usage_To_access_the_command_line_interface_for_Skyline_you_can_use_either_SkylineRunner_exe_or_SkylineCmd_exe_),
                    new ParaUsageBlock(CommandArgUsage.CommandArgs_Usage_para2),
                    new ParaUsageBlock(CommandArgUsage.CommandArgs_Usage_para3),
                    new ParaUsageBlock(CommandArgUsage.CommandArgs_Usage_para4),
                    GROUP_GENERAL_IO,
                    GROUP_INTERNAL, // No output
                    new ParaUsageBlock(CommandArgUsage.CommandArgs_Usage_Until_the_section_titled_Settings_Customization_all_other_command_line_arguments_rely_on_the____in__argument_because_they_all_rely_on_having_a_Skyline_document_open_),
                    GROUP_IMPORT,
                    GROUP_REINTEGRATE,
                    GROUP_REMOVE,
                    GROUP_MINIMIZE_RESULTS,
                    GROUP_IMPORT_DOC,
                    GROUP_ANNOTATIONS,
                    GROUP_FASTA,
                    GROUP_IMPORT_SEARCH,
                    GROUP_IMPORT_LIST,
                    GROUP_ADD_LIBRARY,
                    GROUP_DECOYS,
                    GROUP_REFINEMENT,
                    GROUP_REFINEMENT_W_RESULTS,
                    GROUP_REPORT,
                    GROUP_CHROMATOGRAM,
                    GROUP_LISTS,
                    GROUP_METHOD,
                    GROUP_EXP_GENERAL,
                    GROUP_EXP_INSTRUMENT,
                    GROUP_PANORAMA,
                    GROUP_SETTINGS,
                    GROUP_TOOLS
                };
            }
        }

        public static IEnumerable<Argument> UsageArguments
        {
            get
            {
                return AllArguments.Where(a => !a.InternalUse);
            }
        }

        public static IEnumerable<Argument> AllArguments
        {
            get
            {
                return UsageBlocks.Where(b => b is ArgumentGroup).Cast<ArgumentGroup>()
                    .SelectMany(g => g.Args);
            }
        }

        public static string GenerateUsageHtml()
        {
            var sb = new StringBuilder(@"<html><head>");
            sb.AppendLine(DocumentationGenerator.GetStyleSheetHtml());
            sb.AppendLine(@"</head><body>");
            foreach (var block in UsageBlocks)
                sb.Append(block.ToHtmlString());
            sb.Append(@"</body></html>");
            return sb.ToString();
        }

        public bool UsageShown { get; private set; }

        public bool Usage(string formatType = null)
        {
            if (!UsageShown)    // Avoid showing again
            {
                if (formatType == ARG_VALUE_ASCII)
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;  // Use invariant culture for ascii output
                UsageShown = true;
                foreach (var block in UsageBlocks)
                    _out.Write(block.ToString(_usageWidth, formatType));
            }
            return false;   // End argument processing
        }

        private int _usageWidth = 78;

        private readonly CommandStatusWriter _out;
        private readonly bool _isDocumentLoaded;
        private readonly IList<Argument> _seenArguments = new List<Argument>();

        public CommandArgs(CommandStatusWriter output, bool isDocumentLoaded)
        {
            ResolveToolConflictsBySkipping = null;
            ResolveSkyrConflictsBySkipping = null;
            _out = output;
            _isDocumentLoaded = isDocumentLoaded;

            ReportColumnSeparator = TextUtil.CsvSeparator;
            MaxTransitionsPerInjection = AbstractMassListExporter.MAX_TRANS_PER_INJ_DEFAULT;
            ImportOptimizeType = OPT_NONE;
            ExportOptimizeType = OPT_NONE;
            ExportStrategy = ExportStrategy.Single;
            ExportMethodType = ExportMethodType.Standard;
            PrimaryTransitionCount = AbstractMassListExporter.PRIMARY_COUNT_DEFAULT;
            DwellTime = AbstractMassListExporter.DWELL_TIME_DEFAULT;
            RunLength = AbstractMassListExporter.RUN_LENGTH_DEFAULT;

            DocImportPaths = new List<string>();
            ReplicateFile = new List<MsDataFileUri>();
            SearchResultsFiles = new List<string>();
            ExcludeFeatures = new List<IPeakFeatureCalculator>();
            SharedFileType = ShareType.DEFAULT;

            ImportBeforeDate = null;
            ImportOnOrAfterDate = null;
        }

        private void WriteLine(string value, params object[] obs)
        {
            _out.WriteLine(value, obs);
        }

        public bool ParseArgs(string[] args)
        {
            try
            {
                return ParseArgsInternal(args);
            }
            catch (UsageException x)
            {
                WriteLine(Resources.CommandLine_GeneralException_Error___0_, x.Message);
                return false;
            }
            catch (Exception x)
            {
                // Unexpected behavior, but better to output the error than appear to crash, and
                // have Windows write it to the application event log.
                WriteLine(Resources.CommandLine_GeneralException_Error___0_, x.Message);
                WriteLine(x.StackTrace);
                return false;
            }
        }

        private bool ParseArgsInternal(IEnumerable<string> args)
        {
            _seenArguments.Clear();

            foreach (string s in args)
            {
                var pair = Argument.Parse(s);
                if (!ProcessArgument(pair))
                    return false;
            }

            return ValidateArgs();
        }

        private bool ProcessArgument(NameValuePair pair)
        {
            // Only name-value pairs get processed here
            if (pair == null || pair.IsEmpty)
                return true;

            foreach (var definedArgument in AllArguments)
            {
                if (pair.IsMatch(definedArgument))
                {
                    Assume.IsNotNull(definedArgument.ProcessValue); // Must define some way to process the value

                    _seenArguments.Add(definedArgument);
                    return definedArgument.ProcessValue(this, pair);
                }
            }
            // Unmatched argument
            WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Unexpected_argument____0_, pair.Name);
            return false;
        }

        private bool ValidateArgs()
        {
            // Check argument dependencies
            var allDependencies = new Dictionary<Argument, Argument>();
            foreach (ArgumentGroup group in UsageBlocks.Where(b => b is ArgumentGroup))
            {
                if (group.Validate != null && !group.Validate(this))
                    return false;

                foreach (var pair in group.Dependencies)
                    allDependencies.Add(pair.Key, pair.Value);
            }
            var seenSet = new HashSet<Argument>(_seenArguments);
            var warningSet = new HashSet<Argument>();    // Warn only once
            foreach (var seenArgument in _seenArguments)
            {
                Argument dependency;
                if (allDependencies.TryGetValue(seenArgument, out dependency) &&
                    !seenSet.Contains(dependency) && !warningSet.Contains(seenArgument))
                {
                    WarnArgRequirement(seenArgument, dependency);
                    warningSet.Add(seenArgument);
                }
            }

            return true;
        }

        public static string WarnArgRequirementText(Argument usedArg, params Argument[] requiredArgs)
        {
            if (requiredArgs.Length == 1)
                return string.Format(Resources.CommandArgs_WarnArgRequirment_Warning__Use_of_the_argument__0__requires_the_argument__1_,
                    usedArg.ArgumentText, requiredArgs[0].ArgumentText);

            var requiredArgsText = new List<string>()
            {
                string.Format(
                    Resources
                        .CommandArgs_WarnArgRequirementText_Use_of_the_argument__0__requires_one_of_the_following_arguments_,
                    usedArg.ArgumentText)
            };
            requiredArgsText.AddRange(requiredArgs.Select(i => i.ArgumentText).ToList());
            return TextUtil.LineSeparate(requiredArgsText);
        }

        private void WarnArgRequirement(Argument usedArg, params Argument[] requiredArgs)
        {
            WriteLine(WarnArgRequirementText(usedArg, requiredArgs));
        }

        public static string ErrorArgsExclusiveText(Argument arg1, Argument arg2)
        {
            return string.Format(Resources.CommandArgs_ErrorArgsExclusiveText_Error__The_arguments__0__and__1__options_cannot_be_used_together_,
                arg1.ArgumentText, arg2.ArgumentText);
        }

        private void ErrorArgsExclusive(Argument arg1, Argument arg2)
        {
            WriteLine(ErrorArgsExclusiveText(arg1, arg2));
        }

        public class Argument
        {
            private const string ARG_PREFIX = "--";

            public Argument(string name, Func<CommandArgs, NameValuePair, bool> processValue)
            {
                Name = name;
                ProcessValue = processValue;
            }

            public Argument(string name, Action<CommandArgs, NameValuePair> processValue)
                : this(name, (c, p) =>
                {
                    processValue(c, p);
                    return true;
                })
            {
            }

            public Argument(string name, Func<string> valueExample, Func<CommandArgs, NameValuePair, bool> processValue)
                : this(name, processValue)
            {
                ValueExample = valueExample;
            }

            public Argument(string name, Func<string> valueExample, Action<CommandArgs, NameValuePair> processValue)
                : this(name, valueExample, (c, p) =>
                {
                    processValue(c, p);
                    return true;
                })
            {
            }

            public Argument(string name, string[] values, Func<CommandArgs, NameValuePair, bool> processValue)
                : this(name, () => ValuesToExample(values), processValue)
            {
                _fixedValues = values;
            }

            public Argument(string name, string[] values, Action<CommandArgs, NameValuePair> processValue)
                : this(name, values, (c, p) =>
                {
                    processValue(c, p);
                    return true;
                })
            {
            }

            public Argument(string name, Func<string[]> values, Func<CommandArgs, NameValuePair, bool> processValue)
                : this(name, () => ValuesToExample(values()), processValue)
            {
                _dynamicValues = values;
            }

            public Argument(string name, Func<string[]> values, Action<CommandArgs, NameValuePair> processValue)
                : this(name, values, (c, p) =>
                {
                    processValue(c, p);
                    return true;
                })
            {
            }

            private string[] _fixedValues;
            private Func<string[]> _dynamicValues;

            public Func<CommandArgs, NameValuePair, bool> ProcessValue;

            public string Name { get; private set; }
            public string AppliesTo { get; set; }
            public string Description
            {
                get { return CommandArgUsage.ResourceManager.GetString("_" + Name.Replace('-', '_')); }
            }
            public Func<string> ValueExample { get; private set; }
            public string[] Values
            {
                get
                {
                    return _dynamicValues?.Invoke() ?? _fixedValues;
                }
            }
            public bool WrapValue { get; set; }
            public bool OptionalValue { get; set; }
            public bool InternalUse { get; set; }

            public string ArgumentText
            {
                get { return ARG_PREFIX + Name; }
            }

            public string GetArgumentTextWithValue(string value)
            {
                if (ValueExample == null)
                    throw new ArgumentException(@"The argument {0} is valueless.");
                else if (Values != null && !Values.Any(v => v.Equals(value, StringComparison.CurrentCultureIgnoreCase)))
                    throw new ValueInvalidException(this, value, Values);

                return ArgumentText + '=' + value;
            }

            public string ArgumentDescription
            {
                get
                {
                    var retValue = ArgumentText;
                    if (ValueExample != null)
                    {
                        var valueText = '=' + (WrapValue ? Environment.NewLine : string.Empty) + ValueExample();
                        if (OptionalValue)
                            valueText = '[' + valueText + ']';
                        retValue += valueText;
                    }
                    return retValue;
                }
            }

            public override string ToString()
            {
                return ArgumentDescription;
            }

            public static string ValuesToExample(params string[] options)
            {
                var sb = new StringBuilder();
                sb.Append('<');
                foreach (var o in options)
                {
                    if (sb.Length > 1)
                        sb.Append(@" | ");
                    sb.Append(o);
                }
                sb.Append('>');
                return sb.ToString();
            }

            public static NameValuePair Parse(string arg)
            {
                if (!arg.StartsWith(ARG_PREFIX))
                    return NameValuePair.EMPTY;

                string name, value = null;
                arg = arg.Substring(2);
                int indexEqualsSign = arg.IndexOf('=');
                if (indexEqualsSign >= 0)
                {
                    name = arg.Substring(0, indexEqualsSign);
                    value = arg.Substring(indexEqualsSign + 1);
                }
                else
                {
                    name = arg;
                }
                return new NameValuePair(name, value);
            }
        }

        public class DocArgument : Argument
        {
            public DocArgument(string name, Func<CommandArgs, NameValuePair, bool> processValue)
                : base(name, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public DocArgument(string name, Action<CommandArgs, NameValuePair> processValue)
                : base(name, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public DocArgument(string name, Func<string> valueExample, Func<CommandArgs, NameValuePair, bool> processValue)
                : base(name, valueExample, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public DocArgument(string name, Func<string> valueExample, Action<CommandArgs, NameValuePair> processValue)
                : base(name, valueExample, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public DocArgument(string name, string[] values, Func<CommandArgs, NameValuePair, bool> processValue)
                : base(name, values, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public DocArgument(string name, string[] values, Action<CommandArgs, NameValuePair> processValue)
                : base(name, values, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public DocArgument(string name, Func<string[]> values, Func<CommandArgs, NameValuePair, bool> processValue)
                : base(name, values, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public DocArgument(string name, Func<string[]> values, Action<CommandArgs, NameValuePair> processValue)
                : base(name, values, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            private static bool ProcessValueOverride(CommandArgs c, NameValuePair p, Func<CommandArgs, NameValuePair, bool> processValue)
            {
                c.RequiresSkylineDocument = true;
                return processValue(c, p);
            }
            private static void ProcessValueOverride(CommandArgs c, NameValuePair p, Action<CommandArgs, NameValuePair> processValue)
            {
                c.RequiresSkylineDocument = true;
                processValue(c, p);
            }
        }

        public class RefineArgument : DocArgument
        {
            public RefineArgument(string name, Func<CommandArgs, NameValuePair, bool> processValue)
                : base(name, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public RefineArgument(string name, Action<CommandArgs, NameValuePair> processValue)
                : base(name, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public RefineArgument(string name, Func<string> valueExample, Func<CommandArgs, NameValuePair, bool> processValue)
                : base(name, valueExample, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public RefineArgument(string name, Func<string> valueExample, Action<CommandArgs, NameValuePair> processValue)
                : base(name, valueExample, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public RefineArgument(string name, string[] values, Func<CommandArgs, NameValuePair, bool> processValue)
                : base(name, values, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public RefineArgument(string name, string[] values, Action<CommandArgs, NameValuePair> processValue)
                : base(name, values, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            private static bool ProcessValueOverride(CommandArgs c, NameValuePair p, Func<CommandArgs, NameValuePair, bool> processValue)
            {
                if (c.Refinement == null)
                    c.Refinement = new RefinementSettings();
                return processValue(c, p);
            }
            private static void ProcessValueOverride(CommandArgs c, NameValuePair p, Action<CommandArgs, NameValuePair> processValue)
            {
                if (c.Refinement == null)
                    c.Refinement = new RefinementSettings();
                processValue(c, p);
            }
        }

        public class ToolArgument : Argument
        {
            public ToolArgument(string name, Func<CommandArgs, NameValuePair, bool> processValue)
                : base(name, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public ToolArgument(string name, Action<CommandArgs, NameValuePair> processValue)
                : base(name, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public ToolArgument(string name, Func<string> valueExample, Func<CommandArgs, NameValuePair, bool> processValue)
                : base(name, valueExample, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public ToolArgument(string name, Func<string> valueExample, Action<CommandArgs, NameValuePair> processValue)
                : base(name, valueExample, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public ToolArgument(string name, string[] values, Func<CommandArgs, NameValuePair, bool> processValue)
                : base(name, values, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            public ToolArgument(string name, string[] values, Action<CommandArgs, NameValuePair> processValue)
                : base(name, values, (c, p) => ProcessValueOverride(c, p, processValue))
            {
            }

            private static bool ProcessValueOverride(CommandArgs c, NameValuePair p, Func<CommandArgs, NameValuePair, bool> processValue)
            {
                c.ImportingTool = true;
                return processValue(c, p);
            }
            private static void ProcessValueOverride(CommandArgs c, NameValuePair p, Action<CommandArgs, NameValuePair> processValue)
            {
                c.ImportingTool = true;
                processValue(c, p);
            }
        }

        public class NameValuePair
        {
            public static NameValuePair EMPTY = new NameValuePair(null, null);

            public NameValuePair(string name, string value)
            {
                Name = name;
                Value = value;
            }

            public string Name { get; private set; }
            public string Value { get; private set; }

            public Argument Match { get; private set; }

            public int ValueInt
            {
                get
                {
                    Assume.IsNotNull(Match); // Must be matched before accessing this
                    try
                    {
                        return int.Parse(Value);
                    }
                    catch (FormatException)
                    {
                        throw new ValueInvalidIntException(Match, Value);
                    }
                }
            }

            public int GetValueInt(int minVal, int maxVal)
            {
                int v = ValueInt;
                if (minVal > v || v > maxVal)
                    throw new ValueOutOfRangeIntException(Match, v, minVal, maxVal);
                return v;
            }

            public double ValueDouble
            {
                get
                {
                    Assume.IsNotNull(Match); // Must be matched before accessing this
                    double valueDouble;
                    // Try both local and invariant formats to make batch files more portable
                    if (!double.TryParse(Value, out valueDouble) && !double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out valueDouble))
                        throw new ValueInvalidDoubleException(Match, Value);
                    return valueDouble;
                }
            }

            public double GetValueDouble(double minVal, double maxVal)
            {
                double v = ValueDouble;
                if (minVal > v || v > maxVal)
                    throw new ValueOutOfRangeDoubleException(Match, v, minVal, maxVal);
                return v;
            }

            public DateTime ValueDate
            {
                get
                {
                    Assume.IsNotNull(Match); // Must be matched before accessing this
                    try
                    {
                        // Try local format
                        return Convert.ToDateTime(Value);
                    }
                    catch (Exception)
                    {
                        try
                        {
                            // Try invariant format to make command-line batch files more portable
                            return Convert.ToDateTime(Value, CultureInfo.InvariantCulture);
                        }
                        catch (Exception)
                        {
                            throw new ValueInvalidDateException(Match, Value);
                        }
                    }
                }
            }

            public string ValueFullPath
            {
                get
                {
                    try
                    {
                        return Path.GetFullPath(Value);
                    }
                    catch (Exception)
                    {
                        throw new ValueInvalidPathException(Match, Value);
                    }
                }
            }

            public bool IsEmpty { get { return string.IsNullOrEmpty(Name); } }
            public bool IsNameOnly { get { return string.IsNullOrEmpty(Value); } }

            public bool IsMatch(Argument arg)
            {
                if (!Name.Equals(arg.Name))
                    return false;
                if (arg.ValueExample == null && !IsNameOnly)
                    throw new ValueUnexpectedException(arg);
                if (arg.ValueExample != null)
                {
                    if (IsNameOnly)
                    {
                        if (!arg.OptionalValue)
                            throw new ValueMissingException(arg);
                    }
                    else
                    {
                        var val = Value;
                        if (arg.Values != null && !arg.Values.Any(v => v.Equals(val, StringComparison.CurrentCultureIgnoreCase)))
                            throw new ValueInvalidException(arg, Value, arg.Values);
                    }
                }

                Match = arg;
                return true;
            }

            public bool IsValue(string value)
            {
                return value.Equals(Value, StringComparison.CurrentCultureIgnoreCase);
            }
        }

        public class ArgumentGroup : IUsageBlock
        {
            private readonly Func<string> _getTitle;

            public ArgumentGroup(Func<string> getTitle, bool showHeaders, params Argument[] args)
            {
                _getTitle = getTitle;
                Args = args;
                ShowHeaders = showHeaders;
                Dependencies = new Dictionary<Argument, Argument>();
            }

            public string Title { get { return _getTitle(); } }
            public Func<string> Preamble { get; set; }
            public Func<string> Postamble { get; set; }
            public IList<Argument> Args { get; private set; }
            public bool ShowHeaders { get; private set; }

            public IDictionary<Argument, Argument> Dependencies { get; set; }

            public Func<CommandArgs, bool> Validate { get; set; }

            public int? LeftColumnWidth { get; set; }

            public bool IncludeInUsage
            {
                get { return !Args.All(a => a.InternalUse); }
            }

            public override string ToString()
            {
                return ToString(78, null, true);
            }

            public string ToString(int width, string formatType)
            {
                return ToString(width, formatType, false);
            }

            private string ToString(int width, string formatType, bool forDebugging)
            {
                if (!IncludeInUsage && !forDebugging)
                    return string.Empty;

                var ct = new ConsoleTable
                {
                    Title = Title,
                    Borders = formatType != ARG_VALUE_NO_BORDERS,
                    Ascii = formatType == ARG_VALUE_ASCII
                };
                if (Preamble != null)
                    ct.Preamble = Preamble();
                if (Postamble != null)
                    ct.Postamble = Postamble();
                if (LeftColumnWidth.HasValue)
                    ct.Widths = new[] { LeftColumnWidth.Value, width - LeftColumnWidth.Value - 3 };   // 3 borders
                else
                    ct.Width = width;

                bool hasAppliesTo = Args.Any(a => a.AppliesTo != null);
                if (ShowHeaders)
                {
                    if (hasAppliesTo)
                        ct.SetHeaders(CommandArgUsage.CommandArgGroup_ToString_Applies_To,
                            CommandArgUsage.CommandArgGroup_ToString_Argument,
                            CommandArgUsage.CommandArgGroup_ToString_Description);
                    else
                        ct.SetHeaders(CommandArgUsage.CommandArgGroup_ToString_Argument,
                            CommandArgUsage.CommandArgGroup_ToString_Description);
                }
                foreach (var commandArg in Args.Where(a => !a.InternalUse))
                {
                    if (hasAppliesTo)
                        ct.AddRow(commandArg.AppliesTo ?? string.Empty, commandArg.ArgumentDescription, commandArg.Description);
                    else
                        ct.AddRow(commandArg.ArgumentDescription, commandArg.Description);
                }

                return ct.ToString();
            }

            public string ToHtmlString()
            {
                if (!IncludeInUsage)
                    return string.Empty;

                // ReSharper disable LocalizableElement
                var sb = new StringBuilder();
                sb.AppendLine("<div class=\"RowType\">" + HtmlEncode(Title) + "</div>");
                if (Preamble != null)
                    sb.AppendLine("<p>" + Preamble() + "</p>");
                sb.AppendLine("<table>");
                bool hasAppliesTo = Args.Any(a => a.AppliesTo != null);
                if (ShowHeaders)
                {
                    sb.Append("<tr>");

                    if (hasAppliesTo)
                        sb.Append("<th>").Append(CommandArgUsage.CommandArgGroup_ToString_Applies_To).Append("</th>");
                    sb.Append("<th>").Append(CommandArgUsage.CommandArgGroup_ToString_Argument).Append("</th>");
                    sb.Append("<th>").Append(CommandArgUsage.CommandArgGroup_ToString_Description).Append("</th>");

                    sb.AppendLine("</tr>");
                }
                foreach (var commandArg in Args.Where(a => !a.InternalUse))
                {
                    sb.Append("<tr>");

                    if (hasAppliesTo)
                        sb.Append("<td>").Append(commandArg.AppliesTo != null ? HtmlEncode(commandArg.AppliesTo) : "&nbsp;").Append("</td>");
                    string argDescription = HtmlEncode(commandArg.ArgumentDescription);
                    if (!argDescription.Contains('|'))
                        argDescription = argDescription.Replace(" ", "&nbsp;");
                    argDescription = argDescription.Replace(Environment.NewLine, "<br/>");
                    sb.Append("<td>").Append(argDescription).Append("</td>");
                    sb.Append("<td>").Append(HtmlEncode(commandArg.Description)).Append("</td>");

                    sb.AppendLine("</tr>");
                }
                sb.AppendLine("</table>");
                if (Postamble != null)
                    sb.AppendLine("<p>" + Postamble() + "</p>");

                return sb.ToString();
                // ReSharper restore LocalizableElement
            }

            // Regular expression for an argument: a hyphen surrounded by zero or more word characters
            // (i.e. letters, numbers or UnicodeCategory.ConnectorPunctuation) or hyphens
            private static readonly Regex REGEX_ARGUMENT = new Regex("[\\w-]*-[\\w-]*",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);
            /// <summary>
            /// HTML encodes the string.
            /// Also, puts &lt;nobr> tags around everything that contains a hyphen so that argument names do not get broken across lines.
            /// </summary>
            private static string HtmlEncode(string str)
            {
                str = str ?? string.Empty;
                var result = new StringBuilder();
                int charIndex = 0;
                var matchCollection = REGEX_ARGUMENT.Matches(str);
                foreach (Match match in matchCollection)
                {
                    result.Append(HttpUtility.HtmlEncode(str.Substring(charIndex, match.Index - charIndex)));
                    // ReSharper disable LocalizableElement
                    result.Append("<nobr>");
                    result.Append(HttpUtility.HtmlEncode(match.Value));
                    result.Append("</nobr>");
                    // ReSharper restore LocalizableElement
                    charIndex = match.Index + match.Length;
                }

                result.Append(HttpUtility.HtmlEncode(str.Substring(charIndex)));
                return result.ToString();
            }
        }

        public interface IUsageBlock
        {
            string ToString(int width, string formatType);
            string ToHtmlString();
        }


        public class ValueMissingException : UsageException
        {
            public ValueMissingException(Argument arg)
                : base(string.Format(Resources.ValueMissingException_ValueMissingException_, arg.ArgumentText))
            {
            }
        }

        public class ValueUnexpectedException : UsageException
        {
            public ValueUnexpectedException(Argument arg)
                : base(string.Format(Resources.ValueUnexpectedException_ValueUnexpectedException_The_argument__0__should_not_have_a_value_specified, arg.ArgumentText))
            {
            }
        }

        public class ValueInvalidException : UsageException
        {
            public ValueInvalidException(Argument arg, string value, string[] argValues)
                : base(string.Format(Resources.ValueInvalidException_ValueInvalidException_The_value___0___is_not_valid_for_the_argument__1___Use_one_of__2_, value, arg.ArgumentText, string.Join(@", ", argValues)))
            {
            }
        }

        public class ValueInvalidDoubleException : UsageException
        {
            public ValueInvalidDoubleException(Argument arg, string value)
                : base(string.Format(Resources.ValueInvalidDoubleException_ValueInvalidDoubleException_The_value___0___is_not_valid_for_the_argument__1__which_requires_a_decimal_number_, value, arg.ArgumentText))
            {
            }
        }

        public class ValueOutOfRangeDoubleException : UsageException
        {
            public ValueOutOfRangeDoubleException(Argument arg, double value, double minVal, double maxVal)
                : base(string.Format(Resources.ValueOutOfRangeDoubleException_ValueOutOfRangeException_The_value___0___for_the_argument__1__must_be_between__2__and__3__, value, arg.ArgumentText, minVal, maxVal))
            {
            }
        }

        public class ValueInvalidIntException : UsageException
        {
            public ValueInvalidIntException(Argument arg, string value)
                : base(string.Format(Resources.ValueInvalidIntException_ValueInvalidIntException_The_value___0___is_not_valid_for_the_argument__1__which_requires_an_integer_, value, arg.ArgumentText))
            {
            }
        }

        public class ValueInvalidNumberListException : UsageException
        {
            public ValueInvalidNumberListException(Argument arg, string value)
                : base(string.Format(Resources.ValueInvalidNumberListException_ValueInvalidNumberListException_The_value__0__is_not_valid_for_the_argument__1__which_requires_a_list_of_decimal_numbers_, value, arg.ArgumentText))
            {
            }
        }

        public class ValueInvalidChargeListException : UsageException
        {
            public ValueInvalidChargeListException(Argument arg, string value)
                : base(string.Format(Resources.ValueInvalidChargeListException_ValueInvalidChargeListException_The_value___0___is_not_valid_for_the_argument__1__which_requires_an_comma_separated_list_of_integers_, value, arg.ArgumentText))
            {
            }
        }

        public class ValueInvalidIonTypeListException : UsageException
        {
            public ValueInvalidIonTypeListException(Argument arg, string value)
                : base(string.Format(Resources.ValueInvalidIonTypeListException_ValueInvalidIonTypeListException_The_value___0___is_not_valid_for_the_argument__1__which_requires_an_comma_separated_list_of_fragment_ion_types__a__b__c__x__y__z__p__, value, arg.ArgumentText))
            {
            }
        }

        public class ValueOutOfRangeIntException : UsageException
        {
            public ValueOutOfRangeIntException(Argument arg, int value, int minVal, int maxVal)
                : base(string.Format(Resources.ValueOutOfRangeDoubleException_ValueOutOfRangeException_The_value___0___for_the_argument__1__must_be_between__2__and__3__, value, arg.ArgumentText, minVal, maxVal))
            {
            }
        }

        public class ValueInvalidDateException : UsageException
        {
            public ValueInvalidDateException(Argument arg, string value)
                : base(string.Format(Resources.ValueInvalidDateException_ValueInvalidDateException_The_value___0___is_not_valid_for_the_argument__1__which_requires_a_date_time_value_, value, arg.ArgumentText))
            {
            }
        }

        public class ValueInvalidPathException : UsageException
        {
            public ValueInvalidPathException(Argument arg, string value)
                : base(string.Format(Resources.ValueInvalidPathException_ValueInvalidPathException_The_value___0___is_not_valid_for_the_argument__1__failed_attempting_to_convert_it_to_a_full_file_path_, value, arg.ArgumentText))
            {
            }
        }

        public class UsageException : ArgumentException
        {
            protected UsageException(string message) : base(message)
            {
            }
        }
    }
}
