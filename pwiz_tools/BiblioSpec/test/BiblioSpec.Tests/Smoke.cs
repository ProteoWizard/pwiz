namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// Smoke test confirming the harness compiles + runs in <c>dotnet test</c>. The
/// real per-reader and per-tool tests will go in other files once the BiblioSpec
/// readers + tool exes are ported.
/// </summary>
[TestClass]
public class Smoke
{
    /// <summary>Injected by MSTest.</summary>
    public TestContext? TestContext { get; set; }

    /// <summary>
    /// Confirm <see cref="GoldenFileFixture"/> can locate the cpp test inputs
    /// and reference dirs. The expected sibling-pwiz layout is present in our
    /// standard dev checkout; environments without a cpp tree get an
    /// Inconclusive skip rather than a failure.
    /// </summary>
    [TestMethod]
    public void Harness_LocatesFixtureRoot()
    {
        var fixture = GoldenFileFixture.Instance;
        if (fixture is null)
        {
            Assert.Inconclusive(
                "No sibling pwiz checkout was found exposing pwiz_tools/BiblioSpec/tests/. " +
                "This is expected on environments without the cpp tree; the test project " +
                "still compiled and the runner picked it up.");
            return;
        }
        Assert.IsTrue(Directory.Exists(fixture.InputsDir),
            $"inputs dir reported but missing: {fixture.InputsDir}");
        Assert.IsTrue(Directory.Exists(fixture.ReferenceDir),
            $"reference dir reported but missing: {fixture.ReferenceDir}");
        Assert.IsTrue(Directory.Exists(fixture.OutputDir),
            $"output dir not created: {fixture.OutputDir}");
        TestContext?.WriteLine($"inputs    = {fixture.InputsDir}");
        TestContext?.WriteLine($"reference = {fixture.ReferenceDir}");
        TestContext?.WriteLine($"output    = {fixture.OutputDir}");
    }
}
