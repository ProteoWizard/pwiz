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

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Wraps a stream so that its Task-returning methods do their work before returning,
    /// and hand back a Task which has already completed. Awaiting one of them never
    /// suspends, so no continuation gets scheduled and no thread pool thread is used.
    ///
    /// This is for handing to a library whose API is asynchronous, from a thread which is
    /// going to block waiting for the result anyway. A FileStream that was not opened with
    /// FileOptions.Asynchronous implements WriteAsync by queueing the synchronous write to
    /// the thread pool, so the thread hop buys nothing.
    /// </summary>
    public class SynchronousStream : Stream
    {
        private readonly Stream _stream;

        public SynchronousStream(Stream stream)
        {
            _stream = stream;
        }

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => _stream.CanWrite;
        public override long Length => _stream.Length;

        public override long Position
        {
            get { return _stream.Position; }
            set { _stream.Position = value; }
        }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _stream.Flush();
            return Task.CompletedTask;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_stream.Read(buffer, offset, count));
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _stream.Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _stream.CopyTo(destination, bufferSize);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes the wrapped stream, so that wrapping a stream in this class does not
        /// change who ends up closing it.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
