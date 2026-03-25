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
                    FilterPredicate.Create(FilterOperations.OP_EQUALS, new SpectrumPrecursors(new[]
                    {
                        new SpectrumPrecursor(new SignedMz(422.5)),
                        new SpectrumPrecursor(new SignedMz(475.7))
                    }))),
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
            var spectrumPrecursors = new SpectrumPrecursors(new[]
            {
                new SpectrumPrecursor(new SignedMz(422.5)),
                new SpectrumPrecursor(new SignedMz(475.7))
            });
            var filterSpecs = new[]
            {
                new FilterSpec(SpectrumClassColumn.Ms2Precursors.PropertyPath,FilterOperations.OP_EQUALS, spectrumPrecursors),
                new FilterSpec(SpectrumClassColumn.ScanDescription.PropertyPath, FilterOperations.OP_CONTAINS, "SCAN")
            };
            return new SpectrumClassFilter(new FilterClause(filterSpecs));
        }
    }
}
