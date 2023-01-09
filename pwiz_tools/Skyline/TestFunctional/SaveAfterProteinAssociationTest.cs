/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2022 University of Washington - Seattle, WA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SaveAfterProteinAssociationTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestSaveAfterProteinAssociation()
        {
            TestFilesZip = @"TestFunctional\SaveAfterProteinAssociationTest.zip";
            RunFunctionalTest();
        }

        /// <summary>
        /// Test that protein metadata can be saved with a protein group.
        /// </summary>
        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SaveAfterProteinAssociationTest.sky")));
            WaitForDocumentLoaded();
            WaitForProteinMetadataBackgroundLoaderCompleted();
            RunUI(() => SkylineWindow.SaveDocument());
        }
    }
}
