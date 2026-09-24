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

namespace pwiz.CarafeSharp.Training
{
    /// <summary>
    /// Finds the Osprey training exports (<c>&lt;stem&gt;.training.parquet</c>) a training run
    /// reads, from Carafe's <c>-i</c> and <c>-ms</c>:
    /// <list type="bullet">
    /// <item><c>-i</c> names exports or folders of them directly; <c>-ms</c>, when given, picks
    /// the runs among them by file stem.</item>
    /// <item><c>-i</c> names Osprey's result blib, as Carafe's command line does: each
    /// <c>-ms</c> run's export is looked for beside the blib (Osprey's output folder), then beside
    /// the run; without <c>-ms</c>, every export beside the blib.</item>
    /// </list>
    /// </summary>
    public static class TrainingExportLocator
    {
        public static IReadOnlyList<string> Find(string identifications, IReadOnlyList<string> msFiles)
        {
            var runs = ExpandRuns(msFiles ?? Array.Empty<string>());
            var found = new List<string>();
            foreach (string input in identifications.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Directory.Exists(input))
                    found.AddRange(Select(Directory.GetFiles(input, @"*" + OspreyTrainingExport.FILE_SUFFIX), runs, input));
                else if (input.EndsWith(OspreyTrainingExport.FILE_SUFFIX, StringComparison.OrdinalIgnoreCase))
                    found.AddRange(Select(new[] { RequireFile(input) }, runs, input));
                else
                    found.AddRange(BesideBlib(RequireFile(input), runs));
            }
            var result = found.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (result.Length == 0)
            {
                throw new FileNotFoundException(string.Format(
                    @"No Osprey training export ({0}) found for -i {1}. Run Osprey with --training-export.",
                    OspreyTrainingExport.FILE_SUFFIX, identifications));
            }
            return result;
        }

        /// <summary>The run stem an export belongs to: its file name without <c>.training.parquet</c>.</summary>
        public static string RunStem(string exportPath)
        {
            string name = Path.GetFileName(exportPath);
            return name.Substring(0, name.Length - OspreyTrainingExport.FILE_SUFFIX.Length);
        }

        private static IEnumerable<string> BesideBlib(string blib, IReadOnlyList<(string Stem, string Folder)> runs)
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(blib)) ?? string.Empty;
            if (runs.Count == 0)
                return Directory.GetFiles(folder, @"*" + OspreyTrainingExport.FILE_SUFFIX);
            return runs.Select(run =>
            {
                foreach (string candidate in new[] { folder, run.Folder })
                {
                    string path = Path.Combine(candidate, run.Stem + OspreyTrainingExport.FILE_SUFFIX);
                    if (File.Exists(path))
                        return path;
                }
                throw new FileNotFoundException(string.Format(
                    @"No Osprey training export for {0} beside {1} or in {2}. Run Osprey with --training-export.",
                    run.Stem, blib, run.Folder));
            }).ToArray();
        }

        private static IEnumerable<string> Select(IEnumerable<string> exports, IReadOnlyList<(string Stem, string Folder)> runs, string input)
        {
            if (runs.Count == 0)
                return exports;
            var stems = new HashSet<string>(runs.Select(r => r.Stem), StringComparer.OrdinalIgnoreCase);
            var selected = exports.Where(e => stems.Contains(RunStem(e))).ToArray();
            // A folder must hold every named run; a named export file is just one of them.
            var missing = stems.Except(selected.Select(RunStem), StringComparer.OrdinalIgnoreCase).ToArray();
            if (missing.Length > 0 && Directory.Exists(input))
            {
                throw new FileNotFoundException(string.Format(@"No Osprey training export for {0} in {1}.",
                    string.Join(@", ", missing), input));
            }
            return selected;
        }

        /// <summary>The runs <c>-ms</c> names, as (file stem, folder); a folder names every mzML or raw file in it.</summary>
        private static IReadOnlyList<(string Stem, string Folder)> ExpandRuns(IReadOnlyList<string> msFiles)
        {
            var runs = new List<(string, string)>();
            foreach (string ms in msFiles)
            {
                string path = Path.GetFullPath(ms);
                if (Directory.Exists(path))
                {
                    runs.AddRange(Directory.GetFiles(path)
                        .Where(f => f.EndsWith(@".mzML", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(@".raw", StringComparison.OrdinalIgnoreCase))
                        .Select(f => (Path.GetFileNameWithoutExtension(f), path)));
                }
                else
                {
                    runs.Add((Path.GetFileNameWithoutExtension(path), Path.GetDirectoryName(path) ?? string.Empty));
                }
            }
            return runs.Distinct().ToArray();
        }

        private static string RequireFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(@"File not found: " + path, path);
            return path;
        }
    }
}
