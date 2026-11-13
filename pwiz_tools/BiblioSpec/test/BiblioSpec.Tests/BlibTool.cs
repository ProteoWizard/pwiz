namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// The four BiblioSpec CLI tools, each driven by the test harness in the same
/// shape as the cpp <c>Jamfile.jam</c> golden-output tests.
///
/// <para>The values map onto the planned pwiz-sharp tool projects under
/// <c>src/BiblioSpec.Tools.&lt;Name&gt;/</c> (each producing a single-file exe under
/// <c>bin/&lt;config&gt;/net8.0/</c>) and onto the C++ exe names used by the legacy
/// Jamfile. See <see cref="ExecuteBlib"/> for the path resolution.</para>
/// </summary>
public enum BlibTool
{
    /// <summary>
    /// Builds a <c>.blib</c> SQLite spectral library from one or more search-result
    /// inputs (sqt, ssl, pepXML, mzid, pdResult, etc.).
    /// </summary>
    BlibBuild,

    /// <summary>
    /// Filters a redundant <c>.blib</c> into a non-redundant <c>.blib</c>, keeping the
    /// highest-scoring spectrum per peptide modification state.
    /// </summary>
    BlibFilter,

    /// <summary>
    /// Spectral-library search: matches an ms2/mzML query file against a <c>.blib</c>
    /// and writes a <c>.report</c> with hits.
    /// </summary>
    BlibSearch,

    /// <summary>
    /// Exports a <c>.blib</c> to a text <c>.ms2</c> / <c>.lms2</c>.
    /// </summary>
    BlibToMs2,
}
