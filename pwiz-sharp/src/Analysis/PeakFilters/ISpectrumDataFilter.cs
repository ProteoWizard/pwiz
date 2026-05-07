using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.PeakFilters;

/// <summary>
/// Mutates a <see cref="Spectrum"/>'s binary peak data in place. C# equivalent of pwiz cpp's
/// <c>SpectrumDataFilter</c> (<c>pwiz/analysis/common/DataFilter.hpp</c>): every peak-level
/// filter (<see cref="ThresholdFilter"/>, <see cref="Ms2NoiseFilter"/>, <see cref="Ms2Deisotoper"/>,
/// <see cref="EtdPrecursorMassFilter"/>) implements this so a single
/// <see cref="SpectrumListPeakFilter"/> can drive any of them.
/// </summary>
/// <remarks>
/// Implementations are responsible for their own gating (MS-level, presence of precursor, etc.):
/// <see cref="SpectrumListPeakFilter"/> calls <see cref="Apply"/> on every spectrum it loads with
/// binary data — the filter decides whether to skip.
/// </remarks>
public interface ISpectrumDataFilter
{
    /// <summary>Mutates <paramref name="spectrum"/>'s peak arrays in place.</summary>
    void Apply(Spectrum spectrum);
}
