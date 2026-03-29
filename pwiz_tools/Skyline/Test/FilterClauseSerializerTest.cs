/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Filtering;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{

    [TestClass]
    public class FilterClauseSerializerTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestFilterClauseSerializer()
        {
            var clause1 = new FilterClause(new[]
            {
                new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClass.MsLevel)), FilterOperations.OP_CONTAINS,
                    PrecisionNumber.WithDecimalPlaces(400, 0))
            });
            var clause2 = new FilterClause(new[]
            {
                new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClass.MsLevel)), FilterOperations.OP_CONTAINS,
                    PrecisionNumber.WithDecimalPlaces(400, 1))
            });
            VerifyFilterClause(typeof(SpectrumClass), new[]{clause1});
            VerifyFilterClause(typeof(SpectrumClass), new[]{clause1, clause2});
        }

        [TestMethod]
        public void TestToFilterString()
        {
            var frenchDataSchema =
                new DataSchema(new DataSchemaLocalizer(CultureInfo.GetCultureInfo("fr"),
                    CultureInfo.GetCultureInfo("fr")));
            var serializer =
                new FilterClauseSerializer(ColumnDescriptor.RootColumn(frenchDataSchema, typeof(SpectrumClass)));
            var filterSpec = new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClass.MsLevel)),
                FilterOperations.OP_CONTAINS, PrecisionNumber.WithDecimalPlaces(400, 1));
            var text = serializer.ToFilterString(new[] { new FilterClause(new[] { filterSpec }) });
            Assert.AreEqual("MsLevel contains 400,0", text);
        }

        private void VerifyFilterClause(Type rowType, IList<FilterClause> filterClauses)
        {
            foreach (var dataSchema in GetTestDataSchemas())
            {
                var rootColumnDescriptor = ColumnDescriptor.RootColumn(dataSchema, rowType);
                var serializer = new FilterClauseSerializer(rootColumnDescriptor);
                var text = serializer.ToFilterString(filterClauses);
                var roundTrip = serializer.ParseFilterString(text);
                Assert.AreEqual(filterClauses.Count, roundTrip.Count);
                for (int i = 0; i < filterClauses.Count; i++)
                {
                    AssertEx.AreEqual(filterClauses[i], roundTrip[i]);
                }
            }
        }

        private IEnumerable<DataSchema> GetTestDataSchemas()
        {
            yield return new DataSchema();
            yield return new DataSchema(SkylineDataSchema.GetLocalizedSchemaLocalizer());
            foreach (var language in new[] { "en", "fr", "tr", "zh", "ja" })
            {
                var cultureInfo = CultureInfo.GetCultureInfo(language);
                yield return new DataSchema(new DataSchemaLocalizer(cultureInfo, cultureInfo, ColumnCaptions.ResourceManager));
            }
        }
    }
}
