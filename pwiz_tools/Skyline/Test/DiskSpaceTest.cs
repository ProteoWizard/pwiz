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
    // Test to see if there is less than 5% of space left on disk.
    // Necessary to prevent unrelated tests failing when an issue causes disk to fill up.
    [TestClass]
    public class DiskSpaceTest : AbstractUnitTest
    {
        private const double MIN_SPACE_PERCENT = .05;

        [TestMethod]
        public void TestIsDiskFull()
        {
            var rootPath = Path.GetPathRoot(TestContext.TestDir);
            var drive = new DriveInfo(rootPath);
            var errorMessage = $"Warning: this machine is running out of disk space. The {rootPath} drive has less than {MIN_SPACE_PERCENT * 100}% of space remaining. Running out of space will cause other tests to fail.";
            Assert.IsTrue((double)drive.TotalFreeSpace / drive.TotalSize > MIN_SPACE_PERCENT, errorMessage);
        }
    }
}
