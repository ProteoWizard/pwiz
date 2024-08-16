/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
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


using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify operation of Waters lockmass correction - specifically, our ability to determine which function
    /// is lockmass and thus to be ignored by Skyline. In this data set that's Function 2 (more typically it's
    /// in Function 3, but this is MS1-only data)
    ///
    /// Without the fix, this test will fail as pwiz will try to process function 2 as normal IMS data, which it isn't
    /// 
    /// </summary>
    [TestClass]
    public class DetectLockmassFunctionTest : AbstractFunctionalTestEx
    {

        [TestMethod] 
        public void WatersDetectLockmassFunctionPerfTest()
        {
            TestFilesZip = GetPerfTestDataURL(@"TestLockmassFunction2.zip");
            TestFilesPersistent = new[] { "220621008.raw" }; // List of files that we'd like to unzip alongside parent zipFile, and (re)use in place

            RunFunctionalTest();
            
        }

        private string GetTestPath(string relativePath)
        {
            return TestFilesDirs[0].GetTestPath(relativePath);
        }


        protected override void DoTest()
        {
            var skyfile = TestFilesDir.GetTestPath("TestLockmassFunction2.sky");
            const double lockmassPositive = 556.2771;
            const double lockmassNegative = 554.2615;
            const double lockmassToler = 0.25; 

            RunUI(() => SkylineWindow.OpenFile(skyfile));

            var doc0 = WaitForDocumentLoaded();
            AssertEx.IsDocumentState(doc0, null, 24, 24, 24, 24);

            ImportResults(GetTestPath(TestFilesPersistent[0]),
                new LockMassParameters(lockmassPositive, lockmassNegative, lockmassToler));

            var document = WaitForDocumentLoaded(400000);

            // If we get here, problem is solved

            // delete lockmass file
            var lmgtFile = Path.Combine(GetTestPath(TestFilesPersistent[0]), "lmgt.inf");
            FileEx.SafeDelete(lmgtFile);
        }
    }
}
