using System.Globalization;
using System.Xml;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData.Mzid;

/// <summary>
/// Serializes an <see cref="IdentData"/> tree to mzIdentML 1.1 XML. Port of
/// <c>pwiz::identdata::Serializer_mzid</c>.
/// </summary>
/// <remarks>
/// Emits the schema subset that <see cref="MzidReader"/> can round-trip: cvList, analysis
/// software, provider, sequence collection, analysis collection, analysis protocol collection,
/// data collection (inputs + analysis data), bibliographic references. Empty containers are
/// omitted to keep output diff-friendly.
/// </remarks>
public sealed class MzidWriter
{
    private const string MzidNs = "http://psidev.info/psi/pi/mzIdentML/1.1";

    /// <summary>Writes <paramref name="ident"/> as mzIdentML XML to <paramref name="stream"/>.</summary>
#pragma warning disable CA1822 // kept as instance for symmetry with the reader / future writer config
    public void Write(IdentData ident, Stream stream)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(ident);
        ArgumentNullException.ThrowIfNull(stream);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        using var w = XmlWriter.Create(stream, settings);
        WriteRoot(w, ident);
    }

    private static void WriteRoot(XmlWriter w, IdentData ident)
    {
        w.WriteStartDocument();
        w.WriteStartElement("MzIdentML", MzidNs);
        if (!string.IsNullOrEmpty(ident.Id)) w.WriteAttributeString("id", ident.Id);
        if (!string.IsNullOrEmpty(ident.Name)) w.WriteAttributeString("name", ident.Name);
        w.WriteAttributeString("version", "1.1.0");
        if (!string.IsNullOrEmpty(ident.CreationDate))
            w.WriteAttributeString("creationDate", ident.CreationDate);

        WriteCvList(w, ident.Cvs);
        if (ident.AnalysisSoftwareList.Count > 0) WriteAnalysisSoftwareList(w, ident.AnalysisSoftwareList);
        if (!ident.Provider.IsEmpty) WriteProvider(w, ident.Provider);
        if (ident.AuditCollection.Count > 0) WriteAuditCollection(w, ident.AuditCollection);
        if (!ident.AnalysisSampleCollection.IsEmpty) WriteAnalysisSampleCollection(w, ident.AnalysisSampleCollection);
        if (!ident.SequenceCollection.IsEmpty) WriteSequenceCollection(w, ident.SequenceCollection);
        if (!ident.AnalysisCollection.IsEmpty) WriteAnalysisCollection(w, ident.AnalysisCollection);
        if (!ident.AnalysisProtocolCollection.IsEmpty) WriteAnalysisProtocolCollection(w, ident.AnalysisProtocolCollection);
        if (!ident.DataCollection.IsEmpty) WriteDataCollection(w, ident.DataCollection);
        if (ident.BibliographicReferences.Count > 0)
            foreach (var br in ident.BibliographicReferences) WriteBibliographicReference(w, br);

        w.WriteEndElement();
        w.WriteEndDocument();
    }

    // ------------- cv / analysis software -------------

    private static void WriteCvList(XmlWriter w, IList<CV> cvs)
    {
        // mzIdentML 1.1 schema requires at least one cv, so production-quality output should
        // populate Cvs. We emit a cvList only when one is provided to keep round-trip parity:
        // synthesizing a default would surface in the round-trip as an extra element.
        if (cvs.Count == 0) return;
        w.WriteStartElement("cvList", MzidNs);
        foreach (var cv in cvs) WriteCv(w, cv.Id, cv.FullName, cv.Uri, cv.Version);
        w.WriteEndElement();
    }

    private static void WriteCv(XmlWriter w, string id, string fullName, string uri, string version)
    {
        w.WriteStartElement("cv", MzidNs);
        w.WriteAttributeString("id", string.IsNullOrEmpty(id) ? "PSI-MS" : id);
        if (!string.IsNullOrEmpty(fullName)) w.WriteAttributeString("fullName", fullName);
        if (!string.IsNullOrEmpty(uri)) w.WriteAttributeString("uri", uri);
        if (!string.IsNullOrEmpty(version)) w.WriteAttributeString("version", version);
        w.WriteEndElement();
    }

    private static void WriteAnalysisSoftwareList(XmlWriter w, IList<AnalysisSoftware> swList)
    {
        w.WriteStartElement("AnalysisSoftwareList", MzidNs);
        foreach (var sw in swList)
        {
            w.WriteStartElement("AnalysisSoftware", MzidNs);
            WriteIdentifiableAttrs(w, sw.Id, sw.Name);
            if (!string.IsNullOrEmpty(sw.Version)) w.WriteAttributeString("version", sw.Version);
            if (!string.IsNullOrEmpty(sw.Uri)) w.WriteAttributeString("URI", sw.Uri);
            if (sw.ContactRolePtr is not null) WriteContactRole(w, sw.ContactRolePtr);
            if (!sw.SoftwareName.IsEmpty)
            {
                w.WriteStartElement("SoftwareName", MzidNs);
                WriteParamContainer(w, sw.SoftwareName);
                w.WriteEndElement();
            }
            if (!string.IsNullOrEmpty(sw.Customizations))
                w.WriteElementString("Customizations", MzidNs, sw.Customizations);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteProvider(XmlWriter w, Provider p)
    {
        w.WriteStartElement("Provider", MzidNs);
        WriteIdentifiableAttrs(w, p.Id, p.Name);
        if (p.AnalysisSoftwarePtr is not null && !string.IsNullOrEmpty(p.AnalysisSoftwarePtr.Id))
            w.WriteAttributeString("analysisSoftware_ref", p.AnalysisSoftwarePtr.Id);
        if (p.ContactRolePtr is not null) WriteContactRole(w, p.ContactRolePtr);
        w.WriteEndElement();
    }

    // ------------- audit / sample -------------

    private static void WriteAuditCollection(XmlWriter w, IList<Contact> contacts)
    {
        w.WriteStartElement("AuditCollection", MzidNs);
        foreach (var c in contacts)
        {
            switch (c)
            {
                case Person p: WritePerson(w, p); break;
                case Organization o: WriteOrganization(w, o); break;
            }
        }
        w.WriteEndElement();
    }

    private static void WritePerson(XmlWriter w, Person p)
    {
        w.WriteStartElement("Person", MzidNs);
        WriteIdentifiableAttrs(w, p.Id, p.Name);
        if (!string.IsNullOrEmpty(p.LastName)) w.WriteAttributeString("lastName", p.LastName);
        if (!string.IsNullOrEmpty(p.FirstName)) w.WriteAttributeString("firstName", p.FirstName);
        if (!string.IsNullOrEmpty(p.MidInitials)) w.WriteAttributeString("midInitials", p.MidInitials);
        WriteParamContainer(w, p);
        foreach (var aff in p.Affiliations)
        {
            w.WriteStartElement("Affiliation", MzidNs);
            if (!string.IsNullOrEmpty(aff.Id)) w.WriteAttributeString("organization_ref", aff.Id);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteOrganization(XmlWriter w, Organization o)
    {
        w.WriteStartElement("Organization", MzidNs);
        WriteIdentifiableAttrs(w, o.Id, o.Name);
        WriteParamContainer(w, o);
        if (o.Parent is not null && !string.IsNullOrEmpty(o.Parent.Id))
        {
            w.WriteStartElement("Parent", MzidNs);
            w.WriteAttributeString("organization_ref", o.Parent.Id);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteContactRole(XmlWriter w, ContactRole cr)
    {
        w.WriteStartElement("ContactRole", MzidNs);
        if (cr.ContactPtr is not null && !string.IsNullOrEmpty(cr.ContactPtr.Id))
            w.WriteAttributeString("contact_ref", cr.ContactPtr.Id);
        w.WriteStartElement("Role", MzidNs);
        WriteCvParam(w, cr.Role);
        w.WriteEndElement();
        w.WriteEndElement();
    }

    private static void WriteAnalysisSampleCollection(XmlWriter w, AnalysisSampleCollection asc)
    {
        w.WriteStartElement("AnalysisSampleCollection", MzidNs);
        foreach (var s in asc.Samples)
        {
            w.WriteStartElement("Sample", MzidNs);
            WriteIdentifiableAttrs(w, s.Id, s.Name);
            foreach (var cr in s.ContactRole) WriteContactRole(w, cr);
            foreach (var sub in s.SubSamples)
            {
                w.WriteStartElement("SubSample", MzidNs);
                if (!string.IsNullOrEmpty(sub.Id)) w.WriteAttributeString("sample_ref", sub.Id);
                w.WriteEndElement();
            }
            WriteParamContainer(w, s);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    // ------------- sequence collection -------------

    private static void WriteSequenceCollection(XmlWriter w, SequenceCollection sc)
    {
        w.WriteStartElement("SequenceCollection", MzidNs);
        foreach (var dbs in sc.DBSequences) WriteDBSequence(w, dbs);
        foreach (var p in sc.Peptides) WritePeptide(w, p);
        foreach (var pe in sc.PeptideEvidence) WritePeptideEvidence(w, pe);
        w.WriteEndElement();
    }

    private static void WriteDBSequence(XmlWriter w, DBSequence dbs)
    {
        w.WriteStartElement("DBSequence", MzidNs);
        WriteIdentifiableAttrs(w, dbs.Id, dbs.Name);
        if (dbs.Length > 0) w.WriteAttributeString("length", InvInt(dbs.Length));
        if (!string.IsNullOrEmpty(dbs.Accession)) w.WriteAttributeString("accession", dbs.Accession);
        if (dbs.SearchDatabasePtr is not null && !string.IsNullOrEmpty(dbs.SearchDatabasePtr.Id))
            w.WriteAttributeString("searchDatabase_ref", dbs.SearchDatabasePtr.Id);
        if (!string.IsNullOrEmpty(dbs.Seq))
            w.WriteElementString("Seq", MzidNs, dbs.Seq);
        WriteParamContainer(w, dbs);
        w.WriteEndElement();
    }

    private static void WritePeptide(XmlWriter w, Peptide p)
    {
        w.WriteStartElement("Peptide", MzidNs);
        WriteIdentifiableAttrs(w, p.Id, p.Name);
        if (!string.IsNullOrEmpty(p.PeptideSequence))
            w.WriteElementString("PeptideSequence", MzidNs, p.PeptideSequence);
        foreach (var m in p.Modifications) WriteModification(w, m);
        foreach (var sm in p.SubstitutionModifications) WriteSubstitutionModification(w, sm);
        WriteParamContainer(w, p);
        w.WriteEndElement();
    }

    private static void WriteModification(XmlWriter w, Modification m)
    {
        w.WriteStartElement("Modification", MzidNs);
        if (m.Location != 0) w.WriteAttributeString("location", InvInt(m.Location));
        if (m.Residues.Count > 0) w.WriteAttributeString("residues", new string(m.Residues.ToArray()));
        if (m.AvgMassDelta != 0) w.WriteAttributeString("avgMassDelta", InvDouble(m.AvgMassDelta));
        if (m.MonoisotopicMassDelta != 0) w.WriteAttributeString("monoisotopicMassDelta", InvDouble(m.MonoisotopicMassDelta));
        WriteParamContainer(w, m);
        w.WriteEndElement();
    }

    private static void WriteSubstitutionModification(XmlWriter w, SubstitutionModification sm)
    {
        w.WriteStartElement("SubstitutionModification", MzidNs);
        if (sm.OriginalResidue != '\0') w.WriteAttributeString("originalResidue", sm.OriginalResidue.ToString());
        if (sm.ReplacementResidue != '\0') w.WriteAttributeString("replacementResidue", sm.ReplacementResidue.ToString());
        if (sm.Location != 0) w.WriteAttributeString("location", InvInt(sm.Location));
        if (sm.AvgMassDelta != 0) w.WriteAttributeString("avgMassDelta", InvDouble(sm.AvgMassDelta));
        if (sm.MonoisotopicMassDelta != 0) w.WriteAttributeString("monoisotopicMassDelta", InvDouble(sm.MonoisotopicMassDelta));
        w.WriteEndElement();
    }

    private static void WritePeptideEvidence(XmlWriter w, PeptideEvidence pe)
    {
        w.WriteStartElement("PeptideEvidence", MzidNs);
        WriteIdentifiableAttrs(w, pe.Id, pe.Name);
        if (pe.PeptidePtr is not null && !string.IsNullOrEmpty(pe.PeptidePtr.Id))
            w.WriteAttributeString("peptide_ref", pe.PeptidePtr.Id);
        if (pe.DBSequencePtr is not null && !string.IsNullOrEmpty(pe.DBSequencePtr.Id))
            w.WriteAttributeString("dBSequence_ref", pe.DBSequencePtr.Id);
        if (pe.Start != 0) w.WriteAttributeString("start", InvInt(pe.Start));
        if (pe.End != 0) w.WriteAttributeString("end", InvInt(pe.End));
        if (pe.Pre != '\0') w.WriteAttributeString("pre", pe.Pre.ToString());
        if (pe.Post != '\0') w.WriteAttributeString("post", pe.Post.ToString());
        if (pe.Frame != 0) w.WriteAttributeString("frame", InvInt(pe.Frame));
        w.WriteAttributeString("isDecoy", pe.IsDecoy ? "true" : "false");
        WriteParamContainer(w, pe);
        w.WriteEndElement();
    }

    // ------------- analysis collection / protocol collection -------------

    private static void WriteAnalysisCollection(XmlWriter w, AnalysisCollection ac)
    {
        w.WriteStartElement("AnalysisCollection", MzidNs);
        foreach (var si in ac.SpectrumIdentification)
        {
            w.WriteStartElement("SpectrumIdentification", MzidNs);
            WriteIdentifiableAttrs(w, si.Id, si.Name);
            if (si.SpectrumIdentificationProtocolPtr is not null && !string.IsNullOrEmpty(si.SpectrumIdentificationProtocolPtr.Id))
                w.WriteAttributeString("spectrumIdentificationProtocol_ref", si.SpectrumIdentificationProtocolPtr.Id);
            if (si.SpectrumIdentificationListPtr is not null && !string.IsNullOrEmpty(si.SpectrumIdentificationListPtr.Id))
                w.WriteAttributeString("spectrumIdentificationList_ref", si.SpectrumIdentificationListPtr.Id);
            if (!string.IsNullOrEmpty(si.ActivityDate))
                w.WriteAttributeString("activityDate", si.ActivityDate);
            foreach (var sd in si.InputSpectra)
            {
                w.WriteStartElement("InputSpectra", MzidNs);
                if (!string.IsNullOrEmpty(sd.Id)) w.WriteAttributeString("spectraData_ref", sd.Id);
                w.WriteEndElement();
            }
            foreach (var db in si.SearchDatabase)
            {
                w.WriteStartElement("SearchDatabaseRef", MzidNs);
                if (!string.IsNullOrEmpty(db.Id)) w.WriteAttributeString("searchDatabase_ref", db.Id);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        if (!ac.ProteinDetection.IsEmpty)
        {
            w.WriteStartElement("ProteinDetection", MzidNs);
            WriteIdentifiableAttrs(w, ac.ProteinDetection.Id, ac.ProteinDetection.Name);
            if (ac.ProteinDetection.ProteinDetectionProtocolPtr is not null
                && !string.IsNullOrEmpty(ac.ProteinDetection.ProteinDetectionProtocolPtr.Id))
                w.WriteAttributeString("proteinDetectionProtocol_ref", ac.ProteinDetection.ProteinDetectionProtocolPtr.Id);
            if (ac.ProteinDetection.ProteinDetectionListPtr is not null
                && !string.IsNullOrEmpty(ac.ProteinDetection.ProteinDetectionListPtr.Id))
                w.WriteAttributeString("proteinDetectionList_ref", ac.ProteinDetection.ProteinDetectionListPtr.Id);
            if (!string.IsNullOrEmpty(ac.ProteinDetection.ActivityDate))
                w.WriteAttributeString("activityDate", ac.ProteinDetection.ActivityDate);
            foreach (var sil in ac.ProteinDetection.InputSpectrumIdentifications)
            {
                w.WriteStartElement("InputSpectrumIdentifications", MzidNs);
                if (!string.IsNullOrEmpty(sil.Id)) w.WriteAttributeString("spectrumIdentificationList_ref", sil.Id);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteAnalysisProtocolCollection(XmlWriter w, AnalysisProtocolCollection apc)
    {
        w.WriteStartElement("AnalysisProtocolCollection", MzidNs);
        foreach (var sip in apc.SpectrumIdentificationProtocol) WriteSpectrumIdentificationProtocol(w, sip);
        foreach (var pdp in apc.ProteinDetectionProtocol) WriteProteinDetectionProtocol(w, pdp);
        w.WriteEndElement();
    }

    private static void WriteSpectrumIdentificationProtocol(XmlWriter w, SpectrumIdentificationProtocol sip)
    {
        w.WriteStartElement("SpectrumIdentificationProtocol", MzidNs);
        WriteIdentifiableAttrs(w, sip.Id, sip.Name);
        if (sip.AnalysisSoftwarePtr is not null && !string.IsNullOrEmpty(sip.AnalysisSoftwarePtr.Id))
            w.WriteAttributeString("analysisSoftware_ref", sip.AnalysisSoftwarePtr.Id);

        w.WriteStartElement("SearchType", MzidNs);
        WriteCvParam(w, sip.SearchType);
        w.WriteEndElement();

        if (!sip.AdditionalSearchParams.IsEmpty)
        {
            w.WriteStartElement("AdditionalSearchParams", MzidNs);
            WriteParamContainer(w, sip.AdditionalSearchParams);
            w.WriteEndElement();
        }
        if (sip.ModificationParams.Count > 0)
        {
            w.WriteStartElement("ModificationParams", MzidNs);
            foreach (var sm in sip.ModificationParams) WriteSearchModification(w, sm);
            w.WriteEndElement();
        }
        if (!sip.Enzymes.IsEmpty) WriteEnzymes(w, sip.Enzymes);
        foreach (var mt in sip.MassTable) WriteMassTable(w, mt);
        if (!sip.FragmentTolerance.IsEmpty)
        {
            w.WriteStartElement("FragmentTolerance", MzidNs);
            WriteParamContainer(w, sip.FragmentTolerance);
            w.WriteEndElement();
        }
        if (!sip.ParentTolerance.IsEmpty)
        {
            w.WriteStartElement("ParentTolerance", MzidNs);
            WriteParamContainer(w, sip.ParentTolerance);
            w.WriteEndElement();
        }
        if (!sip.Threshold.IsEmpty)
        {
            w.WriteStartElement("Threshold", MzidNs);
            WriteParamContainer(w, sip.Threshold);
            w.WriteEndElement();
        }
        if (sip.DatabaseFilters.Count > 0)
        {
            w.WriteStartElement("DatabaseFilters", MzidNs);
            foreach (var f in sip.DatabaseFilters) WriteFilter(w, f);
            w.WriteEndElement();
        }
        if (sip.DatabaseTranslation is not null) WriteDatabaseTranslation(w, sip.DatabaseTranslation);
        w.WriteEndElement();
    }

    private static void WriteSearchModification(XmlWriter w, SearchModification sm)
    {
        w.WriteStartElement("SearchModification", MzidNs);
        w.WriteAttributeString("fixedMod", sm.FixedMod ? "true" : "false");
        w.WriteAttributeString("massDelta", InvDouble(sm.MassDelta));
        if (sm.Residues.Count > 0)
            w.WriteAttributeString("residues", new string(sm.Residues.ToArray()));
        if (!sm.SpecificityRules.IsEmpty)
        {
            w.WriteStartElement("SpecificityRules", MzidNs);
            WriteCvParam(w, sm.SpecificityRules);
            w.WriteEndElement();
        }
        WriteParamContainer(w, sm);
        w.WriteEndElement();
    }

    private static void WriteEnzymes(XmlWriter w, Enzymes es)
    {
        w.WriteStartElement("Enzymes", MzidNs);
        if (es.Independent.HasValue)
            w.WriteAttributeString("independent", es.Independent.Value ? "true" : "false");
        foreach (var e in es.EnzymeList)
        {
            w.WriteStartElement("Enzyme", MzidNs);
            WriteIdentifiableAttrs(w, e.Id, e.Name);
            if (!string.IsNullOrEmpty(e.NTermGain)) w.WriteAttributeString("nTermGain", e.NTermGain);
            if (!string.IsNullOrEmpty(e.CTermGain)) w.WriteAttributeString("cTermGain", e.CTermGain);
            w.WriteAttributeString("semiSpecific", e.TerminalSpecificity == DigestionSpecificity.SemiSpecific ? "true" : "false");
            if (e.MissedCleavages != 0) w.WriteAttributeString("missedCleavages", InvInt(e.MissedCleavages));
            if (e.MinDistance != 0) w.WriteAttributeString("minDistance", InvInt(e.MinDistance));
            if (!string.IsNullOrEmpty(e.SiteRegexp))
                w.WriteElementString("SiteRegexp", MzidNs, e.SiteRegexp);
            if (!e.EnzymeName.IsEmpty)
            {
                w.WriteStartElement("EnzymeName", MzidNs);
                WriteParamContainer(w, e.EnzymeName);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteMassTable(XmlWriter w, MassTable mt)
    {
        w.WriteStartElement("MassTable", MzidNs);
        if (!string.IsNullOrEmpty(mt.Id)) w.WriteAttributeString("id", mt.Id);
        if (mt.MsLevel.Count > 0) w.WriteAttributeString("msLevel", string.Join(' ', mt.MsLevel.Select(InvInt)));
        foreach (var r in mt.Residues)
        {
            w.WriteStartElement("Residue", MzidNs);
            if (r.Code != '\0') w.WriteAttributeString("code", r.Code.ToString());
            w.WriteAttributeString("mass", InvDouble(r.Mass));
            w.WriteEndElement();
        }
        foreach (var ar in mt.AmbiguousResidue)
        {
            w.WriteStartElement("AmbiguousResidue", MzidNs);
            if (ar.Code != '\0') w.WriteAttributeString("code", ar.Code.ToString());
            WriteParamContainer(w, ar);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteFilter(XmlWriter w, Filter f)
    {
        w.WriteStartElement("Filter", MzidNs);
        if (!f.FilterType.IsEmpty)
        {
            w.WriteStartElement("FilterType", MzidNs); WriteParamContainer(w, f.FilterType); w.WriteEndElement();
        }
        if (!f.Include.IsEmpty)
        {
            w.WriteStartElement("Include", MzidNs); WriteParamContainer(w, f.Include); w.WriteEndElement();
        }
        if (!f.Exclude.IsEmpty)
        {
            w.WriteStartElement("Exclude", MzidNs); WriteParamContainer(w, f.Exclude); w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteDatabaseTranslation(XmlWriter w, DatabaseTranslation dt)
    {
        w.WriteStartElement("DatabaseTranslation", MzidNs);
        if (dt.Frames.Count > 0)
            w.WriteAttributeString("frames", string.Join(' ', dt.Frames.Select(InvInt)));
        foreach (var tt in dt.TranslationTables)
        {
            w.WriteStartElement("TranslationTable", MzidNs);
            WriteIdentifiableAttrs(w, tt.Id, tt.Name);
            WriteParamContainer(w, tt);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteProteinDetectionProtocol(XmlWriter w, ProteinDetectionProtocol pdp)
    {
        w.WriteStartElement("ProteinDetectionProtocol", MzidNs);
        WriteIdentifiableAttrs(w, pdp.Id, pdp.Name);
        if (pdp.AnalysisSoftwarePtr is not null && !string.IsNullOrEmpty(pdp.AnalysisSoftwarePtr.Id))
            w.WriteAttributeString("analysisSoftware_ref", pdp.AnalysisSoftwarePtr.Id);
        if (!pdp.AnalysisParams.IsEmpty)
        {
            w.WriteStartElement("AnalysisParams", MzidNs);
            WriteParamContainer(w, pdp.AnalysisParams);
            w.WriteEndElement();
        }
        if (!pdp.Threshold.IsEmpty)
        {
            w.WriteStartElement("Threshold", MzidNs);
            WriteParamContainer(w, pdp.Threshold);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    // ------------- data collection -------------

    private static void WriteDataCollection(XmlWriter w, DataCollection dc)
    {
        w.WriteStartElement("DataCollection", MzidNs);
        WriteInputs(w, dc.Inputs);
        WriteAnalysisDataInner(w, dc.AnalysisData);
        w.WriteEndElement();
    }

    private static void WriteInputs(XmlWriter w, Inputs inputs)
    {
        w.WriteStartElement("Inputs", MzidNs);
        foreach (var sf in inputs.SourceFile)
        {
            w.WriteStartElement("SourceFile", MzidNs);
            WriteIdentifiableAttrs(w, sf.Id, sf.Name);
            if (!string.IsNullOrEmpty(sf.Location)) w.WriteAttributeString("location", sf.Location);
            if (!sf.FileFormat.IsEmpty)
            {
                w.WriteStartElement("FileFormat", MzidNs);
                WriteCvParam(w, sf.FileFormat);
                w.WriteEndElement();
            }
            WriteParamContainer(w, sf);
            w.WriteEndElement();
        }
        foreach (var sd in inputs.SearchDatabase)
        {
            w.WriteStartElement("SearchDatabase", MzidNs);
            WriteIdentifiableAttrs(w, sd.Id, sd.Name);
            if (!string.IsNullOrEmpty(sd.Location)) w.WriteAttributeString("location", sd.Location);
            if (!string.IsNullOrEmpty(sd.Version)) w.WriteAttributeString("version", sd.Version);
            if (!string.IsNullOrEmpty(sd.ReleaseDate)) w.WriteAttributeString("releaseDate", sd.ReleaseDate);
            if (sd.NumDatabaseSequences != 0) w.WriteAttributeString("numDatabaseSequences", InvLong(sd.NumDatabaseSequences));
            if (sd.NumResidues != 0) w.WriteAttributeString("numResidues", InvLong(sd.NumResidues));
            if (!sd.FileFormat.IsEmpty)
            {
                w.WriteStartElement("FileFormat", MzidNs);
                WriteCvParam(w, sd.FileFormat);
                w.WriteEndElement();
            }
            if (!sd.DatabaseName.IsEmpty)
            {
                w.WriteStartElement("DatabaseName", MzidNs);
                WriteParamContainer(w, sd.DatabaseName);
                w.WriteEndElement();
            }
            WriteParamContainer(w, sd);
            w.WriteEndElement();
        }
        foreach (var sd in inputs.SpectraData)
        {
            w.WriteStartElement("SpectraData", MzidNs);
            WriteIdentifiableAttrs(w, sd.Id, sd.Name);
            if (!string.IsNullOrEmpty(sd.Location)) w.WriteAttributeString("location", sd.Location);
            if (!sd.FileFormat.IsEmpty)
            {
                w.WriteStartElement("FileFormat", MzidNs);
                WriteCvParam(w, sd.FileFormat);
                w.WriteEndElement();
            }
            if (!sd.SpectrumIDFormat.IsEmpty)
            {
                w.WriteStartElement("SpectrumIDFormat", MzidNs);
                WriteCvParam(w, sd.SpectrumIDFormat);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteAnalysisDataInner(XmlWriter w, AnalysisData ad)
    {
        w.WriteStartElement("AnalysisData", MzidNs);
        foreach (var sil in ad.SpectrumIdentificationList) WriteSpectrumIdentificationList(w, sil);
        if (ad.ProteinDetectionListPtr is not null) WriteProteinDetectionList(w, ad.ProteinDetectionListPtr);
        w.WriteEndElement();
    }

    private static void WriteSpectrumIdentificationList(XmlWriter w, SpectrumIdentificationList sil)
    {
        w.WriteStartElement("SpectrumIdentificationList", MzidNs);
        WriteIdentifiableAttrs(w, sil.Id, sil.Name);
        if (sil.NumSequencesSearched != 0)
            w.WriteAttributeString("numSequencesSearched", InvLong(sil.NumSequencesSearched));
        if (sil.FragmentationTable.Count > 0)
        {
            w.WriteStartElement("FragmentationTable", MzidNs);
            foreach (var m in sil.FragmentationTable)
            {
                w.WriteStartElement("Measure", MzidNs);
                WriteIdentifiableAttrs(w, m.Id, m.Name);
                WriteParamContainer(w, m);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        foreach (var sir in sil.SpectrumIdentificationResult) WriteSpectrumIdentificationResult(w, sir);
        WriteParamContainer(w, sil);
        w.WriteEndElement();
    }

    private static void WriteSpectrumIdentificationResult(XmlWriter w, SpectrumIdentificationResult sir)
    {
        w.WriteStartElement("SpectrumIdentificationResult", MzidNs);
        WriteIdentifiableAttrs(w, sir.Id, sir.Name);
        if (!string.IsNullOrEmpty(sir.SpectrumID)) w.WriteAttributeString("spectrumID", sir.SpectrumID);
        if (sir.SpectraDataPtr is not null && !string.IsNullOrEmpty(sir.SpectraDataPtr.Id))
            w.WriteAttributeString("spectraData_ref", sir.SpectraDataPtr.Id);
        foreach (var sii in sir.SpectrumIdentificationItem) WriteSpectrumIdentificationItem(w, sii);
        WriteParamContainer(w, sir);
        w.WriteEndElement();
    }

    private static void WriteSpectrumIdentificationItem(XmlWriter w, SpectrumIdentificationItem sii)
    {
        w.WriteStartElement("SpectrumIdentificationItem", MzidNs);
        WriteIdentifiableAttrs(w, sii.Id, sii.Name);
        if (sii.ChargeState != 0) w.WriteAttributeString("chargeState", InvInt(sii.ChargeState));
        if (sii.ExperimentalMassToCharge != 0) w.WriteAttributeString("experimentalMassToCharge", InvDouble(sii.ExperimentalMassToCharge));
        if (sii.CalculatedMassToCharge != 0) w.WriteAttributeString("calculatedMassToCharge", InvDouble(sii.CalculatedMassToCharge));
        if (sii.CalculatedPI != 0) w.WriteAttributeString("calculatedPI", InvDouble(sii.CalculatedPI));
        if (sii.PeptidePtr is not null && !string.IsNullOrEmpty(sii.PeptidePtr.Id))
            w.WriteAttributeString("peptide_ref", sii.PeptidePtr.Id);
        if (sii.Rank != 0) w.WriteAttributeString("rank", InvInt(sii.Rank));
        w.WriteAttributeString("passThreshold", sii.PassThreshold ? "true" : "false");
        foreach (var pe in sii.PeptideEvidencePtr)
        {
            w.WriteStartElement("PeptideEvidenceRef", MzidNs);
            if (!string.IsNullOrEmpty(pe.Id)) w.WriteAttributeString("peptideEvidence_ref", pe.Id);
            w.WriteEndElement();
        }
        if (sii.Fragmentation.Count > 0)
        {
            w.WriteStartElement("Fragmentation", MzidNs);
            foreach (var ion in sii.Fragmentation) WriteIonType(w, ion);
            w.WriteEndElement();
        }
        WriteParamContainer(w, sii);
        w.WriteEndElement();
    }

    private static void WriteIonType(XmlWriter w, IonType ion)
    {
        w.WriteStartElement("IonType", MzidNs);
        if (ion.Index.Count > 0)
            w.WriteAttributeString("index", string.Join(' ', ion.Index.Select(InvInt)));
        if (ion.Charge != 0) w.WriteAttributeString("charge", InvInt(ion.Charge));
        WriteCvParam(w, ion.Type);
        foreach (var fa in ion.FragmentArray)
        {
            w.WriteStartElement("FragmentArray", MzidNs);
            if (fa.MeasurePtr is not null && !string.IsNullOrEmpty(fa.MeasurePtr.Id))
                w.WriteAttributeString("measure_ref", fa.MeasurePtr.Id);
            if (fa.Values.Count > 0)
                w.WriteAttributeString("values", string.Join(' ', fa.Values.Select(InvDouble)));
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteProteinDetectionList(XmlWriter w, ProteinDetectionList pdl)
    {
        w.WriteStartElement("ProteinDetectionList", MzidNs);
        WriteIdentifiableAttrs(w, pdl.Id, pdl.Name);
        foreach (var pag in pdl.ProteinAmbiguityGroup)
        {
            w.WriteStartElement("ProteinAmbiguityGroup", MzidNs);
            WriteIdentifiableAttrs(w, pag.Id, pag.Name);
            foreach (var pdh in pag.ProteinDetectionHypothesis)
            {
                w.WriteStartElement("ProteinDetectionHypothesis", MzidNs);
                WriteIdentifiableAttrs(w, pdh.Id, pdh.Name);
                if (pdh.DBSequencePtr is not null && !string.IsNullOrEmpty(pdh.DBSequencePtr.Id))
                    w.WriteAttributeString("dBSequence_ref", pdh.DBSequencePtr.Id);
                w.WriteAttributeString("passThreshold", pdh.PassThreshold ? "true" : "false");
                foreach (var ph in pdh.PeptideHypothesis)
                {
                    w.WriteStartElement("PeptideHypothesis", MzidNs);
                    if (ph.PeptideEvidencePtr is not null && !string.IsNullOrEmpty(ph.PeptideEvidencePtr.Id))
                        w.WriteAttributeString("peptideEvidence_ref", ph.PeptideEvidencePtr.Id);
                    w.WriteEndElement();
                }
                WriteParamContainer(w, pdh);
                w.WriteEndElement();
            }
            WriteParamContainer(w, pag);
            w.WriteEndElement();
        }
        WriteParamContainer(w, pdl);
        w.WriteEndElement();
    }

    private static void WriteBibliographicReference(XmlWriter w, BibliographicReference br)
    {
        w.WriteStartElement("BibliographicReference", MzidNs);
        WriteIdentifiableAttrs(w, br.Id, br.Name);
        if (!string.IsNullOrEmpty(br.Authors)) w.WriteAttributeString("authors", br.Authors);
        if (!string.IsNullOrEmpty(br.Publication)) w.WriteAttributeString("publication", br.Publication);
        if (!string.IsNullOrEmpty(br.Publisher)) w.WriteAttributeString("publisher", br.Publisher);
        if (!string.IsNullOrEmpty(br.Editor)) w.WriteAttributeString("editor", br.Editor);
        if (br.Year != 0) w.WriteAttributeString("year", InvInt(br.Year));
        if (!string.IsNullOrEmpty(br.Volume)) w.WriteAttributeString("volume", br.Volume);
        if (!string.IsNullOrEmpty(br.Issue)) w.WriteAttributeString("issue", br.Issue);
        if (!string.IsNullOrEmpty(br.Pages)) w.WriteAttributeString("pages", br.Pages);
        if (!string.IsNullOrEmpty(br.Title)) w.WriteAttributeString("title", br.Title);
        w.WriteEndElement();
    }

    // ------------- low-level helpers -------------

    private static void WriteIdentifiableAttrs(XmlWriter w, string id, string name)
    {
        if (!string.IsNullOrEmpty(id)) w.WriteAttributeString("id", id);
        if (!string.IsNullOrEmpty(name)) w.WriteAttributeString("name", name);
    }

    private static void WriteParamContainer(XmlWriter w, ParamContainer pc)
    {
        foreach (var cv in pc.CVParams) WriteCvParam(w, cv);
        foreach (var u in pc.UserParams) WriteUserParam(w, u);
    }

    private static void WriteCvParam(XmlWriter w, CVParam cv)
    {
        if (cv.Cvid == CVID.CVID_Unknown) return;
        var info = CvLookup.CvTermInfo(cv.Cvid);
        w.WriteStartElement("cvParam", MzidNs);
        w.WriteAttributeString("cvRef", info.Prefix);
        w.WriteAttributeString("accession", info.Id);
        w.WriteAttributeString("name", info.Name);
        if (!string.IsNullOrEmpty(cv.Value)) w.WriteAttributeString("value", cv.Value);
        if (cv.Units != CVID.CVID_Unknown)
        {
            var uInfo = CvLookup.CvTermInfo(cv.Units);
            w.WriteAttributeString("unitCvRef", uInfo.Prefix);
            w.WriteAttributeString("unitAccession", uInfo.Id);
            w.WriteAttributeString("unitName", uInfo.Name);
        }
        w.WriteEndElement();
    }

    private static void WriteUserParam(XmlWriter w, UserParam u)
    {
        w.WriteStartElement("userParam", MzidNs);
        if (!string.IsNullOrEmpty(u.Name)) w.WriteAttributeString("name", u.Name);
        if (!string.IsNullOrEmpty(u.Value)) w.WriteAttributeString("value", u.Value);
        if (!string.IsNullOrEmpty(u.Type)) w.WriteAttributeString("type", u.Type);
        if (u.Units != CVID.CVID_Unknown)
        {
            var uInfo = CvLookup.CvTermInfo(u.Units);
            w.WriteAttributeString("unitCvRef", uInfo.Prefix);
            w.WriteAttributeString("unitAccession", uInfo.Id);
            w.WriteAttributeString("unitName", uInfo.Name);
        }
        w.WriteEndElement();
    }

    private static string InvInt(int v) => v.ToString(CultureInfo.InvariantCulture);
    private static string InvLong(long v) => v.ToString(CultureInfo.InvariantCulture);
    private static string InvDouble(double v) => v.ToString("R", CultureInfo.InvariantCulture);
}
