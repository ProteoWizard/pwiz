using System.Runtime.InteropServices;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis;

/// <summary>
/// Wraps an inner <see cref="ISpectrumList"/> and, on read, drops every peak outside
/// <c>[Low, High]</c>. Port of pwiz <c>SpectrumList_MZWindow</c>.
/// </summary>
/// <remarks>
/// Every binary and integer array the same length as the m/z array (intensity, and parallel
/// arrays such as ion mobility in combined IMS spectra) is trimmed with it, so each kept peak
/// keeps its own values. Peaks keep their input order: a spectrum with an ion mobility axis is
/// ordered by mobility on purpose (see <see cref="SpectrumListBase.HasNonMzOrderingAxis"/>), so
/// the window does not re-sort it. Arrays of any other length are left as they are, and a
/// spectrum whose intensity array is out of step with m/z is returned unchanged.
/// The <c>defaultArrayLength</c> on each spectrum is recomputed from the trimmed array length;
/// other metadata (TIC / base peak) is left untouched - combine with the <c>metadataFixer</c>
/// filter downstream when those need refreshing.
/// </remarks>
public sealed class SpectrumListMzWindow : SpectrumListWrapper
{
    /// <summary>Lower bound of the kept m/z window (inclusive).</summary>
    public double Low { get; }

    /// <summary>Upper bound of the kept m/z window (inclusive).</summary>
    public double High { get; }

    /// <summary>Wraps <paramref name="inner"/>, keeping only peaks in <c>[low, high]</c>.</summary>
    public SpectrumListMzWindow(ISpectrumList inner, double low, double high) : base(inner)
    {
        if (low > high) throw new ArgumentException("low must be <= high.", nameof(low));
        Low = low;
        High = high;
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        var spec = Inner.GetSpectrum(index, getBinaryData);
        if (!getBinaryData) return spec;

        var mzArr = spec.GetMZArray();
        if (mzArr is null) return spec;

        // Peaks cannot be paired with intensities that are out of step with m/z, so such a
        // spectrum is left untouched rather than trimmed into a worse mismatch
        int n = mzArr.Data.Count;
        var intArr = spec.GetIntensityArray();
        if (intArr is not null && intArr.Data.Count != n) return spec;

        var mzs = mzArr.Data.AsSpan();
        var kept = new List<int>(n);
        for (int i = 0; i < n; i++)
        {
            double m = mzs[i];
            if (m >= Low && m <= High)
                kept.Add(i);
        }
        if (kept.Count == n) return spec; // Nothing outside the window

        foreach (var arr in spec.BinaryDataArrays)
        {
            if (arr.Data.Count == n)
            {
                KeepValuesAt(arr.Data.AsSpan(), kept);
                arr.Data.SetCount(kept.Count);
            }
        }
        foreach (var arr in spec.IntegerDataArrays)
        {
            if (arr.Data.Count == n)
            {
                KeepValuesAt(CollectionsMarshal.AsSpan(arr.Data), kept);
                arr.Data.RemoveRange(kept.Count, n - kept.Count);
            }
        }

        spec.DefaultArrayLength = kept.Count;
        return spec;
    }

    /// <inheritdoc/>
    /// <remarks>The window changes the peaks only, so no peaks are read below full metadata.</remarks>
    public override Spectrum GetSpectrum(int index, DetailLevel detailLevel) =>
        detailLevel < DetailLevel.FullMetadata
            ? GetSpectrumWithoutPeakCount(index, detailLevel)
            : GetSpectrum(index, detailLevel >= DetailLevel.FullData);

    /// <summary>Moves the entries at the ascending <paramref name="indices"/> to the front of
    /// <paramref name="values"/>, in place; the caller then truncates to the kept count.</summary>
    private static void KeepValuesAt<T>(Span<T> values, List<int> indices)
    {
        for (int i = 0; i < indices.Count; i++)
            values[i] = values[indices[i]];
    }
}
