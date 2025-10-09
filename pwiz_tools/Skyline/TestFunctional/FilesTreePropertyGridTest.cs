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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Files;
using pwiz.SkylineTestUtil;
using System;
using System.ComponentModel;
using System.Linq;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FilesTreePropertyGridTest : AbstractFunctionalTest
    {
        // Both 10, but have different props summing to that
        internal const int REP_FILE_PROP_NUM = 5;
        internal const int REP_SAMPLE_FILE_PROP_NUM = 10;

        private const string STRING_ANNOTATION_NAME = "StringAnnotation";
        private const string NUMBER_ANNOTATION_NAME = "NumberAnnotation";
        private const string BOOL_ANNOTATION_NAME = "BoolAnnotation";
        private const string LIST_ANNOTATION_NAME = "ListAnnotation";
        private const string ANNOTATION_NAME_PREFIX = "annotation_";
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

            TestSelectedFileNodeProperties();

            TestAnnotationProperties();

            TestEditProperties();

            TestEditPropertiesUI();

            // Destroy the property form and files tree form to avoid test freezing
            RunUI(() =>
            {
                SkylineWindow.DestroyPropertyGridForm();
                SkylineWindow.DestroyFilesTreeForm();
            });
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

        private static void TestSelectedFileNodeProperties()
        {
            // test selecting a replicate node
            var replicateFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var replicateNode = SkylineWindow.FilesTree.File<Replicate>(replicateFolder);
            Assert.IsNotNull(replicateNode);

            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.FilesTree.SelectNodeWithoutResettingSelection(replicateNode);
                SkylineWindow.FocusPropertyProvider(SkylineWindow.FilesTreeForm);
            });

            var selectedObject = SkylineWindow.PropertyGridForm?.GetPropertyObject();
            Assert.IsNotNull(selectedObject);

            var props = TypeDescriptor.GetProperties(selectedObject, false);
            // only non-null properties end up in the property sheet
            Assert.AreEqual(REP_FILE_PROP_NUM, props.Count);

            // test globalizedPropertyDescriptor property for localization
            var nameProp = props[NAME_PROP_NAME];
            Assert.IsNotNull(nameProp);
            Assert.AreEqual(nameProp.Name, NAME_PROP_NAME);
            Assert.AreEqual(selectedObject.GetResourceManager().GetString(nameProp.Name), nameProp.DisplayName);

            // NOT IMPLEMENTED YET

            /*
            // test selecting a replicate sample file node
            var sampleFileNode = SkylineWindow.FilesTree.File<ReplicateSampleFile>(replicateNode);
            Assert.IsNotNull(sampleFileNode);
            
            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.FilesTree.SelectNodeWithoutResettingSelection(sampleFileNode);
                SkylineWindow.FocusPropertyProvider(SkylineWindow.FilesTreeForm);
            });

            selectedObject = SkylineWindow.PropertyGridForm?.GetPropertyObject();
            Assert.IsNotNull(selectedObject);

            props = TypeDescriptor.GetProperties(selectedObject, false);
            // only non-null properties end up in the property sheet
            Assert.AreEqual(REP_SAMPLE_FILE_PROP_NUM, props.Count);

            // test globalizedPropertyDescriptor property for localization
            // must give string name, not nameof() because it's added dynamically
            const string instrumentModelPropName = "Model";
            var modelProp = props[instrumentModelPropName];
            Assert.IsNotNull(modelProp);
            Assert.AreEqual(modelProp.Name, instrumentModelPropName);
            Assert.AreEqual(selectedObject.GetResourceManagerForTest().GetString(modelProp.Name), modelProp.DisplayName); */
        }

        private static void TestAnnotationProperties()
        {
            var replicateFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var replicateNode = SkylineWindow.FilesTree.File<Replicate>(replicateFolder);
            Assert.IsNotNull(replicateNode);

            // Add annotation definitions for replicates
            AddAnnotations((Replicate)replicateNode.Model);

            // Re-select the replicate node to verify that the new annotation properties appear
            replicateNode = SkylineWindow.FilesTree.File<Replicate>(replicateFolder);
            Assert.IsNotNull(replicateNode);

            RunUI(() =>
            {
                SkylineWindow.FilesTreeForm.FilesTree.SelectNodeWithoutResettingSelection(replicateNode);
                SkylineWindow.FocusPropertyProvider(SkylineWindow.FilesTreeForm);
            });

            var selectedObject = SkylineWindow.PropertyGridForm?.GetPropertyObject();
            Assert.IsNotNull(selectedObject);

            var props = TypeDescriptor.GetProperties(selectedObject, false);
            // Test if sum of original properties and new annotation definition properties appear
            Assert.AreEqual(REP_FILE_PROP_NUM + SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.Count, props.Count);
        }

        private static void AddAnnotations(Replicate replicate)
        {
            const string stringAnnotationValue = "TestValue";
            const double numberAnnotationValue = 42;
            const bool boolAnnotationValue = true;
            const string listAnnotationValue = "B";

            // Define four annotation definitions for replicates: string, number, bool, value list
            var replicateAnnotationDefs = new[]
            {
                new AnnotationDef(
                    STRING_ANNOTATION_NAME,
                    AnnotationDef.AnnotationTargetSet.Singleton(
                        AnnotationDef.AnnotationTarget.replicate),
                    AnnotationDef.AnnotationType.text,
                    Array.Empty<string>()),

                new AnnotationDef(
                    NUMBER_ANNOTATION_NAME,
                    AnnotationDef.AnnotationTargetSet.Singleton(
                        AnnotationDef.AnnotationTarget.replicate),
                    AnnotationDef.AnnotationType.number,
                    Array.Empty<string>()),

                new AnnotationDef(
                    BOOL_ANNOTATION_NAME,
                    AnnotationDef.AnnotationTargetSet.Singleton(
                        AnnotationDef.AnnotationTarget.replicate),
                    AnnotationDef.AnnotationType.true_false,
                    Array.Empty<string>()),

                new AnnotationDef(
                    LIST_ANNOTATION_NAME,
                    AnnotationDef.AnnotationTargetSet.Singleton(
                        AnnotationDef.AnnotationTarget.replicate),
                    AnnotationDef.AnnotationType.value_list,
                    new[] { "A", "B", "C" })
            };

            // Add annotation definitions to the document
            RunUI(() =>
            {
                var doc = SkylineWindow.Document;
                var newSettings = doc.Settings.ChangeDataSettings(
                    doc.Settings.DataSettings.ChangeAnnotationDefs(replicateAnnotationDefs));
                SkylineWindow.ModifyDocument("Add replicate annotation definitions", d => d.ChangeSettings(newSettings));
            });

            var defs = SkylineWindow.Document.Settings.DataSettings.AnnotationDefs;
            var defString = defs.FirstOrDefault(def => def.Name == STRING_ANNOTATION_NAME);
            var defNumber = defs.FirstOrDefault(def => def.Name == NUMBER_ANNOTATION_NAME);
            var defBool = defs.FirstOrDefault(def => def.Name == BOOL_ANNOTATION_NAME);
            var defList = defs.FirstOrDefault(def => def.Name == LIST_ANNOTATION_NAME);

            Assert.IsNotNull(defString);
            Assert.IsNotNull(defNumber);
            Assert.IsNotNull(defBool);
            Assert.IsNotNull(defList);

            // Apply annotation values to the first replicate using Replicate.EditAnnotation
            RunUI(() =>
            {
                using var monitor = new SrmSettingsChangeMonitor(null, "Edit replicate annotations", SkylineWindow);

                var newDoc = Replicate.EditAnnotation(SkylineWindow.Document, monitor, replicate, defString, stringAnnotationValue);
                newDoc = Replicate.EditAnnotation(newDoc.Document, monitor, replicate, defNumber, numberAnnotationValue);
                newDoc = Replicate.EditAnnotation(newDoc.Document, monitor, replicate, defBool, boolAnnotationValue);
                newDoc = Replicate.EditAnnotation(newDoc.Document, monitor, replicate, defList, listAnnotationValue);

                SkylineWindow.ModifyDocument("Set replicate annotation values", _ => newDoc.Document);

                SkylineWindow.SaveDocument();
            });

            // Verify that the annotation values were applied correctly to document
            var doc = SkylineWindow.Document;
            var chromSet = doc.MeasuredResults.Chromatograms[0];
            var annotations = chromSet.Annotations;
            Assert.AreEqual(stringAnnotationValue, annotations.GetAnnotation(defString));
            Assert.AreEqual(numberAnnotationValue, annotations.GetAnnotation(defNumber));
            Assert.AreEqual(boolAnnotationValue, annotations.GetAnnotation(defBool));
            Assert.AreEqual(listAnnotationValue, annotations.GetAnnotation(defList));
        }

        private static void TestEditProperties()
        {
            const string stringEditedValue = "EditedString";
            const double numberEditedValue = 123.45;
            const bool boolEditedValue = false;
            const string listEditedValue = "C";

            var replicateFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var replicateNode = SkylineWindow.FilesTree.File<Replicate>(replicateFolder);
            Assert.IsNotNull(replicateNode);

            // Select the replicate node so its properties appear in the property sheet
            RunUI(() =>
            {
                SkylineWindow.ShowPropertyGridForm(true);
                SkylineWindow.FilesTreeForm.FilesTree.SelectNodeWithoutResettingSelection(replicateNode);
                SkylineWindow.FocusPropertyProvider(SkylineWindow.FilesTreeForm);
            });

            var defs = SkylineWindow.Document.Settings.DataSettings.AnnotationDefs;
            var defString = defs.FirstOrDefault(def => def.Name == STRING_ANNOTATION_NAME);
            var defNumber = defs.FirstOrDefault(def => def.Name == NUMBER_ANNOTATION_NAME);
            var defBool = defs.FirstOrDefault(def => def.Name == BOOL_ANNOTATION_NAME);
            var defList = defs.FirstOrDefault(def => def.Name == LIST_ANNOTATION_NAME);

            Assert.IsNotNull(defString);
            Assert.IsNotNull(defNumber);
            Assert.IsNotNull(defBool);
            Assert.IsNotNull(defList);

            // Edit annotation properties through the PropertyGrid
            Assert.IsNotNull(SkylineWindow.PropertyGridForm);

            var selectedObject = SkylineWindow.PropertyGridForm.GetPropertyObject();
            Assert.IsNotNull(selectedObject);
            var replicateStringProp = TypeDescriptor.GetProperties(selectedObject, false)[ANNOTATION_NAME_PREFIX + STRING_ANNOTATION_NAME];
            RunUI(() =>
            {
                replicateStringProp?.SetValue(selectedObject, stringEditedValue);
            });
            selectedObject = SkylineWindow.PropertyGridForm.GetPropertyObject();
            var replicateNumberProp = TypeDescriptor.GetProperties(selectedObject, false)[ANNOTATION_NAME_PREFIX + NUMBER_ANNOTATION_NAME];
            RunUI(() =>
            {
                replicateNumberProp?.SetValue(selectedObject, numberEditedValue);
            });
            selectedObject = SkylineWindow.PropertyGridForm.GetPropertyObject();
            var replicateBoolProp = TypeDescriptor.GetProperties(selectedObject, false)[ANNOTATION_NAME_PREFIX + BOOL_ANNOTATION_NAME];
            RunUI(() =>
            {
                replicateBoolProp?.SetValue(selectedObject, boolEditedValue);
            });
            selectedObject = SkylineWindow.PropertyGridForm.GetPropertyObject();
            var replicateListProp = TypeDescriptor.GetProperties(selectedObject, false)[ANNOTATION_NAME_PREFIX + LIST_ANNOTATION_NAME];
            RunUI(() =>
            {
                replicateListProp?.SetValue(selectedObject, listEditedValue);
            });

            var doc = SkylineWindow.Document;
            var chromSetId = replicateNode.Model.IdentityPath.GetIdentity(0);
            doc.MeasuredResults.TryGetChromatogramSet(chromSetId.GlobalIndex, out var chromSet, out var _);
            var annotations = chromSet.Annotations;
            Assert.AreEqual(stringEditedValue, annotations.GetAnnotation(defString));
            Assert.AreEqual(numberEditedValue, annotations.GetAnnotation(defNumber));
            Assert.AreEqual(boolEditedValue, annotations.GetAnnotation(defBool));
            Assert.AreEqual(listEditedValue, annotations.GetAnnotation(defList));
        }

        private static void TestEditPropertiesUI()
        {
            var replicateFolder = SkylineWindow.FilesTree.Folder<ReplicatesFolder>();
            var replicateNode = SkylineWindow.FilesTree.File<Replicate>(replicateFolder);
            Assert.IsNotNull(replicateNode);

            // Select the replicate node so its properties appear in the property sheet
            RunUI(() =>
            {
                SkylineWindow.ShowPropertyGridForm(true);
                SkylineWindow.FilesTreeForm.FilesTree.SelectNodeWithoutResettingSelection(replicateNode);
                SkylineWindow.FocusPropertyProvider(SkylineWindow.FilesTreeForm);
            });

            // find griditem associate with Name property
            var selectedObject = SkylineWindow.PropertyGridForm.GetPropertyObject();
            var nameGridItem = SkylineWindow.PropertyGridForm.GetGridItemByPropName(NAME_PROP_NAME);
            Assert.IsNotNull(nameGridItem);
            var nameGridItemValue = nameGridItem.Value;
            Assert.IsTrue(nameGridItemValue is string);
            var nameValue = (string)nameGridItemValue;
            Assert.AreEqual(replicateNode.Name, nameValue);

            // Edit the Name property through the PropertyGrid UI
            const string newName = "EditedName";
            RunUI(() =>
            {
                nameGridItem.PropertyDescriptor?.SetValue(selectedObject, newName);
            });
            lock(SkylineWindow.GetDocumentChangeLock()) { }

            nameGridItem = SkylineWindow.PropertyGridForm.GetGridItemByPropName(NAME_PROP_NAME);
            Assert.IsNotNull(nameGridItem);
            Assert.AreEqual(nameGridItem.Value, newName);
        }
    }
}
