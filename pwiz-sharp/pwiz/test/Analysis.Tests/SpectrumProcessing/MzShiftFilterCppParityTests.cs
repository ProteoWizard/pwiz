using Pwiz.Analysis;
using Pwiz.Data.Common;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

/// <summary>
/// Diagnostic port of cpp's <c>SpectrumList_PeakFilterTest.cpp::testMzShiftFilter()</c>. cpp does not
/// name the params it shifts: it walks each container and shifts EVERY CV param whose units are
/// <see cref="CVID.MS_m_z"/>, then asserts exactly that invariant over the tiny example. This test
/// reproduces the invariant so any param the port forgets - or shifts when cpp does not - shows up.
/// </summary>
[TestClass]
public class MzShiftFilterCppParityTests
{
    [TestMethod]
    public void MzShift_ShiftsEveryMzParam_LikeCpp()
    {
        var shift = new MZTolerance(10, MZToleranceUnits.Ppm);
        var original = new MSData();
        Examples.InitializeTiny(original);
        var filteredSource = new MSData();
        Examples.InitializeTiny(filteredSource);

        var inner = filteredSource.Run.SpectrumList!;
        var filtered = new SpectrumListMzShift(inner, shift, new IntegerSet(1, 2));
        var originalList = original.Run.SpectrumList!;

        var problems = new List<string>();
        for (int i = 0; i < filtered.Count; i++)
        {
            var o = originalList.GetSpectrum(i, getBinaryData: true);
            var f = filtered.GetSpectrum(i, getBinaryData: true);

            Compare(problems, $"spectrum[{i}] params", o.Params, f.Params, shift);
            for (int j = 0; j < o.ScanList.Scans.Count; j++)
                Compare(problems, $"spectrum[{i}] scan[{j}]", o.ScanList.Scans[j], f.ScanList.Scans[j], shift);
            for (int j = 0; j < o.Precursors.Count; j++)
            {
                Compare(problems, $"spectrum[{i}] precursor[{j}]", o.Precursors[j], f.Precursors[j], shift);
                Compare(problems, $"spectrum[{i}] precursor[{j}].activation",
                    o.Precursors[j].Activation, f.Precursors[j].Activation, shift);
                Compare(problems, $"spectrum[{i}] precursor[{j}].isolationWindow",
                    o.Precursors[j].IsolationWindow, f.Precursors[j].IsolationWindow, shift);
                for (int k = 0; k < o.Precursors[j].SelectedIons.Count; k++)
                    Compare(problems, $"spectrum[{i}] precursor[{j}].selectedIon[{k}]",
                        o.Precursors[j].SelectedIons[k], f.Precursors[j].SelectedIons[k], shift);
            }
        }

        Assert.AreEqual(0, problems.Count,
            "cpp shifts every m/z-united CV param; these differ:" +
            Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    /// <summary>The isolation window's lower/upper offsets carry m/z units but are widths, so the
    /// port leaves them alone where cpp's generic loop shifts them. Pinned here so the deviation
    /// stays a decision rather than drifting into an accident.</summary>
    private static readonly CVID[] DeliberatelyNotShifted =
    {
        CVID.MS_isolation_window_lower_offset,
        CVID.MS_isolation_window_upper_offset,
    };

    private static void Compare(List<string> problems, string where,
        ParamContainer original, ParamContainer filtered, MZTolerance shift)
    {
        foreach (var p in original.CVParams)
        {
            if (p.Units != CVID.MS_m_z)
                continue;
            if (DeliberatelyNotShifted.Contains(p.Cvid))
            {
                double unchanged = filtered.CvParam(p.Cvid).ValueAs<double>();
                if (System.Math.Abs(unchanged - p.ValueAs<double>()) > 1e-12)
                    problems.Add($"  {where} {p.Cvid}: expected to stay {p.ValueAs<double>()}, got {unchanged}");
                continue;
            }
            double before = p.ValueAs<double>();
            double expected = before + shift;
            double actual = filtered.CvParam(p.Cvid).ValueAs<double>();
            if (System.Math.Abs(expected - actual) > 1e-9)
                problems.Add($"  {where} {p.Cvid}: {before} -> expected {expected}, got {actual}");
        }
    }
}
