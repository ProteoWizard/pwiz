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

#if NET472
using System;
using System.Runtime.InteropServices;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Correctly-rounded decimal-to-double conversion for the .NET Framework
    /// build, via the C runtime's <c>strtod</c>.
    ///
    /// WHY: .NET Framework's string-to-double conversion is not correctly rounded
    /// for some decimals. "0.86653405" parses to 0x3FEBBAA59DB3DA8E, one ULP above
    /// the correctly rounded 0x3FEBBAA59DB3DA8D that .NET Core 3.0+ and C++
    /// <c>strtod</c> both produce. <c>XmlConvert.ToDouble</c> inherits the defect
    /// despite XML Schema requiring IEEE-correct behavior, so it is not a way out.
    /// Two of 161,099 retention times in one Thermo DIA run were affected, which is
    /// enough to break bit-exact comparison of a <c>.spectra.bin</c> written by the
    /// hand-written mzML reader against one written through ProteoWizard
    /// (issue #4496).
    ///
    /// Going through the CRT is not merely "a" fix, it is the one that matches by
    /// construction: ProteoWizard's own parser
    /// (<c>pwiz/utility/misc/optimized_lexical_cast.hpp</c>) is <c>STRTOD</c>, so
    /// net472 Osprey, net8.0 Osprey and pwiz all land on the same bits.
    ///
    /// net472 only, and deliberately so. .NET 8 needs nothing here, and the
    /// hand-written mzML reader is expected to retire with the .NET 8 port, so this
    /// interop retires with it. Being Windows-specific costs nothing: the net472
    /// configuration is Windows (or Wine) only already, as is the msconvert that
    /// produces the files it reads.
    /// </summary>
    internal static class NativeStrtod
    {
        // ucrtbase.dll is the Universal CRT that MSVC 14.x links, i.e. the same
        // implementation behind pwiz_data_cli's own conversions. Present on every
        // supported Windows and under Wine. The single PInvoke declaration for
        // Osprey lives here, per the "isolate PInvoke in one place" rule.
        // _strtod_l rather than strtod: plain strtod follows the process-global UCRT
        // locale, so a third-party DLL calling setlocale to a comma-decimal locale
        // would make "17.0286203070330" parse as 17 with 2 characters consumed. That
        // fails the whole-string check and returns 0.0 for every retention time in the
        // file. XmlConvert.ToDouble was culture-invariant by contract and this has to
        // be too. Verified under de-DE: plain strtod stops at the '.', _strtod_l with
        // the C locale returns the full value.
        [DllImport(@"ucrtbase.dll", EntryPoint = "_strtod_l", CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, SetLastError = false)]
        private static extern double StrtodL(IntPtr text, out IntPtr endPtr, IntPtr locale);

        // _create_locale(LC_ALL = 0, "C") - an invariant locale handle, created once.
        [DllImport(@"ucrtbase.dll", EntryPoint = "_create_locale", CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true, SetLastError = false)]
        private static extern IntPtr CreateLocale(int category, string locale);

        private static readonly IntPtr C_LOCALE = CreateLocale(0, @"C");

        /// <summary>
        /// Parse an XML decimal value. Returns false unless the WHOLE string was
        /// consumed, so trailing garbage is a failure rather than a silently
        /// truncated number - matching the contract the previous
        /// XmlConvert-plus-catch had.
        /// </summary>
        internal static bool TryParse(string text, out double value)
        {
            value = 0.0;
            if (string.IsNullOrEmpty(text))
                return false;

            // Trim so a trailing newline or padding in the attribute does not read
            // as unconsumed input. strtod already skips leading whitespace.
            string trimmed = text.Trim();
            if (trimmed.Length == 0)
                return false;

            // Hex float forms ("0x1.8p3" -> 12) are accepted by the CRT and rejected by
            // XML Schema. No value-level check can catch them: they parse to ordinary
            // finite numbers, unlike the nan/inf spellings caught below.
            if (trimmed.IndexOf('x') >= 0 || trimmed.IndexOf('X') >= 0)
                return false;

            // Marshal explicitly rather than letting the string marshaller do it:
            // validating "was everything consumed" needs the start pointer to
            // compare endPtr against, which the automatic marshaller does not
            // expose. The values are ASCII (XML numeric text), so byte length and
            // char length agree.
            IntPtr buffer = Marshal.StringToHGlobalAnsi(trimmed);
            try
            {
                double parsed = StrtodL(buffer, out IntPtr endPtr, C_LOCALE);
                long consumed = endPtr.ToInt64() - buffer.ToInt64();
                if (consumed <= 0 || consumed != trimmed.Length)
                    return false;
                // Overflow returns +/-HUGE_VAL; the old XmlConvert path threw and
                // reported failure, and no mzML value is legitimately infinite.
                //
                // NaN matters more than it looks. The CRT accepts spellings XML Schema
                // does not - "nan", "NaN", "-nan(ind)", and hex floats like "0x1.8p3" -
                // and XmlConvert.ToDouble threw on all of them. A NaN that got through
                // would defeat every downstream guard rather than trip one, because
                // both "NaN > 0" and "NaN <= 0" are false: SpectrumBuilder's
                // isolation-window fail-fast would pass it straight through and cache
                // an IsolationWindow(NaN) that silently matches no precursor at all.
                if (double.IsInfinity(parsed) || double.IsNaN(parsed))
                    return false;
                value = parsed;
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
#endif
