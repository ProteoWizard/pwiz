/*
 * Original author: Aaron Banse <acbanse .at. icloud dot com>,
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
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Files;
using pwiz.SkylineTestUtil;
using System.ComponentModel;
using pwiz.Skyline.Controls.FilesTree;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FilesTreePropertyGridTest : AbstractFunctionalTest
    {
        internal const int REPLICATE_EXPECTED_PROP_NUM = 5;
        private const string NAME_PROP_NAME = "Name";

        [TestMethod]
        public void TestFilesTreePropertyGrid()
        {
            // These test files are large (90MB) so reuse rather than duplicate
            TestFilesZip = FilesTreeFormTest.TEST_FILES_ZIP;
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            VerifySetup();

            TestSelectedNodeProperties();

            TestAnnotationProperties();

            TestEditProperty();

            CloseForms();
        }

        private void VerifySetup()
        {
            var documentPath = TestFilesDir.GetTestPath(FilesTreeFormTest.RAT_PLASMA_FILE_NAME);
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            Assert.IsNull(SkylineWindow.PropertyGridForm);
            Assert.IsFalse(SkylineWindow.PropertyGridFormIsVisible);
            Assert.IsFalse(SkylineWindow.PropertyGridFormIsActivated);

            RunUI(() => { SkylineWindow.ShowFilesTreeForm(true); });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

            RunUI(() => { SkylineWindow.ShowPropertyGridForm(true); });
            WaitForConditionUI(() => SkylineWindow.PropertyGridFormIsVisible);
        }

        private static void TestSelectedNodeProperties()
        {
            // test selecting a replicate node
            var replicateFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var replicateNode = SkylineWindow.FilesTree.File<Replicate>(replicateFolder);
            Assert.IsNotNull(replicateNode);
            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.FilesTree.SelectNode(replicateNode, true);
                SkylineWindow.FocusPropertyProvider(SkylineWindow.FilesTreeForm);
            });
            var selectedObject = SkylineWindow.PropertyGridForm?.GetPropertyObject();
            Assert.IsNotNull(selectedObject);

            // smoke test check total number of properties - this failing could just mean another relevant property was added
            var props = TypeDescriptor.GetProperties(selectedObject, false);
            Assert.AreEqual(REPLICATE_EXPECTED_PROP_NUM, props.Count);

            // test globalizedPropertyDescriptor property for localization
            var nameProp = props[NAME_PROP_NAME];
            Assert.IsNotNull(nameProp);
            Assert.AreEqual(nameProp.Name, NAME_PROP_NAME);
            Assert.AreEqual(selectedObject.GetResourceManager().GetString(nameProp.Name), nameProp.DisplayName);
        }

        private static void TestAnnotationProperties()
        {
            // Add annotation definitions for replicates
            RunUI(() => { PropertyGridTestUtil.TestAddAnnotations(SkylineWindow, AnnotationDef.AnnotationTarget.replicate); });

            // Select replicate node
            var replicateFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var replicateNode = SkylineWindow.FilesTree.File<Replicate>(replicateFolder);
            Assert.IsNotNull(replicateNode);
            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.FilesTree.SelectNode(replicateNode, true);
                SkylineWindow.FocusPropertyProvider(SkylineWindow.FilesTreeForm);
            });
            var selectedObject = SkylineWindow.PropertyGridForm?.GetPropertyObject();
            Assert.IsNotNull(selectedObject);
            
            // Test if sum of original properties and new annotation definition properties appear
            var props = TypeDescriptor.GetProperties(selectedObject, false);
            Assert.AreEqual(REPLICATE_EXPECTED_PROP_NUM + SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.Count, props.Count);

            // Test editing annotation properties
            RunUI(() => { PropertyGridTestUtil.TestEditAnnotations(SkylineWindow); });
        }

        private static void TestEditProperty()
        {
            var replicateFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var replicateNode = SkylineWindow.FilesTree.File<Replicate>(replicateFolder);
            Assert.IsNotNull(replicateNode);

            // Select the replicate node so its properties appear in the property sheet
            RunUI(() =>
            {
                SkylineWindow.ShowPropertyGridForm(true);
                SkylineWindow.FilesTreeForm.FilesTree.SelectNode(replicateNode, true);
                SkylineWindow.FocusPropertyProvider(SkylineWindow.FilesTreeForm);
            });

            // Edit the Name property and verify the change was made on the object and UI
            RunUI(() => { PropertyGridTestUtil.TestEditProperty(SkylineWindow, NAME_PROP_NAME, "EditedName"); });
        }

        private static void CloseForms()
        {
            var filesTreeForm = FindOpenForm<FilesTreeForm>();
            Assert.IsNotNull(filesTreeForm);
            OkDialog(filesTreeForm, filesTreeForm.Close);

            var propertyGridForm = FindOpenForm<PropertyGridForm>();
            Assert.IsNotNull(propertyGridForm);
            OkDialog(propertyGridForm, propertyGridForm.Close);
        }
    }
}
