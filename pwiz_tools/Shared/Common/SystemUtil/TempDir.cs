/*
 * Original author: Brian Pratt <bspratt .at. protein.ms>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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

namespace pwiz.Common.SystemUtil
{
    public class TempDir : IDisposable
    {
        // Helps when dealing with 3rd party code that leaves tempfiles behind
        // Creates a directory under the current %TMP% and changes %TMP% to that
        // Restores original %TMP% on Dispose
        private string _savedTMP;
        private string _newTMP;
        private static string TMP = @"TMP";

        public TempDir()
        {
            _savedTMP = Environment.GetEnvironmentVariable(TMP);
            try
            {
                _newTMP = Path.GetTempFileName(); // Creates a file
                File.Delete(_newTMP); // But we want a directory
                Directory.CreateDirectory(_newTMP);
                Environment.SetEnvironmentVariable(TMP, _newTMP);
            }
            catch // If this doesn't work out for any reason, let it go - this tidyness is just nice to have
            {
                // ignored
            }
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_newTMP, true);
            }
            catch
            {
                // ignored
            }
            Environment.SetEnvironmentVariable(TMP, _savedTMP);
        }
    }
}
