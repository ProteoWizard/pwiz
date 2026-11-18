// Compiled into every test project by Directory.Build.targets.

namespace Pwiz.TestSupport;

/// <summary>
/// Turns off CvLookup's online CV update for the test process, so no test downloads a CV
/// because a fixture uses a term the embedded CVs lack. Tests of the update itself set
/// CvLookup.OnlineUpdate explicitly, which overrides this default.
/// </summary>
internal static class DisableCvOnlineUpdate
{
#pragma warning disable CA2255 // a test assembly is where a process-wide default belongs
    [System.Runtime.CompilerServices.ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize()
    {
        if (System.Environment.GetEnvironmentVariable("PWIZ_CV_ONLINE_UPDATE") is null)
            System.Environment.SetEnvironmentVariable("PWIZ_CV_ONLINE_UPDATE", "0");
    }
}
