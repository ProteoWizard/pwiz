using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707

namespace Pwiz.Analysis;

/// <summary>
/// Refines MS2 precursor m/z values by re-centroiding the peak in the surrounding MS1 scans.
/// Port of <c>pwiz::analysis::SpectrumList_PrecursorRefine</c>.
/// </summary>
/// <remarks>
/// <para>For each MS2, looks at the prior MS1 plus the next two MS1 scans, finds the peak in each
/// MS1 within ±windowFactor·m/z around the original precursor estimate, and computes an
/// intensity-weighted centroid across the three. Returns the original m/z if no MS1s have data
/// in the window or if the candidate peak is too close to the window edge.</para>
/// <para>Only operates on Orbitrap, FT-ICR, and TOF data — other mass analyzer types pass through
/// unchanged. Cannot be combined with <see cref="SpectrumList_PeakPicker"/> in the same chain
/// (cpp comment in <c>SpectrumList_PrecursorRefine.cpp:131</c>: "spectrum list wrapper nesting"
/// problem; precursorRefine needs the un-picked profile data the picker has already discarded).</para>
/// </remarks>
public sealed class SpectrumList_PrecursorRefine : SpectrumListWrapper
{
    private readonly CVID _targetMassAnalyzerType;

    /// <summary>Constructs a precursor-refining wrapper for <paramref name="msd"/>'s spectrum list.
    /// The constructor inspects <paramref name="msd"/>'s instrument configurations to pick the
    /// highest-accuracy mass analyzer; spectra acquired on unsupported analyzer types pass through
    /// unchanged.</summary>
    public SpectrumList_PrecursorRefine(MSData msd) : base(GetSpectrumList(msd))
    {
        _targetMassAnalyzerType = ChooseTargetMassAnalyzerType(msd);
    }

    private static ISpectrumList GetSpectrumList(MSData msd)
    {
        ArgumentNullException.ThrowIfNull(msd);
        return msd.Run.SpectrumList ?? throw new InvalidOperationException(
            "[SpectrumList_PrecursorRefine] MSData has no spectrum list.");
    }

    private static CVID ChooseTargetMassAnalyzerType(MSData msd)
    {
        // Cpp picks the highest-accuracy analyzer in priority order: FT-ICR > orbitrap > TOF.
        // First match in the loop wins because the inner condition skips updates once one of
        // those three is set. Mirror that exact ordering.
        var current = CVID.CVID_Unknown;
        foreach (var ic in msd.InstrumentConfigurations)
        {
            foreach (var component in ic.ComponentList)
            {
                if (component.Type != ComponentType.Analyzer) continue;
                if (current is CVID.MS_FT_ICR or CVID.MS_orbitrap or CVID.MS_time_of_flight) continue;
                // Walk component's CV params, take the first that's a child of MS_mass_analyzer_type.
                foreach (var p in component.CVParams)
                {
                    if (CvLookup.CvIsA(p.Cvid, CVID.MS_mass_analyzer_type))
                    {
                        current = p.Cvid;
                        break;
                    }
                }
            }
        }
        return current;
    }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing
    {
        get
        {
            var dp = new Pwiz.Data.MsData.Processing.DataProcessing(Inner.DataProcessing?.Id ?? "pwiz_Reader_conversion");
            if (Inner.DataProcessing is not null)
                foreach (var pm in Inner.DataProcessing.ProcessingMethods)
                    dp.ProcessingMethods.Add(pm);
            var method = new ProcessingMethod
            {
                Order = dp.ProcessingMethods.Count,
                Software = dp.ProcessingMethods.FirstOrDefault()?.Software,
            };
            method.UserParams.Add(new UserParam("precursor refinement", "msPrefix defaults"));
            dp.ProcessingMethods.Add(method);
            return dp;
        }
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        // Pass through for non-supported analyzer types — pre-existing precursor m/z is returned
        // verbatim. Cpp does the same (line 128).
        if (_targetMassAnalyzerType is not CVID.MS_FT_ICR and not CVID.MS_orbitrap and not CVID.MS_time_of_flight)
            return Inner.GetSpectrum(index, getBinaryData);

        var spec = Inner.GetSpectrum(index, getBinaryData);
        if (!IsRefineable(spec)) return spec;

        // Refine each selected ion's m/z via intensity-weighted centroid across 3 nearby MS1s.
        foreach (var ion in spec.Precursors[0].SelectedIons)
        {
            double initial = ion.CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>();
            double refined = RefineMassVal(initial, index);
            ion.Set(CVID.MS_selected_ion_m_z, refined);
        }
        return spec;
    }

    private static bool IsRefineable(Spectrum spec)
    {
        // MSn spectra only — but we can't use cvParamChild semantics easily; key on MS_ms_level
        // ≥ 2 plus presence of a selected ion. Equivalent in practice to cpp's check.
        if (spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) < 2) return false;
        if (spec.Precursors.Count == 0) return false;
        if (spec.Precursors[0].SelectedIons.Count == 0) return false;
        if (spec.Precursors[0].SelectedIons[0].CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>() == 0.0) return false;
        return true;
    }

    private double RefineMassVal(double initialEstimate, int index)
    {
        if (initialEstimate == 0) return initialEstimate;

        // Locate the prior MS1 (parentIndex[0] walking backward) and the next two MS1s
        // (parentIndex[1], parentIndex[2] walking forward). Cpp's loop walks all three indices
        // simultaneously; we replicate the exact stopping conditions so the chosen MS1 trio
        // matches cpp bit-for-bit.
        int idx0 = index, idx1 = index, idx2 = index;
        bool found0 = false, found1 = false, found2 = false;
        Spectrum? ms1a = null, ms1b = null, ms1c = null;

        while (true)
        {
            if (!found0 && idx0-- == 0) return initialEstimate;
            if (!found1 && idx1++ >= Inner.Count - 2) return initialEstimate;
            if (!found2 && idx2++ >= Inner.Count - 2) return initialEstimate;

            if (!found0 && GetMsLevel(idx0) == 1)
            {
                found0 = true;
                ms1a = Inner.GetSpectrum(idx0, getBinaryData: true);
            }
            if (!found1 && GetMsLevel(idx1) == 1)
            {
                ms1b = Inner.GetSpectrum(idx1, getBinaryData: true);
                found1 = true;
                idx2 = idx1 + 1;
            }
            if (found1 && idx2 != index && GetMsLevel(idx2) == 1)
            {
                ms1c = Inner.GetSpectrum(idx2, getBinaryData: true);
                found2 = true;
            }
            if (found0 && found1 && found2) break;
        }

        if (ms1a is null || ms1b is null || ms1c is null) return initialEstimate;

        bool isOrbitrap = _targetMassAnalyzerType == CVID.MS_orbitrap;
        double windowFactor = isOrbitrap ? 30e-6 : 90e-6;
        double mzLow = initialEstimate - windowFactor * initialEstimate;
        double mzHigh = initialEstimate + windowFactor * initialEstimate;
        int exponent = isOrbitrap ? 8 : 4;
        int width = isOrbitrap ? 1 : 2;

        double newCentroid = 0;
        double denom = 0;

        AccumulateRefinedCentroid(ms1a, mzLow, mzHigh, width, exponent, ref newCentroid, ref denom);
        AccumulateRefinedCentroid(ms1b, mzLow, mzHigh, width, exponent, ref newCentroid, ref denom);
        AccumulateRefinedCentroid(ms1c, mzLow, mzHigh, width, exponent, ref newCentroid, ref denom);

        return denom > 0 ? newCentroid / denom : initialEstimate;
    }

    private int GetMsLevel(int index)
    {
        // No metadata-only fast path here — just read the spectrum and pull the CV param.
        var s = Inner.GetSpectrum(index, getBinaryData: false);
        return s.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
    }

    private static void AccumulateRefinedCentroid(Spectrum ms1, double mzLow, double mzHigh,
        int width, int exponent, ref double newCentroid, ref double denom)
    {
        var mz = ms1.GetMZArray()?.Data;
        var intensity = ms1.GetIntensityArray()?.Data;
        if (mz is null || intensity is null || mz.Count == 0) return;

        // Binary-search the lower / upper bound of the window. mz is sorted ascending.
        int lo = LowerBound(mz, mzLow);
        int hi = LowerBound(mz, mzHigh);
        if (hi >= mz.Count) hi = mz.Count - 1;

        // Scan for the max-intensity sample in [lo, hi].
        int maxIdx = 0;
        double intensMax = 0;
        for (int p = lo, ix = 0; p <= hi; p++, ix++)
        {
            if (intensity[p] > intensMax)
            {
                intensMax = intensity[p];
                maxIdx = ix;
            }
        }

        // Skip if the peak hugs the edge of the window — too close means the peak isn't a real
        // local maximum, just edge clipping.
        int span = hi - lo;
        if (maxIdx < width || maxIdx > span - width) return;

        for (int ii = -width; ii <= width; ii++)
        {
            int p = lo + maxIdx + ii;
            double w = System.Math.Pow(intensity[p], exponent);
            newCentroid += mz[p] * w;
            denom += w;
        }
    }

    private static int LowerBound(IReadOnlyList<double> sorted, double key)
    {
        int lo = 0, hi = sorted.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (sorted[mid] < key) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }
}
