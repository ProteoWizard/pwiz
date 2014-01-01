/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Reporting
{
    /// <summary>
    /// Summary description for PivotTypeTest
    /// </summary>
    [TestClass]
    public class PivotTypeTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestCrosstabValues()
        {
            AssertValidProperties(typeof(DbPrecursor), 
                IsotopeLabelPivotType.PRECURSOR_CROSSTAB_VALUES);
            AssertValidProperties(typeof(DbTransition), 
                IsotopeLabelPivotType.TRANSITION_CROSSTAB_VALUES);
        }

        [TestMethod]
        public void TestGroupByColumns()
        {
            using (Database database = new Database())
            {
                Schema schema = database.GetSchema();
                foreach (PivotType pivotType in new[] { PivotType.REPLICATE, PivotType.ISOTOPE_LABEL, PivotType.REPLICATE_ISOTOPE_LABEL })
                {
                    foreach (Type table in schema.GetTables())
                    {
                        var reportColumns = new[] { new ReportColumn(table, "Id") };
                        var columns = pivotType.GetGroupByColumns(reportColumns);
                        if (columns.Count == 0)
                        {
                            Assert.AreEqual(0, pivotType.GetCrosstabHeaders(reportColumns).Count,
                                string.Format("No groupby columns, but crosstab headers for table {0} and pivot type {1}", table, pivotType.GetType()));
                            continue;
                        }
                        foreach (var column in columns)
                        {
                            Assert.IsNotNull(schema.GetColumnInfo(column));
                        }
                        foreach (var column in pivotType.GetCrosstabHeaders(reportColumns))
                        {
                            Assert.IsNotNull(schema.GetColumnInfo(column));
                        }
                    }
                }
            }
        }

        private static void AssertValidProperty(Type type, String property)
        {
            Assert.IsNotNull(type.GetProperty(property),
                             string.Format("No such property {0} on type {1}", property, type));
        }

        private static void AssertValidProperties(Type type, IEnumerable<string> properties)
        {
            foreach (String property in properties)
            {
                AssertValidProperty(type, property);
            }
        }
    }
}
