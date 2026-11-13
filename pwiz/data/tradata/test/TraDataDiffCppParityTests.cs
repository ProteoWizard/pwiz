using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.TraData.Tests;

/// <summary>
/// Port of cpp's <c>pwiz/data/tradata/DiffTest.cpp</c>. cpp builds two equal objects of each type,
/// asserts the diff is empty, mutates exactly one field, and asserts the diff sees it.
/// </summary>
/// <remarks>
/// cpp instantiates a <c>Diff&lt;T&gt;</c> per type; the port exposes IsEqual for the document plus
/// Protein/Peptide/Transition/Software/Contact, so the remaining types are compared by embedding
/// them in a TraData. The mutation and the expectation are cpp's either way.
/// </remarks>
[TestClass]
public class TraDataDiffCppParityTests
{
    private delegate void Mutate(TraData td);

    /// <summary>One cpp test function each: what it changes, on a document that is otherwise equal.</summary>
    private static readonly (string What, Mutate Apply)[] Mutations =
    {
        ("testContact: contact id", td => td.Contacts[0].Id = "bar"),
        ("testInstrument: instrument id", td => td.Instruments[0].Id = "bar"),
        ("testSoftware: software version", td => td.Software[0].Version = "4.21"),
        ("testConfiguration: referenced instrument id",
            td => td.Transitions[0].Configurations[0].Instrument!.Id = "different"),
        ("testPrediction: prediction software reference",
            td => td.Transitions[0].Prediction.Software = td.Software[0]),
        ("testValidation: validation cvParam",
            td => td.Transitions[0].Configurations[0].Validations[0].Set(CVID.MS_peak_intensity, 42)),
        ("testEvidence: peptide evidence cvParam",
            td => td.Peptides[0].Evidence.Set(CVID.MS_peak_intensity, 42)),
        ("testRetentionTime: retention time cvParam",
            td => td.Peptides[0].RetentionTimes[0].Set(CVID.MS_peak_intensity, 42)),
        ("testProtein: protein sequence", td => td.Proteins[0].Sequence = "DCBA"),
        ("testModification: monoisotopic mass delta",
            td => td.Peptides[0].Modifications[0].MonoisotopicMassDelta = 84),
        ("testPeptide: peptide sequence", td => td.Peptides[0].Sequence = "DCBA"),
        ("testCompound: added retention time",
            td => td.Compounds[0].RetentionTimes.Add(new RetentionTime())),
        ("testTransition: referenced peptide sequence",
            td => td.Transitions[0].Peptide!.Sequence = "different"),
        ("testTarget: target precursor m/z",
            td => td.Targets.IncludeList[0].Precursor.Set(CVID.MS_selected_ion_m_z, 999.99, CVID.MS_m_z)),
        ("testTraData: added CV", td => td.CVs.Add(new CV { Id = "EXTRA" })),
        ("testTraData: added software", td => td.Software.Add(new Software("extra"))),
        ("testTraData: added publication", td =>
        {
            var pub = new Publication { Id = "PUBMED2" };
            pub.Set(CVID.UO_dalton, 456);
            td.Publications.Add(pub);
        }),
    };

    [TestMethod]
    public void Diff_SeesEveryChangeCppChecks()
    {
        var missed = new List<string>();
        foreach (var (what, apply) in Mutations)
        {
            var a = Build();
            var b = Build();
            Assert.IsTrue(TraDataDiff.IsEqual(a, b, out string why),
                $"the two unmutated documents must compare equal before testing '{what}': {why}");

            apply(b);
            if (TraDataDiff.IsEqual(b, a, out _))
                missed.Add($"  {what}");
        }

        Assert.AreEqual(0, missed.Count,
            $"TraDataDiff reported no difference for {missed.Count} of {Mutations.Length} changes:" +
            Environment.NewLine + string.Join(Environment.NewLine, missed));
    }

    /// <summary>cpp compares these types directly, so the port's per-type entry points get the
    /// same treatment rather than only being reached through a whole document.</summary>
    [TestMethod]
    public void PerTypeComparisons_SeeTheirOwnChanges()
    {
        Assert.IsTrue(TraDataDiff.IsEqualContact(new Contact("foo"), new Contact("foo"), out _));
        Assert.IsFalse(TraDataDiff.IsEqualContact(new Contact("foo"), new Contact("bar"), out _),
            "contact id");

        var swA = new Software("msdata", CVID.MS_ionization_type, "4.20");
        var swB = new Software("msdata", CVID.MS_ionization_type, "4.20");
        Assert.IsTrue(TraDataDiff.IsEqualSoftware(swA, swB, out _));
        swB.Version = "4.21";
        Assert.IsFalse(TraDataDiff.IsEqualSoftware(swA, swB, out _), "software version");

        var protA = new Protein("foo") { Sequence = "ABCD" };
        var protB = new Protein("foo") { Sequence = "ABCD" };
        Assert.IsTrue(TraDataDiff.IsEqualProtein(protA, protB, out _));
        protB.Sequence = "DCBA";
        Assert.IsFalse(TraDataDiff.IsEqualProtein(protA, protB, out _), "protein sequence");

        var pepA = new Peptide("foo") { Sequence = "ABCD" };
        var pepB = new Peptide("foo") { Sequence = "ABCD" };
        Assert.IsTrue(TraDataDiff.IsEqualPeptide(pepA, pepB, out _));
        pepB.Sequence = "DCBA";
        Assert.IsFalse(TraDataDiff.IsEqualPeptide(pepA, pepB, out _), "peptide sequence");

        var transA = new Transition { Id = "T1", Peptide = new Peptide("common") };
        var transB = new Transition { Id = "T1", Peptide = new Peptide("common") };
        transA.Precursor.Set(CVID.MS_selected_ion_m_z, 123.45, CVID.MS_m_z);
        transB.Precursor.Set(CVID.MS_selected_ion_m_z, 123.45, CVID.MS_m_z);
        Assert.IsTrue(TraDataDiff.IsEqualTransition(transA, transB, out _));
        transB.Peptide!.Sequence = "different";
        Assert.IsFalse(TraDataDiff.IsEqualTransition(transA, transB, out _), "referenced peptide");
    }

    /// <summary>A document holding one of everything cpp's DiffTest touches.</summary>
    private static TraData Build()
    {
        var td = new TraData { Id = "diff" };

        var contact = new Contact("foo");
        contact.Set(CVID.MS_m_z, 1.0, CVID.MS_m_z);
        td.Contacts.Add(contact);

        var instrument = new Instrument("foo");
        instrument.Set(CVID.MS_m_z, 1.0, CVID.MS_m_z);
        td.Instruments.Add(instrument);

        var software = new Software("msdata", CVID.MS_ionization_type, "4.20");
        td.Software.Add(software);

        var publication = new Publication { Id = "PUBMED1" };
        publication.Set(CVID.UO_dalton, 123);
        td.Publications.Add(publication);

        var protein = new Protein("foo") { Sequence = "ABCD" };
        td.Proteins.Add(protein);

        var peptide = new Peptide("foo") { Sequence = "ABCD" };
        peptide.Evidence.Set(CVID.MS_m_z, 1.0, CVID.MS_m_z);
        peptide.Modifications.Add(new Modification
        {
            Location = 7,
            MonoisotopicMassDelta = 42,
            AverageMassDelta = 42,
        });
        peptide.RetentionTimes.Add(new RetentionTime { Software = software });
        peptide.Proteins.Add(protein);
        td.Peptides.Add(peptide);

        var compound = new Compound("C1");
        td.Compounds.Add(compound);

        var validation = new Validation();
        var configuration = new Configuration { Instrument = instrument, Contact = contact };
        configuration.Validations.Add(validation);

        var transition = new Transition { Id = "T1", Peptide = peptide };
        transition.Precursor.Set(CVID.MS_selected_ion_m_z, 123.45, CVID.MS_m_z);
        transition.Product.Set(CVID.MS_selected_ion_m_z, 456.78, CVID.MS_m_z);
        transition.Configurations.Add(configuration);
        td.Transitions.Add(transition);

        var target = new Target { Id = "TARG1", Peptide = peptide };
        target.Precursor.Set(CVID.MS_selected_ion_m_z, 123.45, CVID.MS_m_z);
        td.Targets.IncludeList.Add(target);

        return td;
    }
}
