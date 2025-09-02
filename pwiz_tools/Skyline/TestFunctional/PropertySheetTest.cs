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

using System;
using System.ComponentModel;
using pwiz.Skyline.Model.Files;
using pwiz.SkylineTestUtil;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PropertySheetTest : AbstractFunctionalTest
    {
        internal const int REP_FILE_PROP_NUM = 10;
        internal const int REP_SAMPLE_FILE_PROP_NUM = 10;

        [TestMethod]
        public void TestPropertySheet()
        {
            // These test files are large (90MB) so reuse rather than duplicate
            TestFilesZip = FilesTreeFormTest.TEST_FILES_ZIP;
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestFileNodeProperties();
        }

        protected void TestFileNodeProperties()
        {
            var documentPath = TestFilesDir.GetTestPath(FilesTreeFormTest.RAT_PLASMA_FILE_NAME);
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            Assert.IsNull(SkylineWindow.PropertyForm);
            Assert.IsFalse(SkylineWindow.PropertyFormIsVisible);
            Assert.IsFalse(SkylineWindow.PropertyFormIsActivated);

            RunUI(() => { SkylineWindow.ShowFilesTreeForm(true); });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

            RunUI(() => { SkylineWindow.ShowPropertyForm(true); });
            WaitForConditionUI(() => SkylineWindow.PropertyFormIsVisible);

            // test selecting a replicate node
            var replicateFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var replicateNode = SkylineWindow.FilesTree.File<Replicate>(replicateFolder);
            Assert.IsNotNull(replicateNode);

            RunUI(() => SkylineWindow.FilesTreeForm.FilesTree.SelectNodeWithoutResettingSelection(replicateNode));
            RunUI(() => SkylineWindow.FilesTreeForm.NotifyPropertySheetOwnerGotFocus(SkylineWindow, EventArgs.Empty));

            var selectedObject = SkylineWindow.PropertyForm?.PropertyGrid.SelectedObject;
            Assert.IsNotNull(selectedObject);
            Assert.AreEqual(typeof(ReplicateProperties), selectedObject.GetType());

            var props = TypeDescriptor.GetProperties(selectedObject, false);
            // only non-null properties end up in the property sheet
            Assert.AreEqual(REP_FILE_PROP_NUM, props.Count);

            // test selecting a replicate sample file node
            var sampleFileNode = SkylineWindow.FilesTree.File<ReplicateSampleFile>(replicateNode);
            Assert.IsNotNull(sampleFileNode);

            RunUI(() => SkylineWindow.FilesTreeForm.FilesTree.SelectNodeWithoutResettingSelection(sampleFileNode));
            RunUI(() => SkylineWindow.FilesTreeForm.NotifyPropertySheetOwnerGotFocus(SkylineWindow, EventArgs.Empty));

            selectedObject = SkylineWindow.PropertyForm?.PropertyGrid.SelectedObject;
            Assert.IsNotNull(selectedObject);
            Assert.AreEqual(typeof(ReplicateSampleFileProperties), selectedObject.GetType());

            props = TypeDescriptor.GetProperties(selectedObject, false);
            // only non-null properties end up in the property sheet
            Assert.AreEqual(REP_SAMPLE_FILE_PROP_NUM, props.Count);

            // Destroy the property form and files tree form to avoid test freezing
            RunUI(() => { SkylineWindow.DestroyPropertyForm(); });
            RunUI(() => { SkylineWindow.DestroyFilesTreeForm(); });
        }
    }
}
