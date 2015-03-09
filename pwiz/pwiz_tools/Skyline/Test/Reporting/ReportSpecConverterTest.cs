/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NHibernate;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Reporting
{
    /// <summary>
    /// Summary description for ReportSpecConverterTest
    /// </summary>
    [TestClass]
    public class ReportSpecConverterTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestMapping()
        {
            var settings = SrmSettingsList.GetDefault();
            var document = new SrmDocument(settings);
            var documentContainer = new MemoryDocumentContainer();
            documentContainer.SetDocument(document, null);
            using (var database = new Database(settings))
            {
                var dataSchema = new SkylineDataSchema(documentContainer, DataSchemaLocalizer.INVARIANT);
                var sessionFactory = database.SessionFactory;
                foreach (var classMetaData in sessionFactory.GetAllClassMetadata().Values)
                {
                    var tableType = classMetaData.GetMappedClass(EntityMode.Poco);
                    foreach (var propertyName in classMetaData.PropertyNames)
                    {
                        if (propertyName == "Protein" && tableType == typeof (DbProteinResult))
                        {
                            continue;
                        }
                        var queryDef = new QueryDef
                            {
                                Select = new[] {new ReportColumn(tableType, propertyName),}
                            };
                        var reportSpec = new ReportSpec("test", queryDef);
                        var newTableType = ReportSpecConverter.GetNewTableType(reportSpec);
                        Assert.IsNotNull(newTableType, "No table for type {0}", tableType);
                        var converter = new ReportSpecConverter(dataSchema);
                        var viewInfo = converter.Convert(reportSpec);
                        Assert.IsNotNull(viewInfo, "Unable to convert property {0} in table {1}", propertyName, tableType);
                        Assert.AreEqual(1, viewInfo.DisplayColumns.Count, "No conversion for property {0} in table {1}", propertyName, tableType);
                        Assert.IsNotNull(viewInfo.DisplayColumns[0].ColumnDescriptor, "Column not found for property {0} in table {1}", propertyName, tableType);
                        var report = Report.Load(reportSpec);
                        var resultSet = report.Execute(database);
                        var bindingListSource = new BindingListSource();
                        bindingListSource.SetViewContext(new SkylineViewContext(viewInfo.ParentColumn, Array.CreateInstance(viewInfo.ParentColumn.PropertyType, 0)), viewInfo);
                        var properties = bindingListSource.GetItemProperties(null);
                        var oldCaptions = resultSet.ColumnInfos.Select(columnInfo => columnInfo.Caption).ToArray();
                        var newCaptions = properties.Cast<PropertyDescriptor>().Select(pd=>pd.DisplayName).ToArray();
                        if (oldCaptions.Length != newCaptions.Length)
                        {
                            Console.Out.WriteLine(oldCaptions);
                        }
                        CollectionAssert.AreEqual(oldCaptions, newCaptions, "Caption mismatch on {0} in {1}", propertyName, tableType);
                        for (int i = 0; i < resultSet.ColumnInfos.Count; i++)
                        {
                            var columnInfo = resultSet.ColumnInfos[i];
                            var formatAttribute = (FormatAttribute)properties[i].Attributes[typeof(FormatAttribute)];
                            string message = string.Format("Format problem on column converted from {0} in {1}",
                                columnInfo.ReportColumn.Column, columnInfo.ReportColumn.Table);
                            if (null == columnInfo.Format)
                            {
                                Assert.IsTrue(null == formatAttribute || null == formatAttribute.Format, message);
                            }
                            else
                            {
                                Assert.IsNotNull(formatAttribute, message);
                                Assert.AreEqual(columnInfo.Format, formatAttribute.Format, message);
                            }
                            if (columnInfo.IsNumeric)
                            {
                                Assert.IsNotNull(formatAttribute, message);
                                Assert.AreEqual(TextUtil.EXCEL_NA, formatAttribute.NullValue, message);
                            }
                            else
                            {
                                Assert.IsTrue(null == formatAttribute || null == formatAttribute.NullValue, message);
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void TestBlankDocument()
        {
            var blankDocument = new SrmDocument(SrmSettingsList.GetDefault());
            CheckReportCompatibility.CheckAll(blankDocument);
        }
        [TestMethod]
        public void TestDocumentWithOneLabel()
        {
            TestSmallMolecules = false; // Mixed molecule docs create different report columns
            var assembly = typeof(ReportSpecConverterTest).Assembly;
            XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
            // ReSharper disable once AssignNullToNotNullAttribute
            var docWithLabel = (SrmDocument)ser.Deserialize(
                assembly.GetManifestResourceStream(typeof(ReportSpecConverterTest), "HeavyLabeledLeucine.sky"));
            CheckReportCompatibility.CheckAll(docWithLabel);
        }

        [TestMethod]
        public void TestPivotIsotopeLabel()
        {
            TestSmallMolecules = false; // Mixed molecule docs create different report columns
            var assembly = typeof(ReportSpecConverterTest).Assembly;
            XmlSerializer documentSerializer = new XmlSerializer(typeof(SrmDocument));
            // ReSharper disable once AssignNullToNotNullAttribute
            var docWithLabel = (SrmDocument)documentSerializer.Deserialize(
                assembly.GetManifestResourceStream(typeof(ReportSpecConverterTest), "HeavyLabeledLeucine.sky"));
            XmlSerializer reportSerializer = new XmlSerializer(typeof(ReportSpecList));
            // ReSharper disable once AssignNullToNotNullAttribute
            var reports = (ReportSpecList)
                reportSerializer.Deserialize(assembly.GetManifestResourceStream(typeof (ReportSpecConverterTest),
                    "PivotIsotopeLabel.skyr"));
            Assert.AreNotEqual(0, reports.Count);
            using (var checker = new CheckReportCompatibility(docWithLabel))
            {
                foreach (var report in reports)
                {
                    checker.CheckReport(report);
                }
            }
        }
    }
}
