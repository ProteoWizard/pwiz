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
using pwiz.Skyline.Model.DocSettings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Utility class for testing PropertyGrid functionality.
    /// All functions must be called within a RunUI block.
    /// </summary>
    public class PropertyGridTestUtil
    {
        private const string STRING_ANNOTATION_NAME = "StringAnnotation";
        private const string NUMBER_ANNOTATION_NAME = "NumberAnnotation";
        private const string BOOL_ANNOTATION_NAME = "BoolAnnotation";
        private const string LIST_ANNOTATION_NAME = "ListAnnotation";
        private const string ANNOTATION_NAME_PREFIX = "annotation_";

        private const string EXPECTED_PROPERTIES_FILE_PATH = @"TestFunctional\PropertyGridTest.data";
        private const string JSON_SUFFIX = @"-expected-props.json";

        public const string TEST_FILES_ZIP = @"TestFunctional\PropertyGridTest.zip";

        private static PropertyDescriptorCollection GetBrowsableProperties(object obj)
        {
            return obj == null ? new PropertyDescriptorCollection(null) 
                : TypeDescriptor.GetProperties(obj, new Attribute[] { BrowsableAttribute.Yes });
        }

        public static void TestEditProperty(SkylineWindow skylineWindow, string propName, object newValue)
        {
            var selectedObject = skylineWindow.PropertyGridForm.GetPropertyObject();
            Assert.IsNotNull(selectedObject);
            var prop = GetBrowsableProperties(selectedObject)[propName];
            prop.SetValue(selectedObject, newValue);

            // Test if the change is applied to the document
            var newSelectedObject = skylineWindow.PropertyGridForm.GetPropertyObject();
            var newProp = GetBrowsableProperties(newSelectedObject)[propName];
            Assert.AreEqual(newValue, newProp.GetValue(newSelectedObject));

            // Test if the change is seen in the UI
            var gridItem = skylineWindow.PropertyGridForm.GetGridItemByPropName(propName);
            Assert.IsNotNull(gridItem);
            Assert.AreEqual(newValue, gridItem.Value);
        }

        public static void TestAddAnnotations(SkylineWindow skylineWindow, AnnotationDef.AnnotationTarget target)
        {
            // Define four annotation definitions: string, number, bool, value list
            var annotationDefs = new[]
            {
                new AnnotationDef(
                    STRING_ANNOTATION_NAME,
                    AnnotationDef.AnnotationTargetSet.Singleton(target),
                    AnnotationDef.AnnotationType.text,
                    Array.Empty<string>()),

                new AnnotationDef(
                    NUMBER_ANNOTATION_NAME,
                    AnnotationDef.AnnotationTargetSet.Singleton(target),
                    AnnotationDef.AnnotationType.number,
                    Array.Empty<string>()),

                new AnnotationDef(
                    BOOL_ANNOTATION_NAME,
                    AnnotationDef.AnnotationTargetSet.Singleton(target),
                    AnnotationDef.AnnotationType.true_false,
                    Array.Empty<string>()),

                new AnnotationDef(
                    LIST_ANNOTATION_NAME,
                    AnnotationDef.AnnotationTargetSet.Singleton(target),
                    AnnotationDef.AnnotationType.value_list,
                    new[] { "A", "B", "C" })
            };

            var doc = skylineWindow.Document;
            var newSettings = doc.Settings.ChangeDataSettings(
                doc.Settings.DataSettings.ChangeAnnotationDefs(annotationDefs));
            skylineWindow.ModifyDocument($"Add {target.ToString()} annotation definitions", d => d.ChangeSettings(newSettings));

            var defs = skylineWindow.Document.Settings.DataSettings.AnnotationDefs
                .Where(def => def.AnnotationTargets.Contains(target)).ToList();
            var defString = defs.FirstOrDefault(def => def.Name == STRING_ANNOTATION_NAME);
            var defNumber = defs.FirstOrDefault(def => def.Name == NUMBER_ANNOTATION_NAME);
            var defBool = defs.FirstOrDefault(def => def.Name == BOOL_ANNOTATION_NAME);
            var defList = defs.FirstOrDefault(def => def.Name == LIST_ANNOTATION_NAME);

            Assert.IsNotNull(defString);
            Assert.IsNotNull(defNumber);
            Assert.IsNotNull(defBool);
            Assert.IsNotNull(defList);
        }

        // edit annotations of currently selected property object and test if the changes are applied to the document
        // assumes the annotation definitions already exist, e.g. by calling TestAddAnnotations on that type of object
        public static void TestEditAnnotations(SkylineWindow skylineWindow)
        {
            const string stringEditedValue = "EditedString";
            const double numberEditedValue = 123.45;
            const bool boolEditedValue = false;
            const string listEditedValue = "C";

            var defs = skylineWindow.Document.Settings.DataSettings.AnnotationDefs;
            var defString = defs.FirstOrDefault(def => def.Name == STRING_ANNOTATION_NAME);
            var defNumber = defs.FirstOrDefault(def => def.Name == NUMBER_ANNOTATION_NAME);
            var defBool = defs.FirstOrDefault(def => def.Name == BOOL_ANNOTATION_NAME);
            var defList = defs.FirstOrDefault(def => def.Name == LIST_ANNOTATION_NAME);

            Assert.IsNotNull(defString);
            Assert.IsNotNull(defNumber);
            Assert.IsNotNull(defBool);
            Assert.IsNotNull(defList);

            // Edit annotation properties through the PropertyGrid
            Assert.IsNotNull(skylineWindow.PropertyGridForm);
            
            TestEditProperty(skylineWindow,ANNOTATION_NAME_PREFIX + STRING_ANNOTATION_NAME, stringEditedValue);
            TestEditProperty(skylineWindow, ANNOTATION_NAME_PREFIX + NUMBER_ANNOTATION_NAME, numberEditedValue);
            TestEditProperty(skylineWindow, ANNOTATION_NAME_PREFIX + BOOL_ANNOTATION_NAME, boolEditedValue);
            TestEditProperty(skylineWindow, ANNOTATION_NAME_PREFIX + LIST_ANNOTATION_NAME, listEditedValue);

            // edits should change annotations of the selected object internally
            var selectedObject = skylineWindow.PropertyGridForm.GetPropertyObject();
            Assert.AreEqual(stringEditedValue, selectedObject.GetAnnotation(defString));
            Assert.AreEqual(numberEditedValue, selectedObject.GetAnnotation(defNumber));
            Assert.AreEqual(boolEditedValue, selectedObject.GetAnnotation(defBool));
            Assert.AreEqual(listEditedValue, selectedObject.GetAnnotation(defList));
        }

        public static void LogOrTestExpectedPropertyValues(SkylineWindow skylineWindow,
            Dictionary<string, Dictionary<string, string>> expectedPropertyValues,
            string propertyObjectTypeKey, bool isRecordMode)
        {
            if (isRecordMode)
            {
                expectedPropertyValues[propertyObjectTypeKey] = GetObservedPropertyValues(skylineWindow);
            }
            else
            {
                TestExpectedPropertyValues(skylineWindow, expectedPropertyValues[propertyObjectTypeKey]);
            }
        }

        // Test whether the currently selected object's properties match the given expected values
        private static void TestExpectedPropertyValues(SkylineWindow skylineWindow, Dictionary<string, string> expectedPropertyValues) 
        {
            var selectedObject = skylineWindow.PropertyGridForm.GetPropertyObject();
            Assert.IsNotNull(selectedObject);
            var props = GetBrowsableProperties(selectedObject);
            Assert.AreEqual(props.Count, expectedPropertyValues.Count, $"Expected {expectedPropertyValues.Count} properties on selected object, found {props.Count}");
            foreach (var kvp in expectedPropertyValues)
            {
                var propName = kvp.Key;
                var expectedValue = kvp.Value;
                var gridItem = skylineWindow.PropertyGridForm.GetGridItemByPropName(propName);
                Assert.IsNotNull(gridItem, $"Property '{propName}' not found on selected object.");
                Assert.AreEqual(expectedValue, gridItem.Value.ToString(), $"Property '{propName}' value mismatch.");
            }
        }

        private static Dictionary<string, string> GetObservedPropertyValues(SkylineWindow skylineWindow)
        {
            var selectedObject = skylineWindow.PropertyGridForm.GetPropertyObject();
            Assert.IsNotNull(selectedObject);
            var props = GetBrowsableProperties(selectedObject);

            var expectedPropertyValues = new Dictionary<string, string>();
            foreach (PropertyDescriptor prop in props)
            {
                expectedPropertyValues[prop.Name] = prop.GetValue(selectedObject)?.ToString() ?? string.Empty;
            }

            return expectedPropertyValues;
        }

        public static Dictionary<string, Dictionary<string, string>> ReadAllExpectedPropertyValues(string prefix, bool isRecordMode)
        {
            if (isRecordMode) return new Dictionary<string, Dictionary<string, string>>();

            var filePath = Path.Combine(ExtensionTestContext.GetProjectDirectory(EXPECTED_PROPERTIES_FILE_PATH), prefix + JSON_SUFFIX);
            Assert.IsTrue(File.Exists(filePath));
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
        }

        public static void WriteAllExpectedPropertyValues(string prefix, Dictionary<string, Dictionary<string, string>> expectedProperties) {
            var filePath = Path.Combine(ExtensionTestContext.GetProjectDirectory(EXPECTED_PROPERTIES_FILE_PATH), prefix + JSON_SUFFIX);
            var json = JsonConvert.SerializeObject(expectedProperties, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
    }
}
