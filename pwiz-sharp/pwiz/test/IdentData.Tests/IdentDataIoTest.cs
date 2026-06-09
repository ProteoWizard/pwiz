using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.IdentData;
using Pwiz.Data.IdentData.Mzid;

namespace Pwiz.Data.IdentData.Tests;

/// <summary>
/// One-method end-to-end IO test: builds a populated <see cref="IdentData"/> tree per schema
/// type, round-trips it through <see cref="MzidWriter"/> + <see cref="MzidReader"/>, and asserts
/// deep equality via <see cref="IdentDataDiff"/>. Mirrors cpp's <c>IOTest.cpp</c>; cpp uses 47
/// individual <c>testFoo()</c> functions, we collapse to one <c>[TestMethod]</c> calling 47
/// helpers and accumulate any failures into a single Assert message.
/// </summary>
[TestClass]
public class IdentDataIoTest
{
    [TestMethod]
    public void IdentDataIo()
    {
        var failures = new List<string>();

        TestCase("Identifiable", BuildIdentifiable, failures);
        TestCase("IdentifiableParamContainer", BuildIdentifiableParamContainer, failures);
        TestCase("CV", BuildCv, failures);
        TestCase("BibliographicReference", BuildBibliographicReference, failures);
        TestCase("Person", BuildPerson, failures);
        TestCase("Organization", BuildOrganization, failures);
        TestCase("ContactRole", BuildContactRole, failures);
        TestCase("Provider", BuildProvider, failures);
        TestCase("Sample", BuildSample, failures);
        TestCase("AnalysisSoftware", BuildAnalysisSoftware, failures);
        TestCase("AnalysisSampleCollection", BuildAnalysisSampleCollection, failures);
        TestCase("DBSequence", BuildDbSequence, failures);
        TestCase("Modification", BuildModification, failures);
        TestCase("SubstitutionModification", BuildSubstitutionModification, failures);
        TestCase("Peptide", BuildPeptide, failures);
        TestCase("SequenceCollection", BuildSequenceCollection, failures);
        TestCase("SpectrumIdentification", BuildSpectrumIdentification, failures);
        TestCase("ProteinDetection", BuildProteinDetection, failures);
        TestCase("AnalysisCollection", BuildAnalysisCollection, failures);
        TestCase("SearchModification", BuildSearchModification, failures);
        TestCase("Enzyme", BuildEnzyme, failures);
        TestCase("Enzymes", BuildEnzymes, failures);
        TestCase("Residue", BuildResidue, failures);
        TestCase("AmbiguousResidue", BuildAmbiguousResidue, failures);
        TestCase("MassTable", BuildMassTable, failures);
        TestCase("Filter", BuildFilter, failures);
        TestCase("SpectrumIdentificationProtocol", BuildSpectrumIdentificationProtocol, failures);
        TestCase("ProteinDetectionProtocol", BuildProteinDetectionProtocol, failures);
        TestCase("AnalysisProtocolCollection", BuildAnalysisProtocolCollection, failures);
        TestCase("SpectraData", BuildSpectraData, failures);
        TestCase("SearchDatabase", BuildSearchDatabase, failures);
        TestCase("SourceFile", BuildSourceFile, failures);
        TestCase("Inputs", BuildInputs, failures);
        TestCase("Measure", BuildMeasure, failures);
        TestCase("FragmentArray", BuildFragmentArray, failures);
        TestCase("IonType", BuildIonType, failures);
        TestCase("PeptideEvidence", BuildPeptideEvidence, failures);
        TestCase("SpectrumIdentificationItem", BuildSpectrumIdentificationItem, failures);
        TestCase("SpectrumIdentificationResult", BuildSpectrumIdentificationResult, failures);
        TestCase("ProteinDetectionHypothesis", BuildProteinDetectionHypothesis, failures);
        TestCase("ProteinAmbiguityGroup", BuildProteinAmbiguityGroup, failures);
        TestCase("SpectrumIdentificationList", BuildSpectrumIdentificationList, failures);
        TestCase("ProteinDetectionList", BuildProteinDetectionList, failures);
        TestCase("AnalysisData", BuildAnalysisData, failures);
        TestCase("DataCollection", BuildDataCollection, failures);
        TestCase("BibliographicReferenceList", BuildBibliographicReferenceList, failures);
        TestCase("IdentData", BuildFullIdentData, failures);

        if (failures.Count > 0)
            Assert.Fail($"{failures.Count} sub-test(s) failed:{System.Environment.NewLine}" +
                string.Join(System.Environment.NewLine + "----" + System.Environment.NewLine, failures));
    }

    private static void TestCase(string name, Func<IdentData> build, List<string> failures)
    {
        try
        {
            var orig = build();
            using var ms = new MemoryStream();
            new MzidWriter().Write(orig, ms);
            ms.Position = 0;
            var ident = new IdentData();
            new MzidReader().ReadInto(ms, ident);
            var diff = IdentDataDiff.Diff(orig, ident);
            if (!diff.IsEmpty)
                failures.Add($"[{name}] {diff.Lines.Count} diff(s):{System.Environment.NewLine}{diff}");
        }
        catch (Exception e)
        {
            failures.Add($"[{name}] {e.GetType().Name}: {e.Message}");
        }
    }

    // ============================================================================
    //   Per-type IdentData builders. Each populates one or more representative
    //   instances of the named schema type, plumbed into the minimum surrounding
    //   structure needed for it to round-trip through the document-level writer
    //   (cpp's IOTest uses element-level IO::write/read; we use document-level).
    // ============================================================================

    // -- base / leaf types --

    private static IdentData BuildIdentifiable() =>
        // Identifiable / Name on the root MzIdentML element itself.
        new() { Id = "id", Name = "name" };

    private static IdentData BuildIdentifiableParamContainer()
    {
        // IPC carries Id+Name+ParamContainer. Use an AnalysisSoftware as the carrier.
        var ident = new IdentData();
        var sw = new AnalysisSoftware { Id = "ipc1", Name = "name" };
        sw.SoftwareName.Set(CVID.MS_TIC, "123");
        sw.SoftwareName.UserParams.Add(new UserParam("abc", "123", "!@#"));
        ident.AnalysisSoftwareList.Add(sw);
        return ident;
    }

    private static IdentData BuildCv()
    {
        var ident = new IdentData();
        ident.Cvs.Add(new CV { Id = "PSI-MS", FullName = "PSI-MS", Uri = "http://psidev.info/...", Version = "4.0" });
        return ident;
    }

    private static IdentData BuildBibliographicReference()
    {
        var ident = new IdentData();
        ident.BibliographicReferences.Add(new BibliographicReference
        {
            Id = "br1", Authors = "Smith J", Publication = "JPR", Publisher = "ACS",
            Editor = "Jones K", Year = 1984, Volume = "5", Issue = "1", Pages = "1-10",
            Title = "An example title",
        });
        return ident;
    }

    private static IdentData BuildPerson()
    {
        var ident = new IdentData();
        var p = new Person { Id = "p1", LastName = "Smith", FirstName = "Jane", MidInitials = "Q." };
        p.Set(CVID.MS_contact_address, "123 abc");
        p.Set(CVID.MS_contact_email, "jane@example.com");
        ident.AuditCollection.Add(p);
        // Affiliation referenced organization needs to exist in the audit collection too.
        var org = new Organization { Id = "org1", Name = "Acme" };
        ident.AuditCollection.Add(org);
        p.Affiliations.Add(org);
        return ident;
    }

    private static IdentData BuildOrganization()
    {
        var ident = new IdentData();
        var parent = new Organization { Id = "org-parent", Name = "Parent" };
        var child = new Organization { Id = "org-child", Name = "Child", Parent = parent };
        child.Set(CVID.MS_contact_address, "456 def");
        ident.AuditCollection.Add(parent);
        ident.AuditCollection.Add(child);
        return ident;
    }

    private static IdentData BuildContactRole()
    {
        var ident = new IdentData();
        var contact = new Person { Id = "contact1", LastName = "Smith" };
        ident.AuditCollection.Add(contact);
        var sw = new AnalysisSoftware { Id = "sw1", Name = "MyEngine" };
        sw.ContactRolePtr = new ContactRole
        {
            Role = new CVParam(CVID.MS_software_vendor),
            ContactPtr = contact,
        };
        ident.AnalysisSoftwareList.Add(sw);
        return ident;
    }

    private static IdentData BuildProvider()
    {
        var ident = new IdentData();
        var contact = new Person { Id = "p2", LastName = "Jones" };
        ident.AuditCollection.Add(contact);
        var sw = new AnalysisSoftware { Id = "sw2", Name = "Engine" };
        ident.AnalysisSoftwareList.Add(sw);
        ident.Provider.Id = "PROV_1";
        ident.Provider.AnalysisSoftwarePtr = sw;
        ident.Provider.ContactRolePtr = new ContactRole
        {
            Role = new CVParam(CVID.MS_software_vendor),
            ContactPtr = contact,
        };
        return ident;
    }

    private static IdentData BuildSample()
    {
        var ident = new IdentData();
        var contact = new Person { Id = "c1", LastName = "Smith" };
        ident.AuditCollection.Add(contact);
        var sample = new Sample { Id = "S_1", Name = "Sample 1" };
        sample.Set(CVID.MS_septum, "");
        sample.ContactRole.Add(new ContactRole
        {
            Role = new CVParam(CVID.MS_software_vendor),
            ContactPtr = contact,
        });
        var sub = new Sample { Id = "subSample_ref" };
        ident.AnalysisSampleCollection.Samples.Add(sub);
        sample.SubSamples.Add(sub);
        ident.AnalysisSampleCollection.Samples.Add(sample);
        return ident;
    }

    private static IdentData BuildAnalysisSoftware()
    {
        var ident = new IdentData();
        var contact = new Person { Id = "asc1", LastName = "Smith" };
        ident.AuditCollection.Add(contact);
        var sw = new AnalysisSoftware
        {
            Id = "AS_1", Name = "MyEngine", Version = "1.2", Uri = "http://example.com",
            Customizations = "default",
        };
        sw.SoftwareName.CVParams.Add(new CVParam(CVID.MS_Mascot));
        sw.ContactRolePtr = new ContactRole
        {
            Role = new CVParam(CVID.MS_software_vendor),
            ContactPtr = contact,
        };
        ident.AnalysisSoftwareList.Add(sw);
        return ident;
    }

    private static IdentData BuildAnalysisSampleCollection()
    {
        var ident = new IdentData();
        ident.AnalysisSampleCollection.Samples.Add(new Sample { Id = "S_a", Name = "Sample A" });
        ident.AnalysisSampleCollection.Samples.Add(new Sample { Id = "S_b", Name = "Sample B" });
        return ident;
    }

    private static IdentData BuildDbSequence()
    {
        var ident = new IdentData();
        var sd = new SearchDatabase { Id = "db1", Location = "/tmp/db.fasta" };
        ident.DataCollection.Inputs.SearchDatabase.Add(sd);
        var dbs = new DBSequence
        {
            Id = "dbs1", Name = "human_protein", Length = 250, Accession = "P12345",
            Seq = "MKLVQPS", SearchDatabasePtr = sd,
        };
        dbs.Set(CVID.MS_protein_description, "blahbitty blah blah");
        ident.SequenceCollection.DBSequences.Add(dbs);
        return ident;
    }

    private static IdentData BuildModification()
    {
        var ident = new IdentData();
        var pep = new Peptide { Id = "pep1", PeptideSequence = "PEPTIDER" };
        var mod = new Modification
        {
            Location = 1, AvgMassDelta = 1.001001, MonoisotopicMassDelta = 100.1001,
        };
        mod.Residues.Add('A');
        mod.Residues.Add('C');
        mod.Set(CVID.UNIMOD_Gln__pyro_Glu, "");
        pep.Modifications.Add(mod);
        ident.SequenceCollection.Peptides.Add(pep);
        return ident;
    }

    private static IdentData BuildSubstitutionModification()
    {
        var ident = new IdentData();
        var pep = new Peptide { Id = "pep2", PeptideSequence = "PEPTIDER" };
        pep.SubstitutionModifications.Add(new SubstitutionModification
        {
            OriginalResidue = 'A', ReplacementResidue = 'V',
            Location = 3, AvgMassDelta = 28.123, MonoisotopicMassDelta = 28.0313,
        });
        ident.SequenceCollection.Peptides.Add(pep);
        return ident;
    }

    private static IdentData BuildPeptide()
    {
        var ident = new IdentData();
        var pep = new Peptide { Id = "pepF", Name = "named", PeptideSequence = "PEPTIDER" };
        pep.Set(CVID.MS_taxonomy__NCBI_TaxID, "9606");
        ident.SequenceCollection.Peptides.Add(pep);
        return ident;
    }

    private static IdentData BuildSequenceCollection()
    {
        var ident = new IdentData();
        ident.SequenceCollection.DBSequences.Add(new DBSequence { Id = "dbs", Accession = "P1" });
        var pep = new Peptide { Id = "p", PeptideSequence = "PEPTIDE" };
        ident.SequenceCollection.Peptides.Add(pep);
        var dbs = ident.SequenceCollection.DBSequences[0];
        ident.SequenceCollection.PeptideEvidence.Add(new PeptideEvidence
        {
            Id = "pe", PeptidePtr = pep, DBSequencePtr = dbs, Start = 1, End = 7, Pre = '-', Post = 'A', IsDecoy = false,
        });
        return ident;
    }

    private static IdentData BuildSpectrumIdentification()
    {
        var ident = BuildSpectrumIdentificationProtocol();
        // Hook up an SpectrumIdentification entry referring to the protocol + a fake list.
        var sw = ident.AnalysisSoftwareList[0];
        var sip = ident.AnalysisProtocolCollection.SpectrumIdentificationProtocol[0];
        var sd = new SpectraData { Id = "sd1", Location = "/tmp/sample.mzML" };
        ident.DataCollection.Inputs.SpectraData.Add(sd);
        var db = new SearchDatabase { Id = "db1", Location = "/tmp/db.fasta" };
        ident.DataCollection.Inputs.SearchDatabase.Add(db);
        var sil = new SpectrumIdentificationList { Id = "SIL_1" };
        ident.DataCollection.AnalysisData.SpectrumIdentificationList.Add(sil);
        var si = new SpectrumIdentification
        {
            Id = "SI_1",
            ActivityDate = "2024-01-01T00:00:00",
            SpectrumIdentificationProtocolPtr = sip,
            SpectrumIdentificationListPtr = sil,
        };
        si.InputSpectra.Add(sd);
        si.SearchDatabase.Add(db);
        ident.AnalysisCollection.SpectrumIdentification.Add(si);
        _ = sw;
        return ident;
    }

    private static IdentData BuildProteinDetection()
    {
        var ident = new IdentData();
        var sw = new AnalysisSoftware { Id = "SW_PD" };
        ident.AnalysisSoftwareList.Add(sw);
        var pdp = new ProteinDetectionProtocol { Id = "PDP_1", AnalysisSoftwarePtr = sw };
        ident.AnalysisProtocolCollection.ProteinDetectionProtocol.Add(pdp);
        var pdl = new ProteinDetectionList { Id = "PDL_1" };
        ident.DataCollection.AnalysisData.ProteinDetectionListPtr = pdl;
        var sil = new SpectrumIdentificationList { Id = "SIL_PD" };
        ident.DataCollection.AnalysisData.SpectrumIdentificationList.Add(sil);
        var pd = ident.AnalysisCollection.ProteinDetection;
        pd.Id = "PD_1";
        pd.ActivityDate = "2024-02-01T00:00:00";
        pd.ProteinDetectionProtocolPtr = pdp;
        pd.ProteinDetectionListPtr = pdl;
        pd.InputSpectrumIdentifications.Add(sil);
        return ident;
    }

    private static IdentData BuildAnalysisCollection() => BuildSpectrumIdentification();

    private static IdentData BuildSearchModification()
    {
        var ident = BuildSpectrumIdentificationProtocol();
        var sip = ident.AnalysisProtocolCollection.SpectrumIdentificationProtocol[0];
        var sm = new SearchModification { FixedMod = false, MassDelta = 79.9663 };
        sm.Residues.Add('S');
        sm.Residues.Add('T');
        sm.SpecificityRules = new CVParam(CVID.MS_modification_specificity_protein_N_term);
        sm.Set(CVID.UNIMOD_Phospho, "");
        sip.ModificationParams.Add(sm);
        return ident;
    }

    private static IdentData BuildEnzyme()
    {
        var ident = BuildSpectrumIdentificationProtocol();
        var sip = ident.AnalysisProtocolCollection.SpectrumIdentificationProtocol[0];
        sip.Enzymes.EnzymeList.Clear();
        var e = new Enzyme
        {
            Id = "Trypsin", Name = "Trypsin",
            NTermGain = "H", CTermGain = "OH",
            TerminalSpecificity = DigestionSpecificity.SemiSpecific,
            MissedCleavages = 1, MinDistance = 1,
            SiteRegexp = "(?<=[KR])(?!P)",
        };
        e.EnzymeName.Set(CVID.MS_Trypsin);
        sip.Enzymes.EnzymeList.Add(e);
        return ident;
    }

    private static IdentData BuildEnzymes()
    {
        var ident = BuildEnzyme();
        var sip = ident.AnalysisProtocolCollection.SpectrumIdentificationProtocol[0];
        sip.Enzymes.Independent = true;
        return ident;
    }

    private static IdentData BuildResidue() => BuildMassTable(); // covered together
    private static IdentData BuildAmbiguousResidue() => BuildMassTable();

    private static IdentData BuildMassTable()
    {
        var ident = BuildSpectrumIdentificationProtocol();
        var sip = ident.AnalysisProtocolCollection.SpectrumIdentificationProtocol[0];
        var mt = new MassTable { Id = "MT_1" };
        mt.MsLevel.Add(2);
        mt.Residues.Add(new Residue { Code = 'G', Mass = 57.02146 });
        mt.Residues.Add(new Residue { Code = 'A', Mass = 71.03711 });
        var ar = new AmbiguousResidue { Code = 'X' };
        ar.Set(CVID.MS_alternate_single_letter_codes, "any");
        mt.AmbiguousResidue.Add(ar);
        sip.MassTable.Add(mt);
        return ident;
    }

    private static IdentData BuildFilter()
    {
        var ident = BuildSpectrumIdentificationProtocol();
        var sip = ident.AnalysisProtocolCollection.SpectrumIdentificationProtocol[0];
        var f = new Filter();
        f.FilterType.Set(CVID.MS_DB_filter_taxonomy);
        f.Include.Set(CVID.MS_DB_filter_on_accession_numbers, "9606");
        sip.DatabaseFilters.Add(f);
        return ident;
    }

    private static IdentData BuildSpectrumIdentificationProtocol()
    {
        var ident = new IdentData();
        var sw = new AnalysisSoftware { Id = "AS_proto", Name = "Engine", Version = "1.0" };
        sw.SoftwareName.CVParams.Add(new CVParam(CVID.MS_MyriMatch));
        ident.AnalysisSoftwareList.Add(sw);
        var sip = new SpectrumIdentificationProtocol { Id = "SIP_1", AnalysisSoftwarePtr = sw };
        sip.SearchType = new CVParam(CVID.MS_ms_ms_search);
        sip.AdditionalSearchParams.Set(CVID.MS_param__a_ion);
        sip.Threshold.Set(CVID.MS_no_threshold);
        sip.FragmentTolerance.Set(CVID.MS_search_tolerance_plus_value, "0.5", CVID.UO_dalton);
        sip.ParentTolerance.Set(CVID.MS_search_tolerance_plus_value, "10", CVID.UO_parts_per_million);
        sip.Enzymes.Independent = false;
        var e = new Enzyme { Id = "Tryp", MissedCleavages = 1 };
        e.EnzymeName.Set(CVID.MS_Trypsin);
        sip.Enzymes.EnzymeList.Add(e);
        ident.AnalysisProtocolCollection.SpectrumIdentificationProtocol.Add(sip);
        return ident;
    }

    private static IdentData BuildProteinDetectionProtocol()
    {
        var ident = new IdentData();
        var sw = new AnalysisSoftware { Id = "AS_pdp" };
        ident.AnalysisSoftwareList.Add(sw);
        var pdp = new ProteinDetectionProtocol
        {
            Id = "PDP_1", Name = "default", AnalysisSoftwarePtr = sw,
        };
        pdp.AnalysisParams.Set(CVID.MS_no_threshold);
        pdp.Threshold.Set(CVID.MS_no_threshold);
        ident.AnalysisProtocolCollection.ProteinDetectionProtocol.Add(pdp);
        return ident;
    }

    private static IdentData BuildAnalysisProtocolCollection() => BuildSpectrumIdentificationProtocol();

    private static IdentData BuildSpectraData()
    {
        var ident = new IdentData();
        ident.DataCollection.Inputs.SpectraData.Add(new SpectraData
        {
            Id = "SD_1", Name = "raw", Location = "/tmp/sample.mzML",
            FileFormat = new CVParam(CVID.MS_mzML_format),
            SpectrumIDFormat = new CVParam(CVID.MS_Thermo_nativeID_format),
        });
        return ident;
    }

    private static IdentData BuildSearchDatabase()
    {
        var ident = new IdentData();
        var db = new SearchDatabase
        {
            Id = "DB_1", Location = "/tmp/db.fasta", Version = "v1", ReleaseDate = "2024-01-01",
            NumDatabaseSequences = 100, NumResidues = 50000,
            FileFormat = new CVParam(CVID.MS_FASTA_format),
        };
        db.DatabaseName.UserParams.Add(new UserParam("UniProt", "1.0"));
        ident.DataCollection.Inputs.SearchDatabase.Add(db);
        return ident;
    }

    private static IdentData BuildSourceFile()
    {
        var ident = new IdentData();
        ident.DataCollection.Inputs.SourceFile.Add(new SourceFile
        {
            Id = "SF_1", Name = "input", Location = "/tmp/input.mzML",
            FileFormat = new CVParam(CVID.MS_mzML_format),
        });
        return ident;
    }

    private static IdentData BuildInputs() => BuildSpectraData();

    private static IdentData BuildMeasure()
    {
        var ident = new IdentData();
        var sil = new SpectrumIdentificationList { Id = "SIL_M" };
        var measure = new Measure { Id = "m_mz", Name = "product ion m/z" };
        measure.Set(CVID.MS_product_ion_m_z, "", CVID.MS_m_z);
        sil.FragmentationTable.Add(measure);
        ident.DataCollection.AnalysisData.SpectrumIdentificationList.Add(sil);
        return ident;
    }

    private static IdentData BuildFragmentArray() => BuildIonType(); // covered together
    private static IdentData BuildIonType()
    {
        var ident = new IdentData();
        var sil = new SpectrumIdentificationList { Id = "SIL_I" };
        var measure = new Measure { Id = "m_mz" };
        measure.Set(CVID.MS_product_ion_m_z);
        sil.FragmentationTable.Add(measure);
        var pep = new Peptide { Id = "pep", PeptideSequence = "PEPTIDE" };
        ident.SequenceCollection.Peptides.Add(pep);
        var sir = new SpectrumIdentificationResult { Id = "SIR_1", SpectrumID = "scan=1" };
        var sii = new SpectrumIdentificationItem
        {
            Id = "SII_1", PassThreshold = true, ChargeState = 2, Rank = 1,
            ExperimentalMassToCharge = 400.0, CalculatedMassToCharge = 400.0,
            PeptidePtr = pep,
        };
        var ion = new IonType
        {
            Type = new CVParam(CVID.MS_frag__b_ion),
            Charge = 1,
        };
        ion.Index.Add(3); ion.Index.Add(7); ion.Index.Add(8);
        var fa = new FragmentArray { MeasurePtr = measure };
        fa.Values.Add(123.45); fa.Values.Add(456.78); fa.Values.Add(789.01);
        ion.FragmentArray.Add(fa);
        sii.Fragmentation.Add(ion);
        sir.SpectrumIdentificationItem.Add(sii);
        sil.SpectrumIdentificationResult.Add(sir);
        ident.DataCollection.AnalysisData.SpectrumIdentificationList.Add(sil);
        return ident;
    }

    private static IdentData BuildPeptideEvidence() => BuildSequenceCollection();

    private static IdentData BuildSpectrumIdentificationItem()
    {
        var ident = BuildSequenceCollection();
        var pep = ident.SequenceCollection.Peptides[0];
        var pe = ident.SequenceCollection.PeptideEvidence[0];
        var sil = new SpectrumIdentificationList { Id = "SIL_I2" };
        var sir = new SpectrumIdentificationResult { Id = "SIR_I2", SpectrumID = "scan=2" };
        var sii = new SpectrumIdentificationItem
        {
            Id = "SII_I2", ChargeState = 2, ExperimentalMassToCharge = 400.5, CalculatedMassToCharge = 400.5,
            CalculatedPI = 5.5, Rank = 1, PassThreshold = true, PeptidePtr = pep,
        };
        sii.Set(CVID.MS_SEQUEST_xcorr, "3.5");
        sii.PeptideEvidencePtr.Add(pe);
        sir.SpectrumIdentificationItem.Add(sii);
        sil.SpectrumIdentificationResult.Add(sir);
        ident.DataCollection.AnalysisData.SpectrumIdentificationList.Add(sil);
        return ident;
    }

    private static IdentData BuildSpectrumIdentificationResult()
    {
        var ident = BuildSpectrumIdentificationItem();
        var sd = new SpectraData { Id = "SD_R", Location = "/tmp/run.mzML" };
        ident.DataCollection.Inputs.SpectraData.Add(sd);
        var sil = ident.DataCollection.AnalysisData.SpectrumIdentificationList[0];
        sil.SpectrumIdentificationResult[0].SpectraDataPtr = sd;
        sil.SpectrumIdentificationResult[0].Set(CVID.MS_scan_start_time, "10.0", CVID.UO_minute);
        return ident;
    }

    private static IdentData BuildProteinDetectionHypothesis()
    {
        var ident = BuildSequenceCollection();
        var dbs = ident.SequenceCollection.DBSequences[0];
        var pe = ident.SequenceCollection.PeptideEvidence[0];
        var pdl = new ProteinDetectionList { Id = "PDL_PDH" };
        var pag = new ProteinAmbiguityGroup { Id = "PAG_1" };
        var pdh = new ProteinDetectionHypothesis
        {
            Id = "PDH_1", DBSequencePtr = dbs, PassThreshold = true,
        };
        pdh.PeptideHypothesis.Add(new PeptideHypothesis { PeptideEvidencePtr = pe });
        pag.ProteinDetectionHypothesis.Add(pdh);
        pdl.ProteinAmbiguityGroup.Add(pag);
        ident.DataCollection.AnalysisData.ProteinDetectionListPtr = pdl;
        return ident;
    }

    private static IdentData BuildProteinAmbiguityGroup() => BuildProteinDetectionHypothesis();

    private static IdentData BuildSpectrumIdentificationList() => BuildSpectrumIdentificationResult();

    private static IdentData BuildProteinDetectionList() => BuildProteinDetectionHypothesis();

    private static IdentData BuildAnalysisData() => BuildSpectrumIdentificationResult();

    private static IdentData BuildDataCollection()
    {
        var ident = BuildSpectrumIdentificationResult();
        var sf = new SourceFile
        {
            Id = "SF_DC", Location = "/tmp/source.mzML",
            FileFormat = new CVParam(CVID.MS_mzML_format),
        };
        ident.DataCollection.Inputs.SourceFile.Add(sf);
        return ident;
    }

    private static IdentData BuildBibliographicReferenceList()
    {
        var ident = BuildBibliographicReference();
        ident.BibliographicReferences.Add(new BibliographicReference
        {
            Id = "br2", Authors = "Doe J", Title = "second", Year = 2024,
        });
        return ident;
    }

    private static IdentData BuildFullIdentData()
    {
        // Combine several pieces into one tree to exercise the root.
        var ident = BuildSpectrumIdentificationResult();
        ident.Id = "full";
        ident.Name = "full ident data";
        ident.CreationDate = "2024-01-01T00:00:00";
        ident.Cvs.Add(new CV { Id = "PSI-MS", FullName = "PSI-MS", Uri = "http://...", Version = "4.0" });
        return ident;
    }
}
