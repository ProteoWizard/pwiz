/*
 * Original author: Vagisha Sharma <vsharma .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Linq;

namespace AutoQC
{
    public class ImportContext
    {
        private int _currentIndex = -1;
        private readonly List<string> _resultsFileList;
        public bool ImportExisting { get; private set; }
        // Total results files imported since AutoQC startup
        internal int TotalImportCount { get; set; }
        public string WorkingDir { get; private set; }

        public ImportContext(string resultsFile, bool importExisting = false) 
        {
            if (resultsFile == null)
            {
                throw new ArgumentException("Cannot initialize ImportContext with a null resultsFile");
            }
            _resultsFileList = new List<string> {resultsFile};
            ImportExisting = importExisting;
            WorkingDir = Path.GetDirectoryName(resultsFile);
        }

        public ImportContext(List<string> resultsFiles, bool importExisting = true)
        {
            if (resultsFiles == null || resultsFiles.Count == 0)
            {
                throw new ArgumentException("Cannot initialize ImportContext with a null or empty resultsFile list.");
            }
            _resultsFileList = resultsFiles.OrderBy(f => new FileInfo(f).LastWriteTime).ToList();

            ImportExisting = importExisting;
            WorkingDir = Path.GetDirectoryName(_resultsFileList[0]);
        }

        public string GetNextFile()
        {
            _currentIndex++;
            return GetCurrentFile();
        }

        public string GetCurrentFile()
        {
            return _currentIndex == -1
                ? GetNextFile()
                : (_currentIndex < _resultsFileList.Count ? _resultsFileList[_currentIndex] : null);
        }

        public bool ImportingLast()
        {
            return _currentIndex >= _resultsFileList.Count - 1;
        }

        public virtual DateTime GetOldestImportedFileDate(DateTime lastAcqDate)
        {
            // Results files are sorted by LastWriteTime;
            if (DateTime.MinValue.Equals(lastAcqDate))
            {
                return new FileInfo(_resultsFileList[0]).LastWriteTime;
            }
            for (int i = 0; i < _resultsFileList.Count; i++)
            {
                DateTime lastWriteTime = new FileInfo(_resultsFileList[i]).LastWriteTime;
                if (lastWriteTime.CompareTo(lastAcqDate) > 0)
                {
                    return lastWriteTime;
                }
            }
            return lastAcqDate;
        }
    }
}
