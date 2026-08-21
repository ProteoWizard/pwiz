using Pwiz.Data.MsData.Readers;

namespace Pwiz.Vendor.Thermo;

/// <summary>
/// Helpers that plug the Thermo reader into the format-detection dispatcher.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Pwiz.Data.MsData.Readers.ReaderList"/> lives in <c>Pwiz.Data.MsData</c>, which
/// deliberately doesn't reference vendor projects — that would pull all the encrypted vendor DLLs
/// into every consumer of the core data model. Instead, tools that want vendor-format support
/// call <see cref="AddTo"/> or <see cref="CreateDefaultWithThermo"/> at startup.
/// </para>
/// </remarks>
public static class ThermoReaderRegistration
{
    /// <summary>Appends a <see cref="Reader_Thermo"/> to <paramref name="list"/>.</summary>
    public static void AddTo(ReaderList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        list.Add(new Reader_Thermo());
    }

    /// <summary>Returns <see cref="ReaderList.Default"/> with the Thermo reader appended.</summary>
    public static ReaderList CreateDefaultWithThermo()
    {
        var list = ReaderList.Default;
        AddTo(list);
        return list;
    }
}
