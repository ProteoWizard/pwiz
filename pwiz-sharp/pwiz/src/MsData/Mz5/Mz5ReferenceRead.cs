using System;
using System.Collections.Generic;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Samples;
using Pwiz.Data.MsData.Sources;
using HDF.PInvoke;

#pragma warning disable CA1806 // HDF5 close() ints
#pragma warning disable CS1591 // internals; class summary covers the API

namespace Pwiz.Data.MsData.Mz5;

/// <summary>
/// Reads an open <see cref="Mz5Connection"/> into an <see cref="MSData"/>.
/// Port of pwiz cpp's <c>ReferenceRead_mz5</c>: walks every document-level
/// metadata dataset in dependency order (CVs → CVReference → CVParam /
/// UserParam / RefParam → ParamGroups → SourceFiles / Software /
/// DataProcessings / Samples / ScanSettings / InstrumentConfigurations →
/// Run), resolving cross-table references as it goes.
/// </summary>
/// <remarks>
/// Spectrum + Chromatogram lists will be read as ISpectrumList /
/// chromatogram-list adapters that hold a reference to the connection and
/// load binary arrays lazily on Get(). Right now Fill() populates everything
/// except those two lists — those land in the follow-up.
/// </remarks>
public sealed class Mz5ReferenceRead
{
    private readonly MSData _msd;

    // CVReference table: each row maps a (prefix, accession) pair to a CVID
    // (looked up + memoized via CvLookup.CvTermInfo).
    private CVRefMZ5[] _cvRefs = Array.Empty<CVRefMZ5>();
    private readonly Dictionary<int, CVID> _cvidCache = new();

    // The three global param tables. Each row in another dataset's
    // ParamListMZ5 (start,end) slices these arrays.
    private CVParamMZ5[] _cvParams = Array.Empty<CVParamMZ5>();
    private UserParamMZ5[] _userParams = Array.Empty<UserParamMZ5>();
    private RefMZ5[] _refParams = Array.Empty<RefMZ5>();

    public Mz5ReferenceRead(MSData msd)
    {
        _msd = msd;
    }

    /// <summary>Populate <see cref="_msd"/> from the connection. Caller owns
    /// the connection's lifetime — we don't dispose it.</summary>
    public void Fill(Mz5Connection conn)
    {
        // 1) CV list — minimal type registrations
        if (conn.Has(Mz5Datasets.ControlledVocabulary))
        {
            long t = ContVocabMZ5.CreateType();
            try
            {
                var cvs = conn.ReadFull<ContVocabMZ5>(Mz5Datasets.ControlledVocabulary, t);
                foreach (var c in cvs)
                {
                    _msd.CVs.Add(new CV
                    {
                        Uri = Mz5StringMarshal.FromVlen(c.Uri),
                        FullName = Mz5StringMarshal.FromVlen(c.FullName),
                        Id = Mz5StringMarshal.FromVlen(c.Id),
                        Version = Mz5StringMarshal.FromVlen(c.Version),
                    });
                }
                conn.VlenReclaim(cvs, t);
            }
            finally { H5T.close(t); }
        }

        // 2) CVReference lookup table (lazy CVID resolution per row)
        if (conn.Has(Mz5Datasets.CVReference))
        {
            long t = CVRefMZ5.CreateType();
            try
            {
                _cvRefs = conn.ReadFull<CVRefMZ5>(Mz5Datasets.CVReference, t);
                // Don't reclaim — we hold IntPtr to prefix/name strings for
                // the lifetime of the read. Reclaimed at end.
            }
            finally { H5T.close(t); }
        }

        // 3) Param tables — load raw, slice later
        if (conn.Has(Mz5Datasets.CVParam))
        {
            long t = CVParamMZ5.CreateType();
            try { _cvParams = conn.ReadFull<CVParamMZ5>(Mz5Datasets.CVParam, t); }
            finally { H5T.close(t); }
        }
        if (conn.Has(Mz5Datasets.UserParam))
        {
            long t = UserParamMZ5.CreateType();
            try { _userParams = conn.ReadFull<UserParamMZ5>(Mz5Datasets.UserParam, t); }
            finally { H5T.close(t); }
        }
        if (conn.Has(Mz5Datasets.RefParam))
        {
            long t = RefMZ5.CreateType();
            try { _refParams = conn.ReadFull<RefMZ5>(Mz5Datasets.RefParam, t); }
            finally { H5T.close(t); }
        }

        // 4) ParamGroups (must come before anything that references them)
        if (conn.Has(Mz5Datasets.ParamGroups))
        {
            long t = ParamGroupMZ5.CreateType();
            try
            {
                var groups = conn.ReadFull<ParamGroupMZ5>(Mz5Datasets.ParamGroups, t);
                foreach (var g in groups)
                {
                    var pg = new ParamGroup(Mz5StringMarshal.FromVlen(g.Id) ?? string.Empty);
                    FillParamContainer(pg, g.ParamList);
                    _msd.ParamGroups.Add(pg);
                }
                conn.VlenReclaim(groups, t);
            }
            finally { H5T.close(t); }
        }

        // 5) FileContent (single-row dataset)
        if (conn.Has(Mz5Datasets.FileContent))
        {
            long t = ParamListMZ5.CreateType();
            try
            {
                var fc = conn.ReadFull<ParamListMZ5>(Mz5Datasets.FileContent, t);
                if (fc.Length > 0)
                    FillParamContainer(_msd.FileDescription.FileContent, fc[0]);
            }
            finally { H5T.close(t); }
        }

        // 6) Contacts
        if (conn.Has(Mz5Datasets.Contact))
        {
            long t = ParamListMZ5.CreateType();
            try
            {
                var contacts = conn.ReadFull<ParamListMZ5>(Mz5Datasets.Contact, t);
                foreach (var pl in contacts)
                {
                    var contact = new Contact();
                    FillParamContainer(contact, pl);
                    _msd.FileDescription.Contacts.Add(contact);
                }
            }
            finally { H5T.close(t); }
        }

        // 7) SourceFiles
        if (conn.Has(Mz5Datasets.SourceFiles))
        {
            long t = SourceFileMZ5.CreateType();
            try
            {
                var rows = conn.ReadFull<SourceFileMZ5>(Mz5Datasets.SourceFiles, t);
                foreach (var s in rows)
                {
                    var sf = new SourceFile(
                        Mz5StringMarshal.FromVlen(s.Id),
                        Mz5StringMarshal.FromVlen(s.Name),
                        Mz5StringMarshal.FromVlen(s.Location));
                    FillParamContainer(sf, s.ParamList);
                    _msd.FileDescription.SourceFiles.Add(sf);
                }
                conn.VlenReclaim(rows, t);
            }
            finally { H5T.close(t); }
        }

        // 8) Software
        if (conn.Has(Mz5Datasets.Software))
        {
            long t = SoftwareMZ5.CreateType();
            try
            {
                var rows = conn.ReadFull<SoftwareMZ5>(Mz5Datasets.Software, t);
                foreach (var s in rows)
                {
                    var sw = new Software(Mz5StringMarshal.FromVlen(s.Id))
                    {
                        Version = Mz5StringMarshal.FromVlen(s.Version),
                    };
                    FillParamContainer(sw, s.ParamList);
                    _msd.Software.Add(sw);
                }
                conn.VlenReclaim(rows, t);
            }
            finally { H5T.close(t); }
        }

        // 9) Samples
        if (conn.Has(Mz5Datasets.Samples))
        {
            long t = SampleMZ5.CreateType();
            try
            {
                var rows = conn.ReadFull<SampleMZ5>(Mz5Datasets.Samples, t);
                foreach (var s in rows)
                {
                    var sample = new Sample(
                        Mz5StringMarshal.FromVlen(s.Id),
                        Mz5StringMarshal.FromVlen(s.Name));
                    FillParamContainer(sample, s.ParamList);
                    _msd.Samples.Add(sample);
                }
                conn.VlenReclaim(rows, t);
            }
            finally { H5T.close(t); }
        }

        // 10) DataProcessings — has a vlen ProcessingMethodList; we read the
        // top-level dataset but defer per-method-list expansion for now
        // (requires walking the Hvl pointer). MSData.DataProcessings list is
        // populated with empty-method DataProcessings to preserve count + id.
        if (conn.Has(Mz5Datasets.DataProcessing))
        {
            long t = DataProcessingMZ5.CreateType();
            try
            {
                var rows = conn.ReadFull<DataProcessingMZ5>(Mz5Datasets.DataProcessing, t);
                foreach (var dp in rows)
                {
                    var d = new DataProcessing(Mz5StringMarshal.FromVlen(dp.Id));
                    ExpandProcessingMethods(d, dp.ProcessingMethodList);
                    _msd.DataProcessings.Add(d);
                }
                conn.VlenReclaim(rows, t);
            }
            finally { H5T.close(t); }
        }

        // 11) ScanSettings (vlen sourceFileIDs + vlen targetList — we read the
        // top-level row, defer the vlen children for now)
        if (conn.Has(Mz5Datasets.ScanSetting))
        {
            long t = ScanSettingMZ5.CreateType();
            try
            {
                var rows = conn.ReadFull<ScanSettingMZ5>(Mz5Datasets.ScanSetting, t);
                foreach (var ss in rows)
                {
                    // pwiz-sharp's ScanSettings doesn't inherit ParamContainer
                    // (cpp does); just preserve the id + sourceFileRefs; cv/userParams
                    // on ScanSettings are rare and not modeled in our port.
                    var s = new ScanSettings(Mz5StringMarshal.FromVlen(ss.Id));
                    _msd.ScanSettings.Add(s);
                }
                conn.VlenReclaim(rows, t);
            }
            finally { H5T.close(t); }
        }

        // 12) InstrumentConfigurations
        if (conn.Has(Mz5Datasets.InstrumentConfiguration))
        {
            long t = InstrumentConfigurationMZ5.CreateType();
            try
            {
                var rows = conn.ReadFull<InstrumentConfigurationMZ5>(Mz5Datasets.InstrumentConfiguration, t);
                foreach (var ic in rows)
                {
                    var inst = new InstrumentConfiguration(Mz5StringMarshal.FromVlen(ic.Id));
                    FillParamContainer(inst, ic.ParamList);
                    ExpandComponents(inst, ic.Components);
                    if (ic.SoftwareRefID.RefID < _msd.Software.Count)
                        inst.Software = _msd.Software[(int)ic.SoftwareRefID.RefID];
                    _msd.InstrumentConfigurations.Add(inst);
                }
                conn.VlenReclaim(rows, t);
            }
            finally { H5T.close(t); }
        }

        // 13) Run — single row
        if (conn.Has(Mz5Datasets.Run))
        {
            long t = RunMZ5.CreateType();
            try
            {
                var rows = conn.ReadFull<RunMZ5>(Mz5Datasets.Run, t);
                if (rows.Length > 0)
                {
                    var r = rows[0];
                    _msd.Run.Id = Mz5StringMarshal.FromVlen(r.Id);
                    _msd.Run.StartTimeStamp = Mz5StringMarshal.FromVlen(r.StartTimeStamp);
                    _msd.Id = Mz5StringMarshal.FromVlen(r.Fid);
                    _msd.Accession = Mz5StringMarshal.FromVlen(r.Facc);
                    FillParamContainer(_msd.Run, r.ParamList);
                    if (r.DefaultInstrumentConfigurationRefID.RefID < _msd.InstrumentConfigurations.Count)
                        _msd.Run.DefaultInstrumentConfiguration =
                            _msd.InstrumentConfigurations[(int)r.DefaultInstrumentConfigurationRefID.RefID];
                    if (r.SourceFileRefID.RefID < _msd.FileDescription.SourceFiles.Count)
                        _msd.Run.DefaultSourceFile =
                            _msd.FileDescription.SourceFiles[(int)r.SourceFileRefID.RefID];
                    if (r.SampleRefID.RefID < _msd.Samples.Count)
                        _msd.Run.Sample = _msd.Samples[(int)r.SampleRefID.RefID];
                }
                conn.VlenReclaim(rows, t);
            }
            finally { H5T.close(t); }
        }

        // 14) Spectrum + Chromatogram lists — lazy adapters over the per-
        // spectrum/chromatogram metadata + index + payload datasets. Each
        // list AddRefs the connection in its ctor and Dispose()s its share
        // in DisposeCore; the connection actually closes when both shares
        // (plus the reader's original hold) are released.
        if (conn.Has(Mz5Datasets.SpectrumMetaData))
            _msd.Run.SpectrumList = new Mz5SpectrumList(conn, this);
        if (conn.Has(Mz5Datasets.ChromatogramMetaData))
            _msd.Run.ChromatogramList = new Mz5ChromatogramList(conn, this);

        // CVRefs hold IntPtr to HDF5-allocated strings; reclaim now that all
        // param-container fills are done.
        if (_cvRefs.Length > 0)
        {
            long t = CVRefMZ5.CreateType();
            try { Reclaim(_cvRefs, t); }
            finally { H5T.close(t); }
        }
    }

    private static void Reclaim<T>(T[] arr, long type) where T : unmanaged
    {
        // Mz5Connection.VlenReclaim would normally do this but we hold the
        // CVRef table for the lifetime of Fill; reclaim after.
        if (arr.Length == 0) return;
        ulong[] dims = { (ulong)arr.Length };
        long space = H5S.create_simple(1, dims, dims);
        try
        {
            unsafe
            {
                fixed (T* p = arr)
                {
                    H5D.vlen_reclaim(type, space, H5P.DEFAULT, (IntPtr)p);
                }
            }
        }
        finally { H5S.close(space); }
    }

    /// <summary>Resolve an mz5 CVRef-table index to a CVID via accession
    /// string lookup. Memoized — first call per index hits CvLookup, repeat
    /// calls are dictionary reads.</summary>
    private CVID CVIDByRef(uint index)
    {
        if (_cvidCache.TryGetValue((int)index, out var cached)) return cached;
        if (index >= _cvRefs.Length) return CVID.CVID_Unknown;
        var r = _cvRefs[index];
        string prefix = Mz5StringMarshal.FromVlen(r.Prefix);
        // cpp uses sprintf "%s:%07lu" — zero-pad to 7 digits.
        string id = $"{prefix}:{r.Accession:D7}";
        CVID cvid = CvLookup.CvTermInfo(id).Cvid;
        _cvidCache[(int)index] = cvid;
        return cvid;
    }

    /// <summary>Internal accessor for <see cref="Mz5SpectrumList"/>, which
    /// needs the same param-table slicing logic to attach cvParams to each
    /// spectrum it builds.</summary>
    internal void FillParamContainerInternal(ParamContainer pc, ParamListMZ5 list)
        => FillParamContainer(pc, list);

    /// <summary>Slice the three global param tables into a ParamContainer
    /// using the start/end ranges from <paramref name="list"/>.</summary>
    private void FillParamContainer(ParamContainer pc, ParamListMZ5 list)
    {
        // CVParams. cpp ReferenceRead_mz5::fill clears each target sub-list before repopulating
        // (guarded by a non-empty stored range), replacing any pre-seeded params (e.g. the unitless
        // MS_time_array that setTimeIntensityArrays emplaces). Without this we append and the seed
        // shadows the real MS_time_array(units=UO_minute), so the time unit reads back CVID_Unknown.
        if (list.CVParamEndID > list.CVParamStartID)
            pc.CVParams.Clear();
        for (uint i = list.CVParamStartID; i < list.CVParamEndID; i++)
        {
            if (i >= _cvParams.Length) break;
            var raw = _cvParams[i];
            string value;
            unsafe { value = Mz5StringMarshal.FromFixed(raw.Value, Mz5Configuration.CvParamValueLen); }
            var cv = new CVParam(CVIDByRef(raw.TypeCVRefID), value)
            {
                Units = CVIDByRef(raw.UnitCVRefID),
            };
            pc.CVParams.Add(cv);
        }
        // UserParams
        if (list.UserParamEndID > list.UserParamStartID)
            pc.UserParams.Clear();
        for (uint i = list.UserParamStartID; i < list.UserParamEndID; i++)
        {
            if (i >= _userParams.Length) break;
            var raw = _userParams[i];
            string name, value, type;
            unsafe
            {
                name  = Mz5StringMarshal.FromFixed(raw.Name,  Mz5Configuration.UserParamNameLen);
                value = Mz5StringMarshal.FromFixed(raw.Value, Mz5Configuration.UserParamValueLen);
                type  = Mz5StringMarshal.FromFixed(raw.Type,  Mz5Configuration.UserParamTypeLen);
            }
            pc.UserParams.Add(new UserParam(name, value, type) { Units = CVIDByRef(raw.UnitCVRefID) });
        }
        // ParamGroup refs
        if (list.RefParamGroupEndID > list.RefParamGroupStartID)
            pc.ParamGroups.Clear();
        for (uint i = list.RefParamGroupStartID; i < list.RefParamGroupEndID; i++)
        {
            if (i >= _refParams.Length) break;
            uint pgIdx = _refParams[i].RefID;
            if (pgIdx < _msd.ParamGroups.Count)
                pc.ParamGroups.Add(_msd.ParamGroups[(int)pgIdx]);
        }
    }

    /// <summary>Walk the vlen array of ProcessingMethodMZ5 inside a
    /// DataProcessing's processingMethodList field and populate the
    /// DataProcessing.</summary>
    private unsafe void ExpandProcessingMethods(DataProcessing dest, Hvl methodList)
    {
        ulong n = (ulong)methodList.Length;
        if (n == 0 || methodList.Data == IntPtr.Zero) return;
        var rows = new ProcessingMethodMZ5[n];
        int eltSize = System.Runtime.InteropServices.Marshal.SizeOf<ProcessingMethodMZ5>();
        byte* src = (byte*)methodList.Data;
        for (ulong i = 0; i < n; i++)
        {
            rows[i] = System.Runtime.InteropServices.Marshal.PtrToStructure<ProcessingMethodMZ5>(
                (IntPtr)(src + (long)i * eltSize));
        }
        foreach (var pm in rows)
        {
            var method = new ProcessingMethod { Order = (int)pm.Order };
            FillParamContainer(method, pm.ParamList);
            if (pm.SoftwareRefID.RefID < _msd.Software.Count)
                method.Software = _msd.Software[(int)pm.SoftwareRefID.RefID];
            dest.ProcessingMethods.Add(method);
        }
    }

    /// <summary>Pull ComponentMZ5 entries out of an InstrumentConfiguration's
    /// vlen source/analyzer/detector lists into ComponentList entries.</summary>
    private unsafe void ExpandComponents(InstrumentConfiguration inst, ComponentsMZ5 comps)
    {
        ReadComponentHvl(inst, comps.Sources);
        ReadComponentHvl(inst, comps.Analyzers);
        ReadComponentHvl(inst, comps.Detectors);
    }

    private unsafe void ReadComponentHvl(InstrumentConfiguration inst, Hvl hvl)
    {
        ulong n = (ulong)hvl.Length;
        if (n == 0 || hvl.Data == IntPtr.Zero) return;
        int eltSize = System.Runtime.InteropServices.Marshal.SizeOf<ComponentMZ5>();
        byte* src = (byte*)hvl.Data;
        for (ulong i = 0; i < n; i++)
        {
            var raw = System.Runtime.InteropServices.Marshal.PtrToStructure<ComponentMZ5>(
                (IntPtr)(src + (long)i * eltSize));
            // ComponentType is inferred from the cvParams (source/analyzer/detector
            // CV term children of MS:1000462). The simplest emit: an empty
            // Component whose ParamContainer carries the type-bearing cvParam.
            // We don't have the source/analyzer/detector enum at this level;
            // cpp's fillComponent dispatches on which list it came from.
            var component = new Component(CVID.CVID_Unknown, (int)raw.Order);
            FillParamContainer(component, raw.ParamList);
            inst.ComponentList.Add(component);
        }
    }
}
