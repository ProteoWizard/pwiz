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
using System.Linq;

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

        public const string TEST_FILES_ZIP = @"TestFunctional\PropertyGridTest.zip";

        public static void TestEditProperty(SkylineWindow skylineWindow, string propName, object newValue)
        {
            var selectedObject = skylineWindow.PropertyGridForm.GetPropertyObject();
            Assert.IsNotNull(selectedObject);
            var prop = TypeDescriptor.GetProperties(selectedObject, false)[propName];
            prop.SetValue(selectedObject, newValue);

            // Test if the change is applied to the document
            var newSelectedObject = skylineWindow.PropertyGridForm.GetPropertyObject();
            var newProp = TypeDescriptor.GetProperties(newSelectedObject, false)[propName];
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

        // Test whether the currently selected object's properties match the given expected values
        public static void TestExpectedPropertyValues(SkylineWindow skylineWindow, Dictionary<string, object> expectedValues) 
        {
            var selectedObject = skylineWindow.PropertyGridForm.GetPropertyObject();
            Assert.IsNotNull(selectedObject);
            var props = TypeDescriptor.GetProperties(selectedObject, false);
            Assert.AreEqual(props.Count, expectedValues.Count, $"Expected {expectedValues.Count} properties on selected object, found {props.Count}");
            foreach (var kvp in expectedValues)
            {
                var propName = kvp.Key;
                var expectedValue = kvp.Value;
                var prop = props[propName];
                Assert.IsNotNull(prop, $"Property '{propName}' not found on selected object.");
                Assert.AreEqual(prop.PropertyType, expectedValue.GetType(), $"Property '{propName}' type mismatch.");
                var actualValue = prop.GetValue(selectedObject);
                Assert.AreEqual(expectedValue, actualValue, $"Property '{propName}' value mismatch.");
            }
        }
    }
}
