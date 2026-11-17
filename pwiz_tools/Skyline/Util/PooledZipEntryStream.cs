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
using pwiz.Common.Database.FileSystems;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// An <see cref="IPooledStream"/> that reads a file stored UNCOMPRESSED inside a .zip
    /// (typically a .skyd chromatogram cache inside a .sky.zip) in place, without extracting it.
    /// The pooled stream is a <see cref="SliceStream"/> over the .zip file positioned at the
    /// entry's stored byte range, so code that expects a seekable stream over the .skyd (e.g.
    /// <c>ChromatogramCache</c>) works unchanged.
    ///
    /// <see cref="FilePath"/> reports the logical path the entry would have if extracted (used
    /// for pool identity and logging), while the bytes come from the .zip; staleness is measured
    /// against the .zip file's last-write time.
    /// </summary>
    public sealed class PooledZipEntryStream : ConnectionId<Stream>, IPooledStream
    {
        public PooledZipEntryStream(ConnectionPool connectionPool, ZipFileReader.Entry entry)
            : base(connectionPool)
        {
            if (!entry.IsStored)
                throw new ArgumentException(
                    $@"Zip entry '{entry.FileName}' is compressed and cannot be read in place.", nameof(entry));
            Entry = entry;
            FilePath = entry.LogicalPath;
            FileTime = File.GetLastWriteTime(ZipPath);
        }

        private ZipFileReader.Entry Entry { get; }
        public string FilePath { get; }
        private DateTime FileTime { get; }

        private string ZipPath => Entry.ZipFileReader.ZipPath;

        protected override IDisposable Connect()
        {
            return Entry.OpenRandomAccessStream();
        }

        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        public Stream Stream
        {
            get { return Connection; }
        }

        public bool IsModified
        {
            get
            {
                // If it is still in the pool, then it can't have been modified. Otherwise the
                // backing .zip file's last-write time is what matters.
                try
                {
                    return !IsOpen && !Equals(FileTime, File.GetLastWriteTime(ZipPath));
                }
                catch (UnauthorizedAccessException)
                {
                    return true;
                }
                catch (IOException)
                {
                    return true;
                }
            }
        }

        public string ModifiedExplanation
        {
            get
            {
                if (!IsModified)
                    return @"Unmodified";
                return FileEx.GetElapsedTimeExplanation(FileTime, File.GetLastWriteTime(ZipPath));
            }
        }

        public bool IsOpen
        {
            get { return ConnectionPool.IsInPool(this); }
        }

        public void CloseStream()
        {
            Disconnect();
        }

        public override string ToString()
        {
            return $@"PooledZipEntryStream({Entry.FileName} in {ZipPath})"; // Not L10N - debug only
        }
    }
}
