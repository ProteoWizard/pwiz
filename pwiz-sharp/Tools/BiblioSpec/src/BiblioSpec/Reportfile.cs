// Port of pwiz_tools/BiblioSpec/src/Reportfile.{h,cpp}

using System.Globalization;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Writes BlibSearch's tab-delimited <c>.report</c> output: a comment header summarising the
/// search options, followed by one row per match (within rank threshold).
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::Reportfile</c>. cpp parity: Reportfile.cpp:36-46 constructor
/// translates <c>report-matches == -1</c> to <see cref="int.MaxValue"/>; we do the same.</para>
/// <para>The column layout is reproduced verbatim from Reportfile.cpp:74-101 so existing
/// golden-file tests still match.</para>
/// </remarks>
public sealed class Reportfile : IDisposable
{
    private StreamWriter? _file;
    private readonly int _topMatches;
    private readonly string _optionsString;
    private bool _disposed;

    /// <summary>
    /// Construct a writer with the rank cap and pre-built options header string.
    /// </summary>
    /// <param name="topMatches">Rank cap. Use <c>-1</c> to print every match (cpp parity).</param>
    /// <param name="optionsString">Multi-line comment header to write before the column titles.</param>
    /// <remarks>cpp Reportfile.cpp:35-45. The options-header string is built in cpp by walking
    /// the variables_map; in C# we pass it pre-formatted from <see cref="BlibSearch"/>.</remarks>
    public Reportfile(int topMatches, string optionsString)
    {
        _topMatches = topMatches == -1 ? int.MaxValue : topMatches;
        _optionsString = optionsString ?? string.Empty;
    }

    /// <summary>cpp <c>open</c> at Reportfile.cpp:50 — open file + write header.</summary>
    public void Open(string filename)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);

        try
        {
            _file = new StreamWriter(filename) { AutoFlush = false };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Verbosity.Error($"Could not open report file {filename}.");
        }
        WriteHeader();
    }

    /// <summary>True once <see cref="Open"/> has succeeded.</summary>
    public bool IsOpen => _file is not null;

    /// <summary>
    /// cpp <c>writeHeader</c> at Reportfile.cpp:68 — print the options block, then the column
    /// title row.
    /// </summary>
    private void WriteHeader()
    {
        if (_file is null) return;

        _file.WriteLine(_optionsString);

        // cpp Reportfile.cpp:74-101 — verbatim column titles.
        const string header =
            "Query\t" +
            "LibId\t" +
            "LibSpec\t" +
            "rank\t" +
            "dotp\t" +
            "query-mz\t" +
            "query-z\t" +
            "lib-mz\t" +
            "lib-z\t" +
            "copies\t" +
            "candidates\t" +
            "sequence\t" +
            "TIC-raw\t" +
            "bp-mz-raw\t" +
            "bp-raw\t" +
            "lbp-mz-raw\t" +
            "num-peaks\t" +
            "matched-ions\t" +
            "query-rt\t" +
            "lib-rt\t" +
            "lib-molecule-name\t" +
            "lib-chemical-formula\t" +
            "lib-adduct\t" +
            "lib-inchikey\t" +
            "lib-otherkeys\t" +
            "query-ion-mobility\t" +
            "query-ion-mobility-type";
        _file.WriteLine(header);
    }

    /// <summary>
    /// cpp <c>writeMatches</c> at Reportfile.cpp:171 — write every match whose rank
    /// &lt;= <see cref="_topMatches"/> in the order given (caller has already sorted descending
    /// by dot product).
    /// </summary>
    /// <remarks>
    /// <para>cpp parity: ofstream precision is set to 6 (Reportfile.cpp:183). C# default
    /// <see cref="double.ToString()"/> with no format spec already uses ~15 digits, so we use
    /// <c>"G6"</c> to match cpp's precision in the same spots cpp does — for dotp and most
    /// computed floats. Plain values (m/z, retention time) get default formatting.</para>
    /// <para>cpp prints the possible-charge list as comma-separated when more than one;
    /// preserved exactly via the loop at Reportfile.cpp:201-211.</para>
    /// </remarks>
    public void WriteMatches(IReadOnlyList<Match> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (_file is null) return;

        var numCandidates = results.Count;

        foreach (var match in results)
        {
            if (match.Rank > _topMatches) break;
            // Replace null-forgiving (!) dereferences with explicit guards: a Match with an
            // unset query or reference spectrum is a programmer error, but we'd rather report
            // it with context than throw NullReferenceException mid-write.
            if (match.ExpSpec is null || match.RefSpec is null)
                throw new BlibException(false,
                    $"Match at rank {match.Rank} is missing "
                    + (match.ExpSpec is null ? "the query spectrum" : "the reference spectrum")
                    + "; cannot write a report row.");
            var querySpec = match.ExpSpec;
            var refSpec = match.RefSpec;

            // cpp Reportfile.cpp:183 — precision(6) applies to all subsequent prints.
            // We use G6 (matches cpp ofstream default formatting under precision(6)).
            var inv = CultureInfo.InvariantCulture;
            _file.Write(querySpec.ScanNumber.ToString(inv));
            _file.Write('\t');
            _file.Write(refSpec.LibId.ToString(inv));
            _file.Write('\t');
            _file.Write(refSpec.LibSpecId.ToString(inv));
            _file.Write('\t');
            _file.Write(match.Rank.ToString(inv));
            _file.Write('\t');
            _file.Write(match.GetScore(MatchScoreType.Dotp).ToString("G6", inv));
            _file.Write('\t');
            _file.Write(querySpec.Mz.ToString("G6", inv));
            _file.Write('\t');

            // cpp Reportfile.cpp:201-211 — print first charge with no comma, then ",N" repeated.
            var charges = querySpec.PossibleCharges;
            if (charges.Count == 0)
            {
                _file.Write('0');
            }
            else
            {
                _file.Write(charges[0].ToString(inv));
                for (var k = 1; k < charges.Count; k++)
                {
                    _file.Write(',');
                    _file.Write(charges[k].ToString(inv));
                }
            }
            _file.Write('\t');
            _file.Write(refSpec.Mz.ToString("G6", inv));
            _file.Write('\t');
            _file.Write(refSpec.Charge.ToString(inv));
            _file.Write('\t');
            _file.Write(refSpec.Copies.ToString(inv));
            _file.Write('\t');
            _file.Write(numCandidates.ToString(inv));
            _file.Write('\t');
            _file.Write(refSpec.ModifiedSequence);
            _file.Write('\t');

            // tic, base peak, num peaks (cpp Reportfile.cpp:219-223).
            _file.Write(querySpec.TotalIonCurrentRaw.ToString("G6", inv));
            _file.Write('\t');
            _file.Write(querySpec.BasePeakMzRaw.ToString("G6", inv));
            _file.Write('\t');
            _file.Write(querySpec.BasePeakIntensityRaw.ToString("G6", inv));
            _file.Write('\t');
            _file.Write(refSpec.BasePeakMzRaw.ToString("G6", inv));
            _file.Write('\t');
            _file.Write(querySpec.NumRawPeaks.ToString(inv));
            _file.Write('\t');

            _file.Write(((int)match.GetScore(MatchScoreType.MatchedIons)).ToString(inv));
            _file.Write('\t');
            _file.Write(querySpec.RetentionTime.ToString("G6", inv));
            _file.Write('\t');
            _file.Write(refSpec.RetentionTime.ToString("G6", inv));
            _file.Write('\t');
            _file.Write(refSpec.MoleculeName);
            _file.Write('\t');
            _file.Write(refSpec.ChemicalFormula);
            _file.Write('\t');
            _file.Write(refSpec.Adduct);
            _file.Write('\t');
            _file.Write(refSpec.InchiKey);
            _file.Write('\t');
            _file.Write(refSpec.OtherKeys);
            _file.Write('\t');
            _file.Write(querySpec.IonMobility.ToString("G6", inv));
            _file.Write('\t');
            _file.Write(((int)querySpec.IonMobilityType).ToString(inv));
            _file.WriteLine();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _file?.Flush();
        _file?.Dispose();
        _file = null;
    }
}
