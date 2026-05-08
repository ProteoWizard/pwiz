using System.Globalization;
using System.Xml;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData.Mzid;

/// <summary>
/// Reads an mzIdentML file into an <see cref="IdentData"/> tree. Streaming XmlReader-based —
/// builds only the schema subset currently ported (analysis software, sequence collection,
/// data collection / analysis data / spectrum identification chain). Unknown elements are
/// skipped.
/// </summary>
public sealed class MzidReader
{
    /// <summary>Reads <paramref name="stream"/> as mzIdentML XML and populates
    /// <paramref name="target"/>.</summary>
#pragma warning disable CA1822 // kept as instance for symmetry with PepXmlReader / future buffered state
    public void ReadInto(Stream stream, IdentData target)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(target);

        var settings = new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Ignore,
        };

        using var xr = XmlReader.Create(stream, settings);
        var refs = new ReferenceMaps();

        while (xr.Read())
        {
            if (xr.NodeType != XmlNodeType.Element) continue;
            switch (xr.LocalName)
            {
                case "MzIdentML":
                    target.Id = xr.GetAttribute("id") ?? string.Empty;
                    target.Name = xr.GetAttribute("name") ?? string.Empty;
                    target.CreationDate = xr.GetAttribute("creationDate") ?? string.Empty;
                    break;
                case "AnalysisSoftware":
                    var sw = ReadAnalysisSoftware(xr);
                    target.AnalysisSoftwareList.Add(sw);
                    if (!string.IsNullOrEmpty(sw.Id)) refs.Software[sw.Id] = sw;
                    break;
                case "Provider":
                    target.Provider.Id = xr.GetAttribute("id") ?? string.Empty;
                    target.Provider.Name = xr.GetAttribute("name") ?? string.Empty;
                    var asRef = xr.GetAttribute("analysisSoftware_ref");
                    if (!string.IsNullOrEmpty(asRef) && refs.Software.TryGetValue(asRef, out var providerSw))
                        target.Provider.AnalysisSoftwarePtr = providerSw;
                    break;
                case "DBSequence":
                    var dbs = ReadDBSequence(xr, refs.SearchDb);
                    target.SequenceCollection.DBSequences.Add(dbs);
                    if (!string.IsNullOrEmpty(dbs.Id)) refs.DbSequence[dbs.Id] = dbs;
                    break;
                case "Peptide":
                    var pep = ReadPeptide(xr);
                    target.SequenceCollection.Peptides.Add(pep);
                    if (!string.IsNullOrEmpty(pep.Id)) refs.Peptide[pep.Id] = pep;
                    break;
                case "PeptideEvidence":
                    var pe = ReadPeptideEvidence(xr, refs.Peptide, refs.DbSequence);
                    target.SequenceCollection.PeptideEvidence.Add(pe);
                    if (!string.IsNullOrEmpty(pe.Id)) refs.PeptideEvidence[pe.Id] = pe;
                    break;
                case "SpectraData":
                    var sd = ReadSpectraData(xr);
                    target.DataCollection.Inputs.SpectraData.Add(sd);
                    if (!string.IsNullOrEmpty(sd.Id)) refs.SpectraData[sd.Id] = sd;
                    break;
                case "SearchDatabase":
                    var db = ReadSearchDatabase(xr);
                    target.DataCollection.Inputs.SearchDatabase.Add(db);
                    if (!string.IsNullOrEmpty(db.Id)) refs.SearchDb[db.Id] = db;
                    break;
                case "SpectrumIdentificationList":
                    var sil = ReadSpectrumIdentificationList(xr, refs);
                    target.DataCollection.AnalysisData.SpectrumIdentificationList.Add(sil);
                    if (!string.IsNullOrEmpty(sil.Id)) refs.SpectrumIdList[sil.Id] = sil;
                    break;
                case "SpectrumIdentificationProtocol":
                    var sip = ReadSpectrumIdentificationProtocol(xr, refs);
                    target.AnalysisProtocolCollection.SpectrumIdentificationProtocol.Add(sip);
                    if (!string.IsNullOrEmpty(sip.Id)) refs.SpectrumIdProtocol[sip.Id] = sip;
                    break;
                case "ProteinDetectionProtocol":
                    var pdp = ReadProteinDetectionProtocol(xr, refs);
                    target.AnalysisProtocolCollection.ProteinDetectionProtocol.Add(pdp);
                    if (!string.IsNullOrEmpty(pdp.Id)) refs.ProteinDetProtocol[pdp.Id] = pdp;
                    break;
                case "SpectrumIdentification":
                    target.AnalysisCollection.SpectrumIdentification.Add(ReadSpectrumIdentification(xr, refs));
                    break;
                case "ProteinDetection":
                    ReadProteinDetectionInto(xr, target.AnalysisCollection.ProteinDetection, refs);
                    break;
                case "ProteinDetectionList":
                    target.DataCollection.AnalysisData.ProteinDetectionListPtr =
                        ReadProteinDetectionList(xr, refs);
                    break;
            }
        }
    }

    private sealed class ReferenceMaps
    {
        public Dictionary<string, AnalysisSoftware> Software { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, DBSequence> DbSequence { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Peptide> Peptide { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, PeptideEvidence> PeptideEvidence { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, SpectraData> SpectraData { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, SearchDatabase> SearchDb { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, SpectrumIdentificationList> SpectrumIdList { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, SpectrumIdentificationProtocol> SpectrumIdProtocol { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, ProteinDetectionProtocol> ProteinDetProtocol { get; } = new(StringComparer.Ordinal);
    }

    private static AnalysisSoftware ReadAnalysisSoftware(XmlReader xr)
    {
        var sw = new AnalysisSoftware
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
            Version = xr.GetAttribute("version") ?? string.Empty,
            Uri = xr.GetAttribute("URI") ?? string.Empty,
        };
        if (xr.IsEmptyElement) return sw;

        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "Customizations") sw.Customizations = sub.ReadElementContentAsString();
            else if (sub.LocalName == "SoftwareName")
                ReadParamContainerInto(sub, sw.SoftwareName);
            // ContactRole intentionally skipped for now.
        }
        return sw;
    }

    private static DBSequence ReadDBSequence(XmlReader xr,
        Dictionary<string, SearchDatabase> searchDbById)
    {
        var d = new DBSequence
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
            Accession = xr.GetAttribute("accession") ?? string.Empty,
        };
        if (int.TryParse(xr.GetAttribute("length"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var len))
            d.Length = len;
        var sdRef = xr.GetAttribute("searchDatabase_ref");
        if (!string.IsNullOrEmpty(sdRef) && searchDbById.TryGetValue(sdRef, out var db))
            d.SearchDatabasePtr = db;

        if (xr.IsEmptyElement) return d;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "Seq") d.Seq = sub.ReadElementContentAsString();
            else if (sub.LocalName is "cvParam" or "userParam") ReadOneParamInto(sub, d);
        }
        return d;
    }

    private static Peptide ReadPeptide(XmlReader xr)
    {
        var p = new Peptide
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
        };
        if (xr.IsEmptyElement) return p;

        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            switch (sub.LocalName)
            {
                case "PeptideSequence":
                    p.PeptideSequence = sub.ReadElementContentAsString();
                    break;
                case "Modification":
                    p.Modifications.Add(ReadModification(sub));
                    break;
                case "cvParam":
                case "userParam":
                    ReadOneParamInto(sub, p);
                    break;
            }
        }
        return p;
    }

    private static Modification ReadModification(XmlReader xr)
    {
        var m = new Modification();
        if (int.TryParse(xr.GetAttribute("location"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var loc))
            m.Location = loc;
        var residues = xr.GetAttribute("residues");
        if (!string.IsNullOrEmpty(residues))
            foreach (var c in residues) m.Residues.Add(c);
        if (double.TryParse(xr.GetAttribute("avgMassDelta"), NumberStyles.Float, CultureInfo.InvariantCulture, out var avg))
            m.AvgMassDelta = avg;
        if (double.TryParse(xr.GetAttribute("monoisotopicMassDelta"), NumberStyles.Float, CultureInfo.InvariantCulture, out var mono))
            m.MonoisotopicMassDelta = mono;
        if (xr.IsEmptyElement) return m;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName is "cvParam" or "userParam") ReadOneParamInto(sub, m);
        }
        return m;
    }

    private static PeptideEvidence ReadPeptideEvidence(XmlReader xr,
        Dictionary<string, Peptide> peptideById, Dictionary<string, DBSequence> dbSequenceById)
    {
        var pe = new PeptideEvidence
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
        };
        var pepRef = xr.GetAttribute("peptide_ref");
        if (!string.IsNullOrEmpty(pepRef) && peptideById.TryGetValue(pepRef, out var pep))
            pe.PeptidePtr = pep;
        var dbRef = xr.GetAttribute("dBSequence_ref");
        if (!string.IsNullOrEmpty(dbRef) && dbSequenceById.TryGetValue(dbRef, out var db))
            pe.DBSequencePtr = db;
        if (int.TryParse(xr.GetAttribute("start"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)) pe.Start = s;
        if (int.TryParse(xr.GetAttribute("end"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var e)) pe.End = e;
        var pre = xr.GetAttribute("pre"); if (!string.IsNullOrEmpty(pre)) pe.Pre = pre[0];
        var post = xr.GetAttribute("post"); if (!string.IsNullOrEmpty(post)) pe.Post = post[0];
        if (int.TryParse(xr.GetAttribute("frame"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var f)) pe.Frame = f;
        if (bool.TryParse(xr.GetAttribute("isDecoy"), out var d2)) pe.IsDecoy = d2;
        if (xr.IsEmptyElement) return pe;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName is "cvParam" or "userParam") ReadOneParamInto(sub, pe);
        }
        return pe;
    }

    private static SpectraData ReadSpectraData(XmlReader xr)
    {
        var sd = new SpectraData
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
            Location = xr.GetAttribute("location") ?? string.Empty,
        };
        if (xr.IsEmptyElement) return sd;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "FileFormat")
            {
                using var ff = sub.ReadSubtree();
                while (ff.Read())
                    if (ff.NodeType == XmlNodeType.Element && ff.LocalName == "cvParam")
                        sd.FileFormat = ReadCvParam(ff);
            }
            else if (sub.LocalName == "SpectrumIDFormat")
            {
                using var sf = sub.ReadSubtree();
                while (sf.Read())
                    if (sf.NodeType == XmlNodeType.Element && sf.LocalName == "cvParam")
                        sd.SpectrumIDFormat = ReadCvParam(sf);
            }
        }
        return sd;
    }

    private static SearchDatabase ReadSearchDatabase(XmlReader xr)
    {
        var d = new SearchDatabase
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
            Location = xr.GetAttribute("location") ?? string.Empty,
            Version = xr.GetAttribute("version") ?? string.Empty,
            ReleaseDate = xr.GetAttribute("releaseDate") ?? string.Empty,
        };
        if (long.TryParse(xr.GetAttribute("numDatabaseSequences"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var nseq))
            d.NumDatabaseSequences = nseq;
        if (long.TryParse(xr.GetAttribute("numResidues"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var nres))
            d.NumResidues = nres;
        if (xr.IsEmptyElement) return d;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName is "cvParam" or "userParam") ReadOneParamInto(sub, d);
        }
        return d;
    }

    private static SpectrumIdentificationList ReadSpectrumIdentificationList(XmlReader xr, ReferenceMaps refs)
    {
        var sil = new SpectrumIdentificationList
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
        };
        if (long.TryParse(xr.GetAttribute("numSequencesSearched"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var nseq))
            sil.NumSequencesSearched = nseq;
        if (xr.IsEmptyElement) return sil;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "SpectrumIdentificationResult")
                sil.SpectrumIdentificationResult.Add(ReadSpectrumIdentificationResult(sub, refs));
            else if (sub.LocalName is "cvParam" or "userParam")
                ReadOneParamInto(sub, sil);
        }
        return sil;
    }

    private static SpectrumIdentificationResult ReadSpectrumIdentificationResult(XmlReader xr, ReferenceMaps refs)
    {
        var sir = new SpectrumIdentificationResult
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
            SpectrumID = xr.GetAttribute("spectrumID") ?? string.Empty,
        };
        var sdRef = xr.GetAttribute("spectraData_ref");
        if (!string.IsNullOrEmpty(sdRef) && refs.SpectraData.TryGetValue(sdRef, out var sd))
            sir.SpectraDataPtr = sd;
        if (xr.IsEmptyElement) return sir;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "SpectrumIdentificationItem")
                sir.SpectrumIdentificationItem.Add(ReadSpectrumIdentificationItem(sub, refs));
            else if (sub.LocalName is "cvParam" or "userParam")
                ReadOneParamInto(sub, sir);
        }
        return sir;
    }

    private static SpectrumIdentificationItem ReadSpectrumIdentificationItem(XmlReader xr, ReferenceMaps refs)
    {
        var sii = new SpectrumIdentificationItem
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
        };
        if (int.TryParse(xr.GetAttribute("chargeState"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var z)) sii.ChargeState = z;
        if (double.TryParse(xr.GetAttribute("experimentalMassToCharge"), NumberStyles.Float, CultureInfo.InvariantCulture, out var em)) sii.ExperimentalMassToCharge = em;
        if (double.TryParse(xr.GetAttribute("calculatedMassToCharge"), NumberStyles.Float, CultureInfo.InvariantCulture, out var cm)) sii.CalculatedMassToCharge = cm;
        if (double.TryParse(xr.GetAttribute("calculatedPI"), NumberStyles.Float, CultureInfo.InvariantCulture, out var pi)) sii.CalculatedPI = pi;
        if (int.TryParse(xr.GetAttribute("rank"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)) sii.Rank = r;
        if (bool.TryParse(xr.GetAttribute("passThreshold"), out var pt)) sii.PassThreshold = pt;
        var pepRef = xr.GetAttribute("peptide_ref");
        if (!string.IsNullOrEmpty(pepRef) && refs.Peptide.TryGetValue(pepRef, out var p))
            sii.PeptidePtr = p;
        if (xr.IsEmptyElement) return sii;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "PeptideEvidenceRef")
            {
                var peRef = sub.GetAttribute("peptideEvidence_ref");
                if (!string.IsNullOrEmpty(peRef) && refs.PeptideEvidence.TryGetValue(peRef, out var pe))
                    sii.PeptideEvidencePtr.Add(pe);
            }
            else if (sub.LocalName is "cvParam" or "userParam")
            {
                ReadOneParamInto(sub, sii);
            }
        }
        return sii;
    }

    // -------- protocol / analysis-collection helpers --------

    private static SpectrumIdentificationProtocol ReadSpectrumIdentificationProtocol(XmlReader xr, ReferenceMaps refs)
    {
        var sip = new SpectrumIdentificationProtocol
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
        };
        var asRef = xr.GetAttribute("analysisSoftware_ref");
        if (!string.IsNullOrEmpty(asRef) && refs.Software.TryGetValue(asRef, out var sw))
            sip.AnalysisSoftwarePtr = sw;
        if (xr.IsEmptyElement) return sip;

        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            switch (sub.LocalName)
            {
                case "SearchType":
                    using (var st = sub.ReadSubtree())
                        while (st.Read())
                            if (st.NodeType == XmlNodeType.Element && st.LocalName == "cvParam")
                                sip.SearchType = ReadCvParam(st);
                    break;
                case "AdditionalSearchParams":
                    ReadParamContainerInto(sub, sip.AdditionalSearchParams);
                    break;
                case "ModificationParams":
                    using (var mp = sub.ReadSubtree())
                        while (mp.Read())
                            if (mp.NodeType == XmlNodeType.Element && mp.LocalName == "SearchModification")
                                sip.ModificationParams.Add(ReadSearchModification(mp));
                    break;
                case "Enzymes":
                    var indep = sub.GetAttribute("independent");
                    if (bool.TryParse(indep, out var indepVal)) sip.Enzymes.Independent = indepVal;
                    using (var ez = sub.ReadSubtree())
                        while (ez.Read())
                            if (ez.NodeType == XmlNodeType.Element && ez.LocalName == "Enzyme")
                                sip.Enzymes.EnzymeList.Add(ReadEnzyme(ez));
                    break;
                case "MassTable":
                    sip.MassTable.Add(ReadMassTable(sub));
                    break;
                case "FragmentTolerance":
                    ReadParamContainerInto(sub, sip.FragmentTolerance);
                    break;
                case "ParentTolerance":
                    ReadParamContainerInto(sub, sip.ParentTolerance);
                    break;
                case "Threshold":
                    ReadParamContainerInto(sub, sip.Threshold);
                    break;
                case "DatabaseFilters":
                    using (var df = sub.ReadSubtree())
                        while (df.Read())
                            if (df.NodeType == XmlNodeType.Element && df.LocalName == "Filter")
                                sip.DatabaseFilters.Add(ReadFilter(df));
                    break;
                case "DatabaseTranslation":
                    sip.DatabaseTranslation = ReadDatabaseTranslation(sub);
                    break;
            }
        }
        return sip;
    }

    private static SearchModification ReadSearchModification(XmlReader xr)
    {
        var sm = new SearchModification();
        if (bool.TryParse(xr.GetAttribute("fixedMod"), out var fm)) sm.FixedMod = fm;
        if (double.TryParse(xr.GetAttribute("massDelta"), NumberStyles.Float, CultureInfo.InvariantCulture, out var md))
            sm.MassDelta = md;
        var residues = xr.GetAttribute("residues");
        if (!string.IsNullOrEmpty(residues))
            foreach (var c in residues.Where(c => !char.IsWhiteSpace(c))) sm.Residues.Add(c);
        if (xr.IsEmptyElement) return sm;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "SpecificityRules")
                using (var sr = sub.ReadSubtree())
                    while (sr.Read())
                        if (sr.NodeType == XmlNodeType.Element && sr.LocalName == "cvParam")
                            sm.SpecificityRules = ReadCvParam(sr);
            else if (sub.LocalName is "cvParam" or "userParam")
                ReadOneParamInto(sub, sm);
        }
        return sm;
    }

    private static Enzyme ReadEnzyme(XmlReader xr)
    {
        var e = new Enzyme
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
            NTermGain = xr.GetAttribute("nTermGain") ?? string.Empty,
            CTermGain = xr.GetAttribute("cTermGain") ?? string.Empty,
        };
        var spec = xr.GetAttribute("semiSpecific");
        if (bool.TryParse(spec, out var ss) && ss) e.TerminalSpecificity = DigestionSpecificity.SemiSpecific;
        if (int.TryParse(xr.GetAttribute("missedCleavages"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mc)) e.MissedCleavages = mc;
        if (int.TryParse(xr.GetAttribute("minDistance"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mind)) e.MinDistance = mind;
        if (xr.IsEmptyElement) return e;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "SiteRegexp") e.SiteRegexp = sub.ReadElementContentAsString();
            else if (sub.LocalName == "EnzymeName") ReadParamContainerInto(sub, e.EnzymeName);
        }
        return e;
    }

    private static MassTable ReadMassTable(XmlReader xr)
    {
        var mt = new MassTable { Id = xr.GetAttribute("id") ?? string.Empty };
        var msLevels = xr.GetAttribute("msLevel");
        if (!string.IsNullOrEmpty(msLevels))
            foreach (var tok in msLevels.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(tok, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lvl))
                    mt.MsLevel.Add(lvl);
        if (xr.IsEmptyElement) return mt;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "Residue")
            {
                var r = new Residue();
                var code = sub.GetAttribute("code"); if (!string.IsNullOrEmpty(code)) r.Code = code[0];
                if (double.TryParse(sub.GetAttribute("mass"), NumberStyles.Float, CultureInfo.InvariantCulture, out var m)) r.Mass = m;
                mt.Residues.Add(r);
            }
            else if (sub.LocalName == "AmbiguousResidue")
            {
                var ar = new AmbiguousResidue();
                var code = sub.GetAttribute("code"); if (!string.IsNullOrEmpty(code)) ar.Code = code[0];
                if (!sub.IsEmptyElement) using (var arSub = sub.ReadSubtree())
                    while (arSub.Read())
                        if (arSub.NodeType == XmlNodeType.Element && arSub.LocalName is "cvParam" or "userParam")
                            ReadOneParamInto(arSub, ar);
                mt.AmbiguousResidue.Add(ar);
            }
        }
        return mt;
    }

    private static Filter ReadFilter(XmlReader xr)
    {
        var f = new Filter();
        if (xr.IsEmptyElement) return f;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            switch (sub.LocalName)
            {
                case "FilterType": ReadParamContainerInto(sub, f.FilterType); break;
                case "Include": ReadParamContainerInto(sub, f.Include); break;
                case "Exclude": ReadParamContainerInto(sub, f.Exclude); break;
            }
        }
        return f;
    }

    private static DatabaseTranslation ReadDatabaseTranslation(XmlReader xr)
    {
        var dt = new DatabaseTranslation();
        var frames = xr.GetAttribute("frames");
        if (!string.IsNullOrEmpty(frames))
            foreach (var tok in frames.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(tok, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fr))
                    dt.Frames.Add(fr);
        if (xr.IsEmptyElement) return dt;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "TranslationTable")
            {
                var tt = new TranslationTable
                {
                    Id = sub.GetAttribute("id") ?? string.Empty,
                    Name = sub.GetAttribute("name") ?? string.Empty,
                };
                if (!sub.IsEmptyElement) ReadParamContainerInto(sub, tt);
                dt.TranslationTables.Add(tt);
            }
        }
        return dt;
    }

    private static ProteinDetectionProtocol ReadProteinDetectionProtocol(XmlReader xr, ReferenceMaps refs)
    {
        var pdp = new ProteinDetectionProtocol
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
        };
        var asRef = xr.GetAttribute("analysisSoftware_ref");
        if (!string.IsNullOrEmpty(asRef) && refs.Software.TryGetValue(asRef, out var sw))
            pdp.AnalysisSoftwarePtr = sw;
        if (xr.IsEmptyElement) return pdp;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "AnalysisParams") ReadParamContainerInto(sub, pdp.AnalysisParams);
            else if (sub.LocalName == "Threshold") ReadParamContainerInto(sub, pdp.Threshold);
        }
        return pdp;
    }

    private static SpectrumIdentification ReadSpectrumIdentification(XmlReader xr, ReferenceMaps refs)
    {
        var si = new SpectrumIdentification
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
            ActivityDate = xr.GetAttribute("activityDate") ?? string.Empty,
        };
        var protoRef = xr.GetAttribute("spectrumIdentificationProtocol_ref");
        if (!string.IsNullOrEmpty(protoRef) && refs.SpectrumIdProtocol.TryGetValue(protoRef, out var sip))
            si.SpectrumIdentificationProtocolPtr = sip;
        var listRef = xr.GetAttribute("spectrumIdentificationList_ref");
        if (!string.IsNullOrEmpty(listRef) && refs.SpectrumIdList.TryGetValue(listRef, out var sil))
            si.SpectrumIdentificationListPtr = sil;
        if (xr.IsEmptyElement) return si;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "InputSpectra")
            {
                var sdRef = sub.GetAttribute("spectraData_ref");
                if (!string.IsNullOrEmpty(sdRef) && refs.SpectraData.TryGetValue(sdRef, out var sd))
                    si.InputSpectra.Add(sd);
            }
            else if (sub.LocalName == "SearchDatabaseRef")
            {
                var dbRef = sub.GetAttribute("searchDatabase_ref");
                if (!string.IsNullOrEmpty(dbRef) && refs.SearchDb.TryGetValue(dbRef, out var db))
                    si.SearchDatabase.Add(db);
            }
        }
        return si;
    }

    private static void ReadProteinDetectionInto(XmlReader xr, ProteinDetection pd, ReferenceMaps refs)
    {
        pd.Id = xr.GetAttribute("id") ?? string.Empty;
        pd.Name = xr.GetAttribute("name") ?? string.Empty;
        pd.ActivityDate = xr.GetAttribute("activityDate") ?? string.Empty;
        var protoRef = xr.GetAttribute("proteinDetectionProtocol_ref");
        if (!string.IsNullOrEmpty(protoRef) && refs.ProteinDetProtocol.TryGetValue(protoRef, out var pdp))
            pd.ProteinDetectionProtocolPtr = pdp;
        // proteinDetectionList_ref / inputSpectrumIdentifications: ID-only refs we resolve later
        // when ProteinDetectionList is encountered, if present.
        if (xr.IsEmptyElement) return;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "InputSpectrumIdentifications")
            {
                var sirRef = sub.GetAttribute("spectrumIdentificationList_ref");
                if (!string.IsNullOrEmpty(sirRef) && refs.SpectrumIdList.TryGetValue(sirRef, out var sil))
                    pd.InputSpectrumIdentifications.Add(sil);
            }
        }
    }

    private static ProteinDetectionList ReadProteinDetectionList(XmlReader xr, ReferenceMaps refs)
    {
        var pdl = new ProteinDetectionList
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
        };
        if (xr.IsEmptyElement) return pdl;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "ProteinAmbiguityGroup")
                pdl.ProteinAmbiguityGroup.Add(ReadProteinAmbiguityGroup(sub, refs));
            else if (sub.LocalName is "cvParam" or "userParam")
                ReadOneParamInto(sub, pdl);
        }
        return pdl;
    }

    private static ProteinAmbiguityGroup ReadProteinAmbiguityGroup(XmlReader xr, ReferenceMaps refs)
    {
        var pag = new ProteinAmbiguityGroup
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
        };
        if (xr.IsEmptyElement) return pag;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "ProteinDetectionHypothesis")
                pag.ProteinDetectionHypothesis.Add(ReadProteinDetectionHypothesis(sub, refs));
            else if (sub.LocalName is "cvParam" or "userParam")
                ReadOneParamInto(sub, pag);
        }
        return pag;
    }

    private static ProteinDetectionHypothesis ReadProteinDetectionHypothesis(XmlReader xr, ReferenceMaps refs)
    {
        var pdh = new ProteinDetectionHypothesis
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
        };
        if (bool.TryParse(xr.GetAttribute("passThreshold"), out var pt)) pdh.PassThreshold = pt;
        var dbRef = xr.GetAttribute("dBSequence_ref");
        if (!string.IsNullOrEmpty(dbRef) && refs.DbSequence.TryGetValue(dbRef, out var db))
            pdh.DBSequencePtr = db;
        if (xr.IsEmptyElement) return pdh;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "PeptideHypothesis")
            {
                var ph = new PeptideHypothesis();
                var peRef = sub.GetAttribute("peptideEvidence_ref");
                if (!string.IsNullOrEmpty(peRef) && refs.PeptideEvidence.TryGetValue(peRef, out var pe))
                    ph.PeptideEvidencePtr = pe;
                pdh.PeptideHypothesis.Add(ph);
            }
            else if (sub.LocalName is "cvParam" or "userParam")
                ReadOneParamInto(sub, pdh);
        }
        return pdh;
    }

    // -------- low-level CV / user param helpers --------

    private static void ReadParamContainerInto(XmlReader xr, ParamContainer pc)
    {
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName is "cvParam" or "userParam") ReadOneParamInto(sub, pc);
        }
    }

    private static void ReadOneParamInto(XmlReader xr, ParamContainer pc)
    {
        if (xr.LocalName == "cvParam")
        {
            pc.CVParams.Add(ReadCvParam(xr));
        }
        else // userParam
        {
            var name = xr.GetAttribute("name") ?? string.Empty;
            var value = xr.GetAttribute("value") ?? string.Empty;
            var typ = xr.GetAttribute("type") ?? string.Empty;
            var unitCvid = ParseCvAccession(xr.GetAttribute("unitAccession"));
            pc.UserParams.Add(new UserParam(name, value, typ, unitCvid));
        }
    }

    private static CVParam ReadCvParam(XmlReader xr)
    {
        var cvid = ParseCvAccession(xr.GetAttribute("accession"));
        var value = xr.GetAttribute("value") ?? string.Empty;
        var unitCvid = ParseCvAccession(xr.GetAttribute("unitAccession"));
        return new CVParam(cvid, value, unitCvid);
    }

    private static CVID ParseCvAccession(string? accession)
    {
        if (string.IsNullOrEmpty(accession)) return CVID.CVID_Unknown;
        return CvLookup.CvTermInfo(accession).Cvid;
    }
}
