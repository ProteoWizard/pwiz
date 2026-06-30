// Phase 2 smoke test: exercises the Skyline-shaped MsDataFileImpl public surface
// (constructor, vendor detection, spectrum walk, chromatogram fetch) against a
// real mzML fixture. Proves the pwiz-sharp-backed wrapper does what the
// pwiz.CLI-backed one did, without needing the full Skyline csproj cascade.

using pwiz.ProteowizardWrapper;

string fixture = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "example_data", "small.pwiz.1.1.mzML");
fixture = Path.GetFullPath(fixture);

if (!File.Exists(fixture))
{
    Console.Error.WriteLine($"fixture not found: {fixture}");
    return 2;
}

Console.WriteLine($"opening {fixture}");
if (!MsDataFileImpl.IsValidFile(fixture))
{
    Console.Error.WriteLine("IsValidFile: false (refusing to open)");
    return 3;
}

int failed = 0;

void Check(string name, bool cond, string detail = "")
{
    if (cond) Console.WriteLine($"  PASS  {name}{(detail.Length > 0 ? " — " + detail : "")}");
    else { Console.WriteLine($"  FAIL  {name}{(detail.Length > 0 ? " — " + detail : "")}"); failed++; }
}

using (var msd = new MsDataFileImpl(fixture))
{
    Check("RunId surfaces a non-empty string", !string.IsNullOrEmpty(msd.RunId), $"id='{msd.RunId}'");

    Check("FilePath round-trips", msd.FilePath == fixture);
    Check("SampleIndex defaulted to 0", msd.SampleIndex == 0);

    int n = msd.SpectrumCount;
    Check("SpectrumCount > 0", n > 0, $"count={n}");
    Check("GetSpectrumCount matches property", msd.GetSpectrumCount() == n);

    if (n > 0)
    {
        var id0 = msd.GetSpectrumId(0);
        Check("GetSpectrumId(0) non-empty", !string.IsNullOrEmpty(id0), $"id='{id0}'");

        int found = msd.GetSpectrumIndex(id0);
        Check("GetSpectrumIndex round-trips id0", found == 0, $"got {found}");

        int msLevel = msd.GetMsLevel(0);
        Check("GetMsLevel(0) ∈ {1..3}", msLevel >= 1 && msLevel <= 3, $"level={msLevel}");

        double? t0 = msd.GetStartTime(0);
        Check("GetStartTime(0) is non-null", t0.HasValue, $"t={t0}");

        msd.GetSpectrum(0, out var mz, out var inten);
        Check("GetSpectrum mz array populated", mz.Length > 0, $"len={mz.Length}");
        Check("GetSpectrum intensity array same length as mz", mz.Length == inten.Length);

        // Walk full file via GetScanTimes — exercises the metadata-only read path.
        var times = msd.GetScanTimes();
        Check("GetScanTimes returns array of length SpectrumCount", times.Length == n);
        Check("GetScanTimes are monotonic non-decreasing",
            Enumerable.Range(1, times.Length - 1).All(i => times[i] >= times[i - 1]));
    }

    int chromN = msd.ChromatogramCount;
    Console.WriteLine($"  info  ChromatogramCount={chromN}, HasChromatogramData={msd.HasChromatogramData}");
    if (chromN > 0)
    {
        var name = msd.GetChromatogramId(0, out var idxId);
        Check("GetChromatogramId(0) non-empty", !string.IsNullOrEmpty(name), $"name='{name}' idx={idxId}");
        msd.GetChromatogram(0, out var cid, out var ct, out var ci);
        Check("GetChromatogram(0) populated", ct.Length > 0 && ct.Length == ci.Length, $"name='{cid}' len={ct.Length}");
    }

    // Vendor detection — small.pwiz.1.1.mzML originates from a Thermo run, so the
    // Thermo CV term should be picked up via the SourceFile CV walk.
    Console.WriteLine($"  info  IsThermoFile={msd.IsThermoFile}, IsAgilent={msd.IsAgilentFile}, IsWaters={msd.IsWatersFile}, IsShimadzu={msd.IsShimadzuFile}, IsAB={msd.IsABFile}");
    Check("IsProcessedBy('pwiz') matches expected mzML metadata", msd.IsProcessedBy("pwiz"));
}

Console.WriteLine();
Console.WriteLine(failed == 0
    ? "ALL CHECKS PASSED"
    : $"{failed} CHECK(S) FAILED");

return failed == 0 ? 0 : 1;
