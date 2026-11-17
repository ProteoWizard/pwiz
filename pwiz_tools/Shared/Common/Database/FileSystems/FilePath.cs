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
using pwiz.Common.Properties;

namespace pwiz.Common.Database.FileSystems
{
    /// <summary>
    /// A path to a file that might live inside a .zip. Such a path looks exactly like an ordinary
    /// file system path, except that one of its components is a .zip file, e.g.
    /// <c>C:\MyDocument.sky.zip\Library.blib</c> refers to the entry "Library.blib" inside the zip
    /// "C:\MyDocument.sky.zip". Use <see cref="FilePath"/> instead of a raw string for any path that
    /// might be inside a zip, so file operations go through the zip-aware methods here rather than
    /// <c>File.Exists</c>, <c>File.OpenRead</c>, etc.
    ///
    /// These methods are intentionally simple over efficient - e.g. <see cref="Exists"/> opens the
    /// zip's directory to answer. (Longer term this will grow into a proper strongly-typed path
    /// type; for now it wraps a single string.)
    /// </summary>
    public class FilePath
    {
        public FilePath(string path)
        {
            Path = path;
        }

        public string Path { get; }

        /// <summary>
        /// True if a component of the path is an existing .zip file, i.e. this path points at
        /// something inside a zip.
        /// </summary>
        public bool IsInZipFile => TryParseZip(out _, out _);

        /// <summary>
        /// Whether the file exists (as a real file, or as an entry inside the containing zip).
        /// </summary>
        public bool Exists()
        {
            if (!TryParseZip(out var zipFilePath, out var entryName))
                return File.Exists(Path);
            return new ZipFileReader(zipFilePath).FindEntry(entryName) != null;
        }

        /// <summary>
        /// Opens the file to be read from beginning to end. A compressed zip entry is decompressed
        /// as it is read, so a big file never has to be held in memory. This is what almost every
        /// reader wants; only the .skyd is read with random access.
        /// </summary>
        public Stream OpenSequentialStream()
        {
            var entry = GetZipEntry();
            if (entry == null)
                return File.OpenRead(Path);
            return entry.OpenSequentialStream();
        }

        /// <summary>
        /// Opens the file to be read in any order (a seekable stream). Inside a .zip only a stored
        /// (uncompressed) entry can be read this way, because only its bytes are a contiguous range
        /// of the .zip; a compressed entry throws. Skyline stores the files it needs random access
        /// to when sharing, and checks that they really are stored before opening a .zip in place
        /// (see SrmDocumentSharing.CanOpenInPlace).
        /// </summary>
        public Stream OpenRandomAccessStream()
        {
            var entry = GetZipEntry();
            if (entry == null)
                return File.OpenRead(Path);
            return entry.OpenRandomAccessStream();
        }

        /// <summary>
        /// For a path inside a zip, returns the zip <see cref="ZipFileReader.Entry"/> (which knows
        /// its containing <see cref="ZipFileReader"/>). Returns null for an ordinary path (not
        /// inside a zip). Callers that need a pooled or seekable stream over the entry (e.g.
        /// Skyline's PooledZipEntryStream) use this.
        /// </summary>
        public ZipFileReader.Entry GetZipEntry()
        {
            if (!TryParseZip(out var zipFilePath, out var entryName))
                return null;
            var entry = new ZipFileReader(zipFilePath).FindEntry(entryName);
            if (entry == null)
                throw new FileNotFoundException(string.Format(
                    Resources.FilePath_OpenRead_The_entry__0__was_not_found_in_the_zip_file__1_, entryName, zipFilePath), Path);
            return entry;
        }

        /// <summary>
        /// The length (uncompressed size) of the file, whether it is an ordinary file or an entry
        /// inside a zip.
        /// </summary>
        public long GetLength()
        {
            var entry = GetZipEntry();
            if (entry == null)
                return new FileInfo(Path).Length;
            return entry.UncompressedSize;
        }

        /// <summary>
        /// The last-write time of the file. For a path inside a zip this is the time of the
        /// outermost .zip file (the whole archive is what actually changes on disk).
        /// </summary>
        public DateTime GetLastWriteTime()
        {
            if (TryParseZip(out var zipFilePath, out _))
                return File.GetLastWriteTime(zipFilePath);
            return File.GetLastWriteTime(Path);
        }

        /// <summary>
        /// Splits the path at the outermost component that is an existing .zip file. Returns false if
        /// no component is a .zip file (an ordinary path, or the path IS a .zip with nothing after it).
        /// </summary>
        private bool TryParseZip(out string zipFilePath, out string entryName)
        {
            zipFilePath = null;
            entryName = null;
            if (string.IsNullOrEmpty(Path))
                return false;
            const string zipExt = ".zip";
            int searchFrom = 0;
            while (true)
            {
                // The container's ".zip" extension is a file-system path component, so match it
                // case-insensitively (a file may be named .ZIP). Entry names inside the zip, and
                // FilePath equality, remain case-sensitive (see FindEntryByFileName / Equals).
                int zipIndex = Path.IndexOf(zipExt, searchFrom, StringComparison.OrdinalIgnoreCase);
                if (zipIndex < 0)
                    return false;
                int afterZip = zipIndex + zipExt.Length;
                // A separator must follow (there has to be an entry name after the .zip).
                if (afterZip < Path.Length && (Path[afterZip] == '\\' || Path[afterZip] == '/'))
                {
                    var candidate = Path.Substring(0, afterZip);
                    if (File.Exists(candidate))
                    {
                        zipFilePath = candidate;
                        entryName = Path.Substring(afterZip + 1);
                        return true;
                    }
                }
                searchFrom = afterZip;
            }
        }

        public override string ToString()
        {
            return Path;
        }

        protected bool Equals(FilePath other)
        {
            return Equals(Path, other.Path);
        }

        public override bool Equals(object obj)
        {
            return obj is FilePath other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Path?.GetHashCode() ?? 0;
        }
    }
}
