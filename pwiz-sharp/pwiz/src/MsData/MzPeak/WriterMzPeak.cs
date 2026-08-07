using System;
using System.Collections.Generic;
using System.Linq;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Spectra;
using MzPeakFileMetadata = Pwiz.Data.MsData.MzPeak.FileMetadata;
using MzPeakSourceFile = Pwiz.Data.MsData.MzPeak.SourceFile;
using MzPeakFileDescription = Pwiz.Data.MsData.MzPeak.FileDescription;
using MzPeakInstrumentConfiguration = Pwiz.Data.MsData.MzPeak.InstrumentConfiguration;

namespace Pwiz.Data.MsData.MzPeak;

/// <summary>
/// Writes an <see cref="MSData"/> document to the mzPeak Parquet-archive format.
/// The inverse of <see cref="SpectrumList_MzPeak"/> + <c>MzPeakReaderAdapter</c>:
/// MSData spectra / chromatograms / file-level metadata are translated into the
/// row-shaped records the column-oriented <see cref="MzPeakWriter"/> consumes,
/// then handed off in one call. Round-trip-safe for the columns the reader
/// understands; CV params unknown to the writer's schema land as free-form
/// CV params under spectrum/scan/precursor parameter lists.
/// </summary>
public sealed class WriterMzPeak
{
    /// <summary>Write the MSData to the given .mzpeak path. Overwrites any existing file.</summary>
    public static void Write(MSData msd, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(msd);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        // Assign each instrument configuration a stable integer index (cross-stack ids are ints).
        // The map drives both the per-scan / run-default references and the OriginalId we stash so
        // the reader can restore pwiz's real string ids.
        var icIndexById = BuildInstrumentConfigIndex(msd);

        var spectra = TranslateSpectra(msd, icIndexById);
        var chroms = TranslateChromatograms(msd);
        var fileMetadata = BuildFileMetadata(msd, spectra, icIndexById);

        MzPeakWriter.Write(outputPath, spectra, fileMetadata, chroms);
    }

    /// <summary>Maps each instrument configuration's (decoded) id to its 0-based position.</summary>
    private static Dictionary<string, int> BuildInstrumentConfigIndex(MSData msd)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < msd.InstrumentConfigurations.Count; i++)
        {
            var id = msd.InstrumentConfigurations[i].Id;
            if (!string.IsNullOrEmpty(id)) map[id] = i;
        }
        return map;
    }

    private static uint? ResolveInstrumentConfigRef(string? id, Dictionary<string, int> icIndexById)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (icIndexById.TryGetValue(id, out int idx)) return (uint)idx;
        return TryParseId(id);   // fall back to a numeric id (cross-stack files store ints directly)
    }

    // ===== Spectrum translation =====

    private static IReadOnlyList<MzPeakWriter.SpectrumToWrite> TranslateSpectra(MSData msd, Dictionary<string, int> icIndexById)
    {
        var list = msd.Run.SpectrumList;
        if (list is null || list.Count == 0) return Array.Empty<MzPeakWriter.SpectrumToWrite>();

        var result = new List<MzPeakWriter.SpectrumToWrite>(list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            var s = list.GetSpectrum(i, getBinaryData: true);
            result.Add(TranslateSpectrum(s, (ulong)i, icIndexById));
        }
        return result;
    }

    private static MzPeakWriter.SpectrumToWrite TranslateSpectrum(Spectrum s, ulong index, Dictionary<string, int> icIndexById)
    {
        var intenArr = s.GetIntensityArray();
        // The "value" array is usually m/z, but UV/DAD spectra carry a wavelength array instead
        // (no m/z). Use m/z when present, else the first non-intensity array-type binary array, and
        // record its type/unit so the reader rebuilds the right array rather than assuming m/z.
        var valueArr = s.GetMZArray() ?? FindNonIntensityValueArray(s);
        var mz = valueArr is null ? Array.Empty<double>() : valueArr.Data.ToArray();
        var intensity = NarrowToFloat(intenArr);
        CVID valueTypeCvid = valueArr is null ? CVID.MS_m_z_array : ArrayTypeCvid(valueArr);
        if (valueTypeCvid == CVID.CVID_Unknown) valueTypeCvid = CVID.MS_m_z_array;
        string? valueArrayCurie = valueTypeCvid == CVID.MS_m_z_array ? null : CvLookup.CvTermInfo(valueTypeCvid).Id;
        CVID valueUnitCvid = valueArr?.CvParam(valueTypeCvid).Units ?? CVID.CVID_Unknown;
        string? valueArrayUnitCurie = (valueTypeCvid == CVID.MS_m_z_array || valueUnitCvid == CVID.CVID_Unknown)
            ? null : CvLookup.CvTermInfo(valueUnitCvid).Id;

        Scan? scan0 = s.ScanList.Scans.Count > 0 ? s.ScanList.Scans[0] : null;
        double time = ExtractScanStartTime(scan0);
        int? msLevel = ExtractIntOrNull(s.Params, CVID.MS_ms_level);
        bool isProfile = s.HasCVParam(CVID.MS_profile_spectrum);
        bool hasRepresentation = isProfile || s.HasCVParam(CVID.MS_centroid_spectrum);

        // Spectrum-level scalars pulled from CV params.
        int? scanPolarity = s.HasCVParam(CVID.MS_positive_scan) ? 1
            : s.HasCVParam(CVID.MS_negative_scan) ? -1
            : (int?)null;
        double? basePeakMz = ExtractDoubleOrNull(s.Params, CVID.MS_base_peak_m_z);
        double? basePeakIntensity = ExtractDoubleOrNull(s.Params, CVID.MS_base_peak_intensity);
        double? tic = ExtractDoubleOrNull(s.Params, CVID.MS_total_ion_current);
        double? lowMz = ExtractDoubleOrNull(s.Params, CVID.MS_lowest_observed_m_z);
        double? highMz = ExtractDoubleOrNull(s.Params, CVID.MS_highest_observed_m_z);

        // Scan-level scalars.
        string? filterString = scan0?.CvParam(CVID.MS_filter_string).Value;
        if (string.IsNullOrEmpty(filterString)) filterString = null;
        double? ionInjection = scan0 is null ? null : ExtractDoubleOrNull(scan0, CVID.MS_ion_injection_time);
        long? preset = scan0 is null ? null : ExtractLongOrNull(scan0, CVID.MS_preset_scan_configuration);
        (double? imValue, string? imTypeCurie) = ExtractIonMobility(scan0);

        // Instrument-config ref. mzPeak stores an integer index; map pwiz's string id to it.
        uint? icRef = ResolveInstrumentConfigRef(scan0?.InstrumentConfiguration?.Id, icIndexById);

        // Scan windows split into parallel low/high arrays; per-window free-form params (beyond the
        // lower/upper limits) ride a JSON sidecar so window-level annotations round-trip.
        double?[]? scanWindowLowers = null;
        double?[]? scanWindowUppers = null;
        string? scanWindowParamsJson = null;
        if (scan0 is not null && scan0.ScanWindows.Count > 0)
        {
            scanWindowLowers = new double?[scan0.ScanWindows.Count];
            scanWindowUppers = new double?[scan0.ScanWindows.Count];
            var windowParams = new List<IReadOnlyList<MzPeakCvParam>>(scan0.ScanWindows.Count);
            for (int i = 0; i < scan0.ScanWindows.Count; i++)
            {
                scanWindowLowers[i] = ExtractDoubleOrNull(scan0.ScanWindows[i], CVID.MS_scan_window_lower_limit);
                scanWindowUppers[i] = ExtractDoubleOrNull(scan0.ScanWindows[i], CVID.MS_scan_window_upper_limit);
                windowParams.Add(ExtractWindowParams(scan0.ScanWindows[i]));
            }
            scanWindowParamsJson = ScanWindowParams.Serialize(windowParams);
        }

        // Free-form params (those not handled by the typed columns above) flow
        // through as CV-param lists so the round-trip preserves vendor-specific
        // annotations the reader won't otherwise know about.
        var spectrumParams = ExtractFreeFormParams(s.Params, SpectrumCvScalarBlacklist);
        var scanParams = scan0 is null ? null : ExtractFreeFormParams(scan0, ScanCvScalarBlacklist);

        var precursors = TranslatePrecursors(s);

        // scanList combination method (MS:1000570 children) and referenceableParamGroup refs.
        string? scanCombinationCurie = FindScanCombinationCurie(s.ScanList);
        IReadOnlyList<string>? paramGroupRefs = s.Params.ParamGroups.Count > 0
            ? s.Params.ParamGroups.Select(pg => pg.Id).ToList()
            : null;
        string? auxJson = AuxiliaryArrays.Serialize(
            ExtractAuxiliaryArrays(s.BinaryDataArrays, s.IntegerDataArrays, valueArr));

        // scanList scans beyond the first (combined ion-mobility spectra: one scan per mobility bin).
        string? extraScansJson = ExtractExtraScans(s.ScanList);

        return new MzPeakWriter.SpectrumToWrite(
            Index: index,
            Id: s.Id,
            Time: time,
            MsLevel: msLevel,
            IsProfile: isProfile,
            Mz: mz,
            Intensity: intensity,
            ScanStartTime: scan0 is null ? (double?)null : ExtractDoubleOrNull(scan0, CVID.MS_scan_start_time),
            FilterString: filterString,
            InstrumentConfigurationRef: icRef,
            IonInjectionTime: ionInjection,
            ScanWindowLowerLimits: scanWindowLowers,
            ScanWindowUpperLimits: scanWindowUppers,
            ScanSpectrumRef: string.IsNullOrEmpty(scan0?.SpectrumId) ? null : scan0!.SpectrumId,
            ScanWindowParamsJson: scanWindowParamsJson,
            SpectrumParameters: spectrumParams,
            ScanParameters: scanParams,
            ScanPolarity: scanPolarity,
            BasePeakMz: basePeakMz,
            BasePeakIntensity: basePeakIntensity,
            TotalIonCurrent: tic,
            LowestObservedMz: lowMz,
            HighestObservedMz: highMz,
            ScanIonMobilityValue: imValue,
            ScanIonMobilityTypeCurie: imTypeCurie,
            PresetScanConfiguration: preset,
            ScanCombinationCurie: scanCombinationCurie,
            ParamGroupRefs: paramGroupRefs,
            HasRepresentation: hasRepresentation,
            ValueArrayCurie: valueArrayCurie,
            ValueArrayUnitCurie: valueArrayUnitCurie,
            AuxiliaryArraysJson: auxJson,
            ExtraScansJson: extraScansJson,
            Precursors: precursors);
    }

    private static IReadOnlyList<MzPeakWriter.PrecursorToWrite>? TranslatePrecursors(Spectrum s)
    {
        if (s.Precursors.Count == 0) return null;
        var result = new List<MzPeakWriter.PrecursorToWrite>(s.Precursors.Count);
        foreach (var p in s.Precursors)
        {
            // Isolation window scalars.
            double? targetMz = ExtractDoubleOrNull(p.IsolationWindow, CVID.MS_isolation_window_target_m_z);
            double? lowerOff = ExtractDoubleOrNull(p.IsolationWindow, CVID.MS_isolation_window_lower_offset);
            double? upperOff = ExtractDoubleOrNull(p.IsolationWindow, CVID.MS_isolation_window_upper_offset);
            var isoParams = ExtractFreeFormParams(p.IsolationWindow, IsolationCvScalarBlacklist);

            // Activation: collision energy + the dissociation method CV term.
            double? ce = ExtractDoubleOrNull(p.Activation, CVID.MS_collision_energy);
            string? dissCurie = FindDissociationMethodCurie(p.Activation);
            var actParams = ExtractFreeFormParams(p.Activation, ActivationCvScalarBlacklist);

            // Selected ion: take the first; multi-selected-ion-per-precursor
            // (rare outside HRMS dedup) collapses to the first for now.
            double? siMz = null, siIntensity = null;
            long? siCharge = null;
            IReadOnlyList<MzPeakReader.CvParam>? siParams = null;
            if (p.SelectedIons.Count > 0)
            {
                var si = p.SelectedIons[0];
                siMz = ExtractDoubleOrNull(si, CVID.MS_selected_ion_m_z);
                siIntensity = ExtractDoubleOrNull(si, CVID.MS_peak_intensity);
                siCharge = ExtractLongOrNull(si, CVID.MS_charge_state);
                siParams = ExtractFreeFormParams(si, SelectedIonCvScalarBlacklist);
            }

            result.Add(new MzPeakWriter.PrecursorToWrite(
                PrecursorId: string.IsNullOrEmpty(p.SpectrumId) ? null : p.SpectrumId,
                IsolationTargetMz: targetMz,
                IsolationLowerOffset: lowerOff,
                IsolationUpperOffset: upperOff,
                CollisionEnergy: ce,
                DissociationMethodCurie: dissCurie,
                SelectedIonMz: siMz,
                SelectedIonPeakIntensity: siIntensity,
                SelectedIonChargeState: siCharge,
                IsolationWindowParameters: isoParams,
                ActivationParameters: actParams,
                SelectedIonParameters: siParams));
        }
        return result;
    }

    // ===== Chromatogram translation =====

    private static IReadOnlyList<MzPeakWriter.ChromatogramToWrite> TranslateChromatograms(MSData msd)
    {
        var list = msd.Run.ChromatogramList;
        if (list is null || list.Count == 0) return Array.Empty<MzPeakWriter.ChromatogramToWrite>();

        var result = new List<MzPeakWriter.ChromatogramToWrite>(list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            var c = list.GetChromatogram(i, getBinaryData: true);
            var timeArr = c.GetTimeArray();
            var intenArr = c.GetIntensityArray();
            var time = timeArr is null ? Array.Empty<double>() : timeArr.Data.ToArray();
            var intensity = NarrowToFloat(intenArr);

            // Chromatogram type CURIE: prefer the standard chromatogram types.
            string? typeCurie = FindChromatogramTypeCurie(c.Params);

            // Preserve the time + intensity array units (both vary widely across vendors).
            string? timeUnitCurie = null;
            if (timeArr is not null)
            {
                var timeCv = timeArr.CvParam(CVID.MS_time_array);
                if (timeCv.Units != CVID.CVID_Unknown)
                    timeUnitCurie = CvLookup.CvTermInfo(timeCv.Units).Id;
            }
            string? intensityUnitCurie = null;
            if (intenArr is not null)
            {
                var intenCv = intenArr.CvParam(CVID.MS_intensity_array);
                if (intenCv.Units != CVID.CVID_Unknown)
                    intensityUnitCurie = CvLookup.CvTermInfo(intenCv.Units).Id;
            }

            result.Add(new MzPeakWriter.ChromatogramToWrite(
                Index: (ulong)i,
                Id: c.Id,
                ChromatogramTypeCurie: typeCurie,
                DataProcessingRef: c.DataProcessing?.Id,
                Time: time,
                Intensity: intensity,
                TimeUnitCurie: timeUnitCurie,
                IntensityUnitCurie: intensityUnitCurie,
                Parameters: ExtractChromatogramParams(c.Params),
                AuxiliaryArraysJson: AuxiliaryArrays.Serialize(
                    ExtractAuxiliaryArrays(c.BinaryDataArrays, c.IntegerDataArrays, timeArr))));
        }
        return result;
    }

    // ===== File-level metadata =====

    private static MzPeakFileMetadata BuildFileMetadata(MSData msd, IReadOnlyList<MzPeakWriter.SpectrumToWrite> spectra,
        Dictionary<string, int> icIndexById)
    {
        // Source files + content CV params from MSData.FileDescription.
        var contents = ToMzPeakCvParams(msd.FileDescription.FileContent);
        var sourceFiles = new List<MzPeakSourceFile>(msd.FileDescription.SourceFiles.Count);
        foreach (var sf in msd.FileDescription.SourceFiles)
        {
            sourceFiles.Add(new MzPeakSourceFile(
                Id: sf.Id,
                Name: sf.Name,
                Location: sf.Location,
                Parameters: ToMzPeakCvParams(sf)));
        }

        // Instrument configurations: stable integer index for cross-stack readers, plus the real
        // pwiz string id (OriginalId), the source/analyzer/detector component chain, and the
        // controlling-software reference so the configuration round-trips in full.
        var instrumentConfigs = new List<MzPeakInstrumentConfiguration>(msd.InstrumentConfigurations.Count);
        for (int i = 0; i < msd.InstrumentConfigurations.Count; i++)
        {
            var ic = msd.InstrumentConfigurations[i];
            instrumentConfigs.Add(new MzPeakInstrumentConfiguration(
                Id: i,
                Components: BuildComponents(ic.ComponentList),
                SoftwareReference: ic.Software?.Id,
                Parameters: ToMzPeakCvParams(ic),
                OriginalId: string.IsNullOrEmpty(ic.Id) ? null : ic.Id,
                ParamGroupRefs: ic.Params.ParamGroups.Count > 0
                    ? ic.Params.ParamGroups.Select(pg => pg.Id).ToList()
                    : null));
        }

        var dpMethods = new List<DataProcessingMethod>(msd.DataProcessings.Count);
        foreach (var dp in msd.DataProcessings)
        {
            var methods = new List<ProcessingMethodInfo>(dp.ProcessingMethods.Count);
            foreach (var pm in dp.ProcessingMethods)
                methods.Add(new ProcessingMethodInfo(
                    Order: pm.Order,
                    SoftwareReference: pm.Software?.Id,
                    Parameters: ToMzPeakCvParams(pm)));
            dpMethods.Add(new DataProcessingMethod(Id: dp.Id, Methods: methods));
        }

        // referenceableParamGroupList: bundle id + its params so the reader can rebuild the shared
        // groups (per-spectrum/scan references travel as a row-level column, populated elsewhere).
        var paramGroups = new List<ParamGroupInfo>(msd.ParamGroups.Count);
        foreach (var pg in msd.ParamGroups)
            paramGroups.Add(new ParamGroupInfo(Id: pg.Id, Parameters: ToMzPeakCvParams(pg)));

        var software = new List<SoftwareInfo>(msd.Software.Count);
        foreach (var sw in msd.Software)
        {
            software.Add(new SoftwareInfo(
                Id: sw.Id,
                Version: sw.Version,
                Parameters: ToMzPeakCvParams(sw)));
        }

        var samples = new List<SampleInfo>(msd.Samples.Count);
        foreach (var s in msd.Samples)
        {
            samples.Add(new SampleInfo(
                Id: s.Id,
                Name: s.Name,
                Parameters: ToMzPeakCvParams(s)));
        }

        uint? defaultInstrumentId = ResolveInstrumentConfigRef(msd.Run.DefaultInstrumentConfiguration?.Id, icIndexById);
        var run = new RunInfo(
            Id: msd.Run.Id,
            DefaultDataProcessingId: null,
            DefaultInstrumentId: defaultInstrumentId is uint u ? (int)u : null,
            DefaultSourceFileId: msd.Run.DefaultSourceFile?.Id,
            StartTime: string.IsNullOrEmpty(msd.Run.StartTimeStamp) ? null : msd.Run.StartTimeStamp,
            Parameters: ToMzPeakCvParams(msd.Run));

        long spectrumDataPointCount = 0;
        foreach (var s in spectra) spectrumDataPointCount += s.Mz.Length;

        return new MzPeakFileMetadata(
            FileDescription: new MzPeakFileDescription(Contents: contents, SourceFiles: sourceFiles),
            InstrumentConfigurations: instrumentConfigs,
            DataProcessingMethods: dpMethods,
            Software: software,
            Samples: samples,
            Run: run,
            SpectrumCount: spectra.Count,
            SpectrumDataPointCount: spectrumDataPointCount,
            DocumentId: string.IsNullOrEmpty(msd.Id) ? null : msd.Id,
            ParamGroups: paramGroups.Count == 0 ? null : paramGroups);
    }

    /// <summary>Translate a pwiz component chain into the mzPeak component records.</summary>
    private static IReadOnlyList<ComponentInfo> BuildComponents(ComponentList components)
    {
        if (components.Count == 0) return Array.Empty<ComponentInfo>();
        var result = new List<ComponentInfo>(components.Count);
        foreach (var c in components)
            result.Add(new ComponentInfo(
                Type: ComponentTypeToString(c.Type),
                Order: c.Order,
                Parameters: ToMzPeakCvParams(c)));
        return result;
    }

    private static string ComponentTypeToString(ComponentType type) => type switch
    {
        ComponentType.Source => "source",
        ComponentType.Analyzer => "analyzer",
        ComponentType.Detector => "detector",
        _ => "unknown",
    };

    // ===== Helpers =====

    private static float[] NarrowToFloat(BinaryDataArray? source)
    {
        if (source is null) return Array.Empty<float>();
        var arr = new float[source.Data.Count];
        for (int i = 0; i < source.Data.Count; i++) arr[i] = (float)source.Data[i];
        return arr;
    }

    private static double ExtractScanStartTime(Scan? scan)
    {
        if (scan is null) return 0.0;
        return ExtractDoubleOrNull(scan, CVID.MS_scan_start_time) ?? 0.0;
    }

    private static double? ExtractDoubleOrNull(ParamContainer container, CVID cvid)
    {
        var p = container.CvParam(cvid);
        if (p.Cvid == CVID.CVID_Unknown || string.IsNullOrEmpty(p.Value)) return null;
        return p.ValueAs<double>();
    }

    private static int? ExtractIntOrNull(ParamContainer container, CVID cvid)
    {
        var p = container.CvParam(cvid);
        if (p.Cvid == CVID.CVID_Unknown || string.IsNullOrEmpty(p.Value)) return null;
        return p.ValueAs<int>();
    }

    private static long? ExtractLongOrNull(ParamContainer container, CVID cvid)
    {
        var p = container.CvParam(cvid);
        if (p.Cvid == CVID.CVID_Unknown || string.IsNullOrEmpty(p.Value)) return null;
        return p.ValueAs<long>();
    }

    /// <summary>
    /// Walk the scan's CV params looking for one of the ion-mobility value
    /// terms (drift time, reverse drift time, FAIMS CV, …). Returns (value,
    /// type CURIE) for the first match, or (null, null) when none is present.
    /// </summary>
    private static (double? value, string? typeCurie) ExtractIonMobility(Scan? scan)
    {
        if (scan is null) return (null, null);
        foreach (var im in IonMobilityCvids)
        {
            var p = scan.CvParam(im);
            if (p.Cvid != CVID.CVID_Unknown && !string.IsNullOrEmpty(p.Value))
                return (p.ValueAs<double>(), CvLookup.CvTermInfo(im).Id);
        }
        return (null, null);
    }

    private static string? FindDissociationMethodCurie(Activation activation)
    {
        foreach (var cvParam in activation.CVParams)
        {
            if (CvLookup.CvIsA(cvParam.Cvid, CVID.MS_dissociation_method))
                return CvLookup.CvTermInfo(cvParam.Cvid).Id;
        }
        return null;
    }

    private static string? FindScanCombinationCurie(ScanList scanList)
    {
        foreach (var cvParam in scanList.CVParams)
            if (CvLookup.CvIsA(cvParam.Cvid, CVID.MS_spectra_combination))
                return CvLookup.CvTermInfo(cvParam.Cvid).Id;
        return null;
    }

    /// <summary>
    /// Chromatogram-level free-form params: every CV/user param except the chromatogram-type term
    /// (which rides the typed type-CURIE column). Preserves polarity and any other annotations.
    /// </summary>
    private static IReadOnlyList<MzPeakReader.CvParam>? ExtractChromatogramParams(ParamContainer container)
    {
        var list = new List<MzPeakReader.CvParam>();
        foreach (var cv in container.CVParams)
        {
            if (CvLookup.CvIsA(cv.Cvid, CVID.MS_chromatogram_type)) continue;
            list.Add(ToMzPeakCv(cv));
        }
        foreach (var up in container.UserParams)
            list.Add(new MzPeakReader.CvParam(
                Name: up.Name, Accession: null, ValueString: up.Value,
                ValueInteger: null, ValueFloat: null, ValueBoolean: null,
                Unit: up.Units == CVID.CVID_Unknown ? null : CvLookup.CvTermInfo(up.Units).Id,
                Type: string.IsNullOrEmpty(up.Type) ? null : up.Type));
        return list.Count == 0 ? null : list;
    }

    private static readonly HashSet<CVID> ArrayEncodingCvids = new()
    {
        CVID.MS_32_bit_float, CVID.MS_64_bit_float, CVID.MS_32_bit_integer, CVID.MS_64_bit_integer,
        CVID.MS_no_compression, CVID.MS_zlib_compression,
        CVID.MS_MS_Numpress_linear_prediction_compression,
        CVID.MS_MS_Numpress_positive_integer_compression,
        CVID.MS_MS_Numpress_short_logged_float_compression,
        CVID.MS_MS_Numpress_linear_prediction_compression_followed_by_zlib_compression,
        CVID.MS_MS_Numpress_positive_integer_compression_followed_by_zlib_compression,
        CVID.MS_MS_Numpress_short_logged_float_compression_followed_by_zlib_compression,
    };

    /// <summary>
    /// Capture every binary/integer data array beyond the canonical value+intensity pair
    /// (<paramref name="valueArray"/> = the m/z / wavelength / time array; intensity excluded by CV)
    /// as <see cref="AuxiliaryArrayData"/> records — ion-mobility arrays, "ms level" non-standard
    /// arrays, resolution/baseline/SN arrays, etc. Binary-encoding terms (precision, compression)
    /// are dropped; the array-type / name / unit params are kept so the reader can rebuild them.
    /// </summary>
    private static IReadOnlyList<AuxiliaryArrayData>? ExtractAuxiliaryArrays(
        IReadOnlyList<BinaryDataArray> binaryArrays, IReadOnlyList<IntegerDataArray> integerArrays, BinaryDataArray? valueArray)
    {
        var result = new List<AuxiliaryArrayData>();
        foreach (var arr in binaryArrays)
        {
            if (ReferenceEquals(arr, valueArray) || arr.HasCVParam(CVID.MS_intensity_array)) continue;
            result.Add(new AuxiliaryArrayData(AuxArrayParams(arr), IsInteger: false, DoubleValues: arr.Data.ToArray(), IntValues: null));
        }
        foreach (var arr in integerArrays)
            result.Add(new AuxiliaryArrayData(AuxArrayParams(arr), IsInteger: true, DoubleValues: null, IntValues: arr.Data.ToArray()));
        return result.Count == 0 ? null : result;
    }

    /// <summary>The array-type CV term (child of MS:1000513 binary data array) identifying an array.</summary>
    private static CVID ArrayTypeCvid(ParamContainer arr)
    {
        foreach (var cv in arr.CVParams)
            if (CvLookup.CvIsA(cv.Cvid, CVID.MS_binary_data_array))
                return cv.Cvid;
        return CVID.CVID_Unknown;
    }

    /// <summary>First non-intensity array-type binary array (the value array when there is no m/z, e.g. UV wavelength).</summary>
    private static BinaryDataArray? FindNonIntensityValueArray(Spectrum s)
    {
        foreach (var arr in s.BinaryDataArrays)
        {
            if (arr.HasCVParam(CVID.MS_intensity_array)) continue;
            if (ArrayTypeCvid(arr) != CVID.CVID_Unknown) return arr;
        }
        return null;
    }

    /// <summary>Serialize scanList scans beyond scan[0] (their full params + scan windows) to JSON.</summary>
    private static string? ExtractExtraScans(ScanList scanList)
    {
        if (scanList.Scans.Count <= 1) return null;
        var extras = new List<ExtraScanData>(scanList.Scans.Count - 1);
        for (int i = 1; i < scanList.Scans.Count; i++)
        {
            var scan = scanList.Scans[i];
            var windows = new List<IReadOnlyList<MzPeakCvParam>>(scan.ScanWindows.Count);
            foreach (var w in scan.ScanWindows) windows.Add(ToMzPeakCvParams(w));
            extras.Add(new ExtraScanData(ToMzPeakCvParams(scan), windows,
                SpectrumId: string.IsNullOrEmpty(scan.SpectrumId) ? null : scan.SpectrumId));
        }
        return ExtraScans.Serialize(extras);
    }

    /// <summary>Scan-window params beyond the typed lower/upper limits (e.g. Agilent "centroided min/max").</summary>
    private static IReadOnlyList<MzPeakCvParam> ExtractWindowParams(ParamContainer window)
    {
        var list = new List<MzPeakCvParam>();
        foreach (var cv in window.CVParams)
        {
            if (cv.Cvid is CVID.MS_scan_window_lower_limit or CVID.MS_scan_window_upper_limit) continue;
            list.Add(new MzPeakCvParam(
                Name: CvLookup.CvTermInfo(cv.Cvid).Name,
                Accession: CvLookup.CvTermInfo(cv.Cvid).Id,
                Value: CoerceJsonScalar(cv.Value),
                Unit: cv.Units == CVID.CVID_Unknown ? null : CvLookup.CvTermInfo(cv.Units).Id));
        }
        foreach (var up in window.UserParams)
            list.Add(new MzPeakCvParam(
                Name: up.Name, Accession: null, Value: up.Value,
                Unit: up.Units == CVID.CVID_Unknown ? null : CvLookup.CvTermInfo(up.Units).Id));
        return list;
    }

    private static IReadOnlyList<MzPeakCvParam> AuxArrayParams(ParamContainer arr)
    {
        var list = new List<MzPeakCvParam>();
        foreach (var cv in arr.CVParams)
        {
            if (ArrayEncodingCvids.Contains(cv.Cvid)) continue;
            list.Add(new MzPeakCvParam(
                Name: CvLookup.CvTermInfo(cv.Cvid).Name,
                Accession: CvLookup.CvTermInfo(cv.Cvid).Id,
                Value: CoerceJsonScalar(cv.Value),
                Unit: cv.Units == CVID.CVID_Unknown ? null : CvLookup.CvTermInfo(cv.Units).Id));
        }
        foreach (var up in arr.UserParams)
            list.Add(new MzPeakCvParam(
                Name: up.Name, Accession: null, Value: up.Value,
                Unit: up.Units == CVID.CVID_Unknown ? null : CvLookup.CvTermInfo(up.Units).Id));
        return list;
    }

    private static string? FindChromatogramTypeCurie(ParamContainer container)
    {
        foreach (var cvParam in container.CVParams)
        {
            // Chromatogram-type terms (TIC / SIC / BPC / …) are children of MS:1000626
            // "chromatogram type", NOT MS:1000625 "chromatogram" — walking the wrong root
            // silently dropped the type on round-trip.
            if (CvLookup.CvIsA(cvParam.Cvid, CVID.MS_chromatogram_type))
                return CvLookup.CvTermInfo(cvParam.Cvid).Id;
        }
        return null;
    }

    /// <summary>
    /// Extract every CV/User param NOT already represented by one of the typed
    /// scalar columns. Keeps round-trip lossless for vendor-specific terms.
    /// </summary>
    private static IReadOnlyList<MzPeakReader.CvParam>? ExtractFreeFormParams(ParamContainer container, HashSet<CVID> blacklist)
    {
        var list = new List<MzPeakReader.CvParam>();
        foreach (var cv in container.CVParams)
        {
            if (blacklist.Contains(cv.Cvid)) continue;
            // Also skip polarity / representation CVs that were folded into
            // scalar columns; they have no value text and would re-emit empty.
            if (PolarityRepresentationCvids.Contains(cv.Cvid)) continue;
            list.Add(ToMzPeakCv(cv));
        }
        foreach (var up in container.UserParams)
        {
            // Free-form user params survive as CV-less name/value pairs, keeping their xsd type hint.
            list.Add(new MzPeakReader.CvParam(
                Name: up.Name,
                Accession: null,
                ValueString: up.Value,
                ValueInteger: null,
                ValueFloat: null,
                ValueBoolean: null,
                Unit: up.Units == CVID.CVID_Unknown ? null : CvLookup.CvTermInfo(up.Units).Id,
                Type: string.IsNullOrEmpty(up.Type) ? null : up.Type));
        }
        return list.Count == 0 ? null : list;
    }

    private static IReadOnlyList<MzPeakCvParam> ToMzPeakCvParams(ParamContainer container)
    {
        var list = new List<MzPeakCvParam>(container.CVParams.Count + container.UserParams.Count);
        foreach (var cv in container.CVParams)
        {
            list.Add(new MzPeakCvParam(
                Name: CvLookup.CvTermInfo(cv.Cvid).Name,
                Accession: CvLookup.CvTermInfo(cv.Cvid).Id,
                Value: CoerceJsonScalar(cv.Value),
                Unit: cv.Units == CVID.CVID_Unknown ? null : CvLookup.CvTermInfo(cv.Units).Id));
        }
        foreach (var up in container.UserParams)
        {
            list.Add(new MzPeakCvParam(
                Name: up.Name,
                Accession: null,
                Value: up.Value,
                Unit: up.Units == CVID.CVID_Unknown ? null : CvLookup.CvTermInfo(up.Units).Id));
        }
        return list;
    }

    private static MzPeakReader.CvParam ToMzPeakCv(CVParam cv)
    {
        // We only know the value is a string here — pwiz CV params don't carry
        // typed values, just an XSD-style text representation. Always pack as
        // ValueString; readers needing typed access can parse on demand.
        return new MzPeakReader.CvParam(
            Name: CvLookup.CvTermInfo(cv.Cvid).Name,
            Accession: CvLookup.CvTermInfo(cv.Cvid).Id,
            ValueString: cv.Value,
            ValueInteger: null,
            ValueFloat: null,
            ValueBoolean: null,
            Unit: cv.Units == CVID.CVID_Unknown ? null : CvLookup.CvTermInfo(cv.Units).Id);
    }

    private static object? CoerceJsonScalar(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        // Persist as the most specific JSON scalar type we can recognise so
        // round-trip readers can parse without extra interpretation.
        if (long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var l)) return l;
        if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) return false;
        return value;
    }

    private static uint? TryParseId(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (uint.TryParse(id, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v))
            return v;
        return null;
    }

    // ===== CV blacklists =====
    //
    // CV terms we extract into typed scalar columns are filtered out of the free-form parameter
    // lists to avoid emitting the same value twice. BUT only *unitless* terms are blacklisted:
    // every unit-bearing scalar's unit varies by vendor — present, absent, or different (e.g.
    // total ion current is unitless in some vendors but MS_number_of_detector_counts in others;
    // base peak m/z is unitless in Waters but MS_m_z elsewhere; ion mobility is ms vs Vs/cm²) —
    // and the typed columns can't carry the unit. So unit-bearing scalars are left OUT of the
    // blacklist: they ride the free-form params (which preserve the exact unit, including its
    // absence) while their value also lands in the typed column for cross-stack consumers. On read
    // the typed column seeds a synthesized-unit fallback that the free-form param then overrides,
    // so cross-stack files (no free-form params) still get a sensible default.

    private static readonly HashSet<CVID> SpectrumCvScalarBlacklist = new()
    {
        CVID.MS_ms_level,   // unitless
    };

    private static readonly HashSet<CVID> ScanCvScalarBlacklist = new()
    {
        CVID.MS_filter_string,               // unitless (string)
        CVID.MS_preset_scan_configuration,   // unitless
    };

    private static readonly HashSet<CVID> IsolationCvScalarBlacklist = new();

    private static readonly HashSet<CVID> ActivationCvScalarBlacklist = new();

    private static readonly HashSet<CVID> SelectedIonCvScalarBlacklist = new()
    {
        CVID.MS_charge_state,   // unitless
    };

    private static readonly HashSet<CVID> PolarityRepresentationCvids = new()
    {
        CVID.MS_positive_scan,
        CVID.MS_negative_scan,
        CVID.MS_profile_spectrum,
        CVID.MS_centroid_spectrum,
    };

    private static readonly CVID[] IonMobilityCvids = new[]
    {
        CVID.MS_ion_mobility_drift_time,
        CVID.MS_inverse_reduced_ion_mobility,
        CVID.MS_FAIMS_compensation_voltage,
    };
}
