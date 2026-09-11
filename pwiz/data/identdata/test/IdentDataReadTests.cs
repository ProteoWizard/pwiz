using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.IdentData;
using Pwiz.Data.IdentData.Mzid;
using Pwiz.Data.IdentData.PepXml;

namespace Pwiz.Data.IdentData.Tests;

[TestClass]
public class IdentDataReadTests
{
    [TestMethod]
    public void Mzid_ParsesRealFixture_FromMZRefinerTestData()
    {
        // The cpp MZRefiner test fixture is a real MS-GF+ search result mzid (~1.3 MB).
        // Check that we read enough of the schema to feed the MZRefiner consumer pattern:
        // analysis software, sequence collection, and per-PSM scores.
        string fixture = FindFixture("JD_06232014_sample4_C.mzid");
        var ident = new IdentDataFile(fixture);

        Assert.IsTrue(ident.AnalysisSoftwareList.Count > 0, "no analysis software");
        Assert.IsTrue(ident.SequenceCollection.Peptides.Count > 0, "no peptides");
        Assert.IsTrue(ident.SequenceCollection.PeptideEvidence.Count > 0, "no peptide evidence");
        Assert.IsTrue(ident.DataCollection.AnalysisData.SpectrumIdentificationList.Count > 0,
            "no spectrum identification list");

        var sil = ident.DataCollection.AnalysisData.SpectrumIdentificationList[0];
        Assert.IsTrue(sil.SpectrumIdentificationResult.Count > 0, "no per-spectrum results");

        // Every result has at least one identification item, and items carry charge / mz / rank.
        var firstSir = sil.SpectrumIdentificationResult.First(r => r.SpectrumIdentificationItem.Count > 0);
        var firstSii = firstSir.SpectrumIdentificationItem[0];
        Assert.IsTrue(firstSii.ChargeState >= 1, $"chargeState not parsed: {firstSii.ChargeState}");
        Assert.IsTrue(firstSii.ExperimentalMassToCharge > 0, "experimentalMassToCharge not parsed");
        Assert.IsTrue(firstSii.CalculatedMassToCharge > 0, "calculatedMassToCharge not parsed");
        Assert.IsTrue(firstSii.Rank >= 1, "rank not parsed");
    }

    [TestMethod]
    public void Mzid_RoundTripsCvParamsThroughLookup()
    {
        // Spot-check that CV-param accessions round-trip to known CVID enum values. mzid uses
        // accession strings (e.g. "MS:1002338") which we resolve via CvLookup.CvTermInfo(string).
        string fixture = FindFixture("JD_06232014_sample4_C.mzid");
        var ident = new IdentDataFile(fixture);

        // The fixture's spectrumIdentificationItems carry MS-GF+ score CV terms.
        var sii = ident.DataCollection.AnalysisData.SpectrumIdentificationList[0]
            .SpectrumIdentificationResult.First(r => r.SpectrumIdentificationItem.Count > 0)
            .SpectrumIdentificationItem[0];

        Assert.IsTrue(sii.CVParams.Count > 0,
            "expected at least one CV param on the first SII; got none");

        // At least one CV param should resolve to a known CVID (i.e. accession lookup succeeded).
        Assert.IsTrue(sii.CVParams.Any(cv => cv.Cvid != CVID.CVID_Unknown),
            "no CV params resolved to a known CVID — accession lookup may be broken");
    }

    [TestMethod]
    public void PepXmlTranslator_KnownPairs_LookUpInBothDirections()
    {
        Assert.AreEqual(CVID.MS_SEQUEST_xcorr,
            PepXmlTranslator.PepXmlScoreNameToCVID(CVID.MS_SEQUEST, "xcorr"));
        Assert.AreEqual(CVID.MS_Comet_xcorr,
            PepXmlTranslator.PepXmlScoreNameToCVID(CVID.MS_Comet, "xcorr"));
        Assert.AreEqual(CVID.MS_MyriMatch_MVH,
            PepXmlTranslator.PepXmlScoreNameToCVID(CVID.MS_MyriMatch, "mvh"));
        Assert.AreEqual(CVID.CVID_Unknown,
            PepXmlTranslator.PepXmlScoreNameToCVID(CVID.MS_MyriMatch, "xcorr"));

        // Reverse lookup.
        Assert.AreEqual("xcorr",
            PepXmlTranslator.ScoreCVIDToPepXmlScoreName(CVID.MS_SEQUEST, CVID.MS_SEQUEST_xcorr));

        // Software name → CVID.
        Assert.AreEqual(CVID.MS_SEQUEST, PepXmlTranslator.PepXmlSoftwareNameToCVID("Sequest"));
        Assert.AreEqual(CVID.MS_X_Tandem, PepXmlTranslator.PepXmlSoftwareNameToCVID("X! Tandem"));
        Assert.AreEqual(CVID.MS_X_Tandem, PepXmlTranslator.PepXmlSoftwareNameToCVID("xtandem"));
    }

    [TestMethod]
    public void Mzid_RoundTripsThroughWriter_PreservesPsmCounts()
    {
        // Read → write → read again. Verify the SII counts and per-PSM (charge, exp m/z, calc m/z,
        // rank) survive a full round-trip through MzidWriter.
        string fixture = FindFixture("JD_06232014_sample4_C.mzid");
        var original = new IdentDataFile(fixture);

        using var ms = new MemoryStream();
        new MzidWriter().Write(original, ms);
        ms.Position = 0;

        var roundTripped = new IdentDataFile("round.mzid", ms);

        Assert.AreEqual(original.AnalysisSoftwareList.Count, roundTripped.AnalysisSoftwareList.Count);
        Assert.AreEqual(original.SequenceCollection.Peptides.Count,
            roundTripped.SequenceCollection.Peptides.Count);
        Assert.AreEqual(original.SequenceCollection.PeptideEvidence.Count,
            roundTripped.SequenceCollection.PeptideEvidence.Count);

        var origSilCount = original.DataCollection.AnalysisData.SpectrumIdentificationList[0]
            .SpectrumIdentificationResult.Count;
        var rtSilCount = roundTripped.DataCollection.AnalysisData.SpectrumIdentificationList[0]
            .SpectrumIdentificationResult.Count;
        Assert.AreEqual(origSilCount, rtSilCount);

        // Spot-check the first SII with peptide-evidence references.
        var origSir = original.DataCollection.AnalysisData.SpectrumIdentificationList[0]
            .SpectrumIdentificationResult.First(r => r.SpectrumIdentificationItem.Count > 0);
        var rtSir = roundTripped.DataCollection.AnalysisData.SpectrumIdentificationList[0]
            .SpectrumIdentificationResult.First(r => r.Id == origSir.Id);
        var origSii = origSir.SpectrumIdentificationItem[0];
        var rtSii = rtSir.SpectrumIdentificationItem[0];
        Assert.AreEqual(origSii.ChargeState, rtSii.ChargeState);
        Assert.AreEqual(origSii.ExperimentalMassToCharge, rtSii.ExperimentalMassToCharge, 1e-9);
        Assert.AreEqual(origSii.CalculatedMassToCharge, rtSii.CalculatedMassToCharge, 1e-9);
        Assert.AreEqual(origSii.Rank, rtSii.Rank);
    }

    [TestMethod]
    public void PepXml_RoundTripsThroughWriter_PreservesPsmCounts()
    {
        // Build a synthetic IdentData with one search hit, write to pepXML, read back, compare.
        var src = new IdentData { Id = "pep_synth", CreationDate = "2024-01-01T00:00:00" };
        var sw = new AnalysisSoftware { Id = "AS_1", Name = "Comet", Version = "2024.1" };
        sw.SoftwareName.CVParams.Add(new CVParam(CVID.MS_Comet));
        src.AnalysisSoftwareList.Add(sw);

        var sd = new SpectraData { Id = "SD_1", Location = "/tmp/sample.mzML" };
        src.DataCollection.Inputs.SpectraData.Add(sd);

        var pep = new Peptide { Id = "PEP_1", PeptideSequence = "PEPTIDER" };
        src.SequenceCollection.Peptides.Add(pep);

        var sil = new SpectrumIdentificationList { Id = "SIL_1" };
        var sir = new SpectrumIdentificationResult { Id = "SIR_1", SpectrumID = "scan=42", SpectraDataPtr = sd };
        var sii = new SpectrumIdentificationItem
        {
            Id = "SII_1",
            ChargeState = 2,
            ExperimentalMassToCharge = 472.7345,
            CalculatedMassToCharge = 472.7350,
            Rank = 1,
            PassThreshold = true,
            PeptidePtr = pep,
        };
        sii.CVParams.Add(new CVParam(CVID.MS_Comet_xcorr, "3.21"));
        sii.CVParams.Add(new CVParam(CVID.MS_Comet_expectation_value, "1.2e-5"));
        sir.SpectrumIdentificationItem.Add(sii);
        sil.SpectrumIdentificationResult.Add(sir);
        src.DataCollection.AnalysisData.SpectrumIdentificationList.Add(sil);

        using var ms = new MemoryStream();
        new PepXmlWriter().Write(src, ms);
        ms.Position = 0;
        var roundTripped = new IdentDataFile("round.pep.xml", ms);

        Assert.IsTrue(roundTripped.AnalysisSoftwareList.Count >= 1, "no analysis software round-tripped");
        Assert.AreEqual(CVID.MS_Comet,
            roundTripped.AnalysisSoftwareList[0].SoftwareName.CVParams.FirstOrDefault(p => p.Cvid != CVID.CVID_Unknown)?.Cvid);

        Assert.AreEqual(1, roundTripped.DataCollection.AnalysisData.SpectrumIdentificationList.Count);
        var rtSir = roundTripped.DataCollection.AnalysisData.SpectrumIdentificationList[0]
            .SpectrumIdentificationResult[0];
        Assert.AreEqual("scan=42", rtSir.SpectrumID);
        var rtSii = rtSir.SpectrumIdentificationItem[0];
        Assert.AreEqual(2, rtSii.ChargeState);
        Assert.AreEqual(472.7345, rtSii.ExperimentalMassToCharge, 1e-3);
        Assert.AreEqual(472.7350, rtSii.CalculatedMassToCharge, 1e-3);
        Assert.AreEqual(1, rtSii.Rank);

        // Comet xcorr survived translation in both directions.
        var xcorr = rtSii.CVParams.FirstOrDefault(p => p.Cvid == CVID.MS_Comet_xcorr);
        Assert.IsNotNull(xcorr, "xcorr CV param did not round-trip");
        Assert.AreEqual("3.21", xcorr!.Value);
    }

    private static string FindFixture(string name)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            // mzid lives under cpp's MZRefiner test data dir.
            string c = Path.Combine(dir, "pwiz", "analysis", "spectrum_processing",
                "SpectrumList_MZRefinerTest.data", name);
            if (File.Exists(c)) return c;
            dir = Path.GetDirectoryName(dir);
        }
        Assert.Inconclusive($"test fixture not found: {name}");
        throw new InvalidOperationException("unreachable");
    }
}
