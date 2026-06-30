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
            ShiftCvParam(spec.Params, CVID.MS_base_peak_m_z);
            foreach (var scan in spec.ScanList.Scans)
            {
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
                ShiftCvParam(precursor.IsolationWindow, CVID.MS_isolation_window_target_m_z);
                // lower/upper offsets are widths, not absolute m/z, so they don't shift.
                foreach (var si in precursor.SelectedIons)
                    ShiftCvParam(si, CVID.MS_selected_ion_m_z);
            }
        }

        return spec;
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
