/*
 * Original author: Daniel Broudy <daniel.broudy .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestUtil
{

    /// <summary>
    /// Class used for moving toolDir out of the way in testing tool installation.
    /// This is done for testing scenarios that do not copy Skyline.exe to a different
    /// directory, to avoid destroying the Tools directory for the current build,
    /// which may have been created during manual testing.
    /// </summary>
    public class MovedDirectory : IDisposable
    {
        public const string MOVED_PREFIX = "Moved_";

        public MovedDirectory(string dirPath, bool isLoopingTest)
        {
            if (isLoopingTest)
            {
                SrcDirPath = dirPath;

                // Only move, if there is an existing Tools directory
                if (Directory.Exists(SrcDirPath))
                {
                    DestDirPath = Path.Combine(Directory.GetParent(dirPath)?.FullName ?? string.Empty,
                                               MOVED_PREFIX + Path.GetFileName(dirPath));
                    if (Directory.Exists(DestDirPath))
                    {
                        DestDirPath = DirectoryEx.GetUniqueName(DestDirPath);
                    }

                    Helpers.TryTwice(() => Directory.Move(SrcDirPath, DestDirPath),
                        $@"Directory.Move({SrcDirPath}, {DestDirPath})");
                }
            }
        }

        private string SrcDirPath { get; set; }
        private string DestDirPath { get; set; }

        public void Dispose()
        {
            if (SrcDirPath != null)
            {
                // Get rid of the tools directory created for this test
                if (Directory.Exists(SrcDirPath))
                    Helpers.TryTwice(() => Directory.Delete(SrcDirPath, true), 10, 1000, // More generous retry than default Helper.TryTwice: 10, with 1 sec pause
                        $@"Directory.Delete({SrcDirPath})");
                // If there was an existing tools directory move it back
                if (DestDirPath != null)
                    Helpers.TryTwice(() => Directory.Move(DestDirPath, SrcDirPath), 10, 1000,  // More generous retry than default Helper.TryTwice: 10, with 1 sec pause
                        $@"Directory.Move({DestDirPath},{SrcDirPath})");
            }
        }
    }
}
