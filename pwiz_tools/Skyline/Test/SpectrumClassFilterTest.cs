using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.Spectra;
using pwiz.Skyline.Model;
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
                new SpectrumPrecursors(ImmutableList.Singleton(new SpectrumPrecursor(new SignedMz(precursorMz))));
            var dataSchema = new DataSchema(new DataSchemaLocalizer(CultureInfo.CurrentCulture, CultureInfo.CurrentCulture));
            var filterPredicate = FilterPredicate.CreateFilterPredicate(dataSchema, typeof(SpectrumPrecursors),
                FilterOperations.OP_EQUALS, expectedSpectrumPrecursors.ToString(null, CultureInfo.CurrentCulture));
            var operandValue = filterPredicate.GetOperandValue(dataSchema, typeof(SpectrumPrecursors));
            Assert.IsInstanceOfType(operandValue, typeof(SpectrumPrecursors));
            var spectrumPrecursors = operandValue as SpectrumPrecursors;
            Assert.AreEqual(expectedSpectrumPrecursors, spectrumPrecursors);
        }

        [TestMethod]
        public void TestSpectrumClassFilterSerialization()
        {
            var spectrumClassFilter = CreateTestSpectrumClassFilter();
            var xmlSerializer = new XmlSerializer(typeof(SpectrumClassFilter));
            var stream = new MemoryStream();
            xmlSerializer.Serialize(stream, spectrumClassFilter);
            stream.Seek(0, SeekOrigin.Begin);
            var roundTrip = xmlSerializer.Deserialize(stream);
            Assert.AreEqual(spectrumClassFilter, roundTrip);
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

        private SpectrumClassFilter CreateTestSpectrumClassFilter()
        {
            var spectrumPrecursors = new SpectrumPrecursors(new[]
            {
                new SpectrumPrecursor(new SignedMz(422.5)),
                new SpectrumPrecursor(new SignedMz(475.7))
            });
            var filterSpecs = new[]
            {
                new FilterSpec(SpectrumClassColumn.Ms2Precursors.PropertyPath,
                    FilterPredicate.CreateFilterPredicate(FilterOperations.OP_EQUALS, spectrumPrecursors)),
                new FilterSpec(SpectrumClassColumn.ScanDescription.PropertyPath,
                    FilterPredicate.CreateFilterPredicate(FilterOperations.OP_CONTAINS, "SCAN"))
            };
            return new SpectrumClassFilter(filterSpecs);
        }
    }
}
