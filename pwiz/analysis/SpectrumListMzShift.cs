using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis;

/// <summary>
/// Wraps an inner <see cref="ISpectrumList"/> and shifts m/z values by a constant absolute
/// (Da) or relative (ppm) amount on every spectrum whose MS level is in <see cref="MsLevels"/>.
/// Port of pwiz <c>SpectrumList_PeakFilter(MzShiftFilter)</c>.
/// </summary>
/// <remarks>
/// Shifts both the m/z binary array AND the metadata that carries m/z values:
/// scan windows (lower/upper limit), base peak m/z, and (when the precursor's ms level is in
/// <see cref="MsLevels"/>) isolation window target/offsets and selected ion m/z.
/// </remarks>
public sealed class SpectrumListMzShift : SpectrumListWrapper
{
    /// <summary>The shift to apply.</summary>
    public MZTolerance Shift { get; }

    /// <summary>MS levels whose m/z values are shifted.</summary>
    public IntegerSet MsLevels { get; }

    /// <summary>Wraps <paramref name="inner"/>, applying <paramref name="shift"/> on the given MS levels.</summary>
    public SpectrumListMzShift(ISpectrumList inner, MZTolerance shift, IntegerSet? msLevels = null)
        : base(inner)
    {
        Shift = shift;
        MsLevels = msLevels ?? IntegerSet.Positive;
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        var spec = Inner.GetSpectrum(index, getBinaryData);
        int msLevel = spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);

        // Shift the spectrum's own m/z metadata when this spectrum's level is in scope.
        if (MsLevels.Contains(msLevel))
        {
            // cpp does not name the params it shifts - it rewrites every CV param whose units are
            // m/z (MzShiftFilter.cpp:51). Naming them individually silently dropped
            // lowest/highest observed m/z, which left the spectrum declaring an m/z range its own
            // shifted array contradicted.
            ShiftAllMzParams(spec.Params);
            foreach (var scan in spec.ScanList.Scans)
            {
                ShiftAllMzParams(scan);
                // cpp stops at the scan itself and never descends into its windows. Shifting them
                // keeps the window consistent with the data it describes, so the port does it
                // deliberately; it is the one place here that emits different m/z than cpp would.
                foreach (var window in scan.ScanWindows)
                {
                    ShiftCvParam(window, CVID.MS_scan_window_lower_limit);
                    ShiftCvParam(window, CVID.MS_scan_window_upper_limit);
                }
            }

            if (getBinaryData)
            {
                var mzArr = spec.GetMZArray();
                if (mzArr is not null)
                {
                    for (int i = 0; i < mzArr.Data.Count; i++)
                        mzArr.Data[i] = mzArr.Data[i] + Shift;
                }
            }
        }

        // Shift precursor m/z metadata when the precursor's level (msLevel - 1) is in scope.
        // Lets callers shift only MS2s (msLevels=2) without touching the precursor m/z, or
        // shift only MS1s (msLevels=1) and have the precursor on subsequent MS2s shifted too.
        if (MsLevels.Contains(msLevel - 1))
        {
            foreach (var precursor in spec.Precursors)
            {
                ShiftAllMzParams(precursor);
                ShiftAllMzParams(precursor.Activation);
                ShiftCvParam(precursor.IsolationWindow, CVID.MS_isolation_window_target_m_z);
                // cpp shifts the whole isolation window generically, which moves the lower/upper
                // OFFSETS too - they carry m/z units but are widths, so a shift corrupts the
                // window width rather than relocating it. Only the target moves here. This is the
                // second and last place the port deliberately emits different m/z than cpp.
                foreach (var si in precursor.SelectedIons)
                    ShiftAllMzParams(si);
            }
        }

        return spec;
    }

    /// <summary>Shifts every CV param in the container carrying m/z units, as cpp's
    /// <c>doMzShift</c> does, so a param nobody thought to name still moves with the data.</summary>
    private void ShiftAllMzParams(ParamContainer container)
    {
        foreach (var p in container.CVParams)
        {
            if (p.Units != CVID.MS_m_z) continue;
            p.Value = (p.ValueAs<double>() + Shift).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private void ShiftCvParam(ParamContainer container, CVID cvid)
    {
        var p = container.CvParam(cvid);
        if (p.IsEmpty) return;
        // Replace the existing param with a shifted one. The CVParam API exposes set/replace
        // semantics via Params.Set; for non-Spectrum containers we use the same Set helper.
        double shifted = p.ValueAs<double>() + Shift;
        container.Set(cvid, shifted, p.Units);
    }
}
