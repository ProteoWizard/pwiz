/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using pwiz.BiblioSpec.Properties;
using pwiz.Common.SystemUtil;

namespace pwiz.BiblioSpec
{
// ReSharper disable InconsistentNaming
    public enum LibraryBuildAction { Create, Append }
// ReSharper restore InconsistentNaming
    public static class LibraryBuildActionExtension
    {
        private static string[] LOCALIZED_VALUES
        {
            get
            {
                return new[]
                {
                    Resources.LibraryBuildActionExtension_LOCALIZED_VALUES_Create,
                    Resources.LibraryBuildActionExtension_LOCALIZED_VALUES_Append
                };
            }
        }
        public static string GetLocalizedString(this LibraryBuildAction val)
        {
            return LOCALIZED_VALUES[(int)val];
        }

        public static LibraryBuildAction GetEnum(string enumValue)
        {
            for (int i = 0; i < LOCALIZED_VALUES.Length; i++)
            {
                if (LOCALIZED_VALUES[i] == enumValue)
                {
                    return (LibraryBuildAction)i;
                }
            }
            throw new ArgumentException(string.Format(Resources.LibraryBuildActionExtension_GetEnum_The_string___0___does_not_match_an_enum_value, enumValue));
        }

        public static LibraryBuildAction GetEnum(string enumValue, LibraryBuildAction defaultValue)
        {
            for (int i = 0; i < LOCALIZED_VALUES.Length; i++)
            {
                if (LOCALIZED_VALUES[i] == enumValue)
                {
                    return (LibraryBuildAction)i;
                }
            }
            return defaultValue;
        }
    }

    public class ScoreType
    {
        // N.B. these should agree with the values in PSM_SCORE_TYPE in pwiz_tools\BiblioSpec\src\BlibUtils.h
        // And if you add something here, don't forget to update ToString() below
        public const string UNKNOWN = "UNKNOWN";
        public const string PERCOLATOR_QVALUE = "PERCOLATOR QVALUE";
        public const string PEPTIDE_PROPHET_SOMETHING = "PEPTIDE PROPHET SOMETHING";
        public const string SPECTRUM_MILL = "SPECTRUM MILL";
        public const string IDPICKER_FDR = "IDPICKER FDR";
        public const string MASCOT_IONS_SCORE = "MASCOT IONS SCORE";
        public const string TANDEM_EXPECTATION_VALUE = "TANDEM EXPECTATION VALUE";
        public const string PROTEIN_PILOT_CONFIDENCE = "PROTEIN PILOT CONFIDENCE";
        public const string SCAFFOLD_SOMETHING = "SCAFFOLD SOMETHING";
        public const string WATERS_MSE_PEPTIDE_SCORE = "WATERS MSE PEPTIDE SCORE";
        public const string OMSSA_EXPECTATION_SCORE = "OMSSA EXPECTATION SCORE";
        public const string PROTEIN_PROSPECTOR_EXPECTATION_SCORE = "PROTEIN PROSPECTOR EXPECTATION SCORE";
        public const string SEQUEST_XCORR = "SEQUEST XCORR";
        public const string MAXQUANT_SCORE = "MAXQUANT SCORE";
        public const string MORPHEUS_SCORE = "MORPHEUS SCORE";
        public const string MSGF_SCORE = "MSGF+ SCORE";
        public const string PEAKS_CONFIDENCE_SCORE = "PEAKS CONFIDENCE SCORE";
        public const string BYONIC_SCORE = "BYONIC SCORE";
        public const string PEPTIDE_SHAKER_CONFIDENCE = "PEPTIDE SHAKER CONFIDENCE";
        public const string GENERIC_QVALUE = "GENERIC Q-VALUE";
        public const string HARDKLOR_IDOTP = "HARDKLOR IDOTP"; // Hardklor "The dot-product score of this feature to the theoretical model." We translate the Hardkloe Cosine Angle Correlation to a Normalized Contrast Angle idotp value

        public static HashSet<string> INVARIANT_NAMES = new HashSet<string>(new[]
        {
            UNKNOWN,
            PERCOLATOR_QVALUE,
            PEPTIDE_PROPHET_SOMETHING,
            SPECTRUM_MILL,
            IDPICKER_FDR,
            MASCOT_IONS_SCORE,
            TANDEM_EXPECTATION_VALUE,
            PROTEIN_PILOT_CONFIDENCE,
            SCAFFOLD_SOMETHING,
            WATERS_MSE_PEPTIDE_SCORE,
            OMSSA_EXPECTATION_SCORE,
            PROTEIN_PROSPECTOR_EXPECTATION_SCORE,
            SEQUEST_XCORR,
            MAXQUANT_SCORE,
            MORPHEUS_SCORE,
            MSGF_SCORE,
            PEAKS_CONFIDENCE_SCORE,
            BYONIC_SCORE,
            PEPTIDE_SHAKER_CONFIDENCE,
            GENERIC_QVALUE,
            HARDKLOR_IDOTP
        });

        public const string PROBABILITY_CORRECT = "PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT";
        public const string PROBABILITY_INCORRECT = "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT";
        public const string NOT_PROBABILITY = "NOT_A_PROBABILITY_VALUE";

        public enum EnumProbabilityType { probability_correct, probability_incorrect, not_a_probability }

        public string NameInvariant { get; }
        public EnumProbabilityType ProbabilityType { get; }

        public static ScoreType GenericQValue => new ScoreType(GENERIC_QVALUE, PROBABILITY_INCORRECT);
        public static ScoreType HardklorIdotp => new ScoreType(HARDKLOR_IDOTP, PROBABILITY_CORRECT);

        public ScoreType(string name, string probabilityType)
        {
            if (!INVARIANT_NAMES.Contains(name))
                throw new ArgumentException($@"Invalid ScoreType name '{name}'");

            NameInvariant = name;
            switch (probabilityType)
            {
                case PROBABILITY_CORRECT:
                    ProbabilityType = EnumProbabilityType.probability_correct;
                    break;
                case PROBABILITY_INCORRECT:
                    ProbabilityType = EnumProbabilityType.probability_incorrect;
                    break;
                default:
                    ProbabilityType = EnumProbabilityType.not_a_probability;
                    break;
            }
        }

        public bool CanSet => !ValidRange.Min.Equals(ValidRange.Max);

        public double DefaultValue
        {
            get
            {
                switch (NameInvariant)
                {
                    case SPECTRUM_MILL:
                    case IDPICKER_FDR:
                        return 0;
                    case WATERS_MSE_PEPTIDE_SCORE:
                        return 6;
                    case PERCOLATOR_QVALUE:
                    case GENERIC_QVALUE:
                        return 0.01;    // %1 FDR is the standard in the field for searches
                }
                switch (ProbabilityType)
                {
                    case EnumProbabilityType.probability_correct:
                        return 0.95;
                    case EnumProbabilityType.probability_incorrect:
                        return 0.05;
                }
                return 0;
            }
        }

        public struct RangeValues
        {
            public double? Min { get; }
            public double? Max { get; }

            public RangeValues(double? min, double? max)
            {
                Min = min;
                Max = max;
            }
        };

        public RangeValues ValidRange
        {
            get
            {
                switch (NameInvariant)
                {
                    case SPECTRUM_MILL:
                    case IDPICKER_FDR:
                        return new RangeValues(0, 0);
                    case WATERS_MSE_PEPTIDE_SCORE:
                        // "peptide.score" values in "final_fragment.csv" files seem to 
                        // always be less than 10.
                        return new RangeValues(0, 10);
                    default:
                        return new RangeValues(0, 1);
                }
            }
        }

        public RangeValues SuggestedRange
        {
            get
            {
                switch (ProbabilityType)
                {
                    case EnumProbabilityType.probability_correct:
                        return new RangeValues(0.7, 1.0);
                    case EnumProbabilityType.probability_incorrect:
                        return new RangeValues(0.0, 0.3);
                    default:
                        return new RangeValues(null, null);
                }
            }
        }

        public string ThresholdDescription
        {
            get
            {
                switch (ProbabilityType)
                {
                    case EnumProbabilityType.probability_correct:
                        return Resources.ScoreType_ScoreThresholdDescription_Score_threshold_minimum__score_is_probability_that_identification_is_correct_;
                    case EnumProbabilityType.probability_incorrect:
                        return Resources.ScoreType_ScoreThresholdDescription_Score_threshold_maximum__score_is_probability_that_identification_is_incorrect_;
                    default:
                        return null;
                }
            }
        }

        public string ProbabilityTypeDescription
        {
            get
            {
                switch (ProbabilityType)
                {
                    case EnumProbabilityType.probability_correct:
                        return PROBABILITY_CORRECT;
                    case EnumProbabilityType.probability_incorrect:
                        return PROBABILITY_INCORRECT;
                    default:
                        return NOT_PROBABILITY;
                }
            }
        }

        public override string ToString()
        {
            switch (NameInvariant)
            {
                case PERCOLATOR_QVALUE:
                    return Resources.BiblioSpecScoreType_DisplayName_Percolator_q_value;
                case PEPTIDE_PROPHET_SOMETHING:
                    return Resources.BiblioSpecScoreType_DisplayName_PeptideProphet_confidence;
                case SPECTRUM_MILL:
                    return Resources.BiblioSpecScoreType_DisplayName_Spectrum_Mill;
                case IDPICKER_FDR:
                    return Resources.BiblioSpecScoreType_DisplayName_IDPicker_FDR;
                case MASCOT_IONS_SCORE:
                    return Resources.BiblioSpecScoreType_DisplayName_Mascot_expectation;
                case TANDEM_EXPECTATION_VALUE:
                    return Resources.BiblioSpecScoreType_DisplayName_X__Tandem_expectation;
                case PROTEIN_PILOT_CONFIDENCE:
                    return Resources.BiblioSpecScoreType_DisplayName_ProteinPilot_confidence;
                case SCAFFOLD_SOMETHING:
                    return Resources.BiblioSpecScoreType_DisplayName_Scaffold_confidence;
                case WATERS_MSE_PEPTIDE_SCORE:
                    return Resources.BiblioSpecScoreType_DisplayName_Waters_MSE_peptide_score;
                case OMSSA_EXPECTATION_SCORE:
                    return Resources.BiblioSpecScoreType_DisplayName_OMSSA_expectation;
                case PROTEIN_PROSPECTOR_EXPECTATION_SCORE:
                    return Resources.BiblioSpecScoreType_DisplayName_ProteinProspector_expectation;
                case MAXQUANT_SCORE:
                    return Resources.BiblioSpecScoreType_DisplayName_MaxQuant_PEP;
                case MORPHEUS_SCORE:
                    return Resources.BiblioSpecScoreType_DisplayName_Morpheus_q_value;
                case MSGF_SCORE:
                    return Resources.BiblioSpecScoreType_DisplayName_MSGF__q_value;
                case PEAKS_CONFIDENCE_SCORE:
                    return Resources.BiblioSpecScoreType_DisplayName_PEAKS_confidence;
                case BYONIC_SCORE:
                    return Resources.BiblioSpecScoreType_DisplayName_Byonic_PEP;
                case PEPTIDE_SHAKER_CONFIDENCE:
                    return Resources.BiblioSpecScoreType_DisplayName_PeptideShaker_confidence;
                case SEQUEST_XCORR:
                case GENERIC_QVALUE:
                    return Resources.BiblioSpecScoreType_DisplayName_q_value;
                case HARDKLOR_IDOTP:
                    return Resources.BiblioSpecScoreType_DisplayName_Hardklor_idotp;
                default:
                    return NameInvariant;
            }
        }

        public bool Equals(ScoreType obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj.NameInvariant, NameInvariant) && Equals(obj.ProbabilityType, ProbabilityType);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == typeof(ScoreType) && Equals((ScoreType)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = NameInvariant?.GetHashCode() ?? 0;
                result = (result * 397) ^ ProbabilityType.GetHashCode();
                return result;
            }
        }
    }

    public class ScoreTypesResult
    {
        public ScoreType[] ScoreTypes { get; }
        public int NumScoreTypes => ScoreTypes?.Length ?? 0;

        public string[] Errors { get; }
        public bool HasError => Errors?.Length > 0;

        public ScoreTypesResult(IEnumerable<ScoreType> scoreTypes, IEnumerable<string> errors)
        {
            ScoreTypes = scoreTypes.ToArray();
            Errors = errors.ToArray();
        }
    }

    public sealed class BlibBuild
    {
        private const string EXE_BLIB_BUILD = "BlibBuild";
        public const string EXT_SQLITE_JOURNAL = "-journal";

        private ReadOnlyCollection<string> _inputFiles;

        public BlibBuild(string outputPath, IList<string> inputFiles, IList<string> targetSequences = null)
        {
            OutputPath = outputPath;
            InputFiles = inputFiles;
            TargetSequences = targetSequences;
        }

        public string OutputPath { get; private set; }
        public string Id { get;set; }
        public double? CutOffScore { get; set; }
        public string Authority { get; set; }
        public int? CompressLevel { get; set; }
        public bool IncludeAmbiguousMatches { get; set; }
        public bool? PreferEmbeddedSpectra { get; set; }
        public bool DebugMode { get; set; }

        public IList<string> InputFiles
        {
            get { return _inputFiles; }
            private set { _inputFiles = value as ReadOnlyCollection<string> ?? new ReadOnlyCollection<string>(value); }
        }

        public Dictionary<string, double> ScoreThresholdsByFile { get; set; }

        public IList<string> TargetSequences { get; private set; }
        public IList<int> Charges { get; set; } // Optional list of charges, if non-empty passed to BlibBuild's -z option

        public bool BuildLibrary(LibraryBuildAction libraryBuildAction, IProgressMonitor progressMonitor, ref IProgressStatus status, out string[] ambiguous)
        {
            return BuildLibrary(libraryBuildAction, progressMonitor, ref status, out _, out _, out ambiguous);
        }

        public bool BuildLibrary(LibraryBuildAction libraryBuildAction, IProgressMonitor progressMonitor, ref IProgressStatus status, out string commandArgs, out string messageLog, out string[] ambiguous)
        {
            // Arguments for BlibBuild
            // ReSharper disable LocalizableElement
            List<string> argv = new List<string> { "-s", "-A", "-H" };  // Read from stdin, get ambiguous match messages, high precision modifications

            argv.Add("-v");
            // Verbose for debugging
            if (DebugMode)
                argv.Add("debug");
            else
                argv.Add("warn");

            if (libraryBuildAction == LibraryBuildAction.Create)
                argv.Add("-o");
            if (CutOffScore.HasValue)
            {
                argv.Add("-c");
                argv.Add(CutOffScore.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (CompressLevel.HasValue)
            {
                argv.Add("-l");
                argv.Add(CompressLevel.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (!string.IsNullOrEmpty(Authority))
            {
                argv.Add("-a");
                argv.Add(Authority);
            }
            if (!string.IsNullOrEmpty(Id))
            {
                argv.Add("-i");
                argv.Add(Id);
            }
            if (IncludeAmbiguousMatches)
            {
                argv.Add("-K");
            }
            if (PreferEmbeddedSpectra == true)
            {
                argv.Add("-E");
            }
            if (Charges != null && Charges.Any())
            {
                argv.Add("-z");
                argv.Add(string.Join(@",", Charges)); // Process PSMs with these charges, ignoring others
            }
            string dirCommon = PathEx.GetCommonRoot(InputFiles);

            string stdinFilename = Path.Combine(Path.GetDirectoryName(OutputPath) ?? string.Empty, Path.GetFileNameWithoutExtension(OutputPath) + $"{DateTime.Now.ToString("yyyyMMddhhmm")}.stdin.txt");
            argv.Add($"-S \"{stdinFilename}\"");
            using (var stdinFile = new StreamWriter(stdinFilename, false, new UTF8Encoding(false)))
            {
                foreach (string fileName in InputFiles)
                {
                    var line = PathEx.RemovePrefix(fileName, dirCommon);
                    if (ScoreThresholdsByFile != null && ScoreThresholdsByFile.TryGetValue(fileName, out var threshold))
                        line += @" score_threshold=" + threshold.ToString(CultureInfo.InvariantCulture);
                    stdinFile.WriteLine(line);
                }

                if (TargetSequences != null)
                {
                    argv.Add("-U");
                    stdinFile.WriteLine();
                    foreach (string targetSequence in TargetSequences)
                        stdinFile.WriteLine(targetSequence);
                }
                stdinFile.Close();
            }
            argv.Add("\"" + OutputPath + "\"");
            // ReSharper restore LocalizableElement

            var psiBlibBuilder = new ProcessStartInfo(EXE_BLIB_BUILD)
                                     {
                                         CreateNoWindow = true,
                                         UseShellExecute = false,
                                         // Common directory includes the directory separator
                                         WorkingDirectory = dirCommon.Substring(0, dirCommon.Length - 1),
                                         Arguments = string.Join(@" ", argv.ToArray()),
                                         RedirectStandardOutput = true,
                                         RedirectStandardError = true,
                                         RedirectStandardInput = false,
                                         StandardOutputEncoding = Encoding.UTF8,
                                         StandardErrorEncoding = Encoding.UTF8
                                     };

            bool isComplete = false;
            ambiguous = new string[0];
            messageLog = string.Empty;
            try
            {
                const string ambiguousPrefix = @"AMBIGUOUS:";
                var processRunner = new ProcessRunner { MessagePrefix = DebugMode ? string.Empty : ambiguousPrefix };
                processRunner.Run(psiBlibBuilder, null, progressMonitor, ref status);
                isComplete = status.IsComplete;
                if (isComplete)
                {
                    var messages = processRunner.MessageLog();
                    messageLog = string.Join(Environment.NewLine, processRunner.MessageLog());
                    if (DebugMode)
                    {
                        messages = messages.Where(l => l.StartsWith(ambiguousPrefix))
                            .Select(l => l.Substring(ambiguousPrefix.Length));
                    }
                    ambiguous = messages.Distinct().OrderBy(s => s).ToArray();
                }
            }
            finally 
            {
                // Keep a copy of what got sent to BlibBuild for debugging purposes
                commandArgs = psiBlibBuilder.Arguments + Environment.NewLine + string.Join(Environment.NewLine, File.ReadAllLines(stdinFilename));

                if (!isComplete)
                {
                    // If something happened (error or cancel) to end processing, then
                    // get rid of the possibly partial library.
                    if (OutputPath != null)
                    {
                        File.Delete(OutputPath);
                        File.Delete(OutputPath + EXT_SQLITE_JOURNAL);
                    }
                }
                else
                {
                    // keep the stdin file if an error occurred
                    File.Delete(stdinFilename);
                }
            }
            return isComplete;
        }

        public Dictionary<string, ScoreTypesResult> GetScoreTypes(IProgressMonitor progressMonitor, ref IProgressStatus status, out string commandArgs)
        {
            // Arguments for BlibBuild
            // ReSharper disable LocalizableElement
            var argv = new List<string> { "-s", "-t" };  // Read from stdin, only output score types
            if (DebugMode)
            {
                argv.Add("-v"); // Verbose for debugging
                argv.Add("debug");
            }
            string dirCommon = PathEx.GetCommonRoot(InputFiles);

            string stdinFilename = Path.GetTempFileName();
            argv.Add($"-S \"{stdinFilename}\"");
            using (var stdinFile = new StreamWriter(stdinFilename, false, new UTF8Encoding(false)))
            {
                foreach (string fileName in InputFiles)
                    stdinFile.WriteLine(PathEx.RemovePrefix(fileName, dirCommon));
                stdinFile.Close();
            }
            // ReSharper restore LocalizableElement

            var psiBlibBuilder = new ProcessStartInfo(EXE_BLIB_BUILD)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                // Common directory includes the directory separator
                WorkingDirectory = dirCommon.Substring(0, dirCommon.Length - 1),
                Arguments = string.Join(@" ", argv.ToArray()),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var text = new StringWriter();
            try
            {
                var processRunner = new ProcessRunner();
                processRunner.Run(psiBlibBuilder, null, progressMonitor, ref status, text);
            }
            finally
            {
                // Keep a copy of what got sent to BlibBuild for debugging purposes
                commandArgs = psiBlibBuilder.Arguments + Environment.NewLine + string.Join(Environment.NewLine, File.ReadAllLines(stdinFilename));
                File.Delete(stdinFilename);
            }

            if (!status.IsComplete)
                return null;

            var result = new Dictionary<string, ScoreTypesResult>();
            using (var reader = new StringReader(text.ToString()))
            {
                const string errorPrefix = @"ERROR:";

                string curFile = null;
                var curScoreTypes = new List<ScoreType>();
                var curErrors = new List<string>();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        if (curFile != null)
                            result[curFile] = new ScoreTypesResult(curScoreTypes, curErrors);

                        curFile = null;
                        curScoreTypes.Clear();
                        curErrors.Clear();
                    }
                    else if (curFile == null)
                    {
                        curFile = line;
                    }
                    else if (!line.StartsWith(errorPrefix))
                    {
                        var pieces = line.Split(new[] { '\t' }, 2);
                        if (pieces.Length == 2)
                            curScoreTypes.Add(new ScoreType(pieces[0], pieces[1]));
                    }
                    else
                    {
                        curErrors.Add(line.Substring(errorPrefix.Length).Trim());
                    }
                }

                if (curFile != null)
                    result[curFile] = new ScoreTypesResult(curScoreTypes, curErrors);
            }
            return result;
        }
    }
}
