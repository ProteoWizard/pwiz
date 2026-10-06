using Pwiz.Build.CvGen;
using Pwiz.Data.Common.Obo;

// Regenerates pwiz-sharp/pwiz/src/Common/CVID.generated.cs from the OBO files. Port of
// pwiz cpp cvgen (pwiz/data/common/cvgen.cpp), which the build used to run on the same
// files to produce cv.hpp, from which generate_cvid.py then copied the enum.
//
// Usage: CvGen [--output <file>] [<obo> ...]
//
// With no OBO arguments the three ontologies pwiz ships are used, in the order cvgen took
// them (the order numbers the value blocks, so it is not a free choice):
//   <repo>/pwiz/data/common/psi-ms.obo, unimod.obo, unit.obo
// and the output defaults to <repo>/pwiz-sharp/pwiz/src/Common/CVID.generated.cs, where
// <repo> is found by walking up from this executable. Common.Tests fails when that file is
// stale, so an OBO update is: replace the .obo, run this, commit both.
//
// New OBO files come from the URIs the mzML cvList cites (CvLookup.GetCv):
//   psi-ms.obo  http://purl.obolibrary.org/obo/ms/psi-ms.obo
//   unimod.obo  http://www.unimod.org/obo/unimod.obo
//   unit.obo    http://purl.obolibrary.org/obo/uo.obo

const string usage = "Usage: CvGen [--output <file>] [<obo> ...]";

string? output = null;
var oboPaths = new List<string>();
for (int i = 0; i < args.Length; ++i)
{
    if (args[i] is "-h" or "--help" or "/?")
    {
        Console.WriteLine(usage);
        return 0;
    }
    if (args[i] == "--output")
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine($"CvGen: --output needs a file path.\n{usage}");
            return 2;
        }
        output = args[++i];
        continue;
    }
    if (args[i].StartsWith('-'))
    {
        Console.Error.WriteLine($"CvGen: unrecognized option '{args[i]}'.\n{usage}");
        return 2;
    }
    oboPaths.Add(args[i]);
}

// Writing a partial enum over the tracked file deletes thousands of members and still exits
// 0, so a caller naming its own OBOs has to name its own output too.
if (oboPaths.Count != 0 && output is null)
{
    Console.Error.WriteLine($"CvGen: --output is required when the OBO files are given explicitly, so a partial list cannot overwrite the tracked CVID.generated.cs.\n{usage}");
    return 2;
}

// Only the two defaults below need the repository, so a fully-explicit invocation - from a
// publish folder, a CI staging dir, a copied tool drop - never has to be under one.
string repoRoot = oboPaths.Count == 0 || output is null ? FindRepoRoot() : "";
if (oboPaths.Count == 0)
{
    foreach (var name in new[] { "psi-ms.obo", "unimod.obo", "unit.obo" })
        oboPaths.Add(Path.Combine(repoRoot, "pwiz", "data", "common", name));
}
output ??= Path.Combine(repoRoot, "pwiz-sharp", "pwiz", "src", "Common", "CVID.generated.cs");

var obos = new List<ObOntology>();
foreach (var path in oboPaths)
{
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"CvGen: no such OBO file: {path}");
        return 1;
    }
    obos.Add(ObOntology.Load(path));
}

string source = CvidEnumGenerator.Generate(obos);
string? outputDir = Path.GetDirectoryName(Path.GetFullPath(output));
if (!string.IsNullOrEmpty(outputDir))
    Directory.CreateDirectory(outputDir);
File.WriteAllText(output, source);
Console.WriteLine($"{source.Count(c => c == '\n')} {output}");
return 0;

// The tool runs from pwiz-sharp/build/CvGen/bin/<cfg>/<tfm>/, so the repo root is the
// first ancestor that holds pwiz-sharp/Pwiz.sln.
static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (; dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "pwiz-sharp", "Pwiz.sln")))
            return dir.FullName;
    }
    throw new InvalidOperationException(
        $"CvGen: cannot find the repository root (pwiz-sharp/Pwiz.sln) above {AppContext.BaseDirectory}; pass the OBO paths and --output explicitly.");
}
