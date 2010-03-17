/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Threading;
using Ionic.Zip;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTest
{
    public static class ExtensionTestContext
    {
        public static string GetTestPath(this TestContext testContext, string relativePath)
        {
            return Path.Combine(testContext.TestDir, relativePath);
        }

        public static String GetProjectDirectory(this TestContext testContext, string relativePath)
        {
            for (String directory = testContext.TestDir; directory.Length > 10; directory = Path.GetDirectoryName(directory))
            {
                if (Equals(Path.GetFileName(directory), "TestResults"))
                    return Path.Combine(Path.GetDirectoryName(directory), relativePath);
            }
            return null;
        }

        public static void ExtractTestFiles(this TestContext testContext, string relativePathZip)
        {
            testContext.ExtractTestFiles(relativePathZip, testContext.TestDir);
        }

        public static void ExtractTestFiles(this TestContext testContext, string relativePathZip, string destDir)
        {
            string pathZip = testContext.GetProjectDirectory(relativePathZip);
            using (ZipFile zipFile = ZipFile.Read(pathZip))
            {
                foreach (ZipEntry zipEntry in zipFile)
                    zipEntry.Extract(destDir, ExtractExistingFileAction.OverwriteSilently);
            }
        }
    }

    /// <summary>
    /// Creates and cleans up a directory containing the contents of a
    /// test ZIP file.
    /// </summary>
    public sealed class TestFilesDir : IDisposable
    {
        private TestContext TestContext { get; set; }

        /// <summary>
        /// Creates a sub-directory of the Test Results directory with the same
        /// basename as a ZIP file in the test project tree.
        /// </summary>
        /// <param name="testContext">The test context for the test creating the directory</param>
        /// <param name="relativePathZip">A root project relative path to the ZIP file</param>
        public TestFilesDir(TestContext testContext, string relativePathZip)
        {
            TestContext = testContext;
            FullPath = TestContext.GetTestPath(Path.GetFileNameWithoutExtension(relativePathZip));
            TestContext.ExtractTestFiles(relativePathZip, FullPath);
        }

        public string FullPath { get; private set; }

        /// <summary>
        /// Returns a full path to a file in the unzipped directory.
        /// </summary>
        /// <param name="relativePath">Relative path, as stored in the ZIP file, to the file</param>
        /// <returns>Absolute path to the file for use in tests</returns>
        public string GetTestPath(string relativePath)
        {
            return Path.Combine(FullPath, relativePath);
        }

        /// <summary>
        /// Attempts to move the directory to make sure no file handles are open.
        /// Used to delete the directory, but it can be useful to look at test
        /// artifacts, after the tests complete.
        /// </summary>
        public void Dispose()
        {
            try
            {
                string guidName = Guid.NewGuid().ToString();
                Directory.Move(FullPath, guidName);
                Directory.Move(guidName, FullPath);
            }
            catch (IOException)
            {
                // Useful for debugging. Exception names file that is locked.
                Directory.Delete(FullPath, true);
            }
        }
    }

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
        }

        public int GlobalIndex { get; private set; }

        public Stream Stream { get; private set; }

        public bool IsModified
        {
            get { return false; }
        }

        public void CloseStream()
        {
            // Do nothing for in-memory read-only streams.
        }
    }

    public class TestDocumentContainer : IDocumentContainer
    {
        private SrmDocument _document;
        private event EventHandler<DocumentChangedEventArgs> DocumentChangedEvent;

        private static readonly object CHANGE_EVENT_LOCK = new object();

        public SrmDocument Document
        {
            get { return Interlocked.Exchange(ref _document, _document); }
        }

        public string DocumentFilePath { get; set; }

        /// <summary>
        /// Override for background loaders that update the document with
        /// partially complete results, to keep the container waiting until
        /// the document is complete.  Default returns true to return control
        /// to the test on the first document change.
        /// </summary>
        /// <param name="docNew">A new document being set to the container</param>
        /// <returns>True if no more processing is necessary</returns>
        protected virtual bool IsComplete(SrmDocument docNew)
        {
            return true;
        }

        public bool SetDocument(SrmDocument docNew, SrmDocument docOriginal)
        {
            return SetDocument(docNew, docOriginal, false);
        }

        public bool SetDocument(SrmDocument docNew, SrmDocument docOriginal, bool wait)
        {
            var docResult = Interlocked.CompareExchange(ref _document, docNew, docOriginal);
            if (!ReferenceEquals(docResult, docOriginal))
                return false;

            if (DocumentChangedEvent != null)
            {
                lock (CHANGE_EVENT_LOCK)
                {
                    DocumentChangedEvent(this, new DocumentChangedEventArgs(docOriginal));

                    if (wait)
                        Monitor.Wait(CHANGE_EVENT_LOCK, 10000000);
                    else if (IsComplete(docNew))
                        Monitor.Pulse(CHANGE_EVENT_LOCK);
                }
            }

            return true;
        }

        public ProgressStatus LastProgress { get; private set; }

        public void AssertComplete()
        {
            if (LastProgress != null)
            {
                if (LastProgress.IsError)
                    throw LastProgress.ErrorException;
                else if (LastProgress.IsCanceled)
                    Assert.Fail("Loader cancelled");
                else
                    Assert.Fail("Unknown progress state");
            }
        }

        private void UpdateProgress(object sender, ProgressUpdateEventArgs e)
        {
            // Unblock the waiting thread, if there was a cancel or error
            lock (CHANGE_EVENT_LOCK)
            {
                LastProgress = (!e.Progress.IsComplete ? e.Progress : null);
                if (e.Progress.IsCanceled || e.Progress.IsError)
                    Monitor.Pulse(CHANGE_EVENT_LOCK);
            }
        }

        public void Register(BackgroundLoader loader)
        {
            loader.ProgressUpdateEvent += UpdateProgress;
        }

        public void Unregister(BackgroundLoader loader)
        {
            loader.ProgressUpdateEvent -= UpdateProgress;
        }

        public void Listen(EventHandler<DocumentChangedEventArgs> listener)
        {
            DocumentChangedEvent += listener;
        }

        public void Unlisten(EventHandler<DocumentChangedEventArgs> listener)
        {
            DocumentChangedEvent -= listener;
        }
    }
}
