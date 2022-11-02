/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.DataBinding.Documentation;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog.Databinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Reporting
{
    /// <summary>
    /// Tests that make sure that "Model\Databinding\Entities\ColumnCaptions.resx" contains all of the column names
    /// that can possibly appear in Skyline Live Reports.
    /// </summary>
    [TestClass]
    public class ColumnCaptionLocalizationTest : AbstractUnitTest
    {
        private static readonly IList<Type> STARTING_TYPES = ImmutableList.ValueOf(new[]
        {
            typeof(SkylineDocument), typeof(FoldChangeBindingSource.FoldChangeRow), typeof(AuditLogRow),
            typeof(CandidatePeakGroup)
        });
        /// <summary>
        /// This test method just outputs the entire text that should go in "ColumnCaptions.resx".
        /// </summary>
        [TestMethod]
        public void TestGenerateResxFile()
        {
            var documentContainer = new MemoryDocumentContainer();
            Assert.IsTrue(documentContainer.SetDocument(new SrmDocument(SrmSettingsList.GetDefault()), documentContainer.Document));
            GenerateResXFile(new StringWriter() /* Console.Out */, new SkylineDataSchema(documentContainer, DataSchemaLocalizer.INVARIANT), STARTING_TYPES);
        }

        /// <summary>
        /// Tests that all columns that can be displayed in Skyline Live Reports have an entry in "ColumnCaptions.resx".
        /// If you add a Property to any of the entities that get displayed in Skyline Live Reports, you probably have
        /// to add an entry to ColumnCaptions.resx so that the column header can be localized.
        /// </summary>
        [TestMethod]
        public void TestAllColumnCaptionsAreLocalized()
        {
            var missingCaptions = new HashSet<ColumnCaption>();
            foreach (var skylineDataSchema in EnumerateDataSchemas())
            {
                foreach (var columnDescriptor in EnumerateAllColumnDescriptors(skylineDataSchema, STARTING_TYPES))
                {
                    var invariantCaption = skylineDataSchema.GetColumnCaption(columnDescriptor) as ColumnCaption;
                    if ("Value".Equals(invariantCaption?.InvariantCaption))
                    {
                        ColumnDescriptor rootColumnDescriptor = columnDescriptor;
                        while (rootColumnDescriptor.Parent != null)
                        {
                            rootColumnDescriptor = rootColumnDescriptor.Parent;
                        }
                        // There should not be any columns named "Value". If any column is named that, it
                        // probably needs that the class which is the dictionary value needs
                        // "[InvariantDisplayName]" on top of it.
                        Assert.Fail("Column named 'Value' found on property {0} from type {1}", columnDescriptor.PropertyPath, rootColumnDescriptor.PropertyType);
                    }
                    if (invariantCaption != null && !skylineDataSchema.DataSchemaLocalizer.HasEntry(invariantCaption))
                    {
                        missingCaptions.Add(invariantCaption);
                    }
                }
            }
            if (missingCaptions.Count == 0)
            {
                return;
            }
            StringWriter message = new StringWriter();
            WriteResXFile(message, missingCaptions);
            Assert.Fail("Missing localized column captions {0}", message);
        }

        /// <summary>
        /// Tests that all columns that can be displayed in Skyline Live Reports have an entry in "ColumnToolTips.resx".
        /// If you add a Property to any of the entities that get displayed in Skyline Live Reports, you probably have
        /// to add an entry to ColumnToolTips.resx so that the column has a tooltip.
        /// </summary>
        [TestMethod]
        public void TestAllColumnsHaveTooltips()
        {
            var missingCaptions = new HashSet<ColumnCaption>();
            foreach (var skylineDataSchema in EnumerateDataSchemas())
            {
                foreach (var columnDescriptor in EnumerateAllColumnDescriptors(skylineDataSchema, STARTING_TYPES))
                {
                    var tooltip = skylineDataSchema.GetColumnDescription(columnDescriptor);
                    if (string.IsNullOrEmpty(tooltip))
                    {
                        var invariantCaption = skylineDataSchema.GetColumnCaption(columnDescriptor) as ColumnCaption;
                        if (invariantCaption != null)
                        {
                            missingCaptions.Add(invariantCaption);
                        }
                    }
                }
            }

            if (missingCaptions.Count == 0)
            {
                return;
            }
            StringWriter message = new StringWriter();
            WriteResXFile(message, missingCaptions);
            Assert.Fail("Missing localized tooltips for column captions: {0}", message.ToString().Replace("<data","\r\n<data"));
        }

        /// <summary>
        /// Tests that all of the entries in ColumnCaptions.resx actually show up in Skyline Live Reports somewhere.
        /// </summary>
        [TestMethod]
        public void TestCheckForUnusedColumnCaptions()
        {
            var columnCaptions = new HashSet<string>();
            foreach (
                var resourceManager in SkylineDataSchema.GetLocalizedSchemaLocalizer()
                    .ColumnCaptionResourceManagers)
            {
                var resourceSet = resourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
                var enumerator = resourceSet.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    string key = enumerator.Key as string;
                    if (null != key)
                    {
                        columnCaptions.Add(key);
                    }
                }
            }
            foreach (var dataSchema in EnumerateDataSchemas())
            {
                foreach (var columnDescriptor in EnumerateAllColumnDescriptors(dataSchema, STARTING_TYPES))
                {
                    var invariantCaption = dataSchema.GetColumnCaption(columnDescriptor);
                    columnCaptions.Remove(invariantCaption.GetCaption(DataSchemaLocalizer.INVARIANT));
                }
            }

            var unusedCaptions = columnCaptions.ToArray();
            Assert.AreEqual(0, unusedCaptions.Length, "Unused entries found in ColumnCaptions.resx: {0}", string.Join(",", unusedCaptions));
        }

        /// <summary>
        /// Tests that all of the entries in ColumnTooltips.resx actually show up in Skyline Live Reports somewhere.
        /// </summary>
        [TestMethod]
        public void TestCheckForUnusedColumnTooltips()
        {
            var columnCaptions = new HashSet<string>();
            var resourceManager = ColumnToolTips.ResourceManager;
            var resourceSet = resourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
            var enumerator = resourceSet.GetEnumerator();
            while (enumerator.MoveNext())
            {
                string key = enumerator.Key as string;
                if (null != key)
                {
                    columnCaptions.Add(key);
                }
            }

            foreach (var dataSchema in EnumerateDataSchemas())
            {
                foreach (var columnDescriptor in EnumerateAllColumnDescriptors(dataSchema, STARTING_TYPES))
                {
                    var invariantCaption = dataSchema.GetColumnCaption(columnDescriptor);
                    columnCaptions.Remove(invariantCaption.GetCaption(DataSchemaLocalizer.INVARIANT));
                }
            }

            var unusedCaptions = columnCaptions.ToArray();
            Assert.AreEqual(0, unusedCaptions.Length, "Unused entries found in ColumnToolTips.resx: {0}", string.Join(",", unusedCaptions));
        }

        public IEnumerable<SkylineDataSchema> EnumerateDataSchemas()
        {
            var documentContainer = new MemoryDocumentContainer();
            Assert.IsTrue(documentContainer.SetDocument(new SrmDocument(SrmSettingsList.GetDefault()), documentContainer.Document));
            var dataSchema = new SkylineDataSchema(documentContainer, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            yield return dataSchema;
        }

        public IEnumerable<ColumnDescriptor> EnumerateAllColumnDescriptors(DataSchema dataSchema,
            ICollection<Type> startingTypes)
        {
            var startingTypesSet = new HashSet<Type>(startingTypes);
            var typeQueue = new Queue<Type>(startingTypes);
            var processedTypes = new HashSet<Type>();
            while (typeQueue.Count > 0)
            {
                var type = typeQueue.Dequeue();
                if (!processedTypes.Add(type))
                {
                    continue;
                }

                foreach (var uiMode in UiModes.AllModes)
                {
                    var rootColumn = ColumnDescriptor.RootColumn(dataSchema, type, uiMode.Name);
                    if (startingTypesSet.Contains(type) && typeof(ILinkValue).IsAssignableFrom(type))
                    {
                        // If the root column is selectable, make sure that its caption is localized
                        if (type != typeof(SkylineDocument))
                        {
                            yield return rootColumn;
                        }
                    }
                    foreach (var child in GetChildColumns(rootColumn))
                    {
                        typeQueue.Enqueue(child.PropertyType);
                        yield return child;
                        foreach (var grandChild in GetChildColumns(child))
                        {
                            yield return grandChild;
                            if (grandChild.GetAttributes().OfType<ChildDisplayNameAttribute>().Any()
                                && child.GetAttributes().OfType<ChildDisplayNameAttribute>().Any())
                            {
                                Assert.Fail("Two levels of child display names found on property {0} of type {1}",
                                    grandChild.PropertyPath, rootColumn.PropertyType);
                            }
                        }
                    }
                }
            }
        }

        public void GenerateResXFile(TextWriter writer, DataSchema dataSchema, ICollection<Type> startingTypes)
        {
            var allColumnCaptions = new HashSet<ColumnCaption>();
            foreach (var columnDescriptor in EnumerateAllColumnDescriptors(dataSchema, startingTypes))
            {
                var columnCaption = dataSchema.GetColumnCaption(columnDescriptor) as ColumnCaption;
                if (columnCaption != null)
                {
                    allColumnCaptions.Add(columnCaption);
                }
            }
            WriteResXFile(writer, allColumnCaptions);
        }

        public void WriteResXFile(TextWriter writer, ICollection<ColumnCaption> invariantColumnCaptions)
        {
            var allPropertyNames = new HashSet<string>(invariantColumnCaptions.Select(caption=>caption.InvariantCaption));
            var sortedPropertyNames = allPropertyNames.ToArray();
            Array.Sort(sortedPropertyNames, StringComparer.InvariantCultureIgnoreCase);
            var settings = new XmlWriterSettings()
            {
                NewLineChars = Environment.NewLine,
                Indent = true
            };
            using (var xmlWriter = XmlWriter.Create(writer, settings))
            {
                // Some versions of ReSharper think XmlWriter.Create can return a null, others don't, disable this check to satisfy either
                // ReSharper disable PossibleNullReferenceException
                xmlWriter.WriteStartElement("root");
                foreach (string propertyName in sortedPropertyNames)
                {
                    xmlWriter.WriteStartElement("data");
                    xmlWriter.WriteAttributeString("name", propertyName);
                    xmlWriter.WriteElementString("value", InvariantToEnglishName(propertyName));
                    xmlWriter.WriteEndElement();
                }
                xmlWriter.WriteEndElement();
                // ReSharper restore PossibleNullReferenceException
            }
        }

        private IEnumerable<ColumnDescriptor> GetChildColumns(ColumnDescriptor columnDescriptor)
        {
            return DocumentationGenerator.GetChildColumns(columnDescriptor);
        }

        private string InvariantToEnglishName(string name)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char ch = name[i];
                if (i > 0)
                {
                    char chPrev = name[i - 1];
                    if (char.IsLower(chPrev))
                    {
                        if (char.IsUpper(ch))
                        {
                            result.Append(' ');
                        }
                    }
                }
                result.Append(ch);
            }
            return result.ToString();
        }
    }
}
