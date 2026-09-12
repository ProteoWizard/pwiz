using Pwiz.Data.MsData.Readers;

namespace Pwiz.MsData.NativeAot;

/// <summary>
/// Builds the <see cref="ReaderList"/> the C API reads through, in the one place the two
/// deployment models differ.
/// </summary>
/// <remarks>
/// <para>This file is compiled by BOTH shim projects; <c>PWIZ_VENDOR_READERS</c> selects which
/// half is live:</para>
/// <list type="bullet">
/// <item><description><b>MsData.NativeAot</b> (constant undefined) — Native AOT, cross-platform
/// formats only. Vendor readers can't come along: they bind native vendor SDK assemblies that
/// ILC trims aggressively and that have no AOT-compatible form, so the AOT build deliberately
/// references no vendor project at all.</description></item>
/// <item><description><b>MsData.Hosted</b> (constant defined) — an ordinary managed library
/// loaded through nethost, which is how a native application gets vendor support.</description></item>
/// </list>
/// <para>Keeping this a compile-time switch rather than a runtime probe is what lets the AOT
/// project stay free of vendor references — a runtime check would still need the types resolvable.</para>
/// </remarks>
internal static class VendorBootstrap
{
#if PWIZ_VENDOR_READERS
    /// <summary>Hooks the vendor SDK assembly resolver, then returns the built-in readers plus
    /// every vendor reader — the same set, in the same order, that msconvert-sharp registers
    /// (see <c>Tools/Commandline/MsConvert/src/Converter.cs</c>).</summary>
    public static ReaderList CreateReaderList()
    {
        // MUST precede the first touch of any Reader_* type. The vendor SDK assemblies
        // (ThermoFisher.*, Clearcore2.*, MIDAC.*, …) are not shipped alongside us; the
        // resolver downloads and caches them on demand the first time the JIT tries to bind
        // one. Installing it after a vendor type had already been touched would leave that
        // bind to fail with a bare FileNotFoundException.
        Pwiz.Vendor.Common.VendorSdkLoader.RegisterAssemblyResolver();

        var list = Pwiz.Vendor.Thermo.ThermoReaderRegistration.CreateDefaultWithThermo();
        list.Add(new Pwiz.Vendor.Bruker.Reader_Bruker());
        list.Add(new Pwiz.Vendor.Waters.Reader_Waters());
        list.Add(new Pwiz.Vendor.Agilent.Reader_Agilent());
        list.Add(new Pwiz.Vendor.Sciex.Reader_Sciex());
        list.Add(new Pwiz.Vendor.Shimadzu.Reader_Shimadzu());
        list.Add(new Pwiz.Vendor.UNIFI.Reader_UNIFI());
        list.Add(new Pwiz.Vendor.UIMF.Reader_UIMF());
        list.Add(new Pwiz.Vendor.Mobilion.Reader_Mobilion());
        return list;
    }
#else
    /// <summary>Returns the built-in cross-platform readers (mzML, mzXML, mzMLb, mz5, MGF,
    /// MS1/MS2, Bruker BTDX). No vendor formats — see the remarks on this class.</summary>
    public static ReaderList CreateReaderList() => ReaderList.Default;
#endif
}
