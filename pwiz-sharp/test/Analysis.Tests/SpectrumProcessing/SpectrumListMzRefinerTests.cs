using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

[TestClass]
public class SpectrumListMzRefinerTests
{
    [TestMethod]
    public void RefinesMzAgainstMzGfPlusIdentifications()
    {
        // Loads the cpp fixture (JD_06232014_sample4_C.mzML + matching .mzid from MS-GF+),
        // refines via specEValue ≤ 1e-10, and asserts a few base-peak / m/z values shift to
        // within 0.01 Da of the cpp gold-standard refined values. Cpp's test uses ε=1e-2.
        string mzmlPath = FindFixture("JD_06232014_sample4_C.mzML");
        string mzidPath = FindFixture("JD_06232014_sample4_C.mzid");

        MSData msd;
        using (var fs = File.OpenRead(mzmlPath))
            msd = new MzmlReader().Read(fs);
        Assert.AreEqual(610, msd.Run.SpectrumList!.Count);

        // Sanity-check originals so a future MzmlReader regression doesn't masquerade as an
        // mzRefiner regression.
        AssertBasePeak(msd.Run.SpectrumList.GetSpectrum(0, true), 371.09958, 1e-4);
        AssertBasePeak(msd.Run.SpectrumList.GetSpectrum(10, true), 530.32782, 1e-4);

        var refiner = new SpectrumList_MZRefiner(msd, mzidPath, cvTerm: "specEValue",
            rangeSet: "-1e-10", msLevelsToRefine: new IntegerSet(1, 2));
        Assert.AreEqual(610, refiner.Count);
        Assert.AreNotEqual(0, refiner.ShiftErrorPpm,
            "expected a non-zero ppm shift from MS-GF+ identifications");

        const double epsilon = 1e-2;
        AssertBasePeak(refiner.GetSpectrum(0, true), 371.10060, epsilon);
        AssertBasePeak(refiner.GetSpectrum(224, true), 558.30841, epsilon);
        AssertBasePeak(refiner.GetSpectrum(10, true), 530.32928, epsilon);
        AssertPrecursor(refiner.GetSpectrum(10, true), 530.26830, epsilon);
        AssertPrecursor(refiner.GetSpectrum(173, true), 629.30333, epsilon);
        AssertPrecursor(refiner.GetSpectrum(346, true), 840.45738, epsilon);
        AssertPrecursor(refiner.GetSpectrum(470, true), 838.96963, epsilon);
        AssertPrecursor(refiner.GetSpectrum(551, true), 739.70141, epsilon);
    }

    [TestMethod]
    public void ParseDoubleRange_HandlesAllSyntaxes()
    {
        var (min, max) = CallParse("-1e-10");
        Assert.AreEqual(double.MinValue, min);
        Assert.AreEqual(1e-10, max, 1e-15);

        (min, max) = CallParse("5-");
        Assert.AreEqual(5.0, min);
        Assert.AreEqual(double.MaxValue, max);

        (min, max) = CallParse("1-5");
        Assert.AreEqual(1.0, min);
        Assert.AreEqual(5.0, max);

        (min, max) = CallParse("[1,5]");
        Assert.AreEqual(1.0, min);
        Assert.AreEqual(5.0, max);

        (min, max) = CallParse("[,5]");
        Assert.AreEqual(double.MinValue, min);
        Assert.AreEqual(5.0, max);

        (min, max) = CallParse("[1,]");
        Assert.AreEqual(1.0, min);
        Assert.AreEqual(double.MaxValue, max);

        // Invoke via reflection — internal API.
        static (double, double) CallParse(string s)
        {
            var type = typeof(SpectrumList_MZRefiner).Assembly
                .GetType("Pwiz.Analysis.CVConditionalFilter")!;
            var method = type.GetMethod("ParseDoubleRange",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            var result = method.Invoke(null, new object[] { s })!;
            var minProp = result.GetType().GetField("Item1")!;
            var maxProp = result.GetType().GetField("Item2")!;
            return ((double)minProp.GetValue(result)!, (double)maxProp.GetValue(result)!);
        }
    }

    private static void AssertBasePeak(Spectrum s, double expectedMz, double epsilon)
    {
        double actual = s.Params.CvParam(CVID.MS_base_peak_m_z).ValueAs<double>();
        Assert.AreEqual(expectedMz, actual, epsilon,
            $"index {s.Index}: base peak m/z {actual} differs from expected {expectedMz}");
    }

    private static void AssertPrecursor(Spectrum s, double expectedMz, double epsilon)
    {
        double actual = s.Precursors[0].SelectedIons[0].CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>();
        Assert.AreEqual(expectedMz, actual, epsilon,
            $"index {s.Index}: precursor m/z {actual} differs from expected {expectedMz}");
    }

    private static string FindFixture(string name)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string c = Path.Combine(dir, "pwiz", "analysis", "spectrum_processing",
                "SpectrumList_MZRefinerTest.data", name);
            if (File.Exists(c)) return c;
            dir = Path.GetDirectoryName(dir);
        }
        Assert.Inconclusive($"test fixture not found: {name}");
        throw new InvalidOperationException("unreachable");
    }
}
