namespace Pwiz.Data.MsData;

/// <summary>
/// Version string for the pwiz-sharp MSData assembly. Port of pwiz.CLI's
/// <c>pwiz.CLI.msdata.Version.ToString()</c>, used by consumers that want to
/// display the underlying pwiz version.
/// </summary>
public static class ProteoWizardVersion
{
    /// <summary>Returns the assembly informational version, e.g. "3.0.24000".</summary>
    public new static string ToString() =>
        typeof(MSData).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}
