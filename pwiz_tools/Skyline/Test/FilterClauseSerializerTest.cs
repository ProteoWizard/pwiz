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
                new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClass.Ms1Precursors)), FilterOperations.OP_CONTAINS,
                    PrecisionNumber.WithDecimalPlaces(400, 0))
            });
            var clause2 = new FilterClause(new[]
            {
                new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClass.Ms1Precursors)), FilterOperations.OP_CONTAINS,
                    PrecisionNumber.WithDecimalPlaces(400, 1))
            });
            VerifyFilterClause(typeof(SpectrumClass), new[]{clause1});
            VerifyFilterClause(typeof(SpectrumClass), new[]{clause1, clause2});
        }

        [TestMethod]
        public void TestBlankOperatorRoundTrip()
        {
            // The operand-less blank tests have no readable operator symbol, so they serialize against
            // the empty string ("Column = ''" / "Column <> ''") and must round-trip back to the
            // blank operators rather than to an equals-empty-string comparison.
            var isBlank = new FilterClause(new[]
            {
                new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClass.ScanDescription)),
                    new FilterPredicate(FilterOperations.OP_IS_BLANK, null))
            });
            var isNotBlank = new FilterClause(new[]
            {
                new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClass.ScanDescription)),
                    new FilterPredicate(FilterOperations.OP_IS_NOT_BLANK, null))
            });
            VerifyFilterClause(typeof(SpectrumClass), new[] { isBlank });
            VerifyFilterClause(typeof(SpectrumClass), new[] { isNotBlank });

            // The rendered form uses the empty-string notation (column/operator are culture-independent)
            var serializer = new FilterClauseSerializer(
                ColumnDescriptor.RootColumn(GetDataSchema(DataSchemaLocalizer.INVARIANT), typeof(SpectrumClass)));
            Assert.AreEqual("ScanDescription = ''", serializer.ToFilterString(new[] { isBlank }));
            Assert.AreEqual("ScanDescription <> ''", serializer.ToFilterString(new[] { isNotBlank }));

            // Only the *empty* operand means "is blank". A non-empty value that happens to contain
            // single quotes (e.g. the two-character literal "''") is escaped as six quotes and still
            // round-trips as an equals comparison -- it must not collapse to is-blank.
            var equalsLiteralQuotes = new FilterClause(new[]
            {
                new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClass.ScanDescription)),
                    FilterOperations.OP_EQUALS, "''")
            });
            VerifyFilterClause(typeof(SpectrumClass), new[] { equalsLiteralQuotes });
            Assert.AreEqual("ScanDescription = ''''''", serializer.ToFilterString(new[] { equalsLiteralQuotes }));
        }

        [TestMethod]
        public void TestToFilterString()
        {
            var frenchDataSchema = GetDataSchema(new DataSchemaLocalizer(CultureInfo.GetCultureInfo("fr"),
                CultureInfo.GetCultureInfo("fr")));
            var serializer =
                new FilterClauseSerializer(ColumnDescriptor.RootColumn(frenchDataSchema, typeof(SpectrumClass)));
            var filterSpec = new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClass.Ms1Precursors)),
                FilterOperations.OP_CONTAINS, PrecisionNumber.WithDecimalPlaces(400, 1));
            var text = serializer.ToFilterString(new[] { new FilterClause(new[] { filterSpec }) });
            Assert.AreEqual("Ms1Precursors contains 400,0", text);
        }

        private DataSchema GetDataSchema(DataSchemaLocalizer localizer)
        {
            return new DataSchema(localizer);
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
            yield return GetDataSchema(DataSchemaLocalizer.INVARIANT);
            yield return GetDataSchema(SkylineDataSchema.GetLocalizedSchemaLocalizer());
            foreach (var language in new[] { "en", "fr", "tr", "zh", "ja" })
            {
                var cultureInfo = CultureInfo.GetCultureInfo(language);
                yield return GetDataSchema(new DataSchemaLocalizer(cultureInfo, cultureInfo, ColumnCaptions.ResourceManager));
            }
        }
    }
}
