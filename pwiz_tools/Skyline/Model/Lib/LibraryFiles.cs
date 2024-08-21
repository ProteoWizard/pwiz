/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
{
    /// <summary>
    /// Maintains a list of the names of the spectrum files (or equivalent)
    /// that a library contains results from.
    /// </summary>
    public class LibraryFiles : IReadOnlyList<string>
    {
        public static LibraryFiles EMPTY = new LibraryFiles(Array.Empty<string>());

        private Dictionary<MsDataFileUri, int> _msDataFileUriLookup = new Dictionary<MsDataFileUri, int>();
        private ImmutableList<string> _baseNames;
        public LibraryFiles(IEnumerable<string> sourceFilePaths)
        {
            FilePaths = ImmutableList.ValueOf(sourceFilePaths);
            _baseNames = ImmutableList.ValueOf(FilePaths.Select(GetBaseName));
        }

        public ImmutableList<string> FilePaths { get; }
        public int Count
        {
            get { return FilePaths.Count; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<string> GetEnumerator()
        {
            // ReSharper disable once NotDisposedResourceIsReturned
            return FilePaths.GetEnumerator();
        }

        public string this[int index] => FilePaths[index];

        /// <summary>
        /// Returns the index of the file which matches the MsDataFileUri or -1 if
        /// there is no match.
        /// </summary>
        public int FindIndexOf(MsDataFileUri filePath)
        {
            if (filePath == null || FilePaths.Count == 0)
            {
                return -1;
            }

            lock (_msDataFileUriLookup)
            {
                if (_msDataFileUriLookup.TryGetValue(filePath, out int index))
                {
                    return index;
                }
            }
            string filePathToString = filePath.ToString();
            // First look for an exact path match
            int i = FilePaths.IndexOf(filePathToString);
            // filePath.ToString may include decorators e.g. "C:\\data\\mydata.raw?centroid_ms1=true", try unadorned name ("mydata.raw")
            if (i == -1)
            {
                string fileName = filePath.GetFileName();
                i = FilePaths.IndexOf(fileName);
            }
            // Or a straight basename match, which we sometimes use internally
            if (i == -1)
                i = _baseNames.IndexOf(filePathToString);
            // NOTE: We don't expect multi-part wiff files to appear in a library
            if (i == -1 && null == filePath.GetSampleName())
            {
                try
                {
                    // Failing an exact path match, look for a basename match
                    string baseName = filePath.GetFileNameWithoutExtension();
                    i = _baseNames.IndexOf(sourceFileBaseName => MeasuredResults.IsBaseNameMatch(baseName, sourceFileBaseName));
                }
                catch (ArgumentException)
                {
                    // Handle: Illegal characters in path
                }
            }

            lock (_msDataFileUriLookup)
            {
                // remember the result for faster lookup when asked again
                _msDataFileUriLookup[filePath] = i;
            }
            return i;
        }

        private static string GetBaseName(string filePath)
        {
            try
            {
                return Path.GetFileNameWithoutExtension(filePath);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
