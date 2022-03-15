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

    public class BiblioSpecScoreType
    {
        private const string PERCOLATOR_QVALUE = "PERCOLATOR QVALUE";
        private const string PEPTIDE_PROPHET_SOMETHING = "PEPTIDE PROPHET SOMETHING";
        private const string SPECTRUM_MILL = "SPECTRUM MILL";
        private const string IDPICKER_FDR = "IDPICKER FDR";
        private const string MASCOT_IONS_SCORE = "MASCOT IONS SCORE";
        private const string TANDEM_EXPECTATION_VALUE = "TANDEM EXPECTATION VALUE";
        private const string PROTEIN_PILOT_CONFIDENCE = "PROTEIN PILOT CONFIDENCE";
        private const string SCAFFOLD_SOMETHING = "SCAFFOLD SOMETHING";
        private const string WATERS_MSE_PEPTIDE_SCORE = "WATERS MSE PEPTIDE SCORE";
        private const string OMSSA_EXPECTATION_SCORE = "OMSSA EXPECTATION SCORE";
        private const string PROTEIN_PROSPECTOR_EXPECTATION_SCORE = "PROTEIN PROSPECTOR EXPECTATION SCORE";
        private const string SEQUEST_XCORR = "SEQUEST XCORR";
        private const string MAXQUANT_SCORE = "MAXQUANT SCORE";
        private const string MORPHEUS_SCORE = "MORPHEUS SCORE";
        private const string MSGF_SCORE = "MSGF+ SCORE";
        private const string PEAKS_CONFIDENCE_SCORE = "PEAKS CONFIDENCE SCORE";
        private const string BYONIC_SCORE = "BYONIC SCORE";
        private const string PEPTIDE_SHAKER_CONFIDENCE = "PEPTIDE SHAKER CONFIDENCE";
        private const string GENERIC_QVALUE = "GENERIC Q-VALUE";

        private const string PROBABILITY_CORRECT = "PROBABILITY_THAT_IDENTIFICATION_IS_CORRECT";
        private const string PROBABILITY_INCORRECT = "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT";

        public enum EnumProbabilityType { probability_correct, probability_incorrect, not_a_probability }

        public string NameInvariant { get; }
        public EnumProbabilityType ProbabilityType { get; }

        public static BiblioSpecScoreType GenericQValue => new BiblioSpecScoreType(GENERIC_QVALUE, PROBABILITY_INCORRECT);

        public BiblioSpecScoreType(string name, string probabilityType)
        {
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
                    default:
                        switch (ProbabilityType)
                        {
                            case EnumProbabilityType.probability_correct:
                                return 0.95;
                            case EnumProbabilityType.probability_incorrect:
                                return 0.05;
                            default:
                                return 0;
                        }
                }
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
                        return new RangeValues(6, 6);
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
                default:
                    return NameInvariant;
            }
        }

        public bool Equals(BiblioSpecScoreType obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj.NameInvariant, NameInvariant) && Equals(obj.ProbabilityType, ProbabilityType);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == typeof(BiblioSpecScoreType) && Equals((BiblioSpecScoreType)obj);
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

        public bool BuildLibrary(LibraryBuildAction libraryBuildAction, IProgressMonitor progressMonitor, ref IProgressStatus status, out string[] ambiguous)
        {
            return BuildLibrary(libraryBuildAction, progressMonitor, ref status, out _, out _, out ambiguous);
        }

        public bool BuildLibrary(LibraryBuildAction libraryBuildAction, IProgressMonitor progressMonitor, ref IProgressStatus status, out string commandArgs, out string messageLog, out string[] ambiguous)
        {
            // Arguments for BlibBuild
            // ReSharper disable LocalizableElement
            List<string> argv = new List<string> { "-s", "-A", "-H" };  // Read from stdin, get ambiguous match messages, high precision modifications
            if (DebugMode)
            {
                argv.Add("-v"); // Verbose for debugging
                argv.Add("debug");
            }
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
            string dirCommon = PathEx.GetCommonRoot(InputFiles);

            string stdinFilename = Path.GetTempFileName();
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

                File.Delete(stdinFilename);
                if (!isComplete)
                {
                    // If something happened (error or cancel) to end processing, then
                    // get rid of the possibly partial library.
                    File.Delete(OutputPath);
                    File.Delete(OutputPath + EXT_SQLITE_JOURNAL);
                }
            }
            return isComplete;
        }

        public bool GetScoreTypes(IProgressMonitor progressMonitor, ref IProgressStatus status, out string commandArgs, out string messageLog, out Dictionary<string, BiblioSpecScoreType[]> scoreTypes)
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

            bool isComplete;
            scoreTypes = new Dictionary<string, BiblioSpecScoreType[]>();
            messageLog = string.Empty;
            try
            {
                const string scoreTypePrefix = @"SCORETYPE" + "\t";
                var processRunner = new ProcessRunner { MessagePrefix = DebugMode ? string.Empty : scoreTypePrefix };
                processRunner.Run(psiBlibBuilder, null, progressMonitor, ref status);
                isComplete = status.IsComplete;
                if (isComplete)
                {
                    var messages = processRunner.MessageLog();
                    messageLog = string.Join(Environment.NewLine, processRunner.MessageLog());
                    if (DebugMode)
                    {
                        messages = messages.Where(l => l.StartsWith(scoreTypePrefix)).Select(l => l.Substring(scoreTypePrefix.Length));
                    }
                    foreach (var message in messages)
                    {
                        var pieces = message.Split('\t');
                        if (pieces.Length < 3)
                            continue;
                        var file = string.Join(@"\t", pieces.Take(pieces.Length - 2).ToArray());
                        var scoreType = pieces[pieces.Length - 2];
                        var probType = pieces[pieces.Length - 1];
                        scoreTypes[file] = !scoreTypes.TryGetValue(file, out var existing)
                            ? new[] { new BiblioSpecScoreType(scoreType, probType) }
                            : existing.Append(new BiblioSpecScoreType(scoreType, probType)).ToArray();
                    }
                }
            }
            finally
            {
                // Keep a copy of what got sent to BlibBuild for debugging purposes
                commandArgs = psiBlibBuilder.Arguments + Environment.NewLine + string.Join(Environment.NewLine, File.ReadAllLines(stdinFilename));
                File.Delete(stdinFilename);
            }
            return isComplete;
        }
    }
}
