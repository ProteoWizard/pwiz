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
using pwiz.Skyline.Controls.FilesTree;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Files;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FilesTreePropertyGridTest : AbstractFunctionalTest
    {
        private const string NAME_PROP_NAME = "Name";
        private const string FILES_TREE_EXPECTED_PROPS_PREFIX = "files-tree";
        private const string SPECTRAL_LIBRARY_CASE_PROPS_PREFIX = "speclib-with-datafiles";

        private Dictionary<string, Dictionary<string, string>> _expectedProperties;

        protected override bool IsRecordMode => false;

        [TestMethod]
        public void TestFilesTreePropertyGrid()
        {
            TestFilesZipPaths = new[] { PropertyGridTestUtil.TEST_FILES_ZIP };

            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            Setup(Path.Combine(TestFilesDirs[0].FullPath, @"Rat_plasma.sky"));

            // setup expected properties
            _expectedProperties = PropertyGridTestUtil.ReadAllExpectedPropertyValues(FILES_TREE_EXPECTED_PROPS_PREFIX, IsRecordMode);

            // Test each data object if they display expected properties
            TestReplicateProperties();
            TestSpectralLibraryProperties(0);

            // In record mode, write out expected properties so they can be compared in future test runs
            if (IsRecordMode) PropertyGridTestUtil.WriteAllExpectedPropertyValues(FILES_TREE_EXPECTED_PROPS_PREFIX, _expectedProperties);

            // Test annotation properties and editing
            TestAnnotationProperties();
            TestEditProperty();
            TestHandleReplicateNameException();

            // Test spectral library with datafiles case
            _expectedProperties = PropertyGridTestUtil.ReadAllExpectedPropertyValues(SPECTRAL_LIBRARY_CASE_PROPS_PREFIX, IsRecordMode);
            TestSpectralLibraryProperties(1);
            if (IsRecordMode) PropertyGridTestUtil.WriteAllExpectedPropertyValues(SPECTRAL_LIBRARY_CASE_PROPS_PREFIX, _expectedProperties);

            CloseForms();
        }

        private static void Setup(string documentPath)
        {
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            RunUI(() => { SkylineWindow.ShowFilesTreeForm(true); });
            WaitForConditionUI(() => SkylineWindow.FilesTreeFormIsVisible);

            RunUI(() => { SkylineWindow.ShowPropertyGridForm(true); });
            WaitForConditionUI(() => SkylineWindow.PropertyGridFormIsVisible);
        }

        private void TestReplicateProperties()
        {
            var folder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var replicateNode = folder.Nodes[0] as FilesTreeNode;
            Assert.IsNotNull(replicateNode);
            RunUI(() => { SkylineWindow.FilesTreeForm.FilesTree.SelectedNode = replicateNode; });

            PropertyGridTestUtil.LogOrTestExpectedPropertyValues(SkylineWindow, _expectedProperties,
                nameof(Skyline.Model.Databinding.Entities.Replicate), IsRecordMode);
        }

        private void TestSpectralLibraryProperties(int specLibIndex)
        {
            var folder = SkylineWindow.FilesTree.Folder<SpectralLibrariesFolder>();
            var spectralLibraryNode = folder.Nodes[specLibIndex] as FilesTreeNode;
            Assert.IsNotNull(spectralLibraryNode);
            RunUI(() => { SkylineWindow.FilesTreeForm.FilesTree.SelectedNode = spectralLibraryNode; });

            PropertyGridTestUtil.LogOrTestExpectedPropertyValues(SkylineWindow, _expectedProperties,
                nameof(Skyline.Model.Databinding.Entities.SpectralLibrary), IsRecordMode);
        }

        private void TestAnnotationProperties()
        {
            // Add annotation definitions for replicates
            RunUI(() => { PropertyGridTestUtil.TestAddAnnotations(SkylineWindow, AnnotationDef.AnnotationTarget.replicate); });

            // Select replicate node
            var replicateFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var replicateNode = SkylineWindow.FilesTree.File<Replicate>(replicateFolder);
            Assert.IsNotNull(replicateNode);
            RunUI(() => { SkylineWindow.FilesTreeForm.FilesTree.SelectedNode = replicateNode; });
            var selectedObject = SkylineWindow.PropertyGridForm?.GetPropertyObject();
            Assert.IsNotNull(selectedObject);
            
            // Test if sum of original properties and new annotation definition properties appear
            var props = TypeDescriptor.GetProperties(selectedObject, false);
            Assert.AreEqual(props.Count,
                _expectedProperties[nameof(Skyline.Model.Databinding.Entities.Replicate)].Count 
                + SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.Count);

            // Test editing annotation properties
            RunUI(() => { PropertyGridTestUtil.TestEditAnnotations(SkylineWindow); });
        }

        private static void TestEditProperty()
        {
            // select replicate
            var replicateFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var replicateNode = SkylineWindow.FilesTree.File<Replicate>(replicateFolder);
            Assert.IsNotNull(replicateNode);
            RunUI(() => { SkylineWindow.FilesTreeForm.FilesTree.SelectedNode = replicateNode; });

            // Edit the Name property of the selected object and verify the change was made on the object and UI
            RunUI(() => { PropertyGridTestUtil.TestEditProperty(SkylineWindow, NAME_PROP_NAME, "EditedName"); });
        }

        private static void TestHandleReplicateNameException()
        {
            // select replicate
            var replicateFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var replicateNode = replicateFolder.Nodes[0] as FilesTreeNode;
            Assert.IsNotNull(replicateNode);
            RunUI(() => { SkylineWindow.FilesTreeForm.FilesTree.SelectedNode = replicateNode; });

            // get another replicate to use its name for testing
            var otherReplicateNode = replicateFolder.Nodes[1] as FilesTreeNode;
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
