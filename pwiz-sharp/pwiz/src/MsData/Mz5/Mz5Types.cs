using System;
using System.Runtime.InteropServices;
using HDF.PInvoke;

#pragma warning disable CA1806 // HDF5 close() ints — see MzMlbConnection.cs
// Per-field XML doc comments intentionally stripped from the mz5 record
// structs; the struct-level summary documents the on-disk semantics, and the
// HDF5 field-name strings inside CreateType() are the format's source of truth.
#pragma warning disable CS1591

namespace Pwiz.Data.MsData.Mz5;

/// <summary>
/// HDF5 compound-type definitions for the mz5 data model. Port of
/// <c>pwiz::msdata::mz5::Datastructures_mz5</c>. Each cpp <c>FooMZ5</c> struct
/// becomes a [StructLayout(Sequential)] POD here with a matching
/// <c>CreateType()</c> method that registers the HDF5 compound type with the
/// same field names and offsets cpp uses (binary-compatible).
///
/// Strings in mz5 come in two flavors:
/// <list type="bullet">
///   <item>Fixed-length char[N] (CVL=128, USRVL=128, USRNL=256, USRTL=64) for
///         hot tables (CVParam value, UserParam name/value/type). Stored as
///         HDF5 fixed-length STRPAD_NULLTERM strings.</item>
///   <item>Variable-length char* for the longer / rarer fields (URIs, accession
///         strings, software ids). Stored as HDF5 variable-length strings.</item>
/// </list>
/// Reader-side string marshalling: vlen strings get
/// <c>Marshal.PtrToStringAnsi(field)</c>; fixed-length strings use
/// <c>Marshal.PtrToStringAnsi(addrof(field), maxLen)</c> trimmed at first NUL.
/// </summary>
public static class Mz5Types
{
    /// <summary>Cached HDF5 variable-length string type. Created on first use,
    /// reused for all struct fields that hold a <c>char*</c>. Released at
    /// AppDomain exit (HDF5 reclaims on H5close).</summary>
    public static long VlenStringType { get; } = CreateVlenString();

    private static long CreateVlenString()
    {
        long t = H5T.copy(H5T.C_S1);
        H5T.set_size(t, H5T.VARIABLE);
        return t;
    }

    /// <summary>Build a fixed-length, NUL-terminated HDF5 string type of the
    /// given byte length. Caller owns + closes.</summary>
    public static long CreateFixedString(int byteLen)
    {
        long t = H5T.copy(H5T.C_S1);
        H5T.set_size(t, (IntPtr)byteLen);
        H5T.set_strpad(t, H5T.str_t.NULLTERM);
        return t;
    }

    /// <summary>HDF5 vlen-of-ParamListMZ5 type. Used by every struct that
    /// holds a <c>ParamListsMZ5</c> field (Targets / SelectedIons / Products
    /// / ScanWindows). Caller owns + closes.</summary>
    public static long CreateParamListsType()
    {
        long elt = ParamListMZ5.CreateType();
        try { return H5T.vlen_create(elt); }
        finally { H5T.close(elt); }
    }
}

/// <summary>POD record for mz5's <c>FileInformation</c> dataset. Five
/// unsigned-short flags identifying the file version and how the writer
/// pre-processed binary data. Read on file open to validate it's an mz5 file.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FileInformationMZ5
{
    public ushort MajorVersion;
    public ushort MinorVersion;
    public ushort DidFiltering;
    public ushort DeltaMz;
    public ushort TranslateInten;

    public static long CreateType()
    {
        long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<FileInformationMZ5>());
        H5T.insert(t, "majorVersion",   Marshal.OffsetOf<FileInformationMZ5>(nameof(MajorVersion)),   H5T.NATIVE_USHORT);
        H5T.insert(t, "minorVersion",   Marshal.OffsetOf<FileInformationMZ5>(nameof(MinorVersion)),   H5T.NATIVE_USHORT);
        H5T.insert(t, "didFiltering",   Marshal.OffsetOf<FileInformationMZ5>(nameof(DidFiltering)),   H5T.NATIVE_USHORT);
        H5T.insert(t, "deltaMZ",        Marshal.OffsetOf<FileInformationMZ5>(nameof(DeltaMz)),        H5T.NATIVE_USHORT);
        H5T.insert(t, "translateInten", Marshal.OffsetOf<FileInformationMZ5>(nameof(TranslateInten)), H5T.NATIVE_USHORT);
        return t;
    }
}

/// <summary>POD record for an entry in mz5's <c>ControlledVocabulary</c>
/// dataset. Each entry describes one CV the file uses (e.g. MS / UO). All
/// four fields are HDF5 variable-length strings.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ContVocabMZ5
{
    public IntPtr Uri;
    public IntPtr FullName;
    public IntPtr Id;
    public IntPtr Version;

    public static long CreateType()
    {
        long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<ContVocabMZ5>());
        H5T.insert(t, "uri",      Marshal.OffsetOf<ContVocabMZ5>(nameof(Uri)),      Mz5Types.VlenStringType);
        H5T.insert(t, "fullname", Marshal.OffsetOf<ContVocabMZ5>(nameof(FullName)), Mz5Types.VlenStringType);
        H5T.insert(t, "id",       Marshal.OffsetOf<ContVocabMZ5>(nameof(Id)),       Mz5Types.VlenStringType);
        H5T.insert(t, "version",  Marshal.OffsetOf<ContVocabMZ5>(nameof(Version)),  Mz5Types.VlenStringType);
        return t;
    }
}

/// <summary>POD record for mz5's <c>CVReference</c> dataset. Reference table
/// indexed from <c>CVParam</c> / <c>UserParam</c> rows so accession strings
/// don't have to repeat across millions of CVParam rows.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CVRefMZ5
{
    public IntPtr Name;
    public IntPtr Prefix;
    public uint Accession;

    public static long CreateType()
    {
        long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<CVRefMZ5>());
        H5T.insert(t, "name",      Marshal.OffsetOf<CVRefMZ5>(nameof(Name)),      Mz5Types.VlenStringType);
        H5T.insert(t, "prefix",    Marshal.OffsetOf<CVRefMZ5>(nameof(Prefix)),    Mz5Types.VlenStringType);
        H5T.insert(t, "accession", Marshal.OffsetOf<CVRefMZ5>(nameof(Accession)), H5T.NATIVE_ULONG);
        return t;
    }
}

/// <summary>POD record for mz5's <c>CVParam</c> dataset. Just a value string
/// + indices into the <c>CVReference</c> table for the accession and unit.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct CVParamMZ5
{
    public fixed byte Value[128]; // Mz5Configuration.CvParamValueLen
    public uint TypeCVRefID;
    public uint UnitCVRefID;

    public static long CreateType()
    {
        long valueType = Mz5Types.CreateFixedString(Mz5Configuration.CvParamValueLen);
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<CVParamMZ5>());
            H5T.insert(t, "value",       (IntPtr)0,                                                  valueType);
            H5T.insert(t, "cvRefID", Marshal.OffsetOf<CVParamMZ5>(nameof(TypeCVRefID)), H5T.NATIVE_ULONG);
            H5T.insert(t, "uRefID", Marshal.OffsetOf<CVParamMZ5>(nameof(UnitCVRefID)), H5T.NATIVE_ULONG);
            return t;
        }
        finally
        {
            H5T.close(valueType);
        }
    }
}

/// <summary>POD record for mz5's <c>UserParam</c> dataset. Three fixed-length
/// strings (name / value / type) plus a unit-reference index.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct UserParamMZ5
{
    public fixed byte Name[256];  // Mz5Configuration.UserParamNameLen
    public fixed byte Value[128]; // Mz5Configuration.UserParamValueLen
    public fixed byte Type[64];   // Mz5Configuration.UserParamTypeLen
    public uint UnitCVRefID;

    public static long CreateType()
    {
        long nameType  = Mz5Types.CreateFixedString(Mz5Configuration.UserParamNameLen);
        long valueType = Mz5Types.CreateFixedString(Mz5Configuration.UserParamValueLen);
        long typeType  = Mz5Types.CreateFixedString(Mz5Configuration.UserParamTypeLen);
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<UserParamMZ5>());
            // Field offsets are computed by the C# layout: name=0, value=USRNL,
            // type=USRNL+USRVL, unitCVRefID=USRNL+USRVL+USRTL.
            H5T.insert(t, "name",        (IntPtr)0,                                                    nameType);
            H5T.insert(t, "value",       (IntPtr)Mz5Configuration.UserParamNameLen,                    valueType);
            H5T.insert(t, "type",        (IntPtr)(Mz5Configuration.UserParamNameLen + Mz5Configuration.UserParamValueLen), typeType);
            H5T.insert(t, "uRefID", Marshal.OffsetOf<UserParamMZ5>(nameof(UnitCVRefID)),          H5T.NATIVE_ULONG);
            return t;
        }
        finally
        {
            H5T.close(nameType);
            H5T.close(valueType);
            H5T.close(typeType);
        }
    }
}

/// <summary>POD record for an mz5 reference. A single 32-bit row index into
/// whichever target table it references (ParamGroups, SourceFiles, Software,
/// etc. — context-dependent on which dataset the row appears in).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RefMZ5
{
    public uint RefID;

    public static long CreateType()
    {
        long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<RefMZ5>());
        H5T.insert(t, "refID", Marshal.OffsetOf<RefMZ5>(nameof(RefID)), H5T.NATIVE_ULONG);
        return t;
    }

    public static long CreateListType()
    {
        long elt = CreateType();
        try { return H5T.vlen_create(elt); }
        finally { H5T.close(elt); }
    }
}

/// <summary>POD record for mz5's <c>ParamListMZ5</c> entries. Stores three
/// (start, end) ranges over the global CVParam / UserParam / RefParam tables.
/// Each MSData ParamContainer collapses to a single <c>ParamListMZ5</c> row
/// in the parent dataset — at read time we slice the three global tables to
/// reconstruct the param container.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ParamListMZ5
{
    public uint CVParamStartID;
    public uint CVParamEndID;
    public uint UserParamStartID;
    public uint UserParamEndID;
    public uint RefParamGroupStartID;
    public uint RefParamGroupEndID;

    public bool IsEmpty =>
        CVParamStartID == CVParamEndID &&
        UserParamStartID == UserParamEndID &&
        RefParamGroupStartID == RefParamGroupEndID;

    public static long CreateType()
    {
        long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<ParamListMZ5>());
        H5T.insert(t, "cvstart",       Marshal.OffsetOf<ParamListMZ5>(nameof(CVParamStartID)),       H5T.NATIVE_ULONG);
        H5T.insert(t, "cvend",         Marshal.OffsetOf<ParamListMZ5>(nameof(CVParamEndID)),         H5T.NATIVE_ULONG);
        H5T.insert(t, "usrstart",     Marshal.OffsetOf<ParamListMZ5>(nameof(UserParamStartID)),     H5T.NATIVE_ULONG);
        H5T.insert(t, "usrend",       Marshal.OffsetOf<ParamListMZ5>(nameof(UserParamEndID)),       H5T.NATIVE_ULONG);
        H5T.insert(t, "refstart", Marshal.OffsetOf<ParamListMZ5>(nameof(RefParamGroupStartID)), H5T.NATIVE_ULONG);
        H5T.insert(t, "refend",   Marshal.OffsetOf<ParamListMZ5>(nameof(RefParamGroupEndID)),   H5T.NATIVE_ULONG);
        return t;
    }
}

/// <summary>POD record for mz5's <c>ParamGroups</c> dataset (referenceable
/// parameter groups in mzML). Each row carries an id + a ParamListMZ5
/// slicing into the global CVParam / UserParam / RefParam tables.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ParamGroupMZ5
{
    public IntPtr Id;
    public ParamListMZ5 ParamList;

    public static long CreateType()
    {
        long paramListType = ParamListMZ5.CreateType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<ParamGroupMZ5>());
            H5T.insert(t, "id",        Marshal.OffsetOf<ParamGroupMZ5>(nameof(Id)),        Mz5Types.VlenStringType);
            H5T.insert(t, "params", Marshal.OffsetOf<ParamGroupMZ5>(nameof(ParamList)), paramListType);
            return t;
        }
        finally { H5T.close(paramListType); }
    }
}

/// <summary>HDF5 hvl_t for variable-length list fields inside compound
/// types. Layout matches what HDF5 writes for vlen reads:
/// <c>{ size_t len; T* p; }</c>.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Hvl
{
    public UIntPtr Length;
    public IntPtr Data;
}

/// <summary>POD record for mz5's <c>SourceFiles</c> dataset.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SourceFileMZ5
{
    public IntPtr Id;
    public IntPtr Location;
    public IntPtr Name;
    public ParamListMZ5 ParamList;

    public static long CreateType()
    {
        long pl = ParamListMZ5.CreateType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<SourceFileMZ5>());
            H5T.insert(t, "id",        Marshal.OffsetOf<SourceFileMZ5>(nameof(Id)),        Mz5Types.VlenStringType);
            H5T.insert(t, "location",  Marshal.OffsetOf<SourceFileMZ5>(nameof(Location)),  Mz5Types.VlenStringType);
            H5T.insert(t, "name",      Marshal.OffsetOf<SourceFileMZ5>(nameof(Name)),      Mz5Types.VlenStringType);
            H5T.insert(t, "params", Marshal.OffsetOf<SourceFileMZ5>(nameof(ParamList)), pl);
            return t;
        }
        finally { H5T.close(pl); }
    }
}

/// <summary>POD record for mz5's <c>Samples</c> dataset.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SampleMZ5
{
    public IntPtr Id;
    public IntPtr Name;
    public ParamListMZ5 ParamList;

    public static long CreateType()
    {
        long pl = ParamListMZ5.CreateType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<SampleMZ5>());
            H5T.insert(t, "id",        Marshal.OffsetOf<SampleMZ5>(nameof(Id)),        Mz5Types.VlenStringType);
            H5T.insert(t, "name",      Marshal.OffsetOf<SampleMZ5>(nameof(Name)),      Mz5Types.VlenStringType);
            H5T.insert(t, "params", Marshal.OffsetOf<SampleMZ5>(nameof(ParamList)), pl);
            return t;
        }
        finally { H5T.close(pl); }
    }
}

/// <summary>POD record for mz5's <c>Software</c> dataset.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SoftwareMZ5
{
    public IntPtr Id;
    public IntPtr Version;
    public ParamListMZ5 ParamList;

    public static long CreateType()
    {
        long pl = ParamListMZ5.CreateType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<SoftwareMZ5>());
            H5T.insert(t, "id",        Marshal.OffsetOf<SoftwareMZ5>(nameof(Id)),        Mz5Types.VlenStringType);
            H5T.insert(t, "version",   Marshal.OffsetOf<SoftwareMZ5>(nameof(Version)),   Mz5Types.VlenStringType);
            H5T.insert(t, "params", Marshal.OffsetOf<SoftwareMZ5>(nameof(ParamList)), pl);
            return t;
        }
        finally { H5T.close(pl); }
    }
}

/// <summary>POD record for mz5's <c>ComponentMZ5</c> compound (source /
/// analyzer / detector). Used as element type of the vlen
/// <c>ComponentListMZ5</c> inside <see cref="ComponentsMZ5"/>.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ComponentMZ5
{
    public ParamListMZ5 ParamList;
    public uint Order;

    public static long CreateType()
    {
        long pl = ParamListMZ5.CreateType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<ComponentMZ5>());
            H5T.insert(t, "paramList", Marshal.OffsetOf<ComponentMZ5>(nameof(ParamList)), pl);
            H5T.insert(t, "order",     Marshal.OffsetOf<ComponentMZ5>(nameof(Order)),     H5T.NATIVE_ULONG);
            return t;
        }
        finally { H5T.close(pl); }
    }

    public static long CreateListType()
    {
        long elt = CreateType();
        try { return H5T.vlen_create(elt); }
        finally { H5T.close(elt); }
    }
}

/// <summary>POD record for mz5's <c>ComponentsMZ5</c> (the component list
/// trio: sources, analyzers, detectors), each as an <see cref="Hvl"/> of
/// <see cref="ComponentMZ5"/>.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ComponentsMZ5
{
    public Hvl Sources;
    public Hvl Analyzers;
    public Hvl Detectors;

    public static long CreateType()
    {
        long compList = ComponentMZ5.CreateListType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<ComponentsMZ5>());
            H5T.insert(t, "sources",   Marshal.OffsetOf<ComponentsMZ5>(nameof(Sources)),   compList);
            H5T.insert(t, "analyzers", Marshal.OffsetOf<ComponentsMZ5>(nameof(Analyzers)), compList);
            H5T.insert(t, "detectors", Marshal.OffsetOf<ComponentsMZ5>(nameof(Detectors)), compList);
            return t;
        }
        finally { H5T.close(compList); }
    }
}

/// <summary>POD record for mz5's <c>InstrumentConfiguration</c> dataset.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct InstrumentConfigurationMZ5
{
    public IntPtr Id;
    public ParamListMZ5 ParamList;
    public ComponentsMZ5 Components;
    public RefMZ5 ScanSettingRefID;
    public RefMZ5 SoftwareRefID;

    public static long CreateType()
    {
        long pl = ParamListMZ5.CreateType();
        long comps = ComponentsMZ5.CreateType();
        long refT = RefMZ5.CreateType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<InstrumentConfigurationMZ5>());
            H5T.insert(t, "id",               Marshal.OffsetOf<InstrumentConfigurationMZ5>(nameof(Id)),               Mz5Types.VlenStringType);
            H5T.insert(t, "params",        Marshal.OffsetOf<InstrumentConfigurationMZ5>(nameof(ParamList)),        pl);
            H5T.insert(t, "components",       Marshal.OffsetOf<InstrumentConfigurationMZ5>(nameof(Components)),       comps);
            H5T.insert(t, "refScanSetting", Marshal.OffsetOf<InstrumentConfigurationMZ5>(nameof(ScanSettingRefID)), refT);
            H5T.insert(t, "refSoftware",    Marshal.OffsetOf<InstrumentConfigurationMZ5>(nameof(SoftwareRefID)),    refT);
            return t;
        }
        finally { H5T.close(refT); H5T.close(comps); H5T.close(pl); }
    }
}

/// <summary>POD record for an mz5 ProcessingMethod entry. Used as element of
/// the vlen <c>ProcessingMethodListMZ5</c> inside
/// <see cref="DataProcessingMZ5"/>.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ProcessingMethodMZ5
{
    public ParamListMZ5 ParamList;
    public RefMZ5 SoftwareRefID;
    public uint Order;

    public static long CreateType()
    {
        long pl = ParamListMZ5.CreateType();
        long refT = RefMZ5.CreateType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<ProcessingMethodMZ5>());
            H5T.insert(t, "params",     Marshal.OffsetOf<ProcessingMethodMZ5>(nameof(ParamList)),     pl);
            H5T.insert(t, "refSoftware", Marshal.OffsetOf<ProcessingMethodMZ5>(nameof(SoftwareRefID)), refT);
            H5T.insert(t, "order",         Marshal.OffsetOf<ProcessingMethodMZ5>(nameof(Order)),         H5T.NATIVE_ULONG);
            return t;
        }
        finally { H5T.close(refT); H5T.close(pl); }
    }

    public static long CreateListType()
    {
        long elt = CreateType();
        try { return H5T.vlen_create(elt); }
        finally { H5T.close(elt); }
    }
}

/// <summary>POD record for mz5's <c>DataProcessing</c> dataset.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DataProcessingMZ5
{
    public IntPtr Id;
    public Hvl ProcessingMethodList;

    public static long CreateType()
    {
        long pmList = ProcessingMethodMZ5.CreateListType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<DataProcessingMZ5>());
            H5T.insert(t, "id",                   Marshal.OffsetOf<DataProcessingMZ5>(nameof(Id)),                   Mz5Types.VlenStringType);
            H5T.insert(t, "method", Marshal.OffsetOf<DataProcessingMZ5>(nameof(ProcessingMethodList)), pmList);
            return t;
        }
        finally { H5T.close(pmList); }
    }
}

/// <summary>POD record for mz5's <c>ScanSetting</c> dataset. Generalized
/// container that also stores Targets / SelectedIons / Products / ScanWindows
/// via <see cref="ParamListMZ5"/> vlen lists.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ScanSettingMZ5
{
    public IntPtr Id;
    public ParamListMZ5 ParamList;
    public Hvl SourceFileIDs;
    public Hvl TargetList;

    public static long CreateType()
    {
        long pl = ParamListMZ5.CreateType();
        long refList = RefMZ5.CreateListType();
        long plList = Mz5Types.CreateParamListsType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<ScanSettingMZ5>());
            H5T.insert(t, "id",            Marshal.OffsetOf<ScanSettingMZ5>(nameof(Id)),            Mz5Types.VlenStringType);
            H5T.insert(t, "params",     Marshal.OffsetOf<ScanSettingMZ5>(nameof(ParamList)),     pl);
            H5T.insert(t, "refSourceFiles", Marshal.OffsetOf<ScanSettingMZ5>(nameof(SourceFileIDs)), refList);
            H5T.insert(t, "targets",    Marshal.OffsetOf<ScanSettingMZ5>(nameof(TargetList)),    plList);
            return t;
        }
        finally { H5T.close(plList); H5T.close(refList); H5T.close(pl); }
    }
}

/// <summary>POD record for mz5's <c>Run</c> dataset. One row total — top-level
/// run metadata plus references into the document-level tables.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RunMZ5
{
    public IntPtr Id;
    public IntPtr StartTimeStamp;
    public IntPtr Fid;
    public IntPtr Facc;
    public ParamListMZ5 ParamList;
    public RefMZ5 DefaultSpectrumDataProcessingRefID;
    public RefMZ5 DefaultChromatogramDataProcessingRefID;
    public RefMZ5 DefaultInstrumentConfigurationRefID;
    public RefMZ5 SourceFileRefID;
    public RefMZ5 SampleRefID;

    public static long CreateType()
    {
        long pl = ParamListMZ5.CreateType();
        long refT = RefMZ5.CreateType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<RunMZ5>());
            H5T.insert(t, "id",                                       Marshal.OffsetOf<RunMZ5>(nameof(Id)),                                       Mz5Types.VlenStringType);
            H5T.insert(t, "startTimeStamp",                           Marshal.OffsetOf<RunMZ5>(nameof(StartTimeStamp)),                           Mz5Types.VlenStringType);
            H5T.insert(t, "fid",                                      Marshal.OffsetOf<RunMZ5>(nameof(Fid)),                                      Mz5Types.VlenStringType);
            H5T.insert(t, "facc",                                     Marshal.OffsetOf<RunMZ5>(nameof(Facc)),                                     Mz5Types.VlenStringType);
            H5T.insert(t, "params",                                Marshal.OffsetOf<RunMZ5>(nameof(ParamList)),                                pl);
            H5T.insert(t, "refSpectrumDP",       Marshal.OffsetOf<RunMZ5>(nameof(DefaultSpectrumDataProcessingRefID)),       refT);
            H5T.insert(t, "refChromatogramDP",   Marshal.OffsetOf<RunMZ5>(nameof(DefaultChromatogramDataProcessingRefID)),   refT);
            H5T.insert(t, "refDefaultInstrument",      Marshal.OffsetOf<RunMZ5>(nameof(DefaultInstrumentConfigurationRefID)),      refT);
            H5T.insert(t, "refSourceFile",                          Marshal.OffsetOf<RunMZ5>(nameof(SourceFileRefID)),                          refT);
            H5T.insert(t, "refSample",                              Marshal.OffsetOf<RunMZ5>(nameof(SampleRefID)),                              refT);
            return t;
        }
        finally { H5T.close(refT); H5T.close(pl); }
    }
}

/// <summary>POD record for an mz5 Precursor entry. Used as element of the
/// vlen <see cref="PrecursorMZ5.CreateListType"/> inside
/// <see cref="SpectrumMZ5"/>.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PrecursorMZ5
{
    public IntPtr ExternalSpectrumId;
    public ParamListMZ5 ParamList;
    public ParamListMZ5 Activation;
    public ParamListMZ5 IsolationWindow;
    public Hvl SelectedIonList;
    public RefMZ5 SpectrumRefID;
    public RefMZ5 SourceFileRefID;

    public static long CreateType()
    {
        long pl = ParamListMZ5.CreateType();
        long refT = RefMZ5.CreateType();
        long plList = Mz5Types.CreateParamListsType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<PrecursorMZ5>());
            H5T.insert(t, "externalSpectrumId", Marshal.OffsetOf<PrecursorMZ5>(nameof(ExternalSpectrumId)), Mz5Types.VlenStringType);
            H5T.insert(t, "params",          Marshal.OffsetOf<PrecursorMZ5>(nameof(ParamList)),          pl);
            H5T.insert(t, "activation",         Marshal.OffsetOf<PrecursorMZ5>(nameof(Activation)),         pl);
            H5T.insert(t, "isolationWindow",    Marshal.OffsetOf<PrecursorMZ5>(nameof(IsolationWindow)),    pl);
            H5T.insert(t, "selectedIonList",    Marshal.OffsetOf<PrecursorMZ5>(nameof(SelectedIonList)),    plList);
            H5T.insert(t, "refSpectrum",      Marshal.OffsetOf<PrecursorMZ5>(nameof(SpectrumRefID)),      refT);
            H5T.insert(t, "refSourceFile",    Marshal.OffsetOf<PrecursorMZ5>(nameof(SourceFileRefID)),    refT);
            return t;
        }
        finally { H5T.close(plList); H5T.close(refT); H5T.close(pl); }
    }

    public static long CreateListType()
    {
        long elt = CreateType();
        try { return H5T.vlen_create(elt); }
        finally { H5T.close(elt); }
    }
}

/// <summary>POD record for an mz5 Scan entry. Used as element of the vlen
/// <see cref="ScanMZ5.CreateListType"/> inside <see cref="ScansMZ5"/>.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ScanMZ5
{
    public IntPtr ExternalSpectrumID;
    public ParamListMZ5 ParamList;
    public Hvl ScanWindowList;
    public RefMZ5 InstrumentConfigurationRefID;
    public RefMZ5 SourceFileRefID;
    public RefMZ5 SpectrumRefID;

    public static long CreateType()
    {
        long pl = ParamListMZ5.CreateType();
        long refT = RefMZ5.CreateType();
        long plList = Mz5Types.CreateParamListsType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<ScanMZ5>());
            H5T.insert(t, "externalSpectrumID",           Marshal.OffsetOf<ScanMZ5>(nameof(ExternalSpectrumID)),           Mz5Types.VlenStringType);
            H5T.insert(t, "params",                    Marshal.OffsetOf<ScanMZ5>(nameof(ParamList)),                    pl);
            H5T.insert(t, "scanWindowList",               Marshal.OffsetOf<ScanMZ5>(nameof(ScanWindowList)),               plList);
            H5T.insert(t, "refInstrumentConfiguration", Marshal.OffsetOf<ScanMZ5>(nameof(InstrumentConfigurationRefID)), refT);
            H5T.insert(t, "refSourceFile",              Marshal.OffsetOf<ScanMZ5>(nameof(SourceFileRefID)),              refT);
            H5T.insert(t, "refSpectrum",                Marshal.OffsetOf<ScanMZ5>(nameof(SpectrumRefID)),                refT);
            return t;
        }
        finally { H5T.close(plList); H5T.close(refT); H5T.close(pl); }
    }

    public static long CreateListType()
    {
        long elt = CreateType();
        try { return H5T.vlen_create(elt); }
        finally { H5T.close(elt); }
    }
}

/// <summary>POD record for mz5's nested <c>scanList</c> element. Embedded in
/// <see cref="SpectrumMZ5"/>.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ScansMZ5
{
    public ParamListMZ5 ParamList;
    public Hvl ScanList;

    public static long CreateType()
    {
        long pl = ParamListMZ5.CreateType();
        long scanList = ScanMZ5.CreateListType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<ScansMZ5>());
            H5T.insert(t, "params", Marshal.OffsetOf<ScansMZ5>(nameof(ParamList)), pl);
            H5T.insert(t, "scanList",  Marshal.OffsetOf<ScansMZ5>(nameof(ScanList)),  scanList);
            return t;
        }
        finally { H5T.close(scanList); H5T.close(pl); }
    }
}

/// <summary>POD record for mz5's <c>SpectrumMetaData</c> dataset. One row per
/// spectrum; the actual m/z + intensity peak data lives in the separate
/// SpectrumMZ + SpectrumIntensity datasets, sliced by <c>SpectrumIndex</c>.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SpectrumMZ5
{
    public IntPtr Id;
    public IntPtr SpotID;
    public ParamListMZ5 ParamList;
    public ScansMZ5 ScanList;
    public Hvl PrecursorList;
    public Hvl ProductList;
    public RefMZ5 DataProcessingRefID;
    public RefMZ5 SourceFileRefID;
    public uint Index;

    public static long CreateType()
    {
        long pl = ParamListMZ5.CreateType();
        long scansT = ScansMZ5.CreateType();
        long precList = PrecursorMZ5.CreateListType();
        long plList = Mz5Types.CreateParamListsType();
        long refT = RefMZ5.CreateType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<SpectrumMZ5>());
            H5T.insert(t, "id",                  Marshal.OffsetOf<SpectrumMZ5>(nameof(Id)),                  Mz5Types.VlenStringType);
            H5T.insert(t, "spotID",              Marshal.OffsetOf<SpectrumMZ5>(nameof(SpotID)),              Mz5Types.VlenStringType);
            H5T.insert(t, "params",           Marshal.OffsetOf<SpectrumMZ5>(nameof(ParamList)),           pl);
            H5T.insert(t, "scanList",            Marshal.OffsetOf<SpectrumMZ5>(nameof(ScanList)),            scansT);
            H5T.insert(t, "precursors",       Marshal.OffsetOf<SpectrumMZ5>(nameof(PrecursorList)),       precList);
            H5T.insert(t, "products",         Marshal.OffsetOf<SpectrumMZ5>(nameof(ProductList)),         plList);
            H5T.insert(t, "refDataProcessing", Marshal.OffsetOf<SpectrumMZ5>(nameof(DataProcessingRefID)), refT);
            H5T.insert(t, "refSourceFile",     Marshal.OffsetOf<SpectrumMZ5>(nameof(SourceFileRefID)),     refT);
            H5T.insert(t, "index",               Marshal.OffsetOf<SpectrumMZ5>(nameof(Index)),               H5T.NATIVE_UINT);
            return t;
        }
        finally { H5T.close(refT); H5T.close(plList); H5T.close(precList); H5T.close(scansT); H5T.close(pl); }
    }
}

/// <summary>POD record for mz5's <c>ChromatogramMetaData</c> dataset. One row
/// per chromatogram; time + intensity arrays live in separate datasets sliced
/// by <c>ChromatogramIndex</c>.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ChromatogramMZ5
{
    public IntPtr Id;
    public ParamListMZ5 ParamList;
    public PrecursorMZ5 Precursor;
    public ParamListMZ5 ProductIsolationWindow;
    public RefMZ5 DataProcessingRefID;
    public uint Index;

    public static long CreateType()
    {
        long pl = ParamListMZ5.CreateType();
        long precT = PrecursorMZ5.CreateType();
        long refT = RefMZ5.CreateType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<ChromatogramMZ5>());
            H5T.insert(t, "id",                     Marshal.OffsetOf<ChromatogramMZ5>(nameof(Id)),                     Mz5Types.VlenStringType);
            H5T.insert(t, "params",              Marshal.OffsetOf<ChromatogramMZ5>(nameof(ParamList)),              pl);
            H5T.insert(t, "precursor",              Marshal.OffsetOf<ChromatogramMZ5>(nameof(Precursor)),              precT);
            H5T.insert(t, "productIsolationWindow", Marshal.OffsetOf<ChromatogramMZ5>(nameof(ProductIsolationWindow)), pl);
            H5T.insert(t, "refDataProcessing",    Marshal.OffsetOf<ChromatogramMZ5>(nameof(DataProcessingRefID)),    refT);
            H5T.insert(t, "index",                  Marshal.OffsetOf<ChromatogramMZ5>(nameof(Index)),                  H5T.NATIVE_ULONG);
            return t;
        }
        finally { H5T.close(refT); H5T.close(precT); H5T.close(pl); }
    }
}

/// <summary>POD record for mz5's <c>SpectrumListBinaryData</c> /
/// <c>ChromatogramListBinaryData</c> datasets. One row per binary array
/// pair (x / y) carrying the precision / compression / array-type cvParams
/// for both.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BinaryDataMZ5
{
    public ParamListMZ5 XParamList;
    public ParamListMZ5 YParamList;
    public RefMZ5 XDataProcessingRefID;
    public RefMZ5 YDataProcessingRefID;

    public static long CreateType()
    {
        long pl = ParamListMZ5.CreateType();
        long refT = RefMZ5.CreateType();
        try
        {
            long t = H5T.create(H5T.class_t.COMPOUND, (IntPtr)Marshal.SizeOf<BinaryDataMZ5>());
            H5T.insert(t, "xParams",           Marshal.OffsetOf<BinaryDataMZ5>(nameof(XParamList)),           pl);
            H5T.insert(t, "yParams",           Marshal.OffsetOf<BinaryDataMZ5>(nameof(YParamList)),           pl);
            H5T.insert(t, "xrefDataProcessing", Marshal.OffsetOf<BinaryDataMZ5>(nameof(XDataProcessingRefID)), refT);
            H5T.insert(t, "yrefDataProcessing", Marshal.OffsetOf<BinaryDataMZ5>(nameof(YDataProcessingRefID)), refT);
            return t;
        }
        finally { H5T.close(refT); H5T.close(pl); }
    }
}
