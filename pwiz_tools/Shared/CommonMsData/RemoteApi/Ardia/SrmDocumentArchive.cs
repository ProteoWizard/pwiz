/*
 * Copyright 2025 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    /// <summary>
    /// Represents a .sky.zip file which may be a single file or could be a split across many smaller
    /// archives that can be re-assembled into a single archive.
    ///
    /// Details about the archive are encapsulated including - whether the upload is multipart,
    /// size of the total upload, and info about each part like name, path, and size.
    /// </summary>
    public sealed class SrmDocumentArchive
    {
        /// <summary>
        /// Create an archive
        /// </summary>
        /// <param name="directoryPath">Path of the archive's 1 or more parts</param>
        /// <param name="fileName">Primary name of the archive - typically ends in .zip</param>
        /// <returns></returns>
        public static SrmDocumentArchive Create(string directoryPath, string fileName)
        {
            return new SrmDocumentArchive(directoryPath, fileName);
        }

        private SrmDocumentArchive(string directoryPath, string fileName)
        {
            ArchiveFilePath = Path.Combine(directoryPath, Path.GetFileName(fileName));
            Parts = new List<PartInfo>();
        }

        /// <summary>
        /// Initializes information about the archive, including locating and reading information about each
        /// of the archive's 1 or more parts.
        ///
        /// Must be called *after* creating the document's archive by calling SkylineWindow.ShareDocument
        /// because all the archive's parts must be available locally for processing.
        ///
        /// Note: the parts of a multipart archive might end with .zip or ".z\d+".
        /// </summary>
        public void Init()
        {
            Assume.IsNotNull(Parts);

            var directory = Path.GetDirectoryName(ArchiveFilePath);
            var paths = GatherZipFileParts(directory);
            for(var i = 0; i < paths.Length; i++)
            {
                var sizeInBytes = new FileInfo(paths[i]).Length;
                
                Parts.Add(new PartInfo(i, paths[i], sizeInBytes));
            }

            Parts = ImmutableList<PartInfo>.ValueOf(Parts);
        }

        public string ArchiveFilePath { get; }
        public string ArchiveFileName => Path.GetFileName(Parts.Last().FilePath);

        public IList<PartInfo> Parts { get; private set; }
        public bool IsMultipart => Parts.Count > 1;
        public int PartCount => Parts.Count;

        /// <summary>
        /// Total size in bytes of this archive. For a single-part archive, this will be the same as the size of the 1 part.
        /// </summary>
        public long TotalSize => Parts.Select(item => item.SizeInBytes).Sum();

        // NB: reads 1 or more files in a directory making the assumption that *all* files
        //     are part of the archive.
        private static string[] GatherZipFileParts(string directory)
        {
            if (directory == null || !Directory.Exists(directory))
            {
                return new string[] { };
            }
            else
            {
                return Directory.GetFiles(directory);
            }
        }

        public struct PartInfo
        {
            internal PartInfo(int index, string filePath, long sizeInBytes)
            {
                Index = index;
                ReadableIndex = Index + 1;
                SizeInBytes = sizeInBytes;
                FilePath = filePath;
            }

            public readonly int Index;
            public readonly int ReadableIndex;
            public readonly long SizeInBytes;
            public readonly string FilePath;
        }
    }
}
