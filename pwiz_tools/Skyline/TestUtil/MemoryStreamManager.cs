/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009-2010 University of Washington - Seattle, WA
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
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestUtil
{
    public class MemoryStreamManager : IStreamManager
    {
        private readonly Dictionary<string, byte[]> _binaryFiles = new Dictionary<string, byte[]>();
        private readonly Dictionary<string, MemoryStream> _openStreams = new Dictionary<string, MemoryStream>();
        private readonly Dictionary<string, string> _textFiles = new Dictionary<string, string>();
        private readonly Dictionary<string, TextWriter> _openWriters = new Dictionary<string, TextWriter>();
        private readonly Dictionary<string, string> _cachedFiles = new Dictionary<string, string>();

        private int _fileCount;

        public IDictionary<string, byte[]> BinaryFiles { get { return _binaryFiles; } }
        public IDictionary<string, string> TextFiles { get { return _textFiles; } }

        public Stream CreateStream(string path, FileMode mode, bool buffered)
        {
            MemoryStream stream = new MemoryStream(1024);
            switch (mode)
            {
                case FileMode.Create:
                    _binaryFiles[path] = new byte[0];
                    _openStreams[path] = stream;
                    return stream;
                case FileMode.Open:
                    byte[] fileBytes;
                    if (!_binaryFiles.TryGetValue(path, out fileBytes))
                        throw new FileNotFoundException("File does not exist.", path);
                    return new MemoryStream(fileBytes);
                default:
                    throw new InvalidOperationException("File mode not yet supported.");
            }

        }

        public void Finish(Stream stream)
        {
            foreach (var pair in _openStreams)
            {
                MemoryStream streamOpen = pair.Value;
                if (ReferenceEquals(stream, streamOpen))
                {
                    byte[] output = new byte[streamOpen.Length];
                    Array.Copy(streamOpen.GetBuffer(), output, output.Length);
                    _binaryFiles[pair.Key] = output;

                    _openStreams.Remove(pair.Key);
                    break;
                }
            }
            stream.Close();
        }

        public IPooledStream CreatePooledStream(string path, bool buffer)
        {
            return new MemoryPooledStream(CreateStream(path, FileMode.Open, buffer));
        }

        // CONSIDER: No stream connections are stored in this pool, as is the case
        //           with all opened streams in the running applicaton.
        private readonly ConnectionPool _connectionPool = new ConnectionPool();
        public ConnectionPool ConnectionPool
        {
            get { return _connectionPool; }
        }

        public TextReader CreateReader(string path)
        {
            string content;
            if (!_textFiles.TryGetValue(path, out content))
                throw new FileNotFoundException("File does not exist.", path);
            return new StringReader(content);
        }

        public TextWriter CreateWriter(string path)
        {
            _textFiles[path] = "";
            TextWriter writer = new StringWriter();
            _openWriters[path] = writer;
            return writer;
        }

        public void Finish(TextWriter writer)
        {
            foreach (var pair in _openWriters)
            {
                TextWriter writerOpen = pair.Value;
                if (ReferenceEquals(writer, writerOpen))
                {
                    _textFiles[pair.Key] = writer.ToString();

                    _openWriters.Remove(pair.Key);
                    break;
                }
            }
            writer.Close();
        }

        public FileAttributes GetAttributes(string path)
        {
            return new FileAttributes();
        }

        public long GetLength(string path)
        {
            string content;
            if (_textFiles.TryGetValue(path, out content))
                return content.Length;
            byte[] fileBytes;
            if (_binaryFiles.TryGetValue(path, out fileBytes))
                return fileBytes.Length;

            throw new FileNotFoundException("File does not exist", path);
        }

        public bool Exists(string path)
        {
            return _textFiles.ContainsKey(path) || _binaryFiles.ContainsKey(path);
        }

        public void Delete(string path)
        {
            _textFiles.Remove(path);
            _binaryFiles.Remove(path);

            // Make sure it is removed as a possible cache file.
            foreach (var pair in _cachedFiles)
            {
                if (Equals(pair.Value, path))
                {
                    _cachedFiles.Remove(pair.Key);
                    break;
                }
            }
        }

        public void Commit(string pathTemp, string pathDestination, IPooledStream streamDest)
        {
            if (_textFiles.ContainsKey(pathTemp))
                _textFiles[pathDestination] = _textFiles[pathTemp];
            else if (_binaryFiles.ContainsKey(pathTemp))
                _binaryFiles[pathDestination] = _binaryFiles[pathTemp];
            Delete(pathTemp);
        }

        public void SetCache(string path, string pathCache)
        {
            if (Exists(path) && Exists(pathCache))
                _cachedFiles[path] = pathCache;
        }

        public bool IsCached(string path, string pathCache)
        {
            if (Exists(pathCache))
            {
                string pathCacheStored;
                if (_cachedFiles.TryGetValue(path, out pathCacheStored) && Equals(pathCache, pathCacheStored))
                    return true;
            }
            return false;
        }

        public string GetTempFileName(string basePath, string prefix)
        {
            return Path.Combine(basePath, string.Format("{0}{1}.tmp", prefix, _fileCount++));
        }
    }

    public class MemoryPooledStream : IPooledStream
    {
        private static int _indexNext;        

        public MemoryPooledStream(Stream stream)
        {
            GlobalIndex = _indexNext++;
            Stream = stream;
            IsOpen = true;
        }

        public int GlobalIndex { get; private set; }

        public Stream Stream { get; private set; }

        public bool IsModified
        {
            get { return false; }
        }

        public string ModifiedExplanation
        {
            get { return "Unmodified"; }    // Not L10N
        }

        public bool IsOpen { get; private set; }

        public void CloseStream()
        {
            // Do nothing for in-memory read-only streams.
            IsOpen = false;
        }
    }
}