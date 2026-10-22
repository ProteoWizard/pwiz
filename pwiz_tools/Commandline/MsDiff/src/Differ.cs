using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Diff;
using Pwiz.Data.MsData.Readers;
using Pwiz.Vendor.Bruker;
using Pwiz.Vendor.Thermo;
using Pwiz.Vendor.Waters;

namespace Pwiz.Tools.MsDiff;

/// <summary>Reads two data files and reports how they differ.</summary>
internal sealed class Differ
{
    private readonly ReaderList _readers;

    public Differ()
    {
        // The same list msconvert builds, so msdiff can read anything msconvert can write from.
        _readers = ThermoReaderRegistration.CreateDefaultWithThermo();
        _readers.Add(new Reader_Bruker());
        _readers.Add(new Reader_Waters());
        _readers.Add(new Pwiz.Vendor.Agilent.Reader_Agilent());
        _readers.Add(new Pwiz.Vendor.Sciex.Reader_Sciex());
        _readers.Add(new Pwiz.Vendor.Shimadzu.Reader_Shimadzu());
        _readers.Add(new Pwiz.Vendor.UNIFI.Reader_UNIFI());
        _readers.Add(new Pwiz.Vendor.UIMF.Reader_UIMF());
        _readers.Add(new Pwiz.Vendor.Mobilion.Reader_Mobilion());
    }

    /// <summary>
    /// Compares the two files named in <paramref name="config"/>, writing the report to
    /// <paramref name="output"/>. Returns 1 when they differ and 0 when they do not, matching
    /// cpp msdiff's <c>return diff;</c>.
    /// </summary>
    public int Run(Config config, TextWriter output)
    {
        // Disposed: a vendor- or mzML-backed MSData holds its source file open, and a caller
        // that diffs then replaces one of the inputs would otherwise hit a sharing violation.
        using var a = Read(config.FileA);
        using var b = Read(config.FileB);

        var result = MSDataDiff.Compare(a, b, config.DiffConfig);

        // The two summary lines come first and are always written, even when nothing differs.
        //
        // cpp reaches the same wording by printing the diff object through TextWriter, which
        // renders each list as "<name> (N <units>)" holding only the entries that differ. It
        // prints nothing at all when the files match, because the whole report is guarded by
        // `if (diff)`. Callers that grep for "0 spectra" therefore see a FAILURE for two
        // identical files - the one case where matching cpp byte for byte would be worse than
        // being correct, so these lines are unconditional here.
        output.WriteLine(Summary("spectrumList", result.SpectraDiffering, "spectra"));
        output.WriteLine(Summary("chromatogramList", result.ChromatogramsDiffering, "chromatograms"));

        if (!result.Differs) return 0;

        output.Write(result.Report);
        return 1;
    }

    /// <summary>
    /// Renders one list's summary line: <c>spectrumList (0 spectra)</c> when nothing differs,
    /// <c>spectrumList (12 differing spectra)</c> when something does.
    /// </summary>
    /// <remarks>
    /// The extra word in the non-zero form is deliberate, and it is the one place this port
    /// does not simply reproduce cpp's wording. Consumers test for equality by grepping for
    /// the unanchored string "0 spectra" - the container's vendor sweep does exactly that -
    /// and cpp's uniform "(N spectra)" makes that grep match the tail of any count ending in
    /// zero. A Mobilion fixture differing in all 19570 spectra prints "(19570 spectra)",
    /// which contains "0 spectra", so the sweep would report a PASS for a file that matched
    /// nothing. Putting a word between the digits and the unit means only a real zero can
    /// match. The right long-term fix is to anchor the grep on "(0 spectra)"; until every
    /// consumer does, emitting a string that cannot be misread is the safer half of the deal.
    /// </remarks>
    private static string Summary(string listName, int differing, string unit) =>
        differing == 0
            ? $"{listName} (0 {unit})"
            : $"{listName} ({differing} differing {unit})";

    private MSData Read(string filename)
    {
        if (!File.Exists(filename) && !Directory.Exists(filename))
            throw new FileNotFoundException($"no such file or directory: {filename}", filename);

        var msd = new MSData();
        _readers.Read(filename, msd, new ReaderConfig());
        return msd;
    }
}
