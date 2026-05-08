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
                case "cv":
                    target.Cvs.Add(new CV
                    {
                        Id = xr.GetAttribute("id") ?? string.Empty,
                        FullName = xr.GetAttribute("fullName") ?? string.Empty,
                        Uri = xr.GetAttribute("uri") ?? string.Empty,
                        Version = xr.GetAttribute("version") ?? string.Empty,
                    });
                    break;
                case "AnalysisSoftware":
                    target.AnalysisSoftwareList.Add(ReadAnalysisSoftware(xr));
                    break;
                case "Provider":
                    ReadProviderInto(xr, target.Provider);
                    break;
                case "Person":
                case "Organization":
                    target.AuditCollection.Add(ReadContact(xr));
                    break;
                case "Sample":
                    target.AnalysisSampleCollection.Samples.Add(ReadSample(xr));
                    break;
                case "SourceFile":
                    target.DataCollection.Inputs.SourceFile.Add(ReadSourceFile(xr));
                    break;
                case "BibliographicReference":
                    target.BibliographicReferences.Add(ReadBibliographicReference(xr));
                    break;
                case "DBSequence":
                    target.SequenceCollection.DBSequences.Add(ReadDBSequence(xr));
                    break;
                case "Peptide":
                    target.SequenceCollection.Peptides.Add(ReadPeptide(xr));
                    break;
                case "PeptideEvidence":
                    target.SequenceCollection.PeptideEvidence.Add(ReadPeptideEvidence(xr));
                    break;
                case "SpectraData":
                    target.DataCollection.Inputs.SpectraData.Add(ReadSpectraData(xr));
                    break;
                case "SearchDatabase":
                    target.DataCollection.Inputs.SearchDatabase.Add(ReadSearchDatabase(xr));
                    break;
                case "SpectrumIdentificationList":
                    target.DataCollection.AnalysisData.SpectrumIdentificationList.Add(ReadSpectrumIdentificationList(xr));
                    break;
                case "SpectrumIdentificationProtocol":
                    target.AnalysisProtocolCollection.SpectrumIdentificationProtocol.Add(ReadSpectrumIdentificationProtocol(xr));
                    break;
                case "ProteinDetectionProtocol":
                    target.AnalysisProtocolCollection.ProteinDetectionProtocol.Add(ReadProteinDetectionProtocol(xr));
                    break;
                case "SpectrumIdentification":
                    target.AnalysisCollection.SpectrumIdentification.Add(ReadSpectrumIdentification(xr));
                    break;
                case "ProteinDetection":
                    ReadProteinDetectionInto(xr, target.AnalysisCollection.ProteinDetection);
                    break;
                case "ProteinDetectionList":
                    target.DataCollection.AnalysisData.ProteinDetectionListPtr = ReadProteinDetectionList(xr);
                    break;
            }
        }

        // Replace stub references created during parse with the populated objects from the
        // tree's id→object lookup tables. This covers both forward refs (e.g. DBSequence's
        // searchDatabase_ref pointing at a SearchDatabase that appears later in the document)
        // and cross-section refs.
        References.Resolve(target);
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
            if (sub.LocalName == "Customizations") sw.Customizations = ReadElementText(sub);
            else if (sub.LocalName == "SoftwareName")
                ReadParamContainerInto(sub, sw.SoftwareName);
            else if (sub.LocalName == "ContactRole")
                sw.ContactRolePtr = ReadContactRole(sub);
        }
        return sw;
    }

    private static ContactRole ReadContactRole(XmlReader xr)
    {
        var cr = new ContactRole();
        // contact_ref → stub Contact; References.Resolve replaces it after the doc is parsed.
        var contactRef = xr.GetAttribute("contact_ref");
        if (!string.IsNullOrEmpty(contactRef)) cr.ContactPtr = new Person { Id = contactRef };
        if (xr.IsEmptyElement) return cr;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "Role")
            {
                using var roleSub = sub.ReadSubtree();
                while (roleSub.Read())
                    if (roleSub.NodeType == XmlNodeType.Element && roleSub.LocalName == "cvParam")
                        cr.Role = ReadCvParam(roleSub);
            }
        }
        return cr;
    }

    private static Contact ReadContact(XmlReader xr)
    {
        var elementName = xr.LocalName;
        Contact contact = elementName == "Person" ? new Person() : new Organization();
        contact.Id = xr.GetAttribute("id") ?? string.Empty;
        contact.Name = xr.GetAttribute("name") ?? string.Empty;
        if (contact is Person p)
        {
            p.LastName = xr.GetAttribute("lastName") ?? string.Empty;
            p.FirstName = xr.GetAttribute("firstName") ?? string.Empty;
            p.MidInitials = xr.GetAttribute("midInitials") ?? string.Empty;
        }
        if (xr.IsEmptyElement) return contact;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName is "cvParam" or "userParam") ReadOneParamInto(sub, contact);
            else if (contact is Person person && sub.LocalName == "Affiliation")
            {
                var orgRef = sub.GetAttribute("organization_ref");
                if (!string.IsNullOrEmpty(orgRef))
                {
                    // Defer: organizations might appear later in the audit collection.
                    person.Affiliations.Add(new Organization { Id = orgRef });
                }
            }
            else if (contact is Organization org && sub.LocalName == "Parent")
            {
                var parentRef = sub.GetAttribute("organization_ref");
                if (!string.IsNullOrEmpty(parentRef))
                    org.Parent = new Organization { Id = parentRef };
            }
        }
        return contact;
    }

    private static Sample ReadSample(XmlReader xr)
    {
        var s = new Sample
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
        };
        if (xr.IsEmptyElement) return s;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            switch (sub.LocalName)
            {
                case "ContactRole":
                    s.ContactRole.Add(ReadContactRole(sub));
                    break;
                case "SubSample":
                    var sampleRef = sub.GetAttribute("sample_ref");
                    if (!string.IsNullOrEmpty(sampleRef))
                        s.SubSamples.Add(new Sample { Id = sampleRef });
                    break;
                case "cvParam":
                case "userParam":
                    ReadOneParamInto(sub, s);
                    break;
            }
        }
        return s;
    }

    private static SourceFile ReadSourceFile(XmlReader xr)
    {
        var sf = new SourceFile
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
            Location = xr.GetAttribute("location") ?? string.Empty,
        };
        if (xr.IsEmptyElement) return sf;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "FileFormat")
            {
                using var ff = sub.ReadSubtree();
                while (ff.Read())
                    if (ff.NodeType == XmlNodeType.Element && ff.LocalName == "cvParam")
                        sf.FileFormat = ReadCvParam(ff);
            }
            else if (sub.LocalName is "cvParam" or "userParam") ReadOneParamInto(sub, sf);
        }
        return sf;
    }

    private static BibliographicReference ReadBibliographicReference(XmlReader xr)
    {
        var br = new BibliographicReference
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
            Authors = xr.GetAttribute("authors") ?? string.Empty,
            Publication = xr.GetAttribute("publication") ?? string.Empty,
            Publisher = xr.GetAttribute("publisher") ?? string.Empty,
            Editor = xr.GetAttribute("editor") ?? string.Empty,
            Volume = xr.GetAttribute("volume") ?? string.Empty,
            Issue = xr.GetAttribute("issue") ?? string.Empty,
            Pages = xr.GetAttribute("pages") ?? string.Empty,
            Title = xr.GetAttribute("title") ?? string.Empty,
        };
        if (int.TryParse(xr.GetAttribute("year"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
            br.Year = y;
        return br;
    }

    private static void ReadProviderInto(XmlReader xr, Provider p)
    {
        p.Id = xr.GetAttribute("id") ?? string.Empty;
        p.Name = xr.GetAttribute("name") ?? string.Empty;
        var asRef = xr.GetAttribute("analysisSoftware_ref");
        if (!string.IsNullOrEmpty(asRef)) p.AnalysisSoftwarePtr = new AnalysisSoftware { Id = asRef };
        if (xr.IsEmptyElement) return;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "ContactRole")
                p.ContactRolePtr = ReadContactRole(sub);
        }
    }

    private static DBSequence ReadDBSequence(XmlReader xr)
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
        if (!string.IsNullOrEmpty(sdRef)) d.SearchDatabasePtr = new SearchDatabase { Id = sdRef };

        if (xr.IsEmptyElement) return d;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "Seq") d.Seq = ReadElementText(sub);
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
                    p.PeptideSequence = ReadElementText(sub);
                    break;
                case "Modification":
                    p.Modifications.Add(ReadModification(sub));
                    break;
                case "SubstitutionModification":
                    p.SubstitutionModifications.Add(ReadSubstitutionModification(sub));
                    break;
                case "cvParam":
                case "userParam":
                    ReadOneParamInto(sub, p);
                    break;
            }
        }
        return p;
    }

    private static SubstitutionModification ReadSubstitutionModification(XmlReader xr)
    {
        var sm = new SubstitutionModification();
        var orig = xr.GetAttribute("originalResidue");
        if (!string.IsNullOrEmpty(orig)) sm.OriginalResidue = orig[0];
        var repl = xr.GetAttribute("replacementResidue");
        if (!string.IsNullOrEmpty(repl)) sm.ReplacementResidue = repl[0];
        if (int.TryParse(xr.GetAttribute("location"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var loc))
            sm.Location = loc;
        if (double.TryParse(xr.GetAttribute("avgMassDelta"), NumberStyles.Float, CultureInfo.InvariantCulture, out var avg))
            sm.AvgMassDelta = avg;
        if (double.TryParse(xr.GetAttribute("monoisotopicMassDelta"), NumberStyles.Float, CultureInfo.InvariantCulture, out var mono))
            sm.MonoisotopicMassDelta = mono;
        return sm;
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

    private static PeptideEvidence ReadPeptideEvidence(XmlReader xr)
    {
        var pe = new PeptideEvidence
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
        };
        var pepRef = xr.GetAttribute("peptide_ref");
        if (!string.IsNullOrEmpty(pepRef)) pe.PeptidePtr = new Peptide { Id = pepRef };
        var dbRef = xr.GetAttribute("dBSequence_ref");
        if (!string.IsNullOrEmpty(dbRef)) pe.DBSequencePtr = new DBSequence { Id = dbRef };
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
            if (sub.LocalName == "FileFormat")
            {
                using var ff = sub.ReadSubtree();
                while (ff.Read())
                    if (ff.NodeType == XmlNodeType.Element && ff.LocalName == "cvParam")
                        d.FileFormat = ReadCvParam(ff);
            }
            else if (sub.LocalName == "DatabaseName") ReadParamContainerInto(sub, d.DatabaseName);
            else if (sub.LocalName is "cvParam" or "userParam") ReadOneParamInto(sub, d);
        }
        return d;
    }

    private static SpectrumIdentificationList ReadSpectrumIdentificationList(XmlReader xr)
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
            if (sub.LocalName == "FragmentationTable")
            {
                using var ftSub = sub.ReadSubtree();
                while (ftSub.Read())
                {
                    if (ftSub.NodeType != XmlNodeType.Element || ftSub.LocalName != "Measure") continue;
                    var m = new Measure
                    {
                        Id = ftSub.GetAttribute("id") ?? string.Empty,
                        Name = ftSub.GetAttribute("name") ?? string.Empty,
                    };
                    if (!ftSub.IsEmptyElement) ReadParamContainerInto(ftSub, m);
                    sil.FragmentationTable.Add(m);
                }
            }
            else if (sub.LocalName == "SpectrumIdentificationResult")
                sil.SpectrumIdentificationResult.Add(ReadSpectrumIdentificationResult(sub));
            else if (sub.LocalName is "cvParam" or "userParam")
                ReadOneParamInto(sub, sil);
        }
        return sil;
    }

    private static SpectrumIdentificationResult ReadSpectrumIdentificationResult(XmlReader xr)
    {
        var sir = new SpectrumIdentificationResult
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
            SpectrumID = xr.GetAttribute("spectrumID") ?? string.Empty,
        };
        var sdRef = xr.GetAttribute("spectraData_ref");
        if (!string.IsNullOrEmpty(sdRef)) sir.SpectraDataPtr = new SpectraData { Id = sdRef };
        if (xr.IsEmptyElement) return sir;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "SpectrumIdentificationItem")
                sir.SpectrumIdentificationItem.Add(ReadSpectrumIdentificationItem(sub));
            else if (sub.LocalName is "cvParam" or "userParam")
                ReadOneParamInto(sub, sir);
        }
        return sir;
    }

    private static SpectrumIdentificationItem ReadSpectrumIdentificationItem(XmlReader xr)
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
        if (!string.IsNullOrEmpty(pepRef)) sii.PeptidePtr = new Peptide { Id = pepRef };
        if (xr.IsEmptyElement) return sii;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "PeptideEvidenceRef")
            {
                var peRef = sub.GetAttribute("peptideEvidence_ref");
                if (!string.IsNullOrEmpty(peRef)) sii.PeptideEvidencePtr.Add(new PeptideEvidence { Id = peRef });
            }
            else if (sub.LocalName == "Fragmentation")
            {
                using var fragSub = sub.ReadSubtree();
                while (fragSub.Read())
                    if (fragSub.NodeType == XmlNodeType.Element && fragSub.LocalName == "IonType")
                        sii.Fragmentation.Add(ReadIonType(fragSub));
            }
            else if (sub.LocalName is "cvParam" or "userParam")
            {
                ReadOneParamInto(sub, sii);
            }
        }
        return sii;
    }

    private static IonType ReadIonType(XmlReader xr)
    {
        var ion = new IonType();
        var idx = xr.GetAttribute("index");
        if (!string.IsNullOrEmpty(idx))
            foreach (var tok in idx.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(tok, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                    ion.Index.Add(v);
        if (int.TryParse(xr.GetAttribute("charge"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var charge))
            ion.Charge = charge;
        if (xr.IsEmptyElement) return ion;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "cvParam") ion.Type = ReadCvParam(sub);
            else if (sub.LocalName == "FragmentArray")
            {
                var fa = new FragmentArray();
                var measureRef = sub.GetAttribute("measure_ref");
                if (!string.IsNullOrEmpty(measureRef))
                    fa.MeasurePtr = new Measure { Id = measureRef };
                var values = sub.GetAttribute("values");
                if (!string.IsNullOrEmpty(values))
                    foreach (var tok in values.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        if (double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                            fa.Values.Add(d);
                ion.FragmentArray.Add(fa);
            }
        }
        return ion;
    }

    // -------- protocol / analysis-collection helpers --------

    private static SpectrumIdentificationProtocol ReadSpectrumIdentificationProtocol(XmlReader xr)
    {
        var sip = new SpectrumIdentificationProtocol
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
        };
        var asRef = xr.GetAttribute("analysisSoftware_ref");
        if (!string.IsNullOrEmpty(asRef)) sip.AnalysisSoftwarePtr = new AnalysisSoftware { Id = asRef };
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
            {
                using var sr = sub.ReadSubtree();
                while (sr.Read())
                    if (sr.NodeType == XmlNodeType.Element && sr.LocalName == "cvParam")
                        sm.SpecificityRules = ReadCvParam(sr);
            }
            else if (sub.LocalName is "cvParam" or "userParam")
            {
                ReadOneParamInto(sub, sm);
            }
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
            if (sub.LocalName == "SiteRegexp") e.SiteRegexp = ReadElementText(sub);
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

    private static ProteinDetectionProtocol ReadProteinDetectionProtocol(XmlReader xr)
    {
        var pdp = new ProteinDetectionProtocol
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
        };
        var asRef = xr.GetAttribute("analysisSoftware_ref");
        if (!string.IsNullOrEmpty(asRef)) pdp.AnalysisSoftwarePtr = new AnalysisSoftware { Id = asRef };
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

    private static SpectrumIdentification ReadSpectrumIdentification(XmlReader xr)
    {
        var si = new SpectrumIdentification
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
            ActivityDate = xr.GetAttribute("activityDate") ?? string.Empty,
        };
        var protoRef = xr.GetAttribute("spectrumIdentificationProtocol_ref");
        if (!string.IsNullOrEmpty(protoRef))
            si.SpectrumIdentificationProtocolPtr = new SpectrumIdentificationProtocol { Id = protoRef };
        var listRef = xr.GetAttribute("spectrumIdentificationList_ref");
        if (!string.IsNullOrEmpty(listRef))
            si.SpectrumIdentificationListPtr = new SpectrumIdentificationList { Id = listRef };
        if (xr.IsEmptyElement) return si;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "InputSpectra")
            {
                var sdRef = sub.GetAttribute("spectraData_ref");
                if (!string.IsNullOrEmpty(sdRef)) si.InputSpectra.Add(new SpectraData { Id = sdRef });
            }
            else if (sub.LocalName == "SearchDatabaseRef")
            {
                var dbRef = sub.GetAttribute("searchDatabase_ref");
                if (!string.IsNullOrEmpty(dbRef)) si.SearchDatabase.Add(new SearchDatabase { Id = dbRef });
            }
        }
        return si;
    }

    private static void ReadProteinDetectionInto(XmlReader xr, ProteinDetection pd)
    {
        pd.Id = xr.GetAttribute("id") ?? string.Empty;
        pd.Name = xr.GetAttribute("name") ?? string.Empty;
        pd.ActivityDate = xr.GetAttribute("activityDate") ?? string.Empty;
        var protoRef = xr.GetAttribute("proteinDetectionProtocol_ref");
        if (!string.IsNullOrEmpty(protoRef)) pd.ProteinDetectionProtocolPtr = new ProteinDetectionProtocol { Id = protoRef };
        var listRef = xr.GetAttribute("proteinDetectionList_ref");
        if (!string.IsNullOrEmpty(listRef)) pd.ProteinDetectionListPtr = new ProteinDetectionList { Id = listRef };
        if (xr.IsEmptyElement) return;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "InputSpectrumIdentifications")
            {
                var sirRef = sub.GetAttribute("spectrumIdentificationList_ref");
                if (!string.IsNullOrEmpty(sirRef)) pd.InputSpectrumIdentifications.Add(new SpectrumIdentificationList { Id = sirRef });
            }
        }
    }

    private static ProteinDetectionList ReadProteinDetectionList(XmlReader xr)
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
                pdl.ProteinAmbiguityGroup.Add(ReadProteinAmbiguityGroup(sub));
            else if (sub.LocalName is "cvParam" or "userParam")
                ReadOneParamInto(sub, pdl);
        }
        return pdl;
    }

    private static ProteinAmbiguityGroup ReadProteinAmbiguityGroup(XmlReader xr)
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
                pag.ProteinDetectionHypothesis.Add(ReadProteinDetectionHypothesis(sub));
            else if (sub.LocalName is "cvParam" or "userParam")
                ReadOneParamInto(sub, pag);
        }
        return pag;
    }

    private static ProteinDetectionHypothesis ReadProteinDetectionHypothesis(XmlReader xr)
    {
        var pdh = new ProteinDetectionHypothesis
        {
            Id = xr.GetAttribute("id") ?? string.Empty,
            Name = xr.GetAttribute("name") ?? string.Empty,
        };
        if (bool.TryParse(xr.GetAttribute("passThreshold"), out var pt)) pdh.PassThreshold = pt;
        var dbRef = xr.GetAttribute("dBSequence_ref");
        if (!string.IsNullOrEmpty(dbRef)) pdh.DBSequencePtr = new DBSequence { Id = dbRef };
        if (xr.IsEmptyElement) return pdh;
        using var sub = xr.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element) continue;
            if (sub.LocalName == "PeptideHypothesis")
            {
                var ph = new PeptideHypothesis();
                var peRef = sub.GetAttribute("peptideEvidence_ref");
                if (!string.IsNullOrEmpty(peRef)) ph.PeptideEvidencePtr = new PeptideEvidence { Id = peRef };
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

    /// <summary>Reads the text content of the current element, leaving the reader positioned on
    /// its <c>EndElement</c>. Avoids the advancement-past-next-sibling behavior of
    /// <see cref="XmlReader.ReadElementContentAsString()"/>, which would cause our outer
    /// while-loops to skip whatever element follows.</summary>
    private static string ReadElementText(XmlReader sub)
    {
        if (sub.IsEmptyElement) return string.Empty;
        var sb = new System.Text.StringBuilder();
        while (sub.Read() && sub.NodeType != XmlNodeType.EndElement)
            if (sub.NodeType is XmlNodeType.Text or XmlNodeType.CDATA) sb.Append(sub.Value);
        return sb.ToString();
    }
}
