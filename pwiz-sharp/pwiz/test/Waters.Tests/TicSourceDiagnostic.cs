using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.TestHarness;

namespace Pwiz.Vendor.Waters.Tests;

/// <summary>
/// TEMPORARY DIAGNOSTIC — delete once the Waters TIC discrepancy is resolved.
///
/// Reader_Waters_ATEHLSTLSEK_LM_684_3469 / _LM_785_8426 fail on the CI agents with
///   spectrum[5]: MS_TIC  a=1.63409e05  b=1.63408e05
/// but PASS on a developer machine at the same commit with the same fixtures. The formatter
/// has been ruled out (an Agilent tie proves cpp rounds away-from-zero, which PwizFloat
/// already does), so the difference must be WHICH value the reader hands to the formatter.
///
/// SpectrumList_Waters.BuildSpectrum has three mutually exclusive sources for MS_TIC, and
/// each leaves a distinct fingerprint in the emitted spectrum:
///
///   A. SDK scan stats            (!willCentroid &amp;&amp; (Block &lt; 0 || Combined))
///      -> MS_TIC from GetScanItem(TotalIonCurrent), parsed as double, NO unit
///      -> ALSO sets MS_base_peak_m_z + MS_base_peak_intensity, DefaultArrayLength = PeaksInScan
///   B. Per-block IMS TIC         (!willCentroid &amp;&amp; Block &gt;= 0 &amp;&amp; !Combined)
///      -> MS_TIC from TicByFunctionIndex[f][block], a FLOAT (6 sig figs), NO unit
///      -> NO base peak params, DefaultArrayLength = 0
///   C. Recomputed from peaks     (willCentroid)
///      -> MS_TIC WITH unit MS_number_of_detector_counts, plus lowest/highest observed m/z
///
/// 1.63408e05 is what the double overload emits for the SDK's literal "163408" (source A);
/// 1.63409e05 is what the float overload emits for 163408.5f (source B). So this test prints
/// the fingerprint: if CI reports "no base peak / arrayLength=0" it is taking source B while
/// the dev machine takes source A, and the question becomes why the index entry is classified
/// as non-combined IMS there.
///
/// Always passes — it only reports. Assert.Inconclusive is deliberately NOT used so the
/// output shows up on a green run too.
/// </summary>
[TestClass]
public class TicSourceDiagnostic
{
    private static string? FindTestDataRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "data", "vendor_readers", "Waters",
                "Reader_Waters_Test.data");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    [TestMethod]
    public void Diagnostic_ReportTicSourceForLockmassFixtures()
    {
        string? root = FindTestDataRoot();
        if (root is null)
        {
            Console.WriteLine("[TIC-DIAG] Waters test data tree not found; nothing to report.");
            return;
        }

        foreach (string fixture in new[] { "ATEHLSTLSEK_LM_684.3469.raw", "ATEHLSTLSEK_LM_785.8426.raw" })
        {
            string path = Path.Combine(root, fixture);
            Console.WriteLine($"[TIC-DIAG] ===== {fixture} =====");
            if (!Directory.Exists(path))
            {
                Console.WriteLine("[TIC-DIAG]   fixture not present");
                continue;
            }

            // Report any persisted Waters lockmass state: lmgt.inf is written INTO the .raw and
            // survives across processes, so a fixture that has been read before can differ from
            // a freshly extracted one.
            foreach (string f in Directory.GetFiles(path))
            {
                string name = Path.GetFileName(f);
                if (name.StartsWith("lmgt", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("_extern.inf", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[TIC-DIAG]   state file: {name} ({new FileInfo(f).Length} bytes)");
                }
            }

            try
            {
                var msd = new MSData();
                new Reader_Waters().Read(path, msd);
                var sl = msd.Run.SpectrumList;
                if (sl is null || sl.Count <= 5)
                {
                    Console.WriteLine($"[TIC-DIAG]   spectrumList count={(sl?.Count ?? 0)} — no spectrum[5]");
                    continue;
                }

                var spec = sl.GetSpectrum(5, getBinaryData: false);
                var tic = spec.Params.CvParam(CVID.MS_total_ion_current);
                bool hasBasePeakMz = spec.HasCVParam(CVID.MS_base_peak_m_z);
                bool hasDetectorCountsUnit = tic.Units == CVID.MS_number_of_detector_counts;

                string source = hasDetectorCountsUnit ? "C (recomputed from peaks, willCentroid)"
                    : hasBasePeakMz ? "A (SDK scan stats, double)"
                    : "B (per-block IMS TIC, FLOAT)";

                Console.WriteLine($"[TIC-DIAG]   id                 = {spec.Id}");
                Console.WriteLine($"[TIC-DIAG]   MS_TIC             = {tic.Value}  units={tic.Units}");
                Console.WriteLine($"[TIC-DIAG]   has base_peak_m_z  = {hasBasePeakMz}");
                Console.WriteLine($"[TIC-DIAG]   defaultArrayLength = {spec.DefaultArrayLength.ToString(CultureInfo.InvariantCulture)}");
                Console.WriteLine($"[TIC-DIAG]   => TIC SOURCE      = {source}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TIC-DIAG]   read failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
