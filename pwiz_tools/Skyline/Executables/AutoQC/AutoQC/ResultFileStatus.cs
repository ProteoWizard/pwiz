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
    interface IResultFileStatus
    {
        bool isReady(string filePath);
    }

    class XRawFileSatus : IResultFileStatus
    {
        public bool isReady(string filePath)
        {
            IXRawfile rawFile = null;
            try
            {
                rawFile = new MSFileReader_XRawfileClass();
                rawFile.Open(filePath);
                var inAcq = 1;
                rawFile.InAcquisition(ref inAcq);
                if (inAcq == 1)
                {
                    return false;
                }
            }
            finally
            {
                if (rawFile != null)
                {
                    rawFile.Close();

                }
            }
            return true;
        }
    }

    class DelayTimeFileStatus : IResultFileStatus
    {
        // Delay time in minutes. This is how long we will wait till we consider the file ready for import.
        private readonly int _delayTime;

        public DelayTimeFileStatus(int delayTime)
        {
            _delayTime = delayTime;
        }

        public bool isReady(string filePath)
        {
            // Get the time elapsed since the file was first created.
            var createTime = File.GetCreationTime(filePath);
            return createTime.AddMinutes(_delayTime) < DateTime.Now;
        }
    }
}
