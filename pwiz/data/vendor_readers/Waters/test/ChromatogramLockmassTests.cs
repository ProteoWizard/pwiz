using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Vendor.Waters.Tests;

/// <summary>
/// Verifies <see cref="Pwiz.Analysis.ChromatogramListLockmassRefiner"/> wired around a real
/// <see cref="ChromatogramList_Waters"/>. The refiner is an API-parity port of cpp's
/// <c>ChromatogramList_LockmassRefiner</c> — the lockmass parameters are threaded through
/// <see cref="ChromatogramList_Waters.GetChromatogramWithLockmass"/> but the chromatogram
/// payload is unchanged (Waters' MassLynx <c>ChromatogramReader</c> doesn't expose a
/// lockmass-aware read; cpp's overload has the same parameters-accepted-but-unused shape).
/// What we verify here is the metadata trail: DataProcessing gains the m/z-calibration term
/// when the inner is Waters, no warning is emitted, and the binary arrays survive the wrap.
/// </summary>
[TestClass]
public class ChromatogramLockmassTests
{
    private static string FixturePath(string fixture)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string c = Path.Combine(dir, "pwiz", "data", "vendor_readers", "Waters",
                "Reader_Waters_Test.data", fixture);
            if (Directory.Exists(c)) return c;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("fixture not found: " + fixture);
    }

    private static ChromatogramList_Waters? OpenChroms(string fixture)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string c = Path.Combine(dir, "pwiz", "data", "vendor_readers", "Waters",
                "Reader_Waters_Test.data", fixture);
            if (Directory.Exists(c))
            {
                var reader = new Reader_Waters();
                var msd = new MSData();
                reader.Read(c, msd, new ReaderConfig());
                return msd.Run.ChromatogramList as ChromatogramList_Waters;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    [TestMethod]
    public void Waters_LockmassRefiner_StampsDataProcessing_AndPreservesChromatograms()
    {
        var inner = OpenChroms("ATEHLSTLSEK_profile.raw");
        if (inner is null) { Assert.Inconclusive("Waters test fixture not found locally."); return; }

        using var err = new System.IO.StringWriter();
        var savedErr = Console.Error;
        Console.SetError(err);
        try
        {
            // 785.8426 is the standard Waters Glu-Fibrinopeptide lockmass; 0.5 Da is the cpp default.
            var wrapped = new ChromatogramListLockmassRefiner(inner, 785.8426, 785.8426, 0.5);

            // No warning on Waters input (cpp warns only when inner is NOT Waters).
            Assert.AreEqual(0, err.ToString().Length,
                "no warning expected on Waters input, got: " + err.ToString());

            // Metadata: the wrapper's DataProcessing has the m/z calibration CV term + the
            // "Waters lockmass correction" UserParam — verifies the constructor took the
            // Waters branch.
            Assert.IsNotNull(wrapped.DataProcessing);
            bool hasMzCalibration = wrapped.DataProcessing!.ProcessingMethods.Any(pm =>
                pm.HasCVParam(CVID.MS_m_z_calibration));
            Assert.IsTrue(hasMzCalibration,
                "expected MS_m_z_calibration in the lockmass refiner's DataProcessing chain");
            bool hasUserParam = wrapped.DataProcessing.ProcessingMethods
                .SelectMany(pm => pm.UserParams)
                .Any(u => u.Name == "Waters lockmass correction");
            Assert.IsTrue(hasUserParam, "expected 'Waters lockmass correction' UserParam");

            // Count is preserved (wrapper doesn't filter).
            Assert.AreEqual(inner.Count, wrapped.Count);

            // First chromatogram (TIC) round-trips: id, type CV, and binary arrays survive
            // the wrap. Matches cpp's pass-through behavior on the binary side.
            var tic = wrapped.GetChromatogram(0, getBinaryData: true);
            Assert.AreEqual("TIC", tic.Id);
            Assert.IsTrue(tic.Params.HasCVParam(CVID.MS_TIC_chromatogram));
            Assert.IsNotNull(tic.GetTimeArray());
            Assert.IsNotNull(tic.GetIntensityArray());
            Assert.IsTrue(tic.GetIntensityArray()!.Data.Count > 0, "TIC should have non-empty intensity");
            // The returned chromatogram now carries the refiner's DataProcessing rather than
            // the inner's — that's the visible byproduct of the refinement step.
            Assert.AreSame(wrapped.DataProcessing, tic.DataProcessing);
        }
        finally { Console.SetError(savedErr); }
    }
}
