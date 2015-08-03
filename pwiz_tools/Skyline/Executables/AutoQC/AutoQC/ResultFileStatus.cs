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
using MSFileReaderLib;

namespace AutoQC
{
    public enum Status { Ready, Waiting, ExceedMaximumAcquiTime }
    
    interface IResultFileStatus
    {
        /// <exception cref="FileStatusException"></exception>
        Status CheckStatus(string filePath) ;
    }

    class XRawFileStatus : IResultFileStatus
    {
        // Acquision time in minutes. This is how long we will wait till we consider the file ready for import.
        private readonly int _acquisitionTime;

        public XRawFileStatus(int acquisitionTime)
        {
            _acquisitionTime = acquisitionTime;
        }

        public Status CheckStatus(string filePath)
        {
            IXRawfile rawFile = null;
            // Get the time elapsed since the file was first created.
            DateTime createTime;

            var inAcq = 1;
            try
            {
                createTime = File.GetCreationTime(filePath);

                rawFile = new MSFileReader_XRawfileClass();
                rawFile.Open(filePath);
                rawFile.InAcquisition(ref inAcq);          
            }
            catch (Exception e)
            {
                throw new FileStatusException(string.Format("Error getting status of file {0}", filePath), e);
            }
            finally
            {
                if (rawFile != null)
                {
                    rawFile.Close();
                }
            }

            if (inAcq == 1)
            {
                // Check whether we have exceeded the expected acquisition time
                return createTime.AddMinutes(_acquisitionTime) < DateTime.Now
                    ? Status.ExceedMaximumAcquiTime
                    : Status.Waiting;
            }

            return Status.Ready;
        }
    }

    class AcquisitionTimeFileStatus : IResultFileStatus
    {
        // Expected aquisition time in minutes. This is how long we will wait till we consider the file ready for import.
        private readonly int _acquisitionTime;

        public AcquisitionTimeFileStatus(int acquisitionTime)
        {
            _acquisitionTime = acquisitionTime;
        }

        public Status CheckStatus(string filePath)
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
                createTime = lastModifiedTime;
            }

            return createTime.AddMinutes(_acquisitionTime) < DateTime.Now ? Status.Ready : Status.Waiting;
        }
    }
}
