/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Threading.Tasks;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Writes a loaded spectral library as a BiblioSpec <c>.blib</c> that Skyline reads and Osprey
    /// searches back: one spectrum per library precursor (targets and any decoys the library
    /// supplies) in the form <see cref="BlibSpectrum"/> gives the search output too. The search
    /// output adds per-run rows; a library has one <c>RetentionTimes</c> row per spectrum holding
    /// its library retention time, where Skyline reads library retention times from, with no peak
    /// boundaries.
    ///
    /// <para>What survives the round trip: precursor m/z, charge, retention time, fragment m/z
    /// and intensity (both stored at the precision Osprey holds them, though the reader scales
    /// intensities to a base peak of 1, as a DIA-NN library already is), and proteins. Fragment
    /// ion types are not stored - the reader computes them from m/z, as Skyline does. A modification mass is written with at most four decimals; a known
    /// modification reads back as its exact mass. Library decoys are recognized by their protein
    /// accession prefix, which is how <c>--decoys-in-library</c> finds them in a blib, so a decoy
    /// whose accessions carry none of the decoy prefixes (a DIA-NN library flags it in a
    /// <c>Decoy</c> column) is written with the first prefix added.</para>
    /// </summary>
    public static class LibraryBlibWriter
    {
        // Spectra prepared in parallel per block, then inserted in order, so memory holds one
        // block of compressed peaks rather than the whole library.
        private const int BLOCK_SIZE = 10000;

        private const string DEFAULT_DECOY_PREFIX = @"DECOY_";

        /// <summary>
        /// Write <paramref name="entries"/> to a new blib at <paramref name="path"/>, replacing any
        /// file there only once the new one is complete. <paramref name="librarySource"/> names the
        /// library the entries were loaded from, recorded as the spectrum source.
        /// <paramref name="decoyPrefixes"/> are the accession prefixes that mark a library decoy
        /// (a search's defaults when none are given). Returns the number of spectra written.
        /// </summary>
        public static int Write(string path, IReadOnlyList<LibraryEntry> entries, string librarySource,
            int nThreads = 0, IList<string> decoyPrefixes = null)
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = nThreads > 0 ? nThreads : Environment.ProcessorCount
            };
            // No prefixes given: the ones a search recognizes by default.
            var prefixes = decoyPrefixes ?? new OspreyConfig().DecoyPrefixes;
            string decoyPrefix = prefixes.FirstOrDefault(p => !string.IsNullOrEmpty(p)) ?? DEFAULT_DECOY_PREFIX;
            using (var saver = new FileSaver(path))
            {
                using (var writer = new BlibWriter(saver.SafeName))
                {
                    writer.BeginBatch();
                    string sourceName = Path.GetFileName(librarySource);
                    long fileId = writer.AddSourceFile(Path.GetFullPath(librarySource), sourceName, 0.0);
                    var block = new BlibSpectrum[BLOCK_SIZE];
                    using (var progress = new ProgressReporter(
                               string.Format(OspreyIOResources.LibraryBlibWriter_Write_Writing__0__library_precursors_to__1_,
                                   entries.Count, path),
                               entries.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
                    {
                        for (int start = 0; start < entries.Count; start += BLOCK_SIZE)
                        {
                            int count = Math.Min(BLOCK_SIZE, entries.Count - start);
                            int blockStart = start;
                            Parallel.For(0, count, parallelOptions, i =>
                            {
                                var entry = entries[blockStart + i];
                                block[i] = BlibSpectrum.FromLibraryEntry(entry,
                                    DecoyAccessions(entry, prefixes, decoyPrefix));
                            });
                            for (int i = 0; i < count; i++)
                            {
                                double rt = entries[start + i].RetentionTime;
                                long refId = writer.AddSpectrum(block[i], rt, rt, rt, 0.0, fileId, 1);
                                writer.AddRetentionTime(refId, fileId, rt, null, null, 0.0, true);
                            }
                            progress.Report(start + count);
                        }
                    }
                    writer.Commit();
                    writer.AddMetadata(@"osprey_version", OspreyVersion.Current);
                    writer.AddMetadata(@"library_source", sourceName);
                    writer.FinalizeDatabase();
                }
                saver.Commit();
            }
            return entries.Count;
        }

        /// <summary>
        /// The accessions to write for a decoy that no prefix already marks: each one with
        /// <paramref name="decoyPrefix"/> added, or the prefix and the sequence when it has none.
        /// Null (keep the entry's own) for a target or an already-marked decoy.
        /// </summary>
        private static IReadOnlyList<string> DecoyAccessions(LibraryEntry entry, IList<string> prefixes, string decoyPrefix)
        {
            if (!entry.IsDecoy || entry.LooksLikeLibraryDecoy(prefixes))
                return null;
            if (entry.ProteinIds == null || entry.ProteinIds.Count == 0)
                return new[] { decoyPrefix + entry.Sequence };
            return entry.ProteinIds.Select(accession => decoyPrefix + accession).ToArray();
        }
    }
}
