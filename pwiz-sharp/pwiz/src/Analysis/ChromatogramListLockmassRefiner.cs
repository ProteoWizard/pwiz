using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Vendor.Waters;

namespace Pwiz.Analysis;

/// <summary>
/// <see cref="IChromatogramList"/> wrapper for Waters lockmass refinement of chromatograms.
/// Port of <c>pwiz::analysis::ChromatogramList_LockmassRefiner</c>.
/// </summary>
/// <remarks>
/// On Waters input, the wrapper appends a "m/z calibration" + "Waters lockmass correction"
/// ProcessingMethod to the data-processing chain and threads the lockmass parameters through
/// <see cref="ChromatogramList_Waters.GetChromatogramWithLockmass"/>. On non-Waters input,
/// the wrapper logs a warning and passes chromatograms through unchanged — matching cpp's
/// behavior in <c>ChromatogramList_LockmassRefiner.cpp:60-66</c>.
/// <para>The binary chromatogram payload is not modified even on Waters input: cpp's
/// chromatogram-lockmass overload (<c>ChromatogramList_Waters.cpp:91</c>) accepts the
/// lockmass parameters in its signature but never references them, because the Waters
/// MassLynx <c>ChromatogramReader</c> doesn't expose a lockmass-aware read path and SRM/SIM
/// transition m/z values come straight from the function definition rather than peak-picked
/// spectra. Sharp matches that exactly: the metadata trail records the user's intent and the
/// extension hook is wired up, but no array values change.</para>
/// </remarks>
public sealed class ChromatogramListLockmassRefiner : ChromatogramListWrapper
{
    private readonly double _mzPositiveScans;
    private readonly double _mzNegativeScans;
    private readonly double _tolerance;

    /// <summary>Constructs the wrapper. <paramref name="lockmassMzPosScans"/> is the lockmass
    /// m/z for positive-mode scans; <paramref name="lockmassMzNegScans"/> is the value for
    /// negative-mode scans (often equal). <paramref name="lockmassTolerance"/> is in Da.</summary>
    public ChromatogramListLockmassRefiner(IChromatogramList inner,
        double lockmassMzPosScans, double lockmassMzNegScans, double lockmassTolerance)
        : base(inner)
    {
        _mzPositiveScans = lockmassMzPosScans;
        _mzNegativeScans = lockmassMzNegScans;
        _tolerance = lockmassTolerance;

        if (inner is ChromatogramList_Waters)
        {
            var method = new ProcessingMethod
            {
                Order = Dp.ProcessingMethods.Count,
                Software = Dp.ProcessingMethods.FirstOrDefault()?.Software,
            };
            method.Set(CVID.MS_m_z_calibration);
            method.UserParams.Add(new UserParam("Waters lockmass correction"));
            Dp.ProcessingMethods.Add(method);
        }
        else
        {
            Console.Error.WriteLine(
                "Warning: lockmass refinement for chromatogram data was requested, but is unavailable for non-Waters input data.");
        }
    }

    /// <inheritdoc/>
    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false)
    {
        Chromatogram c = Inner is ChromatogramList_Waters waters
            ? waters.GetChromatogramWithLockmass(index, getBinaryData,
                _mzPositiveScans, _mzNegativeScans, _tolerance)
            : Inner.GetChromatogram(index, getBinaryData);
        c.DataProcessing = Dp;
        return c;
    }
}
