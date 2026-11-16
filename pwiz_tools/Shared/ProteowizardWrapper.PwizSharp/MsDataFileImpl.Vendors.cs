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
using System.Collections.Generic;
using Pwiz.Data.MsData.Readers;

namespace pwiz.ProteowizardWrapper
{
    public static class VendorReaderRegistration
    {
        private static readonly List<string> FAILURES = new List<string>();

        /// <summary>
        /// Vendor readers that could not be registered, as "<c>name: reason</c>". Empty in a
        /// healthy build. Exposed rather than logged here because this assembly has no
        /// opinion about where a host writes diagnostics, and the failure MUST reach the
        /// user somehow: it surfaces much later as ProteoWizard's "No registered reader
        /// recognized the file", which blames the FORMAT for what was a BUILD problem.
        /// </summary>
        public static IReadOnlyList<string> Failures
        {
            get { return FAILURES; }
        }

        // CA2255 warns off module initializers in libraries because callers cannot control when they
        // run. That is exactly the property being relied on here: registration has to happen before
        // MsDataFileImpl's static ReaderList snapshot, and no caller-invoked hook runs early enough.
#pragma warning disable CA2255
        [System.Runtime.CompilerServices.ModuleInitializer]
#pragma warning restore CA2255
        internal static void RegisterVendorReaders()
        {
            var extra = ReaderList.AdditionalReaders;
            void TryAdd(System.Func<IReader> factory, string name)
            {
                try { extra.Add(factory()); }
                catch (System.Exception e)
                {
                    // Vendor-support conditional compile can leave a reader in a state that
                    // throws on ctor. Skip it so the other vendors still register, but
                    // RECORD it - Debug.WriteLine is [Conditional("DEBUG")], so in a
                    // Release build this was an empty catch and the failure left no trace
                    // in any shipping path.
                    FAILURES.Add($"{name}: {e.Message}");
                }
            }
            TryAdd(() => new Pwiz.Vendor.Thermo.Reader_Thermo(), "Thermo");
            TryAdd(() => new Pwiz.Vendor.Waters.Reader_Waters(), "Waters");
            TryAdd(() => new Pwiz.Vendor.Sciex.Reader_Sciex(), "Sciex");
            TryAdd(() => new Pwiz.Vendor.Shimadzu.Reader_Shimadzu(), "Shimadzu");
            TryAdd(() => new Pwiz.Vendor.Agilent.Reader_Agilent(), "Agilent");
            TryAdd(() => new Pwiz.Vendor.Bruker.Reader_Bruker(), "Bruker");
            TryAdd(() => new Pwiz.Vendor.UIMF.Reader_UIMF(), "UIMF");
            TryAdd(() => new Pwiz.Vendor.UNIFI.Reader_UNIFI(), "UNIFI");
            TryAdd(() => new Pwiz.Vendor.Mobilion.Reader_Mobilion(), "Mobilion");
        }
    }
}
