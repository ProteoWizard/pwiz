/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.IO;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding.Layout;
using pwiz.SkylineTestUtil;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Clustering;

namespace CommonTest.DataBinding
{
    [TestClass]
    public class ViewLayoutListTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestViewLayoutRowTransforms()
        {
            var dataSchema = new DataSchema();
            var viewLayout = new ViewLayout("TestName").ChangeRowTransforms(
                new IRowTransform[]
                {
                    new PivotSpec()
                        .ChangeRowHeaders(new[]
                            {new PivotSpec.Column(new ColumnId("RowHeaderOne")).ChangeCaption("MyCaption")})
                        .ChangeColumnHeaders(new[] {new PivotSpec.Column(new ColumnId("ColumnOne")),})
                        .ChangeValues(new[]
                        {
                            (PivotSpec.AggregateColumn) new PivotSpec.AggregateColumn(new ColumnId("ValueOne"),
                                AggregateOperation.Cv).ChangeCaption("Aggregate column caption"),
                        }),
                    new RowFilter("filterText", true, new[]
                    {
                        new RowFilter.ColumnFilter(new ColumnId("column1"),FilterPredicate.CreateFilterPredicate(dataSchema, typeof(string), FilterOperations.OP_CONTAINS, "contains1"))

                    }).SetColumnSorts(new[]
                    {
                        new RowFilter.ColumnSort(new ColumnId("columnSort1"), ListSortDirection.Ascending), 
                        new RowFilter.ColumnSort(new ColumnId("columnSort2"), ListSortDirection.Descending)
                    })
                }
            ).ChangeColumnFormats(new []
            {
                Tuple.Create(new ColumnId("ColumnId1"), ColumnFormat.EMPTY.ChangeFormat("R").ChangeWidth(20))
            }).ChangeClusterSpec(new ClusteringSpec(new []
            {
                new ClusteringSpec.ValueSpec(new ClusteringSpec.ColumnRef(new ColumnId("column_id_1")), "role1"), 
                new ClusteringSpec.ValueSpec(new ClusteringSpec.ColumnRef(PropertyPath.Root.Property("property1")), "role2"), 
            }).ChangeDistanceMetric("distance_metric"));
               
            var viewLayoutList = new ViewLayoutList("Test").ChangeLayouts(new []{viewLayout})
                .ChangeDefaultLayoutName("TestName");
            var roundTrip = RoundTripToXml(viewLayoutList);
            Assert.AreEqual(viewLayoutList, roundTrip);
        }

        private static ViewLayoutList RoundTripToXml(ViewLayoutList viewLayoutList)
        {
            var viewSpecList = new ViewSpecList(new[]{new ViewSpec().SetName(viewLayoutList.ViewName)});
            viewSpecList = viewSpecList.SaveViewLayouts(viewLayoutList);
            var xmlSerializer = new XmlSerializer(typeof(ViewSpecList));
            var memoryStream = new MemoryStream();
            xmlSerializer.Serialize(memoryStream, viewSpecList);
            memoryStream.Seek(0, SeekOrigin.Begin);
            var roundTripViewSpecList = (ViewSpecList)xmlSerializer.Deserialize(memoryStream);
            return roundTripViewSpecList.GetViewLayouts(viewLayoutList.ViewName);
        }
    }
}
