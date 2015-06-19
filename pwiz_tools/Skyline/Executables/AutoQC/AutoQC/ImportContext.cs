using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AutoQC
{
    public class ImportContext
    {
        private int _tryCount;
        private int _currentIndex;
        private readonly List<FileInfo> _resultsFileList; 

        public ImportContext(FileInfo resultsFile) 
        {
            if (resultsFile == null)
            {
                throw new ArgumentException("Cannot initialize ImportContext with a null resultsFile.");
            }
            _resultsFileList = new List<FileInfo> {resultsFile};
        }

        public ImportContext(List<FileInfo> resultsFiles)
        {
            if (resultsFiles == null || resultsFiles.Count == 0)
            {
                throw new ArgumentException("Cannot initialize ImportContext with a null or empty resultsFile list.");
            }
            _resultsFileList = resultsFiles;
            if (_resultsFileList != null)
            {
                _resultsFileList = _resultsFileList.OrderBy(f => f.LastWriteTime).ToList();
            }
        }

        public int GetTryCount()
        {
            return _tryCount;
        }

        public void incrementTryCount()
        {
            _tryCount++;
        }

        public bool canRetry()
        {
            return _tryCount < AutoQCForm.MAX_TRY_COUNT;
        }

        public string GetResultsFilePath()
        {
            FileInfo currentFile = getCurrentFile();
            return currentFile != null ? currentFile.FullName : null;
        }

        public FileInfo GetNextFile()
        {
            return _currentIndex < _resultsFileList.Count ? _resultsFileList[_currentIndex++] : null;
        }

        public FileInfo getCurrentFile()
        {
            return _currentIndex < _resultsFileList.Count ? _resultsFileList[_currentIndex] : null; 
        }

        public bool ImportExisting()
        {
            return _resultsFileList.Count > 1;
        }

        public bool ImportingLast()
        {
            return _currentIndex == _resultsFileList.Count - 1;
        }

        public DateTime GetOldestFileDate()
        {
            return _resultsFileList[_resultsFileList.Count - 1].LastWriteTime;
        }
    }
}
