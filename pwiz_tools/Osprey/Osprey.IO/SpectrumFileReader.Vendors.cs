/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using Pwiz.Data.MsData.Readers;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Vendor reader registration. <c>ReaderList.Default</c> carries only the built-in
    /// format readers (mzML, mzMLb, mz5, mzXML, MGF, MSn, BTDX); the vendor readers live
    /// in separate assemblies so that <c>Pwiz.Data.MsData</c> does not drag the vendor
    /// SDKs into every consumer, and each host appends the ones it wants to
    /// <c>ReaderList.AdditionalReaders</c>. Same set
    /// <c>ProteowizardWrapper</c> registers for Skyline, so a file Skyline can open is a
    /// file Osprey can open.
    ///
    /// No opt-in property on this side: pwiz-sharp gates the SDKs itself
    /// (<c>IAgreeToVendorLicenses</c>, and <c>NativeVendorsAvailable</c> = that AND
    /// Windows). A vendor project built without them still compiles - <c>Identify()</c>
    /// works and <c>Read()</c> throws - which is what lets one Osprey build run on Linux
    /// with only the managed Thermo SDK behind it.
    /// </summary>
    public static partial class SpectrumFileReader
    {
        private static void RegisterVendorReaders()
        {
            TryAddReader(() => new Pwiz.Vendor.Thermo.Reader_Thermo(), @"Thermo");
            TryAddReader(() => new Pwiz.Vendor.Waters.Reader_Waters(), @"Waters");
            TryAddReader(() => new Pwiz.Vendor.Sciex.Reader_Sciex(), @"Sciex");
            TryAddReader(() => new Pwiz.Vendor.Shimadzu.Reader_Shimadzu(), @"Shimadzu");
            TryAddReader(() => new Pwiz.Vendor.Agilent.Reader_Agilent(), @"Agilent");
            TryAddReader(() => new Pwiz.Vendor.Bruker.Reader_Bruker(), @"Bruker");
            TryAddReader(() => new Pwiz.Vendor.UIMF.Reader_UIMF(), @"UIMF");
            TryAddReader(() => new Pwiz.Vendor.UNIFI.Reader_UNIFI(), @"UNIFI");
        }

        private static void TryAddReader(Func<IReader> factory, string name)
        {
            try
            {
                ReaderList.AdditionalReaders.Add(factory());
            }
            catch (Exception ex)
            {
                // A vendor-support conditional compile can leave a reader in a state that
                // throws from its constructor. Skip it so the other vendors still
                // register.
                //
                // Reported through OspreyOutput, NOT Debug.WriteLine: that is
                // [Conditional("DEBUG")], so in the Release build every shipping path
                // produces it compiles away to an empty catch. The failure then surfaces
                // much later as ProteoWizard's "No registered reader recognized the file"
                // - a message about the FORMAT when the cause was the BUILD, with no way
                // to tell the two apart.
                OspreyOutput.Out.WriteLine(
                    $@"[warn] vendor reader {name} not registered: {ex.Message}");
            }
        }
    }
}
