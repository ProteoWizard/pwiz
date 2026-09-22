using System.Globalization;
using System.Text.RegularExpressions;
using Pwiz.Build.CvGen;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Obo;
using Pwiz.TestHarness;

namespace Pwiz.Data.Common.Tests.Cv;

[TestClass]
public class CvidEnumGeneratorTests
{
    private const string TREE_UNREACHABLE =
        "The pwiz source tree is not reachable from this test drop.";

    /// <summary>
    /// The checked-in enum must be what CvGen produces from the checked-in OBO files, so an
    /// OBO refresh cannot land without its regenerated enum (or the other way round).
    /// </summary>
    [TestMethod]
    public void CvidGeneratedCs_IsCurrentForTheTrackedObos()
    {
        var obos = LoadTrackedObos();
        string generatedPath = Path.Combine(SharpRootOrSkip(), "pwiz", "src", "Common", "CVID.generated.cs");
        if (!File.Exists(generatedPath))
            Assert.Inconclusive($"{generatedPath} not present. {TREE_UNREACHABLE}");

        string expected = CvidEnumGenerator.Generate(obos);
        string actual = File.ReadAllText(generatedPath).Replace("\r\n", "\n");
        if (expected == actual)
            return;

        // Name the first differing line rather than dumping 19,000 of them.
        string[] expectedLines = expected.Split('\n'), actualLines = actual.Split('\n');
        int line = 0;
        while (line < expectedLines.Length && line < actualLines.Length && expectedLines[line] == actualLines[line])
            ++line;
        Assert.Fail($"{generatedPath} is stale (first difference at line {line + 1}: " +
                    $"expected '{expectedLines.ElementAtOrDefault(line)}', found '{actualLines.ElementAtOrDefault(line)}'). " +
                    "Run `dotnet run --project pwiz-sharp/build/CvGen` and commit the result with the OBO change.");
    }

    /// <summary>
    /// Every identifier cpp cvgen put in <c>cv.hpp</c> must still be in the C# enum with the
    /// same value. Without this the only check on the generator is that it agrees with its own
    /// committed output, and a rule that drifted from cvgen would regenerate green while the
    /// C++ and C# halves of ProteoWizard disagreed about what an accession means.
    /// </summary>
    /// <remarks>
    /// The C# enum is allowed to be a strict superset: it carries PEFF:1002001-1002003, which
    /// cvgen dropped because its term set was keyed on the accession NUMBER and MS:1002001-
    /// 1002003 reached it first. Goes Inconclusive once the C++ tree is retired and cv.hpp no
    /// longer exists - by then there is no cvgen output left to be parity with.
    /// </remarks>
    [TestMethod]
    public void CvidGeneratedCs_MatchesCppCvHpp()
    {
        string cvHpp = Path.Combine(CppRootOrSkip(), "data", "common", "cv.hpp");
        if (!File.Exists(cvHpp))
            Assert.Inconclusive($"{cvHpp} not present; the cpp tree is gone, so there is no cvgen output to compare against.");

        var cppMembers = ParseEnumMembers(File.ReadAllText(cvHpp));
        var csMembers = ParseEnumMembers(CvidEnumGenerator.Generate(LoadTrackedObos()));
        Assert.IsTrue(cppMembers.Count > 6000,
            $"only {cppMembers.Count} members parsed out of cv.hpp; the parse is broken, not the enum");

        var missing = cppMembers.Keys.Where(name => !csMembers.ContainsKey(name))
            .OrderBy(n => n, StringComparer.Ordinal).ToList();
        var differing = cppMembers
            .Where(kvp => csMembers.TryGetValue(kvp.Key, out string? value) && value != kvp.Value)
            .Select(kvp => $"{kvp.Key}: cpp={kvp.Value} cs={csMembers[kvp.Key]}")
            .OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.AreEqual(0, missing.Count,
            $"CVIDs in cv.hpp missing from the C# enum: {string.Join(", ", missing.Take(20))}");
        Assert.AreEqual(0, differing.Count,
            $"CVIDs whose value differs from cv.hpp: {string.Join(", ", differing.Take(20))}");
    }

    /// <summary>
    /// The prefix-to-value-block numbering has two owners - the generator derives it from each
    /// OBO's prefix set, <see cref="CvLookup"/> hardcodes the offsets - and nothing else ties
    /// them together. A psi-ms.obo revision introducing a term in a namespace that sorts before
    /// an existing one renumbers every later block, and the only symptom would be accessions
    /// quietly computed against the wrong offset.
    /// </summary>
    [TestMethod]
    public void GeneratedValues_AgreeWithCvLookupOffsets()
    {
        var expectations = new (string Prefix, uint Id, int Offset)[]
        {
            ("MS", 1000001, CvLookup.OffsetMs),
            ("PEFF", 1, CvLookup.OffsetPeff),
            ("UNIMOD", 1, CvLookup.OffsetUnimod),
            ("UO", 1, CvLookup.OffsetUo),
        };

        var members = ParseEnumMembers(CvidEnumGenerator.Generate(LoadTrackedObos()));
        foreach (var (prefix, id, offset) in expectations)
        {
            string wanted = (id + (long)offset).ToString(CultureInfo.InvariantCulture);
            bool found = members.Any(kvp =>
                kvp.Value == wanted && kvp.Key.StartsWith(prefix + "_", StringComparison.Ordinal));
            Assert.IsTrue(found,
                $"CvGen gave {prefix}:{id} a value other than {wanted}. The prefix numbering moved, " +
                "so CvLookup's Offset* constants and AccessionForCvid have to move with it.");
        }
    }

    /// <summary>
    /// Every identifier rule cvgen.cpp applied, on one small ontology pair: block numbering
    /// per prefix (in first-seen order, letters in an NCIT accession read as digits), the
    /// unit terms that psi-ms.obo duplicates skipped, OBSOLETE handling, character escaping,
    /// same-identifier terms suffixed with their values, synonym aliases for MS terms only -
    /// hex-encoded when only punctuation separates them from the name, dropped when two of
    /// them escape alike - and a comment standing in for a missing def.
    /// </summary>
    [TestMethod]
    public void Generate_AppliesCvgenIdentifierRules()
    {
        var psiMsObo = ObOntology.Parse(new StringReader(SAMPLE_PSI_MS));
        psiMsObo.Filename = "psi-ms.obo";
        var unitObo = ObOntology.Parse(new StringReader(SAMPLE_UNIT));
        unitObo.Filename = "unit.obo";

        const string expectedBody = """
            public enum CVID
            {
                CVID_Unknown = -1,

                /// PEFF CV term: PSI Extended FASTA Format controlled vocabulary term.
                PEFF_PEFF_CV_term = 200000001,

                /// Duration: The period of time during which something continues.
                NCIT_Duration = 100325330,

                /// sample number: A reference number relevant to the sample under study.
                MS_sample_number = 1000001,

                /// sample id (sample number): A reference number relevant to the sample under study.
                MS_sample_id = MS_sample_number,

                /// tandem time-of-flight: Two TOF analyzers.
                MS_tandem_time_of_flight = 1000002,

                /// TOF/TOF (tandem time-of-flight): Two TOF analyzers.
                MS_TOF_TOF = MS_tandem_time_of_flight,

                /// LTQ Velos/ETD: Velos with ETD.
                MS_LTQ_Velos_ETD = 1000003,

                /// LTQ Velos_x20_ETD (LTQ Velos/ETD): Velos with ETD.
                MS_LTQ_Velos_x20_ETD = MS_LTQ_Velos_ETD,

                /// M+H ion: Protonated.
                MS_M_H_ion_1000004 = 1000004,

                /// M-H ion: Deprotonated.
                MS_M_H_ion_1000005 = 1000005,

                /// legacy scan: Was a scan.
                MS_legacy_scan_OBSOLETE = 1000006,

                /// µ-scan of λmax at 10°: Escapes: \"quoted\" text.
                MS___micro___scan_of__xCE__xBB_max_at_10__deg__ = 1000007,

                /// (?<=[KR])(?!P): Trypsin regex.
                MS______KR_____P_ = 1000008,

                /// undefined term: Definition lives in the comment.
                MS_undefined_term = 1000009,

                /// unit: A unit.
                UO_unit = 300000001
            }

            """;

        string generated = CvidEnumGenerator.Generate(new[] { psiMsObo, unitObo });
        string body = generated[generated.IndexOf("public enum CVID", StringComparison.Ordinal)..];
        Assert.AreEqual(expectedBody.Replace("\r\n", "\n"), body);
        StringAssert.Contains(generated, "//   psi-ms.obo  format-version: 1.2  data-version: test-4.1\n");
    }

    /// <summary>
    /// An ontology parsed from a stream carries no filename, and the rule that drops
    /// psi-ms.obo's duplicated unit terms is keyed on one - so generating from such an
    /// ontology would renumber the value blocks and emit 53 identifiers twice.
    /// </summary>
    [TestMethod]
    public void Generate_RefusesAnOntologyWithNoFilename()
    {
        var obo = ObOntology.Parse(new StringReader(SAMPLE_UNIT));
        Assert.ThrowsException<ArgumentException>(() => CvidEnumGenerator.Generate(new[] { obo }));
    }

    /// <summary>
    /// Two terms escaping to one identifier across OBO files is not a case cvgen's per-OBO
    /// de-collision catches, and this artifact is committed rather than compiled on the spot,
    /// so the generator refuses instead of emitting source that cannot build.
    /// </summary>
    [TestMethod]
    public void Generate_RefusesDuplicateIdentifiers()
    {
        const string first = """
            format-version: 1.2
            remark: namespace: MS

            [Term]
            id: MS:1000001
            name: collision
            def: "First." [PSI:MS]
            """;
        const string second = """
            format-version: 1.2
            remark: namespace: MS

            [Term]
            id: MS:2000001
            name: collision
            def: "Second, same escaped identifier, different OBO." [PSI:MS]
            """;
        var a = ObOntology.Parse(new StringReader(first));
        a.Filename = "a.obo";
        var b = ObOntology.Parse(new StringReader(second));
        b.Filename = "b.obo";

        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => CvidEnumGenerator.Generate(new[] { a, b }));
        StringAssert.Contains(ex.Message, "MS_collision");
    }

    private static List<ObOntology> LoadTrackedObos()
    {
        string oboDir = Path.Combine(CppRootOrSkip(), "data", "common");
        var obos = new List<ObOntology>();
        foreach (var name in new[] { "psi-ms.obo", "unimod.obo", "unit.obo" })
        {
            string path = Path.Combine(oboDir, name);
            if (!File.Exists(path))
                Assert.Inconclusive($"{path} not present. {TREE_UNREACHABLE}");
            obos.Add(ObOntology.Load(path));
        }
        return obos;
    }

    // PwizSharpPaths walks up from the test assembly looking for the checkout and throws when
    // it is not under one, which is a skip rather than a failure - CodeInspectionTests and
    // Installer.Tests treat it the same way.
    private static string SharpRootOrSkip() => RootOrSkip(() => PwizSharpPaths.Root);

    private static string CppRootOrSkip() => RootOrSkip(() => PwizSharpPaths.CppRoot);

    private static string RootOrSkip(Func<string> root)
    {
        try
        {
            return root();
        }
        catch (InvalidOperationException)
        {
            Assert.Inconclusive(TREE_UNREACHABLE);
            throw; // unreachable: Assert.Inconclusive throws
        }
    }

    // "    NAME = VALUE," / "    NAME = OTHER_NAME," out of an enum body, cv.hpp's or ours.
    private static Dictionary<string, string> ParseEnumMembers(string source)
    {
        var members = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in s_enumMember.Matches(source))
        {
            string name = match.Groups[1].Value;
            if (name == "CVID_Unknown")
                continue;
            members[name] = match.Groups[2].Value.Trim();
        }

        // Aliases point at another identifier ("MS_TOF_TOF = MS_tandem_time_of_flight");
        // resolve them so both sides are compared as numbers.
        foreach (string name in members.Keys.ToList())
        {
            string value = members[name];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                   && members.TryGetValue(value, out string? next) && seen.Add(value))
                value = next;
            members[name] = value;
        }
        return members;
    }

    private static readonly Regex s_enumMember =
        new(@"^\s{4}([A-Za-z_][A-Za-z0-9_]*)\s*=\s*([^,\r\n]+),?\s*$", RegexOptions.Multiline);

    private const string SAMPLE_PSI_MS = """
        format-version: 1.2
        data-version: test-4.1
        remark: namespace: MS
        remark: namespace: PEFF

        [Term]
        id: MS:1000001
        name: sample number
        def: "A reference number relevant to the sample under study." [PSI:MS]
        synonym: "sample id" EXACT []
        synonym: "sample no." RELATED []

        [Term]
        id: MS:1000002
        name: tandem time-of-flight
        def: "Two TOF analyzers." [PSI:MS]
        synonym: "TOF/TOF" EXACT []
        synonym: "TOF-TOF" EXACT []

        [Term]
        id: MS:1000003
        name: LTQ Velos/ETD
        def: "Velos with ETD." [PSI:MS]
        synonym: "LTQ Velos ETD" EXACT []

        [Term]
        id: MS:1000004
        name: M+H ion
        def: "Protonated." [PSI:MS]

        [Term]
        id: MS:1000005
        name: M-H ion
        def: "Deprotonated." [PSI:MS]

        [Term]
        id: MS:1000006
        name: legacy scan
        def: "OBSOLETE Was a scan." [PSI:MS]
        is_obsolete: true

        [Term]
        id: MS:1000007
        name: µ-scan of λmax at 10°
        def: "Escapes: \"quoted\" text." [PSI:MS]

        [Term]
        id: MS:1000008
        name: (?<=[KR])(?\!P)
        def: "Trypsin regex." [PSI:MS]

        [Term]
        id: MS:1000009
        name: undefined term
        comment: Definition lives in the comment.

        [Term]
        id: PEFF:0000001
        name: PEFF CV term
        def: "PSI Extended FASTA Format controlled vocabulary term." [PSI:PEFF]
        synonym: "PEFF term" EXACT []

        [Term]
        id: NCIT:C25330
        name: Duration
        def: "The period of time during which something continues." [] {http://purl.obolibrary.org/obo/NCIT_P378="NCI"}

        [Term]
        id: UO:0000001
        name: unit copied into psi-ms
        def: "cvgen reads unit terms from unit.obo only." [PSI:MS]

        [Typedef]
        id: has_units
        name: has_units
        """;

    private const string SAMPLE_UNIT = """
        format-version: 1.2

        [Term]
        id: UO:0000001
        name: unit
        def: "A unit." [UO]
        synonym: "unit of measure" EXACT []
        """;
}
