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
    /// <item><c>-ms</c> names runs, or folders of them: a run is an mzML, raw, wiff, wiff2 or lcd
    /// file, or a folder with a <c>.d</c> or <c>.raw</c> extension (Bruker, Waters). A folder
    /// holding no run is an error.</item>
    /// <item>Every <c>-ms</c> run must have an export among all of <c>-i</c>, and the exports of
    /// one blib or folder must all come from one Osprey search (the same
    /// <c>osprey.search_hash</c> and <c>osprey.library_hash</c>); across separate <c>-i</c>
    /// entries a difference is only a warning.</item>
    /// </list>
    /// </summary>
    public static class TrainingExportLocator
    {
        private static readonly string[] RUN_FILE_EXTENSIONS = { @".mzML", @".raw", @".wiff", @".wiff2", @".lcd" };
        private static readonly string[] RUN_FOLDER_EXTENSIONS = { @".d", @".raw" };

        public static TrainingExportSelection Find(string identifications, IReadOnlyList<string> msFiles)
        {
            return Find(identifications, msFiles, OspreyTrainingExport.ReadFooter);
        }

        /// <summary>The run stem an export belongs to: its file name without <c>.training.parquet</c>.</summary>
        public static string RunStem(string exportPath)
        {
            string name = Path.GetFileName(exportPath);
            return name.Substring(0, name.Length - OspreyTrainingExport.FILE_SUFFIX.Length);
        }

        /// <summary><see cref="Find(string, IReadOnlyList{string})"/> with the export footers read by <paramref name="readFooter"/>.</summary>
        internal static TrainingExportSelection Find(string identifications, IReadOnlyList<string> msFiles,
            Func<string, IReadOnlyDictionary<string, string>> readFooter)
        {
            var runs = ExpandRuns(msFiles ?? Array.Empty<string>());
            var stems = new HashSet<string>(runs.Select(r => r.Stem), StringComparer.OrdinalIgnoreCase);
            var inputs = new List<(string Input, string[] Exports)>();
            foreach (string input in identifications.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                IEnumerable<string> exports;
                if (Directory.Exists(input))
                    exports = Directory.GetFiles(input, @"*" + OspreyTrainingExport.FILE_SUFFIX);
                else if (input.EndsWith(OspreyTrainingExport.FILE_SUFFIX, StringComparison.OrdinalIgnoreCase))
                    exports = new[] { RequireFile(input) };
                else
                    exports = BesideBlib(RequireFile(input), runs);
                inputs.Add((input, exports.Where(e => stems.Count == 0 || stems.Contains(RunStem(e)))
                    .OrderBy(p => p, StringComparer.Ordinal).ToArray()));
            }
            var result = inputs.SelectMany(p => p.Exports).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();

            // One check over every input, so runs split between folders are found.
            var missing = stems.Except(result.Select(RunStem), StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            if (missing.Length > 0)
            {
                throw new FileNotFoundException(string.Format(
                    @"No Osprey training export ({0}) for {1} in -i {2} or beside the run. Run Osprey with --training-export.",
                    OspreyTrainingExport.FILE_SUFFIX, string.Join(@", ", missing), identifications));
            }
            if (result.Length == 0)
            {
                throw new FileNotFoundException(string.Format(
                    @"No Osprey training export ({0}) found for -i {1}. Run Osprey with --training-export.",
                    OspreyTrainingExport.FILE_SUFFIX, identifications));
            }
            var warnings = CheckOneSearch(inputs, readFooter);
            var runPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var run in runs)
                runPaths[run.Stem] = run.MetaKey;
            return new TrainingExportSelection(result, runPaths, warnings);
        }

        private static IEnumerable<string> BesideBlib(string blib, IReadOnlyList<Run> runs)
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(blib)) ?? string.Empty;
            if (runs.Count == 0)
                return Directory.GetFiles(folder, @"*" + OspreyTrainingExport.FILE_SUFFIX);
            var found = new List<string>();
            foreach (var run in runs)
            {
                string path = new[] { folder, run.Folder }
                    .Select(candidate => Path.Combine(candidate, run.Stem + OspreyTrainingExport.FILE_SUFFIX))
                    .FirstOrDefault(File.Exists);
                if (path != null)
                    found.Add(path);
            }
            return found;
        }

        /// <summary>
        /// Throws when the exports of one <c>-i</c> input come from different Osprey searches,
        /// and returns a warning for each input whose search differs from the first input's.
        /// </summary>
        private static IReadOnlyList<string> CheckOneSearch(IEnumerable<(string Input, string[] Exports)> inputs,
            Func<string, IReadOnlyDictionary<string, string>> readFooter)
        {
            var warnings = new List<string>();
            string firstExport = null;
            string firstSearch = null;
            foreach (var (input, exports) in inputs)
            {
                if (exports.Length == 0)
                    continue;
                string search = SearchIdentity(readFooter(exports[0]));
                foreach (string other in exports.Skip(1))
                {
                    if (SearchIdentity(readFooter(other)) != search)
                    {
                        throw new InvalidDataException(string.Format(
                            @"{0} and {1} in {2} come from different Osprey searches ({3} or {4} differ); train on one search's exports.",
                            exports[0], other, input, OspreyTrainingExport.SEARCH_HASH_KEY, OspreyTrainingExport.LIBRARY_HASH_KEY));
                    }
                }
                if (firstExport == null)
                {
                    firstExport = exports[0];
                    firstSearch = search;
                }
                else if (search != firstSearch)
                {
                    warnings.Add(string.Format(@"WARNING: {0} and {1} come from different Osprey searches ({2} or {3} differ).",
                        firstExport, exports[0], OspreyTrainingExport.SEARCH_HASH_KEY, OspreyTrainingExport.LIBRARY_HASH_KEY));
                }
            }
            return warnings;
        }

        private static string SearchIdentity(IReadOnlyDictionary<string, string> footer)
        {
            footer.TryGetValue(OspreyTrainingExport.SEARCH_HASH_KEY, out string search);
            footer.TryGetValue(OspreyTrainingExport.LIBRARY_HASH_KEY, out string library);
            return search + @"|" + library;
        }

        /// <summary>
        /// The runs <c>-ms</c> names. A file, or a folder that is itself a run, is one run keyed by
        /// the entry as typed; any other folder names every run in it, keyed by the folder as typed,
        /// a directory separator and the run's file name, as Carafe keys meta.json.
        /// </summary>
        private static IReadOnlyList<Run> ExpandRuns(IReadOnlyList<string> msFiles)
        {
            var runs = new List<Run>();
            foreach (string ms in msFiles)
            {
                string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ms));
                if (Directory.Exists(path) && !IsRunFolder(path))
                {
                    var found = Directory.GetFiles(path).Where(f => HasExtension(f, RUN_FILE_EXTENSIONS))
                        .Concat(Directory.GetDirectories(path).Where(IsRunFolder))
                        .OrderBy(p => p, StringComparer.Ordinal)
                        .Select(p => new Run(Path.GetFileNameWithoutExtension(p), path, ms + Path.DirectorySeparatorChar + Path.GetFileName(p)))
                        .ToArray();
                    if (found.Length == 0)
                    {
                        throw new FileNotFoundException(string.Format(@"-ms {0} holds no MS run (a {1} file, or a {2} folder).",
                            ms, string.Join(@", ", RUN_FILE_EXTENSIONS), string.Join(@" or ", RUN_FOLDER_EXTENSIONS)), ms);
                    }
                    runs.AddRange(found);
                }
                else
                {
                    runs.Add(new Run(Path.GetFileNameWithoutExtension(path), Path.GetDirectoryName(path) ?? string.Empty, ms));
                }
            }
            return runs.GroupBy(r => r.Stem, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToArray();
        }

        private static bool IsRunFolder(string path)
        {
            return Directory.Exists(path) && HasExtension(path, RUN_FOLDER_EXTENSIONS);
        }

        private static bool HasExtension(string path, IEnumerable<string> extensions)
        {
            return extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
        }

        private static string RequireFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(@"File not found: " + path, path);
            return path;
        }

        /// <summary>One run <c>-ms</c> names: its file stem, the folder it is in, and its meta.json key.</summary>
        private sealed class Run
        {
            public Run(string stem, string folder, string metaKey)
            {
                Stem = stem;
                Folder = folder;
                MetaKey = metaKey;
            }

            public string Stem { get; }
            public string Folder { get; }
            public string MetaKey { get; }
        }
    }
}
