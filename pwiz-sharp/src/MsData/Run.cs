using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Samples;
using Pwiz.Data.MsData.Sources;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData;

/// <summary>A single coherent run: spectra + chromatograms + default references.</summary>
/// <remarks>
/// Port of pwiz::msdata::Run. Implements <see cref="IDisposable"/> so the SpectrumList /
/// ChromatogramList (which may hold native vendor handles) get released when the enclosing
/// <see cref="MSData"/> is disposed.
/// </remarks>
public sealed class Run : ParamContainer, IDisposable
{
    /// <summary>Unique id for this run.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Default instrument configuration (used when a scan doesn't reference one explicitly).</summary>
    public InstrumentConfiguration? DefaultInstrumentConfiguration { get; set; }

    /// <summary>The sample for this run.</summary>
    public Sample? Sample { get; set; }

    /// <summary>Run start timestamp, UT, ISO 8601 string.</summary>
    public string StartTimeStamp { get; set; } = string.Empty;

    /// <summary>Default source file.</summary>
    public SourceFile? DefaultSourceFile { get; set; }

    /// <summary>All mass spectra for this run.</summary>
    public ISpectrumList? SpectrumList { get; set; }

    /// <summary>All chromatograms for this run.</summary>
    public IChromatogramList? ChromatogramList { get; set; }

    /// <inheritdoc/>
    public override bool IsEmpty =>
        string.IsNullOrEmpty(Id)
        && DefaultInstrumentConfiguration is null
        && Sample is null
        && string.IsNullOrEmpty(StartTimeStamp)
        && DefaultSourceFile is null
        && SpectrumList is null
        && ChromatogramList is null
        && base.IsEmpty;

    /// <summary>Disposes the <see cref="SpectrumList"/> and <see cref="ChromatogramList"/>.</summary>
    public void Dispose()
    {
        SpectrumList?.Dispose();
        ChromatogramList?.Dispose();
    }
}
