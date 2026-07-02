// Vendor reader registration for the pwiz-sharp sandbox. Runs on ASSEMBLY LOAD
// via [ModuleInitializer] — before any static field init in MsDataFileImpl.cs.
// Legacy pwiz.CLI's `ReaderList.FullReaderList` was populated by the C++/CLI
// side; pwiz-sharp's ReaderList.Default only has the built-in mzML / mzMLb /
// mzXML / MGF / MSn / BTDX readers, so vendor readers must be appended to
// AdditionalReaders. Timing matters: MsDataFileImpl.cs has a
// `private static readonly ReaderList FULL_READER_LIST = ReaderList.Default;`
// field that snapshots the reader list; if vendors aren't in
// AdditionalReaders at that snapshot time, they never appear in
// FULL_READER_LIST. Module initializers fire before any field init in the
// module, so this is the correct hook.
using Pwiz.Data.MsData.Readers;

namespace pwiz.ProteowizardWrapper
{
    internal static class VendorReaderRegistration
    {
        [System.Runtime.CompilerServices.ModuleInitializer]
        internal static void RegisterVendorReaders()
        {
            var extra = ReaderList.AdditionalReaders;
            void TryAdd(System.Func<IReader> factory, string name)
            {
                try { extra.Add(factory()); }
                catch (System.Exception e)
                {
                    // Vendor-support conditional compile can leave a reader in a state that
                    // throws on ctor. Silently skip so the other vendors still register.
                    System.Diagnostics.Debug.WriteLine($"  ! {name}: {e.Message}");
                }
            }
            TryAdd(() => new Pwiz.Vendor.Thermo.Reader_Thermo(), "Thermo");
            TryAdd(() => new Pwiz.Vendor.Waters.Reader_Waters(), "Waters");
            TryAdd(() => new Pwiz.Vendor.Sciex.Reader_Sciex(), "Sciex");
            TryAdd(() => new Pwiz.Vendor.Shimadzu.Reader_Shimadzu(), "Shimadzu");
            TryAdd(() => new Pwiz.Vendor.Agilent.Reader_Agilent(), "Agilent");
            TryAdd(() => new Pwiz.Vendor.Bruker.Reader_Bruker(), "Bruker");
            TryAdd(() => new Pwiz.Vendor.UIMF.Reader_UIMF(), "UIMF");
        }
    }
}
