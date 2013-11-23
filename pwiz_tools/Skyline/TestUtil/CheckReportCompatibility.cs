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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Properties;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;
using Transition = pwiz.Skyline.Model.Databinding.Entities.Transition;

namespace pwiz.SkylineTestUtil
{
    public class CheckReportCompatibility : IDisposable
    {
        private readonly Database _database;
        private readonly SkylineDataSchema _dataSchema;
        public CheckReportCompatibility(SrmDocument document)
        {
            IDocumentContainer documentContainer = new MemoryDocumentContainer();
            Assert.IsTrue(documentContainer.SetDocument(document, null));
            _database = new Database(document.Settings);
            _database.AddSrmDocument(document);
            _dataSchema = new SkylineDataSchema(documentContainer);
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        public void CheckAll()
        {
            foreach (var reportSpec in ListReportSpecs())
            {
                CheckReport(reportSpec);
            }
        }

        public void CheckReport(ReportSpec reportSpec)
        {
            string message = string.Format("Report {0}", reportSpec.Name);
            var converter = new ReportSpecConverter(_dataSchema);
            var viewInfo = converter.Convert(reportSpec);
            var report = Report.Load(reportSpec);
            ResultSet resultSet;
            try
            {
                resultSet = report.Execute(_database);
            }
            catch (Exception)
            {
                return;
            }
            using (var bindingListSource = new BindingListSource())
            {
                bindingListSource.SetViewContext(new SkylineViewContext(viewInfo.ParentColumn, GetRowSource(viewInfo)),
                    viewInfo);
                PropertyDescriptorCollection properties;
                IList resultRows;
                var oldCaptions = resultSet.ColumnInfos.Select(columnInfo => columnInfo.Caption).ToArray();
                properties = bindingListSource.GetItemProperties(null);
                resultRows = bindingListSource;
                var newCaptions = properties.Cast<PropertyDescriptor>().Select(pd => pd.DisplayName).ToArray();
                if (!oldCaptions.SequenceEqual(newCaptions))
                {
                    CollectionAssert.AreEqual(oldCaptions, newCaptions, message);
                }
                if (resultSet.RowCount != resultRows.Count)
                {
                    Assert.AreEqual(resultSet.RowCount, resultRows.Count, message);
                }
                var orderedPropertyIndexes =
                    properties.Cast<PropertyDescriptor>()
                        .Select((pd, index) => new KeyValuePair<PropertyDescriptor, int>(pd, index))
                        .ToArray();
                Array.Sort(orderedPropertyIndexes, ComparePropertyIndexes);
                resultRows = SortRows(resultRows,
                    new PropertyDescriptorCollection(orderedPropertyIndexes.Select(kvp => kvp.Key).ToArray()));
                resultSet = SortResultSet(resultSet, orderedPropertyIndexes.Select(kvp => kvp.Value).ToArray());
                bool allValuesExactlyEqual = true;
                for (int iRow = 0; iRow < resultSet.RowCount; iRow++)
                {
                    for (int iCol = 0; iCol < resultSet.ColumnInfos.Count; iCol++)
                    {
                        var propertyDescriptor = properties[iCol];
                        object oldValue = resultSet.GetRow(iRow)[iCol];
                        object newValue = propertyDescriptor.GetValue(resultRows[iRow]);
                        allValuesExactlyEqual = allValuesExactlyEqual && Equals(oldValue, newValue);
                        AssertValuesEqual(oldValue, newValue,
                            message + "{0}:Values are not equal on Row {1} Column {2} ({3}) FullName:{4}",
                            message, iRow, iCol, propertyDescriptor.DisplayName, propertyDescriptor.Name);
                    }
                }
                foreach (char separator in new[] { ',', '\t' })
                {
                    StringWriter oldStringWriter = new StringWriter();
                    var cultureInfo = LocalizationHelper.CurrentCulture;
                    ResultSet.WriteReportHelper(resultSet, separator, oldStringWriter, cultureInfo);
                    StringWriter newStringWriter = new StringWriter();
                    var skylineViewContext = (SkylineViewContext) bindingListSource.ViewContext;
                    ProgressStatus progressStatus = new ProgressStatus("Status");
                    skylineViewContext.Export(null, ref progressStatus, viewInfo, newStringWriter,
                        new DsvWriter(cultureInfo, separator));
                    var newLineSeparators = new[] { "\r\n" };
                    var oldLines = oldStringWriter.ToString().Split(newLineSeparators, StringSplitOptions.None);
                    var newLines = newStringWriter.ToString().Split(newLineSeparators, StringSplitOptions.None);
                    // TODO(nicksh): Old reports would hide columns for annotations that were not in the document.
                    bool anyHiddenColumns = resultSet.ColumnInfos.Any(column => column.IsHidden);
                    if (!anyHiddenColumns)
                    {
                        Assert.AreEqual(oldLines[0], newLines[0]);
                        if (allValuesExactlyEqual)
                        {
                            CollectionAssert.AreEquivalent(oldLines, newLines);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// When sorting the rows in the result set, we first sort on all of the properties that are not doubles, and
        /// then on the doubles, so that, if there are rounding errors with the doubles, we still end up comparing
        /// each row with the one it's supposed to match to.
        /// </summary>
        private int ComparePropertyIndexes(KeyValuePair<PropertyDescriptor, int> kvp1,
            KeyValuePair<PropertyDescriptor, int> kvp2)
        {
            if (kvp1.Key.PropertyType == typeof (double))
            {
                return kvp2.Key.PropertyType == typeof (double) ? kvp1.Value.CompareTo(kvp2.Value) : 1;
            }
            if (kvp2.Key.PropertyType == typeof (double))
            {
                return -1;
            }
            return kvp1.Value.CompareTo(kvp2.Value);
        }

        private IList SortRows(IList rows, PropertyDescriptorCollection properties)
        {
            var sortedRows = rows.Cast<object>().Select((row,index)=>new Tuple<object, List<object>, int>(row, new List<object>(), index)).ToArray();
            Array.Sort(sortedRows, (entry1, entry2) =>
            {
                var values1 = entry1.Item2;
                var values2 = entry2.Item2;
                for (int i = 0; i < properties.Count; i++)
                {
                    if (values1.Count == i)
                    {
                        values1.Add(properties[i].GetValue(entry1.Item1));
                    }
                    if (values2.Count == i)
                    {
                        values2.Add(properties[i].GetValue(entry2.Item1));
                    }
                    int result = _dataSchema.Compare(values1[i], values2[i]);
                    if (0 != result)
                    {
                        return result;
                    }
                }
                return entry1.Item3.CompareTo(entry2.Item3);
            });
            return sortedRows.Select(entry=>entry.Item1).ToArray();
        }

        private ResultSet SortResultSet(ResultSet resultSet, int[] orderedColumnIndexes)
        {
            var sortedRows = new List<object[]>();
            for (int i = 0; i < resultSet.RowCount; i++)
            {
                sortedRows.Add(resultSet.GetRow(i));
            }
            sortedRows.Sort((row1, row2) =>
            {
                foreach (int i in orderedColumnIndexes)
                {
                    var value1 = row1[i];
                    var value2 = row2[i];
                    var result = _dataSchema.Compare(value1, value2);
                    if (0 != result)
                    {
                        return result;
                    }
                }
                return 0;
            });
            return new ResultSet(resultSet.ColumnInfos, sortedRows);
        }


        public IEnumerable GetRowSource(ViewInfo viewInfo)
        {
            var type = viewInfo.ParentColumn.PropertyType;
            if (type == typeof (Protein))
            {
                return new Proteins(_dataSchema);
            }
            if (type == typeof (Peptide))
            {
                return new Peptides(_dataSchema, new[]{IdentityPath.ROOT});
            }
            if (type == typeof (Precursor))
            {
                return new Precursors(_dataSchema, new[]{IdentityPath.ROOT});
            }
            if (type == typeof (Transition))
            {
                return new Transitions(_dataSchema, new[]{IdentityPath.ROOT});
            }
            throw new ArgumentException(string.Format("No row source for {0}", viewInfo.ParentColumn.PropertyType));
        }
        
        public static void CheckAll(SrmDocument document)
        {
            using (var checkReportCompatibility = new CheckReportCompatibility(document))
            {
                checkReportCompatibility.CheckAll();
            }
        }

        public static IList<ReportSpec> ListReportSpecs()
        {
            var reportSpecs = new List<ReportSpec>();
            var reportSpecSet = new HashSet<ReportSpec>();
            reportSpecs.AddRange(Settings.Default.ReportSpecList);
            reportSpecSet.UnionWith(reportSpecs);
            var serializer = new XmlSerializer(typeof (ReportSpecList));
            // ReSharper disable once AssignNullToNotNullAttribute
            var reportSpecList = (ReportSpecList) serializer.Deserialize(typeof(CheckReportCompatibility).Assembly
                .GetManifestResourceStream(typeof(CheckReportCompatibility), "CheckReportCompatibility.skyr"));
            Assert.AreNotEqual(0, reportSpecList.Count);
            foreach (var reportSpec in reportSpecList)
            {
                if (reportSpecSet.Add(reportSpec))
                {
                    reportSpecs.Add(reportSpec);
                }
            }
            CollectionAssert.AreEquivalent(reportSpecs, reportSpecSet.ToArray());
            return reportSpecs;
        }

        public static void AssertValuesEqual(object value1, object value2, string message, params object[] parameters)
        {
            if (value1 is double && value2 is double)
            {
                const double delta = .000001;
                double d1 = (double) value1;
                double d2 = (double) value2;
                if (!d1.Equals(d2) && Math.Abs(d1 - d2) >= delta)
                {
                    Assert.AreEqual(d1, d2, delta, message, parameters);
                }
            }
            else
            {
                if (!Equals(value1, value2))
                {
                    Assert.AreEqual(value1, value2, message, parameters);
                }
            }
        }
    }
}
