// Port of pwiz_tools/BiblioSpec/src/LibReader.{h,cpp}

using System.Data.SQLite;
using System.Globalization;
using pwiz.BiblioSpec.IO;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Reads a BiblioSpec <c>.blib</c> for BlibSearch and BlibToMs2 as <see cref="RefSpectrum"/>
/// rows. The shared <see cref="BlibReader"/> reads the file; this class is the port's view of
/// its rows, keeping what the cpp <c>LibReader</c> did where the tools' output shows it.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::LibReader</c> (cpp LibReader.h / LibReader.cpp). The SQL and the
/// peak blob decoding live in <see cref="BlibReader"/>; cpp behavior preserved here:</para>
/// <list type="bullet">
///   <item>Only spectra with a <c>RefSpectraPeaks</c> row are returned: every cpp query joins
///         the two tables (<c>WHERE id = RefSpectraID</c>), so a precursor-only entry is unseen,
///         and a library without either table fails at open, where cpp's prepare failed.</item>
///   <item><c>prevAA</c> / <c>nextAA</c>: <see cref="GetSpecInMzRange"/> leaves them empty
///         (cpp LibReader.cpp:156-183 reads neither column); every other entry point sets
///         <c>"-"</c> whatever the columns hold (LibReader.cpp:449-450, 669-670, 722-723).</item>
///   <item>A NULL <c>retentionTime</c> reads as 0 (cpp's <c>sqlite3_column_double</c>).</item>
///   <item>When <c>modPrecision &gt;= 0</c>, <see cref="GetRefSpec"/> rebuilds the modified
///         sequence from the Modifications table, inserting <c>[+15.99]</c>-style brackets with
///         that many decimals (LibReader.cpp:472-502).</item>
/// </list>
/// </remarks>
public sealed class LibReader : IDisposable
{
    /// <summary>What cpp sets a flanking residue to where it does not read the column.</summary>
    private const string FLANKING_UNREAD = "-";

    private readonly string _libraryName;
    private readonly int _modPrecision;
    private readonly SQLiteConnection _connection;
    private readonly BlibReader _reader;
    private IEnumerator<BlibSpectrumRecord>? _enumSpectra;

    /// <summary>
    /// Open <paramref name="libName"/> read-only.
    /// </summary>
    /// <param name="libName">Path to the <c>.blib</c> file.</param>
    /// <param name="modPrecision">When &gt;= 0, <see cref="GetRefSpec"/> rebuilds the
    /// modified-peptide-sequence string from the Modifications table with this many decimals
    /// per mod-mass; when -1 (the cpp default), the raw <c>peptideModSeq</c> column is used as-is.</param>
    /// <remarks>cpp LibReader.cpp:44-96. Read-only matches the cpp contract: LibReader never
    /// writes. The connection is the port's own (<see cref="SqliteRoutine.Open"/>, unpooled, so
    /// a test can delete the library after disposing the reader); the shared reader reads on it.</remarks>
    public LibReader(string libName, int modPrecision = -1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libName);
        _libraryName = libName;
        _modPrecision = modPrecision;
        try
        {
            _connection = SqliteRoutine.Open(libName, readOnly: true);
        }
        catch (Exception ex)
        {
            Verbosity.Error($"Could not open database {libName}. ({ex.Message})");
            throw; // Unreachable - Verbosity.Error throws.
        }

        try
        {
            _reader = new BlibReader(_connection);
            // cpp LibReader.cpp:79-96 prepared the enumeration over both tables at open, so a
            // library missing either failed there, not at the first read.
            if (!_reader.HasTable(BlibReader.TABLE_REF_SPECTRA) || !_reader.HasTable(BlibReader.TABLE_REF_SPECTRA_PEAKS))
            {
                Verbosity.Error(
                    $"LibReader::initialize cannot prepare SQL statement for enumerating spectra " +
                    $"from {libName} (no RefSpectra or RefSpectraPeaks table)");
            }
        }
        catch
        {
            _connection.Dispose();
            throw;
        }
    }

    // ---- setters / getters (cpp LibReader.cpp:269-317) --------------------

    /// <summary>Experimental spectral low-end precursor m/z filter (cpp <c>expLowMZ_</c>).</summary>
    public double LowMz { get; set; }

    /// <summary>Experimental spectral high-end precursor m/z filter (cpp <c>expHighMZ_</c>).</summary>
    public double HighMz { get; set; }

    /// <summary>Experimental charge if determined (cpp <c>expPreChg_</c>, default -1).</summary>
    public int Charge { get; set; } = -1;

    /// <summary>Experimental charge range low end if not determined (cpp <c>expLowChg_</c>, default -1).</summary>
    public int LowCharge { get; set; } = -1;

    /// <summary>Experimental charge range high end if not determined (cpp <c>expHighChg_</c>, default -1).</summary>
    public int HighCharge { get; set; } = -1;

    /// <summary>Count of rows in the <c>RefSpectra</c> table (cpp LibReader.cpp:319-335).</summary>
    public int CountAllSpec()
    {
        return _reader.CountSpectra();
    }

    // ---- spectrum fetch ---------------------------------------------------

    /// <summary>
    /// Select all RefSpectra with precursor m/z in <c>[minMz, maxMz]</c> and at least
    /// <c>minPeaks + 1</c> peaks (cpp uses <c>numPeaks &gt; minPeaks</c>, not <c>&gt;=</c>).
    /// </summary>
    /// <remarks>cpp LibReader.cpp:129-188, the vector overload BlibSearch calls (inclusive low
    /// bound). Its 10-column SELECT reads neither flanking residue, so they stay empty.</remarks>
    public IList<RefSpectrum> GetSpecInMzRange(double minMz, double maxMz, int minPeaks)
    {
        var filter = PeaksRowFilter();
        filter.MinPrecursorMz = minMz;
        filter.MaxPrecursorMz = maxMz;
        filter.MinNumPeaks = minPeaks + 1;
        return ReadSpectra(filter, null);
    }

    /// <summary>
    /// Look up a spectrum by id. Returns <c>null</c> if no row matches (cpp's
    /// <c>bool getRefSpec(int, RefSpectrum&amp;)</c> overload, LibReader.cpp:422-505).
    /// </summary>
    /// <remarks>
    /// <para>cpp LibReader.cpp:444-450 - populates <c>prevAA</c> / <c>nextAA</c> with
    /// <c>"-"</c> rather than the column values, even though the SELECT pulls them. Preserved.</para>
    /// <para>When <see cref="_modPrecision"/> &gt;= 0 (set via constructor), the modified
    /// sequence is rebuilt from the Modifications table by inserting <c>[+15.99]</c>-style
    /// brackets at each position with that many decimals. cpp LibReader.cpp:472-502.</para>
    /// </remarks>
    public RefSpectrum? GetRefSpec(int libID)
    {
        var record = _reader.ReadSpectrum(libID, true);
        // A spectrum without a peaks row is one cpp's inner join did not find.
        if (record == null || record.MzBlob == null)
        {
            Verbosity.Debug($"LibReader::getRefSpec cannot find spectrum {libID} in {_libraryName}.");
            return null;
        }

        var spec = ToRefSpectrum(record, FLANKING_UNREAD);
        if (_modPrecision >= 0)
        {
            ApplyModificationsTable(spec, libID);
        }

        return spec;
    }

    /// <summary>
    /// Select all spectra whose precursor m/z and charge match the configured experimental
    /// filters (<see cref="LowMz"/>, <see cref="HighMz"/>, <see cref="Charge"/> /
    /// <see cref="LowCharge"/> / <see cref="HighCharge"/>).
    /// </summary>
    /// <remarks>
    /// <para>cpp LibReader.cpp:623-697: with <see cref="Charge"/> set (not cpp's -1 sentinel),
    /// <c>precursorCharge = Charge</c>; otherwise <c>precursorCharge</c> between
    /// <see cref="LowCharge"/> and <see cref="HighCharge"/>.</para>
    /// <para>cpp also hard-codes <c>prevAA</c>/<c>nextAA</c> to <c>"-"</c> (LibReader.cpp:669-670),
    /// despite the SELECT pulling them. Preserved.</para>
    /// </remarks>
    public IList<RefSpectrum> GetAllRefSpec()
    {
        var filter = PeaksRowFilter();
        filter.MinPrecursorMz = LowMz;
        filter.MaxPrecursorMz = HighMz;
        if (Charge != -1)
        {
            filter.MinPrecursorCharge = Charge;
            filter.MaxPrecursorCharge = Charge;
        }
        else
        {
            filter.MinPrecursorCharge = LowCharge;
            filter.MaxPrecursorCharge = HighCharge;
        }
        return ReadSpectra(filter, FLANKING_UNREAD);
    }

    /// <summary>
    /// Stateful iterator over every spectrum in the library (ordered by id). Returns
    /// <c>true</c> if a spectrum was hydrated, <c>false</c> after the last spectrum.
    /// </summary>
    /// <remarks>
    /// <para>cpp LibReader.cpp:705-738.</para>
    /// <para>cpp behavior quirk: even when <c>sqlite3_step</c> returns <c>SQLITE_DONE</c>, cpp
    /// still calls all the <c>sqlite3_column_*</c> accessors on the now-empty row (which return
    /// 0 / null) and assigns them into <paramref name="spec"/> (LibReader.cpp:717-734). The
    /// observable effect is that on the final call the <paramref name="spec"/> is reset to an
    /// empty-sequence spectrum without peaks and the return value is <c>false</c>. Preserved.</para>
    /// </remarks>
    public bool GetNextSpectrum(out RefSpectrum spec)
    {
        _enumSpectra ??= _reader.ReadSpectra(true, PeaksRowFilter()).GetEnumerator();
        if (!_enumSpectra.MoveNext())
        {
            Verbosity.Debug("Returned the last spec from the library.");
            // cpp clobbers spec with the zero-row state. Match that.
            spec = new RefSpectrum();
            return false;
        }

        spec = ToRefSpectrum(_enumSpectra.Current, FLANKING_UNREAD);
        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _enumSpectra?.Dispose();
        _enumSpectra = null;
        // The shared reader does not own the connection, which is disposed here.
        _reader.Dispose();
        _connection.Dispose();
    }

    // ---- helpers ----------------------------------------------------------

    /// <summary>A filter admitting only spectra with a peaks row: cpp's inner join on the two tables.</summary>
    private static BlibSpectrumFilter PeaksRowFilter()
    {
        return new BlibSpectrumFilter { RequirePeaksRow = true };
    }

    /// <summary>The spectra <paramref name="filter"/> admits, as <see cref="RefSpectrum"/>s.</summary>
    private List<RefSpectrum> ReadSpectra(BlibSpectrumFilter filter, string? flanking)
    {
        var spectra = new List<RefSpectrum>();
        foreach (var record in _reader.ReadSpectra(true, filter))
            spectra.Add(ToRefSpectrum(record, flanking));
        return spectra;
    }

    /// <summary>
    /// The port's <see cref="RefSpectrum"/> of a row read with its peaks; the flanking residues
    /// set to <paramref name="flanking"/>, or left empty when it is null.
    /// </summary>
    private RefSpectrum ToRefSpectrum(BlibSpectrumRecord record, string? flanking)
    {
        var spec = new RefSpectrum
        {
            LibSpecId = checked((int)record.Id),
            Sequence = record.PeptideSeq ?? string.Empty,
            Mz = record.PrecursorMz,
            Charge = record.PrecursorCharge,
            ModifiedSequence = record.PeptideModSeq ?? string.Empty,
            Copies = record.Copies,
            RetentionTime = record.RetentionTime ?? 0,
        };
        if (flanking != null)
        {
            spec.PrevAa = flanking;
            spec.NextAa = flanking;
        }
        spec.SetRawPeaks(ToPeaks(record));
        return spec;
    }

    /// <summary>
    /// The peaks of a row, decoded by the shared reader (raw when the blob is the expected
    /// length, else zlib, as cpp <c>getUncompressedPeaks</c> decided), a bad blob reported with
    /// the spectrum and the library. A spectrum of no peaks has none, whatever its blobs hold
    /// (cpp LibReader.cpp:507-553).
    /// </summary>
    private PeakT[] ToPeaks(BlibSpectrumRecord record)
    {
        if (record.NumPeaks <= 0)
            return Array.Empty<PeakT>();
        BlibPeaks? peaks;
        try
        {
            peaks = BlibReader.DecodePeaks(record);
        }
        catch (InvalidDataException ex)
        {
            throw new BlibException($"LibReader: spectrum {record.Id} in {_libraryName}: {ex.Message}", ex);
        }
        if (peaks == null)
        {
            throw new BlibException(false,
                $"LibReader: spectrum {record.Id} in {_libraryName} has no peak blobs for its {record.NumPeaks} peaks.");
        }
        var result = new PeakT[peaks.Count];
        for (var i = 0; i < result.Length; i++)
            result[i] = new PeakT(peaks.Mzs[i], peaks.Intensities[i]);
        return result;
    }

    /// <summary>
    /// Rebuild <see cref="RefSpectrum.ModifiedSequence"/> from the <c>Modifications</c> table.
    /// </summary>
    /// <remarks>
    /// cpp LibReader.cpp:472-502. The cpp pipeline:
    /// <list type="number">
    ///   <item>Query <c>SELECT position, mass FROM Modifications WHERE RefSpectraId = libID</c>.</item>
    ///   <item>For each row, clamp position to <c>seq.length()</c> and sum masses per position
    ///         into a <c>map&lt;int, double&gt;</c>.</item>
    ///   <item>Iterate the map in <em>reverse</em> order (rbegin to rend) and insert
    ///         <c>"[+15.99]"</c>-style brackets at each position; reverse order keeps earlier
    ///         positions stable as later inserts shift the string.</item>
    /// </list>
    /// </remarks>
    private void ApplyModificationsTable(RefSpectrum spec, int libID)
    {
        // cpp uses std::map<int, double> for sort+sum; SortedDictionary mirrors the order.
        var mods = new SortedDictionary<int, double>();
        var seq = spec.Sequence;
        foreach (var modification in _reader.ReadModifications(libID))
        {
            // cpp LibReader.cpp:484-486 - clamp to sequence length.
            var position = Math.Min(modification.Position, seq.Length);
            mods[position] = mods.TryGetValue(position, out var existing) ? existing + modification.Mass : modification.Mass;
        }

        // cpp LibReader.cpp:497-500 - iterate in reverse so earlier insertions don't shift
        // the indices of later ones. std::map<>::rbegin gives largest key first.
        var builder = new System.Text.StringBuilder(seq);
        var precisionFmt = "F" + _modPrecision.ToString(CultureInfo.InvariantCulture);
        foreach (var kv in mods.Reverse())
        {
            // cpp LibReader.cpp:498 - "[%s%.*f]" with sign='+' when mass >= 0.
            var sign = kv.Value >= 0 ? "+" : string.Empty;
            builder.Insert(kv.Key, string.Concat("[", sign, kv.Value.ToString(precisionFmt, CultureInfo.InvariantCulture), "]"));
        }

        spec.ModifiedSequence = builder.ToString();
    }
}
