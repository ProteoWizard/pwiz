using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis;

/// <summary>
/// Wraps an inner <see cref="ISpectrumList"/> and, on read, drops every (m/z, intensity) pair
/// outside <c>[Low, High]</c>. Port of pwiz <c>SpectrumList_MZWindow</c>.
/// </summary>
/// <remarks>
/// The <c>defaultArrayLength</c> on each spectrum is recomputed from the trimmed array length;
/// other metadata (TIC / base peak) is left untouched — combine with the <c>metadataFixer</c>
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
        var intArr = spec.GetIntensityArray();
        if (mzArr is null || intArr is null) return spec;

        int n = Math.Min(mzArr.Data.Count, intArr.Data.Count);
        var newMz = new List<double>(n);
        var newInt = new List<double>(n);
        for (int i = 0; i < n; i++)
        {
            double m = mzArr.Data[i];
            if (m < Low || m > High) continue;
            newMz.Add(m);
            newInt.Add(intArr.Data[i]);
        }

        mzArr.Data.Clear();
        mzArr.Data.AddRange(newMz);
        intArr.Data.Clear();
        intArr.Data.AddRange(newInt);
        spec.DefaultArrayLength = newMz.Count;
        return spec;
    }
}
