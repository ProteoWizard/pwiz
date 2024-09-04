/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ErrorIfPersistentFilesDirModifiedTest : AbstractUnitTestEx
    {
        [TestMethod, NoParallelTesting(TestExclusionReason.SHARED_DIRECTORY_WRITE)]
        public void TestErrorIfPersistentFilesDirModified()
        {
            TestFilesZip = "https://skyline.ms/tutorials/OptimizeCEMzml.zip";
            TestFilesPersistent = new[] { "CE_Vantage" };
            TestFilesDir = new TestFilesDir(TestContext, TestFilesZip, TestContext.TestName, TestFilesPersistent);
            string persistentFile = TestFilesDir.GetTestPath("OptimizeCEMzml/CE_Vantage_15mTorr_0001.mzML");
            string persistentFileCopy = persistentFile + ".copy";
            try
            {
                FileEx.SafeDelete(persistentFileCopy, true);
                File.Copy(persistentFile, persistentFileCopy);
                AssertEx.ThrowsException<IOException>(() => TestFilesDir.Cleanup(),
                    ex => StringAssert.Contains(ex.Message, $"PersistentFilesDir ({TestFilesDir.PersistentFilesDir}) has been modified"));
            }
            finally
            {
                File.Delete(persistentFileCopy);
            }
        }
    }
}
