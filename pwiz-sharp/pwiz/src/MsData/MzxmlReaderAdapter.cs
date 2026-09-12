using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.MzXml;

namespace Pwiz.Data.MsData.Readers;

/// <summary>Identifies and reads mzXML files.</summary>
/// <remarks>Port of pwiz::msdata::Reader_mzXML.</remarks>
public sealed class MzxmlReaderAdapter : IReader
{
    /// <inheritdoc/>
    public string TypeName => "mzXML";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_ISB_mzXML_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".mzXML", ".mzxml", ".mz.xml" };

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);

        if (head is not null && head.Contains("<mzXML", StringComparison.Ordinal))
            return CvType;

        foreach (var ext in FileExtensions)
            if (filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return CvType;

        return CVID.CVID_Unknown;
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);

        // Fast path: indexed mzXML with an <indexOffset> footer (the format MzxmlWriter
        // and TPP/ReAdW both emit). Parse the <index> table for per-scan byte offsets,
        // read the header eagerly, halt at the first <scan>, and install a lazy
        // SpectrumList_Mzxml that serves spectra on demand by seeking to the recorded
        // offsets. Required for multi-GB DDA mzXMLs where eager parsing would exhaust
        // the heap.
        var indexed = MzxmlIndexFooter.TryRead(filename);
        if (indexed is not null)
        {
            var idx = indexed.Value;
            var lazyReader = new MzxmlReader { LazyMode = true };
            using (var headerStream = File.OpenRead(filename))
            {
                lazyReader.ReadInternal(headerStream, result);
            }
            // Add input file as SourceFile + pwiz software + pwiz_Reader_conversion DP before
            // installing the lazy list (cpp parity — DefaultReaderList.cpp:233/247).
            var dpPwiz = MSDataFile.FillInCommonMetadata(filename, result);
            result.Run.SpectrumList = new SpectrumList_Mzxml(filename, lazyReader,
                idx.ScanIds, idx.ScanOffsets, dpPwiz);
            return;
        }

        // Fallback: not indexed or footer malformed. Read eagerly.
        using var stream = File.OpenRead(filename);
        MzxmlReader.Read(stream, result);
        MSDataFile.FillInCommonMetadata(filename, result);
    }
}
