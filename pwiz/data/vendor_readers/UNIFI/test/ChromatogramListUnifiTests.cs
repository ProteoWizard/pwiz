using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Vendor.UNIFI.Tests;

[TestClass]
public class ChromatogramListUnifiTests
{
    [TestMethod]
    public void Index_CollapsesPerFunctionTicsIntoSingleEntry()
    {
        // cpp ChromatogramList_UNIFI.cpp:252-272: many "(TIC)" entries (one per function),
        // we emit a single "TIC" chromatogram. BPI is currently disabled in cpp; verify we
        // skip it too.
        var infos = new List<UnifiChromatogramInfo>
        {
            new() { Index = 0, Type = ChromatogramType.TIC, Name = "1: TOF MSe Low (TIC)" },
            new() { Index = 1, Type = ChromatogramType.TIC, Name = "1: TOF MSe Low (BPC)" }, // BPI — should NOT add a separate entry
            new() { Index = 2, Type = ChromatogramType.TIC, Name = "2: TOF MSe High (TIC)" },
            new() { Index = 3, Type = ChromatogramType.TIC, Name = "Integrated: TIC" }, // skipped: "Integrated"
        };
        var list = new ChromatogramList_UNIFI(new FakeSource(infos), globalChromatogramsAreMs1Only: false);
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("TIC", list.ChromatogramIdentity(0).Id);
    }

    [TestMethod]
    public void Index_SrmEntries_PreserveSignAndPolarityPrefix()
    {
        // cpp ChromatogramList_UNIFI.cpp:275-287 prepends "+ " / "- " when the chromatogram
        // name ends with the matching polarity character.
        var infos = new List<UnifiChromatogramInfo>
        {
            new() { Index = 0, Type = ChromatogramType.MRM, Name = "Q1=100 → Q3=50 +" },
            new() { Index = 1, Type = ChromatogramType.MRM, Name = "Q1=200 → Q3=70 -" },
            new() { Index = 2, Type = ChromatogramType.MRM, Name = "Q1=300 → Q3=90" },
        };
        var list = new ChromatogramList_UNIFI(new FakeSource(infos), false);
        Assert.AreEqual(3, list.Count);
        Assert.AreEqual("+ SRM SIC Q1=100 → Q3=50 +", list.ChromatogramIdentity(0).Id);
        Assert.AreEqual("- SRM SIC Q1=200 → Q3=70 -", list.ChromatogramIdentity(1).Id);
        Assert.AreEqual("SRM SIC Q1=300 → Q3=90", list.ChromatogramIdentity(2).Id);
    }

    [TestMethod]
    public void GetChromatogram_TIC_SumsAcrossFunctions()
    {
        // Two functions, each with three RTs: [0, 0.5, 1.0]. globalChromatogramsAreMs1Only=false
        // → both contribute. The synthesized TIC has 6 (rt, intensity) pairs back-sorted by RT.
        var infos = new List<UnifiChromatogramInfo>
        {
            new() { Index = 0, Type = ChromatogramType.TIC, Name = "1: TOF MS Low (TIC)" },
            new() { Index = 1, Type = ChromatogramType.TIC, Name = "2: TOF MS High (TIC)" },
        };
        var src = new FakeSource(infos);
        src.ChromatogramFor[0] = new UnifiChromatogram
        {
            ArrayLength = 3,
            TimeArray = new[] { 0.0, 0.5, 1.0 },
            IntensityArray = new[] { 100.0, 200.0, 300.0 },
        };
        src.ChromatogramFor[1] = new UnifiChromatogram
        {
            ArrayLength = 3,
            TimeArray = new[] { 0.25, 0.5, 0.75 },
            IntensityArray = new[] { 50.0, 60.0, 70.0 },
        };

        var list = new ChromatogramList_UNIFI(src, globalChromatogramsAreMs1Only: false);
        var c = list.GetChromatogram(0, getBinaryData: true);

        Assert.AreEqual("TIC", c.Id);
        Assert.IsTrue(c.Params.HasCVParam(CVID.MS_TIC_chromatogram));
        Assert.AreEqual(6, c.DefaultArrayLength); // 3 + 3 entries
        // Pairs sorted by RT: 0, 0.25, 0.5(fn1), 0.5(fn2), 0.75, 1.0.
        CollectionAssert.AreEqual(new[] { 0.0, 0.25, 0.5, 0.5, 0.75, 1.0 }, c.BinaryDataArrays[0].Data);
        // Intensities follow the matching insertion order; both 0.5 entries come from fn1 then fn2.
        CollectionAssert.AreEqual(new[] { 100.0, 50.0, 200.0, 60.0, 70.0, 300.0 }, c.BinaryDataArrays[1].Data);
    }

    [TestMethod]
    public void GetChromatogram_TIC_GlobalIsMs1Only_RestrictsToFunctionOne()
    {
        // cpp ChromatogramList_UNIFI.cpp:108-109 — drop functions != 1 when the flag is set.
        var infos = new List<UnifiChromatogramInfo>
        {
            new() { Index = 0, Type = ChromatogramType.TIC, Name = "1: TOF MS Low (TIC)" },
            new() { Index = 1, Type = ChromatogramType.TIC, Name = "2: TOF MS High (TIC)" },
        };
        var src = new FakeSource(infos);
        src.ChromatogramFor[0] = new UnifiChromatogram { ArrayLength = 1, TimeArray = new[] { 0.0 }, IntensityArray = new[] { 1.0 } };
        src.ChromatogramFor[1] = new UnifiChromatogram { ArrayLength = 1, TimeArray = new[] { 0.5 }, IntensityArray = new[] { 9.0 } };

        var list = new ChromatogramList_UNIFI(src, globalChromatogramsAreMs1Only: true);
        var c = list.GetChromatogram(0, true);
        Assert.AreEqual(1, c.DefaultArrayLength);
        CollectionAssert.AreEqual(new[] { 0.0 }, c.BinaryDataArrays[0].Data);
        CollectionAssert.AreEqual(new[] { 1.0 }, c.BinaryDataArrays[1].Data);
    }

    [TestMethod]
    public void GetChromatogram_SRM_PopulatesQ1Q3AndPolarity()
    {
        var infos = new List<UnifiChromatogramInfo>
        {
            new() { Index = 0, Type = ChromatogramType.MRM, Name = "Q1=489 -> Q3=120 +" },
        };
        var src = new FakeSource(infos);
        src.ChromatogramFor[0] = new UnifiChromatogram
        {
            ArrayLength = 2,
            Q1 = 489.5,
            Q3 = 120.0,
            Polarity = UnifiPolarity.Positive,
            TimeArray = new[] { 0.5, 1.0 },
            IntensityArray = new[] { 1234.0, 5678.0 },
        };
        var list = new ChromatogramList_UNIFI(src, false);
        var c = list.GetChromatogram(0, getBinaryData: true);

        Assert.IsTrue(c.Params.HasCVParam(CVID.MS_SRM_chromatogram));
        Assert.IsTrue(c.Params.HasCVParam(CVID.MS_positive_scan));
        Assert.AreEqual(489.5, c.Precursor.IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>());
        Assert.AreEqual(120.0, c.Product.IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>());
        CollectionAssert.AreEqual(new[] { 0.5, 1.0 }, c.BinaryDataArrays[0].Data);
        CollectionAssert.AreEqual(new[] { 1234.0, 5678.0 }, c.BinaryDataArrays[1].Data);
    }

    [TestMethod]
    public void GetChromatogram_UV_UsesAbsorbanceUnit()
    {
        // cpp ChromatogramList_UNIFI.cpp:222-234: UV chromatograms use absorbance unit on
        // the intensity array.
        var infos = new List<UnifiChromatogramInfo>
        {
            new() { Index = 0, Type = ChromatogramType.UV, Name = "Detector A 254nm" },
        };
        var src = new FakeSource(infos);
        src.ChromatogramFor[0] = new UnifiChromatogram
        {
            ArrayLength = 1,
            TimeArray = new[] { 0.0 },
            IntensityArray = new[] { 0.5 },
        };
        var list = new ChromatogramList_UNIFI(src, false);
        var c = list.GetChromatogram(0, getBinaryData: true);

        Assert.AreEqual("Detector A 254nm", c.Id);
        Assert.IsTrue(c.Params.HasCVParam(CVID.MS_absorption_chromatogram));
        Assert.AreEqual(2, c.BinaryDataArrays.Count);
        // Intensity array's unit is absorbance.
        Assert.AreEqual(CVID.UO_absorbance_unit, c.BinaryDataArrays[1].CvParam(CVID.MS_intensity_array).Units);
    }

    [TestMethod]
    public void Index_SkipsUnsupportedDetectorTypes()
    {
        var infos = new List<UnifiChromatogramInfo>
        {
            new() { Index = 0, Type = ChromatogramType.TIC, Name = "1: MS (TIC)" },
            new() { Index = 1, Type = ChromatogramType.IR, Name = "IR" },     // unsupported
            new() { Index = 2, Type = ChromatogramType.NMR, Name = "NMR" },   // unsupported
            new() { Index = 3, Type = ChromatogramType.SIM, Name = "SIM" },   // unsupported
        };
        var list = new ChromatogramList_UNIFI(new FakeSource(infos), false);
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("TIC", list.ChromatogramIdentity(0).Id);
    }

    private sealed class FakeSource : IUnifiDataSource
    {
        public FakeSource(IReadOnlyList<UnifiChromatogramInfo> infos)
        {
            ChromatogramInfo = infos;
        }

        public RemoteApiType RemoteApi => RemoteApiType.Unifi;
        public bool HasIonMobilityData => false;
        public int NumberOfSpectra => 0;
        public IReadOnlyList<UnifiChromatogramInfo> ChromatogramInfo { get; }
        public Dictionary<int, UnifiChromatogram> ChromatogramFor { get; } = new();

        public int GetMsLevel(int index) => 1;
        public (int ChannelIndex, int ScanIndexInChannel) GetChannelAndScanIndex(int index)
            => throw new NotSupportedException();
        public void GetSpectrum(int index, UnifiSpectrum spectrum, bool getBinaryData, bool doCentroid)
            => throw new NotSupportedException();

        public void GetChromatogram(int index, UnifiChromatogram chromatogram, bool getBinaryData)
        {
            if (!ChromatogramFor.TryGetValue(index, out var src))
                throw new InvalidOperationException($"no fake chromatogram for infoIndex={index}");
            chromatogram.ArrayLength = src.ArrayLength;
            chromatogram.Q1 = src.Q1;
            chromatogram.Q3 = src.Q3;
            chromatogram.Polarity = src.Polarity;
            chromatogram.Type = src.Type;
            chromatogram.Name = src.Name;
            if (getBinaryData)
            {
                chromatogram.TimeArray = src.TimeArray;
                chromatogram.IntensityArray = src.IntensityArray;
            }
        }
    }
}
