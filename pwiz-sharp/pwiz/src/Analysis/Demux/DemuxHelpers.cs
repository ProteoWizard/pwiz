using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Demux;

/// <summary>
/// Precursor / isolation-window accessor helpers used throughout the demux subsystem. Port of
/// pwiz cpp's <c>pwiz/analysis/demux/DemuxHelpers.hpp</c>.
/// </summary>
internal static class DemuxHelpers
{
    /// <summary>Reads the isolation window upper offset for <paramref name="p"/>; throws when
    /// missing or non-positive.</summary>
    public static double PrecursorUpperOffset(Precursor p)
    {
        var param = p.IsolationWindow.CvParam(CVID.MS_isolation_window_upper_offset);
        if (string.IsNullOrEmpty(param.Value))
            throw new InvalidOperationException("PrecursorUpperOffset() No isolation window upper offset m/z specified");
        double upperOffset = param.ValueAs<double>();
        if (upperOffset <= 0.0)
            throw new InvalidOperationException("PrecursorUpperOffset() Positive values expected for isolation window m/z offsets");
        return upperOffset;
    }

    /// <summary>Reads the isolation window lower offset for <paramref name="p"/>; throws when
    /// missing or non-positive.</summary>
    public static double PrecursorLowerOffset(Precursor p)
    {
        var param = p.IsolationWindow.CvParam(CVID.MS_isolation_window_lower_offset);
        if (string.IsNullOrEmpty(param.Value))
            throw new InvalidOperationException("PrecursorLowerOffset() No isolation window lower offset m/z specified");
        double lowerOffset = param.ValueAs<double>();
        if (lowerOffset <= 0.0)
            throw new InvalidOperationException("PrecursorLowerOffset() Positive values expected for isolation window m/z offsets");
        return lowerOffset;
    }

    /// <summary>Isolation window target m/z for <paramref name="p"/>; throws when missing.</summary>
    public static double PrecursorTarget(Precursor p)
    {
        var param = p.IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z);
        if (string.IsNullOrEmpty(param.Value))
            throw new InvalidOperationException("PrecursorTarget() No isolation window target m/z specified");
        return param.ValueAs<double>();
    }

    /// <summary>Lower edge of the isolation window: target − lower offset.</summary>
    public static double PrecursorMzLow(Precursor p) => PrecursorTarget(p) - PrecursorLowerOffset(p);

    /// <summary>Upper edge of the isolation window: target + upper offset.</summary>
    public static double PrecursorMzHigh(Precursor p) => PrecursorTarget(p) + PrecursorUpperOffset(p);

    /// <summary>Geometric center of the isolation window: (low + high) / 2.</summary>
    public static double PrecursorIsoCenter(Precursor p)
    {
        double target = PrecursorTarget(p);
        return (target - PrecursorLowerOffset(p) + target + PrecursorUpperOffset(p)) / 2.0;
    }

    /// <summary>Total width of the isolation window: lower offset + upper offset.</summary>
    public static double PrecursorIsoWidth(Precursor p) => PrecursorLowerOffset(p) + PrecursorUpperOffset(p);

    /// <summary>Two-decimal stringification of the precursor's iso-center, used as a map key
    /// during cycle identification (cpp <c>prec_to_string</c>).</summary>
    public static string PrecToString(Precursor p) =>
        PrecursorIsoCenter(p).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Returns indices of MS2 spectra near <paramref name="centerIndex"/> in
    /// <paramref name="sl"/>, suitable for assembling a demux block. Walks backward and forward
    /// from the center; if not enough MS2 spectra exist on one side, pulls extra from the other.
    /// Returns <c>false</c> when fewer than <paramref name="numSpectraToFind"/> MS2 spectra are
    /// available in the file. Port of cpp's <c>FindNearbySpectra</c>.</summary>
    public static bool FindNearbySpectra(List<int> spectraIndices, ISpectrumList sl, int centerIndex,
        int numSpectraToFind, int stride = 1)
    {
        ArgumentNullException.ThrowIfNull(sl);
        ArgumentNullException.ThrowIfNull(spectraIndices);
        if (centerIndex < 0 || centerIndex >= sl.Count)
            throw new ArgumentOutOfRangeException(nameof(centerIndex));

        // Use the cached MS-level lookup when available — drops ~500 metadata-only GetSpectrum
        // calls per source spectrum (one per stride step × backwards+forward × cycles × mux rows)
        // down to zero. Falls back to the GetSpectrum path for callers that pass a raw list.
        var levels = sl as IMsLevelProvider;
        int MsLevel(int idx) => levels is not null
            ? levels.GetMsLevel(idx)
            : sl.GetSpectrum(idx, getBinaryData: false).Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);

        if (MsLevel(centerIndex) != 2)
            throw new InvalidOperationException("Center index must be an MS2 spectrum");

        spectraIndices.Clear();
        spectraIndices.Add(centerIndex);
        int backwardsNeeded = (int)System.Math.Round((numSpectraToFind - 1) / 2.0);
        int afterNeeded = numSpectraToFind - 1 - backwardsNeeded;
        int indexLoc = centerIndex;
        int stepCount = 0;

        // Walk backward.
        while (backwardsNeeded > 0 && indexLoc != 0)
        {
            indexLoc--;
            if (MsLevel(indexLoc) == 2)
            {
                stepCount++;
                if (stepCount == stride)
                {
                    spectraIndices.Add(indexLoc);
                    backwardsNeeded--;
                    stepCount = 0;
                }
            }
        }

        // Walk forward — pulling extras for whatever the backward walk couldn't satisfy.
        afterNeeded += backwardsNeeded;
        indexLoc = centerIndex + 1;
        stepCount = 0;
        while (indexLoc < sl.Count && afterNeeded > 0)
        {
            if (MsLevel(indexLoc) == 2)
            {
                stepCount++;
                if (stepCount == stride)
                {
                    spectraIndices.Add(indexLoc);
                    afterNeeded--;
                    stepCount = 0;
                }
            }
            indexLoc++;
        }

        // Hit end of file too — try walking backward from the earliest already-found.
        if (afterNeeded > 0 && spectraIndices.Count > 0)
            indexLoc = spectraIndices.Min();
        while (afterNeeded > 0 && indexLoc != 0)
        {
            indexLoc--;
            if (MsLevel(indexLoc) == 2)
            {
                stepCount++;
                if (stepCount == stride)
                {
                    spectraIndices.Add(indexLoc);
                    afterNeeded--;
                    stepCount = 0;
                }
            }
        }

        if (spectraIndices.Count != numSpectraToFind) return false;
        spectraIndices.Sort();
        return true;
    }
}
