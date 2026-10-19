using Pwiz.Data.Common.Proteome;

namespace Pwiz.Data.Common.Tests;

/// <summary>
/// The cases from cpp's <c>pwiz/data/proteome/DiffTest.cpp</c> that
/// <c>ProteomeTests.Diff_DetectsSequenceAndDescriptionMismatches</c> does not already cover:
/// list-length asymmetry, which side each unmatched protein came from, and the ProteomeData level.
/// </summary>
[TestClass]
public class ProteomeDataDiffCppParityTests
{
    private static ProteinListSimple ListOf(params Protein[] proteins)
    {
        var list = new ProteinListSimple();
        list.Proteins.AddRange(proteins);
        return list;
    }

    private static Protein P(string id, string description = "", string sequence = "") =>
        new(id, 0, description, sequence);

    /// <summary>cpp testProteinList: a protein present only in a is reported, and stays reported
    /// under ignoreMetadata - that flag suppresses description differences, not missing entries.</summary>
    [TestMethod]
    public void ProteinList_ExtraEntryOnOneSide_IsReportedEvenIgnoringMetadata()
    {
        var a = ListOf(P("420"), P("421"));
        var b = ListOf(P("420"));

        // cpp exposes the unmatched proteins structurally (diff.a_b.proteins[0]->id == "421");
        // the port returns a reason string and short-circuits on the length mismatch, so the most
        // that can be compared here is that the difference is detected and the sizes are named.
        Assert.IsFalse(ProteomeDataDiff.IsEqual(a, b, out string reason), "extra protein in a");
        StringAssert.Contains(reason, "2", StringComparison.Ordinal);

        Assert.IsFalse(ProteomeDataDiff.IsEqual(a, b, out _, ignoreMetadata: true),
            "ignoreMetadata must not hide a missing protein");
    }

    /// <summary>cpp testProteinList: with a different extra entry on each side, the report has to
    /// name both, so a reader can tell which side each came from.</summary>
    [TestMethod]
    public void ProteinList_DifferentExtraOnEachSide_NamesBoth()
    {
        var a = ListOf(P("420"), P("421"));
        var b = ListOf(P("420"), P("422"));

        Assert.IsFalse(ProteomeDataDiff.IsEqual(a, b, out string reason));
        StringAssert.Contains(reason, "421", StringComparison.Ordinal);
        StringAssert.Contains(reason, "422", StringComparison.Ordinal);
    }

    /// <summary>cpp testProteinList: sequence differences survive ignoreMetadata, description
    /// differences do not. Same distinction, checked on the list rather than the document.</summary>
    [TestMethod]
    public void ProteinList_IgnoreMetadata_SuppressesDescriptionButNotSequence()
    {
        var a = ListOf(P("421", description: "", sequence: ""));

        var describedDifferently = ListOf(P("421", description: "different metadata", sequence: ""));
        Assert.IsFalse(ProteomeDataDiff.IsEqual(a, describedDifferently, out _));
        Assert.IsTrue(ProteomeDataDiff.IsEqual(a, describedDifferently, out _, ignoreMetadata: true));

        var sequencedDifferently = ListOf(P("421", description: "", sequence: "ELVISLIVES"));
        Assert.IsFalse(ProteomeDataDiff.IsEqual(a, sequencedDifferently, out _));
        Assert.IsFalse(ProteomeDataDiff.IsEqual(a, sequencedDifferently, out _, ignoreMetadata: true),
            "a sequence difference is data, not metadata");
    }

    /// <summary>cpp testProteomeData: the id and the presence of a protein list both count.</summary>
    [TestMethod]
    public void ProteomeData_IdAndProteinListPresence_AreReported()
    {
        var a = new ProteomeData { Id = "goober" };
        var b = new ProteomeData { Id = "goober" };
        Assert.IsTrue(ProteomeDataDiff.IsEqual(a, b, out _), "identical documents match");

        b.Id = "raisinet";
        Assert.IsFalse(ProteomeDataDiff.IsEqual(a, b, out string idReason), "id difference");
        StringAssert.Contains(idReason, "raisinet", StringComparison.Ordinal);

        b.Id = "goober";
        b.ProteinList = ListOf(P("p1"));
        Assert.IsFalse(ProteomeDataDiff.IsEqual(a, b, out _),
            "a protein list on one side only is a difference");
    }
}
