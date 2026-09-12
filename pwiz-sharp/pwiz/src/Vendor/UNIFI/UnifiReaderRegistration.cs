using Pwiz.Data.MsData.Readers;

namespace Pwiz.Vendor.UNIFI;

/// <summary>
/// Helpers that plug the UNIFI / waters_connect reader into the format-detection dispatcher.
/// </summary>
public static class UnifiReaderRegistration
{
    /// <summary>Appends a <see cref="Reader_UNIFI"/> to <paramref name="list"/>.</summary>
    public static void AddTo(ReaderList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        list.Add(new Reader_UNIFI());
    }
}
