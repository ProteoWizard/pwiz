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
using System.IO;
using System.Text;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Where an <see cref="ArtifactStamp"/> lives in Osprey's fixed-record binary sidecars
    /// (the <c>OSPRY*</c> family: per-file and experiment FDR score sidecars, pass-2 competition
    /// decoys, retained base ids). They share one 32-byte header:
    /// <code>
    ///   magic         [0..8]
    ///   format        [8]      u8
    ///   pass          [9]      u8
    ///   reserved      [10..16]
    ///   record count  [16..24] u64
    ///   stamp length  [24..28] u32   (this class)
    ///   reserved      [28..32]
    ///   records       [32..]   count * recordLength bytes
    ///   stamp         [..end]  stamp length bytes of UTF-8 <see cref="ArtifactStamp.ToString"/>
    /// </code>
    /// The stamp follows the records so every record stays at its fixed offset from the header;
    /// a reader that walks records is unchanged, and the file's exact length remains a function
    /// of its header (records plus stamp), which is how a damaged sidecar is still rejected.
    /// </summary>
    public static class BinarySidecarStamp
    {
        public const int HEADER_LENGTH = 32;
        private const int COUNT_OFFSET = 16;
        private const int STAMP_LENGTH_OFFSET = 24;

        /// <summary>The bytes a writer appends after its records.</summary>
        public static byte[] Encode(ArtifactStamp stamp)
        {
            if (stamp == null)
                throw new ArgumentNullException(nameof(stamp));
            return Encoding.UTF8.GetBytes(stamp.ToString());
        }

        /// <summary>
        /// Write header bytes [24..32] - the stamp length and four reserved zero bytes - in place
        /// of the eight reserved bytes the header carried before it held a stamp.
        /// </summary>
        public static void WriteHeaderField(BinaryWriter writer, byte[] stampBytes)
        {
            writer.Write((uint)stampBytes.Length);
            writer.Write(0u);
        }

        /// <summary>The stamp length recorded in a 32-byte header.</summary>
        public static uint ReadLength(byte[] header)
        {
            return BitConverter.ToUInt32(header, STAMP_LENGTH_OFFSET);
        }

        /// <summary>
        /// The record count a 32-byte <paramref name="header"/> declares and the exact file length
        /// that implies - header, records and stamp. False when the count does not fit an
        /// <see cref="int"/>: checked arithmetic, so a corrupt or hostile count is rejected rather
        /// than wrapping and letting a size check pass spuriously.
        /// </summary>
        public static bool TryComputeExpectedLength(byte[] header, int recordLength,
            out int recordCount, out long expectedLength)
        {
            ulong count = BitConverter.ToUInt64(header, COUNT_OFFSET);
            try
            {
                recordCount = checked((int)count);
                expectedLength = checked(HEADER_LENGTH + (long)recordCount * recordLength) + ReadLength(header);
                return true;
            }
            catch (OverflowException)
            {
                recordCount = 0;
                expectedLength = 0;
                return false;
            }
        }

        /// <summary>
        /// Read the stamp of the binary sidecar at <paramref name="path"/>, whose records are
        /// <paramref name="recordLength"/> bytes. Null when the file is missing, unreadable, or its
        /// length disagrees with its header. The caller has already decided which format the file
        /// is; this checks only the layout above, not the magic or format byte.
        /// </summary>
        public static ArtifactStamp TryRead(string path, int recordLength)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length < HEADER_LENGTH)
                        return null;
                    var header = new byte[HEADER_LENGTH];
                    if (!ReadFully(stream, header))
                        return null;
                    uint stampLength = ReadLength(header);
                    if (stampLength == 0 ||
                        !TryComputeExpectedLength(header, recordLength, out _, out long expectedLength) ||
                        stream.Length != expectedLength)
                    {
                        return null;
                    }
                    long stampOffset = expectedLength - stampLength;
                    stream.Seek(stampOffset, SeekOrigin.Begin);
                    var stampBytes = new byte[stampLength];
                    if (!ReadFully(stream, stampBytes))
                        return null;
                    return ArtifactStamp.Parse(Encoding.UTF8.GetString(stampBytes));
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

        private static bool ReadFully(Stream stream, byte[] buffer)
        {
            int read = 0;
            while (read < buffer.Length)
            {
                int n = stream.Read(buffer, read, buffer.Length - read);
                if (n <= 0)
                    return false;
                read += n;
            }
            return true;
        }
    }
}
