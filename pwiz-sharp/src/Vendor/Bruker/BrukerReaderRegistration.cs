using Pwiz.Data.MsData.Readers;

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// Helpers that plug the Bruker reader into the format-detection dispatcher.
/// </summary>
/// <remarks>
/// Callers invoke this at startup (msconvert-sharp's <c>Converter</c>) rather than having the
/// core <c>Pwiz.Data.MsData</c> assembly reference the vendor project — otherwise every
/// consumer of the core data model would drag in the native <c>timsdata.dll</c>.
/// </remarks>
public static class BrukerReaderRegistration
{
    /// <summary>Appends a <see cref="Reader_Bruker"/> to <paramref name="list"/>.</summary>
    public static void AddTo(ReaderList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        list.Add(new Reader_Bruker());
    }
}
