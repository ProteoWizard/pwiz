using Pwiz.TestHarness;

namespace Pwiz.Vendor.Mobilion.Tests;

/// <summary>
/// End-to-end harness modeled on pwiz cpp <c>Reader_Mobilion_Test.cpp</c>: reads each
/// <c>.mbi</c> fixture under <c>pwiz/data/vendor_readers/Mobilion/Reader_Mobilion_Test.data</c>
/// through <see cref="Reader_Mobilion"/>, normalizes via
/// <see cref="VendorReaderTestHarness"/>, and diffs against the matching reference mzML.
/// </summary>
/// <remarks>
/// Per the test-consolidation plan, one <c>[TestMethod]</c> per <c>.mbi</c> fixture
/// running every variant cpp exercises against that fixture. cpp test ranges:
/// <list type="bullet">
///   <item>both fixtures get default + <c>indexRange=(0,100)</c></item>
///   <item><c>ExampleTuneMix_binned5</c> additionally runs <c>peakPickingCWT</c> with and
///   without <c>combineIonMobilitySpectra</c></item>
///   <item><c>CCS Calibration_02</c> additionally runs <c>combineIMS</c>, <c>combineIMS +
///   ignoreZeros</c>, and <c>ignoreZeros</c> alone</item>
/// </list>
/// </remarks>
[TestClass]
public class ReaderMobilionTests
{
    private static string? FindTestDataRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "data", "vendor_readers", "Mobilion",
                "Reader_Mobilion_Test.data");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static FixtureRunContext? SetUp(string fixtureFileName)
    {
        string? root = FindTestDataRoot();
        if (root is null) { Assert.Inconclusive("Mobilion test data tree not found."); return null; }
        if (!File.Exists(Path.Combine(root, fixtureFileName)))
        {
            Assert.Inconclusive($"{fixtureFileName} not present under test data.");
            return null;
        }
        return new FixtureRunContext(new Reader_Mobilion(), root, new IsNamedRawFile(fixtureFileName), fixtureFileName);
    }

    [TestMethod]
    public void Reader_Mobilion_ExampleTuneMix_binned5()
    {
        // cpp Reader_Mobilion_Test.cpp:57-76: default + indexRange, then CWT, then CWT+combineIMS.
        // Designated coverage rep — the default-config variant runs the HDF5-backed
        // round-trips so at least one Mobilion fixture exercises them under TC dotCover.
        var ctx = SetUp("ExampleTuneMix_binned5.mbi");
        if (ctx is null) return;

        // Reference mzMLs were generated on a cpp build agent, so checksum + start-timestamp
        // mismatches are baked into the references. The harness already normalizes pwiz software,
        // so leaving these two off via the diff config keeps the comparison meaningful.
        var baseConfig = new ReaderTestConfig
        {
            IgnoreSourceFileChecksum = true,
            IgnoreStartTimeStamp = true,
            IndexRange = (0, 100),
        };

        // 1. default + indexRange(0,100) → ExampleTuneMix_binned5.mzML
        ctx.Run(baseConfig with { RunRoundTripUnderProfiler = true });

        // 2. peakPickingCWT + indexRange(0,100) → ExampleTuneMix_binned5-centroid-cwt.mzML
        ctx.Run(new ReaderTestConfig
        {
            IgnoreSourceFileChecksum = true,
            IgnoreStartTimeStamp = true,
            IndexRange = (0, 100),
            PeakPickingCWT = true,
        });

        // 3. peakPickingCWT + combineIMS (no indexRange — cpp resets it) →
        //    ExampleTuneMix_binned5-combineIMS-centroid-cwt.mzML
        ctx.Run(new ReaderTestConfig
        {
            IgnoreSourceFileChecksum = true,
            IgnoreStartTimeStamp = true,
            PeakPickingCWT = true,
            CombineIonMobilitySpectra = true,
        });

        ctx.Check();
    }

    [TestMethod]
    public void Reader_Mobilion_CcsCalibration02()
    {
        // cpp Reader_Mobilion_Test.cpp lines 57-88: default+indexRange, +combineIMS,
        // +combineIMS+ignoreZeros, +ignoreZeros (combineIMS off again).
        var ctx = SetUp("2024-02-16-16.02.20-CCS Calibration_02.mbi");
        if (ctx is null) return;

        // 1. default + indexRange(0,100) → 2024-02-16-16.02.20-CCS Calibration_02.mzML
        ctx.Run(new ReaderTestConfig
        {
            IgnoreSourceFileChecksum = true,
            IgnoreStartTimeStamp = true,
            IndexRange = (0, 100),
        });

        // 2. combineIMS (no indexRange) → 2024-02-16-16.02.20-CCS Calibration_02-combineIMS.mzML
        ctx.Run(new ReaderTestConfig
        {
            IgnoreSourceFileChecksum = true,
            IgnoreStartTimeStamp = true,
            CombineIonMobilitySpectra = true,
        });

        // 3. combineIMS + ignoreZeros → ...-ignoreZeros-combineIMS.mzML
        ctx.Run(new ReaderTestConfig
        {
            IgnoreSourceFileChecksum = true,
            IgnoreStartTimeStamp = true,
            CombineIonMobilitySpectra = true,
            IgnoreZeroIntensityPoints = true,
        });

        // 4. ignoreZeros (combineIMS off, indexRange(0,100)) → ...-ignoreZeros.mzML
        ctx.Run(new ReaderTestConfig
        {
            IgnoreSourceFileChecksum = true,
            IgnoreStartTimeStamp = true,
            IndexRange = (0, 100),
            IgnoreZeroIntensityPoints = true,
        });

        ctx.Check();
    }

    /// <summary>
    /// Rewrites every Mobilion reference mzML in this class from the current reader output.
    ///
    /// <para>The <c>[TestMethod]</c> attribute below is COMMENTED OUT deliberately. An
    /// undiscovered test cannot run in "Run All Tests", and MSTest has no attribute that
    /// excludes a test from a run-all while leaving it runnable on demand: <c>[Ignore]</c> is
    /// skipped even when selected explicitly, and a runsettings <c>TestCaseFilter</c> removes it
    /// from discovery altogether, so it never appears to click on. Commenting the attribute is
    /// the only mechanism that gives both.</para>
    ///
    /// <para>To regenerate: uncomment the attribute, run this one test, then comment it back.
    /// It ends in <c>Assert.Fail</c>, so a run that completes is never green and the message is
    /// the reminder. That is NOT a CI guard: if this vendor's data is absent, <c>SetUp</c> throws
    /// <c>Assert.Inconclusive</c>, which aborts before the <c>Assert.Fail</c> and reports Skipped -
    /// and <c>dotnet test</c> exits 0 on skips. Afterwards run the suite again WITHOUT it - a
    /// regenerated reference that
    /// does not then compare equal means the generate and compare paths disagree, which is the
    /// one failure this mode can hide.</para>
    /// </summary>
    //[TestMethod]
    public void Regenerate_Mobilion_References()
    {
        VendorReaderTestHarness.GenerateReferences = true;
        try
        {
            Reader_Mobilion_ExampleTuneMix_binned5();
            Reader_Mobilion_CcsCalibration02();
        }
        finally
        {
            VendorReaderTestHarness.GenerateReferences = false;
        }

        Assert.Fail("Reference mzMLs regenerated. Comment out this [TestMethod] and run the normal tests.");
    }
}
