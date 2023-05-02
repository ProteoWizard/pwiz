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
            }
            catch // It's nice to tidy up but not critical (though we do have tests for this in Skyline)
            {
                _newTMP = null;
                return;
            }

            if (Directory.Exists(_newTMP) || File.Exists(_newTMP))
            {
                // Should never happen
                _newTMP = null;
                throw new IOException($@"proposed temp directory {_newTMP} already exists");
            }

            try
            {
                Directory.CreateDirectory(_newTMP);
                Environment.SetEnvironmentVariable(TMP, _newTMP);
            }
            catch // Ignored - it's nice to tidy up but not critical (though we do have tests for this in Skyline)
            {
                _newTMP = null;
            }
        }

        public void Dispose()
        {
            try
            {
                if (_newTMP != null)
                {
                    Directory.Delete(_newTMP, true);
                }
            }
            catch
            {
                // ignored - it's nice to tidy up but not critical (though we do have tests for this in Skyline)
            }
            Environment.SetEnvironmentVariable(TMP, _savedTMP);
        }
    }
}
