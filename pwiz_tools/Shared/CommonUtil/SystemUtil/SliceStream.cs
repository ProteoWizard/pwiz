/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// A read-only, seekable stream that exposes a contiguous byte range
    /// [offset, offset+length) of an underlying stream as if it were a whole file starting
    /// at position 0. Used to read a stored zip entry's data in place. Owns (and disposes)
    /// the underlying stream unless constructed with leaveOpen.
    /// <br/>
    /// The position of <see cref="BaseStream"/> is the one and only truth about where this stream
    /// is, so that code which reads the base stream itself (see
    /// <see cref="pwiz.Common.Collections.StructSerializer{TItem}"/>, which reads a FileStream
    /// directly) and this stream cannot disagree about it.
    /// </summary>
    public sealed class SliceStream : Stream
    {
        private readonly long _length;
        private readonly bool _leaveOpen;

        public SliceStream(Stream baseStream, long offset, long length, bool leaveOpen = false)
        {
            BaseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (!baseStream.CanSeek)
                throw new ArgumentException(@"Base stream must be seekable.", nameof(baseStream));
            Offset = offset;
            _length = length;
            _leaveOpen = leaveOpen;
            baseStream.Seek(offset, SeekOrigin.Begin);
        }

        /// <summary>
        /// The stream that this is a window onto. Its position says where this stream is, and code
        /// which knows how to read it faster than through this stream may do so, so long as it
        /// leaves the position wherever it read up to.
        /// </summary>
        public Stream BaseStream { get; }

        /// <summary>
        /// Where in <see cref="BaseStream"/> this stream's position 0 is.
        /// </summary>
        public long Offset { get; }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => BaseStream.Position - Offset;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                BaseStream.Position = Offset + value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long position = Position;
            if (position >= _length)
                return 0;
            long remaining = _length - position;
            if (count > remaining)
                count = (int) remaining;
            if (count <= 0)
                return 0;
            int total = 0;
            while (total < count)
            {
                int read = BaseStream.Read(buffer, offset + total, count - total);
                if (read <= 0)
                    break;
                total += read;
            }
            return total;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;
                case SeekOrigin.Current:
                    newPosition = Position + offset;
                    break;
                case SeekOrigin.End:
                    newPosition = _length + offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin));
            }
            if (newPosition < 0)
                throw new IOException(@"An attempt was made to move the position before the beginning of the stream.");
            Position = newPosition;
            return newPosition;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveOpen)
                BaseStream.Dispose();
            base.Dispose(disposing);
        }
    }
}
