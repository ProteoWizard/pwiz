using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Sources;

namespace Pwiz.Data.MsData.Spectra;

/// <summary>Isolation (selection) window for a precursor. Port of pwiz::msdata::IsolationWindow.</summary>
public sealed class IsolationWindow : ParamContainer { }

/// <summary>A precursor ion that was selected for MS/MS. Port of pwiz::msdata::SelectedIon.</summary>
public sealed class SelectedIon : ParamContainer
{
    /// <summary>Creates an empty selected ion.</summary>
    public SelectedIon() { }

    /// <summary>Creates a selected ion with a selected-ion m/z CV param.</summary>
    public SelectedIon(double mz)
    {
        Set(CVID.MS_selected_ion_m_z, mz, CVID.MS_m_z);
    }

    /// <summary>Creates a selected ion with m/z and intensity.</summary>
    public SelectedIon(double mz, double intensity, CVID intensityUnit)
    {
        Set(CVID.MS_selected_ion_m_z, mz, CVID.MS_m_z);
        Set(CVID.MS_peak_intensity, intensity, intensityUnit);
    }

    /// <summary>Creates a selected ion with m/z and charge state.</summary>
    public SelectedIon(double mz, int chargeState)
    {
        Set(CVID.MS_selected_ion_m_z, mz, CVID.MS_m_z);
        Set(CVID.MS_charge_state, chargeState);
    }

    /// <summary>Creates a selected ion with m/z, intensity, and charge state.</summary>
    public SelectedIon(double mz, double intensity, int chargeState, CVID intensityUnit)
    {
        Set(CVID.MS_selected_ion_m_z, mz, CVID.MS_m_z);
        Set(CVID.MS_peak_intensity, intensity, intensityUnit);
        Set(CVID.MS_charge_state, chargeState);
    }
}

/// <summary>Type and energy level used for activation. Port of pwiz::msdata::Activation.</summary>
public sealed class Activation : ParamContainer { }

/// <summary>Precursor ion selection and activation. Port of pwiz::msdata::Precursor.</summary>
public sealed class Precursor : ParamContainer
{
    /// <summary>
    /// Source file for precursor spectra external to this document.
    /// Mutually exclusive with <see cref="SpectrumId"/>.
    /// </summary>
    public SourceFile? SourceFile { get; set; }

    /// <summary>Spectrum id in an external source file (mutually exclusive with <see cref="SpectrumId"/>).</summary>
    public string ExternalSpectrumId { get; set; } = string.Empty;

    /// <summary>Local spectrum id from which this precursor was selected.</summary>
    public string SpectrumId { get; set; } = string.Empty;

    /// <summary>Isolation window used to isolate this precursor.</summary>
    public IsolationWindow IsolationWindow { get; set; } = new();

    /// <summary>Ions that were selected.</summary>
    public List<SelectedIon> SelectedIons { get; } = new();

    /// <summary>Activation type/energy.</summary>
    public Activation Activation { get; set; } = new();

    /// <summary>Creates an empty precursor.</summary>
    public Precursor() { }

    /// <summary>Creates a precursor with a single selected ion (m/z only).</summary>
    public Precursor(double mz) => SelectedIons.Add(new SelectedIon(mz));

    /// <summary>Creates a precursor with a single selected ion (m/z + intensity).</summary>
    public Precursor(double mz, double intensity, CVID intensityUnit) =>
        SelectedIons.Add(new SelectedIon(mz, intensity, intensityUnit));

    /// <summary>Creates a precursor with a single selected ion (m/z + charge).</summary>
    public Precursor(double mz, int chargeState) =>
        SelectedIons.Add(new SelectedIon(mz, chargeState));

    /// <summary>Creates a precursor with a single selected ion (m/z + intensity + charge).</summary>
    public Precursor(double mz, double intensity, int chargeState, CVID intensityUnit) =>
        SelectedIons.Add(new SelectedIon(mz, intensity, chargeState, intensityUnit));

    /// <inheritdoc/>
    public override bool IsEmpty =>
        SourceFile is null
        && string.IsNullOrEmpty(ExternalSpectrumId)
        && string.IsNullOrEmpty(SpectrumId)
        && IsolationWindow.IsEmpty
        && SelectedIons.Count == 0
        && Activation.IsEmpty
        && base.IsEmpty;
}

/// <summary>Product ion information. Port of pwiz::msdata::Product.</summary>
public sealed class Product : IEquatable<Product>
{
    /// <summary>Isolation window for this product.</summary>
    public IsolationWindow IsolationWindow { get; set; } = new();

    /// <summary>True iff the isolation window is empty.</summary>
    public bool IsEmpty => IsolationWindow.IsEmpty;

    /// <inheritdoc/>
    public bool Equals(Product? other) => other is not null && IsolationWindow.Equals(other.IsolationWindow);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Product);

    /// <inheritdoc/>
    public override int GetHashCode() => IsolationWindow.GetHashCode();

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Product? a, Product? b) => Equals(a, b);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Product? a, Product? b) => !Equals(a, b);
}

/// <summary>A scan window (a single [low, high] m/z range). Port of pwiz::msdata::ScanWindow.</summary>
public sealed class ScanWindow : ParamContainer
{
    /// <summary>Creates an empty scan window.</summary>
    public ScanWindow() { }

    /// <summary>Creates a scan window with lower/upper limits in the given units.</summary>
    public ScanWindow(double low, double high, CVID unit)
    {
        Set(CVID.MS_scan_window_lower_limit, low, unit);
        Set(CVID.MS_scan_window_upper_limit, high, unit);
    }
}

/// <summary>A single scan within a spectrum. Port of pwiz::msdata::Scan.</summary>
public sealed class Scan : ParamContainer
{
    /// <summary>External source file (mutually exclusive with <see cref="SpectrumId"/>).</summary>
    public SourceFile? SourceFile { get; set; }

    /// <summary>External spectrum id.</summary>
    public string ExternalSpectrumId { get; set; } = string.Empty;

    /// <summary>Local spectrum id.</summary>
    public string SpectrumId { get; set; } = string.Empty;

    /// <summary>Instrument configuration used for this scan.</summary>
    public InstrumentConfiguration? InstrumentConfiguration { get; set; }

    /// <summary>Scan windows used.</summary>
    public List<ScanWindow> ScanWindows { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty =>
        SourceFile is null
        && string.IsNullOrEmpty(ExternalSpectrumId)
        && string.IsNullOrEmpty(SpectrumId)
        && InstrumentConfiguration is null
        && ScanWindows.Count == 0
        && base.IsEmpty;
}

/// <summary>List of scans for a spectrum. Port of pwiz::msdata::ScanList.</summary>
public sealed class ScanList : ParamContainer
{
    /// <summary>The scans.</summary>
    public List<Scan> Scans { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty => Scans.Count == 0 && base.IsEmpty;
}
