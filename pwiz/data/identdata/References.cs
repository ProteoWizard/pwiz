namespace Pwiz.Data.IdentData;

/// <summary>
/// Resolves cross-references inside an <see cref="IdentData"/> tree. Port of
/// <c>pwiz::identdata::References</c>.
/// </summary>
/// <remarks>
/// <para>mzIdentML uses id-references between elements: e.g. <c>SpectrumIdentificationItem</c>
/// references its source <c>Peptide</c> by <c>peptide_ref</c>. After parsing, schema objects
/// hold *stub* references (just <see cref="Identifiable.Id"/> populated) for any forward refs.
/// This utility walks the tree once to build id→object lookup tables and then walks again to
/// replace each stub with the fully-populated object from the table.</para>
/// <para>Idempotent: running <c>Resolve</c> twice produces the same tree. Callers who
/// build an IdentData programmatically (and don't want to plumb refs by hand) can populate the
/// list-owners (Peptides, DBSequences, etc.) and assign id-only stubs to cross-reference
/// fields, then call <c>Resolve</c> to link everything up.</para>
/// </remarks>
public static class References
{
    /// <summary>Resolves all id-references inside <paramref name="ident"/> in place.</summary>
    public static void Resolve(IdentData ident)
    {
        ArgumentNullException.ThrowIfNull(ident);
        var tables = LookupTables.From(ident);
        ResolveInto(ident, tables);
    }

    /// <summary>Resolves a single <see cref="ContactRole"/>'s contact reference.</summary>
    public static void Resolve(ContactRole cr, IdentData ident)
    {
        ArgumentNullException.ThrowIfNull(cr);
        ArgumentNullException.ThrowIfNull(ident);
        var tables = LookupTables.From(ident);
        ResolveContactRole(cr, tables);
    }

    private sealed class LookupTables
    {
        public Dictionary<string, AnalysisSoftware> Software { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Contact> Contact { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Organization> Organization { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Sample> Sample { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, DBSequence> DbSequence { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Peptide> Peptide { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, PeptideEvidence> PeptideEvidence { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, SearchDatabase> SearchDb { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, SpectraData> SpectraData { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, SpectrumIdentificationList> SpectrumIdList { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, SpectrumIdentificationProtocol> SpectrumIdProtocol { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, ProteinDetectionProtocol> ProteinDetProtocol { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, ProteinDetectionList> ProteinDetList { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Measure> Measure { get; } = new(StringComparer.Ordinal);

        public static LookupTables From(IdentData ident)
        {
            var t = new LookupTables();
            foreach (var sw in ident.AnalysisSoftwareList) Add(t.Software, sw.Id, sw);
            foreach (var c in ident.AuditCollection)
            {
                Add(t.Contact, c.Id, c);
                if (c is Organization o) Add(t.Organization, o.Id, o);
            }
            foreach (var s in ident.AnalysisSampleCollection.Samples) Add(t.Sample, s.Id, s);
            foreach (var dbs in ident.SequenceCollection.DBSequences) Add(t.DbSequence, dbs.Id, dbs);
            foreach (var p in ident.SequenceCollection.Peptides) Add(t.Peptide, p.Id, p);
            foreach (var pe in ident.SequenceCollection.PeptideEvidence) Add(t.PeptideEvidence, pe.Id, pe);
            foreach (var sip in ident.AnalysisProtocolCollection.SpectrumIdentificationProtocol) Add(t.SpectrumIdProtocol, sip.Id, sip);
            foreach (var pdp in ident.AnalysisProtocolCollection.ProteinDetectionProtocol) Add(t.ProteinDetProtocol, pdp.Id, pdp);
            foreach (var sd in ident.DataCollection.Inputs.SpectraData) Add(t.SpectraData, sd.Id, sd);
            foreach (var db in ident.DataCollection.Inputs.SearchDatabase) Add(t.SearchDb, db.Id, db);
            foreach (var sil in ident.DataCollection.AnalysisData.SpectrumIdentificationList)
            {
                Add(t.SpectrumIdList, sil.Id, sil);
                foreach (var m in sil.FragmentationTable) Add(t.Measure, m.Id, m);
            }
            if (ident.DataCollection.AnalysisData.ProteinDetectionListPtr is { } pdl)
                Add(t.ProteinDetList, pdl.Id, pdl);
            return t;
        }

        private static void Add<T>(Dictionary<string, T> map, string id, T value)
        {
            if (!string.IsNullOrEmpty(id)) map[id] = value;
        }
    }

    private static void ResolveInto(IdentData ident, LookupTables t)
    {
        foreach (var sw in ident.AnalysisSoftwareList)
            ResolveContactRole(sw.ContactRolePtr, t);

        ResolveContactRole(ident.Provider.ContactRolePtr, t);
        ident.Provider.AnalysisSoftwarePtr = ResolveRef(ident.Provider.AnalysisSoftwarePtr, t.Software);

        foreach (var c in ident.AuditCollection)
        {
            switch (c)
            {
                case Person p:
                    for (int i = 0; i < p.Affiliations.Count; i++)
                        p.Affiliations[i] = ResolveRefIpc(p.Affiliations[i], t.Organization)!;
                    break;
                case Organization o:
                    o.Parent = ResolveRefIpc(o.Parent, t.Organization);
                    break;
            }
        }

        foreach (var s in ident.AnalysisSampleCollection.Samples)
        {
            foreach (var cr in s.ContactRole) ResolveContactRole(cr, t);
            for (int i = 0; i < s.SubSamples.Count; i++)
                s.SubSamples[i] = ResolveRefIpc(s.SubSamples[i], t.Sample)!;
        }

        foreach (var dbs in ident.SequenceCollection.DBSequences)
            dbs.SearchDatabasePtr = ResolveRefIpc(dbs.SearchDatabasePtr, t.SearchDb);

        foreach (var pe in ident.SequenceCollection.PeptideEvidence)
        {
            pe.PeptidePtr = ResolveRefIpc(pe.PeptidePtr, t.Peptide);
            pe.DBSequencePtr = ResolveRefIpc(pe.DBSequencePtr, t.DbSequence);
        }

        foreach (var sip in ident.AnalysisProtocolCollection.SpectrumIdentificationProtocol)
            sip.AnalysisSoftwarePtr = ResolveRef(sip.AnalysisSoftwarePtr, t.Software);
        foreach (var pdp in ident.AnalysisProtocolCollection.ProteinDetectionProtocol)
            pdp.AnalysisSoftwarePtr = ResolveRef(pdp.AnalysisSoftwarePtr, t.Software);

        foreach (var si in ident.AnalysisCollection.SpectrumIdentification)
        {
            si.SpectrumIdentificationProtocolPtr = ResolveRef(si.SpectrumIdentificationProtocolPtr, t.SpectrumIdProtocol);
            si.SpectrumIdentificationListPtr = ResolveRefIpc(si.SpectrumIdentificationListPtr, t.SpectrumIdList);
            for (int i = 0; i < si.InputSpectra.Count; i++)
                si.InputSpectra[i] = ResolveRef(si.InputSpectra[i], t.SpectraData)!;
            for (int i = 0; i < si.SearchDatabase.Count; i++)
                si.SearchDatabase[i] = ResolveRefIpc(si.SearchDatabase[i], t.SearchDb)!;
        }

        var pd = ident.AnalysisCollection.ProteinDetection;
        pd.ProteinDetectionProtocolPtr = ResolveRef(pd.ProteinDetectionProtocolPtr, t.ProteinDetProtocol);
        pd.ProteinDetectionListPtr = ResolveRefIpc(pd.ProteinDetectionListPtr, t.ProteinDetList);
        for (int i = 0; i < pd.InputSpectrumIdentifications.Count; i++)
            pd.InputSpectrumIdentifications[i] = ResolveRefIpc(pd.InputSpectrumIdentifications[i], t.SpectrumIdList)!;

        foreach (var sil in ident.DataCollection.AnalysisData.SpectrumIdentificationList)
        {
            foreach (var sir in sil.SpectrumIdentificationResult)
            {
                sir.SpectraDataPtr = ResolveRef(sir.SpectraDataPtr, t.SpectraData);
                foreach (var sii in sir.SpectrumIdentificationItem)
                {
                    sii.PeptidePtr = ResolveRefIpc(sii.PeptidePtr, t.Peptide);
                    for (int i = 0; i < sii.PeptideEvidencePtr.Count; i++)
                        sii.PeptideEvidencePtr[i] = ResolveRefIpc(sii.PeptideEvidencePtr[i], t.PeptideEvidence)!;
                    foreach (var ion in sii.Fragmentation)
                        foreach (var fa in ion.FragmentArray)
                            fa.MeasurePtr = ResolveRefIpc(fa.MeasurePtr, t.Measure);
                }
            }
        }

        if (ident.DataCollection.AnalysisData.ProteinDetectionListPtr is { } proteinList)
        {
            foreach (var pag in proteinList.ProteinAmbiguityGroup)
            {
                foreach (var pdh in pag.ProteinDetectionHypothesis)
                {
                    pdh.DBSequencePtr = ResolveRefIpc(pdh.DBSequencePtr, t.DbSequence);
                    foreach (var ph in pdh.PeptideHypothesis)
                        ph.PeptideEvidencePtr = ResolveRefIpc(ph.PeptideEvidencePtr, t.PeptideEvidence);
                }
            }
        }
    }

    private static void ResolveContactRole(ContactRole? cr, LookupTables t)
    {
        if (cr is null) return;
        cr.ContactPtr = ResolveRefIpc(cr.ContactPtr, t.Contact);
    }

    /// <summary>Replaces a stub reference (one with a non-empty Id but otherwise empty) with the
    /// populated entry from <paramref name="map"/>. Returns the populated entry when found,
    /// otherwise returns the original (which may be a stub or null).</summary>
    private static T? ResolveRef<T>(T? stub, Dictionary<string, T> map) where T : Identifiable
    {
        if (stub is null) return null;
        if (string.IsNullOrEmpty(stub.Id)) return stub;
        return map.TryGetValue(stub.Id, out var hit) ? hit : stub;
    }

    /// <summary>Same as <see cref="ResolveRef{T}(T, Dictionary{string, T})"/> but for
    /// <see cref="IdentifiableParamContainer"/> descendants — separate overload because the two
    /// base classes don't share a common ancestor with an Id property in pwiz-sharp's port.</summary>
    private static T? ResolveRefIpc<T>(T? stub, Dictionary<string, T> map) where T : IdentifiableParamContainer
    {
        if (stub is null) return null;
        if (string.IsNullOrEmpty(stub.Id)) return stub;
        return map.TryGetValue(stub.Id, out var hit) ? hit : stub;
    }
}
