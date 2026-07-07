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
using System.Linq;
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
            // The message shows an example of the expected format. The operator must be neither a symbol
            // nor a friendly alias to trigger it - "foobar" here, since "Equals" is now a valid alias.
            const string badText = "CollisionEnergy foobar 17";
            StringAssert.Contains(SpectrumClassFilter.ValidateFilterString(badText),
                string.Format(SpectraResources.SpectrumClassFilter_ParseFilterString_Invalid_spectrum_filter_format, badText));
        }

        [TestMethod]
        public void TestCvColumnFriendlyReference()
        {
            // A CV term can be referenced in a filter string (transition list / command line) by its
            // accession instead of the internal encoded column token: a bare "MS:1000505", or a
            // double-quoted caption that merely contains the accession. Each resolves to the same filter as
            // the canonical "cvid..." token.
            var canonicalGt = SpectrumClassFilter.ParseFilterString("cvidMS1000505 > 500");
            AssertEx.AreEqual(canonicalGt, SpectrumClassFilter.ParseFilterString("MS:1000505 > 500"));

            var canonicalDeclared = SpectrumClassFilter.ParseFilterString("cvidMS1000505 isdeclared");
            AssertEx.AreEqual(canonicalDeclared,
                SpectrumClassFilter.ParseFilterString("\"base peak intensity (MS:1000505)\" isdeclared"));
            // Only the MS:xxx reference matters - any other text in the caption is ignored.
            AssertEx.AreEqual(canonicalDeclared,
                SpectrumClassFilter.ParseFilterString("\"whatever text MS:1000505 more text\" isdeclared"));

            // The reference is normalized to the canonical encoded column, so a friendly-authored filter is
            // identical to a UI-authored one everywhere downstream (editor display, equality, round-trip).
            var parsed = SpectrumClassFilter.ParseFilterString("MS:1000505 isdeclared");
            Assert.AreEqual("cvidMS1000505", parsed.Clauses.Single().FilterSpecs.Single().ColumnId.Name);

            // Validation accepts the friendly forms.
            Assert.IsNull(SpectrumClassFilter.ValidateFilterString("MS:1000505 isdeclared"));
            Assert.IsNull(SpectrumClassFilter.ValidateFilterString("\"base peak intensity (MS:1000505)\" isdeclared"));

            // An accession inside a single-quoted OPERAND is left untouched (a string CV filter may match
            // literal text that happens to look like an accession); only the column reference is rewritten.
            AssertEx.AreEqual(
                SpectrumClassFilter.ParseFilterString("cvidMS1000512 contains 'cv=MS:1000505'"),
                SpectrumClassFilter.ParseFilterString("MS:1000512 contains 'cv=MS:1000505'"));

            // A userParam has no accession, so it is named with the explicit "userParam:" marker (bare for a
            // simple name; the marker is case-insensitive), resolving to the same "cvup..." column.
            var canonicalUp = SpectrumClassFilter.ParseFilterString(
                SpectrumClassColumn.CvParam("vendorSetting", null, false).ColumnName + " isdeclared");
            AssertEx.AreEqual(canonicalUp, SpectrumClassFilter.ParseFilterString("userParam:vendorSetting isdeclared"));
            AssertEx.AreEqual(canonicalUp, SpectrumClassFilter.ParseFilterString("USERPARAM:vendorSetting isdeclared"));
            // A userParam name with spaces uses the marker inside a double-quoted caption.
            var canonicalUpSpaces = SpectrumClassFilter.ParseFilterString(
                SpectrumClassColumn.CvParam("vendor setting", null, false).ColumnName + " isdeclared");
            AssertEx.AreEqual(canonicalUpSpaces,
                SpectrumClassFilter.ParseFilterString("\"userParam:vendor setting\" isdeclared"));
        }

        [TestMethod]
        public void TestOperatorAliases()
        {
            // Friendly, case-insensitive operator aliases parse to the same filter as the terse or cryptic
            // symbol, so an authored spectrum filter can use readable operator words matching the UI names.
            AssertEx.AreEqual(SpectrumClassFilter.ParseFilterString("MsLevel = 1"),
                SpectrumClassFilter.ParseFilterString("MsLevel equals 1"));
            AssertEx.AreEqual(SpectrumClassFilter.ParseFilterString("MsLevel > 1"),
                SpectrumClassFilter.ParseFilterString("MsLevel greaterthan 1"));
            // The cryptic blank symbols get readable aliases.
            AssertEx.AreEqual(SpectrumClassFilter.ParseFilterString("cvidMS1000512 isnullorblank"),
                SpectrumClassFilter.ParseFilterString("MS:1000512 isblank"));
            // Case-insensitive, including the operators that are already words.
            AssertEx.AreEqual(SpectrumClassFilter.ParseFilterString("cvidMS1000505 isdeclared"),
                SpectrumClassFilter.ParseFilterString("MS:1000505 ISDECLARED"));
            AssertEx.AreEqual(SpectrumClassFilter.ParseFilterString("cvidMS1000512 contains 'x'"),
                SpectrumClassFilter.ParseFilterString("cvidMS1000512 CONTAINS 'x'"));
            // An alias-looking word inside a single-quoted operand is not rewritten.
            AssertEx.AreEqual(SpectrumClassFilter.ParseFilterString("cvidMS1000512 contains 'equals'"),
                SpectrumClassFilter.ParseFilterString("MS:1000512 contains 'equals'"));
        }

        [TestMethod]
        public void TestInvalidFilterListsOperators()
        {
            // A filter whose operator is neither a symbol nor a known alias fails, and the error lists the
            // valid operator tokens so the vocabulary is discoverable. (The tokens are grammar keywords,
            // not localized text.)
            var error = SpectrumClassFilter.ValidateFilterString("MsLevel foobar 1");
            Assert.IsFalse(string.IsNullOrEmpty(error));
            StringAssert.Contains(error, "isdeclared");
            StringAssert.Contains(error, "isblank");
        }

        [TestMethod]
        public void TestNonsenseColumnReferences()
        {
            // A well-formed but non-existent CV accession is accepted (a term's identity is its accession;
            // it need not be in the compiled ontology) and simply matches nothing - it is not an error.
            Assert.IsNull(SpectrumClassFilter.ValidateFilterString("MS:9999999 isdeclared"));

            // A malformed accession (letters but no digits after the colon) is not a CV reference and, like
            // any bad column token, fails to parse.
            Assert.IsFalse(string.IsNullOrEmpty(SpectrumClassFilter.ValidateFilterString("MS: isdeclared")));
            Assert.IsFalse(string.IsNullOrEmpty(SpectrumClassFilter.ValidateFilterString("MS:abc isdeclared")));

            // A malformed encoded token (a "cvid"/"cvup" prefix that does not decode) is an unknown column.
            Assert.IsFalse(string.IsNullOrEmpty(SpectrumClassFilter.ValidateFilterString("cvidMSabc isdeclared")));
            Assert.IsFalse(string.IsNullOrEmpty(SpectrumClassFilter.ValidateFilterString("cvupZZ isdeclared")));

            // A userParam requires the explicit marker: a bare unknown name stays an unknown property (so a
            // typo of an interpreted column does not silently resolve to a no-match userParam)...
            Assert.IsFalse(string.IsNullOrEmpty(SpectrumClassFilter.ValidateFilterString("vendorSetting isdeclared")));
            // ...whereas the marker makes any name a valid userParam reference.
            Assert.IsNull(SpectrumClassFilter.ValidateFilterString("userParam:vendorSetting isdeclared"));
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
