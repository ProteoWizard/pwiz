using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Mzml;

namespace Pwiz.Data.MsData.Readers;

/// <summary>Identifies and reads mzML files.</summary>
/// <remarks>Port of pwiz::msdata::Reader_mzML.</remarks>
public sealed class MzmlReaderAdapter : IReader
{
    /// <inheritdoc/>
    public string TypeName => "mzML";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_mzML_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".mzML", ".mzml", ".mzML.gz" };

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);

        // Sniff the content if we have it. mzML can be wrapped as indexedmzML or bare.
        if (head is not null)
        {
            if (head.Contains("<mzML", StringComparison.Ordinal) ||
                head.Contains("<indexedmzML", StringComparison.Ordinal))
                return CvType;
        }

        // Fall back to extension match.
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

        // Fast path: indexed mzML (which is what cpp's Serializer_mzML always emits) — parse
        // the <indexList> footer for byte offsets + the byte position of </spectrumList>,
        // then read the header eagerly up to <spectrumList> and BAIL — no XmlReader.Skip
        // over the 90k spectrum bodies. Resume parsing past </spectrumList> on a second
        // pass to pick up the optional <chromatogramList>. The lazy SpectrumList_Mzml
        // serves the spectra on demand from the file. Required for multi-gig mzMLs.
        var indexed = MzmlIndexFooter.TryRead(filename);
        if (indexed is not null)
        {
            var idx = indexed.Value;
            var lazyReader = new MzmlReader { LazyMode = true };
            using (var headerStream = File.OpenRead(filename))
            {
                var headerOnly = lazyReader.Read(headerStream);
                CopyInto(headerOnly, result);
            }
            using (var tailStream = File.OpenRead(filename))
            {
                tailStream.Position = idx.EndOfSpectrumListByteOffset;
                lazyReader.ResumeAfterSpectrumList(tailStream, result);
            }
            // Add the input file as a SourceFile + pwiz software + pwiz_Reader_conversion DP
            // BEFORE installing the lazy list so the list's defaultDataProcessingRef points to
            // the new DP. Cpp's DefaultReaderList::Reader_mzML::read calls fillInCommonMetadata
            // at the same spot (DefaultReaderList.cpp:168).
            var dpPwiz = MSDataFile.FillInCommonMetadata(filename, result);
            result.Run.SpectrumList = new SpectrumList_Mzml(filename, lazyReader,
                idx.SpectrumIds, idx.SpectrumOffsets, dpPwiz);
            return;
        }

        // Fallback: not an indexedmzML (or footer was malformed). Read eagerly.
        using var stream = File.OpenRead(filename);
        var parsed = new MzmlReader().Read(stream);
        CopyInto(parsed, result);
        MSDataFile.FillInCommonMetadata(filename, result);
    }

    internal static void CopyInto(MSData source, MSData dest)
    {
        dest.Accession = source.Accession;
        dest.Id = source.Id;
        dest.Version = source.Version;
        dest.CVs.AddRange(source.CVs);
        dest.FileDescription = source.FileDescription;
        dest.ParamGroups.AddRange(source.ParamGroups);
        dest.Samples.AddRange(source.Samples);
        dest.Software.AddRange(source.Software);
        dest.ScanSettings.AddRange(source.ScanSettings);
        dest.InstrumentConfigurations.AddRange(source.InstrumentConfigurations);
        dest.DataProcessings.AddRange(source.DataProcessings);
        dest.Run = source.Run;
    }
}
