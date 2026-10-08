using System.Globalization;
using System.Xml;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.TraData;

/// <summary>
/// TraML reader / writer. Port of <c>pwiz::tradata::IO::read</c> + <c>write</c>
/// (cpp's IO.cpp). Two-pass read: first pass populates the lists, second pass
/// resolves <c>peptideRef</c> / <c>compoundRef</c> / <c>proteinRef</c> / etc.
/// against the populated lookup maps.
/// </summary>
public static class TraDataIO
{
    private const string Ns = "http://psi.hupo.org/ms/traml";
    private static readonly System.Text.CompositeFormat s_schemaLocation =
        System.Text.CompositeFormat.Parse(
            "http://psi.hupo.org/ms/traml http://www.peptideatlas.org/tmp/TraML/{0}/TraML{0}.xsd");

    // ---------------------------------------------------------------------------
    // Write
    // ---------------------------------------------------------------------------

    /// <summary>Writes <paramref name="td"/> to <paramref name="stream"/> as TraML.</summary>
    public static void Write(Stream stream, TraData td)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(td);
        var settings = new XmlWriterSettings
        {
            Indent = true, IndentChars = "  ", Encoding = new System.Text.UTF8Encoding(false),
            OmitXmlDeclaration = false, CloseOutput = false, NewLineChars = "\n",
        };
        using var w = XmlWriter.Create(stream, settings);
        w.WriteStartDocument();
        WriteTraData(w, td);
        w.WriteEndDocument();
    }

    /// <summary>Convenience overload — opens <paramref name="path"/> truncating.</summary>
    public static void WriteFile(string path, TraData td)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var fs = File.Create(path);
        Write(fs, td);
    }

    private static void WriteTraData(XmlWriter w, TraData td)
    {
        w.WriteStartElement("TraML", Ns);
        w.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
        w.WriteAttributeString("xsi", "schemaLocation", null, string.Format(CultureInfo.InvariantCulture, s_schemaLocation, td.Version));
        w.WriteAttributeString("version", td.Version);

        WriteCvList(w, td.CVs);
        if (td.Contacts.Count > 0) WriteContactList(w, td.Contacts);
        if (td.Publications.Count > 0) WriteList(w, "PublicationList", td.Publications, WritePublication);
        if (td.Instruments.Count > 0) WriteList(w, "InstrumentList", td.Instruments, WriteInstrument);
        if (td.Software.Count > 0) WriteList(w, "SoftwareList", td.Software, WriteSoftware);
        if (td.Proteins.Count > 0) WriteList(w, "ProteinList", td.Proteins, WriteProtein);
        if (td.Peptides.Count > 0 || td.Compounds.Count > 0)
        {
            w.WriteStartElement("CompoundList", Ns);
            foreach (var p in td.Peptides) WritePeptide(w, p);
            foreach (var c in td.Compounds) WriteCompound(w, c);
            w.WriteEndElement();
        }
        if (td.Transitions.Count > 0) WriteList(w, "TransitionList", td.Transitions, WriteTransition);
        WriteTargetList(w, td.Targets);
        w.WriteEndElement();
    }

    private static void WriteList<T>(XmlWriter w, string elementName, IList<T> items, Action<XmlWriter, T> writer)
    {
        w.WriteStartElement(elementName, Ns);
        w.WriteAttributeString("count", items.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var item in items) writer(w, item);
        w.WriteEndElement();
    }

    private static void WriteCvList(XmlWriter w, IList<CV> cvs)
    {
        w.WriteStartElement("cvList", Ns);
        w.WriteAttributeString("count", cvs.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var cv in cvs)
        {
            w.WriteStartElement("cv", Ns);
            w.WriteAttributeString("id", cv.Id);
            w.WriteAttributeString("fullName", cv.FullName);
            if (!string.IsNullOrEmpty(cv.Version)) w.WriteAttributeString("version", cv.Version);
            w.WriteAttributeString("URI", cv.Uri);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteContactList(XmlWriter w, IList<Contact> contacts)
    {
        w.WriteStartElement("ContactList", Ns);
        w.WriteAttributeString("count", contacts.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var c in contacts)
        {
            w.WriteStartElement("Contact", Ns);
            w.WriteAttributeString("id", c.Id);
            WriteParams(w, c);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WritePublication(XmlWriter w, Publication p)
    {
        w.WriteStartElement("Publication", Ns);
        w.WriteAttributeString("id", p.Id);
        WriteParams(w, p);
        w.WriteEndElement();
    }

    private static void WriteInstrument(XmlWriter w, Instrument inst)
    {
        w.WriteStartElement("Instrument", Ns);
        w.WriteAttributeString("id", inst.Id);
        WriteParams(w, inst);
        w.WriteEndElement();
    }

    private static void WriteSoftware(XmlWriter w, Software sw)
    {
        w.WriteStartElement("Software", Ns);
        w.WriteAttributeString("id", sw.Id);
        w.WriteAttributeString("version", sw.Version);
        WriteParams(w, sw);
        w.WriteEndElement();
    }

    private static void WriteProtein(XmlWriter w, Protein p)
    {
        w.WriteStartElement("Protein", Ns);
        w.WriteAttributeString("id", p.Id);
        WriteParams(w, p);
        if (!string.IsNullOrEmpty(p.Sequence))
        {
            w.WriteStartElement("Sequence", Ns);
            w.WriteString(p.Sequence);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WritePeptide(XmlWriter w, Peptide p)
    {
        w.WriteStartElement("Peptide", Ns);
        w.WriteAttributeString("id", p.Id);
        w.WriteAttributeString("sequence", p.Sequence);
        WriteParams(w, p);
        // ProteinRefs
        foreach (var pr in p.Proteins)
        {
            w.WriteStartElement("ProteinRef", Ns);
            w.WriteAttributeString("ref", pr.Id);
            w.WriteEndElement();
        }
        foreach (var mod in p.Modifications) WriteModification(w, mod);
        foreach (var rt in p.RetentionTimes) WriteRetentionTime(w, rt);
        if (!p.Evidence.IsEmpty)
        {
            w.WriteStartElement("Evidence", Ns);
            WriteParams(w, p.Evidence);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteCompound(XmlWriter w, Compound c)
    {
        w.WriteStartElement("Compound", Ns);
        w.WriteAttributeString("id", c.Id);
        WriteParams(w, c);
        foreach (var rt in c.RetentionTimes) WriteRetentionTime(w, rt);
        w.WriteEndElement();
    }

    private static void WriteModification(XmlWriter w, Modification mod)
    {
        w.WriteStartElement("Modification", Ns);
        w.WriteAttributeString("location", mod.Location.ToString(CultureInfo.InvariantCulture));
        w.WriteAttributeString("monoisotopicMassDelta", mod.MonoisotopicMassDelta.ToString("R", CultureInfo.InvariantCulture));
        w.WriteAttributeString("averageMassDelta", mod.AverageMassDelta.ToString("R", CultureInfo.InvariantCulture));
        WriteParams(w, mod);
        w.WriteEndElement();
    }

    private static void WriteRetentionTime(XmlWriter w, RetentionTime rt)
    {
        w.WriteStartElement("RetentionTime", Ns);
        if (rt.Software is not null) w.WriteAttributeString("softwareRef", rt.Software.Id);
        WriteParams(w, rt);
        w.WriteEndElement();
    }

    private static void WritePrediction(XmlWriter w, Prediction pred)
    {
        w.WriteStartElement("Prediction", Ns);
        if (pred.Software is not null) w.WriteAttributeString("softwareRef", pred.Software.Id);
        if (pred.Contact is not null) w.WriteAttributeString("contactRef", pred.Contact.Id);
        WriteParams(w, pred);
        w.WriteEndElement();
    }

    private static void WriteConfiguration(XmlWriter w, Configuration cfg)
    {
        w.WriteStartElement("Configuration", Ns);
        if (cfg.Contact is not null) w.WriteAttributeString("contactRef", cfg.Contact.Id);
        if (cfg.Instrument is not null) w.WriteAttributeString("instrumentRef", cfg.Instrument.Id);
        WriteParams(w, cfg);
        foreach (var v in cfg.Validations)
        {
            w.WriteStartElement("ValidationStatus", Ns);
            WriteParams(w, v);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteTransition(XmlWriter w, Transition t)
    {
        w.WriteStartElement("Transition", Ns);
        w.WriteAttributeString("id", t.Id);
        if (t.Peptide is not null) w.WriteAttributeString("peptideRef", t.Peptide.Id);
        if (t.Compound is not null) w.WriteAttributeString("compoundRef", t.Compound.Id);
        WriteParams(w, t);

        // Precursor (Q1)
        w.WriteStartElement("Precursor", Ns);
        WriteParams(w, t.Precursor);
        w.WriteEndElement();

        // Product (Q3)
        w.WriteStartElement("Product", Ns);
        WriteParams(w, t.Product);
        w.WriteEndElement();

        if (!t.RetentionTime.IsEmpty || t.RetentionTime.Software is not null)
            WriteRetentionTime(w, t.RetentionTime);
        if (!t.Prediction.IsEmpty || t.Prediction.Software is not null || t.Prediction.Contact is not null)
            WritePrediction(w, t.Prediction);

        if (t.Interpretations.Count > 0)
        {
            w.WriteStartElement("InterpretationList", Ns);
            foreach (var i in t.Interpretations)
            {
                w.WriteStartElement("Interpretation", Ns);
                WriteParams(w, i);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        if (t.Configurations.Count > 0)
        {
            w.WriteStartElement("ConfigurationList", Ns);
            foreach (var c in t.Configurations) WriteConfiguration(w, c);
            w.WriteEndElement();
        }

        w.WriteEndElement();
    }

    private static void WriteTarget(XmlWriter w, Target t)
    {
        w.WriteStartElement("Target", Ns);
        w.WriteAttributeString("id", t.Id);
        if (t.Peptide is not null) w.WriteAttributeString("peptideRef", t.Peptide.Id);
        if (t.Compound is not null) w.WriteAttributeString("compoundRef", t.Compound.Id);
        WriteParams(w, t);

        w.WriteStartElement("Precursor", Ns);
        WriteParams(w, t.Precursor);
        w.WriteEndElement();

        if (!t.RetentionTime.IsEmpty || t.RetentionTime.Software is not null)
            WriteRetentionTime(w, t.RetentionTime);
        if (t.Configurations.Count > 0)
        {
            w.WriteStartElement("ConfigurationList", Ns);
            foreach (var c in t.Configurations) WriteConfiguration(w, c);
            w.WriteEndElement();
        }

        w.WriteEndElement();
    }

    private static void WriteTargetList(XmlWriter w, TargetList list)
    {
        // TargetList is always emitted (cpp does too) — it carries the include/exclude pair
        // plus any direct params.
        w.WriteStartElement("TargetList", Ns);
        WriteParams(w, list);
        if (list.IncludeList.Count > 0)
            WriteList(w, "TargetIncludeList", list.IncludeList, WriteTarget);
        if (list.ExcludeList.Count > 0)
            WriteList(w, "TargetExcludeList", list.ExcludeList, WriteTarget);
        w.WriteEndElement();
    }

    private static void WriteParams(XmlWriter w, ParamContainer pc)
    {
        foreach (var cv in pc.CVParams) WriteCvParam(w, cv);
        foreach (var u in pc.UserParams) WriteUserParam(w, u);
    }

    private static void WriteCvParam(XmlWriter w, CVParam p)
    {
        w.WriteStartElement("cvParam", Ns);
        var info = CvLookup.CvTermInfo(p.Cvid);
        // cvRef = the ontology prefix ("MS", "UO", "UNIMOD"). CvLookup exposes this via Info.Ontology.
        // Fall back to "MS" so we always emit something parseable; readers that don't have MS_*
        // accession recognise the term by accession anyway.
        w.WriteAttributeString("cvRef", string.IsNullOrEmpty(info.Prefix) ? "MS" : info.Prefix);
        w.WriteAttributeString("accession", FormatAccession(info));
        w.WriteAttributeString("name", info.Name);
        w.WriteAttributeString("value", p.Value);
        if (p.Units != CVID.CVID_Unknown)
        {
            var u = CvLookup.CvTermInfo(p.Units);
            w.WriteAttributeString("unitCvRef", string.IsNullOrEmpty(u.Prefix) ? "UO" : u.Prefix);
            w.WriteAttributeString("unitAccession", FormatAccession(u));
            w.WriteAttributeString("unitName", u.Name);
        }
        w.WriteEndElement();
    }

    private static void WriteUserParam(XmlWriter w, UserParam u)
    {
        w.WriteStartElement("userParam", Ns);
        w.WriteAttributeString("name", u.Name);
        if (!string.IsNullOrEmpty(u.Type)) w.WriteAttributeString("type", u.Type);
        w.WriteAttributeString("value", u.Value);
        if (u.Units != CVID.CVID_Unknown)
        {
            var ui = CvLookup.CvTermInfo(u.Units);
            w.WriteAttributeString("unitCvRef", string.IsNullOrEmpty(ui.Prefix) ? "UO" : ui.Prefix);
            w.WriteAttributeString("unitAccession", FormatAccession(ui));
            w.WriteAttributeString("unitName", ui.Name);
        }
        w.WriteEndElement();
    }

    private static string FormatAccession(CVTermInfo info)
    {
        // CVTermInfo.Accession is already in "MS:1001234" / "UO:0000123" form (matches the CV).
        return string.IsNullOrEmpty(info.Id) ? "MS:0000000" : info.Id;
    }

    // ---------------------------------------------------------------------------
    // Read
    // ---------------------------------------------------------------------------

    /// <summary>Reads a TraML stream into a fresh <see cref="TraData"/>.</summary>
    public static TraData Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var td = new TraData();
        using var r = XmlReader.Create(stream, new XmlReaderSettings
        {
            IgnoreWhitespace = true, IgnoreComments = true, DtdProcessing = DtdProcessing.Ignore, CloseInput = false,
        });
        // Use ref maps populated during the first pass; we then patch ref-bearing entities.
        var ctx = new ReadContext();
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.Element && r.LocalName == "TraML")
            {
                td.Version = r.GetAttribute("version") ?? td.Version;
                ReadTraMlBody(r, td, ctx);
                break;
            }
        }
        ResolveReferences(td, ctx);
        return td;
    }

    /// <summary>Convenience overload — opens <paramref name="path"/> for read.</summary>
    public static TraData ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var fs = File.OpenRead(path);
        return Read(fs);
    }

    private sealed class ReadContext
    {
        public readonly Dictionary<string, Contact> Contacts = new(StringComparer.Ordinal);
        public readonly Dictionary<string, Instrument> Instruments = new(StringComparer.Ordinal);
        public readonly Dictionary<string, Software> Software = new(StringComparer.Ordinal);
        public readonly Dictionary<string, Protein> Proteins = new(StringComparer.Ordinal);
        public readonly Dictionary<string, Peptide> Peptides = new(StringComparer.Ordinal);
        public readonly Dictionary<string, Compound> Compounds = new(StringComparer.Ordinal);

        // Deferred ref-resolution lists. Each entry holds the entity to patch + the id it needs.
        public readonly List<(RetentionTime, string)> RtSoftwareRefs = new();
        public readonly List<(Prediction, string, string)> PredictionRefs = new(); // (pred, softwareRef, contactRef)
        public readonly List<(Configuration, string, string)> ConfigRefs = new(); // (cfg, contactRef, instrumentRef)
        public readonly List<(Transition, string, string)> TransitionRefs = new();
        public readonly List<(Target, string, string)> TargetRefs = new();
        public readonly List<(Peptide, List<string>)> PeptideProteinRefs = new();
    }

    private static void ReadTraMlBody(XmlReader r, TraData td, ReadContext ctx)
    {
        if (r.IsEmptyElement) return;
        int depth = r.Depth;
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.EndElement && r.Depth == depth) break;
            if (r.NodeType != XmlNodeType.Element) continue;
            switch (r.LocalName)
            {
                case "cvList": ReadCvList(r, td.CVs); break;
                case "ContactList": ReadContactList(r, td, ctx); break;
                case "PublicationList": ReadEntityList(r, td.Publications, ReadPublication); break;
                case "InstrumentList": ReadInstrumentList(r, td, ctx); break;
                case "SoftwareList": ReadSoftwareList(r, td, ctx); break;
                case "ProteinList": ReadProteinList(r, td, ctx); break;
                case "CompoundList": ReadCompoundList(r, td, ctx); break;
                case "TransitionList": ReadEntityList(r, td.Transitions, (XmlReader rr) => ReadTransition(rr, ctx)); break;
                case "TargetList": ReadTargetList(r, td.Targets, ctx); break;
                default: r.Skip(); break;
            }
        }
    }

    private static void ReadCvList(XmlReader r, List<CV> cvs)
    {
        if (r.IsEmptyElement) return;
        int d = r.Depth;
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.EndElement && r.Depth == d) break;
            if (r.NodeType != XmlNodeType.Element || r.LocalName != "cv") continue;
            cvs.Add(new CV
            {
                Id = r.GetAttribute("id") ?? string.Empty,
                FullName = r.GetAttribute("fullName") ?? string.Empty,
                Version = r.GetAttribute("version") ?? string.Empty,
                Uri = r.GetAttribute("URI") ?? string.Empty,
            });
            if (!r.IsEmptyElement) r.Skip();
        }
    }

    private static void ReadContactList(XmlReader r, TraData td, ReadContext ctx)
    {
        ReadEntityList(r, td.Contacts, (XmlReader rr) =>
        {
            var c = new Contact(rr.GetAttribute("id") ?? string.Empty);
            ReadParamsInto(rr, c);
            ctx.Contacts[c.Id] = c;
            return c;
        });
    }

    private static void ReadInstrumentList(XmlReader r, TraData td, ReadContext ctx)
    {
        ReadEntityList(r, td.Instruments, (XmlReader rr) =>
        {
            var inst = new Instrument(rr.GetAttribute("id") ?? string.Empty);
            ReadParamsInto(rr, inst);
            ctx.Instruments[inst.Id] = inst;
            return inst;
        });
    }

    private static void ReadSoftwareList(XmlReader r, TraData td, ReadContext ctx)
    {
        ReadEntityList(r, td.Software, (XmlReader rr) =>
        {
            var sw = new Software(rr.GetAttribute("id") ?? string.Empty)
            {
                Version = rr.GetAttribute("version") ?? string.Empty,
            };
            ReadParamsInto(rr, sw);
            ctx.Software[sw.Id] = sw;
            return sw;
        });
    }

    private static void ReadProteinList(XmlReader r, TraData td, ReadContext ctx)
    {
        ReadEntityList(r, td.Proteins, (XmlReader rr) =>
        {
            var p = new Protein(rr.GetAttribute("id") ?? string.Empty);
            // Children: cvParam, userParam, and <Sequence>...</Sequence>
            if (!rr.IsEmptyElement)
            {
                int d = rr.Depth;
                while (rr.Read())
                {
                    if (rr.NodeType == XmlNodeType.EndElement && rr.Depth == d) break;
                    if (rr.NodeType != XmlNodeType.Element) continue;
                    switch (rr.LocalName)
                    {
                        case "cvParam": ReadCvParamInto(rr, p); break;
                        case "userParam": ReadUserParamInto(rr, p); break;
                        case "Sequence":
                            // Read text content WITHOUT advancing past </Sequence>; if we
                            // used ReadElementContentAsString here, the inner loop would
                            // walk past </Protein> without noticing (its break condition
                            // checks EndElement at the protein depth, but the call leaves
                            // the reader on whatever follows </Sequence> — including
                            // sibling <CompoundList> later in the document, which the
                            // default-case Skip then eats).
                            p.Sequence = ReadElementText(rr);
                            break;
                        default: rr.Skip(); break;
                    }
                }
            }
            ctx.Proteins[p.Id] = p;
            return p;
        });
    }

    private static void ReadCompoundList(XmlReader r, TraData td, ReadContext ctx)
    {
        if (r.IsEmptyElement) return;
        int d = r.Depth;
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.EndElement && r.Depth == d) break;
            if (r.NodeType != XmlNodeType.Element) continue;
            if (r.LocalName == "Peptide")
            {
                var p = ReadPeptide(r, ctx);
                td.Peptides.Add(p);
                ctx.Peptides[p.Id] = p;
            }
            else if (r.LocalName == "Compound")
            {
                var c = ReadCompound(r, ctx);
                td.Compounds.Add(c);
                ctx.Compounds[c.Id] = c;
            }
            else r.Skip();
        }
    }

    private static Peptide ReadPeptide(XmlReader r, ReadContext ctx)
    {
        var p = new Peptide(r.GetAttribute("id") ?? string.Empty)
        {
            Sequence = r.GetAttribute("sequence") ?? string.Empty,
        };
        var deferredProteinRefs = new List<string>();
        if (!r.IsEmptyElement)
        {
            int d = r.Depth;
            while (r.Read())
            {
                if (r.NodeType == XmlNodeType.EndElement && r.Depth == d) break;
                if (r.NodeType != XmlNodeType.Element) continue;
                switch (r.LocalName)
                {
                    case "cvParam": ReadCvParamInto(r, p); break;
                    case "userParam": ReadUserParamInto(r, p); break;
                    case "ProteinRef":
                        {
                            string id = r.GetAttribute("ref") ?? string.Empty;
                            if (id.Length > 0) deferredProteinRefs.Add(id);
                            if (!r.IsEmptyElement) r.Skip();
                            break;
                        }
                    case "Modification": p.Modifications.Add(ReadModification(r)); break;
                    case "RetentionTime": p.RetentionTimes.Add(ReadRetentionTime(r, ctx)); break;
                    case "Evidence": ReadParamsInto(r, p.Evidence); break;
                    default: r.Skip(); break;
                }
            }
        }
        if (deferredProteinRefs.Count > 0) ctx.PeptideProteinRefs.Add((p, deferredProteinRefs));
        return p;
    }

    private static Compound ReadCompound(XmlReader r, ReadContext ctx)
    {
        var c = new Compound(r.GetAttribute("id") ?? string.Empty);
        if (!r.IsEmptyElement)
        {
            int d = r.Depth;
            while (r.Read())
            {
                if (r.NodeType == XmlNodeType.EndElement && r.Depth == d) break;
                if (r.NodeType != XmlNodeType.Element) continue;
                switch (r.LocalName)
                {
                    case "cvParam": ReadCvParamInto(r, c); break;
                    case "userParam": ReadUserParamInto(r, c); break;
                    case "RetentionTime": c.RetentionTimes.Add(ReadRetentionTime(r, ctx)); break;
                    default: r.Skip(); break;
                }
            }
        }
        return c;
    }

    private static Modification ReadModification(XmlReader r)
    {
        var m = new Modification
        {
            Location = ParseInt(r.GetAttribute("location")),
            MonoisotopicMassDelta = ParseDouble(r.GetAttribute("monoisotopicMassDelta")),
            AverageMassDelta = ParseDouble(r.GetAttribute("averageMassDelta")),
        };
        ReadParamsInto(r, m);
        return m;
    }

    private static RetentionTime ReadRetentionTime(XmlReader r, ReadContext ctx)
    {
        var rt = new RetentionTime();
        string softwareRef = r.GetAttribute("softwareRef") ?? string.Empty;
        ReadParamsInto(r, rt);
        if (softwareRef.Length > 0) ctx.RtSoftwareRefs.Add((rt, softwareRef));
        return rt;
    }

    private static Prediction ReadPrediction(XmlReader r, ReadContext ctx)
    {
        var pred = new Prediction();
        string softwareRef = r.GetAttribute("softwareRef") ?? string.Empty;
        string contactRef = r.GetAttribute("contactRef") ?? string.Empty;
        ReadParamsInto(r, pred);
        if (softwareRef.Length > 0 || contactRef.Length > 0)
            ctx.PredictionRefs.Add((pred, softwareRef, contactRef));
        return pred;
    }

    private static Configuration ReadConfiguration(XmlReader r, ReadContext ctx)
    {
        var cfg = new Configuration();
        string contactRef = r.GetAttribute("contactRef") ?? string.Empty;
        string instrumentRef = r.GetAttribute("instrumentRef") ?? string.Empty;
        if (!r.IsEmptyElement)
        {
            int d = r.Depth;
            while (r.Read())
            {
                if (r.NodeType == XmlNodeType.EndElement && r.Depth == d) break;
                if (r.NodeType != XmlNodeType.Element) continue;
                switch (r.LocalName)
                {
                    case "cvParam": ReadCvParamInto(r, cfg); break;
                    case "userParam": ReadUserParamInto(r, cfg); break;
                    case "ValidationStatus":
                        {
                            var v = new Validation();
                            ReadParamsInto(r, v);
                            cfg.Validations.Add(v);
                            break;
                        }
                    default: r.Skip(); break;
                }
            }
        }
        if (contactRef.Length > 0 || instrumentRef.Length > 0)
            ctx.ConfigRefs.Add((cfg, contactRef, instrumentRef));
        return cfg;
    }

    private static Transition ReadTransition(XmlReader r, ReadContext ctx)
    {
        var t = new Transition { Id = r.GetAttribute("id") ?? string.Empty };
        string peptideRef = r.GetAttribute("peptideRef") ?? string.Empty;
        string compoundRef = r.GetAttribute("compoundRef") ?? string.Empty;
        if (!r.IsEmptyElement)
        {
            int d = r.Depth;
            while (r.Read())
            {
                if (r.NodeType == XmlNodeType.EndElement && r.Depth == d) break;
                if (r.NodeType != XmlNodeType.Element) continue;
                switch (r.LocalName)
                {
                    case "cvParam": ReadCvParamInto(r, t); break;
                    case "userParam": ReadUserParamInto(r, t); break;
                    case "Precursor": ReadParamsInto(r, t.Precursor); break;
                    case "Product": ReadParamsInto(r, t.Product); break;
                    case "RetentionTime":
                        {
                            var rt = ReadRetentionTime(r, ctx);
                            // Transition.RetentionTime is a single value, not a list — overwrite.
                            t.RetentionTime.Software = rt.Software;
                            foreach (var cv in rt.CVParams) t.RetentionTime.CVParams.Add(cv);
                            foreach (var u in rt.UserParams) t.RetentionTime.UserParams.Add(u);
                            break;
                        }
                    case "Prediction":
                        {
                            var pred = ReadPrediction(r, ctx);
                            t.Prediction.Software = pred.Software;
                            t.Prediction.Contact = pred.Contact;
                            foreach (var cv in pred.CVParams) t.Prediction.CVParams.Add(cv);
                            foreach (var u in pred.UserParams) t.Prediction.UserParams.Add(u);
                            break;
                        }
                    case "InterpretationList":
                        ReadEntityList(r, t.Interpretations, rr =>
                        {
                            var i = new Interpretation();
                            ReadParamsInto(rr, i);
                            return i;
                        });
                        break;
                    case "ConfigurationList":
                        ReadEntityList(r, t.Configurations, rr => ReadConfiguration(rr, ctx));
                        break;
                    default: r.Skip(); break;
                }
            }
        }
        if (peptideRef.Length > 0 || compoundRef.Length > 0)
            ctx.TransitionRefs.Add((t, peptideRef, compoundRef));
        return t;
    }

    private static Target ReadTarget(XmlReader r, ReadContext ctx)
    {
        var t = new Target { Id = r.GetAttribute("id") ?? string.Empty };
        string peptideRef = r.GetAttribute("peptideRef") ?? string.Empty;
        string compoundRef = r.GetAttribute("compoundRef") ?? string.Empty;
        if (!r.IsEmptyElement)
        {
            int d = r.Depth;
            while (r.Read())
            {
                if (r.NodeType == XmlNodeType.EndElement && r.Depth == d) break;
                if (r.NodeType != XmlNodeType.Element) continue;
                switch (r.LocalName)
                {
                    case "cvParam": ReadCvParamInto(r, t); break;
                    case "userParam": ReadUserParamInto(r, t); break;
                    case "Precursor": ReadParamsInto(r, t.Precursor); break;
                    case "RetentionTime":
                        {
                            var rt = ReadRetentionTime(r, ctx);
                            t.RetentionTime.Software = rt.Software;
                            foreach (var cv in rt.CVParams) t.RetentionTime.CVParams.Add(cv);
                            foreach (var u in rt.UserParams) t.RetentionTime.UserParams.Add(u);
                            break;
                        }
                    case "ConfigurationList":
                        ReadEntityList(r, t.Configurations, rr => ReadConfiguration(rr, ctx));
                        break;
                    default: r.Skip(); break;
                }
            }
        }
        if (peptideRef.Length > 0 || compoundRef.Length > 0)
            ctx.TargetRefs.Add((t, peptideRef, compoundRef));
        return t;
    }

    private static void ReadTargetList(XmlReader r, TargetList list, ReadContext ctx)
    {
        if (r.IsEmptyElement) return;
        int d = r.Depth;
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.EndElement && r.Depth == d) break;
            if (r.NodeType != XmlNodeType.Element) continue;
            switch (r.LocalName)
            {
                case "cvParam": ReadCvParamInto(r, list); break;
                case "userParam": ReadUserParamInto(r, list); break;
                case "TargetIncludeList": ReadEntityList(r, list.IncludeList, rr => ReadTarget(rr, ctx)); break;
                case "TargetExcludeList": ReadEntityList(r, list.ExcludeList, rr => ReadTarget(rr, ctx)); break;
                default: r.Skip(); break;
            }
        }
    }

    private static Publication ReadPublication(XmlReader r)
    {
        var p = new Publication { Id = r.GetAttribute("id") ?? string.Empty };
        ReadParamsInto(r, p);
        return p;
    }

    private static void ReadEntityList<T>(XmlReader r, List<T> dest, Func<XmlReader, T> readOne)
    {
        if (r.IsEmptyElement) return;
        int d = r.Depth;
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.EndElement && r.Depth == d) break;
            if (r.NodeType != XmlNodeType.Element) continue;
            dest.Add(readOne(r));
        }
    }

    private static void ReadParamsInto(XmlReader r, ParamContainer pc)
    {
        if (r.IsEmptyElement) return;
        int d = r.Depth;
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.EndElement && r.Depth == d) break;
            if (r.NodeType != XmlNodeType.Element) continue;
            switch (r.LocalName)
            {
                case "cvParam": ReadCvParamInto(r, pc); break;
                case "userParam": ReadUserParamInto(r, pc); break;
                default: r.Skip(); break;
            }
        }
    }

    private static void ReadCvParamInto(XmlReader r, ParamContainer pc)
    {
        string accession = r.GetAttribute("accession") ?? string.Empty;
        string value = r.GetAttribute("value") ?? string.Empty;
        string unitAccession = r.GetAttribute("unitAccession") ?? string.Empty;
        CVID cvid = CvLookup.CvTermInfo(accession).Cvid;
        CVID units = string.IsNullOrEmpty(unitAccession) ? CVID.CVID_Unknown
            : CvLookup.CvTermInfo(unitAccession).Cvid;
        pc.Set(cvid, value, units);
        if (!r.IsEmptyElement) r.Skip();
    }

    private static void ReadUserParamInto(XmlReader r, ParamContainer pc)
    {
        string name = r.GetAttribute("name") ?? string.Empty;
        string type = r.GetAttribute("type") ?? string.Empty;
        string value = r.GetAttribute("value") ?? string.Empty;
        string unitAccession = r.GetAttribute("unitAccession") ?? string.Empty;
        CVID units = string.IsNullOrEmpty(unitAccession) ? CVID.CVID_Unknown
            : CvLookup.CvTermInfo(unitAccession).Cvid;
        pc.UserParams.Add(new UserParam(name, value, type, units));
        if (!r.IsEmptyElement) r.Skip();
    }

    private static string ReadElementText(XmlReader r)
    {
        // Drain text/cdata children but leave the reader ON the EndElement of the current
        // element, so the calling loop's natural `Read()` advances to the next sibling.
        // `XmlReader.ReadElementContentAsString` would advance past the EndElement, which
        // breaks depth-tracking parents that need to detect their own EndElement.
        if (r.IsEmptyElement) return string.Empty;
        int depth = r.Depth;
        var sb = new System.Text.StringBuilder();
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.EndElement && r.Depth == depth) break;
            if (r.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace)
                sb.Append(r.Value);
        }
        return sb.ToString();
    }

    private static int ParseInt(string? s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

    private static double ParseDouble(string? s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0.0;

    // ---------------------------------------------------------------------------
    // Reference resolution (post-pass)
    // ---------------------------------------------------------------------------

    private static void ResolveReferences(TraData td, ReadContext ctx)
    {
        foreach (var (rt, swRef) in ctx.RtSoftwareRefs)
            if (ctx.Software.TryGetValue(swRef, out var sw)) rt.Software = sw;

        foreach (var (pred, swRef, conRef) in ctx.PredictionRefs)
        {
            if (swRef.Length > 0 && ctx.Software.TryGetValue(swRef, out var sw)) pred.Software = sw;
            if (conRef.Length > 0 && ctx.Contacts.TryGetValue(conRef, out var c)) pred.Contact = c;
        }

        foreach (var (cfg, conRef, instRef) in ctx.ConfigRefs)
        {
            if (conRef.Length > 0 && ctx.Contacts.TryGetValue(conRef, out var c)) cfg.Contact = c;
            if (instRef.Length > 0 && ctx.Instruments.TryGetValue(instRef, out var inst)) cfg.Instrument = inst;
        }

        foreach (var (t, pepRef, comRef) in ctx.TransitionRefs)
        {
            if (pepRef.Length > 0 && ctx.Peptides.TryGetValue(pepRef, out var p)) t.Peptide = p;
            if (comRef.Length > 0 && ctx.Compounds.TryGetValue(comRef, out var c)) t.Compound = c;
        }

        foreach (var (t, pepRef, comRef) in ctx.TargetRefs)
        {
            if (pepRef.Length > 0 && ctx.Peptides.TryGetValue(pepRef, out var p)) t.Peptide = p;
            if (comRef.Length > 0 && ctx.Compounds.TryGetValue(comRef, out var c)) t.Compound = c;
        }

        foreach (var (pep, refs) in ctx.PeptideProteinRefs)
            foreach (var pr in refs)
                if (ctx.Proteins.TryGetValue(pr, out var proteinObj))
                    pep.Proteins.Add(proteinObj);
    }
}
