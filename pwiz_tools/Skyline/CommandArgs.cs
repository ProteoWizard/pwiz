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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.Results.RemoteApi.Chorus;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline
{
    public class CommandArg
    {
        private const string ARG_PREFIX = "--";

        public CommandArg(string name, Func<string> valueExample = null, bool wrapValue = false, bool optionalValue = false)
        {
            Name = name;
            ValueExample = valueExample;
            WrapValue = wrapValue;
            OptionalValue = optionalValue;
        }

        public CommandArg(string name, string[] values, bool wrapValue = false, bool optionalValue = false)
            : this(name, () => ValuesToExample(values), wrapValue, optionalValue)
        {
            Values = values;
        }

        public string Name { get; private set; }
        public string Description
        {
            get { return CommandArgUsage.ResourceManager.GetString("_" + Name.Replace('-', '_')); }
        }
        public Func<string> ValueExample { get; private set; }
        public string[] Values { get; private set; }
        public bool WrapValue { get; private set; }
        public bool OptionalValue { get; private set; }

        public string ArgumentText
        {
            get { return ARG_PREFIX + Name; }
        }

        public string ArgumentDescription
        {
            get
            {
                var retValue = ArgumentText;
                if (ValueExample != null)
                {
                    var valueText = '=' + (WrapValue ?  Environment.NewLine : string.Empty) + ValueExample();
                    if (OptionalValue)
                        valueText = '[' + valueText + ']';
                    retValue += valueText;
                }
                return retValue;
            }
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
                return null;

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

    public class NameValuePair
    {
        public NameValuePair(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; private set; }
        public string Value { get; private set; }

        public CommandArg Match { get; private set; }

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
                    throw new ValueInvalidDoubleException(Match, Value);
                }
            }
        }

        public double ValueDouble
        {
            get
            {
                Assume.IsNotNull(Match); // Must be matched before accessing this
                try
                {
                    return double.Parse(Value);
                }
                catch (FormatException)
                {
                    throw new ValueInvalidIntException(Match, Value);
                }
            }
        }

        public bool IsEmpty { get { return string.IsNullOrEmpty(Name); } }
        public bool IsNameOnly { get { return string.IsNullOrEmpty(Value); } }

        public bool IsMatch(CommandArg arg)
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

    public class ValueMissingException : UsageException
    {
        public ValueMissingException(CommandArg arg)
            : base(string.Format(Resources.ValueMissingException_ValueMissingException_, arg.ArgumentText))
        {
        }
    }

    public class ValueUnexpectedException : UsageException
    {
        public ValueUnexpectedException(CommandArg arg)
            : base(string.Format(Resources.ValueUnexpectedException_ValueUnexpectedException_The_argument__0__should_not_have_a_value_specified, arg.ArgumentText))
        {
        }
    }

    public class ValueInvalidException : UsageException
    {
        public ValueInvalidException(CommandArg arg, string value, string[] argValues)
            : base(string.Format(Resources.ValueInvalidException_ValueInvalidException_The_value___0___is_not_valid_for_the_argument__1___Use_one_of__2_, value, arg.ArgumentText, string.Join(@", ", argValues)))
        {
        }
    }

    public class ValueInvalidDoubleException : UsageException
    {
        public ValueInvalidDoubleException(CommandArg arg, string value)
            : base(string.Format(Resources.ValueInvalidDoubleException_ValueInvalidDoubleException_Error__The_value___0___is_not_valid_for_the_argument__1__which_requires_a_decimal_number_, arg.ArgumentText, value))
        {
        }
    }

    public class ValueInvalidIntException : UsageException
    {
        public ValueInvalidIntException(CommandArg arg, string value)
            : base(string.Format(Resources.ValueInvalidIntException_ValueInvalidIntException_Error__The_value___0___is_not_valid_for_the_argument__1__which_requires_an_integer_, arg.ArgumentText, value))
        {
        }
    }

    public class UsageException : ArgumentException
    {
        protected UsageException(string message) : base(message)
        {
        }
    }

    public class CommandArgGroup : IUsageBlock
    {
        private readonly Func<string> _getTitle;

        public CommandArgGroup(Func<string> getTitle, bool showHeaders, params CommandArg[] args)
        {
            _getTitle = getTitle;
            Args = args;
            ShowHeaders = showHeaders;
        }

        public string Title { get { return _getTitle(); } }        
        public Func<string> Preamble { get; set; }
        public Func<string> Postamble { get; set; }
        public IList<CommandArg> Args { get; private set; }
        public bool ShowHeaders { get; private set; }

        public int? LeftColumnWidth { get; set; }

        public override string ToString()
        {
            return ToString(78);
        }

        public string ToString(int width)
        {
            var ct = new ConsoleTable {Title = Title};
            if (Preamble != null)
                ct.Preamble = Preamble();
            if (Postamble != null)
                ct.Postamble = Postamble();
            if (LeftColumnWidth.HasValue)
                ct.Widths = new[] {LeftColumnWidth.Value, width - LeftColumnWidth.Value - 3};   // 3 borders
            else
                ct.Width = width;

            if (ShowHeaders)
                ct.SetHeaders(CommandArgUsage.CommandArgGroup_ToString_Argument, CommandArgUsage.CommandArgGroup_ToString_Description);
            foreach (var commandArg in Args)
            {
                ct.AddRow(commandArg.ArgumentDescription, commandArg.Description);
            }

            return ct.ToString();
        }
    }

    public interface IUsageBlock
    {
        string ToString(int width);
    }

    public class CommandArgs
    {
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

        // Internal use arguments
        public static readonly CommandArg ARG_INTERNAL_SCREEN_WIDTH = new CommandArg(@"sw", INT_VALUE);
        // Multi process import
        public static readonly CommandArg ARG_TEST_IMPORT_FILE_CACHE = new CommandArg(@"import-file-cache", PATH_TO_FILE);
        public static readonly CommandArg ARG_TEST_IMPORT_PROGRESS_PIPE = new CommandArg(@"import-progress-pipe", PIPE_NAME_VALUE);
        public static readonly CommandArg ARG_TEST_UI = new CommandArg(@"ui");
        public static readonly CommandArg ARG_TEST_HIDEACG = new CommandArg(@"hideacg");
        public static readonly CommandArg ARG_TEST_NOACG = new CommandArg(@"noacg");

        public bool HideAllChromatogramsGraph { get; private set; }
        public bool NoAllChromatogramsGraph { get; private set; }

        // Conflict resolution values
        public const string ARG_VALUE_OVERWRITE = "overwrite";
        public const string ARG_VALUE_SKIP = "skip";
        public const string ARG_VALUE_PARALLEL = "parallel";

        public static readonly CommandArg ARG_IN = new CommandArg(@"in", PATH_TO_DOCUMENT);
        public static readonly CommandArg ARG_SAVE = new CommandArg(@"save");
        public static readonly CommandArg ARG_OUT = new CommandArg(@"out", PATH_TO_DOCUMENT);
        public static readonly CommandArg ARG_SHARE_ZIP = new CommandArg(@"share-zip", () => GetPathToFile(SrmDocumentSharing.EXT_SKY_ZIP), false, true);
        public static readonly CommandArg ARG_SHARE_TYPE = new CommandArg(@"share-type",
            new[] {ARG_VALUE_SHARE_TYPE_MINIMAL, ARG_VALUE_SHARE_TYPE_COMPLETE});
        public const string ARG_VALUE_SHARE_TYPE_MINIMAL = "minimal";
        public const string ARG_VALUE_SHARE_TYPE_COMPLETE = "complete";
        public static readonly CommandArg ARG_BATCH = new CommandArg(@"batch-commands", PATH_TO_FILE);
        public static readonly CommandArg ARG_DIR = new CommandArg(@"dir", PATH_TO_FOLDER);
        public static readonly CommandArg ARG_TIMESTAMP = new CommandArg(@"timestamp");
        public static readonly CommandArg ARG_MEMSTAMP = new CommandArg(@"memstamp");
        public static readonly CommandArg ARG_LOG_FILE = new CommandArg(@"log-file", PATH_TO_FILE);
        public static readonly CommandArg ARG_HELP = new CommandArg(@"help");

        private static readonly CommandArgGroup GROUP_GENERAL_IO = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_GENERAL_IO_General_input_output, true,
            ARG_IN, ARG_SAVE, ARG_OUT, ARG_SHARE_ZIP, ARG_SHARE_TYPE, ARG_BATCH, ARG_DIR, ARG_TIMESTAMP, ARG_MEMSTAMP,
            ARG_LOG_FILE, ARG_HELP);

        public string LogFile { get; private set; }
        public string SkylineFile { get; private set; }
        public string SaveFile { get; private set; }
        private bool _saving;
        public bool Saving
        {
            get { return !String.IsNullOrEmpty(SaveFile) || _saving; }
            set { _saving = value; }
        }

        // For sharing zip file
        public bool SharingZipFile { get; private set; }
        public string SharedFile { get; private set; }
        public ShareType SharedFileType { get; private set; }

        public static readonly CommandArg ARG_IMPORT_FILE = new CommandArg(@"import-file", PATH_TO_FILE);
        public static readonly CommandArg ARG_IMPORT_REPLICATE_NAME = new CommandArg(@"import-replicate-name", NAME_VALUE);
        public static readonly CommandArg ARG_IMPORT_OPTIMIZING = new CommandArg(@"import-optimizing", new[] {OPT_CE, OPT_DP});
        public static readonly CommandArg ARG_IMPORT_APPEND = new CommandArg(@"import-append");
        public static readonly CommandArg ARG_IMPORT_ALL = new CommandArg(@"import-all", PATH_TO_FOLDER);
        public static readonly CommandArg ARG_IMPORT_ALL_FILES = new CommandArg(@"import-all-files", PATH_TO_FOLDER);
        public static readonly CommandArg ARG_IMPORT_NAMING_PATTERN = new CommandArg(@"import-naming-pattern", REGEX_VALUE);
        public static readonly CommandArg ARG_IMPORT_BEFORE = new CommandArg(@"import-before", DATE_VALUE);
        public static readonly CommandArg ARG_IMPORT_ON_OR_AFTER = new CommandArg(@"import-on-or-after", DATE_VALUE);
        public static readonly CommandArg ARG_IMPORT_WARN_ON_FAILURE = new CommandArg(@"import-warn-on-failure");
        public static readonly CommandArg ARG_IMPORT_NO_JOIN = new CommandArg(@"import-no-join");
        public static readonly CommandArg ARG_IMPORT_PROCESS_COUNT = new CommandArg(@"import-process-count", INT_VALUE);
        public static readonly CommandArg ARG_IMPORT_THREADS = new CommandArg(@"import-threads", INT_VALUE);
        public static readonly CommandArg ARG_IMPORT_LOCKMASS_POSITIVE = new CommandArg(@"import-lockmass-positive", NUM_VALUE);
        public static readonly CommandArg ARG_IMPORT_LOCKMASS_NEGATIVE = new CommandArg(@"import-lockmass-negative", NUM_VALUE);
        public static readonly CommandArg ARG_IMPORT_LOCKMASS_TOLERANCE = new CommandArg(@"import-lockmass-tolerance", NUM_VALUE);

        private static readonly CommandArgGroup GROUP_IMPORT = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_IMPORT_Importing_results_replicates, false,
            ARG_IMPORT_FILE, ARG_IMPORT_REPLICATE_NAME, ARG_IMPORT_OPTIMIZING, ARG_IMPORT_APPEND, ARG_IMPORT_ALL,
            ARG_IMPORT_ALL_FILES, ARG_IMPORT_NAMING_PATTERN, ARG_IMPORT_BEFORE, ARG_IMPORT_ON_OR_AFTER, ARG_IMPORT_NO_JOIN,
            ARG_IMPORT_PROCESS_COUNT, ARG_IMPORT_THREADS, ARG_IMPORT_LOCKMASS_POSITIVE, ARG_IMPORT_LOCKMASS_NEGATIVE,
            ARG_IMPORT_LOCKMASS_TOLERANCE);

        public static readonly CommandArg ARG_REMOVE_BEFORE = new CommandArg(@"remove-before", DATE_VALUE);
        public static readonly CommandArg ARG_REMOVE_ALL = new CommandArg(@"remove-all");

        private static readonly CommandArgGroup GROUP_REMOVE = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_REMOVE_Removing_results_replicates, false,
            ARG_REMOVE_BEFORE, ARG_REMOVE_ALL);     

        public List<MsDataFileUri> ReplicateFile { get; private set; }
        public string ReplicateName { get; private set; }
        public int ImportThreads { get; private set; }
        public bool ImportAppend { get; private set; }
        public bool ImportDisableJoining { get; private set; }
        public bool ImportRecursive { get; private set; }
        public string ImportSourceDirectory { get; private set; }
        public Regex ImportNamingPattern { get; private set; }
        public bool ImportWarnOnFailure { get; private set; }
        public bool RemovingResults { get; private set; }
        public DateTime? RemoveBeforeDate { get; private set; }
        public DateTime? ImportBeforeDate { get; private set; }
        public DateTime? ImportOnOrAfterDate { get; private set; }
        // Waters lockmass correction
        public double? LockmassPositive { get; private set; }
        public double? LockmassNegative { get; private set; }
        public double? LockmassTolerance { get; private set; }
        public LockMassParameters LockMassParameters { get { return new LockMassParameters(LockmassPositive, LockmassNegative, LockmassTolerance); } }

        // Document import
        public static readonly CommandArg ARG_IMPORT_DOCUMENT = new CommandArg(@"import-document", PATH_TO_DOCUMENT);
        public static readonly CommandArg ARG_IMPORT_DOCUMENT_RESULTS = new CommandArg(@"import-document-results", 
            new []
            {
                ARG_VALUE_IMPORT_DOCUMENT_RESULTS_REMOVE,
                ARG_VALUE_IMPORT_DOCUMENT_RESULTS_MERGE_NAMES,
                ARG_VALUE_IMPORT_DOCUMENT_RESULTS_MERGE_INDICES,
                ARG_VALUE_IMPORT_DOCUMENT_RESULTS_ADD
            }, true);
        public const string ARG_VALUE_IMPORT_DOCUMENT_RESULTS_REMOVE = "remove";
        public const string ARG_VALUE_IMPORT_DOCUMENT_RESULTS_MERGE_NAMES = "merge_names";
        public const string ARG_VALUE_IMPORT_DOCUMENT_RESULTS_MERGE_INDICES = "merge_indices";
        public const string ARG_VALUE_IMPORT_DOCUMENT_RESULTS_ADD = "add";
        public static readonly CommandArg ARG_IMPORT_DOCUMENT_MERGE_PEPTIDES = new CommandArg(@"import-document-merge-peptides");

        private static readonly CommandArgGroup GROUP_IMPORT_DOC = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_IMPORT_DOC_Importing_other_Skyline_documents, false,
            ARG_IMPORT_DOCUMENT, ARG_IMPORT_DOCUMENT_RESULTS, ARG_IMPORT_DOCUMENT_MERGE_PEPTIDES)
            { LeftColumnWidth = 36 };

        public bool ImportingDocuments { get { return DocImportPaths.Any(); } }
        public List<string> DocImportPaths { get; private set; }
        public MeasuredResults.MergeAction? DocImportResultsMerge { get; private set; }
        public bool DocImportMergePeptides { get; private set; }

        // Importing FASTA
        public static readonly CommandArg ARG_IMPORT_FASTA = new CommandArg(@"import-fasta", PATH_TO_FILE);
        public static readonly CommandArg ARG_KEEP_EMPTY_PROTEINS = new CommandArg(@"keep-empty-proteins");

        private static readonly CommandArgGroup GROUP_FASTA = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_FASTA_Importing_FASTA_files, false,
            ARG_IMPORT_FASTA, ARG_KEEP_EMPTY_PROTEINS);

        public string FastaPath { get; private set; }
        public bool KeepEmptyProteins { get; private set; }

        // Transition list and assay library import
        public static readonly CommandArg ARG_IMPORT_TRANSITION_LIST = new CommandArg(@"import-transition-list", PATH_TO_FILE);
        public static readonly CommandArg ARG_IMPORT_ASSAY_LIBRARY = new CommandArg(@"import-assay-library", PATH_TO_FILE);
        public static readonly CommandArg ARG_IGNORE_TRANSITION_ERRORS = new CommandArg(@"ignore-transition-errors");
        public static readonly CommandArg ARG_IRT_STANDARDS_GROUP_NAME = new CommandArg(@"irt-standards-group-name", NAME_VALUE);
        public static readonly CommandArg ARG_IRT_STANDARDS_FILE = new CommandArg(@"irt-standards-file", PATH_TO_FILE);
        public static readonly CommandArg ARG_IRT_DATABASE_PATH = new CommandArg(@"irt-database-path", () => GetPathToFile(IrtDb.EXT));
        public static readonly CommandArg ARG_IRT_CALC_NAME = new CommandArg(@"irt-calc-name", NAME_VALUE);

        private static readonly CommandArgGroup GROUP_IMPORT_LIST = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_IMPORT_LIST_Importing_transition_lists_and_assay_libraries, false,
            ARG_IMPORT_TRANSITION_LIST, ARG_IMPORT_ASSAY_LIBRARY, ARG_IGNORE_TRANSITION_ERRORS, ARG_IRT_STANDARDS_GROUP_NAME,
            ARG_IRT_STANDARDS_FILE, ARG_IRT_DATABASE_PATH, ARG_IRT_CALC_NAME);

        public string TransitionListPath { get; private set; }
        public bool IsTransitionListAssayLibrary { get; private set; }
        public bool IsIgnoreTransitionErrors { get; private set; }
        public string IrtGroupName { get; private set; }
        public string IrtStandardsPath { get; private set; }
        public string IrtDatabasePath { get; private set; }
        public string IrtCalcName { get; private set; }

        // Add a library
        public static readonly CommandArg ARG_ADD_LIBRARY_NAME = new CommandArg(@"add-library-name", NAME_VALUE);
        public static readonly CommandArg ARG_ADD_LIBRARY_PATH = new CommandArg(@"add-library-path", PATH_TO_FILE);

        private static readonly CommandArgGroup GROUP_ADD_LIBRARY = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_ADD_LIBRARY_Adding_spectral_libraries, false,
            ARG_ADD_LIBRARY_PATH, ARG_ADD_LIBRARY_NAME);

        public string LibraryName { get; private set; }
        public string LibraryPath { get; private set; }

        // Decoys
        public static readonly CommandArg ARG_DECOYS_ADD = new CommandArg(@"decoys-add",
            new[] {ARG_VALUE_DECOYS_ADD_REVERSE, ARG_VALUE_DECOYS_ADD_SHUFFLE}, false, true);
        public const string ARG_VALUE_DECOYS_ADD_SHUFFLE = "shuffle";
        public const string ARG_VALUE_DECOYS_ADD_REVERSE = "reverse";
        public static readonly CommandArg ARG_DECOYS_ADD_COUNT = new CommandArg(@"decoys-add-count", INT_VALUE);
        public static readonly CommandArg ARG_DECOYS_DISCARD = new CommandArg(@"decoys-discard");

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
        private static readonly CommandArg ARG_IMPORT_ANNOTATIONS = new CommandArg(@"import-annotations", PATH_TO_FILE);

        private static readonly CommandArgGroup GROUP_ANNOTATIONS = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_ANNOTATIONS_Importing_annotations, false,
            ARG_IMPORT_ANNOTATIONS);

        public string ImportAnnotations { get; private set; }

        // For reintegration
        private static readonly CommandArg ARG_REINTEGRATE_MODEL_NAME = new CommandArg(@"reintegrate-model-name", NAME_VALUE);
        private static readonly CommandArg ARG_REINTEGRATE_CREATE_MODEL = new CommandArg(@"reintegrate-create-model");
        private static readonly CommandArg ARG_REINTEGRATE_MODEL_ITERATION_COUNT = new CommandArg(@"reintegrate-model-iteration-count", INT_VALUE);
        private static readonly CommandArg ARG_REINTEGRATE_MODEL_SECOND_BEST = new CommandArg(@"reintegrate-model-second-best");
        private static readonly CommandArg ARG_REINTEGRATE_MODEL_BOTH = new CommandArg(@"reintegrate-model-both");
        private static readonly CommandArg ARG_REINTEGRATE_OVERWRITE_PEAKS = new CommandArg(@"reintegrate-overwrite-peaks");
        private static readonly CommandArg ARG_REINTEGRATE_LOG_TRAINING = new CommandArg(@"reintegrate-log-training");
        private static readonly CommandArg ARG_REINTEGRATE_EXCLUDE_FEATURE = new CommandArg(@"reintegrate-exclude-feature", FEATURE_NAME_VALUE, true);

        private static readonly CommandArgGroup GROUP_REINTEGRATE = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_REINTEGRATE_Reintegrate_with_advanced_peak_picking_models, false,
            ARG_REINTEGRATE_MODEL_NAME, ARG_REINTEGRATE_CREATE_MODEL, /* ARG_REINTEGRATE_MODEL_ITERATION_COUNT, */
            ARG_REINTEGRATE_MODEL_SECOND_BEST, ARG_REINTEGRATE_MODEL_BOTH, ARG_REINTEGRATE_OVERWRITE_PEAKS,
            /* ARG_REINTEGRATE_LOG_TRAINING, */ ARG_REINTEGRATE_EXCLUDE_FEATURE)
            { LeftColumnWidth = 32 };

        public string ReintegratModelName { get; private set; }
        public int? ReintegrateModelIterationCount { get; private set; }
        public bool IsOverwritePeaks { get; private set; }
        public bool IsCreateScoringModel { get; private set; }
        public bool IsSecondBestModel { get; private set; }
        public bool IsDecoyModel { get; private set; }
        public bool IsLogTraining { get; private set; }
        public List<IPeakFeatureCalculator> ExcludeFeatures { get; private set; }

        public bool Reintegrating { get { return !string.IsNullOrEmpty(ReintegratModelName); } }

        // For exporting reports
        public static readonly CommandArg ARG_REPORT_NAME = new CommandArg(@"report-name", NAME_VALUE);
        public static readonly CommandArg ARG_REPORT_FILE = new CommandArg(@"report-file", () => GetPathToFile(TextUtil.EXT_CSV));
        public static readonly CommandArg ARG_REPORT_ADD = new CommandArg(@"report-add", NAME_VALUE);
        public static readonly CommandArg ARG_REPORT_CONFLICT_RESOLUTION = new CommandArg(@"report-conflict-resolution",
            new []{ARG_VALUE_OVERWRITE, ARG_VALUE_SKIP}, true);
        public static readonly CommandArg ARG_REPORT_FORMAT = new CommandArg(@"report-format",
            new []{ARG_VALUE_CSV, ARG_VALUE_TSV});
        public const string ARG_VALUE_CSV = "csv";
        public const string ARG_VALUE_TSV = "tsv";
        public static readonly CommandArg ARG_REPORT_INVARIANT = new CommandArg(@"report-invariant");

        private static readonly CommandArgGroup GROUP_REPORT = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_REPORT_Exporting_reports, false,
            ARG_REPORT_NAME, ARG_REPORT_FILE, ARG_REPORT_ADD, ARG_REPORT_CONFLICT_RESOLUTION, ARG_REPORT_FORMAT,
            ARG_REPORT_INVARIANT) {LeftColumnWidth = 30};

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
        private static readonly CommandArg ARG_CHROMATOGRAM_FILE = new CommandArg(@"chromatogram-file", () => GetPathToFile(TextUtil.EXT_TSV));
        private static readonly CommandArg ARG_CHROMATOGRAM_PRECURSORS = new CommandArg(@"chromatogram-precursors");
        private static readonly CommandArg ARG_CHROMATOGRAM_PRODUCTS = new CommandArg(@"chromatogram-products");
        private static readonly CommandArg ARG_CHROMATOGRAM_BASE_PEAKS = new CommandArg(@"chromatogram-base-peaks");
        private static readonly CommandArg ARG_CHROMATOGRAM_TICS = new CommandArg(@"chromatogram-tics");

        private static readonly CommandArgGroup GROUP_CHROMATOGRAM = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_CHROMATOGRAM_Exporting_chromatograms, false,
            ARG_CHROMATOGRAM_FILE, ARG_CHROMATOGRAM_PRECURSORS, ARG_CHROMATOGRAM_PRODUCTS, ARG_CHROMATOGRAM_BASE_PEAKS,
            ARG_CHROMATOGRAM_TICS);

        public string ChromatogramsFile { get; private set; }
        public bool ChromatogramsPrecursors { get; private set; }
        public bool ChromatogramsProducts { get; private set; }
        public bool ChromatogramsBasePeaks { get; private set; }
        public bool ChromatogramsTics { get; private set; }
        public bool ExportingChromatograms { get { return !string.IsNullOrEmpty(ChromatogramsFile); } }


        // For publishing the document to Panorama
        private static readonly CommandArg ARG_PANORAMA_SERVER = new CommandArg(@"panorama-server", SERVER_URL_VALUE);
        private static readonly CommandArg ARG_PANORAMA_USERNAME = new CommandArg(@"panorama-username", USERNAME_VALUE);
        private static readonly CommandArg ARG_PANORAMA_PASSWORD = new CommandArg(@"panorama-password", PASSWORD_VALUE);
        private static readonly CommandArg ARG_PANORAMA_FOLDER = new CommandArg(@"panorama-folder", PATH_TO_FOLDER);

        private static readonly CommandArgGroup GROUP_PANORAMA = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_PANORAMA_Publishing_to_Panorama, false,
            ARG_PANORAMA_SERVER, ARG_PANORAMA_USERNAME, ARG_PANORAMA_PASSWORD, ARG_PANORAMA_FOLDER
        ) {Postamble = () => CommandArgUsage.CommandArgs_GROUP_PANORAMA_postamble};

        private string PanoramaServerUri { get; set; }
        private string PanoramaUserName { get; set; }
        private string PanoramaPassword { get; set; }
        public string PanoramaFolder { get; private set; }
        public bool PublishingToPanorama { get; private set; }
        public Server PanoramaServer { get; private set; }

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
        public static readonly CommandArg ARG_IMPORT_PEPTIDE_SEARCH_FILE = new CommandArg(@"import-search-file", PATH_TO_FILE);
        public static readonly CommandArg ARG_IMPORT_PEPTIDE_SEARCH_CUTOFF = new CommandArg(@"import-search-cutoff-score", NUM_VALUE);
        public static readonly CommandArg ARG_IMPORT_PEPTIDE_SEARCH_MODS = new CommandArg(@"import-search-add-mods");
        public static readonly CommandArg ARG_IMPORT_PEPTIDE_SEARCH_AMBIGUOUS = new CommandArg(@"import-search-include-ambiguous");
        public static readonly CommandArg ARG_IMPORT_PEPTIDE_SEARCH_PREFER_EMBEDDED = new CommandArg(@"import-search-prefer-embedded-spectra");

        private static readonly CommandArgGroup GROUP_IMPORT_SEARCH = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_IMPORT_SEARCH_Importing_peptide_searches, false, 
            ARG_IMPORT_PEPTIDE_SEARCH_FILE, ARG_IMPORT_PEPTIDE_SEARCH_CUTOFF, ARG_IMPORT_PEPTIDE_SEARCH_MODS,
            ARG_IMPORT_PEPTIDE_SEARCH_AMBIGUOUS, ARG_IMPORT_PEPTIDE_SEARCH_PREFER_EMBEDDED);

        public List<string> SearchResultsFiles { get; private set; }
        public double? CutoffScore { get; private set; }
        public bool AcceptAllModifications { get; private set; }
        public bool IncludeAmbiguousMatches { get; private set; }
        public bool? PreferEmbeddedSpectra { get; private set; }
        public bool ImportingSearch
        {
            get { return SearchResultsFiles.Count > 0; }
        }

        // For adjusting full-scan settings
        public static readonly CommandArg ARG_FULL_SCAN_PRECURSOR_RES = new CommandArg(@"full-scan-precursor-res", RP_VALUE);
        public static readonly CommandArg ARG_FULL_SCAN_PRECURSOR_RES_MZ = new CommandArg(@"full-scan-precursor-res-mz", MZ_VALUE);
        public static readonly CommandArg ARG_FULL_SCAN_PRODUCT_RES = new CommandArg(@"full-scan-product-res", RP_VALUE);
        public static readonly CommandArg ARG_FULL_SCAN_PRODUCT_RES_MZ = new CommandArg(@"full-scan-product-res-mz", MZ_VALUE);
        public static readonly CommandArg ARG_FULL_SCAN_RT_FILTER_TOLERANCE = new CommandArg(@"full-scan-rt-filter-tolerance", MINUTES_VALUE);

        private static readonly CommandArgGroup GROUP_SETTINGS = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_SETTINGS_Document_Settings, false,
            ARG_FULL_SCAN_PRECURSOR_RES, ARG_FULL_SCAN_PRECURSOR_RES_MZ,
            ARG_FULL_SCAN_PRODUCT_RES, ARG_FULL_SCAN_PRODUCT_RES_MZ,
            ARG_FULL_SCAN_RT_FILTER_TOLERANCE);

        public double? FullScanPrecursorRes { get; private set; }
        public double? FullScanPrecursorResMz { get; private set; }
        public double? FullScanProductRes { get; private set; }
        public double? FullScanProductResMz { get; private set; }
        public double? FullScanRetentionTimeFilterLength { get; private set; }

        public bool FullScanSetting
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

        // For importing a tool from a zip file.
        public static readonly CommandArg ARG_TOOL_ADD = new CommandArg(@"tool-add", NAME_VALUE);
        public static readonly CommandArg ARG_TOOL_COMMAND = new CommandArg(@"tool-command", COMMAND_VALUE);
        public static readonly CommandArg ARG_TOOL_ARGUMENTS = new CommandArg(@"tool-arguments", COMMAND_ARGUMENTS_VALUE);
        public static readonly CommandArg ARG_TOOL_INITIAL_DIR = new CommandArg(@"tool-initial-dir", PATH_TO_FOLDER);
        public static readonly CommandArg ARG_TOOL_CONFLICT_RESOLUTION = new CommandArg(@"tool-conflict-resolution", 
            new[] {ARG_VALUE_OVERWRITE, ARG_VALUE_SKIP}, true);
        public static readonly CommandArg ARG_TOOL_REPORT = new CommandArg(@"tool-report", REPORT_NAME_VALUE);
        public static readonly CommandArg ARG_TOOL_OUTPUT_TO_IMMEDIATE_WINDOW = new CommandArg(@"tool-output-to-immediate-window");
        public static readonly CommandArg ARG_TOOL_ADD_ZIP = new CommandArg(@"tool-add-zip", () => GetPathToFile(ToolDescription.EXT_INSTALL));
        public static readonly CommandArg ARG_TOOL_ZIP_CONFLICT_RESOLUTION = new CommandArg(@"tool-zip-conflict-resolution",
            new [] {ARG_VALUE_OVERWRITE, ARG_VALUE_PARALLEL}, true);
        public static readonly CommandArg ARG_TOOL_ZIP_OVERWRITE_ANNOTATIONS = new CommandArg(@"tool-zip-overwrite-annotations",
            new[] {ARG_VALUE_TRUE, ARG_VALUE_FALSE}, true);
        public const string ARG_VALUE_TRUE = "true";
        public const string ARG_VALUE_FALSE = "false";
        public static readonly CommandArg ARG_TOOL_PROGRAM_MACRO = new CommandArg(@"tool-program-macro",
            PROGRAM_MACRO_VALUE, true);
        public static readonly CommandArg ARG_TOOL_PROGRAM_PATH = new CommandArg(@"tool-program-path", PATH_TO_FILE);
        public static readonly CommandArg ARG_TOOL_IGNORE_REQUIRED_PACKAGES = new CommandArg(@"tool-ignore-required-packages");
        public static readonly CommandArg ARG_TOOL_LIST_EXPORT = new CommandArg(@"tool-list-export", PATH_TO_FILE);

        private static readonly CommandArgGroup GROUP_TOOLS = new CommandArgGroup(() => Resources.CommandArgs_GROUP_TOOLS_Tools_Installation, false,
            ARG_TOOL_ADD, ARG_TOOL_COMMAND, ARG_TOOL_ARGUMENTS, ARG_TOOL_INITIAL_DIR, ARG_TOOL_CONFLICT_RESOLUTION,
            ARG_TOOL_REPORT, ARG_TOOL_OUTPUT_TO_IMMEDIATE_WINDOW, ARG_TOOL_ADD_ZIP, ARG_TOOL_ZIP_CONFLICT_RESOLUTION,
            ARG_TOOL_ZIP_OVERWRITE_ANNOTATIONS, ARG_TOOL_PROGRAM_MACRO, ARG_TOOL_PROGRAM_PATH,
            ARG_TOOL_IGNORE_REQUIRED_PACKAGES
            /* undocumented ARG_TOOL_LIST_EXPORT */)
        {
            Preamble = () => Resources.CommandArgs_GROUP_TOOLS_The_arguments_below_can_be_used_to_install_tools_onto_the_Tools_menu_and_do_not_rely_on_the____in__argument_because_they_independent_of_a_specific_Skyline_document_,
            LeftColumnWidth = 36
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
        public static readonly CommandArg ARG_EXP_ISOLATION_LIST_INSTRUMENT = new CommandArg(@"exp-isolationlist-instrument",
            ExportInstrumentType.ISOLATION_LIST_TYPES, true);
        public static readonly CommandArg ARG_EXP_TRANSITION_LIST_INSTRUMENT = new CommandArg(@"exp-translist-instrument",
            ExportInstrumentType.TRANSITION_LIST_TYPES, true);
        private static readonly CommandArgGroup GROUP_LISTS = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_LISTS_Exporting_isolation_transition_lists, false,
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

        // Export method
        public static readonly CommandArg ARG_EXP_METHOD_INSTRUMENT = new CommandArg(@"exp-method-instrument",
            ExportInstrumentType.METHOD_TYPES);
        public static readonly CommandArg ARG_EXP_TEMPLATE = new CommandArg(@"exp-template", PATH_TO_FILE);
        private static readonly CommandArgGroup GROUP_METHOD = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_METHOD_Exporting_native_instrument_methods, false,
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

        // Export list/method arguments
        public static readonly CommandArg ARG_EXP_FILE = new CommandArg(@"exp-file", PATH_TO_FILE);
        public static readonly CommandArg ARG_EXP_STRATEGY = new CommandArg(@"exp-strategy",
            Helpers.GetEnumValues<ExportStrategy>().Select(p => p.ToString()).ToArray());
        public static readonly CommandArg ARG_EXP_METHOD_TYPE = new CommandArg(@"exp-method-type",
            Helpers.GetEnumValues<ExportMethodType>().Select(p => p.ToString()).ToArray());
        public static readonly CommandArg ARG_EXP_MAX_TRANS = new CommandArg(@"exp-max-trans", NAME_VALUE);
        public static readonly CommandArg ARG_EXP_OPTIMIZING = new CommandArg(@"exp-optimizing", 
            new[] { OPT_CE, OPT_DP});
        public static readonly CommandArg ARG_EXP_SCHEDULING_REPLICATE = new CommandArg(@"exp-scheduling-replicate", NAME_VALUE);
        public static readonly CommandArg ARG_EXP_IGNORE_PROTEINS = new CommandArg(@"exp-ignore-proteins");
        public static readonly CommandArg ARG_EXP_PRIMARY_COUNT = new CommandArg(@"exp-primary-count", INT_VALUE);
        public static readonly CommandArg ARG_EXP_POLARITY = new CommandArg(@"exp-polarity", 
            Helpers.GetEnumValues<ExportPolarity>().Select(p => p.ToString()).ToArray());
        // Instrument specific arguments
        public static readonly CommandArg ARG_EXP_DWELL_TIME = new CommandArg(@"exp-dwell-time", MILLIS_VALE);
        public static readonly CommandArg ARG_EXP_ADD_ENERGY_RAMP = new CommandArg(@"exp-add-energy-ramp");
        public static readonly CommandArg ARG_EXP_USE_S_LENS = new CommandArg(@"exp-use-s-lens");
        public static readonly CommandArg ARG_EXP_RUN_LENGTH = new CommandArg(@"exp-run-length", MINUTES_VALUE);

        private static readonly CommandArgGroup GROUP_EXP_GENERAL = new CommandArgGroup(() => CommandArgUsage.CommandArgs_GROUP_EXP_GENERAL_Method_and_transition_list_options, false,
            ARG_EXP_FILE, ARG_EXP_STRATEGY, ARG_EXP_METHOD_TYPE, ARG_EXP_MAX_TRANS,
            ARG_EXP_OPTIMIZING, ARG_EXP_SCHEDULING_REPLICATE, ARG_EXP_IGNORE_PROTEINS,
            ARG_EXP_PRIMARY_COUNT, ARG_EXP_POLARITY) {LeftColumnWidth = 34};

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
        private int _runLength;
        public int RunLength
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

            public string ToString(int width)
            {
                return ConsoleTable.ParaToString(width, Text, true);
            }
        }

        public IEnumerable<IUsageBlock> UsageBlocks
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
                    new ParaUsageBlock(CommandArgUsage.CommandArgs_Usage_Until_the_section_titled_Settings_Customization_all_other_command_line_arguments_rely_on_the____in__argument_because_they_all_rely_on_having_a_Skyline_document_open_),
                    GROUP_IMPORT,
                    GROUP_REINTEGRATE,
                    GROUP_REMOVE,
                    GROUP_IMPORT_DOC,
                    GROUP_ANNOTATIONS,
                    GROUP_FASTA,
                    GROUP_IMPORT_SEARCH,
                    GROUP_IMPORT_LIST,
                    GROUP_ADD_LIBRARY,
                    GROUP_REPORT,
                    GROUP_CHROMATOGRAM,
                    GROUP_LISTS,
                    // TODO: GROUP_LISTS_VENDOR,
                    GROUP_METHOD,
                    // TODO: GROUP_METHOD_VENDOR,
                    GROUP_EXP_GENERAL,
                    GROUP_PANORAMA,
                    GROUP_SETTINGS,
                    GROUP_TOOLS
                };
            }
        }

        private void Usage()
        {
            foreach (var block in UsageBlocks)
                _out.Write(block.ToString(_usageWidth));
        }

        private int _usageWidth = 78;

        private readonly CommandStatusWriter _out;
        private readonly bool _isDocumentLoaded;

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

            ImportBeforeDate = null;
            ImportOnOrAfterDate = null;
        }

        public bool ParseArgs(string[] args)
        {
            try
            {
                return ParseArgsInternal(args);
            }
            catch (UsageException x)
            {
                _out.WriteLine(Resources.CommandLine_GeneralException_Error___0_, x.Message);
                return false;
            }
            catch (Exception x)
            {
                // Unexpected behavior, but better to output the error then appear to crash, and
                // have Windows write it to the application event log.
                _out.WriteLine(Resources.CommandLine_GeneralException_Error___0_, x.Message);
                _out.WriteLine(x.StackTrace);
                return false;
            }
        }

        private bool ParseArgsInternal(IEnumerable<string> args)
        {
            ImportThreads = 1;

            foreach (string s in args)
            {
                var pair = CommandArg.Parse(s);
                if (pair.IsEmpty)
                    continue;

                if (pair.IsMatch(ARG_HELP))
                {
                    Usage();
                    return false;
                }

                if (pair.IsMatch(ARG_TEST_UI))
                {
                    // Handled by Program
                }
                else if (pair.IsMatch(ARG_INTERNAL_SCREEN_WIDTH))
                {
                    _usageWidth = pair.ValueInt;
                }
                else if (pair.IsMatch(ARG_TEST_IMPORT_FILE_CACHE))
                {
                    Program.ReplicateCachePath = pair.Value;
                    RequiresSkylineDocument = true;
                }
                else if (pair.IsMatch(ARG_TEST_IMPORT_PROGRESS_PIPE))
                {
                    Program.ImportProgressPipe = pair.Value;
                    RequiresSkylineDocument = true;
                }
                else if (pair.IsMatch(ARG_TEST_HIDEACG))
                {
                    HideAllChromatogramsGraph = true;
                }
                else if (pair.IsMatch(ARG_TEST_NOACG))
                {
                    NoAllChromatogramsGraph = true;
                }
                else if (pair.IsMatch(ARG_LOG_FILE))
                {
                    LogFile = pair.Value;
                }
                else if (pair.IsMatch(ARG_IN))
                {
                    SkylineFile = GetFullPath(pair.Value);
                    // Set requiresInCommand to be true so if SkylineFile is null or empty it still complains.
                    RequiresSkylineDocument = true;
                }
                else if (pair.IsMatch(ARG_DIR))
                {
                    if (!Directory.Exists(pair.Value))
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__The_specified_working_directory__0__does_not_exist_, pair.Value);
                        return false;
                    }
                    Directory.SetCurrentDirectory(pair.Value);
                }
                else if (pair.IsMatch(ARG_IMPORT_THREADS))
                {
                    ImportThreads = pair.ValueInt;
                }
                else if (pair.IsMatch(ARG_IMPORT_PROCESS_COUNT))
                {
                    ImportThreads = pair.ValueInt;
                    if (ImportThreads > 0)
                        Program.MultiProcImport = true;
                }
                else if (pair.IsMatch(ARG_TIMESTAMP))
                {
                    _out.IsTimeStamped = true;
                }
                else if (pair.IsMatch(ARG_MEMSTAMP))
                {
                    _out.IsMemStamped = true;
                }
                else if (pair.IsMatch(ARG_FULL_SCAN_PRECURSOR_RES))
                {
                    RequiresSkylineDocument = true;
                    FullScanPrecursorRes = pair.ValueDouble;
                }
                else if (pair.IsMatch(ARG_FULL_SCAN_PRECURSOR_RES_MZ))
                {
                    RequiresSkylineDocument = true;
                    FullScanPrecursorResMz = pair.ValueDouble;
                }
                else if (pair.IsMatch(ARG_FULL_SCAN_PRODUCT_RES))
                {
                    RequiresSkylineDocument = true;
                    FullScanProductRes = pair.ValueDouble;
                }
                else if (pair.IsMatch(ARG_FULL_SCAN_PRODUCT_RES_MZ))
                {
                    RequiresSkylineDocument = true;
                    FullScanProductResMz = pair.ValueDouble;
                }
                else if (pair.IsMatch(ARG_FULL_SCAN_RT_FILTER_TOLERANCE))
                {
                    RequiresSkylineDocument = true;
                    FullScanRetentionTimeFilterLength = pair.ValueDouble;
                }

                else if (pair.IsMatch(ARG_TOOL_ADD_ZIP))
                {
                    InstallingToolsFromZip = true;
                    ZippedToolsPath = pair.Value;
                }
                else if (pair.IsMatch(ARG_TOOL_ZIP_CONFLICT_RESOLUTION))
                {
                    ResolveZipToolConflictsBySkipping = pair.IsValue(ARG_VALUE_OVERWRITE)
                        ? CommandLine.ResolveZipToolConflicts.overwrite
                        : CommandLine.ResolveZipToolConflicts.in_parallel;
                }
                else if (pair.IsMatch(ARG_TOOL_ZIP_OVERWRITE_ANNOTATIONS))
                {
                    ResolveZipToolAnotationConflictsBySkipping = pair.IsValue(ARG_VALUE_TRUE);
                }
                else if (pair.IsMatch(ARG_TOOL_PROGRAM_MACRO)) // example --tool-program-macro=R,2.15.2
                {
                    string [] spliced = pair.Value.Split(',');
                    if (spliced.Length > 2)
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Warning__Incorrect_Usage_of_the___tool_program_macro_command_);
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
                else if (pair.IsMatch(ARG_TOOL_PROGRAM_PATH))
                {
                    ZippedToolsProgramPathValue = pair.Value;
                }
                else if (pair.IsMatch(ARG_TOOL_IGNORE_REQUIRED_PACKAGES))
                {
                    ZippedToolsPackagesHandled = true;
                }

                else if (pair.IsMatch(ARG_TOOL_ADD))
                {
                    ImportingTool = true;
                    ToolName = pair.Value;
                }

                else if (pair.IsMatch(ARG_TOOL_COMMAND))
                {
                    ImportingTool = true;
                    ToolCommand = pair.Value;
                }

                else if (pair.IsMatch(ARG_TOOL_ARGUMENTS))
                {
                    ImportingTool = true;
                    ToolArguments = pair.Value;
                }

                else if (pair.IsMatch(ARG_TOOL_INITIAL_DIR))
                {
                    ImportingTool = true;
                    ToolInitialDirectory = pair.Value;
                }
                else if (pair.IsMatch(ARG_TOOL_REPORT))
                {
                    ImportingTool = true;
                    ToolReportTitle = pair.Value;
                }
                else if (pair.IsMatch(ARG_TOOL_OUTPUT_TO_IMMEDIATE_WINDOW))
                {
                    ImportingTool = true;
                    ToolOutputToImmediateWindow = true;
                }

                else if (pair.IsMatch(ARG_TOOL_CONFLICT_RESOLUTION))
                {
                    ResolveToolConflictsBySkipping = pair.IsValue(ARG_VALUE_SKIP);
                }
                // A command that exports all the tools to a text file in a SkylineRunner form for --batch-commands
                // Not advertised.
                else if (pair.IsMatch(ARG_TOOL_LIST_EXPORT))
                {
                    string pathToOutputFile = pair.Value;
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
                else if (pair.IsMatch(ARG_IMPORT_PEPTIDE_SEARCH_FILE))
                {
                    RequiresSkylineDocument = true;
                    SearchResultsFiles.Add(GetFullPath(pair.Value));
                    CutoffScore = CutoffScore ?? Settings.Default.LibraryResultCutOff;
                }
                else if (pair.IsMatch(ARG_IMPORT_PEPTIDE_SEARCH_CUTOFF))
                {
                    double? cutoff;
                    try
                    {
                        cutoff = pair.ValueDouble;
                        if (cutoff < 0 || cutoff > 1)
                        {
                            cutoff = null;
                        }
                    }
                    catch
                    {
                        cutoff = null;
                    }
                    if (cutoff.HasValue)
                    {
                        CutoffScore = cutoff.Value;
                    }
                    else
                    {
                        var defaultScore = Settings.Default.LibraryResultCutOff;
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Warning__The_cutoff_score__0__is_invalid__It_must_be_a_value_between_0_and_1_, pair.Value);
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Defaulting_to__0__, defaultScore);
                        CutoffScore = defaultScore;
                    }
                }
                else if (pair.IsMatch(ARG_IMPORT_PEPTIDE_SEARCH_MODS))
                {
                    AcceptAllModifications = true;
                }
                else if (pair.IsMatch(ARG_IMPORT_PEPTIDE_SEARCH_AMBIGUOUS))
                {
                    IncludeAmbiguousMatches = true;
                }
                else if (pair.IsMatch(ARG_IMPORT_PEPTIDE_SEARCH_PREFER_EMBEDDED))
                {
                    PreferEmbeddedSpectra = true;
                }

                // Run each line of a text file like a SkylineRunner command
                else if (pair.IsMatch(ARG_BATCH))
                {
                    BatchCommandsPath = GetFullPath(pair.Value);
                    RunningBatchCommands = true;
                }

                else if (pair.IsMatch(ARG_SAVE))
                {
                    Saving = true;
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_OUT))
                {
                    SaveFile = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_ADD_LIBRARY_NAME))
                {
                    LibraryName = pair.Value;
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_ADD_LIBRARY_PATH))
                {
                    LibraryPath = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_IMPORT_FASTA))
                {
                    FastaPath = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_KEEP_EMPTY_PROTEINS))
                {
                    KeepEmptyProteins = true;
                }

                else if (pair.IsMatch(ARG_IMPORT_DOCUMENT))
                {
                    DocImportPaths.Add(GetFullPath(pair.Value));
                    DocImportResultsMerge = DocImportResultsMerge ?? MeasuredResults.MergeAction.remove;
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_IMPORT_DOCUMENT_RESULTS))
                {
                    MeasuredResults.MergeAction mergeAction;
                    if (!Enum.TryParse(pair.Value, out mergeAction))
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Invalid_value__0__for__1___Check_the_documentation_for_available_results_merging_options_,
                            pair.Value, ARG_IMPORT_DOCUMENT_RESULTS.ArgumentText);
                        return false;
                    }
                    DocImportResultsMerge = mergeAction;
                }

                else if (pair.IsMatch(ARG_IMPORT_DOCUMENT_MERGE_PEPTIDES))
                {
                    DocImportMergePeptides = true;
                }

                else if (pair.IsMatch(ARG_IMPORT_TRANSITION_LIST))
                {
                    TransitionListPath = GetFullPath(pair.Value);
                    IsTransitionListAssayLibrary = false;
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_IMPORT_ASSAY_LIBRARY))
                {
                    TransitionListPath = GetFullPath(pair.Value);
                    IsTransitionListAssayLibrary = true;
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_IGNORE_TRANSITION_ERRORS))
                {
                    IsIgnoreTransitionErrors = true;
                }

                else if (pair.IsMatch(ARG_IRT_STANDARDS_GROUP_NAME))
                {
                    IrtGroupName = pair.Value;
                }

                else if (pair.IsMatch(ARG_IRT_STANDARDS_FILE))
                {
                    IrtStandardsPath = pair.Value;
                }

                else if (pair.IsMatch(ARG_IRT_DATABASE_PATH))
                {
                    IrtDatabasePath = pair.Value;
                }

                else if (pair.IsMatch(ARG_IRT_CALC_NAME))
                {
                    IrtCalcName = pair.Value;
                }

                else if (pair.IsMatch(ARG_DECOYS_ADD))
                {
                    AddDecoysType = pair.IsNameOnly || pair.IsValue(ARG_VALUE_DECOYS_ADD_REVERSE)
                        ? DecoyGeneration.REVERSE_SEQUENCE
                        : DecoyGeneration.SHUFFLE_SEQUENCE;
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_DECOYS_ADD_COUNT))
                {
                    AddDecoysCount = pair.ValueInt;
                }

                else if (pair.IsMatch(ARG_DECOYS_DISCARD))
                {
                    DiscardDecoys = true;
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_IMPORT_FILE))
                {
                    if (pair.Value.StartsWith(ChorusUrl.ChorusUrlPrefix))
                    {
                        ReplicateFile.Add(MsDataFileUri.Parse(pair.Value));
                    }
                    else
                    {
                        ReplicateFile.Add(new MsDataFilePath(GetFullPath(pair.Value)));
                    }
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_IMPORT_REPLICATE_NAME))
                {
                    ReplicateName = pair.Value;
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_IMPORT_LOCKMASS_POSITIVE))
                {
                    LockmassPositive = pair.ValueDouble;
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_IMPORT_LOCKMASS_NEGATIVE))
                {
                    LockmassNegative = pair.ValueDouble;
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_IMPORT_LOCKMASS_TOLERANCE))
                {
                    LockmassTolerance = pair.ValueDouble;
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_IMPORT_ANNOTATIONS))
                {
                    ImportAnnotations = pair.Value;
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_IMPORT_APPEND))
                {
                    ImportAppend = true;
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_IMPORT_ALL))
                {
                    ImportSourceDirectory = GetFullPath(pair.Value);
                    ImportRecursive = true;
                    RequiresSkylineDocument = true;
                }
                else if (pair.IsMatch(ARG_IMPORT_ALL_FILES))
                {
                    ImportSourceDirectory = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_IMPORT_NO_JOIN))
                {
                    ImportDisableJoining = true;
                    RequiresSkylineDocument = true;
                }
                // ReSharper restore LocalizableElement

                else if (pair.IsMatch(ARG_IMPORT_NAMING_PATTERN))
                {
                    var importNamingPatternVal = pair.Value;
                    RequiresSkylineDocument = true;
                    if (importNamingPatternVal != null)
                    {
                        try
                        {
                            ImportNamingPattern = new Regex(importNamingPatternVal);
                        }
                        catch (Exception e)
                        {
                            _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Regular_expression__0__cannot_be_parsed_, importNamingPatternVal);
                            _out.WriteLine(e.Message);
                            return false;
                        }

                        // ReSharper disable LocalizableElement
                        Match match = Regex.Match(importNamingPatternVal, @".*\(.+\).*");
                        // ReSharper restore LocalizableElement
                        if (!match.Success)
                        {
                            _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Regular_expression___0___does_not_have_any_groups___String,
                                importNamingPatternVal);
                            return false;
                        }
                    }
                }

                else if (pair.IsMatch(ARG_IMPORT_OPTIMIZING))
                {
                    try
                    {
                        ImportOptimizeType = pair.Value;
                    }
                    catch (ArgumentException)
                    {
                        _out.WriteLine(
                            Resources.CommandArgs_ParseArgsInternal_Warning__Invalid_optimization_parameter___0____Use__ce____dp___or__none____Defaulting_to_none_,
                            pair.Value);
                    }
                }

                else if (pair.IsMatch(ARG_IMPORT_BEFORE))
                {
                    var importBeforeDate = pair.Value;
                    if (importBeforeDate != null)
                    {
                        try
                        {
                            ImportBeforeDate = Convert.ToDateTime(importBeforeDate);
                        }
                        catch (Exception e)
                        {
                            _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Date__0__cannot_be_parsed_, importBeforeDate);
                            _out.WriteLine(e.Message);
                            return false;
                        }
                    }
                }

                else if (pair.IsMatch(ARG_IMPORT_ON_OR_AFTER))
                {
                    var importAfterDate = pair.Value;
                    if (importAfterDate != null)
                    {
                        try
                        {
                            ImportOnOrAfterDate = Convert.ToDateTime(importAfterDate);
                        }
                        catch (Exception e)
                        {
                            _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Date__0__cannot_be_parsed_, importAfterDate);
                            _out.WriteLine(e.Message);
                            return false;
                        }
                    }
                }

                else if (pair.IsMatch(ARG_IMPORT_WARN_ON_FAILURE))
                {
                    ImportWarnOnFailure = true;
                }

                else if (pair.IsMatch(ARG_REMOVE_ALL))
                {
                    RemovingResults = true;
                    RequiresSkylineDocument = true;
                    RemoveBeforeDate = null;
                }
                else if (pair.IsMatch(ARG_REMOVE_BEFORE))
                {
                    var removeBeforeDate = pair.Value;
                    RemovingResults = true;
                    RequiresSkylineDocument = true;
                    if (removeBeforeDate != null)
                    {
                        try
                        {
                            RemoveBeforeDate = Convert.ToDateTime(removeBeforeDate);
                        }
                        catch (Exception e)
                        {
                            _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Date__0__cannot_be_parsed_, removeBeforeDate);
                            _out.WriteLine(e.Message);
                            return false;
                        }
                    }
                }
                else if (pair.IsMatch(ARG_REINTEGRATE_MODEL_NAME))
                {
                    ReintegratModelName = pair.Value;
                }
                else if (pair.IsMatch(ARG_REINTEGRATE_CREATE_MODEL))
                {
                    IsCreateScoringModel = true;
                    if (!IsSecondBestModel)
                        IsDecoyModel = true;
                }
                else if (pair.IsMatch(ARG_REINTEGRATE_MODEL_ITERATION_COUNT))
                {
                    ReintegrateModelIterationCount = pair.ValueInt;
                }
                else if (pair.IsMatch(ARG_REINTEGRATE_OVERWRITE_PEAKS))
                {
                    IsOverwritePeaks = true;
                }
                else if (pair.IsMatch(ARG_REINTEGRATE_MODEL_SECOND_BEST))
                {
                    IsSecondBestModel = true;
                    IsDecoyModel = false;
                }
                else if (pair.IsMatch(ARG_REINTEGRATE_MODEL_BOTH))
                {
                    IsSecondBestModel = IsDecoyModel = true;
                }
                else if (pair.IsMatch(ARG_REINTEGRATE_LOG_TRAINING))
                {
                    IsLogTraining = true;
                }
                else if (pair.IsMatch(ARG_REINTEGRATE_EXCLUDE_FEATURE))
                {
                    string featureName = pair.Value;
                    var calc = PeakFeatureCalculator.Calculators.FirstOrDefault(c =>
                        Equals(featureName, c.HeaderName) || Equals(featureName, c.Name));
                    if (calc == null)
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Attempting_to_exclude_an_unknown_feature_name___0____Try_one_of_the_following_, featureName);
                        foreach (var featureCalculator in PeakFeatureCalculator.Calculators)
                        {
                            if (Equals(featureCalculator.HeaderName, featureCalculator.Name))
                                _out.WriteLine(@"    {0}", featureCalculator.HeaderName);
                            else
                                _out.WriteLine(Resources.CommandArgs_ParseArgsInternal______0__or___1__, featureCalculator.HeaderName, featureCalculator.Name);
                        }
                        return false;
                    }
                    ExcludeFeatures.Add(calc);
                }
                else if (pair.IsMatch(ARG_REPORT_NAME))
                {
                    ReportName = pair.Value;
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_REPORT_FILE))
                {
                    ReportFile = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_REPORT_FORMAT))
                {
                    ReportColumnSeparator = pair.IsValue(ARG_VALUE_TSV)
                        ? TextUtil.SEPARATOR_TSV
                        : TextUtil.CsvSeparator;
                }

                else if (pair.IsMatch(ARG_REPORT_INVARIANT))
                {
                    IsReportInvariant = true;
                }

                // Import a skyr file.
                else if (pair.IsMatch(ARG_REPORT_ADD))
                {
                    ImportingSkyr = true;
                    SkyrPath = pair.Value;
                }

                else if (pair.IsMatch(ARG_REPORT_CONFLICT_RESOLUTION))
                {
                    ResolveSkyrConflictsBySkipping = pair.IsValue(ARG_VALUE_SKIP);
                }

                else if (pair.IsMatch(ARG_CHROMATOGRAM_FILE))
                {
                    ChromatogramsFile = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_CHROMATOGRAM_PRECURSORS))
                {
                    ChromatogramsPrecursors = true;
                }

                else if (pair.IsMatch(ARG_CHROMATOGRAM_PRODUCTS))
                {
                    ChromatogramsProducts = true;
                }

                else if (pair.IsMatch(ARG_CHROMATOGRAM_BASE_PEAKS))
                {
                    ChromatogramsBasePeaks = true;
                }

                else if (pair.IsMatch(ARG_CHROMATOGRAM_TICS))
                {
                    ChromatogramsTics = true;
                }
                else if (pair.IsMatch(ARG_EXP_ISOLATION_LIST_INSTRUMENT))
                {
                    try
                    {
                        IsolationListInstrumentType = pair.Value;
                        RequiresSkylineDocument = true;
                    }
                    catch (ArgumentException)
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Warning__The_instrument_type__0__is_not_valid__Please_choose_from_,
                            pair.Value);
                        foreach (string str in ExportInstrumentType.ISOLATION_LIST_TYPES)
                        {
                            _out.WriteLine(str);
                        }
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_No_isolation_list_will_be_exported_);
                    }
                }
                else if (pair.IsMatch(ARG_EXP_TRANSITION_LIST_INSTRUMENT))
                {
                    try
                    {
                        TransListInstrumentType = pair.Value;
                        RequiresSkylineDocument = true;
                    }
                    catch (ArgumentException)
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Warning__The_instrument_type__0__is_not_valid__Please_choose_from_,
                            pair.Value);
                        foreach (string str in ExportInstrumentType.TRANSITION_LIST_TYPES)
                        {
                            _out.WriteLine(str);
                        }
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_No_transition_list_will_be_exported_);
                    }
                }
                else if (pair.IsMatch(ARG_EXP_METHOD_INSTRUMENT))
                {
                    try
                    {
                        MethodInstrumentType = pair.Value;
                        RequiresSkylineDocument = true;
                    }
                    catch (ArgumentException)
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Warning__The_instrument_type__0__is_not_valid__Please_choose_from_,
                            pair.Value);
                        foreach (string str in ExportInstrumentType.METHOD_TYPES)
                        {
                            _out.WriteLine(str);
                        }
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_No_method_will_be_exported_);
                    }
                }
                else if (pair.IsMatch(ARG_EXP_TEMPLATE))
                {
                    TemplateFile = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }
                else if (pair.IsMatch(ARG_EXP_FILE))
                {
                    ExportPath = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }
                else if (pair.IsMatch(ARG_EXP_POLARITY))
                {
                    ExportPolarityFilter = (ExportPolarity)Enum.Parse(typeof(ExportPolarity), pair.Value, true);
                    RequiresSkylineDocument = true;
                }
                else if (pair.IsMatch(ARG_EXP_STRATEGY))
                {
                    ExportStrategySet = true;
                    RequiresSkylineDocument = true;
                    ExportStrategy = (ExportStrategy)Enum.Parse(typeof(ExportStrategy), pair.Value, true);
                }
                else if (pair.IsMatch(ARG_EXP_METHOD_TYPE))
                {
                    RequiresSkylineDocument = true;
                    ExportMethodType = (ExportMethodType)Enum.Parse(typeof(ExportMethodType), pair.Value, true);
                }
                else if (pair.IsMatch(ARG_EXP_MAX_TRANS))
                {
                    //This one can't be kept within bounds because the bounds depend on the instrument
                    //and the document. 
                    try
                    {
                        MaxTransitionsPerInjection = pair.ValueInt;
                    }
                    catch
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Warning__Invalid_max_transitions_per_injection_parameter___0___, pair.Value);
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_It_must_be_a_number__Defaulting_to__0__, AbstractMassListExporter.MAX_TRANS_PER_INJ_DEFAULT);
                        MaxTransitionsPerInjection = AbstractMassListExporter.MAX_TRANS_PER_INJ_DEFAULT;
                    }
                    RequiresSkylineDocument = true;
                }

                else if (pair.IsMatch(ARG_EXP_OPTIMIZING))
                {
                    try
                    {
                        ExportOptimizeType = pair.Value;
                    }
                    catch (ArgumentException)
                    {
                        _out.WriteLine(
                            Resources.CommandArgs_ParseArgsInternal_Warning__Invalid_optimization_parameter___0____Use__ce____dp___or__none__,
                            pair.Value);
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Defaulting_to_none_);
                    }
                    RequiresSkylineDocument = true;
                }
                else if (pair.IsMatch(ARG_EXP_SCHEDULING_REPLICATE))
                {
                    SchedulingReplicate = pair.Value;
                    RequiresSkylineDocument = true;
                }
                else if (pair.IsMatch(ARG_EXP_IGNORE_PROTEINS))
                {
                    IgnoreProteins = true;
                    RequiresSkylineDocument = true;
                }
                else if (pair.IsMatch(ARG_EXP_PRIMARY_COUNT))
                {
                    try
                    {
                        PrimaryTransitionCount = pair.ValueInt;
                    }
                    catch
                    {
                        _out.WriteLine(
                            Resources.CommandArgs_ParseArgsInternal_Warning__The_primary_transition_count__0__is_invalid__it_must_be_a_number_between__1__and__2__,
                            pair.Value,
                            AbstractMassListExporter.PRIMARY_COUNT_MIN, AbstractMassListExporter.PRIMARY_COUNT_MAX);
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Defaulting_to__0__, AbstractMassListExporter.PRIMARY_COUNT_DEFAULT);
                    }
                    RequiresSkylineDocument = true;
                }
                else if (pair.IsMatch(ARG_EXP_DWELL_TIME))
                {
                    try
                    {
                        DwellTime = pair.ValueInt;
                    }
                    catch
                    {
                        _out.WriteLine(
                            Resources.CommandArgs_ParseArgsInternal_Warning__The_dwell_time__0__is_invalid__it_must_be_a_number_between__1__and__2__,
                            pair.Value,
                            AbstractMassListExporter.DWELL_TIME_MIN, AbstractMassListExporter.DWELL_TIME_MAX);
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Defaulting_to__0__, AbstractMassListExporter.DWELL_TIME_DEFAULT);
                    }
                    RequiresSkylineDocument = true;
                }
                else if (pair.IsMatch(ARG_EXP_ADD_ENERGY_RAMP))
                {
                    AddEnergyRamp = true;
                    RequiresSkylineDocument = true;
                }
                else if (pair.IsMatch(ARG_EXP_USE_S_LENS))
                {
                    UseSlens = true;
                    RequiresSkylineDocument = true;
                }
                else if (pair.IsMatch(ARG_EXP_RUN_LENGTH))
                {
                    try
                    {
                        RunLength = pair.ValueInt;
                    }
                    catch
                    {
                        _out.WriteLine(
                            Resources
                                .CommandArgs_ParseArgsInternal_Warning__The_run_length__0__is_invalid__It_must_be_a_number_between__1__and__2__,
                            pair.Value,
                            AbstractMassListExporter.RUN_LENGTH_MIN, AbstractMassListExporter.RUN_LENGTH_MAX);
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Defaulting_to__0__,
                            AbstractMassListExporter.RUN_LENGTH_DEFAULT);
                    }
                    RequiresSkylineDocument = true;
                }
                else if (pair.IsMatch(ARG_PANORAMA_SERVER))
                {
                    PanoramaServerUri = pair.Value;
                }
                else if (pair.IsMatch(ARG_PANORAMA_USERNAME))
                {
                    PanoramaUserName = pair.Value;
                }
                else if (pair.IsMatch(ARG_PANORAMA_PASSWORD))
                {
                    PanoramaPassword = pair.Value;
                }
                else if (pair.IsMatch(ARG_PANORAMA_FOLDER))
                {
                    PanoramaFolder = pair.Value;
                }
                else if (pair.IsMatch(ARG_SHARE_ZIP))
                {
                    SharingZipFile = true;
                    RequiresSkylineDocument = true;
                    if (!string.IsNullOrEmpty(pair.Value))
                    {
                        SharedFile = pair.Value;
                    }
                }
                else if (pair.IsMatch(ARG_SHARE_TYPE))
                {
                    if (pair.IsValue(ARG_VALUE_SHARE_TYPE_MINIMAL))
                        SharedFileType = ShareType.MINIMAL;
                    else if (pair.IsValue(ARG_VALUE_SHARE_TYPE_COMPLETE))
                        SharedFileType = ShareType.COMPLETE;
                }
                // Unmatched argument
                else
                {
                    _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Unexpected_argument____0_, pair.Name);
                    return false;
                }
            }

            return ValidateArgs();
        }

        private bool ValidateArgs()
        {
            if (Reintegrating)
                RequiresSkylineDocument = true;
            else
            {
                if (IsCreateScoringModel)
                    WarnArgRequirement(ARG_REINTEGRATE_MODEL_NAME, ARG_REINTEGRATE_CREATE_MODEL);
                if (IsOverwritePeaks)
                    WarnArgRequirement(ARG_REINTEGRATE_MODEL_NAME, ARG_REINTEGRATE_OVERWRITE_PEAKS);
            }

            if (FullScanPrecursorResMz.HasValue && !FullScanPrecursorRes.HasValue)
                WarnArgRequirement(ARG_FULL_SCAN_PRECURSOR_RES, ARG_FULL_SCAN_PRECURSOR_RES_MZ);
            if (FullScanProductResMz.HasValue && !FullScanProductRes.HasValue)
                WarnArgRequirement(ARG_FULL_SCAN_PRODUCT_RES, ARG_FULL_SCAN_PRODUCT_RES_MZ);
            if (!IsCreateScoringModel && IsSecondBestModel)
            {
                if (IsDecoyModel)
                    WarnArgRequirement(ARG_REINTEGRATE_CREATE_MODEL, ARG_REINTEGRATE_MODEL_BOTH);
                else
                    WarnArgRequirement(ARG_REINTEGRATE_CREATE_MODEL, ARG_REINTEGRATE_MODEL_SECOND_BEST);
            }

            if (!IsCreateScoringModel && ExcludeFeatures.Count > 0)
            {
                WarnArgRequirement(ARG_REINTEGRATE_CREATE_MODEL, ARG_REINTEGRATE_EXCLUDE_FEATURE);
            }

            if (!AddDecoys && AddDecoysCount.HasValue)
            {
                WarnArgRequirement(ARG_DECOYS_ADD, ARG_DECOYS_ADD_COUNT);
            }

            if (!ImportingDocuments)
            {
                if (DocImportResultsMerge.HasValue)
                {
                    WarnArgRequirement(ARG_IMPORT_DOCUMENT, ARG_IMPORT_DOCUMENT_RESULTS);
                }

                if (DocImportMergePeptides)
                {
                    WarnArgRequirement(ARG_IMPORT_DOCUMENT, ARG_IMPORT_DOCUMENT_MERGE_PEPTIDES);
                }
            }

            if (!ImportingTransitionList)
            {
                if (IsIgnoreTransitionErrors)
                    WarnArgRequirement(ARG_IMPORT_TRANSITION_LIST, ARG_IGNORE_TRANSITION_ERRORS);
            }

            if (!ImportingTransitionList || !IsTransitionListAssayLibrary)
            {
                if (!string.IsNullOrEmpty(IrtGroupName))
                    WarnArgRequirement(ARG_IMPORT_ASSAY_LIBRARY, ARG_IRT_STANDARDS_GROUP_NAME);
                if (!string.IsNullOrEmpty(IrtStandardsPath))
                    WarnArgRequirement(ARG_IMPORT_ASSAY_LIBRARY, ARG_IRT_STANDARDS_FILE);
            }

            if (!string.IsNullOrEmpty(PanoramaServerUri) || !string.IsNullOrEmpty(PanoramaFolder))
            {
                if (!PanoramaArgsComplete())
                {
                    return false;
                }

                var serverUri = PanoramaUtil.ServerNameToUri(PanoramaServerUri);
                if (serverUri == null)
                {
                    _out.WriteLine(Resources.EditServerDlg_OkDialog_The_text__0__is_not_a_valid_server_name_,
                        PanoramaServerUri);
                    return false;
                }

                var panoramaClient = new WebPanoramaClient(serverUri);
                var panoramaHelper = new PanoramaHelper(_out);
                PanoramaServer = panoramaHelper.ValidateServer(panoramaClient, PanoramaUserName, PanoramaPassword);
                if (PanoramaServer == null)
                {
                    return false;
                }

                if (!panoramaHelper.ValidateFolder(panoramaClient, PanoramaServer, PanoramaFolder))
                {
                    return false;
                }

                RequiresSkylineDocument = true;
                PublishingToPanorama = true;
            }

            if (!ImportingSearch)
            {
                if (CutoffScore.HasValue)
                    WarnArgRequirement(ARG_IMPORT_PEPTIDE_SEARCH_FILE, ARG_IMPORT_PEPTIDE_SEARCH_CUTOFF);
                if (AcceptAllModifications)
                    WarnArgRequirement(ARG_IMPORT_PEPTIDE_SEARCH_FILE, ARG_IMPORT_PEPTIDE_SEARCH_MODS);
                if (IncludeAmbiguousMatches)
                    WarnArgRequirement(ARG_IMPORT_PEPTIDE_SEARCH_FILE, ARG_IMPORT_PEPTIDE_SEARCH_AMBIGUOUS);
                if (PreferEmbeddedSpectra.HasValue)
                    WarnArgRequirement(ARG_IMPORT_PEPTIDE_SEARCH_FILE, ARG_IMPORT_PEPTIDE_SEARCH_PREFER_EMBEDDED);
            }

            if (ExportingChromatograms)
            {
                if (!ChromatogramsPrecursors && !ChromatogramsProducts && !ChromatogramsBasePeaks && !ChromatogramsTics)
                    ChromatogramsPrecursors = ChromatogramsProducts = true;
            }
            else
            {
                if (ChromatogramsPrecursors)
                    WarnArgRequirement(ARG_CHROMATOGRAM_FILE, ARG_CHROMATOGRAM_PRECURSORS);
                if (ChromatogramsPrecursors)
                    WarnArgRequirement(ARG_CHROMATOGRAM_FILE, ARG_CHROMATOGRAM_PRECURSORS);
                if (ChromatogramsPrecursors)
                    WarnArgRequirement(ARG_CHROMATOGRAM_FILE, ARG_CHROMATOGRAM_PRECURSORS);
                if (ChromatogramsPrecursors)
                    WarnArgRequirement(ARG_CHROMATOGRAM_FILE, ARG_CHROMATOGRAM_PRECURSORS);
            }

            // If skylineFile isn't set and one of the commands that requires --in is called, complain.
            if (string.IsNullOrEmpty(SkylineFile) && RequiresSkylineDocument && !_isDocumentLoaded)
            {
                _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Use___in_to_specify_a_Skyline_document_to_open_);
                return false;
            }

            if (ImportingReplicateFile && ImportingSourceDirectory)
            {
                _out.WriteLine(Resources
                    .CommandArgs_ParseArgsInternal_Error____import_file_and___import_all_options_cannot_be_used_simultaneously_);
                return false;
            }

            if (ImportingReplicateFile && ImportNamingPattern != null)
            {
                _out.WriteLine(Resources
                    .CommandArgs_ParseArgsInternal_Error____import_naming_pattern_cannot_be_used_with_the___import_file_option_);
                return false;
            }
            // This is now handled by creating a single replicate with the contents of the directory
//            if(ImportingSourceDirectory && !string.IsNullOrEmpty(ReplicateName))
//            {
//                _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error____import_replicate_name_cannot_be_used_with_the___import_all_option_);
//                return false;
//            }

            // Use the original file as the output file, if not told otherwise.
            if (Saving && string.IsNullOrEmpty(SaveFile))
            {
                SaveFile = SkylineFile;
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
                _out.WriteLine(missingArgs.Count > 1
                        ? Resources.CommandArgs_PanoramaArgsComplete_plural_
                        : Resources.CommandArgs_PanoramaArgsComplete_,
                    TextUtil.LineSeparate(missingArgs));
                return false;
            }

            return true;
        }

        public static string WarnArgRequirementText(CommandArg requiredArg, CommandArg usedArg)
        {
            return string.Format(Resources.CommandArgs_WarnArgRequirment_Warning__Use_of_the_argument__0__requires_the_argument__1_,
                usedArg.ArgumentText, requiredArg.ArgumentText);
        }

        private void WarnArgRequirement(CommandArg requiredArg, CommandArg usedArg)
        {
            _out.WriteLine(WarnArgRequirementText(requiredArg, usedArg));
        }

        private static string GetFullPath(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception)
            {
                throw new IOException(string.Format(Resources.CommandArgs_GetFullPath_Failed_attempting_to_get_full_path_for__0_, path));
            }
        }

        public class PanoramaHelper
        {
            private readonly TextWriter _statusWriter;
           
            public PanoramaHelper(TextWriter statusWriter)
            {
                _statusWriter = statusWriter;
            }

            public Uri ValidateServerUri(string panoramaServer)
            {
                // Make sure that the given server URL is valid.
                var serverUri = PanoramaUtil.ServerNameToUri(panoramaServer);
                if (serverUri == null)
                {
                    _statusWriter.WriteLine(Resources.EditServerDlg_OkDialog_The_text__0__is_not_a_valid_server_name_,
                        panoramaServer);
                    return null;
                }
                return serverUri;
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
                    _statusWriter.WriteLine(x.Message); 
                }
                catch (Exception x)
                {
                    _statusWriter.WriteLine(Resources.PanoramaHelper_ValidateServer_, x.Message);
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
                    _statusWriter.WriteLine(x.Message);
                }
                catch (Exception x)
                {
                    _statusWriter.WriteLine(
                        Resources.PanoramaHelper_ValidateFolder_,
                        panoramaFolder, panoramaClient.ServerUri,
                        x.Message);
                }
                return false;
            }
        }
    }
}
