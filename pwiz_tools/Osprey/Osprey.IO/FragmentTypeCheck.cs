/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Compares the fragment ion types a library STATES (blib annotation rows, DIA-NN TSV
    /// columns) with the types Osprey computes from m/z (<see cref="FragmentTyping"/>), peak by
    /// peak, so a library whose statements and Osprey's typing part ways is reported rather than
    /// silently searched one way or the other.
    ///
    /// <list type="bullet">
    /// <item><b>Agree</b>: the library names a primary b or y ion (<see cref="FragmentTyping.IsPrimary"/>)
    /// for the peak and Osprey computes one of the ions it names. A library may name several
    /// candidates for one peak, as NIST does.</item>
    /// <item><b>Isobaric</b>: the library names one of two or more ions at one m/z - <c>b2</c> and
    /// <c>b4^2</c> of <c>IQQLTEEIGR</c> - which Osprey cannot tell apart and leaves untyped. Not a
    /// disagreement: the library chose where Osprey declines to guess.</item>
    /// <item><b>Differ</b>: the library names a primary b or y ion and Osprey computes none of
    /// them - another ion, or no ion within the search tolerance.</item>
    /// <item><b>Outside the model</b>: the library types the peak only as something typing does
    /// not produce - a neutral loss, an a/c/x/z ion, a fragment charge above 2 - or states it
    /// unreadably. Reported, not a disagreement.</item>
    /// </list>
    /// Peaks the library states nothing about are not counted. Counted from parallel loads, so
    /// the counts are updated atomically.
    /// </summary>
    public sealed class FragmentTypeCheck
    {
        /// <summary>The differing peaks kept as examples for <c>--verbose</c>.</summary>
        public const int MAX_EXAMPLES = 10;

        private readonly List<string> _examples = new List<string>();
        private long _agree;
        private long _isobaric;
        private long _differ;
        private long _outside;

        public long Agree => Interlocked.Read(ref _agree);
        public long Isobaric => Interlocked.Read(ref _isobaric);
        public long Differ => Interlocked.Read(ref _differ);
        public long Outside => Interlocked.Read(ref _outside);

        /// <summary>Peaks the library types as a primary b or y ion - the denominator.</summary>
        public long StatedPrimary => Agree + Isobaric + Differ;

        /// <summary>Whether the library stated a type for any peak.</summary>
        public bool AnyStated => StatedPrimary + Outside > 0;

        /// <summary>
        /// Count one peak: <paramref name="stated"/> is every type the library gives it that
        /// could be read; <paramref name="unreadable"/> that it also gave one that could not.
        /// <paramref name="computed"/> is what Osprey computes for it.
        /// </summary>
        public void AddPeak(IReadOnlyList<FragmentAnnotation> stated, bool unreadable, ComputedType computed,
            string sequence, int precursorCharge, double peakMz)
        {
            bool anyPrimary = false;
            bool agrees = false;
            bool isobaric = false;
            foreach (var annotation in stated)
            {
                if (!FragmentTyping.IsPrimary(annotation))
                    continue;
                anyPrimary = true;
                if (computed.Ion.HasValue && IsSameIon(annotation, computed.Ion.Value))
                    agrees = true;
                else if (computed.TiedIons != null && computed.TiedIons.Any(ion => IsSameIon(annotation, ion)))
                    isobaric = true;
            }
            if (!anyPrimary)
            {
                if (stated.Count > 0 || unreadable)
                    Interlocked.Increment(ref _outside);
                return;
            }
            if (agrees)
            {
                Interlocked.Increment(ref _agree);
                return;
            }
            if (isobaric)
            {
                Interlocked.Increment(ref _isobaric);
                return;
            }
            Interlocked.Increment(ref _differ);
            lock (_examples)
            {
                if (_examples.Count < MAX_EXAMPLES)
                {
                    _examples.Add(string.Format(OspreyIOResources.FragmentTypeCheck_AddPeak__0___charge__1___peak_m_z__2_F4___the_library_says__3___Osprey_computes__4_,
                        sequence, precursorCharge, peakMz, FormatIons(stated),
                        computed.Ion.HasValue ? FormatIon(computed.Ion.Value) : OspreyIOResources.FragmentTypeCheck_AddPeak_no_ion));
                }
            }
        }

        /// <summary>The first <see cref="MAX_EXAMPLES"/> differing peaks, for <c>--verbose</c>.</summary>
        public IReadOnlyList<string> Examples
        {
            get
            {
                lock (_examples)
                {
                    return _examples.ToArray();
                }
            }
        }

        /// <summary>The one line reporting the comparison for the library <paramref name="libraryName"/>.</summary>
        public string Summary(string libraryName)
        {
            return string.Format(OspreyIOResources.FragmentTypeCheck_Summary_Fragment_types_in__0___Osprey_s_computed_types_agree_with__1_N0__of__2_N0__peaks,
                libraryName, Agree, StatedPrimary, Differ, Outside, Isobaric);
        }

        /// <summary>
        /// Log the comparison: a warning when any peak differs, else a line of information; the
        /// examples too when <paramref name="verbose"/>. Nothing when the library stated no type.
        /// </summary>
        public void Report(string libraryName, bool verbose, Action<string> logInfo, Action<string> logWarning)
        {
            if (!AnyStated)
                return;
            if (Differ > 0)
                logWarning(Summary(libraryName));
            else
                logInfo(Summary(libraryName));
            if (!verbose)
                return;
            foreach (string example in Examples)
                logInfo(example);
        }

        /// <summary>An ion in the notation libraries use: <c>y6</c>, <c>b3^2</c>, <c>y7-H2O</c>.</summary>
        public static string FormatIon(FragmentAnnotation annotation)
        {
            string name = char.ToLowerInvariant(annotation.IonType.ToString()[0]) +
                          annotation.Ordinal.ToString(CultureInfo.InvariantCulture);
            if (annotation.Charge > 1)
                name += @"^" + annotation.Charge.ToString(CultureInfo.InvariantCulture);
            switch (annotation.NeutralLoss)
            {
                case NeutralLossCode.None:
                    return name;
                case NeutralLossCode.Custom:
                    return name + @"-" + annotation.CustomLossMass.ToString(@"0.0###", CultureInfo.InvariantCulture);
                default:
                    return name + @"-" + annotation.NeutralLoss;
            }
        }

        private static string FormatIons(IReadOnlyList<FragmentAnnotation> annotations)
        {
            var names = new string[annotations.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = FormatIon(annotations[i]);
            return string.Join(@",", names);
        }

        private static bool IsSameIon(FragmentAnnotation a, FragmentAnnotation b)
        {
            return a.IonType == b.IonType && a.Ordinal == b.Ordinal && a.Charge == b.Charge;
        }
    }
}
