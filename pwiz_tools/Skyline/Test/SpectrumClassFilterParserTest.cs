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
using pwiz.SkylineTestUtil;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Filtering;
using pwiz.Skyline.Model.Results.Spectra;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class SpectrumClassFilterParserTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestParseSpectrumClassFilter()
        {
            var filter1 = SpectrumClassFilter.ParseFilterString(string.Format("DissociationMethod = 'CID' and CollisionEnergy = {0:F1} and MsLevel = 2", 20));
            Assert.IsNotNull(filter1);
            var filter2 = SpectrumClassFilter.ParseFilterString("MsLevel = 1");
            Assert.IsNotNull(filter2);
            var filter3 = SpectrumClassFilter.ParseFilterString(string.Format("(DissociationMethod = 'CID' and CollisionEnergy = {0:F1} and MsLevel = 2) or (MsLevel = 1)", 20));
            Assert.IsNotNull(filter3);
            var filter4 = SpectrumClassFilter.ParseFilterString(string.Format("(DissociationMethod = 'CID' and CollisionEnergy = {0:F1} and MsLevel = 2) or MsLevel = 1", 20));
            Assert.IsNotNull(filter4);
        }

        [TestMethod]
        public void TestParseSpectrumClassFilterLocale()
        {
            // A comma-decimal value (European locale) and a period-decimal value parse to the same
            // filter regardless of the running culture, via the parse-culture fallback.
            var periodDecimal = SpectrumClassFilter.ParseFilterString("CollisionEnergy = 1.5");
            var commaDecimal = SpectrumClassFilter.ParseFilterString("CollisionEnergy = 1,5");
            Assert.IsFalse(periodDecimal.IsEmpty);
            Assert.IsFalse(commaDecimal.IsEmpty);
            AssertEx.AreEqual(periodDecimal, commaDecimal);
        }

        [TestMethod]
        public void TestValidateSpectrumClassFilter()
        {
            // A valid filter validates clean; empty is valid (clears the filter)
            Assert.IsNull(SpectrumClassFilter.ValidateFilterString(nameof(SpectrumClass.CollisionEnergy) + " = 25"));
            Assert.IsNull(SpectrumClassFilter.ValidateFilterString(string.Empty));
            // A syntactically valid filter that references an unknown property is rejected
            var unknownPropertyError = SpectrumClassFilter.ValidateFilterString("MeaningLessJunk = 'x'");
            Assert.IsFalse(string.IsNullOrEmpty(unknownPropertyError));
            // Syntactically invalid text is rejected
            var syntaxError = SpectrumClassFilter.ValidateFilterString("not a filter");
            Assert.IsFalse(string.IsNullOrEmpty(syntaxError));
            // The message shows an example of the expected format (e.g. for a wrong operator like "Equals")
            const string badText = "CollisionEnergy Equals -17";
            AssertEx.AreEqual(
                string.Format(SpectraResources.SpectrumClassFilter_ParseFilterString_Invalid_spectrum_filter_format, badText),
                SpectrumClassFilter.ValidateFilterString(badText));
        }

        [TestMethod]
        public void TestSpectrumClassFilterToFilterString()
        {
            var filter = new SpectrumClassFilter(
                new FilterClause(new[]
                {
                    new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClass.DissociationMethod)),
                        FilterOperations.OP_EQUALS, "CID"),
                    new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClassColumn.CollisionEnergy)), FilterOperations.OP_EQUALS, "2.00e1"),
                    new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClassColumn.MsLevel)), FilterOperations.OP_EQUALS, "2")
                }),
                new FilterClause(new[]
                {
                    new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClassColumn.MsLevel)), FilterOperations.OP_EQUALS, "1")
                })
            );
            var text = filter.ToFilterString();
            Assert.IsNotNull(text);
            var roundTrip = SpectrumClassFilter.ParseFilterString(text);
            Assert.IsNotNull(roundTrip);
        }
    }
}
