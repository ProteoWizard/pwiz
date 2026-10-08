using System.Globalization;
using System.Text;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData;

/// <summary>
/// Accumulates field-path diff messages produced by <see cref="IdentDataDiff"/>. Empty when the
/// two values are equal across every recursively compared field.
/// </summary>
public sealed class DiffReport
{
    private readonly List<string> _lines = new();

    /// <summary>True when no differences have been recorded.</summary>
    public bool IsEmpty => _lines.Count == 0;

    /// <summary>The diff messages collected so far, in insertion order.</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>Adds a single diff message (typically "path: a vs b").</summary>
    public void Add(string line) => _lines.Add(line);

    /// <inheritdoc/>
    public override string ToString() => string.Join(System.Environment.NewLine, _lines);
}

/// <summary>
/// Deep-equality comparison for the <see cref="IdentData"/> tree. Mirrors cpp's
/// <c>pwiz::identdata::Diff</c> conceptually but emits a flat list of "field path: a vs b"
/// strings instead of returning the structural diff as another tree (good enough for tests
/// and clearer in failure messages).
/// </summary>
/// <remarks>
/// References to other schema objects (e.g. <c>SpectrumIdentificationItem.PeptidePtr</c>) compare
/// by <c>Id</c> only — references aren't physically preserved across round-trip, they're
/// resolved by id. ParamContainers compare CVParams and UserParams as ordered lists; if the
/// writer/reader preserves insertion order (which it does), index-by-index comparison is
/// equivalent to set comparison.
/// </remarks>
public static class IdentDataDiff
{
    /// <summary>Recursively diffs <paramref name="a"/> against <paramref name="b"/> and returns
    /// a report of any field-level differences.</summary>
    public static DiffReport Diff(IdentData a, IdentData b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var r = new DiffReport();
        DiffIdentData(a, b, r, "IdentData");
        return r;
    }

    // ----- top-level recursion -----

    private static void DiffIdentData(IdentData a, IdentData b, DiffReport r, string path)
    {
        DiffIdentifiable(a, b, r, path);
        DiffString(a.CreationDate, b.CreationDate, r, $"{path}.CreationDate");
        DiffList(a.Cvs, b.Cvs, r, $"{path}.Cvs", DiffCv);
        DiffList(a.AnalysisSoftwareList, b.AnalysisSoftwareList, r, $"{path}.AnalysisSoftwareList", DiffAnalysisSoftware);
        DiffProvider(a.Provider, b.Provider, r, $"{path}.Provider");
        DiffList(a.AuditCollection, b.AuditCollection, r, $"{path}.AuditCollection", DiffContact);
        DiffList(a.AnalysisSampleCollection.Samples, b.AnalysisSampleCollection.Samples, r, $"{path}.AnalysisSampleCollection.Samples", DiffSample);
        DiffSequenceCollection(a.SequenceCollection, b.SequenceCollection, r, $"{path}.SequenceCollection");
        DiffAnalysisCollection(a.AnalysisCollection, b.AnalysisCollection, r, $"{path}.AnalysisCollection");
        DiffAnalysisProtocolCollection(a.AnalysisProtocolCollection, b.AnalysisProtocolCollection, r, $"{path}.AnalysisProtocolCollection");
        DiffDataCollection(a.DataCollection, b.DataCollection, r, $"{path}.DataCollection");
        DiffList(a.BibliographicReferences, b.BibliographicReferences, r, $"{path}.BibliographicReferences", DiffBibliographicReference);
    }

    private static void DiffIdentifiable(Identifiable a, Identifiable b, DiffReport r, string path)
    {
        DiffString(a.Id, b.Id, r, $"{path}.Id");
        DiffString(a.Name, b.Name, r, $"{path}.Name");
    }

    private static void DiffIdentifiableParamContainer(IdentifiableParamContainer a, IdentifiableParamContainer b, DiffReport r, string path)
    {
        DiffString(a.Id, b.Id, r, $"{path}.Id");
        DiffString(a.Name, b.Name, r, $"{path}.Name");
        DiffParamContainer(a, b, r, path);
    }

    // ----- per-type diffs -----

    private static void DiffCv(CV a, CV b, DiffReport r, string path)
    {
        DiffString(a.Id, b.Id, r, $"{path}.Id");
        DiffString(a.FullName, b.FullName, r, $"{path}.FullName");
        DiffString(a.Uri, b.Uri, r, $"{path}.Uri");
        DiffString(a.Version, b.Version, r, $"{path}.Version");
    }

    private static void DiffAnalysisSoftware(AnalysisSoftware a, AnalysisSoftware b, DiffReport r, string path)
    {
        DiffIdentifiable(a, b, r, path);
        DiffString(a.Version, b.Version, r, $"{path}.Version");
        DiffString(a.Uri, b.Uri, r, $"{path}.Uri");
        DiffString(a.Customizations, b.Customizations, r, $"{path}.Customizations");
        DiffParamContainer(a.SoftwareName, b.SoftwareName, r, $"{path}.SoftwareName");
        DiffNullableObject(a.ContactRolePtr, b.ContactRolePtr, r, $"{path}.ContactRolePtr", DiffContactRole);
    }

    private static void DiffProvider(Provider a, Provider b, DiffReport r, string path)
    {
        DiffIdentifiable(a, b, r, path);
        DiffNullableObject(a.ContactRolePtr, b.ContactRolePtr, r, $"{path}.ContactRolePtr", DiffContactRole);
        DiffRefById(a.AnalysisSoftwarePtr, b.AnalysisSoftwarePtr, r, $"{path}.AnalysisSoftwarePtr");
    }

    private static void DiffContactRole(ContactRole a, ContactRole b, DiffReport r, string path)
    {
        DiffCvParam(a.Role, b.Role, r, $"{path}.Role");
        DiffRefById(a.ContactPtr, b.ContactPtr, r, $"{path}.ContactPtr");
    }

    private static void DiffContact(Contact a, Contact b, DiffReport r, string path)
    {
        if (a.GetType() != b.GetType())
        {
            r.Add($"{path}: contact type {a.GetType().Name} != {b.GetType().Name}");
            return;
        }
        switch (a)
        {
            case Person pa when b is Person pb: DiffPerson(pa, pb, r, path); break;
            case Organization oa when b is Organization ob: DiffOrganization(oa, ob, r, path); break;
            default: DiffIdentifiableParamContainer(a, b, r, path); break;
        }
    }

    private static void DiffPerson(Person a, Person b, DiffReport r, string path)
    {
        DiffIdentifiableParamContainer(a, b, r, path);
        DiffString(a.LastName, b.LastName, r, $"{path}.LastName");
        DiffString(a.FirstName, b.FirstName, r, $"{path}.FirstName");
        DiffString(a.MidInitials, b.MidInitials, r, $"{path}.MidInitials");
        DiffList(a.Affiliations, b.Affiliations, r, $"{path}.Affiliations",
            (oa, ob, rep, p) => DiffRefById(oa, ob, rep, p));
    }

    private static void DiffOrganization(Organization a, Organization b, DiffReport r, string path)
    {
        DiffIdentifiableParamContainer(a, b, r, path);
        DiffRefById(a.Parent, b.Parent, r, $"{path}.Parent");
    }

    private static void DiffSample(Sample a, Sample b, DiffReport r, string path)
    {
        DiffIdentifiableParamContainer(a, b, r, path);
        DiffList(a.ContactRole, b.ContactRole, r, $"{path}.ContactRole", DiffContactRole);
        DiffList(a.SubSamples, b.SubSamples, r, $"{path}.SubSamples",
            (sa, sb, rep, p) => DiffRefById(sa, sb, rep, p));
    }

    private static void DiffBibliographicReference(BibliographicReference a, BibliographicReference b, DiffReport r, string path)
    {
        DiffIdentifiable(a, b, r, path);
        DiffString(a.Authors, b.Authors, r, $"{path}.Authors");
        DiffString(a.Publication, b.Publication, r, $"{path}.Publication");
        DiffString(a.Publisher, b.Publisher, r, $"{path}.Publisher");
        DiffString(a.Editor, b.Editor, r, $"{path}.Editor");
        DiffInt(a.Year, b.Year, r, $"{path}.Year");
        DiffString(a.Volume, b.Volume, r, $"{path}.Volume");
        DiffString(a.Issue, b.Issue, r, $"{path}.Issue");
        DiffString(a.Pages, b.Pages, r, $"{path}.Pages");
        DiffString(a.Title, b.Title, r, $"{path}.Title");
    }

    private static void DiffSequenceCollection(SequenceCollection a, SequenceCollection b, DiffReport r, string path)
    {
        DiffList(a.DBSequences, b.DBSequences, r, $"{path}.DBSequences", DiffDBSequence);
        DiffList(a.Peptides, b.Peptides, r, $"{path}.Peptides", DiffPeptide);
        DiffList(a.PeptideEvidence, b.PeptideEvidence, r, $"{path}.PeptideEvidence", DiffPeptideEvidence);
    }

    private static void DiffDBSequence(DBSequence a, DBSequence b, DiffReport r, string path)
    {
        DiffIdentifiableParamContainer(a, b, r, path);
        DiffInt(a.Length, b.Length, r, $"{path}.Length");
        DiffString(a.Accession, b.Accession, r, $"{path}.Accession");
        DiffRefById(a.SearchDatabasePtr, b.SearchDatabasePtr, r, $"{path}.SearchDatabasePtr");
        DiffString(a.Seq, b.Seq, r, $"{path}.Seq");
    }

    private static void DiffPeptide(Peptide a, Peptide b, DiffReport r, string path)
    {
        DiffIdentifiableParamContainer(a, b, r, path);
        DiffString(a.PeptideSequence, b.PeptideSequence, r, $"{path}.PeptideSequence");
        DiffList(a.Modifications, b.Modifications, r, $"{path}.Modifications", DiffModification);
        DiffList(a.SubstitutionModifications, b.SubstitutionModifications, r, $"{path}.SubstitutionModifications", DiffSubstitutionModification);
    }

    private static void DiffModification(Modification a, Modification b, DiffReport r, string path)
    {
        DiffParamContainer(a, b, r, path);
        DiffInt(a.Location, b.Location, r, $"{path}.Location");
        DiffCharList(a.Residues, b.Residues, r, $"{path}.Residues");
        DiffDouble(a.AvgMassDelta, b.AvgMassDelta, r, $"{path}.AvgMassDelta");
        DiffDouble(a.MonoisotopicMassDelta, b.MonoisotopicMassDelta, r, $"{path}.MonoisotopicMassDelta");
    }

    private static void DiffSubstitutionModification(SubstitutionModification a, SubstitutionModification b, DiffReport r, string path)
    {
        DiffChar(a.OriginalResidue, b.OriginalResidue, r, $"{path}.OriginalResidue");
        DiffChar(a.ReplacementResidue, b.ReplacementResidue, r, $"{path}.ReplacementResidue");
        DiffInt(a.Location, b.Location, r, $"{path}.Location");
        DiffDouble(a.AvgMassDelta, b.AvgMassDelta, r, $"{path}.AvgMassDelta");
        DiffDouble(a.MonoisotopicMassDelta, b.MonoisotopicMassDelta, r, $"{path}.MonoisotopicMassDelta");
    }

    private static void DiffPeptideEvidence(PeptideEvidence a, PeptideEvidence b, DiffReport r, string path)
    {
        DiffIdentifiableParamContainer(a, b, r, path);
        DiffRefById(a.PeptidePtr, b.PeptidePtr, r, $"{path}.PeptidePtr");
        DiffRefById(a.DBSequencePtr, b.DBSequencePtr, r, $"{path}.DBSequencePtr");
        DiffInt(a.Start, b.Start, r, $"{path}.Start");
        DiffInt(a.End, b.End, r, $"{path}.End");
        DiffChar(a.Pre, b.Pre, r, $"{path}.Pre");
        DiffChar(a.Post, b.Post, r, $"{path}.Post");
        DiffInt(a.Frame, b.Frame, r, $"{path}.Frame");
        DiffBool(a.IsDecoy, b.IsDecoy, r, $"{path}.IsDecoy");
    }

    private static void DiffAnalysisCollection(AnalysisCollection a, AnalysisCollection b, DiffReport r, string path)
    {
        DiffList(a.SpectrumIdentification, b.SpectrumIdentification, r, $"{path}.SpectrumIdentification", DiffSpectrumIdentification);
        DiffProteinDetection(a.ProteinDetection, b.ProteinDetection, r, $"{path}.ProteinDetection");
    }

    private static void DiffSpectrumIdentification(SpectrumIdentification a, SpectrumIdentification b, DiffReport r, string path)
    {
        DiffIdentifiable(a, b, r, path);
        DiffRefById(a.SpectrumIdentificationProtocolPtr, b.SpectrumIdentificationProtocolPtr, r, $"{path}.SpectrumIdentificationProtocolPtr");
        DiffRefById(a.SpectrumIdentificationListPtr, b.SpectrumIdentificationListPtr, r, $"{path}.SpectrumIdentificationListPtr");
        DiffString(a.ActivityDate, b.ActivityDate, r, $"{path}.ActivityDate");
        DiffList(a.InputSpectra, b.InputSpectra, r, $"{path}.InputSpectra",
            (sa, sb, rep, p) => DiffRefById(sa, sb, rep, p));
        DiffList(a.SearchDatabase, b.SearchDatabase, r, $"{path}.SearchDatabase",
            (sa, sb, rep, p) => DiffRefById(sa, sb, rep, p));
    }

    private static void DiffProteinDetection(ProteinDetection a, ProteinDetection b, DiffReport r, string path)
    {
        DiffIdentifiable(a, b, r, path);
        DiffRefById(a.ProteinDetectionProtocolPtr, b.ProteinDetectionProtocolPtr, r, $"{path}.ProteinDetectionProtocolPtr");
        DiffRefById(a.ProteinDetectionListPtr, b.ProteinDetectionListPtr, r, $"{path}.ProteinDetectionListPtr");
        DiffString(a.ActivityDate, b.ActivityDate, r, $"{path}.ActivityDate");
        DiffList(a.InputSpectrumIdentifications, b.InputSpectrumIdentifications, r, $"{path}.InputSpectrumIdentifications",
            (sa, sb, rep, p) => DiffRefById(sa, sb, rep, p));
    }

    private static void DiffAnalysisProtocolCollection(AnalysisProtocolCollection a, AnalysisProtocolCollection b, DiffReport r, string path)
    {
        DiffList(a.SpectrumIdentificationProtocol, b.SpectrumIdentificationProtocol, r, $"{path}.SpectrumIdentificationProtocol", DiffSpectrumIdentificationProtocol);
        DiffList(a.ProteinDetectionProtocol, b.ProteinDetectionProtocol, r, $"{path}.ProteinDetectionProtocol", DiffProteinDetectionProtocol);
    }

    private static void DiffSpectrumIdentificationProtocol(SpectrumIdentificationProtocol a, SpectrumIdentificationProtocol b, DiffReport r, string path)
    {
        DiffIdentifiable(a, b, r, path);
        DiffRefById(a.AnalysisSoftwarePtr, b.AnalysisSoftwarePtr, r, $"{path}.AnalysisSoftwarePtr");
        DiffCvParam(a.SearchType, b.SearchType, r, $"{path}.SearchType");
        DiffParamContainer(a.AdditionalSearchParams, b.AdditionalSearchParams, r, $"{path}.AdditionalSearchParams");
        DiffList(a.ModificationParams, b.ModificationParams, r, $"{path}.ModificationParams", DiffSearchModification);
        DiffEnzymes(a.Enzymes, b.Enzymes, r, $"{path}.Enzymes");
        DiffList(a.MassTable, b.MassTable, r, $"{path}.MassTable", DiffMassTable);
        DiffParamContainer(a.FragmentTolerance, b.FragmentTolerance, r, $"{path}.FragmentTolerance");
        DiffParamContainer(a.ParentTolerance, b.ParentTolerance, r, $"{path}.ParentTolerance");
        DiffParamContainer(a.Threshold, b.Threshold, r, $"{path}.Threshold");
        DiffList(a.DatabaseFilters, b.DatabaseFilters, r, $"{path}.DatabaseFilters", DiffFilter);
        DiffNullableObject(a.DatabaseTranslation, b.DatabaseTranslation, r, $"{path}.DatabaseTranslation", DiffDatabaseTranslation);
    }

    private static void DiffProteinDetectionProtocol(ProteinDetectionProtocol a, ProteinDetectionProtocol b, DiffReport r, string path)
    {
        DiffIdentifiable(a, b, r, path);
        DiffRefById(a.AnalysisSoftwarePtr, b.AnalysisSoftwarePtr, r, $"{path}.AnalysisSoftwarePtr");
        DiffParamContainer(a.AnalysisParams, b.AnalysisParams, r, $"{path}.AnalysisParams");
        DiffParamContainer(a.Threshold, b.Threshold, r, $"{path}.Threshold");
    }

    private static void DiffSearchModification(SearchModification a, SearchModification b, DiffReport r, string path)
    {
        DiffParamContainer(a, b, r, path);
        DiffBool(a.FixedMod, b.FixedMod, r, $"{path}.FixedMod");
        DiffDouble(a.MassDelta, b.MassDelta, r, $"{path}.MassDelta");
        DiffCharList(a.Residues, b.Residues, r, $"{path}.Residues");
        DiffCvParam(a.SpecificityRules, b.SpecificityRules, r, $"{path}.SpecificityRules");
    }

    private static void DiffEnzymes(Enzymes a, Enzymes b, DiffReport r, string path)
    {
        DiffNullableBool(a.Independent, b.Independent, r, $"{path}.Independent");
        DiffList(a.EnzymeList, b.EnzymeList, r, $"{path}.EnzymeList", DiffEnzyme);
    }

    private static void DiffEnzyme(Enzyme a, Enzyme b, DiffReport r, string path)
    {
        DiffIdentifiable(a, b, r, path);
        DiffString(a.NTermGain, b.NTermGain, r, $"{path}.NTermGain");
        DiffString(a.CTermGain, b.CTermGain, r, $"{path}.CTermGain");
        if (a.TerminalSpecificity != b.TerminalSpecificity)
            r.Add($"{path}.TerminalSpecificity: {a.TerminalSpecificity} != {b.TerminalSpecificity}");
        DiffInt(a.MissedCleavages, b.MissedCleavages, r, $"{path}.MissedCleavages");
        DiffInt(a.MinDistance, b.MinDistance, r, $"{path}.MinDistance");
        DiffString(a.SiteRegexp, b.SiteRegexp, r, $"{path}.SiteRegexp");
        DiffParamContainer(a.EnzymeName, b.EnzymeName, r, $"{path}.EnzymeName");
    }

    private static void DiffMassTable(MassTable a, MassTable b, DiffReport r, string path)
    {
        DiffString(a.Id, b.Id, r, $"{path}.Id");
        DiffIntList(a.MsLevel, b.MsLevel, r, $"{path}.MsLevel");
        DiffList(a.Residues, b.Residues, r, $"{path}.Residues", DiffResidue);
        DiffList(a.AmbiguousResidue, b.AmbiguousResidue, r, $"{path}.AmbiguousResidue", DiffAmbiguousResidue);
    }

    private static void DiffResidue(Residue a, Residue b, DiffReport r, string path)
    {
        DiffChar(a.Code, b.Code, r, $"{path}.Code");
        DiffDouble(a.Mass, b.Mass, r, $"{path}.Mass");
    }

    private static void DiffAmbiguousResidue(AmbiguousResidue a, AmbiguousResidue b, DiffReport r, string path)
    {
        DiffChar(a.Code, b.Code, r, $"{path}.Code");
        DiffParamContainer(a, b, r, path);
    }

    private static void DiffFilter(Filter a, Filter b, DiffReport r, string path)
    {
        DiffParamContainer(a.FilterType, b.FilterType, r, $"{path}.FilterType");
        DiffParamContainer(a.Include, b.Include, r, $"{path}.Include");
        DiffParamContainer(a.Exclude, b.Exclude, r, $"{path}.Exclude");
    }

    private static void DiffDatabaseTranslation(DatabaseTranslation a, DatabaseTranslation b, DiffReport r, string path)
    {
        DiffIntList(a.Frames, b.Frames, r, $"{path}.Frames");
        DiffList(a.TranslationTables, b.TranslationTables, r, $"{path}.TranslationTables",
            (ta, tb, rep, p) => { DiffIdentifiableParamContainer(ta, tb, rep, p); });
    }

    private static void DiffDataCollection(DataCollection a, DataCollection b, DiffReport r, string path)
    {
        DiffInputs(a.Inputs, b.Inputs, r, $"{path}.Inputs");
        DiffAnalysisData(a.AnalysisData, b.AnalysisData, r, $"{path}.AnalysisData");
    }

    private static void DiffInputs(Inputs a, Inputs b, DiffReport r, string path)
    {
        DiffList(a.SourceFile, b.SourceFile, r, $"{path}.SourceFile", DiffSourceFile);
        DiffList(a.SearchDatabase, b.SearchDatabase, r, $"{path}.SearchDatabase", DiffSearchDatabase);
        DiffList(a.SpectraData, b.SpectraData, r, $"{path}.SpectraData", DiffSpectraData);
    }

    private static void DiffSourceFile(SourceFile a, SourceFile b, DiffReport r, string path)
    {
        DiffIdentifiableParamContainer(a, b, r, path);
        DiffString(a.Location, b.Location, r, $"{path}.Location");
        DiffCvParam(a.FileFormat, b.FileFormat, r, $"{path}.FileFormat");
    }

    private static void DiffSearchDatabase(SearchDatabase a, SearchDatabase b, DiffReport r, string path)
    {
        DiffIdentifiableParamContainer(a, b, r, path);
        DiffString(a.Location, b.Location, r, $"{path}.Location");
        DiffString(a.Version, b.Version, r, $"{path}.Version");
        DiffString(a.ReleaseDate, b.ReleaseDate, r, $"{path}.ReleaseDate");
        if (a.NumDatabaseSequences != b.NumDatabaseSequences) r.Add($"{path}.NumDatabaseSequences: {a.NumDatabaseSequences} != {b.NumDatabaseSequences}");
        if (a.NumResidues != b.NumResidues) r.Add($"{path}.NumResidues: {a.NumResidues} != {b.NumResidues}");
        DiffCvParam(a.FileFormat, b.FileFormat, r, $"{path}.FileFormat");
        DiffParamContainer(a.DatabaseName, b.DatabaseName, r, $"{path}.DatabaseName");
    }

    private static void DiffSpectraData(SpectraData a, SpectraData b, DiffReport r, string path)
    {
        DiffIdentifiable(a, b, r, path);
        DiffString(a.Location, b.Location, r, $"{path}.Location");
        DiffCvParam(a.FileFormat, b.FileFormat, r, $"{path}.FileFormat");
        DiffCvParam(a.SpectrumIDFormat, b.SpectrumIDFormat, r, $"{path}.SpectrumIDFormat");
    }

    private static void DiffAnalysisData(AnalysisData a, AnalysisData b, DiffReport r, string path)
    {
        DiffList(a.SpectrumIdentificationList, b.SpectrumIdentificationList, r, $"{path}.SpectrumIdentificationList", DiffSpectrumIdentificationList);
        DiffNullableObject(a.ProteinDetectionListPtr, b.ProteinDetectionListPtr, r, $"{path}.ProteinDetectionListPtr", DiffProteinDetectionList);
    }

    private static void DiffSpectrumIdentificationList(SpectrumIdentificationList a, SpectrumIdentificationList b, DiffReport r, string path)
    {
        DiffIdentifiableParamContainer(a, b, r, path);
        if (a.NumSequencesSearched != b.NumSequencesSearched)
            r.Add($"{path}.NumSequencesSearched: {a.NumSequencesSearched} != {b.NumSequencesSearched}");
        DiffList(a.FragmentationTable, b.FragmentationTable, r, $"{path}.FragmentationTable",
            (ma, mb, rep, p) => DiffIdentifiableParamContainer(ma, mb, rep, p));
        DiffList(a.SpectrumIdentificationResult, b.SpectrumIdentificationResult, r, $"{path}.SpectrumIdentificationResult", DiffSpectrumIdentificationResult);
    }

    private static void DiffSpectrumIdentificationResult(SpectrumIdentificationResult a, SpectrumIdentificationResult b, DiffReport r, string path)
    {
        DiffIdentifiableParamContainer(a, b, r, path);
        DiffString(a.SpectrumID, b.SpectrumID, r, $"{path}.SpectrumID");
        DiffRefById(a.SpectraDataPtr, b.SpectraDataPtr, r, $"{path}.SpectraDataPtr");
        DiffList(a.SpectrumIdentificationItem, b.SpectrumIdentificationItem, r, $"{path}.SpectrumIdentificationItem", DiffSpectrumIdentificationItem);
    }

    private static void DiffSpectrumIdentificationItem(SpectrumIdentificationItem a, SpectrumIdentificationItem b, DiffReport r, string path)
    {
        DiffIdentifiableParamContainer(a, b, r, path);
        DiffInt(a.ChargeState, b.ChargeState, r, $"{path}.ChargeState");
        DiffDouble(a.ExperimentalMassToCharge, b.ExperimentalMassToCharge, r, $"{path}.ExperimentalMassToCharge");
        DiffDouble(a.CalculatedMassToCharge, b.CalculatedMassToCharge, r, $"{path}.CalculatedMassToCharge");
        DiffDouble(a.CalculatedPI, b.CalculatedPI, r, $"{path}.CalculatedPI");
        DiffRefById(a.PeptidePtr, b.PeptidePtr, r, $"{path}.PeptidePtr");
        DiffInt(a.Rank, b.Rank, r, $"{path}.Rank");
        DiffBool(a.PassThreshold, b.PassThreshold, r, $"{path}.PassThreshold");
        DiffList(a.PeptideEvidencePtr, b.PeptideEvidencePtr, r, $"{path}.PeptideEvidencePtr",
            (pa, pb, rep, p) => DiffRefById(pa, pb, rep, p));
        DiffList(a.Fragmentation, b.Fragmentation, r, $"{path}.Fragmentation", DiffIonType);
    }

    private static void DiffIonType(IonType a, IonType b, DiffReport r, string path)
    {
        DiffCvParam(a.Type, b.Type, r, $"{path}.Type");
        DiffIntList(a.Index, b.Index, r, $"{path}.Index");
        DiffInt(a.Charge, b.Charge, r, $"{path}.Charge");
        DiffList(a.FragmentArray, b.FragmentArray, r, $"{path}.FragmentArray", DiffFragmentArray);
    }

    private static void DiffFragmentArray(FragmentArray a, FragmentArray b, DiffReport r, string path)
    {
        DiffRefById(a.MeasurePtr, b.MeasurePtr, r, $"{path}.MeasurePtr");
        if (a.Values.Count != b.Values.Count) { r.Add($"{path}.Values.Count: {a.Values.Count} != {b.Values.Count}"); return; }
        for (int i = 0; i < a.Values.Count; i++)
            if (System.Math.Abs(a.Values[i] - b.Values[i]) > 1e-9)
                r.Add($"{path}.Values[{i}]: {a.Values[i]} != {b.Values[i]}");
    }

    private static void DiffProteinDetectionList(ProteinDetectionList a, ProteinDetectionList b, DiffReport r, string path)
    {
        DiffIdentifiableParamContainer(a, b, r, path);
        DiffList(a.ProteinAmbiguityGroup, b.ProteinAmbiguityGroup, r, $"{path}.ProteinAmbiguityGroup", DiffProteinAmbiguityGroup);
    }

    private static void DiffProteinAmbiguityGroup(ProteinAmbiguityGroup a, ProteinAmbiguityGroup b, DiffReport r, string path)
    {
        DiffIdentifiableParamContainer(a, b, r, path);
        DiffList(a.ProteinDetectionHypothesis, b.ProteinDetectionHypothesis, r, $"{path}.ProteinDetectionHypothesis", DiffProteinDetectionHypothesis);
    }

    private static void DiffProteinDetectionHypothesis(ProteinDetectionHypothesis a, ProteinDetectionHypothesis b, DiffReport r, string path)
    {
        DiffIdentifiableParamContainer(a, b, r, path);
        DiffRefById(a.DBSequencePtr, b.DBSequencePtr, r, $"{path}.DBSequencePtr");
        DiffBool(a.PassThreshold, b.PassThreshold, r, $"{path}.PassThreshold");
        DiffList(a.PeptideHypothesis, b.PeptideHypothesis, r, $"{path}.PeptideHypothesis",
            (ha, hb, rep, p) => DiffRefById(ha.PeptideEvidencePtr, hb.PeptideEvidencePtr, rep, $"{p}.PeptideEvidencePtr"));
    }

    // ----- low-level diff primitives -----

    private static void DiffParamContainer(ParamContainer a, ParamContainer b, DiffReport r, string path)
    {
        if (a.CVParams.Count != b.CVParams.Count)
            r.Add($"{path}.CVParams.Count: {a.CVParams.Count} != {b.CVParams.Count}");
        for (int i = 0; i < System.Math.Min(a.CVParams.Count, b.CVParams.Count); i++)
            DiffCvParam(a.CVParams[i], b.CVParams[i], r, $"{path}.CVParams[{i}]");

        if (a.UserParams.Count != b.UserParams.Count)
            r.Add($"{path}.UserParams.Count: {a.UserParams.Count} != {b.UserParams.Count}");
        for (int i = 0; i < System.Math.Min(a.UserParams.Count, b.UserParams.Count); i++)
            DiffUserParam(a.UserParams[i], b.UserParams[i], r, $"{path}.UserParams[{i}]");
    }

    private static void DiffCvParam(CVParam a, CVParam b, DiffReport r, string path)
    {
        if (a.Cvid != b.Cvid) r.Add($"{path}.Cvid: {a.Cvid} != {b.Cvid}");
        if (a.Value != b.Value) r.Add($"{path}.Value: \"{a.Value}\" != \"{b.Value}\"");
        if (a.Units != b.Units) r.Add($"{path}.Units: {a.Units} != {b.Units}");
    }

    private static void DiffUserParam(UserParam a, UserParam b, DiffReport r, string path)
    {
        if (a.Name != b.Name) r.Add($"{path}.Name: \"{a.Name}\" != \"{b.Name}\"");
        if (a.Value != b.Value) r.Add($"{path}.Value: \"{a.Value}\" != \"{b.Value}\"");
        if (a.Type != b.Type) r.Add($"{path}.Type: \"{a.Type}\" != \"{b.Type}\"");
        if (a.Units != b.Units) r.Add($"{path}.Units: {a.Units} != {b.Units}");
    }

    private static void DiffString(string a, string b, DiffReport r, string path)
    {
        if (a != b) r.Add($"{path}: \"{a}\" != \"{b}\"");
    }

    private static void DiffInt(int a, int b, DiffReport r, string path)
    {
        if (a != b) r.Add($"{path}: {a} != {b}");
    }

    private static void DiffDouble(double a, double b, DiffReport r, string path)
    {
        if (System.Math.Abs(a - b) > 1e-9)
            r.Add($"{path}: {a.ToString("R", CultureInfo.InvariantCulture)} != {b.ToString("R", CultureInfo.InvariantCulture)}");
    }

    private static void DiffBool(bool a, bool b, DiffReport r, string path)
    {
        if (a != b) r.Add($"{path}: {a} != {b}");
    }

    private static void DiffNullableBool(bool? a, bool? b, DiffReport r, string path)
    {
        if (a != b) r.Add($"{path}: {(a?.ToString() ?? "null")} != {(b?.ToString() ?? "null")}");
    }

    private static void DiffChar(char a, char b, DiffReport r, string path)
    {
        if (a != b) r.Add($"{path}: '{a}' != '{b}'");
    }

    private static void DiffCharList(List<char> a, List<char> b, DiffReport r, string path)
    {
        if (a.Count != b.Count)
        {
            r.Add($"{path}.Count: {a.Count} != {b.Count}");
            return;
        }
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) r.Add($"{path}[{i}]: '{a[i]}' != '{b[i]}'");
    }

    private static void DiffIntList(List<int> a, List<int> b, DiffReport r, string path)
    {
        if (a.Count != b.Count)
        {
            r.Add($"{path}.Count: {a.Count} != {b.Count}");
            return;
        }
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) r.Add($"{path}[{i}]: {a[i]}!= {b[i]}");
    }

    private delegate void DiffOf<T>(T a, T b, DiffReport r, string path);

    private static void DiffList<T>(IList<T> a, IList<T> b, DiffReport r, string path, DiffOf<T> diffOne)
    {
        if (a.Count != b.Count)
        {
            r.Add($"{path}.Count: {a.Count} != {b.Count}");
            return;
        }
        for (int i = 0; i < a.Count; i++)
            diffOne(a[i], b[i], r, $"{path}[{i}]");
    }

    private static void DiffNullableObject<T>(T? a, T? b, DiffReport r, string path, DiffOf<T> diffOne) where T : class
    {
        if (a is null && b is null) return;
        if (a is null) { r.Add($"{path}: null != non-null"); return; }
        if (b is null) { r.Add($"{path}: non-null != null"); return; }
        diffOne(a, b, r, path);
    }

    private static void DiffRefById(Identifiable? a, Identifiable? b, DiffReport r, string path)
    {
        var aId = a?.Id ?? string.Empty;
        var bId = b?.Id ?? string.Empty;
        if (aId != bId) r.Add($"{path}.Id-ref: \"{aId}\" != \"{bId}\"");
    }

    private static void DiffRefById(IdentifiableParamContainer? a, IdentifiableParamContainer? b, DiffReport r, string path)
    {
        var aId = a?.Id ?? string.Empty;
        var bId = b?.Id ?? string.Empty;
        if (aId != bId) r.Add($"{path}.Id-ref: \"{aId}\" != \"{bId}\"");
    }
}
