using System.Reflection;

namespace Pwiz.Vendor.Sciex.Tests;

/// <summary>
/// Hard-caps the SCIEX Clearcore2 SDK's log4net output to <c>OFF</c> before any test runs.
/// The SDK ships log4net wired to a default ConsoleAppender and emits ~30 IoC-registration
/// lines per <c>CreateSampleDataApi()</c> call plus 5–10 lines per API request. When TC runs
/// <c>dotnet test</c> for several assemblies in parallel against a shared stdout, those
/// <c>[INFO]</c>/<c>[DEBUG]</c> lines collide with <c>##teamcity[testFinished ...]</c>
/// service messages — TC drops the malformed messages and the affected tests silently
/// disappear from the count (observed: builds 3974560/3974563/3974564, same commit, totals
/// 221/238/191 driven by log-flood-induced corruption).
/// </summary>
/// <remarks>
/// We reach the SDK's log4net via reflection rather than a direct package reference because
/// log4net is an internal dependency of the SDK; it's not exposed in pwiz-sharp's deps.json.
/// The SDK assemblies load log4net at first use, and reflecting against
/// <c>Assembly.Load("log4net")</c> finds whichever instance they loaded.
/// </remarks>
[TestClass]
public static class SilenceSciexSdkLogging
{
    [AssemblyInitialize]
    public static void Init(TestContext _)
    {
        try
        {
            // log4net is loaded by the SDK on first IoC bootstrap. Force-load here so
            // GetRepository/Threshold resolve against the same instance the SDK will use.
            var log4netAssembly = Assembly.Load("log4net");
            var logManager = log4netAssembly.GetType("log4net.LogManager")
                ?? throw new InvalidOperationException("log4net.LogManager not found");
            var levelType = log4netAssembly.GetType("log4net.Core.Level")
                ?? throw new InvalidOperationException("log4net.Core.Level not found");
            var off = levelType.GetField("Off", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                ?? throw new InvalidOperationException("log4net.Core.Level.Off not found");

            // GetRepository() with no args → default repository (the one the SDK uses).
            var getRepository = logManager.GetMethod("GetRepository", Type.EmptyTypes)
                ?? throw new InvalidOperationException("log4net.LogManager.GetRepository() not found");
            var repository = getRepository.Invoke(null, null)
                ?? throw new InvalidOperationException("default log4net repository was null");

            // Threshold acts as a hard cutoff applied AFTER each logger's own level decision —
            // setting it to Off turns every appender into a no-op without traversing the
            // logger hierarchy.
            var thresholdProperty = repository.GetType().GetProperty("Threshold")
                ?? throw new InvalidOperationException("log4net Threshold property not found");
            thresholdProperty.SetValue(repository, off);
        }
        catch (FileNotFoundException)
        {
            // No log4net loaded (build without --i-agree-to-the-vendor-licenses leaves the
            // Wiff2 plugin out, so the SDK never gets a chance to pull log4net in). Tests
            // that need the SDK will surface VendorSupportNotEnabledException; we don't need
            // to silence anything.
        }
        catch (Exception ex)
        {
            // Log but don't fail the assembly setup — silencing the SDK is best-effort.
            // If the reflection breaks (log4net major-version bump etc.), tests still run;
            // the consequence is just the noisy CI output we had before.
            Console.WriteLine($"[Sciex.Tests] failed to silence SDK log4net: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
