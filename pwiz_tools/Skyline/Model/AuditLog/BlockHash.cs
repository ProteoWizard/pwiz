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
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace pwiz.Skyline.Model.AuditLog
{
    public class BlockHash
    {
        private readonly HashAlgorithm _hashAlgorithm;
        private readonly Encoding _encoding;
        private readonly char[] _buffer;
        private int _bufferIndex;

        public BlockHash(HashAlgorithm hashAlgorithm, Encoding encoding, int bufferSize)
        {
            if (bufferSize <= 0 || hashAlgorithm == null || encoding == null)
                throw new ArgumentException();

            _hashAlgorithm = hashAlgorithm;
            _encoding = encoding;
            _buffer = new char[bufferSize];
        }

        public string Hash { get; private set; }

        // Adds the given characters to the hash
        public void ProcessChars(char[] chars)
        {
            var inputIndex = 0;
            var newIndex = _bufferIndex + chars.Length;

            while (newIndex > _buffer.Length)
            {
                // Copy bytes from the new buffer until _buffer is full
                var copySize = _buffer.Length - _bufferIndex;
                Array.Copy(chars, inputIndex, _buffer, _bufferIndex, copySize);
                inputIndex += copySize;

                // Update hash
                var bytes = _encoding.GetBytes(_buffer);
                _hashAlgorithm.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
                _bufferIndex = 0;

                newIndex -= _buffer.Length;
            }

            // Copy remaining bytes into _buffer
            Array.Copy(chars, inputIndex, _buffer, _bufferIndex, chars.Length - inputIndex);
            _bufferIndex = newIndex;
        }

        // Finalizes the hash and returns the hash, which after calling this method
        // is accessible using the Hash property
        public string FinalizeHash()
        {
            if (_bufferIndex <= 0 || Hash != null)
                return Hash;

            var bytes = _encoding.GetBytes(_buffer);
            _hashAlgorithm.TransformFinalBlock(bytes, 0, _bufferIndex);
            return Hash = string.Join(string.Empty,
                _hashAlgorithm.Hash.Select(b => b.ToString(@"X2"))); // Not L10N
        }
    }

    public class HashingStreamWriter : StreamWriter
    {
        private readonly Encoding _encoding;
        private readonly SHA1Managed _sha1;
        private readonly BlockHash _blockHash;

        public HashingStreamWriter(string path, Encoding encoding) : base(path)
        {
            _encoding = encoding;
            _sha1 = new SHA1Managed();
            _blockHash = new BlockHash(_sha1, _encoding, 1024 * 1024);
        }

        public HashingStreamWriter(string path) : this(path, Encoding.UTF8)
        {
        }

        public string Hash
        {
            get { return _blockHash.Hash; }
        }

        public override Encoding Encoding
        {
            get { return _encoding; }
        }

        #region Write functions used by the XmlTextWriter
        public override void Write(char value)
        {
            base.Write(value);
            _blockHash.ProcessChars(new[] { value });
        }

        public override void Write(char[] buffer)
        {
            base.Write(buffer);
            _blockHash.ProcessChars(buffer);
        }

        public override void Write(string value)
        {
            base.Write(value);
            _blockHash.ProcessChars(value.ToCharArray());
        }

        public override void Write(char[] buffer, int index, int count)
        {
            base.Write(buffer, index, count);
            var copy = new char[count];
            Array.Copy(buffer, index, copy, 0, count);
            _blockHash.ProcessChars(copy);
        }
        #endregion

        public string DoneWriting()
        {
            return _blockHash.FinalizeHash();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                _sha1.Dispose();
        }
    }

    public class HashingStreamReader : StreamReader
    {
        private readonly SHA1Managed _sha1;
        private readonly BlockHash _blockHash;
        protected readonly long _totalChars;
        protected long _charsReaad;

        public HashingStreamReader(string path, Encoding encoding) : base(path, encoding)
        {
            _sha1 = new SHA1Managed();
            _blockHash = new BlockHash(_sha1, encoding, 1024 * 1024);
            _totalChars = new FileInfo(path).Length;
        }

        public HashingStreamReader(string path) : this(path, Encoding.UTF8)
        {
        }
        
        public string Hash
        {
            get { return _blockHash.Hash; }
        }

        public override int Read(char[] buffer, int index, int count)
        {
            var charsRead = base.Read(buffer, index, count);
            if (charsRead <= 0)
                return charsRead;
            
            _charsReaad += charsRead;

            var copy = new char[charsRead];
            Array.Copy(buffer, index, copy, 0, charsRead);
            _blockHash.ProcessChars(copy);

            if (_charsReaad == _totalChars)
                _blockHash.FinalizeHash();

            return charsRead;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                _sha1.Dispose();
        }
    }
}