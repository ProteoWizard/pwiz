/*
 * Original author: Aaron Banse <acbanse .at. acbanse dot com>,
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Files;
using pwiz.SkylineTestUtil;
using System.ComponentModel;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PropertySheetTest : AbstractFunctionalTest
    {
        internal const string RAT_PLASMA_FILE_NAME = "Rat_plasma.sky";
        internal const int REP_SAMPLE_FILE_PROP_NUM = 5;

        [TestMethod]
        public void TestPropertySheet()
        {
            TestFilesZip = @"TestFunctional\FilesTreeFormTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestFileNodeProperties();
        }

        protected void TestFileNodeProperties()
        {
            var documentPath = TestFilesDir.GetTestPath(RAT_PLASMA_FILE_NAME);
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            Assert.IsNull(SkylineWindow.PropertyForm);
            Assert.IsFalse(SkylineWindow.PropertyFormIsVisible);
            Assert.IsFalse(SkylineWindow.PropertyFormIsActivated);


            RunUI(() => { SkylineWindow.ShowFilesTreeForm(true); });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

            RunUI(() => { SkylineWindow.ShowPropertyForm(true); });
            WaitForConditionUI(() => SkylineWindow.PropertyFormIsVisible);

            // test selecting a replicate sample file node
            var replicateNode = SkylineWindow.FilesTree.Folder<ReplicatesFolder>().NodeAt(0);
            RunUI(() => SkylineWindow.FilesTreeForm.FilesTree.SelectNodeWithoutResettingSelection(replicateNode));

            var selectedObject = SkylineWindow.PropertyForm?.PropertyGrid.SelectedObject;

            Assert.IsNotNull(selectedObject);

            Assert.AreEqual(typeof(ReplicateSampleFileProperties), selectedObject.GetType());

            var props = TypeDescriptor.GetProperties(selectedObject, true);

            Assert.AreEqual(REP_SAMPLE_FILE_PROP_NUM, props.Count);

            RunUI(() => { SkylineWindow.DestroyPropertyForm(); });
            RunUI(() => { SkylineWindow.DestroyFilesTreeForm(); });
        }
    }
}
