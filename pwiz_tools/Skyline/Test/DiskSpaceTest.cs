/*
 * Original author: Aaron Banse <abanse .at. uw dot edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    // Test to ensure there is enough space on the disk before starting.
    public class DiskSpaceTest : AbstractUnitTest
    {
        private const double MIN_SPACE_PERCENT = .05;

        [TestMethod]
        public void TestIsDiskFull()
        {
            DriveInfo drive = new DriveInfo(Path.GetPathRoot(TestContext.TestDir));
            Assert.IsTrue((double)drive.TotalFreeSpace / drive.TotalSize > MIN_SPACE_PERCENT, 
                $"The disk has less than {MIN_SPACE_PERCENT * 100}% of space remaining.");
        }
    }
}
