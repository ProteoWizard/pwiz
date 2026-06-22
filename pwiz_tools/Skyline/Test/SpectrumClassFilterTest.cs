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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
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
        public void TestSpectrumPrecursorFilterOperand()
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

        [TestMethod]
        public void TestCollisionEnergyFilter()
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

            // A negative collision energy is rejected at parse time with the specific "must be positive"
            // message (not the generic "invalid format" one). Equality is a valid operator for the
            // list-valued CollisionEnergy property, so only the negative sign is at fault here.
            AssertEx.ThrowsException<FormatException>(
                () => SpectrumClassFilter.ParseFilterString(nameof(SpectrumClass.CollisionEnergy) + @" = -17"),
                SpectraResources.SpectrumClassFilter_ValidateCollisionEnergyOperands_Collision_energy_must_be_a_positive_value);

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
                SpectraResources.SpectrumClassFilter_ValidateCollisionEnergyOperands_Collision_energy_must_be_a_positive_value);
        }

        [TestMethod]
        public void TestSpectrumClassFilterSerialization()
        {
            var spectrumClassFilter = CreateTestSpectrumClassFilter();
            VerifyRoundTrip(spectrumClassFilter);
        }

        [TestMethod]
        public void TestTransitionGroupSpectrumClassFilter()
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

        [TestMethod]
        public void TestRoundTripSpectrumFilters()
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
