using Pwiz.Data.Common.Cv;
using Pwiz.Data.TraData;

namespace Pwiz.Data.TraData.Tests;

/// <summary>
/// Covers the TraML port (data model + IO + Diff). Mirrors the spirit of cpp's
/// <c>TraDataTest</c> / <c>Serializer_traML_Test</c> / <c>DiffTest</c>: tight per-feature
/// methods with named asserts.
/// </summary>
[TestClass]
public class TraDataRoundTripTests
{
    /// <summary>Build a small but exercises-most-features TraData. Mirrors the spirit
    /// of cpp's <c>tradata::examples::initializeTiny</c>.</summary>
    private static TraData BuildSynthetic()
    {
        var td = new TraData { Id = "synthetic" };
        td.CVs.AddRange(TraMlVersion.DefaultCVList());

        // Contact + Software + Instrument
        var contact = new Contact("C1");
        contact.Set(CVID.MS_contact_email, "test@example.org");
        td.Contacts.Add(contact);

        var sw = new Software("pwiz", CVID.MS_pwiz, "3.0.0");
        td.Software.Add(sw);

        var inst = new Instrument("I1");
        inst.Set(CVID.MS_LTQ_Orbitrap);
        td.Instruments.Add(inst);

        // Protein + Peptide
        var prot = new Protein("PROT_1") { Sequence = "MKWVTFISLLLLFSSAYSRGVF" };
        td.Proteins.Add(prot);

        var pep = new Peptide("PEP_1") { Sequence = "WVTFISLLLLF" };
        pep.Set(CVID.MS_theoretical_mass, "1290.7281", CVID.UO_dalton);
        pep.Modifications.Add(new Modification
        {
            Location = 0,
            MonoisotopicMassDelta = 42.0106,
            AverageMassDelta = 42.0367,
        });
        // Reference back to the parent protein (resolved on read by id).
        pep.Proteins.Add(prot);

        var rt = new RetentionTime { Software = sw };
        rt.Set(CVID.MS_local_retention_time, "1234.5", CVID.UO_second);
        pep.RetentionTimes.Add(rt);

        td.Peptides.Add(pep);

        // Compound (non-peptide)
        var compound = new Compound("C_GLUCOSE");
        compound.Set(CVID.MS_molecular_formula, "C6H12O6");
        td.Compounds.Add(compound);

        // Transition (Q1 → Q3 for peptide PEP_1)
        var t = new Transition { Id = "T1", Peptide = pep };
        t.Precursor.Set(CVID.MS_isolation_window_target_m_z, "645.86", CVID.MS_m_z);
        t.Product.Set(CVID.MS_isolation_window_target_m_z, "402.21", CVID.MS_m_z);

        var interp = new Interpretation();
        interp.Set(CVID.MS_product_ion_series_ordinal, "5");
        t.Interpretations.Add(interp);

        var cfg = new Configuration { Instrument = inst, Contact = contact };
        cfg.Set(CVID.MS_collision_energy, "27.0", CVID.UO_electronvolt);
        var v = new Validation();
        v.Set(CVID.MS_product_ion_m_z, "402.21", CVID.MS_m_z);
        cfg.Validations.Add(v);
        t.Configurations.Add(cfg);

        td.Transitions.Add(t);

        // Target (include side; references PEP_1)
        var target = new Target { Id = "TARG1", Peptide = pep };
        target.Precursor.Set(CVID.MS_isolation_window_target_m_z, "645.86", CVID.MS_m_z);
        td.Targets.IncludeList.Add(target);

        return td;
    }

    [TestMethod]
    public void Synthetic_RoundTripsThroughMemoryStream()
    {
        var td = BuildSynthetic();

        using var ms = new MemoryStream();
        TraDataIO.Write(ms, td);
        ms.Position = 0;
        var rt = TraDataIO.Read(ms);

        // TraData.Id is "for internal use; not currently in the schema" (cpp), so a pure
        // stream round-trip drops it. Compare with ignoreMetadata to skip the id check.
        Assert.IsTrue(TraDataDiff.IsEqual(td, rt, out string reason, ignoreMetadata: true),
            $"round-trip mismatch: {reason}");

        // Spot-check the resolved cross-references made it through the second pass.
        Assert.AreSame(rt.Software[0], rt.Peptides[0].RetentionTimes[0].Software,
            "RetentionTime.softwareRef must resolve to the actual Software instance");
        Assert.AreSame(rt.Peptides[0], rt.Transitions[0].Peptide,
            "Transition.peptideRef must resolve to the actual Peptide instance");
        Assert.AreSame(rt.Peptides[0].Proteins[0], rt.Proteins[0],
            "Peptide.ProteinRef must resolve to the actual Protein instance");
        Assert.AreSame(rt.Contacts[0], rt.Transitions[0].Configurations[0].Contact);
        Assert.AreSame(rt.Instruments[0], rt.Transitions[0].Configurations[0].Instrument);
    }

    [TestMethod]
    public void File_RoundTripsThroughDisk_AndDetectsByExtension()
    {
        var td = BuildSynthetic();
        string path = Path.Combine(Path.GetTempPath(), $"tradata-{System.Guid.NewGuid():N}.TraML");
        try
        {
            TraDataFile.Write(td, path);
            var rt = TraDataFile.Read(path);
            // File-level Read derives id from the file basename — match by setting it on td.
            td.Id = Path.GetFileNameWithoutExtension(path);
            Assert.IsTrue(TraDataDiff.IsEqual(td, rt, out string reason), reason);
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [TestMethod]
    public void Diff_DetectsTransitionMismatch_AndReportsLocation()
    {
        var a = BuildSynthetic();
        var b = BuildSynthetic();
        Assert.IsTrue(TraDataDiff.IsEqual(a, b, out _), "identical synthetics must match");

        // Mutate the m/z of the first transition's precursor and verify the diff path
        // reports the right level.
        b.Transitions[0].Precursor.CVParams[0] = new Pwiz.Data.Common.Params.CVParam(
            CVID.MS_isolation_window_target_m_z, "999.99", CVID.MS_m_z);
        Assert.IsFalse(TraDataDiff.IsEqual(a, b, out string reason));
        StringAssert.Contains(reason, "Transitions[0]", System.StringComparison.Ordinal);
        StringAssert.Contains(reason, "Precursor", System.StringComparison.Ordinal);
    }

    [TestMethod]
    public void Empty_TraData_RoundTrips()
    {
        var td = new TraData();
        using var ms = new MemoryStream();
        TraDataIO.Write(ms, td);
        ms.Position = 0;
        var rt = TraDataIO.Read(ms);
        Assert.IsTrue(rt.IsEmpty || (rt.Transitions.Count == 0 && rt.Peptides.Count == 0),
            "empty-write empty-read must round-trip");
    }
}
