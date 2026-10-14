using System.Collections.Generic;
using HDF.PInvoke;

namespace Pwiz.Data.MsData.Mz5;

/// <summary>
/// mz5 file-level configuration: dataset names, on-disk HDF5 types, and per-
/// dataset chunk/buffer sizes. Read-only subset of
/// <c>pwiz::msdata::mz5::Configuration_mz5</c> — we don't need the write-time
/// per-array precision overrides since we only read mz5 files (HDF5 promotes
/// NATIVE_FLOAT -&gt; NATIVE_DOUBLE transparently on read).
/// </summary>
public static class Mz5Configuration
{
    /// <summary>mz5 file format major version we recognize. The cpp writer
    /// produces 0.10 today; we accept that or anything with the same major.</summary>
    public const ushort MajorVersion = 0;

    /// <summary>Minor version cpp writes today.</summary>
    public const ushort MinorVersion = 10;

    private static readonly Dictionary<Mz5Datasets, string> DatasetNames = new()
    {
        [Mz5Datasets.ControlledVocabulary]      = "ControlledVocabulary",
        [Mz5Datasets.CVReference]               = "CVReference",
        [Mz5Datasets.CVParam]                   = "CVParam",
        [Mz5Datasets.UserParam]                 = "UserParam",
        [Mz5Datasets.RefParam]                  = "RefParam",
        [Mz5Datasets.FileContent]               = "FileContent",
        [Mz5Datasets.Contact]                   = "Contact",
        [Mz5Datasets.ParamGroups]               = "ParamGroups",
        [Mz5Datasets.SourceFiles]               = "SourceFiles",
        [Mz5Datasets.Samples]                   = "Samples",
        [Mz5Datasets.Software]                  = "Software",
        [Mz5Datasets.ScanSetting]               = "ScanSetting",
        [Mz5Datasets.InstrumentConfiguration]   = "InstrumentConfiguration",
        [Mz5Datasets.DataProcessing]            = "DataProcessing",
        [Mz5Datasets.Run]                       = "Run",
        [Mz5Datasets.SpectrumMetaData]          = "SpectrumMetaData",
        // NB: SpectrumBinaryMetaData's on-disk name is "SpectrumListBinaryData"
        // (cpp Configuration_mz5.cpp:108) — names differ from the enum.
        [Mz5Datasets.SpectrumBinaryMetaData]    = "SpectrumListBinaryData",
        [Mz5Datasets.ChromatogramMetaData]      = "ChromatogramList",
        [Mz5Datasets.ChromatogramBinaryMetaData] = "ChromatogramListBinaryData",
        [Mz5Datasets.ChromatogramIndex]         = "ChromatogramIndex",
        [Mz5Datasets.SpectrumIndex]             = "SpectrumIndex",
        [Mz5Datasets.SpectrumMZ]                = "SpectrumMZ",
        [Mz5Datasets.SpectrumIntensity]         = "SpectrumIntensity",
        // cpp typo preserved: "ChomatogramTime" without the 'r'.
        [Mz5Datasets.ChromatogramTime]          = "ChomatogramTime",
        [Mz5Datasets.ChromatogramIntensity]     = "ChromatogramIntensity",
        [Mz5Datasets.FileInformation]           = "FileInformation",
    };

    /// <summary>Returns the on-disk HDF5 dataset name for an enum value.
    /// Names are part of the mz5 format and must match cpp exactly.</summary>
    public static string DatasetName(Mz5Datasets ds) => DatasetNames[ds];

    /// <summary>Fixed-length string sizes from cpp Datastructures_mz5.hpp.
    /// HDF5 fixed-length strings include a trailing NUL, so storage size
    /// equals these constants. The pwiz cpp constants:
    /// <list type="bullet">
    ///   <item>CVL   = 128 — CVParam value string</item>
    ///   <item>USRVL = 128 — UserParam value</item>
    ///   <item>USRNL = 256 — UserParam name</item>
    ///   <item>USRTL = 64  — UserParam type</item>
    /// </list>
    /// </summary>
    public const int CvParamValueLen = 128;

    /// <summary>UserParam value length.</summary>
    public const int UserParamValueLen = 128;

    /// <summary>UserParam name length.</summary>
    public const int UserParamNameLen = 256;

    /// <summary>UserParam type length.</summary>
    public const int UserParamTypeLen = 64;
}
