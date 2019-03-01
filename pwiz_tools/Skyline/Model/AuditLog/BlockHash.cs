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
using System.Security.Cryptography;

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
        public void ProcessBytes(byte[] bytes)
        {
            if (bytes == null)
                return;

            var inputIndex = 0;
            var newIndex = _bufferIndex + bytes.Length;

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
            Array.Copy(bytes, inputIndex, _buffer, _bufferIndex, bytes.Length - inputIndex);
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
    }

    public class HashingStream : Stream
    {
        private readonly Stream _inner;
        private readonly SHA1CryptoServiceProvider _sha1;
        private readonly BlockHash _blockHash;

        public HashingStream(Stream inner)
        {
            _inner = inner;
            _sha1 = new SHA1CryptoServiceProvider();
            _blockHash = new BlockHash(_sha1);
        }

        public static Stream CreateWriteStream(string path)
        {
            return new HashingStream(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096,
                FileOptions.SequentialScan));
        }

        public static Stream CreateReadStream(string path)
        {
            return new HashingStream(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                FileOptions.SequentialScan));
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = _inner.Read(buffer, offset, count);
            if (bytesRead <= 0)
                return bytesRead;

            var copy = new byte[bytesRead];
            Array.Copy(buffer, offset, copy, 0, bytesRead);
            _blockHash.ProcessBytes(copy);

            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);

            var copy = new byte[count];
            Array.Copy(buffer, offset, copy, 0, count);
            _blockHash.ProcessBytes(copy);
        }


        public string Hash
        {
            get { return BlockHash.SafeToBase64(HashBytes); }
        }

        public byte[] HashBytes
        {
            get { return _blockHash.HashBytes; }
        }

        public string Done()
        {
            _blockHash.FinalizeHashBytes();
            return Hash;
        }

        public byte[] DoneBytes()
        {
            _blockHash.FinalizeHashBytes();
            return HashBytes;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _inner.Dispose();
                _sha1.Dispose();
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
