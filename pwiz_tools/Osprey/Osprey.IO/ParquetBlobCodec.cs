/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Buffers.Binary;
using System.IO;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// The little-endian typed-array blobs Osprey's parquet files carry in <c>byte[]</c>
    /// columns: no length prefix (the element count is <c>bytes / sizeof(element)</c>), and a
    /// NULL cell - never a zero-length blob - for an empty array. The f64/f32 pair moved here
    /// unchanged from <see cref="ParquetScoreCache"/> so the training export
    /// (<see cref="TrainingExportParquet"/>) writes its arrays exactly as the scores parquet
    /// does; the integer forms follow the same two rules.
    /// </summary>
    internal static class ParquetBlobCodec
    {
        /// <summary>
        /// Encode an array of f64 values as a little-endian byte blob with
        /// no length prefix - bytes / 8 recovers the count on read. Mirrors
        /// Rust pipeline.rs:1620-1623 (`v.to_le_bytes().flat_map(...)`)
        /// byte-for-byte for a non-empty input. A null or empty input encodes
        /// as a NULL cell (the column is declared nullable). A zero-length blob
        /// would instead leave a whole row group's column zero-length whenever
        /// every row in that group is empty -- reachable once the file is
        /// written in bounded row groups (e.g. a group that falls entirely in
        /// the contiguous decoy region, where reference XICs can be absent) --
        /// which the Parquet reader cannot decode (an all-zero-length page
        /// overruns on the length prefix). A null cell reads back as an empty
        /// array (DecodeF64Blob(null) == empty), so the decoded value is
        /// unchanged and no gate is affected (the regression + cross-impl gates
        /// compare the blib + protein-FDR, never parquet bytes). This is the
        /// "more parquet-idiomatic" form the cwt_candidates TODO in ParquetScoreCache anticipates.
        /// </summary>
        internal static byte[] EncodeF64Blob(double[] values)
        {
            if (values == null || values.Length == 0)
                return null;
            var buf = new byte[values.Length * 8];
            for (int i = 0; i < values.Length; i++)
            {
                long bits = BitConverter.DoubleToInt64Bits(values[i]);
                BinaryPrimitives.WriteInt64LittleEndian(
                    new Span<byte>(buf, i * 8, 8), bits);
            }
            return buf;
        }

        /// <summary>
        /// Encode an array of f32 values as a little-endian byte blob with
        /// no length prefix - bytes / 4 recovers the count on read. Mirrors
        /// Rust pipeline.rs:1626-1631 byte-for-byte. Used for
        /// `fragment_intensities` (f32 in both impls). Uses a single
        /// <see cref="Buffer.BlockCopy"/> over the underlying float[] storage
        /// (allocation-free per element); the IEEE-754 little-endian byte
        /// layout matches the Rust blob exactly on LE hosts (x64/x86 - both
        /// pwiz target archs are LE), avoiding net472's missing
        /// <c>BitConverter.SingleToInt32Bits</c>. A null or empty input encodes
        /// as a NULL cell (not a zero-length blob) for the same reason as
        /// <see cref="EncodeF64Blob"/> -- see that method for the rationale.
        /// </summary>
        internal static byte[] EncodeF32Blob(float[] values)
        {
            if (values == null || values.Length == 0)
                return null;
            var buf = new byte[values.Length * 4];
            Buffer.BlockCopy(values, 0, buf, 0, buf.Length);
            return buf;
        }

        /// <summary>
        /// Inverse of <see cref="EncodeF64Blob"/>. Returns an empty array
        /// for null or empty input (preserves <see cref="EncodeF64Blob"/>'s
        /// invariant). Throws if the byte length is not a multiple of 8.
        /// </summary>
        internal static double[] DecodeF64Blob(byte[] blob)
        {
            if (blob == null || blob.Length == 0)
                return Array.Empty<double>();
            if (blob.Length % 8 != 0)
                throw new InvalidDataException(string.Format(
                    "f64 blob length {0} is not a multiple of 8", blob.Length));
            int n = blob.Length / 8;
            var values = new double[n];
            for (int i = 0; i < n; i++)
            {
                long bits = BinaryPrimitives.ReadInt64LittleEndian(
                    new ReadOnlySpan<byte>(blob, i * 8, 8));
                values[i] = BitConverter.Int64BitsToDouble(bits);
            }
            return values;
        }

        /// <summary>
        /// Inverse of <see cref="EncodeF32Blob"/>. Returns an empty array
        /// for null or empty input. Throws if the byte length is not a
        /// multiple of 4.
        /// </summary>
        internal static float[] DecodeF32Blob(byte[] blob)
        {
            if (blob == null || blob.Length == 0)
                return Array.Empty<float>();
            if (blob.Length % 4 != 0)
                throw new InvalidDataException(string.Format(
                    "f32 blob length {0} is not a multiple of 4", blob.Length));
            int n = blob.Length / 4;
            var values = new float[n];
            Buffer.BlockCopy(blob, 0, values, 0, blob.Length);
            return values;
        }

        /// <summary>Little-endian i32 blob; NULL for an empty or null array.</summary>
        internal static byte[] EncodeI32Blob(int[] values)
        {
            if (values == null || values.Length == 0)
                return null;
            var buf = new byte[values.Length * 4];
            for (int i = 0; i < values.Length; i++)
                BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(buf, i * 4, 4), values[i]);
            return buf;
        }

        /// <summary>Inverse of <see cref="EncodeI32Blob"/>; empty for null.</summary>
        internal static int[] DecodeI32Blob(byte[] blob)
        {
            if (blob == null || blob.Length == 0)
                return Array.Empty<int>();
            if (blob.Length % 4 != 0)
            {
                throw new InvalidDataException(string.Format(
                    "i32 blob length {0} is not a multiple of 4", blob.Length));
            }
            var values = new int[blob.Length / 4];
            for (int i = 0; i < values.Length; i++)
                values[i] = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(blob, i * 4, 4));
            return values;
        }

        /// <summary>Little-endian u16 blob; NULL for an empty or null array.</summary>
        internal static byte[] EncodeU16Blob(ushort[] values)
        {
            if (values == null || values.Length == 0)
                return null;
            var buf = new byte[values.Length * 2];
            for (int i = 0; i < values.Length; i++)
                BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(buf, i * 2, 2), values[i]);
            return buf;
        }

        /// <summary>Inverse of <see cref="EncodeU16Blob"/>; empty for null.</summary>
        internal static ushort[] DecodeU16Blob(byte[] blob)
        {
            if (blob == null || blob.Length == 0)
                return Array.Empty<ushort>();
            if (blob.Length % 2 != 0)
            {
                throw new InvalidDataException(string.Format(
                    "u16 blob length {0} is not a multiple of 2", blob.Length));
            }
            var values = new ushort[blob.Length / 2];
            for (int i = 0; i < values.Length; i++)
                values[i] = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(blob, i * 2, 2));
            return values;
        }

        /// <summary>A u8 array is its own blob; NULL for an empty or null array.</summary>
        internal static byte[] EncodeU8Blob(byte[] values)
        {
            return values == null || values.Length == 0 ? null : (byte[])values.Clone();
        }

        /// <summary>Inverse of <see cref="EncodeU8Blob"/>; empty for null.</summary>
        internal static byte[] DecodeU8Blob(byte[] blob)
        {
            return blob == null || blob.Length == 0 ? Array.Empty<byte>() : (byte[])blob.Clone();
        }
    }
}
