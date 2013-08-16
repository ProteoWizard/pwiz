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
    /// </summary>
    public class MovedDirectory : IDisposable
    {
        public const string MOVED_PREFIX = "Moved_"; // Not L10N

        public MovedDirectory(string dirPath, bool isLoopingTest)
        {
            LoopingTest = isLoopingTest;
            if (isLoopingTest)
            {
                SrcDirPath = dirPath;
                DestDirPath = Path.Combine(Directory.GetParent(dirPath).FullName,
                                           MOVED_PREFIX + Path.GetFileName(dirPath));
                if (Directory.Exists(DestDirPath))
                {
                    DestDirPath = DirectoryEx.GetUniqueName(DestDirPath);
                }

                Helpers.TryTwice(() => Directory.Move(SrcDirPath, DestDirPath));
            }
        }

        public string SrcDirPath { get; private set; }
        public string DestDirPath { get; private set; }
        public bool LoopingTest { get; private set; }

        public void Dispose()
        {
            if (LoopingTest)
            {
                if (Directory.Exists(SrcDirPath))
                    Helpers.TryTwice(() => Directory.Delete(SrcDirPath));
                Helpers.TryTwice(() => Directory.Move(DestDirPath, SrcDirPath));
            }
        }
    }
}
