using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData.Tests;

/// <summary>
/// Port of cpp's <c>pwiz/data/identdata/DiffTest.cpp</c>. cpp builds two equal objects of each
/// schema type, asserts the diff is empty, changes exactly one field, and asserts the diff sees
/// it. The fixtures are the same <c>Build*</c> helpers <see cref="IdentDataIoTest"/> uses, so
/// every case here is mutating a tree the IO test already round-trips.
/// </summary>
/// <remarks>
/// The IO test only ever asserts the diff comes back EMPTY. On its own that cannot distinguish a
/// working comparison from one that returns "no differences" for everything, which is exactly the
/// gap this closes - and exactly the gap that, in the tradata equivalent, was hiding eight
/// unread regions of the document.
/// </remarks>
[TestClass]
public class IdentDataDiffCppParityTests
{
    private delegate void Mutate(IdentData ident);

    private static readonly (string What, Func<IdentData> Build, Mutate Apply)[] Cases =
    {
        ("testIdentifiable: name", IdentDataIoTest.BuildIdentifiable, i => i.Name = "different"),
        ("testIdentifiable: id", IdentDataIoTest.BuildIdentifiable, i => i.Id = "different"),
        ("IdentifiableParamContainer: cvParam value", IdentDataIoTest.BuildIdentifiableParamContainer,
            i => i.AnalysisSoftwareList[0].SoftwareName.Set(CVID.MS_TIC, "999")),
        ("IdentifiableParamContainer: userParam", IdentDataIoTest.BuildIdentifiableParamContainer,
            i => i.AnalysisSoftwareList[0].SoftwareName.UserParams.Add(new UserParam("extra", "1"))),
        ("testCv: version", IdentDataIoTest.BuildCv, i => i.Cvs[0].Version = "9.9"),
        ("testCv: URI", IdentDataIoTest.BuildCv, i => i.Cvs[0].Uri = "http://example.org"),
        ("testBibliographicReference: year", IdentDataIoTest.BuildBibliographicReference,
            i => i.BibliographicReferences[0].Year = 1999),
        ("testBibliographicReference: title", IdentDataIoTest.BuildBibliographicReference,
            i => i.BibliographicReferences[0].Title = "another title"),
        ("testPerson: last name", IdentDataIoTest.BuildPerson,
            i => ((Person)i.AuditCollection[0]).LastName = "Jones"),
        ("testPerson: affiliation", IdentDataIoTest.BuildPerson,
            i => ((Person)i.AuditCollection[0]).Affiliations.Clear()),
        ("testOrganization: parent ref", IdentDataIoTest.BuildOrganization,
            i => ((Organization)i.AuditCollection[1]).Parent = null),
        ("testContact: contact param", IdentDataIoTest.BuildPerson,
            i => ((Person)i.AuditCollection[0]).Set(CVID.MS_contact_email, "someone@else.org")),
        ("ContactRole: role", IdentDataIoTest.BuildContactRole,
            i => i.AnalysisSoftwareList[0].ContactRolePtr!.Role = new CVParam(CVID.MS_researcher)),
        ("Provider: software ref", IdentDataIoTest.BuildProvider,
            i => i.Provider.AnalysisSoftwarePtr = null),
        ("testSample: contact role list", IdentDataIoTest.BuildSample,
            i => i.AnalysisSampleCollection.Samples[1].ContactRole.Clear()),
        ("AnalysisSoftware: version", IdentDataIoTest.BuildAnalysisSoftware,
            i => i.AnalysisSoftwareList[0].Version = "0.0"),
        ("testDBSequence: sequence", IdentDataIoTest.BuildDbSequence,
            i => i.SequenceCollection.DBSequences[0].Seq = "ELVIS"),
        ("testDBSequence: length", IdentDataIoTest.BuildDbSequence,
            i => i.SequenceCollection.DBSequences[0].Length = 4242),
        ("testModification: mono mass delta", IdentDataIoTest.BuildModification,
            i => i.SequenceCollection.Peptides[0].Modifications[0].MonoisotopicMassDelta = 99.9),
        ("testModification: location", IdentDataIoTest.BuildModification,
            i => i.SequenceCollection.Peptides[0].Modifications[0].Location = 7),
        ("testSubstitutionModification: replacement residue", IdentDataIoTest.BuildSubstitutionModification,
            i => i.SequenceCollection.Peptides[0].SubstitutionModifications[0].ReplacementResidue = 'W'),
        ("testPeptide: sequence", IdentDataIoTest.BuildPeptide,
            i => i.SequenceCollection.Peptides[0].PeptideSequence = "ELVISLIVES"),
        ("testSpectrumIdentification: input spectra ref", IdentDataIoTest.BuildSpectrumIdentification,
            i => i.AnalysisCollection.SpectrumIdentification[0].InputSpectra.Clear()),
        ("testProteinDetection: activity date", IdentDataIoTest.BuildProteinDetection,
            i => i.AnalysisCollection.ProteinDetection.ActivityDate = "2001-01-01T00:00:00"),
        ("testSearchModification: fixed flag", IdentDataIoTest.BuildSearchModification,
            i => i.AnalysisProtocolCollection.SpectrumIdentificationProtocol[0]
                  .ModificationParams[0].FixedMod ^= true),
        ("testEnzyme: missed cleavages", IdentDataIoTest.BuildEnzyme,
            i => i.AnalysisProtocolCollection.SpectrumIdentificationProtocol[0]
                  .Enzymes.EnzymeList[0].MissedCleavages = 42),
        ("testEnzyme: site regexp", IdentDataIoTest.BuildEnzyme,
            i => i.AnalysisProtocolCollection.SpectrumIdentificationProtocol[0]
                  .Enzymes.EnzymeList[0].SiteRegexp = "(?<=[KR])"),
        ("testEnzymes: independent flag", IdentDataIoTest.BuildEnzymes,
            i => i.AnalysisProtocolCollection.SpectrumIdentificationProtocol[0]
                  .Enzymes.Independent = false),
        ("testResidue: mass", IdentDataIoTest.BuildResidue,
            i => i.AnalysisProtocolCollection.SpectrumIdentificationProtocol[0]
                  .MassTable[0].Residues[0].Mass = 12.34),
        ("testAmbiguousResidue: code", IdentDataIoTest.BuildAmbiguousResidue,
            i => i.AnalysisProtocolCollection.SpectrumIdentificationProtocol[0]
                  .MassTable[0].AmbiguousResidue[0].Code = 'Z'),
        ("testMassTable: msLevel list", IdentDataIoTest.BuildMassTable,
            i => i.AnalysisProtocolCollection.SpectrumIdentificationProtocol[0]
                  .MassTable[0].MsLevel.Add(9)),
        ("testFilter: filter type param", IdentDataIoTest.BuildFilter,
            i => i.AnalysisProtocolCollection.SpectrumIdentificationProtocol[0]
                  .DatabaseFilters[0].FilterType.CVParams.Clear()),
        ("testSpectraData: location", IdentDataIoTest.BuildSpectraData,
            i => i.DataCollection.Inputs.SpectraData[0].Location = "file:///elsewhere"),
        ("testSearchDatabase: number of sequences", IdentDataIoTest.BuildSearchDatabase,
            i => i.DataCollection.Inputs.SearchDatabase[0].NumDatabaseSequences = 4242),
        ("testSourceFile: location", IdentDataIoTest.BuildSourceFile,
            i => i.DataCollection.Inputs.SourceFile[0].Location = "file:///elsewhere"),
        ("testMeasure: measure param", IdentDataIoTest.BuildMeasure,
            i => i.DataCollection.AnalysisData.SpectrumIdentificationList[0]
                  .FragmentationTable[0].Set(CVID.MS_product_ion_intensity)),
        ("testFragmentArray: values", IdentDataIoTest.BuildFragmentArray,
            i => i.DataCollection.AnalysisData.SpectrumIdentificationList[0]
                  .SpectrumIdentificationResult[0].SpectrumIdentificationItem[0]
                  .Fragmentation[0].FragmentArray[0].Values[0] = 999.9),
        ("testIonType: index", IdentDataIoTest.BuildIonType,
            i => i.DataCollection.AnalysisData.SpectrumIdentificationList[0]
                  .SpectrumIdentificationResult[0].SpectrumIdentificationItem[0]
                  .Fragmentation[0].Index.Add(42)),
        ("testPeptideEvidence: start", IdentDataIoTest.BuildPeptideEvidence,
            i => i.SequenceCollection.PeptideEvidence[0].Start = 42),
        ("testPeptideEvidence: is decoy", IdentDataIoTest.BuildPeptideEvidence,
            i => i.SequenceCollection.PeptideEvidence[0].IsDecoy ^= true),
        ("SpectrumIdentificationItem: charge state", IdentDataIoTest.BuildSpectrumIdentificationItem,
            i => i.DataCollection.AnalysisData.SpectrumIdentificationList[0]
                  .SpectrumIdentificationResult[0].SpectrumIdentificationItem[0].ChargeState = 9),
        ("SpectrumIdentificationItem: experimental m/z", IdentDataIoTest.BuildSpectrumIdentificationItem,
            i => i.DataCollection.AnalysisData.SpectrumIdentificationList[0]
                  .SpectrumIdentificationResult[0].SpectrumIdentificationItem[0].ExperimentalMassToCharge = 1.5),
        ("SpectrumIdentificationResult: spectrum id", IdentDataIoTest.BuildSpectrumIdentificationResult,
            i => i.DataCollection.AnalysisData.SpectrumIdentificationList[0]
                  .SpectrumIdentificationResult[0].SpectrumID = "scan=999"),
        ("testProteinDetectionHypothesis: pass threshold", IdentDataIoTest.BuildProteinDetectionHypothesis,
            i => i.DataCollection.AnalysisData.ProteinDetectionListPtr!
                  .ProteinAmbiguityGroup[0].ProteinDetectionHypothesis[0].PassThreshold ^= true),
        ("testProteinAmbiguityGroup: hypothesis list", IdentDataIoTest.BuildProteinAmbiguityGroup,
            i => i.DataCollection.AnalysisData.ProteinDetectionListPtr!
                  .ProteinAmbiguityGroup[0].ProteinDetectionHypothesis.Clear()),
        ("testSpectrumIdentificationList: numSequencesSearched", IdentDataIoTest.BuildSpectrumIdentificationList,
            i => i.DataCollection.AnalysisData.SpectrumIdentificationList[0].NumSequencesSearched = 4242),
    };

    [TestMethod]
    public void Diff_SeesEveryChangeCppChecks()
    {
        var missed = new List<string>();
        foreach (var (what, build, apply) in Cases)
        {
            var a = build();
            var b = build();
            var sanity = IdentDataDiff.Diff(a, b);
            Assert.IsTrue(sanity.IsEmpty,
                $"two unmutated fixtures must compare equal before testing '{what}':{Environment.NewLine}{sanity}");

            apply(b);
            if (IdentDataDiff.Diff(a, b).IsEmpty)
                missed.Add($"  {what}");
        }

        Assert.AreEqual(0, missed.Count,
            $"IdentDataDiff reported no difference for {missed.Count} of {Cases.Length} changes:" +
            Environment.NewLine + string.Join(Environment.NewLine, missed));
    }
}
