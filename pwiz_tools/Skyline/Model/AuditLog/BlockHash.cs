/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.AuditLog
{
    public class BlockHash
    {
        private readonly HashAlgorithm _hashAlgorithm;
        private readonly byte[] _buffer;
        private int _bufferIndex;

        public BlockHash(HashAlgorithm hashAlgorithm, int bufferSize = 1024 * 1024)
        {
            if (bufferSize <= 0 || hashAlgorithm == null)
                throw new ArgumentException();

            _hashAlgorithm = hashAlgorithm;
            _buffer = new byte[bufferSize];
        }

        public byte[] HashBytes { get; private set; }

        // Adds the given bytes to the hash
        public void ProcessBytes(byte[] bytes, int length = 0)
        {
            if (bytes == null)
                return;

            var inputIndex = 0;
            var bytesLength = length > 0 ? length : bytes.Length;
            var newIndex = _bufferIndex + bytesLength;

            while (newIndex > _buffer.Length)
            {
                // Copy bytes from the new buffer until _buffer is full
                var copySize = _buffer.Length - _bufferIndex;
                Array.Copy(bytes, inputIndex, _buffer, _bufferIndex, copySize);
                inputIndex += copySize;

                // Update hash
                _hashAlgorithm.TransformBlock(_buffer, 0, _buffer.Length, _buffer, 0);
                _bufferIndex = 0;

                newIndex -= _buffer.Length;
            }

            // Copy remaining bytes into _buffer
            Array.Copy(bytes, inputIndex, _buffer, _bufferIndex, bytesLength - inputIndex);
            _bufferIndex = newIndex;
        }

        // Finalizes the hash and returns the hash, which after calling this method
        // is accessible using the HashBytes property
        public byte[] FinalizeHashBytes()
        {
            if (_bufferIndex <= 0 || HashBytes != null)
                return null;

            _hashAlgorithm.TransformFinalBlock(_buffer, 0, _bufferIndex);
            HashBytes = new byte[_hashAlgorithm.Hash.Length];
            Array.Copy(_hashAlgorithm.Hash, HashBytes, HashBytes.Length);

            return HashBytes;
        }

        public static string SafeToBase64(byte[] hash)
        {
            return hash != null ? Convert.ToBase64String(hash) : null;
        }

        public byte[] HashFile(string path)
        {

            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                FileOptions.SequentialScan);

            var fileBuffer = new byte[4096];
            while (true)
            {
                var bytesRead = fileStream.Read(fileBuffer, 0, 4096);
                if (bytesRead <= 0)
                    break;
                ProcessBytes(fileBuffer, bytesRead);
            }

            FinalizeHashBytes();
            return HashBytes;
        }
    }

    /// <summary>
    /// Wraps a stream and computes the SHA1 hash of every byte read from or written to it.
    /// The hashing happens on a background thread so that Read and Write return as soon as
    /// the bytes have been copied and handed to that thread. <see cref="HashBytes"/> and <see cref="Done"/>
    /// wait for the background thread to catch up.
    /// </summary>
    public class HashingStream : Stream
    {
        /// <summary>
        /// Maximum number of Read or Write buffers waiting to be hashed. Read and Write block when
        /// the queue is full so that memory use stays bounded if hashing falls behind the I/O.
        /// Hashing is expected to be much faster than the I/O, so the queue should rarely fill,
        /// but the bound must be large enough that the I/O thread does not stall waiting for the
        /// hashing thread to wake up: callers typically pass only 1-4KB per call.
        /// </summary>
        private const int MAX_QUEUED_BUFFERS = 256;

        private readonly Stream _inner;
        private readonly SHA1CryptoServiceProvider _sha1;
        private readonly BlockHash _blockHash;
        private readonly bool _keepOpen;
        private readonly QueueWorker<byte[]> _hashWorker;

        public HashingStream(Stream inner, bool keepOpen)
        {
            _inner = inner;
            _keepOpen = keepOpen;
            _sha1 = new SHA1CryptoServiceProvider();
            _blockHash = new BlockHash(_sha1);
            _hashWorker = new QueueWorker<byte[]>(consume: HashBuffer);
            _hashWorker.RunAsync(1, @"HashingStream", MAX_QUEUED_BUFFERS);
        }

        public static Stream CreateWriteStream(string path)
        {
            return new HashingStream(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 0x20000,
                FileOptions.SequentialScan), false);
        }

        public static Stream CreateReadStream(string path)
        {
            var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                FileOptions.SequentialScan);
            // Reads ahead on its own thread so the file keeps transferring while the XML is parsed
            return new HashingStream(new SequentialStream(fileStream), false);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = _inner.Read(buffer, offset, count);
            if (bytesRead <= 0)
                return bytesRead;
            AddBytesToHash(buffer, offset, bytesRead);

            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);

            AddBytesToHash(buffer, offset, count);
        }

        public string Hash
        {
            get { return BlockHash.SafeToBase64(HashBytes); }
        }

        /// <summary>
        /// Waits for the hashing thread to catch up and returns the hash bytes.
        /// This is null until <see cref="Done"/> has been called.
        /// </summary>
        public byte[] HashBytes
        {
            get
            {
                WaitForHashing();
                return _blockHash.HashBytes;
            }
        }

        public string Done()
        {
            WaitForHashing();
            _blockHash.FinalizeHashBytes();
            return Hash;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                // Stops the hashing thread, discarding anything it has not hashed yet
                _hashWorker.Dispose();
                if (!_keepOpen)
                {
                    _inner.Dispose();
                }
                _sha1.Dispose();
            }
        }

        /// <summary>
        /// Copies the bytes and hands them to the hashing thread. The caller's buffer must be
        /// copied because the caller is free to reuse it as soon as Read or Write returns.
        /// </summary>
        private void AddBytesToHash(byte[] buffer, int offset, int count)
        {
            if (count <= 0)
            {
                return;
            }
            // Checking before every Add means at most one buffer can be queued after the
            // hashing thread has stopped, so Add never blocks on a queue nobody is draining
            ThrowIfHashingFailed();
            var bytes = new byte[count];
            Array.Copy(buffer, offset, bytes, 0, count);
            _hashWorker.Add(bytes);
        }

        /// <summary>
        /// Waits until the hashing thread has hashed everything handed to it so far.
        /// </summary>
        private void WaitForHashing()
        {
            _hashWorker.Wait();
            ThrowIfHashingFailed();
        }

        /// <summary>
        /// Runs on the hashing thread. Each array is exactly the bytes to be hashed.
        /// </summary>
        private void HashBuffer(byte[] bytes, int threadIndex)
        {
            _blockHash.ProcessBytes(bytes);
        }

        private void ThrowIfHashingFailed()
        {
            var exception = _hashWorker.Exception;
            if (exception != null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }

        #region Unused wrappers
        public override void Flush()
        {
            _inner.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _inner.SetLength(value);
        }

        public override bool CanRead
        {
            get { return _inner.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _inner.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _inner.CanWrite; }
        }

        public override long Length
        {
            get { return _inner.Length; }
        }

        public override long Position
        {
            get { return _inner.Position; }
            set { _inner.Position = value; }
        }
        #endregion
    }
}
