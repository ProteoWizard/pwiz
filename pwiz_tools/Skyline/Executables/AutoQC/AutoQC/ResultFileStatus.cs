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
using System.IO;

namespace AutoQC
{
    public enum FileStatus { Ready, Waiting, ExceedMaximumAcquiTime}
    
    interface IResultFileStatus
    {
        /// <exception cref="FileStatusException"></exception>
        FileStatus CheckStatus(string filePath) ;
    }

    class AcquisitionTimeFileStatus : IResultFileStatus
    {
        // Expected acquisition time in minutes. This is how long we will wait till we consider the file ready for import.
        private readonly int _acquisitionTime;

        public AcquisitionTimeFileStatus(int acquisitionTime)
        {
            _acquisitionTime = acquisitionTime;
        }

        public FileStatus CheckStatus(string filePath)
        {
            if (File.Exists(filePath))
            {
                return GetFileStatus(filePath);
            }
            if (Directory.Exists(filePath))
            {
                return GetDirectoryStatus(filePath);
            }
            throw new FileStatusException(string.Format("Error getting status for {0}", filePath));
        }

        private FileStatus GetFileStatus(string filePath)
        {
            // Get the time elapsed since the file was first created.
            DateTime createTime;
            DateTime lastModifiedTime;
            try
            {
                createTime = File.GetCreationTime(filePath);
                lastModifiedTime = File.GetLastWriteTime(filePath);
            }
            catch (Exception e)
            {
                throw new FileStatusException(string.Format("Error getting status of file {0}", filePath), e);
            }

            if (lastModifiedTime.CompareTo(createTime) == -1)
            {
                // If the file was copied to the directory, its "creation time" will be later than the 
                // "last write time". In this case use the "last write time" otherwise we will have to 
                // wait for "acquisition time" to elapse before the file is considered "ready" to import.
                return FileStatus.Ready;
            }

            return createTime.AddMinutes(_acquisitionTime) < DateTime.Now ? FileStatus.Ready : FileStatus.Waiting;    
        }

        private FileStatus GetDirectoryStatus(string filePath)
        {
            var files = Directory.GetFiles(filePath, "*", SearchOption.AllDirectories);
            return files.Length > 0 ?
                GetFileStatus(files[0]) : // Check only for one file. If this directory was copied
                                          // The "creation time" for the first one we encounter should be later
                                          // that the "last write time". 
                FileStatus.Waiting;
        }
    }
}
