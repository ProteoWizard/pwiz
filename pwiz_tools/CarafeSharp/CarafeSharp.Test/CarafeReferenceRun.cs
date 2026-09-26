/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Proteome;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// A Carafe library-prediction output folder used as a parity reference: its
    /// <c>parameter.txt</c> command line, the <c>peptide_forms_k</c> prediction inputs, the
    /// <c>k_ms2_df</c> / <c>k_ms2_mz_df</c> / <c>k_ms2_pred</c> / <c>k_rt_pred</c> files of its
    /// Python, and the library it wrote. A folder from a training run (<c>-ms</c>) is read as
    /// the library prediction that followed training: its models, and the settings the
    /// training left (<see cref="CarafeModelDirectory.ApplyTrainingRunOverrides"/>).
    /// </summary>
    public sealed class CarafeReferenceRun
    {
        private const string COMMAND_LINE_PREFIX = @"Command line: ";

        /// <summary>The run in <paramref name="folder"/>, or null when it has no parameter.txt.</summary>
        public static CarafeReferenceRun Open(string folder)
        {
            string parameters = Path.Combine(folder, @"parameter.txt");
            if (!File.Exists(parameters))
                return null;
            string line = File.ReadLines(parameters).FirstOrDefault(l => l.StartsWith(COMMAND_LINE_PREFIX, StringComparison.Ordinal));
            if (line == null)
                return null;
            return new CarafeReferenceRun(folder, line.Substring(COMMAND_LINE_PREFIX.Length).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList());
        }

        private CarafeReferenceRun(string folder, List<string> args)
        {
            Folder = folder;
            Name = Path.GetFileName(folder);
            IsTraining = args.Contains(@"-ms");
            // The library half of a training run: drop what only training reads.
            RemoveOption(args, @"-ms");
            RemoveOption(args, @"-i");
            RemoveOption(args, @"-tf");
            Settings = CarafeCommandLine.Parse(args).LibrarySettings;
            if (!File.Exists(Settings.Database))
                Settings.Database = Path.Combine(Path.GetDirectoryName(folder) ?? string.Empty, Path.GetFileName(Settings.Database) ?? string.Empty);
            FastaPath = Settings.Database;
            if (IsTraining)
            {
                Settings.ModelDirectory = folder;
                CarafeModelDirectory.Open(folder).ApplyTrainingRunOverrides(Settings);
            }
            Settings.Device = @"cpu";
        }

        public string Folder { get; }

        public string Name { get; }

        /// <summary>True for a folder a training run (<c>-ms</c>) predicted its library into.</summary>
        public bool IsTraining { get; }

        /// <summary>The library settings of the run, with the output folder still Carafe's.</summary>
        public LibrarySettings Settings { get; }

        public string FastaPath { get; }

        /// <summary>The <c>peptide_forms_k.parquet</c> files, k ascending.</summary>
        public IReadOnlyList<string> PeptideFormFiles
        {
            get { return Numbered(@"peptide_forms_", @".parquet"); }
        }

        /// <summary>The prediction batch indexes k with a <c>k_ms2_df.parquet</c>.</summary>
        public IReadOnlyList<int> Batches
        {
            get
            {
                return Directory.GetFiles(Folder, @"*_ms2_df.parquet")
                    .Select(f => Path.GetFileName(f).Split('_')[0])
                    .Select(n => int.TryParse(n, out int k) ? k : -1)
                    .Where(k => k >= 0)
                    .OrderBy(k => k)
                    .ToList();
            }
        }

        public string BatchFile(int batch, string suffix)
        {
            return Path.Combine(Folder, batch + suffix);
        }

        /// <summary>
        /// The library TSV: carafe_spectral_library.tsv, or the TSV with Carafe's header the GUI
        /// renamed it to.
        /// </summary>
        public string LibraryTsv
        {
            get
            {
                string standard = Path.Combine(Folder, CarafeLibraryTsvWriter.FILE_NAME);
                if (File.Exists(standard))
                    return standard;
                return Directory.GetFiles(Folder, @"*.tsv")
                    .Where(f => File.ReadLines(f).FirstOrDefault() == CarafeLibraryTsvWriter.HEADER)
                    .OrderByDescending(f => new FileInfo(f).Length)
                    .FirstOrDefault();
            }
        }

        /// <summary>Carafe's own Skyline .blib, when the run wrote one.</summary>
        public string LibraryBlib
        {
            get
            {
                string path = Path.Combine(Folder, BlibLibraryWriter.FILE_NAME);
                return File.Exists(path) ? path : null;
            }
        }

        /// <summary>A copy of the settings predicting from <paramref name="fasta"/> into <paramref name="outputFolder"/>.</summary>
        public LibrarySettings CreateSettings(string fasta, string outputFolder)
        {
            var settings = Open(Folder).Settings;
            settings.Database = fasta;
            settings.OutputDirectory = outputFolder;
            return settings;
        }

        public override string ToString()
        {
            return Name;
        }

        private List<string> Numbered(string prefix, string suffix)
        {
            return Directory.GetFiles(Folder, prefix + @"*" + suffix)
                .Select(f => new { Path = f, Text = Path.GetFileName(f).Substring(prefix.Length).Replace(suffix, string.Empty) })
                .Where(f => int.TryParse(f.Text, out _))
                .OrderBy(f => int.Parse(f.Text))
                .Select(f => f.Path)
                .ToList();
        }

        private static void RemoveOption(List<string> args, string option)
        {
            int index = args.IndexOf(option);
            if (index < 0)
                return;
            int count = index + 1 < args.Count && !args[index + 1].StartsWith(@"-", StringComparison.Ordinal) ? 2 : 1;
            args.RemoveRange(index, count);
        }
    }
}
