using Pwiz.Data.MsData.Readers;

namespace Pwiz.Vendor.Shimadzu;

/// <summary>
/// Helpers that plug the Shimadzu reader into the format-detection dispatcher.
/// </summary>
/// <remarks>
/// Callers invoke this at startup (msconvert-sharp's <c>Converter</c>) rather than having the
/// core <c>Pwiz.Data.MsData</c> assembly reference the vendor project — otherwise every
/// consumer of the core data model would drag in the Shimadzu LabSolutions IO assemblies.
/// </remarks>
public static class ShimadzuReaderRegistration
{
    /// <summary>Appends a <see cref="Reader_Shimadzu"/> to <paramref name="list"/>.</summary>
    public static void AddTo(ReaderList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        list.Add(new Reader_Shimadzu());
    }
}
