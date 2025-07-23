/*
 * Original author: Aaron Banse <abanse .at. uw dot edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *         
 * Copyright 2025 University of Washington - Seattle, WA
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    // Test to see if there is less than 20GB of space left on disk.
    // Necessary to prevent unrelated tests failing when an issue causes disk to fill up.
    [TestClass]
    public class DiskSpaceTest : AbstractUnitTest
    {
        private const long MIN_SPACE_BYTES = 20*1024*1024*1024L;

        // Name changed so that test is fired early in test process.
        //[TestMethod]
        public void AaaTestIsDiskFull()
        {
            var rootPath = Path.GetPathRoot(TestContext.TestDir);
            var drive = new DriveInfo(rootPath);
            Assert.IsTrue(drive.TotalFreeSpace >= MIN_SPACE_BYTES, $"Warning: this machine is running out of disk space. The {rootPath} drive has {drive.TotalFreeSpace} bytes of space remaining, which is {100.0 * drive.TotalFreeSpace / drive.TotalSize}% of total space. Running out of space will cause other tests to fail.");
        }
    }
}
