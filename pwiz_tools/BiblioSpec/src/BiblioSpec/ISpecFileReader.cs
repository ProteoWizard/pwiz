// Port of pwiz_tools/BiblioSpec/src/SpecFileReader.h

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Interface implemented by every BiblioSpec spectrum-file reader (e.g. mzML, MS2, msp).
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::SpecFileReader</c> (SpecFileReader.h:36). cpp declares a
/// virtual destructor; in C# we extend <see cref="IDisposable"/> to model the same
/// "owns native handles" contract.</para>
/// <para>The cpp version provides a default-implemented <c>getSpectrum(PSM*, ...)</c> that
/// dispatches by <see cref="SpecIdType"/>; in this port that dispatch lives on
/// <see cref="SpecFileReaderBase"/>, so concrete implementations only need to override the
/// three primitive overloads.</para>
/// </remarks>
public interface ISpecFileReader : IDisposable
{
    /// <summary>
    /// Open <paramref name="path"/> and prepare to find spectra. Implementations must throw
    /// (typically <see cref="BlibException"/>) on failure to open or recognise the file.
    /// </summary>
    /// <param name="path">Path to the spectrum file.</param>
    /// <param name="mzSort">When true, iteration via <see cref="GetNextSpectrum"/> is sorted by
    /// precursor m/z; otherwise spectra are returned in file order.</param>
    void OpenFile(string path, bool mzSort = false);

    /// <summary>
    /// Hack for accessing spectra by index using pwiz; some implementations need to know
    /// which identifier kind the caller intends to use before they can build an index.
    /// </summary>
    /// <remarks>cpp parity: SpecFileReader.h:49 <c>setIdType</c>. The cpp comment promises
    /// to eventually delete this in favour of the explicit <see cref="SpecIdType"/> arg on
    /// <see cref="GetSpectrum(int, SpecData, SpecIdType, bool)"/>.</remarks>
    SpecIdType IdType { set; }

    /// <summary>
    /// Find the spectrum matching the integer <paramref name="identifier"/> (scan number or
    /// zero-based index, controlled by <paramref name="findBy"/>) and populate
    /// <paramref name="returnData"/>.
    /// </summary>
    /// <returns>true if found and parsed; false if not in file.</returns>
    bool GetSpectrum(int identifier, SpecData returnData, SpecIdType findBy, bool getPeaks = true);

    /// <summary>
    /// Find the spectrum matching the string <paramref name="identifier"/> (native id / name)
    /// and populate <paramref name="returnData"/>.
    /// </summary>
    /// <returns>true if found and parsed; false if not in file.</returns>
    bool GetSpectrum(string identifier, SpecData returnData, bool getPeaks = true);

    /// <summary>
    /// Return the next spectrum in iteration order (file order, or m/z order if
    /// <see cref="OpenFile"/> was called with <c>mzSort=true</c>).
    /// </summary>
    /// <returns>true if a spectrum was returned; false if iteration is exhausted.</returns>
    bool GetNextSpectrum(SpecData returnData, bool getPeaks = true);
}

/// <summary>
/// Abstract base class for <see cref="ISpecFileReader"/> implementations. Provides the
/// PSM-dispatching <see cref="GetSpectrum(PSM, SpecIdType, SpecData, bool)"/> overload so
/// concrete readers only need to implement the three primitive overloads.
/// </summary>
/// <remarks>
/// cpp parity: mirrors the default-implemented <c>getSpectrum(PSM*, ...)</c> on
/// <c>BiblioSpec::SpecFileReader</c> (SpecFileReader.h:60-83).
/// </remarks>
public abstract class SpecFileReaderBase : ISpecFileReader
{
    /// <inheritdoc/>
    public abstract void OpenFile(string path, bool mzSort = false);

    /// <inheritdoc/>
    public abstract SpecIdType IdType { set; }

    /// <inheritdoc/>
    public abstract bool GetSpectrum(int identifier, SpecData returnData, SpecIdType findBy, bool getPeaks = true);

    /// <inheritdoc/>
    public abstract bool GetSpectrum(string identifier, SpecData returnData, bool getPeaks = true);

    /// <inheritdoc/>
    public abstract bool GetNextSpectrum(SpecData returnData, bool getPeaks = true);

    /// <summary>
    /// Look up the spectrum that matches the identifier carried on <paramref name="psm"/>,
    /// dispatching to the appropriate primitive overload based on <paramref name="findBy"/>.
    /// </summary>
    /// <remarks>
    /// cpp parity: SpecFileReader.h:60. Precursor-only PSMs force <paramref name="getPeaks"/>
    /// to false, and if the scan number is unset but the caller asked to find by scan number,
    /// fall back to name-based lookup (no actual spectrum is associated).
    /// </remarks>
    public virtual bool GetSpectrum(PSM psm, SpecIdType findBy, SpecData returnData, bool getPeaks)
    {
        ArgumentNullException.ThrowIfNull(psm);
        ArgumentNullException.ThrowIfNull(returnData);

        var isMs1 = psm.IsPrecursorOnly();
        if (isMs1)
        {
            getPeaks = false;
            if (psm.SpecKey == -1 && findBy == SpecIdType.ScanNumberId)
            {
                // cpp parity: SpecFileReader.h:71 — fall back to name-based lookup because
                // there's no actual MS2 spectrum.
                findBy = SpecIdType.NameId;
            }
        }

        return findBy switch
        {
            SpecIdType.NameId => GetSpectrum(psm.SpecName, returnData, getPeaks),
            SpecIdType.ScanNumberId => GetSpectrum(psm.SpecKey, returnData, findBy, getPeaks),
            SpecIdType.IndexId => GetSpectrum(psm.SpecIndex, returnData, findBy, getPeaks),
            _ => false,
        };
    }

    /// <summary>Releases unmanaged resources held by this reader.</summary>
    /// <remarks>cpp parity: SpecFileReader.h:125 virtual destructor.</remarks>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Override to release reader-specific resources.</summary>
    protected virtual void Dispose(bool disposing)
    {
        // No-op by default; subclasses override.
    }
}
