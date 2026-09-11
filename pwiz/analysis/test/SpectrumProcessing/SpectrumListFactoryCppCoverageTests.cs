using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

/// <summary>
/// Every filter name cpp's <c>SpectrumListFactory</c> registers, driven through the port's
/// <see cref="SpectrumListFactory.Wrap(ISpectrumList, string)"/> with the argument syntax cpp
/// documents in its own <c>usage_*</c> strings.
/// </summary>
/// <remarks>
/// This is a parsing test, not a behaviour test: the per-filter behaviour lives in the dedicated
/// suites. What it defends is the layer between the command line and those filters - a name that
/// stops being registered, or an argument form that stops parsing, is otherwise invisible until a
/// user hits it, because nothing else in the suite goes through the filter STRING.
/// </remarks>
[TestClass]
public class SpectrumListFactoryCppCoverageTests
{
    /// <summary>Name plus a representative argument string, both taken from cpp's usage text.</summary>
    private static readonly (string Name, string Args)[] CppFilters =
    {
        ("activation", "CID"),
        ("analyzer", "FT"),
        ("analyzerType", "FT"),
        ("chargeState", "2-3"),
        ("chargeStatePredictor", "overrideExistingCharge=true maxMultipleCharge=3 minMultipleCharge=2"),
        ("collisionEnergy", "low=10 high=50"),
        ("defaultArrayLength", "1-"),
        ("ETDFilter", "true true true false 0.1Da"),
        ("id", "scan=1"),
        ("index", "0-1"),
        ("isolationWidth", "[1,10]"),
        ("isolationWindows", "[1,10]"),
        ("lockmassRefiner", "mz=500 tol=1.0"),
        ("metadataFixer", ""),
        ("MS2Deisotope", "Poisson minCharge=1 maxCharge=3"),
        ("MS2Denoise", "20 100 true"),
        ("msLevel", "1-2"),
        ("mzPrecursors", "[100,200]"),
        // cpp insists on the bracketed list here and throws otherwise, so the port does too.
        ("mzPresent", "[100,200] mzTol=0.5 type=count threshold=1"),
        ("mzShift", "10ppm msLevels=1-"),
        ("mzWindow", "[100,500]"),
        ("peakPicking", "true 1-"),
        ("polarity", "positive"),
        ("scanEvent", "1-2"),
        ("scanNumber", "1-2"),
        ("scanSumming", "precursorTol=0.05 scanTimeTol=10"),
        ("scanTime", "[0,100]"),
        ("sortByScanTime", ""),
        ("stripIT", ""),
        ("thermoScanFilter", "contains include ms2"),
        ("threshold", "absolute 10 most-intense"),
        ("titleMaker", "<RunId>.<ScanNumber>"),
        ("turbocharger", "minCharge=2 maxCharge=3"),
        ("zeroSamples", "removeExtra 1-"),
    };

    /// <summary>
    /// Registered by cpp but not exercised here, each for a reason that is about the test rig
    /// rather than the filter. Listed explicitly so the set stays small and visible.
    /// </summary>
    private static readonly (string Name, string Why)[] NotExercised =
    {
        ("diaUmpire", "needs a .params file on disk"),
        ("mzRefiner", "needs pepXML/mzid identification files on disk"),
        ("demultiplex", "needs a real DIA multiplexed scan set to construct"),
        ("precursorRefine", "constructed from MSData rather than a spectrum list"),
        ("precursorRecalculation", "not ported - cpp calls it superseded by Thermo's own estimation"),
    };

    [TestMethod]
    public void EveryCppFilterName_ParsesAndWrapsTheList()
    {
        var failures = new List<string>();
        foreach (var (name, args) in CppFilters)
        {
            var inner = BuildList();
            string spec = string.IsNullOrEmpty(args) ? name : $"{name} {args}";
            try
            {
                var msd = new MSData();
                msd.Run.SpectrumList = inner;
                var wrapped = SpectrumListFactory.Wrap(inner, spec, msd);
                if (wrapped is null)
                    failures.Add($"  '{spec}' returned null");
                else if (ReferenceEquals(wrapped, inner))
                    failures.Add($"  '{spec}' returned the inner list unwrapped");
            }
            catch (Exception ex)
            {
                failures.Add($"  '{spec}' threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.AreEqual(0, failures.Count,
            $"{failures.Count} of {CppFilters.Length} cpp filter specs did not parse:" +
            Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    /// <summary>An unregistered name has to fail loudly rather than silently pass the list through.</summary>
    [TestMethod]
    public void UnknownFilterName_Throws()
    {
        var inner = BuildList();
        Assert.ThrowsException<ArgumentException>(() => SpectrumListFactory.Wrap(inner, "nosuchfilter 1-"));
    }

    /// <summary>Keeps the not-exercised list honest: every name in it is still one cpp registers.</summary>
    [TestMethod]
    public void NotExercisedFilters_AreDocumentedWithReasons()
    {
        foreach (var (name, why) in NotExercised)
            Assert.IsFalse(string.IsNullOrWhiteSpace(why), $"{name} needs a reason");
        Assert.AreEqual(0, CppFilters.Select(f => f.Name)
            .Intersect(NotExercised.Select(n => n.Name), StringComparer.OrdinalIgnoreCase).Count(),
            "a filter should be exercised or excused, not both");
    }

    private static SpectrumListSimple BuildList()
    {
        var inner = new SpectrumListSimple();
        for (int i = 0; i < 3; i++)
        {
            var s = new Spectrum { Index = i, Id = $"scan={i + 1}" };
            s.Params.Set(CVID.MS_ms_level, i == 0 ? 1 : 2);
            s.Params.Set(CVID.MS_MSn_spectrum);
            // Profile-shaped: each peak sits between zero samples. turbocharger's peak detector
            // rejects data without those flanks, and several other filters are happier with them.
            s.SetMZIntensityArrays(
                new[] { 99.0, 100.0, 101.0, 199.0, 200.0, 201.0, 299.0, 300.0, 301.0 },
                new[] { 0.0, 10.0, 0.0, 0.0, 20.0, 0.0, 0.0, 30.0, 0.0 },
                CVID.MS_number_of_detector_counts);
            var scan = new Scan();
            scan.Set(CVID.MS_scan_start_time, 10.0 * (i + 1), CVID.UO_second);
            s.ScanList.Scans.Add(scan);
            if (i > 0)
            {
                var precursor = new Precursor();
                precursor.Activation.Set(CVID.MS_collision_induced_dissociation);
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, 200.0, CVID.MS_m_z);
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, 1.0, CVID.MS_m_z);
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, 1.0, CVID.MS_m_z);
                var ion = new SelectedIon();
                ion.Set(CVID.MS_selected_ion_m_z, 200.0, CVID.MS_m_z);
                ion.Set(CVID.MS_charge_state, 2);
                precursor.SelectedIons.Add(ion);
                s.Precursors.Add(precursor);
            }
            inner.Spectra.Add(s);
        }
        return inner;
    }
}
