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
using System.Threading.Tasks;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Writes a loaded spectral library as a BiblioSpec <c>.blib</c> that Skyline reads and Osprey
    /// searches back: one spectrum per library precursor (targets and any decoys the library
    /// supplies) in the form <see cref="BlibSpectrum"/> gives the search output too, with the
    /// library retention time. The search output adds per-run rows; a library has none.
    ///
    /// <para>What survives the round trip: precursor m/z, charge, retention time, fragment m/z
    /// and intensity (both stored at the precision Osprey holds them, though the reader scales
    /// intensities to a base peak of 1, as a DIA-NN library already is), fragment ion types,
    /// and proteins. Modification masses are written with four decimals; a known modification
    /// reads back as its exact mass, and any other one to within 0.00005 Da. Library decoys stay
    /// recognizable by their protein accession prefix, which is how <c>--decoys-in-library</c>
    /// finds them in a blib.</para>
    /// </summary>
    public static class LibraryBlibWriter
    {
        // Spectra prepared in parallel per block, then inserted in order, so memory holds one
        // block of compressed peaks rather than the whole library.
        private const int BLOCK_SIZE = 10000;

        /// <summary>
        /// Write <paramref name="entries"/> to a new blib at <paramref name="path"/>, replacing any
        /// file there only once the new one is complete. <paramref name="librarySource"/> names the
        /// library the entries were loaded from, recorded as the spectrum source.
        /// <paramref name="annotate"/> false leaves out the ion annotations, which is how a
        /// library without them is made for testing. Returns the number of spectra written.
        /// </summary>
        public static int Write(string path, IReadOnlyList<LibraryEntry> entries, string librarySource,
            bool annotate = true, int nThreads = 0)
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = nThreads > 0 ? nThreads : Environment.ProcessorCount
            };
            using (var saver = new FileSaver(path))
            {
                using (var writer = new BlibWriter(saver.SafeName))
                {
                    writer.BeginBatch();
                    string sourceName = Path.GetFileName(librarySource);
                    long fileId = writer.AddSourceFile(Path.GetFullPath(librarySource), sourceName, 0.0);
                    var block = new BlibSpectrum[BLOCK_SIZE];
                    for (int start = 0; start < entries.Count; start += BLOCK_SIZE)
                    {
                        int count = Math.Min(BLOCK_SIZE, entries.Count - start);
                        int blockStart = start;
                        Parallel.For(0, count, parallelOptions,
                            i => block[i] = BlibSpectrum.FromLibraryEntry(entries[blockStart + i], annotate));
                        for (int i = 0; i < count; i++)
                        {
                            double rt = entries[start + i].RetentionTime;
                            writer.AddSpectrum(block[i], rt, rt, rt, 0.0, fileId, 1);
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
    }
}
