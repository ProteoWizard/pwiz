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
using pwiz.Skyline;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.FilesTree;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Files;
using pwiz.SkylineTestUtil;
using System;
using System.ComponentModel;
using System.IO;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FilesTreePropertyGridTest : AbstractFunctionalTest
    {
        private const int REPLICATE_EXPECTED_PROP_NUM = 3;
        private const string NAME_PROP_NAME = "Name";

        [TestMethod]
        public void TestFilesTreePropertyGrid()
        {
            TestFilesZipPaths = new[] { PropertyGridTestUtil.TEST_FILES_ZIP };

            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            VerifySetup();

            TestReplicateProperties();

            TestAnnotationProperties();

            TestEditProperty();

            TestHandleReplicateNameException();

            CloseForms();
        }

        private void VerifySetup()
        {
            var documentPath = Path.Combine(TestFilesDirs[0].FullPath, @"Main", @"test.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            Assert.IsNull(SkylineWindow.PropertyGridForm);
            Assert.IsFalse(SkylineWindow.PropertyGridFormIsVisible);
            Assert.IsFalse(SkylineWindow.PropertyGridFormIsActivated);

            RunUI(() => { SkylineWindow.ShowFilesTreeForm(true); });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

            RunUI(() => { SkylineWindow.ShowPropertyGridForm(true); });
            WaitForConditionUI(() => SkylineWindow.PropertyGridFormIsVisible);
        }

        private static void TestReplicateProperties()
        {
            // test selecting a replicate node
            var replicateFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var replicateNode = SkylineWindow.FilesTree.File<Replicate>(replicateFolder);
            Assert.IsNotNull(replicateNode);
            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.FilesTree.SelectedNode = replicateNode;
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
            // select replicate
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

            // Edit the Name property of the selected object and verify the change was made on the object and UI
            RunUI(() => { PropertyGridTestUtil.TestEditProperty(SkylineWindow, NAME_PROP_NAME, "EditedName"); });
        }

        private static void TestHandleReplicateNameException()
        {
            // select replicate
            var replicateFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var replicateNode = replicateFolder.Nodes[0] as FilesTreeNode;
            var otherReplicateNode = replicateFolder.Nodes[1] as FilesTreeNode;
            Assert.IsNotNull(replicateNode);
            Assert.IsNotNull(otherReplicateNode);

            // attempt to edit the name to the name of another replicate. Should throw an exception.
            RunUI(() =>
            {
                var selectedObject = SkylineWindow.PropertyGridForm.GetPropertyObject();
                Assert.IsNotNull(selectedObject);
                var prop = TypeDescriptor.GetProperties(selectedObject, false)[NAME_PROP_NAME];
                try
                {
                    prop.SetValue(selectedObject, otherReplicateNode.Name);
                    // this should fail, if not, fail the test
                    Assert.Fail("Setting replicate name to an existing replicate name should throw an exception.");
                }
                catch (ArgumentException)
                {
                    // Expected exception, verify that the name was not changed
                    Assert.IsFalse(prop.GetValue(selectedObject)?.Equals(otherReplicateNode.Name) ?? true);
                }
            });
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
