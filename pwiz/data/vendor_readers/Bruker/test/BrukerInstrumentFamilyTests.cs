namespace Pwiz.Vendor.Bruker.Tests;

[TestClass]
public class BrukerInstrumentFamilyTests
{
    /// <summary>
    /// The raw <c>InstrumentFamily</c> code maps to the same family for every Bruker container
    /// format, TDF / TSF / BAF alike.
    /// </summary>
    /// <remarks>
    /// <para>Guards a deliberate divergence from cpp, whose <c>translateInstrumentFamily</c> sits
    /// in an anonymous namespace so each of <c>Baf2Sql.cpp</c>, <c>TimsData.cpp</c> and
    /// <c>TsfData.cpp</c> compiles its own copy - and the BAF copy is missing
    /// <c>case 9</c> (timsTOF). A timsTOF can write BAF, so that is a defect rather than a format
    /// distinction, and matching it here would be porting a bug.</para>
    /// <para>Needs no SDK and no data file, so it runs on a CI leg built without vendor archives.
    /// The Bruker file that shows the divergence is corpus-only with no reference mzML, so the
    /// vendor harness cannot cover this.</para>
    /// </remarks>
    [TestMethod]
    public void InstrumentFamilyCode_MapsIdenticallyForEveryContainerFormat()
    {
        (string Raw, BrukerInstrumentFamily Expected)[] cases =
        {
            ("1", BrukerInstrumentFamily.Otof),
            ("2", BrukerInstrumentFamily.OtofQ),
            ("6", BrukerInstrumentFamily.Maxis),
            ("7", BrukerInstrumentFamily.Impact),
            ("8", BrukerInstrumentFamily.Compact),
            ("9", BrukerInstrumentFamily.TimsTof),   // cpp's BAF copy returns Unknown here
            ("512", BrukerInstrumentFamily.Ftms),
            ("513", BrukerInstrumentFamily.SolariX),
            ("0", BrukerInstrumentFamily.Unknown),
            ("4242", BrukerInstrumentFamily.Unknown),
            ("not-a-number", BrukerInstrumentFamily.Unknown),
            ("", BrukerInstrumentFamily.Unknown),
        };

        foreach (var (raw, expected) in cases)
        {
            var actual = BrukerInstrumentFamilyCodes.FromGlobalMetadata(
                new Dictionary<string, string> { ["InstrumentFamily"] = raw });
            Assert.AreEqual(expected, actual, $"InstrumentFamily=\"{raw}\"");
        }

        // Absent key: the metadata table of a file that does not record the family at all.
        Assert.AreEqual(BrukerInstrumentFamily.Unknown,
            BrukerInstrumentFamilyCodes.FromGlobalMetadata(new Dictionary<string, string>()),
            "missing InstrumentFamily key");
    }
}
