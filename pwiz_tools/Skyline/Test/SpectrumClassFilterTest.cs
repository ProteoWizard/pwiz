/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.CommonResources;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Filtering;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class SpectrumClassFilterTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestSpectrumClassFilter()
        {
            TestSpectrumPrecursorFilterOperand();
            TestCollisionEnergyFilter();
            TestCollisionEnergyComparisonLengthMismatch();
            TestSpectrumClassFilterSerialization();
            TestTransitionGroupSpectrumClassFilter();
            TestRoundTripSpectrumFilters();
            TestCvParamColumnRoundTrip();
            TestCvParamFilterPredicate();
            TestCvParamDeclaredFilter();
            TestDeclaredOperatorScoping();
            TestDiscoverCvColumns();
        }

        private void TestSpectrumPrecursorFilterOperand()
        {
            double precursorMz = 422.5;
            var expectedSpectrumPrecursors =
                ListColumnValue.FromItems(new object[] { PrecisionNumber.WithDecimalPlaces((decimal)precursorMz, 1) });
            var dataSchema = SkylineDataSchema.MemoryDataSchema(new SrmDocument(SrmSettingsList.GetDefault()),
                SkylineDataSchema.GetLocalizedSchemaLocalizer());
            var filterPredicate = FilterPredicate.Parse(dataSchema, typeof(SpectrumPrecursors),
                FilterOperations.OP_EQUALS, precursorMz.ToString(null, CultureInfo.CurrentCulture));
            var operandValue = filterPredicate.GetOperandValue(dataSchema, typeof(SpectrumPrecursors));
            Assert.IsInstanceOfType(operandValue, typeof(ListColumnValue<object>));
            Assert.AreEqual(expectedSpectrumPrecursors, operandValue);
        }

        private void TestCollisionEnergyFilter()
        {
            // Collision energy is read straight from the spectrum file as a positive magnitude (the
            // mzML/PSI convention), regardless of scan polarity, and the filter compares it as a plain
            // signed number with no magnitude folding: the value the user filters against is the value
            // shown in the grid. Scan polarity is pinned by the precursor charge, so it is never part of
            // a collision energy criterion.
            Predicate<SpectrumMetadata> Predicate(string criterion) =>
                SpectrumClassFilter.ParseFilterString(nameof(SpectrumClass.CollisionEnergy) + criterion).MakePredicate();

            SpectrumMetadata CeSpectrum(string id, double ce) => new SpectrumMetadata(id, 1.0)
                .ChangePrecursors(new[] { new[] { new SpectrumPrecursor(new SignedMz(500.0)).ChangeCollisionEnergy(ce) } });

            var spectrum = CeSpectrum(@"ce17", 17.0);
            Assert.IsTrue(Predicate(@" = 17")(spectrum));
            Assert.IsFalse(Predicate(@" = 25")(spectrum));

            // The entered precision controls the match tolerance (number of decimal places): CE 17.4 is
            // within integer tolerance of 17 but well outside the tolerance of 17.00.
            Assert.IsTrue(Predicate(@" = 17.00")(spectrum));
            Assert.IsFalse(Predicate(@" = 17.00")(CeSpectrum(@"ce17_4", 17.4)));

            // CollisionEnergy is a PositiveNumber column, so a negative operand is rejected at parse time
            // (by PositiveNumberFilterHandler) with the "cannot be negative" message. Zero is allowed.
            AssertEx.ThrowsException<FormatException>(
                () => SpectrumClassFilter.ParseFilterString(nameof(SpectrumClass.CollisionEnergy) + @" = -17"),
                MessageResources.PositiveNumberFilterHandler_RejectNegative_The_filter_value_cannot_be_negative_);

            // Ordered comparison operators work on the list-valued CollisionEnergy: the operator is
            // applied to each MS-level CE and, for a single operand, the criterion holds only if every
            // CE satisfies it. With a single CE of 17:
            Assert.IsTrue(Predicate(@" > 15")(spectrum));
            Assert.IsFalse(Predicate(@" > 17")(spectrum));   // strict ">" uses exact precision
            Assert.IsTrue(Predicate(@" < 20")(spectrum));
            Assert.IsTrue(Predicate(@" >= 17")(spectrum));
            Assert.IsTrue(Predicate(@" <= 17")(spectrum));

            // A spectrum with a CE per MS level: "> 15" holds only if every CE exceeds 15.
            var multiCe = new SpectrumMetadata(@"multi", 1.0).ChangePrecursors(new[]
            {
                new[] { new SpectrumPrecursor(new SignedMz(500.0)).ChangeCollisionEnergy(17.0) },
                new[] { new SpectrumPrecursor(new SignedMz(250.0)).ChangeCollisionEnergy(35.0) }
            });
            Assert.IsTrue(Predicate(@" > 15")(multiCe));     // both 17 and 35 are > 15
            Assert.IsFalse(Predicate(@" > 20")(multiCe));    // 17 is not > 20

            // A negative collision energy is still rejected regardless of the operator: with comparisons
            // now allowed, the negative value (not the operator) is the problem.
            AssertEx.ThrowsException<FormatException>(
                () => SpectrumClassFilter.ParseFilterString(nameof(SpectrumClass.CollisionEnergy) + @" > -20"),
                MessageResources.PositiveNumberFilterHandler_RejectNegative_The_filter_value_cannot_be_negative_);
        }

        private void TestCollisionEnergyComparisonLengthMismatch()
        {
            // A multi-value comparison operand pairwise-compares against the spectrum's CE list. This is
            // the predicate the extraction layer (SpectrumFilterPair.MatchesSpectrum) invokes per spectrum.
            // (Built directly: the spectrum-filter string grammar broadcasts a single value for a
            // comparison rather than accepting a multi-value operand, so this is only reachable
            // programmatically — but the predicate must still behave predictably.)
            var filter = new SpectrumClassFilter(new FilterClause(new[]
            {
                new FilterSpec(SpectrumClassColumn.CollisionEnergy.PropertyPath, FilterOperations.OP_IS_GREATER_THAN, @"15,30")
            }));
            var predicate = filter.MakePredicate();

            SpectrumMetadata CeSpectrum(string id, params double[] ces) => new SpectrumMetadata(id, 1.0)
                .ChangePrecursors(ces.Select(ce => (IEnumerable<SpectrumPrecursor>)
                    new[] { new SpectrumPrecursor(new SignedMz(500.0)).ChangeCollisionEnergy(ce) }));

            // Equal lengths compare pairwise: a multi-value comparison is perfectly valid when it lines up.
            Assert.IsTrue(predicate(CeSpectrum(@"ok", 17, 35)));    // 17 > 15 and 35 > 30
            Assert.IsFalse(predicate(CeSpectrum(@"no", 17, 25)));   // 25 is not > 30

            // Length mismatch (3 vs 2), neither a single value: the comparison throws rather than
            // silently returning a misleading false. MakePredicate wraps it as an InvalidDataException with
            // spectrum-filter context, so chromatogram extraction reports a clear, meaningful error (which
            // its standard exception handling surfaces) rather than faulting with an opaque exception.
            AssertEx.ThrowsException<InvalidDataException>(
                () => predicate(CeSpectrum(@"mismatch", 17, 35, 50)),
                string.Format(SpectraResources.SpectrumClassFilter_MakePredicate_Error_evaluating_the_spectrum_filter___0_,
                    string.Format(
                        MessageResources.ListFilterHandler_MatchesComparison_Cannot_compare_a_list_of__0__values_with_a_list_of__1__values_,
                        3, 2)));
        }

        private static SpectrumMetadata CvSpectrum(string id, string accession, string name, string value, string unit)
        {
            return new SpectrumMetadata(id, 1.0)
                .ChangeOtherParams(new[] { new SpectrumMetadataTerm(accession, name, value, unit) });
        }

        private void TestCvParamColumnRoundTrip()
        {
            // A dynamic mzML CV column is not one of the static columns, but it must resolve from its
            // encoded property path (so saved filters validate and reload before any file is imported),
            // and it must survive the filter-string round-trip that persists to .sky and the cache.
            const string accession = @"MS:1000505";
            var column = SpectrumClassColumn.CvParam(accession, @"base peak intensity", true);

            // The encoded column name is a single alphanumeric identifier (so PropertyPath and the
            // filter-string serializer never have to quote it), and readably encodes the CV accession.
            Assert.IsTrue(column.ColumnName.All(char.IsLetterOrDigit), @"CV column name must be alphanumeric: " + column.ColumnName);
            Assert.AreEqual(@"cvidMS1000505", column.ColumnName);

            // FindColumn reconstructs a CV column from the path alone (no backing data).
            var reconstructed = SpectrumClassColumn.FindColumn(column.PropertyPath);
            Assert.IsNotNull(reconstructed);
            Assert.IsTrue(SpectrumClassColumn.IsCvParamColumn(reconstructed));
            Assert.AreEqual(column.PropertyPath, reconstructed.PropertyPath);

            // The path also round-trips through PropertyPath's own string form.
            Assert.AreEqual(column.PropertyPath, PropertyPath.Parse(column.PropertyPath.ToString()));

            // A filter built on the CV column round-trips through the filter-string form and validates.
            var filter = new SpectrumClassFilter(new FilterClause(new[]
            {
                new FilterSpec(column.PropertyPath, FilterOperations.OP_IS_GREATER_THAN, @"500"),
                new FilterSpec(SpectrumClassColumn.MsLevel.PropertyPath, FilterOperations.OP_EQUALS, 1)
            }));
            var filterString = filter.ToFilterString();
            Assert.AreEqual(filter, SpectrumClassFilter.ParseFilterString(filterString));
            Assert.IsNull(SpectrumClassFilter.ValidateFilterString(filterString),
                @"CV filter string should validate: " + filterString);

            // The capture gate: a filter that references a CV column reports it (so extraction knows to
            // capture the otherwise-dropped terms); a filter without one, and the empty filter, do not.
            Assert.IsTrue(filter.ReferencesCvColumns());
            Assert.IsFalse(new SpectrumClassFilter(new FilterClause(new[]
            {
                new FilterSpec(SpectrumClassColumn.MsLevel.PropertyPath, FilterOperations.OP_EQUALS, 1)
            })).ReferencesCvColumns());
            Assert.IsFalse(default(SpectrumClassFilter).ReferencesCvColumns());

            // A vendor userParam (arbitrary name, no CVID) encodes via the hex scheme and reconstructs too.
            var userParamColumn = SpectrumClassColumn.CvParam(@"vendorSetting", @"vendorSetting", false);
            Assert.IsTrue(userParamColumn.ColumnName.StartsWith(@"cvup"));
            var userParamReconstructed = SpectrumClassColumn.FindColumn(userParamColumn.PropertyPath);
            Assert.IsNotNull(userParamReconstructed);
            Assert.AreEqual(userParamColumn.PropertyPath, userParamReconstructed.PropertyPath);
        }

        private void TestCvParamFilterPredicate()
        {
            const string accession = @"MS:1000505";
            const string name = @"base peak intensity";
            const string unit = @"number of detector counts";
            var numericColumn = SpectrumClassColumn.CvParam(accession, name, true);

            Predicate<SpectrumMetadata> Numeric(IFilterOperation op, string operand) =>
                new SpectrumClassFilter(new FilterClause(new[] { new FilterSpec(numericColumn.PropertyPath, op, operand) }))
                    .MakePredicate();

            // Numeric comparison filters include/exclude by the parsed value; a spectrum lacking the term
            // simply does not match (no value), it does not error.
            var big = CvSpectrum(@"big", accession, name, @"1000", unit);
            var small = CvSpectrum(@"small", accession, name, @"100", unit);
            var absent = new SpectrumMetadata(@"absent", 1.0);
            Assert.IsTrue(Numeric(FilterOperations.OP_IS_GREATER_THAN, @"500")(big));
            Assert.IsFalse(Numeric(FilterOperations.OP_IS_GREATER_THAN, @"500")(small));
            Assert.IsFalse(Numeric(FilterOperations.OP_IS_GREATER_THAN, @"500")(absent));
            Assert.IsTrue(Numeric(FilterOperations.OP_IS_LESS_THAN, @"500")(small));
            Assert.IsTrue(Numeric(FilterOperations.OP_EQUALS, @"1000")(big));
            Assert.IsFalse(Numeric(FilterOperations.OP_EQUALS, @"999")(big));

            // A numeric filter that meets a present but non-numeric value hard-fails with filter context
            // (user decision), so chromatogram extraction reports a clear error rather than skipping it.
            // The predicate reconstructs the column from the persisted filter path, which carries the
            // accession but not the friendly name (that is resolved from imported data on the interactive
            // surfaces), so the error names the property by its accession.
            var nonNumeric = CvSpectrum(@"bad", accession, name, @"not a number", unit);
            var columnDisplay = accession;
            AssertEx.ThrowsException<InvalidDataException>(
                () => Numeric(FilterOperations.OP_IS_GREATER_THAN, @"500")(nonNumeric),
                string.Format(SpectraResources.SpectrumClassFilter_MakePredicate_Error_evaluating_the_spectrum_filter___0_,
                    string.Format(
                        SpectraResources.SpectrumClassFilter_CoerceCvValue_The_value___0___of_spectrum_property___1___is_not_a_number,
                        @"not a number", columnDisplay)));

            // A string term filters with equals/contains. Identity is the accession; the term's unit is
            // not part of it (units are unavailable from the ontology), so matching is unit-independent.
            var stringColumn = SpectrumClassColumn.CvParam(@"MS:1000512", @"filter string", false);
            Predicate<SpectrumMetadata> StringFilter(IFilterOperation op, string operand) =>
                new SpectrumClassFilter(new FilterClause(new[] { new FilterSpec(stringColumn.PropertyPath, op, operand) }))
                    .MakePredicate();
            var thermo = CvSpectrum(@"thermo", @"MS:1000512", @"filter string", @"FTMS + p ESI Full ms", null);
            Assert.IsTrue(StringFilter(FilterOperations.OP_CONTAINS, @"ESI")(thermo));
            Assert.IsFalse(StringFilter(FilterOperations.OP_CONTAINS, @"CID")(thermo));
            Assert.IsTrue(StringFilter(FilterOperations.OP_EQUALS, @"FTMS + p ESI Full ms")(thermo));

            // Identity is the accession alone: a column matches a term with the same accession regardless
            // of the unit the term happens to carry.
            var sameAccessionDifferentUnitTerm = CvSpectrum(@"pct", accession, name, @"1000", @"percent of base peak");
            Assert.IsTrue(Numeric(FilterOperations.OP_IS_GREATER_THAN, @"500")(sameAccessionDifferentUnitTerm));

            // Equals/Not Equals with a numeric operand does NOT hard-fail the way the ordered comparisons
            // do. It compares numerically where the term's value is a number (so scientific notation still
            // matches "1000"), and by string otherwise, so a string term meeting a numeric operand is just
            // compared as text rather than aborting extraction.
            var scientific = CvSpectrum(@"sci", accession, name, @"1.0e03", unit);
            Assert.IsTrue(Numeric(FilterOperations.OP_EQUALS, @"1000")(scientific));
            Assert.IsFalse(Numeric(FilterOperations.OP_EQUALS, @"999")(scientific));
            // The string term "filter string" holds non-numeric text; "equals 5" must string-compare
            // (no match, no throw), while a term whose value really is "5" matches via the numeric path.
            Assert.IsFalse(StringFilter(FilterOperations.OP_EQUALS, @"5")(thermo));
            var literalFive = CvSpectrum(@"five", @"MS:1000512", @"filter string", @"5", null);
            Assert.IsTrue(StringFilter(FilterOperations.OP_EQUALS, @"5")(literalFive));
            Assert.IsTrue(StringFilter(FilterOperations.OP_NOT_EQUALS, @"5")(thermo));
            Assert.IsFalse(StringFilter(FilterOperations.OP_NOT_EQUALS, @"5")(literalFive));
        }

        private void TestCvParamDeclaredFilter()
        {
            // "Is Declared"/"Is Not Declared" test only whether a CV/user term is present in the spectrum,
            // independent of any value it carries. This is the only way to match a value-less flag term
            // (e.g. "zoom scan"), which captures with an empty value and so reads as blank under the value
            // operators. Identity is the accession alone, as for the value operators.
            const string accession = @"MS:1000497";
            const string name = @"zoom scan";
            var flagColumn = SpectrumClassColumn.CvParam(accession, name, false);

            Predicate<SpectrumMetadata> Declared(IFilterOperation op) =>
                new SpectrumClassFilter(new FilterClause(new[] { new FilterSpec(flagColumn.PropertyPath, op, (string)null) }))
                    .MakePredicate();

            // A value-less flag term captures with an empty (non-null) value: it is Declared.
            var flagPresent = CvSpectrum(@"flag", accession, name, string.Empty, null);
            // A value-bearing term of the same accession is likewise Declared.
            var valued = CvSpectrum(@"valued", accession, name, @"1", null);
            // A spectrum lacking the term entirely is not Declared.
            var absent = new SpectrumMetadata(@"absent", 1.0);

            Assert.IsTrue(Declared(FilterOperations.OP_IS_DECLARED)(flagPresent));
            Assert.IsTrue(Declared(FilterOperations.OP_IS_DECLARED)(valued));
            Assert.IsFalse(Declared(FilterOperations.OP_IS_DECLARED)(absent));

            Assert.IsFalse(Declared(FilterOperations.OP_IS_NOT_DECLARED)(flagPresent));
            Assert.IsFalse(Declared(FilterOperations.OP_IS_NOT_DECLARED)(valued));
            Assert.IsTrue(Declared(FilterOperations.OP_IS_NOT_DECLARED)(absent));

            // The distinction from Is Not Blank, which is exactly why Declared exists: a present flag has
            // an empty value, so Is Not Blank reports it as absent, whereas Is Declared matches it.
            Predicate<SpectrumMetadata> NotBlank() =>
                new SpectrumClassFilter(new FilterClause(new[]
                    { new FilterSpec(flagColumn.PropertyPath, FilterOperations.OP_IS_NOT_BLANK, (string)null) })).MakePredicate();
            Assert.IsFalse(NotBlank()(flagPresent),
                @"a present flag has an empty value, so Is Not Blank does not match it (Is Declared does)");

            // A Declared filter references a CV column, so it triggers term capture during extraction, and
            // its persisted filter string validates and re-parses to a predicate that behaves identically.
            var filter = new SpectrumClassFilter(new FilterClause(new[]
                { new FilterSpec(flagColumn.PropertyPath, FilterOperations.OP_IS_DECLARED, (string)null) }));
            Assert.IsTrue(filter.ReferencesCvColumns());
            var filterString = filter.ToFilterString();
            Assert.IsNull(SpectrumClassFilter.ValidateFilterString(filterString),
                @"Declared filter string should validate: " + filterString);
            var roundTripped = SpectrumClassFilter.ParseFilterString(filterString).MakePredicate();
            Assert.IsTrue(roundTripped(flagPresent));
            Assert.IsFalse(roundTripped(absent));
        }

        private void TestDeclaredOperatorScoping()
        {
            // The Declared operators are registered - so they serialize by symbol and the spectrum-filter
            // editor, which lists every operation, offers them for the CV columns.
            var ops = FilterOperations.ListOperations();
            Assert.IsTrue(ops.Contains(FilterOperations.OP_IS_DECLARED));
            Assert.IsTrue(ops.Contains(FilterOperations.OP_IS_NOT_DECLARED));
            Assert.AreSame(FilterOperations.OP_IS_DECLARED,
                FilterOperations.GetOperationBySymbol(FilterOperations.OP_IS_DECLARED.OpSymbol));
            Assert.AreSame(FilterOperations.OP_IS_NOT_DECLARED,
                FilterOperations.GetOperationBySymbol(FilterOperations.OP_IS_NOT_DECLARED.OpSymbol));

            // ...but they are gated OUT of the general report/quick filters, which honor IsValidFor. For an
            // ordinary column a presence test is redundant with Is Blank/Has Any Value, so IsValidFor is
            // false for every filter handler. This scoping (false everywhere, plus the spectrum editor's
            // ungated list) is what confines them to spectrum filtering; if it regresses they leak into
            // every filter UI in the application.
            var dataSchema = new DataSchema();
            foreach (var type in new[] { typeof(string), typeof(double) })
            {
                var handler = dataSchema.GetFilterHandler(type);
                Assert.IsFalse(FilterOperations.OP_IS_DECLARED.IsValidFor(handler),
                    @"Is Declared must not be offered in report/quick filters for " + type);
                Assert.IsFalse(FilterOperations.OP_IS_NOT_DECLARED.IsValidFor(handler),
                    @"Is Not Declared must not be offered in report/quick filters for " + type);
            }
            // Sanity: an ordinary unary operator IS valid for such a column, so the assertions above
            // reflect the Declared ops' own gating rather than a handler that rejects everything.
            Assert.IsTrue(FilterOperations.OP_IS_NOT_BLANK.IsValidFor(dataSchema.GetFilterHandler(typeof(string))));
        }

        private void TestDiscoverCvColumns()
        {
            SpectrumMetadata Spectrum(string id, params SpectrumMetadataTerm[] terms) =>
                new SpectrumMetadata(id, 1.0).ChangeOtherParams(terms);

            const string counts = @"number of detector counts";
            var specA = Spectrum(@"a",
                new SpectrumMetadataTerm(@"MS:1000505", @"base peak intensity", @"500", counts),
                new SpectrumMetadataTerm(@"MS:1000512", @"filter string", @"FTMS + p NSI", null));
            var specB = Spectrum(@"b",
                new SpectrumMetadataTerm(@"MS:1000505", @"base peak intensity", @"600", counts),
                new SpectrumMetadataTerm(@"MS:1000900", @"custom", @"5", @"ea"));
            // The same accession with a non-numeric value makes that column string-typed.
            var specC = Spectrum(@"c", new SpectrumMetadataTerm(@"MS:1000900", @"custom", @"abc", @"ea"));

            var columns = SpectrumClassColumn.DiscoverCvColumns(new[] { specA, specB, specC });
            Assert.AreEqual(3, columns.Count);
            Assert.IsTrue(columns.All(SpectrumClassColumn.IsCvParamColumn));

            SpectrumClassColumn Find(string accession) =>
                columns.Single(c => Equals(c.PropertyPath, SpectrumClassColumn.CvParam(accession, null, false).PropertyPath));

            // Every value seen for base peak intensity parses as a number, so it is numeric, and it
            // displays its friendly name with the accession as the cue.
            var bpi = Find(@"MS:1000505");
            Assert.AreEqual(typeof(double), bpi.ValueType);
            var bpiName = bpi.GetLocalizedColumnName(CultureInfo.CurrentCulture);
            Assert.IsTrue(bpiName.Contains(@"base peak intensity") && bpiName.Contains(@"MS:1000505"), bpiName);

            // The filter string (no numeric value) and the mixed-value custom term are string-typed.
            Assert.AreEqual(typeof(string), Find(@"MS:1000512").ValueType);
            Assert.AreEqual(typeof(string), Find(@"MS:1000900").ValueType);
        }

        private void TestSpectrumClassFilterSerialization()
        {
            var spectrumClassFilter = CreateTestSpectrumClassFilter();
            VerifyRoundTrip(spectrumClassFilter);
        }

        private void TestTransitionGroupSpectrumClassFilter()
        {
            var peptide = new Peptide("ELVIS");
            var srmSettings = SrmSettingsList.GetDefault();
            var transitionGroupDocNode = new TransitionGroupDocNode(
                new TransitionGroup(peptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light),
                Annotations.EMPTY, srmSettings, ExplicitMods.EMPTY, null, ExplicitTransitionGroupValues.EMPTY, null,
                Array.Empty<TransitionDocNode>(), false);
            transitionGroupDocNode = transitionGroupDocNode.ChangeSpectrumClassFilter(CreateTestSpectrumClassFilter());
            var peptideDocNode = (PeptideDocNode) new PeptideDocNode(peptide).ChangeChildren(new[] {transitionGroupDocNode});
            var peptideGroupDocNode =
                new PeptideGroupDocNode(new PeptideGroup(), "MyPeptideGroup", null, new[] {peptideDocNode});
            var srmDocument = (SrmDocument) new SrmDocument(srmSettings).ChangeChildren(new[] {peptideGroupDocNode});
            AssertEx.Serializable(srmDocument);
        }

        private void TestRoundTripSpectrumFilters()
        {
            var filterSpecs = new[]
            {
                new FilterSpec(SpectrumClassColumn.Ms2Precursors.PropertyPath,
                    FilterPredicate.Create(FilterOperations.OP_EQUALS, "422.5,475.7")),
                new FilterSpec(SpectrumClassColumn.MsLevel.PropertyPath,
                    FilterPredicate.Create(FilterOperations.OP_EQUALS, 1)),
                new FilterSpec(SpectrumClassColumn.MsLevel.PropertyPath, FilterPredicate.Create(FilterOperations.OP_IS_GREATER_THAN, 1)),
                new FilterSpec(SpectrumClassColumn.PresetScanConfiguration.PropertyPath, new FilterPredicate(FilterOperations.OP_IS_BLANK, null )),
                new FilterSpec(SpectrumClassColumn.ScanDescription.PropertyPath, new FilterPredicate(FilterOperations.OP_CONTAINS, "hello"))
            };
            var clauses = Enumerable.Range(1, filterSpecs.Length - 1).SelectMany(count => new[]
            {
                new FilterClause(filterSpecs.Take(count)),
                new FilterClause(filterSpecs.Reverse().Take(count))
            }).ToList();
            foreach (var clause1 in clauses)
            {
                VerifyRoundTrip(new SpectrumClassFilter(clause1));
                foreach (var clause2 in clauses)
                {
                    VerifyRoundTrip(new SpectrumClassFilter(clause1, clause2));
                    foreach (var clause3 in clauses)
                    {
                        VerifyRoundTrip(new SpectrumClassFilter(clause1, clause2, clause3));
                    }
                }
            }
        }

        private void VerifyRoundTrip(SpectrumClassFilter spectrumClassFilter)
        {
            foreach (var filterClause in spectrumClassFilter.Clauses)
            {
                VerifyXmlRoundTrip(filterClause);
            }
            VerifyToFilterStringRoundTrip(spectrumClassFilter);
        }

        private void VerifyXmlRoundTrip(FilterClause filterClause)
        {
            var xmlSerializer = new XmlSerializer(typeof(FilterClause));
            var stream = new MemoryStream();
            xmlSerializer.Serialize(stream, filterClause);
            stream.Seek(0, SeekOrigin.Begin);
            var roundTrip = xmlSerializer.Deserialize(stream);
            Assert.AreEqual(filterClause, roundTrip);
        }

        private void VerifyToFilterStringRoundTrip(SpectrumClassFilter spectrumClassFilter)
        {
            var text = spectrumClassFilter.ToFilterString();
            var roundTrip = SpectrumClassFilter.ParseFilterString(text);
            Assert.AreEqual(spectrumClassFilter, roundTrip);
        }

        private SpectrumClassFilter CreateTestSpectrumClassFilter()
        {
            var filterSpecs = new[]
            {
                new FilterSpec(SpectrumClassColumn.Ms2Precursors.PropertyPath,FilterOperations.OP_EQUALS, "422.5,475.7"),
                new FilterSpec(SpectrumClassColumn.ScanDescription.PropertyPath, FilterOperations.OP_CONTAINS, "SCAN")
            };
            return new SpectrumClassFilter(new FilterClause(filterSpecs));
        }
    }
}
