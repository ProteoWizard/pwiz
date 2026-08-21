using System;
using System.IO;
using Pwiz.Data.MsData.Diff;
using Pwiz.Data.MsData.Readers;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Asserts that the mzML-complete binary formats (mzMLb, mzPeak) round-trip the *full* document
/// metadata — not just peak data. Reads the canonical <c>tiny.pwiz.1.1.mzML</c> fixture (rich in
/// instrument configs, component chains, scans, precursors, dataProcessing, param groups, and
/// chromatograms), writes it through each format's writer, reads it back, and requires
/// <see cref="MSDataDiff.DescribeRoundTrip"/> to be clean (modulo format-inherent annotations the
/// helper tolerates). This runs without any vendor SDK, so it guards metadata fidelity locally —
/// the vendor harness applies the same diff to real vendor documents.
/// </summary>
[TestClass]
public class FullMetadataRoundTripTests
{
    private static string FixturePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "example_data", filename);

    private static MSData ReadTiny()
    {
        var msd = new MSData();
        new MzmlReaderAdapter().Read(FixturePath("tiny.pwiz.1.1.mzML"), msd);
        return msd;
    }

    [TestMethod]
    public void MzMlb_FullMetadata_RoundTrips()
    {
        var orig = ReadTiny();
        string tmp = Path.Combine(Path.GetTempPath(), $"fullmeta-mzmlb-{Guid.NewGuid():N}.mzMLb");
        try
        {
            new Pwiz.Data.MsData.MzMlb.MzMlbWriter().Write(orig, tmp);
            var rt = new MSData();
            new MzMlbReaderAdapter().Read(tmp, rt);

            string report = MSDataDiff.DescribeRoundTrip(orig, rt, precision: 1.0);
            Assert.AreEqual(string.Empty, report, "mzMLb full-metadata round-trip diff:\n" + report);
        }
        finally { try { File.Delete(tmp); } catch { /* best-effort */ } }
    }

    [TestMethod]
    public void MzPeak_FullMetadata_RoundTrips()
    {
        var orig = ReadTiny();
        string tmp = Path.Combine(Path.GetTempPath(), $"fullmeta-mzpeak-{Guid.NewGuid():N}.mzpeak");
        try
        {
            Pwiz.Data.MsData.MzPeak.WriterMzPeak.Write(orig, tmp);
            var rt = new MSData();
            new MzPeakReaderAdapter().Read(tmp, rt);

            string report = MSDataDiff.DescribeRoundTrip(orig, rt, precision: 1.0);
            Assert.AreEqual(string.Empty, report, "mzPeak full-metadata round-trip diff:\n" + report);
        }
        finally { try { File.Delete(tmp); } catch { /* best-effort */ } }
    }
}
