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
/// Construction-time behavior matches cpp: if the inner list is a
/// <see cref="ChromatogramList_Waters"/>, append a "m/z calibration" + "Waters lockmass
/// correction" ProcessingMethod to the data-processing chain. Otherwise the wrapper is a
/// passthrough and a warning is logged.
/// <para>Read-time: sharp's <c>ChromatogramList_Waters</c> does not yet expose a
/// lockmass-aware <c>GetChromatogram</c> overload (cpp passes (mzPos, mzNeg, tol) to
/// <c>ChromatogramList_Waters::chromatogram</c>). Until that's wired up, this wrapper just
/// stamps the DataProcessing on each returned chromatogram so downstream consumers see the
/// processing history — the binary data itself isn't refined yet. Matches cpp's behavior on
/// builds without <c>PWIZ_READER_WATERS</c>.</para>
/// </remarks>
public sealed class ChromatogramListLockmassRefiner : ChromatogramListWrapper
{
    // Stored for the day sharp's Waters reader gains the lockmass-aware chromatogram overload;
    // until then these are documentation. Kept here so the public ctor signature matches cpp's
    // and so callers can configure the wrapper from the msconvert filter string today.
#pragma warning disable CA1823 // unused fields — see remarks
    private readonly double _mzPositiveScans;
    private readonly double _mzNegativeScans;
    private readonly double _tolerance;
#pragma warning restore CA1823

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
            // cpp logs this to stderr (Connection_LockmassRefiner.cpp:60); mirror.
            Console.Error.WriteLine(
                "Warning: lockmass refinement for chromatogram data was requested, but is unavailable for non-Waters input data.");
        }
    }

    /// <inheritdoc/>
    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false)
    {
        var c = Inner.GetChromatogram(index, getBinaryData);
        c.DataProcessing = Dp;
        return c;
    }
}
