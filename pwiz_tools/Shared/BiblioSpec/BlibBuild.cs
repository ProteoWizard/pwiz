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
                    stdinFile.WriteLine(PathEx.RemovePrefix(fileName, dirCommon));

                if (TargetSequences != null)
                {
                    argv.Add("-U");
                    stdinFile.WriteLine();
                    foreach (string targetSequence in TargetSequences)
                        stdinFile.WriteLine(targetSequence);
                }
                stdinFile.Close();
            }
            // ReSharper restore LocalizableElement

            // ReSharper disable LocalizableElement
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
    }
}
