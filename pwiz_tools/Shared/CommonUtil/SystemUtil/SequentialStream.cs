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
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// A forward-only stream which reads ahead of its caller on a dedicated thread.
    /// The reading thread pulls fixed-size blocks from the inner stream into a bounded
    /// queue, so that a slow device (such as a network drive) keeps transferring while
    /// the caller is busy processing the bytes it has already received.
    /// Disposing this stream stops the reading thread and disposes the inner stream.
    /// </summary>
    public class SequentialStream : Stream
    {
        public const int DEFAULT_BLOCK_SIZE = 0x10000;
        public const int DEFAULT_MAX_QUEUED_BLOCKS = 64;

        private readonly Stream _inner;
        private readonly int _blockSize;
        private readonly BlockingCollection<ArraySegment<byte>> _blocks;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly Thread _readThread;
        private Exception _readException;
        // Block most recently taken from the queue, and how much of it has been returned to the caller
        private ArraySegment<byte> _currentBlock;
        private int _currentPosition;
        private long _position;

        public SequentialStream(Stream inner, int blockSize = DEFAULT_BLOCK_SIZE, int maxQueuedBlocks = DEFAULT_MAX_QUEUED_BLOCKS)
        {
            _inner = inner;
            _blockSize = blockSize;
            _blocks = new BlockingCollection<ArraySegment<byte>>(maxQueuedBlocks);
            _readThread = new Thread(ReadBlocks)
            {
                Name = @"SequentialStream",
                IsBackground = true
            };
            _readThread.Start();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_currentPosition == _currentBlock.Count)
            {
                // TryTake returns false once the reading thread has finished and the queue is empty
                if (!_blocks.TryTake(out _currentBlock, Timeout.Infinite))
                {
                    _currentBlock = default;
                    _currentPosition = 0;
                    if (_readException != null)
                    {
                        ExceptionDispatchInfo.Capture(_readException).Throw();
                    }
                    return 0;
                }
                _currentPosition = 0;
            }
            int bytesRead = Math.Min(count, _currentBlock.Count - _currentPosition);
            Array.Copy(_currentBlock.Array, _currentBlock.Offset + _currentPosition, buffer, offset, bytesRead);
            _currentPosition += bytesRead;
            _position += bytesRead;
            return bytesRead;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { return _inner.Length; }
        }

        /// <summary>
        /// The number of bytes returned to the caller so far, which is behind the
        /// position of the inner stream by however much has been read ahead.
        /// </summary>
        public override long Position
        {
            get { return _position; }
            set { throw new NotSupportedException(); }
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
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
            base.Dispose(disposing);

            if (disposing)
            {
                // Unblocks the reading thread if it is waiting for room in the queue, then
                // waits for it to exit so that no thread outlives this stream
                _cancellationTokenSource.Cancel();
                _readThread.Join();
                _inner.Dispose();
                _blocks.Dispose();
                _cancellationTokenSource.Dispose();
            }
        }

        /// <summary>
        /// Runs on the reading thread until the end of the inner stream, an error, or disposal.
        /// </summary>
        private void ReadBlocks()
        {
            try
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    // A new buffer for every block, because the caller may still be reading the previous one
                    var buffer = new byte[_blockSize];
                    int count = _inner.Read(buffer, 0, _blockSize);
                    if (count <= 0)
                    {
                        break;
                    }
                    _blocks.Add(new ArraySegment<byte>(buffer, 0, count), _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Disposed while waiting for room in the queue
            }
            catch (Exception e)
            {
                // Reported to the caller by Read after the blocks read before the error have been returned
                _readException = e;
            }
            finally
            {
                _blocks.CompleteAdding();
            }
        }
    }
}
