using System.Reflection;

namespace Pwiz.Vendor.Sciex.Tests;

/// <summary>
/// Hard-caps SCIEX Clearcore2 SDK <b>log4net</b> output to <c>OFF</c> before any test runs.
/// Pairs with <c>OFX.Logging.dll</c> (built from the OfxLoggingStub project): together they
/// silence the two independent logging frameworks the SDK uses.
/// <list type="bullet">
///   <item><b>log4net</b> — used by parts of Clearcore2 / wiff2 RFLight. Silenced here by
///   reflecting against <c>log4net.LogManager</c> and setting the default repository's
///   threshold to <c>Off</c>.</item>
///   <item><b>OFX</b> — falls back to a noisy <c>DefaultLogManager</c> when it can't load
///   <c>OFX.Logging.LogManager,OFX.Logging</c>. The <c>OfxLoggingStub</c> project ships a
///   no-op <c>OFX.Logging.dll</c> so OFX uses our silent log manager. <c>Console.SetOut</c>
///   doesn't help — OFX's DefaultLogManager bypasses <c>Console.Out</c> by holding a direct
///   reference to the underlying stdout stream — which is why the stub-DLL approach is the
///   one that actually works.</item>
/// </list>
/// Both flooded stdout enough that <c>##teamcity[testFinished ...]</c> service messages
/// got truncated/corrupted in TC, dropping tests from the count. Builds 3974560 / 3974563
/// / 3974564 hit the same commit with totals 221 / 238 / 191.
/// </summary>
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
