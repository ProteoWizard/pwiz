using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.MzPeak;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Samples;
using Pwiz.Data.MsData.Sources;
// MzPeak's file-level metadata records share a lot of names with pwiz's
// MSData model (SourceFile, InstrumentConfiguration, FileDescription, …).
// Alias the mzPeak side so the translation methods can mention both.
using MzPeakFileMetadata = Pwiz.Data.MsData.MzPeak.FileMetadata;
using MzPeakSourceFile = Pwiz.Data.MsData.MzPeak.SourceFile;
using MzPeakInstrumentConfiguration = Pwiz.Data.MsData.MzPeak.InstrumentConfiguration;

namespace Pwiz.Data.MsData.Readers;

/// <summary>Identifies and reads mzPeak files (mass-spec data packed as Parquet tables inside a ZIP container).</summary>
/// <remarks>
/// A mzPeak file is a ZIP archive holding <c>spectra_metadata.parquet</c>,
/// <c>spectra_data.parquet</c>, optional <c>spectra_peaks.parquet</c>, and the
/// chromatogram parquets, plus a <c>mzpeak_index.json</c> manifest. We
/// recognize it by extension first; without an extension hit, fall back to ZIP
/// magic + presence of the manifest entry.
/// </remarks>
public sealed class MzPeakReaderAdapter : IReader
{
    /// <inheritdoc/>
    public string TypeName => "mzPeak";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_mzPeak_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".mzpeak", ".mzPeak" };

    // ZIP local-file-header magic. Every ZIP archive starts with these 4 bytes
    // (PK\3\4). Not every ZIP is mzPeak though — the secondary check is the
    // presence of the mzpeak_index.json manifest entry.
    private static readonly byte[] ZipMagic = { 0x50, 0x4B, 0x03, 0x04 };

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);

        foreach (var ext in FileExtensions)
            if (filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return CvType;

        if (head is null || head.Length < ZipMagic.Length) return CVID.CVID_Unknown;
        for (int i = 0; i < ZipMagic.Length; i++)
            if ((byte)head[i] != ZipMagic[i]) return CVID.CVID_Unknown;

        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(filename);
            if (archive.GetEntry("mzpeak_index.json") != null) return CvType;
        }
        catch
        {
            // Malformed ZIP or no permission to open — fall through to unknown.
        }
        return CVID.CVID_Unknown;
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);

        // Open the reader eagerly — it extracts the archive to a scratch dir
        // and loads every metadata column into memory at construction. The
        // SpectrumList / ChromatogramList we install below share ownership of
        // the reader; the first list disposed cleans up the scratch dir, and
        // MzPeakReader.Dispose is a no-op on a second call.
        var reader = new MzPeakReader(filename);
        try
        {
            var fm = reader.FileMetadata;

            if (!string.IsNullOrEmpty(fm.DocumentId)) result.Id = fm.DocumentId!;

            TranslateFileDescription(fm, result);
            // Software first: instrument configurations and dataProcessing methods reference it by id.
            TranslateSoftware(fm, result);
            TranslateParamGroups(fm, result);
            TranslateInstrumentConfigurations(fm, result);
            TranslateDataProcessings(fm, result);
            TranslateSamples(fm, result);
            TranslateRun(fm, result);

            var dp = MSDataFile.FillInCommonMetadata(filename, result);

            // Ownership: spectrum list owns the reader, chromatogram list rides
            // along. If there's no chromatogram list (count == 0) we still
            // install one because callers iterate Run.ChromatogramList.Count.
            result.Run.SpectrumList = new SpectrumList_MzPeak(reader, dp, ownsReader: true, result.ParamGroups);
            result.Run.ChromatogramList = new ChromatogramList_MzPeak(reader, dp, ownsReader: false);
            reader = null!;  // ownership transferred
        }
        finally
        {
            reader?.Dispose();
        }
    }

    // ===== File-metadata translation =====

    private static void TranslateFileDescription(MzPeakFileMetadata fm, MSData result)
    {
        // FileContent: bag of CV params describing the kinds of spectra inside.
        ApplyCvParams(result.FileDescription.FileContent, fm.FileDescription.Contents);

        foreach (MzPeakSourceFile sf in fm.FileDescription.SourceFiles)
        {
            var pwizSf = new Sources.SourceFile(
                id: sf.Id ?? string.Empty,
                name: sf.Name ?? string.Empty,
                location: sf.Location ?? string.Empty);
            ApplyCvParams(pwizSf, sf.Parameters);
            result.FileDescription.SourceFiles.Add(pwizSf);
        }
    }

    private static void TranslateParamGroups(MzPeakFileMetadata fm, MSData result)
    {
        if (fm.ParamGroups is null) return;
        foreach (var pg in fm.ParamGroups)
        {
            var pwizPg = new Pwiz.Data.Common.Params.ParamGroup(pg.Id ?? string.Empty);
            ApplyCvParams(pwizPg, pg.Parameters);
            result.ParamGroups.Add(pwizPg);
        }
    }

    private static void TranslateInstrumentConfigurations(MzPeakFileMetadata fm, MSData result)
    {
        foreach (MzPeakInstrumentConfiguration ic in fm.InstrumentConfigurations)
        {
            // Restore pwiz's real string id when we stashed it; otherwise fall back to the
            // cross-stack integer index (files written by other stacks only carry the int).
            string id = string.IsNullOrEmpty(ic.OriginalId)
                ? ic.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : ic.OriginalId!;
            var pwizIc = new Instruments.InstrumentConfiguration(id);
            ApplyCvParams(pwizIc, ic.Parameters);

            foreach (var comp in ic.Components ?? System.Linq.Enumerable.Empty<ComponentInfo>())
            {
                var pwizComp = new Instruments.Component(ComponentTypeFromString(comp.Type), comp.Order);
                ApplyCvParams(pwizComp, comp.Parameters);
                pwizIc.ComponentList.Add(pwizComp);
            }

            if (!string.IsNullOrEmpty(ic.SoftwareReference))
                pwizIc.Software = result.Software.FirstOrDefault(s => s.Id == ic.SoftwareReference);

            if (ic.ParamGroupRefs is not null)
                foreach (var refId in ic.ParamGroupRefs)
                {
                    var pg = result.ParamGroups.FirstOrDefault(g => g.Id == refId);
                    if (pg is null) continue;
                    pwizIc.Params.ParamGroups.Add(pg);
                    // Drop any direct param a referenced group already provides (avoid duplication).
                    var provided = pg.CVParams.Select(cv => cv.Cvid).ToHashSet();
                    pwizIc.CVParams.RemoveAll(cv => provided.Contains(cv.Cvid));
                }

            result.InstrumentConfigurations.Add(pwizIc);
        }
    }

    private static void TranslateDataProcessings(MzPeakFileMetadata fm, MSData result)
    {
        foreach (var dp in fm.DataProcessingMethods)
        {
            var pwizDp = new Processing.DataProcessing(dp.Id ?? string.Empty);
            foreach (var m in dp.Methods ?? System.Linq.Enumerable.Empty<ProcessingMethodInfo>())
            {
                var pwizPm = new Processing.ProcessingMethod { Order = m.Order };
                if (!string.IsNullOrEmpty(m.SoftwareReference))
                    pwizPm.Software = result.Software.FirstOrDefault(s => s.Id == m.SoftwareReference);
                ApplyCvParams(pwizPm, m.Parameters);
                pwizDp.ProcessingMethods.Add(pwizPm);
            }
            result.DataProcessings.Add(pwizDp);
        }
    }

    private static Instruments.ComponentType ComponentTypeFromString(string? type) => type switch
    {
        "source" => Instruments.ComponentType.Source,
        "analyzer" => Instruments.ComponentType.Analyzer,
        "detector" => Instruments.ComponentType.Detector,
        _ => Instruments.ComponentType.Unknown,
    };

    private static void TranslateSoftware(MzPeakFileMetadata fm, MSData result)
    {
        foreach (var sw in fm.Software)
        {
            var pwizSw = new Software(sw.Id ?? string.Empty)
            {
                Version = sw.Version ?? string.Empty,
            };
            ApplyCvParams(pwizSw, sw.Parameters);
            result.Software.Add(pwizSw);
        }
    }

    private static void TranslateSamples(MzPeakFileMetadata fm, MSData result)
    {
        foreach (var s in fm.Samples)
        {
            var pwizS = new Sample(s.Id ?? string.Empty, s.Name ?? string.Empty);
            ApplyCvParams(pwizS, s.Parameters);
            result.Samples.Add(pwizS);
        }
    }

    private static void TranslateRun(MzPeakFileMetadata fm, MSData result)
    {
        if (!string.IsNullOrEmpty(fm.Run.Id)) result.Run.Id = fm.Run.Id!;

        if (!string.IsNullOrEmpty(fm.Run.StartTime)) result.Run.StartTimeStamp = fm.Run.StartTime!;

        if (!string.IsNullOrEmpty(fm.Run.DefaultSourceFileId))
            result.Run.DefaultSourceFile = result.FileDescription.SourceFiles
                .FirstOrDefault(s => s.Id == fm.Run.DefaultSourceFileId);

        // DefaultInstrument: the run stores the configuration's integer index (null when the run had
        // no default); the configurations were added in that same order, so resolve by position.
        if (fm.Run.DefaultInstrumentId is int instrIdx && instrIdx >= 0 && instrIdx < result.InstrumentConfigurations.Count)
            result.Run.DefaultInstrumentConfiguration = result.InstrumentConfigurations[instrIdx];

        ApplyCvParams(result.Run, fm.Run.Parameters);
    }

    // ===== Shared CV param helper =====

    /// <summary>Apply a list of mzPeak-shaped CV params to a pwiz <see cref="ParamContainer"/>.
    /// Tolerates a null list (a foreign writer may omit the field entirely).</summary>
    private static void ApplyCvParams(ParamContainer target, IReadOnlyList<MzPeakCvParam>? src)
    {
        if (src is null) return;
        foreach (var p in src)
        {
            var cvid = CvidFromCurie(p.Accession);
            var unitCvid = CvidFromCurie(p.Unit);
            string value = ValueToString(p.Value);

            if (cvid != CVID.CVID_Unknown)
            {
                target.Set(cvid, value, unitCvid);
            }
            else
            {
                target.UserParams.Add(new Pwiz.Data.Common.Params.UserParam(
                    p.Name ?? string.Empty, value, type: string.Empty, units: unitCvid));
            }
        }
    }

    private static string ValueToString(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        bool b => b ? "true" : "false",
        long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
        int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
        double d => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        float f => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static CVID CvidFromCurie(string? curie)
    {
        if (string.IsNullOrEmpty(curie)) return CVID.CVID_Unknown;
        return CvLookup.CvTermInfo(curie).Cvid;
    }
}
