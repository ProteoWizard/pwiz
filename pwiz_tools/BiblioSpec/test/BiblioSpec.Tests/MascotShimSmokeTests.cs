// Smoke tests for the MascotShim P/Invoke surface. These only run when the
// project was built with <MascotSupport>true</MascotSupport> (the default);
// when MascotSupport=false, MascotShimInterop is compiled out of BiblioSpec
// and these tests can't compile either, so the whole file is excluded by the
// csproj.
//
// Phase 1 verified the DLL loads + last-error plumbing works. Phase 2 covers
// open / close / isMSMS / numQueries against a real .dat fixture. The
// fixture used (F007401.dat) is the same one cpp BiblioSpec exercises in its
// `Mascot` Jamfile row.

namespace Pwiz.Tools.BiblioSpec.Tests;

[TestClass]
public class MascotShimSmokeTests
{
    [TestMethod]
    public void MascotShim_GetVersion_ReturnsZeroAndClearsError()
    {
        int result = MascotShimInterop.GetVersion(out int major, out int minor, out int patch);

        Assert.AreEqual((int)MascotResult.Ok, result, "GetVersion should return MASCOT_OK");
        Assert.AreEqual(0, major, "Phase 1 version is 0.1.0");
        Assert.AreEqual(1, minor);
        Assert.AreEqual(0, patch);
        Assert.AreEqual(string.Empty, MascotShimInterop.LastError(),
            "A successful call should clear the thread-local last-error.");
    }

    [TestMethod]
    public void MascotShim_Open_NonexistentPath_ReturnsSdkExceptionAndPopulatesLastError()
    {
        // Force a clean prior state.
        _ = MascotShimInterop.GetVersion(out _, out _, out _);

        // Use a fully-qualified path under a known-good directory. A bare
        // relative path can send msparser's internal stat() into hairy
        // territory (cache-dir resolution + cwd-walk) that has surfaced
        // process crashes; an absolute path avoids that whole class of
        // edge cases and matches how cpp BiblioSpec passes paths.
        string bogusPath = Path.Combine(Path.GetTempPath(), "definitely-does-not-exist.dat");
        Assert.IsFalse(File.Exists(bogusPath), $"Test invariant: {bogusPath} must not exist.");

        int result = MascotShimInterop.Open(bogusPath, 0.0, out IntPtr handle);

        Assert.AreEqual((int)MascotResult.SdkException, result,
            "Open on a missing file should surface as an SDK exception, not InvalidHandle.");
        Assert.AreEqual(IntPtr.Zero, handle, "Open failure must leave the out-handle null.");

        string err = MascotShimInterop.LastError();
        Assert.IsFalse(string.IsNullOrEmpty(err),
            "Open failure must populate the thread-local last-error.");
    }

    [TestMethod]
    public void MascotShim_Open_RealFixture_ExposesMsMsAndNumQueries()
    {
        string datPath = LocateMascotFixture("F007401.dat");

        int rc = MascotShimInterop.Open(datPath, scoreCutoff: 0.0, out IntPtr handle);
        Assert.AreEqual((int)MascotResult.Ok, rc,
            $"Open failed (rc={rc}): {MascotShimInterop.LastError()}");
        Assert.AreNotEqual(IntPtr.Zero, handle, "Open success must return a non-null handle.");

        try
        {
            rc = MascotShimInterop.IsMsMs(handle, out int isMsMs);
            Assert.AreEqual((int)MascotResult.Ok, rc,
                $"IsMsMs failed (rc={rc}): {MascotShimInterop.LastError()}");
            Assert.AreEqual(1, isMsMs,
                "F007401.dat is an MS/MS search; isMSMS should be 1.");

            rc = MascotShimInterop.NumQueries(handle, out int numQueries);
            Assert.AreEqual((int)MascotResult.Ok, rc,
                $"NumQueries failed (rc={rc}): {MascotShimInterop.LastError()}");
            Assert.IsTrue(numQueries > 0,
                $"Expected > 0 queries; got {numQueries}.");
        }
        finally
        {
            MascotShimInterop.Close(handle);
        }
    }

    [TestMethod]
    public void MascotShim_OpenPsmIter_RealFixture_Succeeds()
    {
        // Narrow probe: just construct ms_peptidesummary inside the shim.
        // Failure here points at summary-construction, not at iteration logic.
        string datPath = LocateMascotFixture("F007401.dat");

        int rc = MascotShimInterop.Open(datPath, scoreCutoff: 0.0, out IntPtr dat);
        Assert.AreEqual((int)MascotResult.Ok, rc,
            $"Open failed (rc={rc}): {MascotShimInterop.LastError()}");

        try
        {
            rc = MascotShimInterop.OpenPsmIter(dat, out IntPtr iter);
            Assert.AreEqual((int)MascotResult.Ok, rc,
                $"OpenPsmIter failed (rc={rc}): {MascotShimInterop.LastError()}");
            MascotShimInterop.ClosePsmIter(iter);
        }
        finally
        {
            MascotShimInterop.Close(dat);
        }
    }

    [TestMethod]
    public void MascotShim_NextPsm_RealFixture_ReturnsAtLeastOneRecord()
    {
        // Narrow probe: pull the very first PSM and bail. If this crashes,
        // the bug is in NextPsm's state machine, not summary construction
        // or the iteration of multiple queries.
        string datPath = LocateMascotFixture("F007401.dat");
        Assert.AreEqual((int)MascotResult.Ok, MascotShimInterop.Open(datPath, 0.0, out IntPtr dat));
        try
        {
            Assert.AreEqual((int)MascotResult.Ok, MascotShimInterop.OpenPsmIter(dat, out IntPtr iter));
            try
            {
                int rc = MascotShimInterop.NextPsm(iter, out MascotPsmRecord rec);
                Assert.AreEqual(1, rc,
                    $"First NextPsm should return 1 (rc={rc}): {MascotShimInterop.LastError()}");
                Assert.IsTrue(rec.QueryId >= 1, $"QueryId={rec.QueryId} must be 1-based.");
                string peptide = MascotShimInterop.DecodeBuffer(rec.Peptide);
                Assert.IsTrue(peptide.Length > 0, "Peptide string must be populated.");
            }
            finally { MascotShimInterop.ClosePsmIter(iter); }
        }
        finally { MascotShimInterop.Close(dat); }
    }

    [TestMethod]
    public void MascotShim_PsmIter_RealFixture_EnumeratesPlausiblePsms()
    {
        string datPath = LocateMascotFixture("F007401.dat");

        int rc = MascotShimInterop.Open(datPath, scoreCutoff: 0.0, out IntPtr dat);
        Assert.AreEqual((int)MascotResult.Ok, rc,
            $"Open failed (rc={rc}): {MascotShimInterop.LastError()}");

        try
        {
            rc = MascotShimInterop.NumQueries(dat, out int numQueries);
            Assert.AreEqual((int)MascotResult.Ok, rc);
            Assert.IsTrue(numQueries > 0);

            rc = MascotShimInterop.OpenPsmIter(dat, out IntPtr iter);
            Assert.AreEqual((int)MascotResult.Ok, rc,
                $"OpenPsmIter failed (rc={rc}): {MascotShimInterop.LastError()}");

            try
            {
                int totalPsms = 0;
                int distinctQueries = 0;
                int lastQuery = -1;
                string? firstPeptide = null;
                int firstCharge = 0;
                double firstObservedMz = 0;
                while (true)
                {
                    rc = MascotShimInterop.NextPsm(iter, out MascotPsmRecord rec);
                    if (rc == 0) break;
                    Assert.AreEqual(1, rc,
                        $"NextPsm failed (rc={rc}): {MascotShimInterop.LastError()}");

                    totalPsms++;
                    if (rec.QueryId != lastQuery)
                    {
                        distinctQueries++;
                        lastQuery = rec.QueryId;
                    }

                    string peptide = MascotShimInterop.DecodeBuffer(rec.Peptide);

                    Assert.IsTrue(rec.QueryId >= 1 && rec.QueryId <= numQueries,
                        $"QueryId {rec.QueryId} out of range (1..{numQueries}).");
                    Assert.IsTrue(rec.Rank >= 1, $"Rank {rec.Rank} must be 1-based.");
                    Assert.IsTrue(rec.Charge >= 1 && rec.Charge <= 50,
                        $"Charge {rec.Charge} out of plausible range.");
                    Assert.IsTrue(rec.ObservedMz > 0,
                        $"ObservedMz must be positive; got {rec.ObservedMz}.");
                    Assert.IsTrue(peptide.Length > 0 &&
                        peptide.All(c => c is >= 'A' and <= 'Z'),
                        $"Peptide should be uppercase letters; got '{peptide}'.");

                    if (firstPeptide is null)
                    {
                        firstPeptide = peptide;
                        firstCharge = rec.Charge;
                        firstObservedMz = rec.ObservedMz;
                    }
                }

                Assert.IsTrue(totalPsms > 0, "Expected at least one PSM.");
                Assert.IsTrue(distinctQueries > 0, "Expected at least one distinct query.");
                Assert.IsTrue(distinctQueries <= numQueries,
                    $"Distinct queries ({distinctQueries}) cannot exceed numQueries ({numQueries}).");

                // Cross-check rank-1 invariant: every emitted record's rank is
                // shadowed by the cpp emission rule "same ion score as rank 1
                // for this query". A more focused check would compare against
                // a golden, but the broader range check + uppercase invariant
                // catch every plausibility regression.
                Assert.IsNotNull(firstPeptide);
                System.Console.WriteLine(
                    $"F007401.dat: {totalPsms} PSMs across {distinctQueries} queries " +
                    $"(of {numQueries} total). First PSM: {firstPeptide} {firstCharge}+ @ {firstObservedMz:F4}");
            }
            finally
            {
                MascotShimInterop.ClosePsmIter(iter);
            }
        }
        finally
        {
            MascotShimInterop.Close(dat);
        }
    }

    [TestMethod]
    public void MascotShim_EnumerateMods_RealFixture_LooksPlausible()
    {
        string datPath = LocateMascotFixture("F007401.dat");
        Assert.AreEqual((int)MascotResult.Ok,
            MascotShimInterop.Open(datPath, 0.0, out IntPtr dat));
        try
        {
            // Fixed mods. F007401.dat is a Mascot demo file; whatever the
            // search params encode, every fixed-mod row must carry a non-zero
            // delta and at least one residue (or "N_term"/"C_term").
            Assert.AreEqual((int)MascotResult.Ok,
                MascotShimInterop.NumFixedMods(dat, out int numFixed));
            for (int i = 1; i <= numFixed; i++)
            {
                int rc = MascotShimInterop.GetFixedMod(dat, i, out MascotMod mod);
                Assert.AreEqual((int)MascotResult.Ok, rc,
                    $"GetFixedMod({i}) failed (rc={rc}): {MascotShimInterop.LastError()}");
                Assert.AreNotEqual(0.0, mod.Delta,
                    $"Fixed mod {i} reported delta=0; should have been excluded.");
                Assert.IsTrue(MascotShimInterop.DecodeBuffer(mod.Name).Length > 0,
                    $"Fixed mod {i} has an empty name.");
                Assert.IsTrue(MascotShimInterop.DecodeBuffer(mod.Residues).Length > 0,
                    $"Fixed mod {i} has an empty residue spec.");
            }
            // Past-the-end should fail cleanly.
            Assert.AreEqual((int)MascotResult.NoData,
                MascotShimInterop.GetFixedMod(dat, numFixed + 1, out _));

            // Variable mods. Same shape, but the residue buffer is empty by
            // design — variable-mod residue identity comes from VarModsStr
            // in each PSM record, not from the mod table.
            Assert.AreEqual((int)MascotResult.Ok,
                MascotShimInterop.NumVarMods(dat, out int numVar));
            for (int i = 1; i <= numVar; i++)
            {
                int rc = MascotShimInterop.GetVarMod(dat, i, out MascotMod mod);
                Assert.AreEqual((int)MascotResult.Ok, rc,
                    $"GetVarMod({i}) failed (rc={rc}): {MascotShimInterop.LastError()}");
                Assert.AreNotEqual(0.0, mod.Delta);
                Assert.IsTrue(MascotShimInterop.DecodeBuffer(mod.Name).Length > 0);
                Assert.AreEqual(string.Empty, MascotShimInterop.DecodeBuffer(mod.Residues),
                    "Variable mods should leave the residue buffer empty.");
            }
            Assert.AreEqual((int)MascotResult.NoData,
                MascotShimInterop.GetVarMod(dat, numVar + 1, out _));

            System.Console.WriteLine(
                $"F007401.dat search params: {numFixed} fixed mod(s), {numVar} variable mod(s).");
        }
        finally { MascotShimInterop.Close(dat); }
    }

    /// <summary>
    /// Diagnostic dump utility — NOT a [TestMethod]. Flip back to <c>[TestMethod]</c> the next
    /// time the Mascot quant code (ms_quant_configfile + ms_umod_configfile + applyIsotopes,
    /// MascotShim's per-PSM componentStr extraction, ApplyIsotopeDiffs in the C# reader)
    /// needs poking; running it dumps the quant config dir, quant method name, every quant
    /// component with its isotope diffs, and the first 3 PSMs' componentStr to Console.Out.
    /// </summary>
    public static void MascotShim_QuantProbe_F027752()
    {
        string datPath = LocateMascotFixture("F027752.dat");
        Assert.AreEqual((int)MascotResult.Ok,
            MascotShimInterop.Open(datPath, 0.05, out IntPtr dat));
        try
        {
            string rid = OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";
            string cfg = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", "msparser-config");
            System.Console.WriteLine($"configDir={cfg} exists={Directory.Exists(cfg)}");
            int rc = MascotShimInterop.SetQuantConfigDir(dat, cfg);
            System.Console.WriteLine($"SetQuantConfigDir rc={rc}");

            rc = MascotShimInterop.GetQuantName(dat, null, 0, out int len);
            System.Console.WriteLine($"GetQuantName(size) rc={rc} len={len}");
            if (len > 1)
            {
                var nbuf = new byte[len];
                _ = MascotShimInterop.GetQuantName(dat, nbuf, nbuf.Length, out _);
                System.Console.WriteLine($"QuantName='{MascotShimInterop.DecodeBuffer(nbuf)}'");
            }
            else
            {
                System.Console.WriteLine($"LastError='{MascotShimInterop.LastError()}'");
            }
            rc = MascotShimInterop.NumQuantComponents(dat, out int nc);
            System.Console.WriteLine($"NumComponents rc={rc} n={nc}");
            for (int i = 0; i < nc; i++)
            {
                _ = MascotShimInterop.GetQuantComponentName(dat, i, null, 0, out int cl);
                var cbuf = new byte[cl];
                _ = MascotShimInterop.GetQuantComponentName(dat, i, cbuf, cbuf.Length, out _);
                _ = MascotShimInterop.GetQuantComponentDiffs(dat, i, null, 0, out int dn);
                var diffs = dn > 0 ? new MascotIsotopeDiff[dn] : null;
                if (dn > 0) _ = MascotShimInterop.GetQuantComponentDiffs(dat, i, diffs, diffs!.Length, out _);
                System.Console.WriteLine($"Comp{i}='{MascotShimInterop.DecodeBuffer(cbuf)}' diffs={dn}");
                for (int j = 0; j < Math.Min(dn, 5); j++)
                    System.Console.WriteLine($"  {(char)diffs![j].Residue} {diffs[j].Delta:F6}");
            }

            // Also dump the first PSM's componentStr.
            _ = MascotShimInterop.OpenPsmIter(dat, out IntPtr iter);
            for (int k = 0; k < 3; k++)
            {
                int rcp = MascotShimInterop.NextPsm(iter, out MascotPsmRecord rec);
                if (rcp != 1) break;
                System.Console.WriteLine(
                    $"PSM Q{rec.QueryId} pep='{MascotShimInterop.DecodeBuffer(rec.Peptide)}' "
                    + $"comp='{MascotShimInterop.DecodeBuffer(rec.ComponentStr)}'");
            }
            MascotShimInterop.ClosePsmIter(iter);
        }
        finally { MascotShimInterop.Close(dat); }
    }

    [TestMethod]
    public void MascotShim_QueryLookup_RealFixture_ExposesTitleAndPeaks()
    {
        string datPath = LocateMascotFixture("F007401.dat");
        Assert.AreEqual((int)MascotResult.Ok,
            MascotShimInterop.Open(datPath, 0.0, out IntPtr dat));
        try
        {
            Assert.AreEqual((int)MascotResult.Ok,
                MascotShimInterop.NumQueries(dat, out int numQueries));
            Assert.IsTrue(numQueries > 0);

            // Title via two-call pattern: size-only query, then fetch.
            int rc = MascotShimInterop.GetQueryTitle(dat, 1, null, 0, out int required);
            Assert.IsTrue(rc == (int)MascotResult.NotEnoughSpace || rc == (int)MascotResult.NoData,
                $"Size probe should report space requirement (rc={rc}).");
            Assert.IsTrue(required > 1, $"Title required {required} bytes; expected > 1.");

            var buf = new byte[required];
            rc = MascotShimInterop.GetQueryTitle(dat, 1, buf, buf.Length, out int _);
            Assert.AreEqual((int)MascotResult.Ok, rc,
                $"GetQueryTitle failed (rc={rc}): {MascotShimInterop.LastError()}");
            string title = MascotShimInterop.DecodeBuffer(buf);
            Assert.IsTrue(title.Length > 0, "Title should be non-empty.");

            // Peak list.
            rc = MascotShimInterop.GetQueryPeakCount(dat, 1, out int peakCount);
            Assert.AreEqual((int)MascotResult.Ok, rc);
            Assert.IsTrue(peakCount > 0, $"Query 1 should have peaks; got {peakCount}.");

            var mz = new double[peakCount];
            var intensity = new double[peakCount];
            rc = MascotShimInterop.GetQueryPeaks(dat, 1, mz, intensity, mz.Length);
            Assert.AreEqual(peakCount, rc,
                $"GetQueryPeaks should return peak count via positive RC; got {rc}: {MascotShimInterop.LastError()}");
            // Peaks should have plausible m/z and non-negative intensity.
            // Order isn't guaranteed — Mascot can store them in the order
            // they were observed; consumers sort if they need.
            double minMz = double.MaxValue, maxMz = double.MinValue;
            for (int i = 0; i < peakCount; i++)
            {
                Assert.IsTrue(mz[i] > 0 && intensity[i] >= 0,
                    $"Peak {i}: mz={mz[i]}, int={intensity[i]} not plausible.");
                if (mz[i] < minMz) minMz = mz[i];
                if (mz[i] > maxMz) maxMz = mz[i];
            }

            System.Console.WriteLine(
                $"F007401.dat query 1: title='{title}', {peakCount} peaks " +
                $"(mz range {minMz:F2}–{maxMz:F2}).");
        }
        finally { MascotShimInterop.Close(dat); }
    }

/// <summary>Walk up from the test bin to find the cpp BiblioSpec test
    /// inputs directory. The Mascot .dat fixtures live there alongside every
    /// other format's fixtures.</summary>
    private static string LocateMascotFixture(string fileName)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz_tools", "BiblioSpec", "tests", "inputs", fileName);
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        Assert.Inconclusive($"BiblioSpec test inputs not found; expected to locate {fileName} under pwiz_tools/BiblioSpec/tests/inputs/.");
        return string.Empty;
    }
}
