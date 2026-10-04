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
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Parquet.Meta;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// A read-only file stream that reads from disk in few, large reads and serves the caller's
    /// smaller reads from memory, optionally taking a process-wide lock for each disk read.
    ///
    /// <para>For readers that walk many files from a cold spinning disk. A parquet walk reads
    /// each needed column chunk separately, and a score file holds dozens of row groups, so one
    /// walk is hundreds of small reads per file. Told each row group's column-chunk extents
    /// (<see cref="BeginRowGroup"/>), the stream learns which chunks the reader touches - readers
    /// ask for the same columns in every row group - and on a miss reads the whole span of
    /// touched chunks around the one asked for, in one read. Untouched chunks inside the span
    /// are read through when the gap is small and split the span when it is not. Without
    /// row-group extents (a sequential file such as a sidecar) it reads fixed-size blocks.</para>
    ///
    /// <para>With the gate, concurrent file lanes take turns at the disk - each holding it for
    /// one read - and decode concurrently, instead of interleaving their reads so the disk seeks
    /// between files.</para>
    ///
    /// <para>Bytes read are the file's bytes, so readers decode exactly what they decode from a
    /// <see cref="FileStream"/>.</para>
    /// </summary>
    public sealed class BlockReadStream : Stream
    {
        private static readonly object DISK_LOCK = new object();
        // An untouched stretch up to this long is read through rather than splitting the read:
        // on a spinning disk a seek costs about as much as reading a megabyte.
        private const long MAX_GAP_BYTES = 1L << 20;
        private const long MAX_PLANNED_READ_BYTES = 64L << 20;

        /// <summary>
        /// Opens <paramref name="path"/> for reading: a <see cref="BlockReadStream"/> when
        /// <see cref="OspreyEnvironment.BlockReadMb"/> is set, otherwise a plain
        /// <see cref="FileStream"/>, exactly as before.
        /// </summary>
        public static Stream OpenRead(string path)
        {
            int blockMb = OspreyEnvironment.BlockReadMb;
            if (blockMb <= 0)
                return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new BlockReadStream(path, blockMb * 1024 * 1024, OspreyEnvironment.BlockReadGate);
        }

        /// <summary>
        /// A <see cref="BlockReadStream"/> with an explicit block size and gate, independent of
        /// the process environment - for tests, which cannot change the environment settings.
        /// </summary>
        internal static BlockReadStream Open(string path, int blockBytes, bool gate)
        {
            return new BlockReadStream(path, blockBytes, gate);
        }

        private readonly FileStream _file;
        private readonly long _length;
        private readonly bool _gate;
        private readonly int _blockSize;
        private byte[] _block;
        private long _blockStart;
        private int _blockCount;
        private long _position;
        // The current row group's column-chunk extents, by column index, and the column indices
        // in file order. Null until a row group begins.
        private long[] _chunkStart;
        private long[] _chunkEnd;
        private int[] _fileOrder;
        // Columns the reader has touched in any row group of this file.
        private bool[] _touched;

        private BlockReadStream(string path, int blockSize, bool gate)
        {
            // Buffer size 1 turns off FileStream's own buffer: each disk read goes straight
            // into the block.
            _file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            _length = _file.Length;
            _gate = gate;
            _blockSize = (int)Math.Min(blockSize, Math.Max(1, _length));
            _block = new byte[_blockSize];
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => _position = value;
        }

        /// <summary>
        /// Tells the stream the column-chunk extents of the row group the reader is about to
        /// read, so its misses read planned spans instead of fixed blocks.
        /// </summary>
        public void BeginRowGroup(List<ColumnChunk> columns)
        {
            int n = columns.Count;
            if (_touched == null || _touched.Length != n)
                _touched = new bool[n];
            _chunkStart = new long[n];
            _chunkEnd = new long[n];
            for (int c = 0; c < n; c++)
            {
                var meta = columns[c].MetaData;
                long start = meta.DataPageOffset;
                long? dictionary = meta.DictionaryPageOffset;
                if (dictionary.HasValue && dictionary.Value > 0 && dictionary.Value < start)
                    start = dictionary.Value;
                _chunkStart[c] = start;
                _chunkEnd[c] = start + meta.TotalCompressedSize;
            }
            var order = new int[n];
            for (int c = 0; c < n; c++)
                order[c] = c;
            var starts = _chunkStart;
            Array.Sort(order, (a, b) => starts[a].CompareTo(starts[b])); // Array.Sort OK: chunk extents never overlap, so no two keys tie
            _fileOrder = order;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(new Span<byte>(buffer, offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            int total = 0;
            while (total < buffer.Length && _position < _length)
            {
                var dest = buffer.Slice(total);
                long inBlock = _position - _blockStart;
                if (inBlock >= 0 && inBlock < _blockCount)
                {
                    int n = (int)Math.Min(dest.Length, _blockCount - inBlock);
                    new ReadOnlySpan<byte>(_block, (int)inBlock, n).CopyTo(dest);
                    _position += n;
                    total += n;
                    continue;
                }

                int k = _fileOrder != null ? FindChunk(_position) : -1;
                if (k >= 0)
                {
                    _touched[_fileOrder[k]] = true;
                    PlanSpan(k, out long spanStart, out long spanEnd);
                    FillBlock(spanStart, (int)(spanEnd - spanStart));
                }
                else if (_fileOrder != null || dest.Length >= _blockSize)
                {
                    // Outside every column chunk (the footer, a page index) or at least a block
                    // long: read exactly what was asked, straight into the caller's buffer.
                    int n = (int)Math.Min(dest.Length, _length - _position);
                    ReadFromDisk(_position, dest.Slice(0, n));
                    _position += n;
                    total += n;
                }
                else
                {
                    FillBlock(_position, (int)Math.Min(_blockSize, _length - _position));
                }
            }
            return total;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return new ValueTask<int>(Read(buffer.Span));
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Task.FromResult(Read(buffer, offset, count));
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _position = offset;
                    break;
                case SeekOrigin.Current:
                    _position += offset;
                    break;
                default:
                    _position = _length + offset;
                    break;
            }
            return _position;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _file.Dispose();
            base.Dispose(disposing);
        }

        // Position in _fileOrder of the column chunk holding pos, or -1.
        private int FindChunk(long pos)
        {
            for (int k = 0; k < _fileOrder.Length; k++)
            {
                int c = _fileOrder[k];
                if (pos >= _chunkStart[c] && pos < _chunkEnd[c])
                    return k;
            }
            return -1;
        }

        // The span to read for a miss in the chunk at file-order position k: that chunk plus
        // every touched chunk reachable from it across gaps no longer than MAX_GAP_BYTES, in
        // both directions, capped at MAX_PLANNED_READ_BYTES.
        private void PlanSpan(int k, out long spanStart, out long spanEnd)
        {
            int c = _fileOrder[k];
            spanStart = _chunkStart[c];
            spanEnd = _chunkEnd[c];
            for (int j = k + 1; j < _fileOrder.Length; j++)
            {
                int col = _fileOrder[j];
                if (!_touched[col])
                    continue;
                if (_chunkStart[col] - spanEnd > MAX_GAP_BYTES || _chunkEnd[col] - spanStart > MAX_PLANNED_READ_BYTES)
                    break;
                spanEnd = _chunkEnd[col];
            }
            for (int j = k - 1; j >= 0; j--)
            {
                int col = _fileOrder[j];
                if (!_touched[col])
                    continue;
                if (spanStart - _chunkEnd[col] > MAX_GAP_BYTES || spanEnd - _chunkStart[col] > MAX_PLANNED_READ_BYTES)
                    break;
                spanStart = _chunkStart[col];
            }
            spanEnd = Math.Min(spanEnd, _length);
        }

        private void FillBlock(long start, int count)
        {
            if (_block.Length < count)
                _block = new byte[count];
            _blockStart = start;
            _blockCount = count;
            ReadFromDisk(start, new Span<byte>(_block, 0, count));
        }

        private void ReadFromDisk(long start, Span<byte> dest)
        {
            long t0 = Stopwatch.GetTimestamp();
            long waited = 0;
            if (_gate)
            {
                lock (DISK_LOCK)
                {
                    long t1 = Stopwatch.GetTimestamp();
                    waited = t1 - t0;
                    t0 = t1;
                    ReadExactlyAt(start, dest);
                }
            }
            else
            {
                ReadExactlyAt(start, dest);
            }
            BlockReadStats.Add(dest.Length, Stopwatch.GetTimestamp() - t0, waited, _fileOrder != null);
        }

        private void ReadExactlyAt(long start, Span<byte> dest)
        {
            _file.Seek(start, SeekOrigin.Begin);
            _file.ReadExactly(dest);
        }
    }
}
