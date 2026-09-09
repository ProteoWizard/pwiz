/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Reader / writer for the ANALYSIS-wide <c>&lt;blib-stem&gt;.1st-pass.retained_base_ids.bin</c>
    /// summary: the COMPLETE set of base_ids the Stage 6 rescore retains, which is the join-wide
    /// first-pass passing set UNION the base_id of every reconciliation action target across
    /// every run.
    ///
    /// <para>Why it exists. Both terms are known only to the planner, and only when planning has
    /// finished. The first term alone is already carried by every run's
    /// <c>reconciliation.json</c> (<c>first_pass_base_ids</c>, identical in all of them), but the
    /// second cannot be: each envelope is written the moment THAT run's planning finishes, so at
    /// that instant the actions of the runs planned later do not exist. Every consumer therefore
    /// had to rebuild the union by reading every run's envelope - a pre-pass over all files that
    /// made <c>--task PerFileRescoring</c> a join task in everything but name, at 10.7 GB of JSON
    /// and 30.8 M actions resident on a 446-run cohort. Written once here, it is a summary a
    /// per-run worker reads in full and holds like the spectral library.</para>
    ///
    /// <para>The set is bounded by the LIBRARY, not by run count: both terms are sets of
    /// base_ids drawn from the same library, so a cohort of any size produces at most one entry
    /// per library precursor. On the 446-run CHS cohort the first term alone is 744,943 ids -
    /// 2.98 MB here against the 6.26 MB of JSON each of the 446 envelopes spends restating
    /// it.</para>
    ///
    /// <para>Layout - little-endian, mirroring <see cref="FdrExperimentSidecar"/>'s header so the
    /// two analysis-wide summaries stay recognizably one family:</para>
    /// <code>
    ///   magic        [0..8]   = b"OSPRYRET"
    ///   version      [8]      = u8 (= 1)
    ///   pass         [9]      = u8 (1 = first-pass)
    ///   reserved     [10..16] = 6 bytes (zero)
    ///   base_id_count[16..24] = u64
    ///   reserved     [24..32] = 8 bytes (zero)
    ///   body         [32..]   = base_id_count * 4 bytes: u32 base_id
    /// </code>
    ///
    /// <para>The magic differs from both sibling sidecars deliberately, for the reason
    /// <see cref="FdrExperimentSidecar"/> gives: a shared header shape means identical magic
    /// would let one file be read as another and decode into plausible garbage rather than a
    /// rejection.</para>
    ///
    /// <para>Base_ids are written in ASCENDING order, so the file is a function of its contents
    /// and not of the order the planner happened to visit runs. The straight-through pipeline
    /// and the distributed task chain plan in different orders and the regression gate compares
    /// their artifacts byte for byte.</para>
    /// </summary>
    public static class RetainedBaseIdSidecar
    {
        // 8-byte magic. ASCII "OSPRYRET".
        private static readonly byte[] Magic =
            { (byte)'O', (byte)'S', (byte)'P', (byte)'R', (byte)'Y', (byte)'R', (byte)'E', (byte)'T' };

        public const byte FormatVersion = 1;
        public const int HeaderLength = 32;
        public const int RecordLength = 4;

        /// <summary>
        /// Path for the analysis-wide retained base_id summary: named after the output blib's
        /// stem, in the SAME directory the per-file sidecars go to. Returns null when the
        /// analysis has no output blib, which is the caller's signal that there is no
        /// analysis-scope artifact to read or write.
        ///
        /// <para>The NAME comes from the blib and the DIRECTORY does not, for exactly the reason
        /// <see cref="FdrExperimentSidecar.PathFor"/> documents at length: every distributed
        /// <c>--task</c> phase runs in its own working directory with the same relative
        /// <c>-o output.blib</c>, so a file placed beside the blib is written by one phase into a
        /// directory the next phase never looks in. Resolving through
        /// <see cref="ArtifactPaths.ResolveOutputDir"/> off <paramref name="siblingArtifactPath"/>
        /// is the identical resolution the per-file sidecars use, so all three artifacts land
        /// together and every phase agrees on where.</para>
        /// </summary>
        /// <param name="outputBlib">The run's output blib; supplies the NAME only.</param>
        /// <param name="siblingArtifactPath">Any input / scores-parquet path of this analysis;
        /// supplies the DIRECTORY, via the same resolution the per-file sidecars use.</param>
        public static string PathFor(string outputBlib, string siblingArtifactPath)
        {
            if (string.IsNullOrEmpty(outputBlib) || string.IsNullOrEmpty(siblingArtifactPath))
                return null;
            string stem = Path.GetFileNameWithoutExtension(outputBlib);
            if (string.IsNullOrEmpty(stem))
                return null;
            string filename = string.Format("{0}.1st-pass.retained_base_ids.bin", stem);
            string parent = ArtifactPaths.ResolveOutputDir(siblingArtifactPath);
            return string.IsNullOrEmpty(parent) ? filename : Path.Combine(parent, filename);
        }

        /// <summary>
        /// Whether <paramref name="path"/> is a retained base_id summary this build can consume:
        /// it exists, carries the expected magic and the current <see cref="FormatVersion"/>, and
        /// its length matches its own header count. Never throws - a missing, short, or foreign
        /// file is simply false. Same contract, and the same reasons for it, as
        /// <see cref="FdrExperimentSidecar.IsCurrentFormat"/>.
        /// </summary>
        public static bool IsCurrentFormat(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return false;
                var info = new FileInfo(path);
                if (!info.Exists || info.Length < HeaderLength)
                    return false;
                var header = new byte[HeaderLength];
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (!ReadFully(fs, header, HeaderLength))
                        return false;
                }
                if (!HeaderOk(header, out ulong headerCount))
                    return false;
                return TryComputeExpectedLen(headerCount, out int expectedLen) &&
                       info.Length == expectedLen;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        /// <summary>
        /// Write the retained base_id summary in ascending base_id order. Atomic via
        /// <see cref="FileSaver"/>: the temp file is promoted on Commit, so a failure leaves any
        /// existing destination untouched.
        /// </summary>
        public static void Write(string path, IReadOnlyCollection<uint> baseIds)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (baseIds == null) throw new ArgumentNullException(nameof(baseIds));

            // Canonical order, so the file is a function of its contents and not of the order
            // the planner walked its runs (the straight-through and distributed routes do not
            // agree on that, and the gate compares their outputs byte for byte).
            var ids = new uint[baseIds.Count];
            int idIdx = 0;
            foreach (uint id in baseIds)
                ids[idIdx++] = id;
            Array.Sort(ids); // Array.Sort OK: distinct base_ids, so the comparison never ties

            string parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            using (var saver = new FileSaver(path))
            {
                using (var fs = new FileStream(saver.SafeName, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(Magic);                                // [0..8]
                    bw.Write(FormatVersion);                        // [8]
                    bw.Write((byte)FdrScoresSidecar.Pass.FirstPass); // [9]
                    bw.Write(new byte[6]);                          // [10..16] reserved
                    bw.Write((ulong)ids.Length);                    // [16..24]
                    bw.Write(new byte[8]);                          // [24..32] reserved

                    foreach (uint id in ids)
                        bw.Write(id);
                }
                saver.Commit();
            }
        }

        /// <summary>
        /// Read the whole summary into a set, or return null on a missing file or any header /
        /// size mismatch. Read in full rather than streamed: this IS the baseline a per-run
        /// worker holds for the whole task, so there is no bounded-consumption variant to offer,
        /// and at 4 bytes per library precursor the whole file is smaller than one run's stubs.
        /// </summary>
        public static HashSet<uint> Read(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!IsCurrentFormat(path))
                return null;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var header = new byte[HeaderLength];
                    if (!ReadFully(fs, header, HeaderLength) || !HeaderOk(header, out ulong count))
                        return null;
                    var result = new HashSet<uint>();
                    var record = new byte[RecordLength];
                    for (ulong i = 0; i < count; i++)
                    {
                        if (!ReadFully(fs, record, RecordLength))
                            return null;
                        result.Add(BitConverter.ToUInt32(record, 0));
                    }
                    return result;
                }
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Validate the 32-byte header: magic, current version, first-pass pass byte. Shared by
        /// every entry point so they cannot drift on what they accept.
        /// </summary>
        private static bool HeaderOk(byte[] header, out ulong headerCount)
        {
            headerCount = 0;
            for (int i = 0; i < Magic.Length; i++)
            {
                if (header[i] != Magic[i])
                    return false;
            }
            if (header[8] != FormatVersion || header[9] != (byte)FdrScoresSidecar.Pass.FirstPass)
                return false;
            headerCount = BitConverter.ToUInt64(header, 16);
            return true;
        }

        /// <summary>
        /// Compute <c>HeaderLength + headerCount * RecordLength</c> with overflow detection, so a
        /// corrupt count cannot wrap int and let the size check pass spuriously.
        /// </summary>
        private static bool TryComputeExpectedLen(ulong headerCount, out int expectedLen)
        {
            try
            {
                expectedLen = checked(HeaderLength + (int)headerCount * RecordLength);
                return true;
            }
            catch (OverflowException)
            {
                expectedLen = 0;
                return false;
            }
        }

        /// <summary>
        /// Fill <paramref name="buffer"/> with exactly <paramref name="count"/> bytes, looping
        /// because one <see cref="Stream.Read(byte[],int,int)"/> may return fewer than asked.
        /// </summary>
        private static bool ReadFully(Stream stream, byte[] buffer, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(buffer, read, count - read);
                if (n <= 0)
                    return false;
                read += n;
            }
            return true;
        }
    }
}
