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
using System.Text;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// One <c>RefSpectraPeakAnnotations</c> row: the peak it names, in the stored peak order.
    /// </summary>
    public struct BlibPeakAnnotation
    {
        public int PeakIndex;
        public string Name;
        public int Charge;
        public double MzTheoretical;
        public double MzObserved;
    }

    /// <summary>
    /// A library precursor in the form a blib stores it: the <c>RefSpectra</c> identity, the
    /// compressed peaks, the <c>Modifications</c>, protein and <c>RefSpectraPeakAnnotations</c>
    /// rows. The one place a <see cref="LibraryEntry"/> becomes blib rows, shared by the search
    /// output and <see cref="LibraryBlibWriter"/>; <see cref="BlibWriter.AddSpectrum(BlibSpectrum, double, double, double, double, long, int)"/>
    /// writes it. Built from the entry alone, so it can be prepared on any thread while the
    /// SQLite inserts stay on one.
    ///
    /// <list type="bullet">
    /// <item>Peaks are sorted by m/z, as BiblioSpec stores them and Skyline expects.</item>
    /// <item>The modified sequence is built from <see cref="LibraryEntry.Modifications"/>, never
    /// from the library's own text: one signed four-decimal mass after each modified residue
    /// (<c>AC[+57.0215]K</c>), the form Skyline matches by mass. An N-terminal modification sits
    /// on the first residue, as BiblioSpec writes it, and modifications at one position are
    /// summed, in the text and in the <c>Modifications</c> rows alike.</item>
    /// <item>Each b or y ion whose m/z, recomputed from the sequence and modifications, names its
    /// peak gets an annotation row in the grammar <see cref="BlibPeakAnnotations"/> reads: the
    /// rows that let <see cref="BlibLoader"/> build real decoys from the blib. Other ion types,
    /// and peaks with no fragment number, are left unannotated: the reader could not check
    /// them.</item>
    /// </list>
    /// </summary>
    public sealed class BlibSpectrum
    {
        private BlibSpectrum(LibraryEntry entry, string modifiedSequence, IReadOnlyList<Modification> modifications,
            byte[] mzBlob, byte[] intensityBlob, int numPeaks, IReadOnlyList<BlibPeakAnnotation> annotations)
        {
            PeptideSeq = entry.Sequence;
            ModifiedSequence = modifiedSequence;
            PrecursorMz = entry.PrecursorMz;
            Charge = entry.Charge;
            Modifications = modifications;
            ProteinIds = entry.ProteinIds ?? Array.Empty<string>();
            MzBlob = mzBlob;
            IntensityBlob = intensityBlob;
            NumPeaks = numPeaks;
            Annotations = annotations;
        }

        /// <summary>
        /// The blib form of <paramref name="entry"/>. <paramref name="annotate"/> false leaves out
        /// the ion annotations, which is how a library without them is made for testing.
        /// </summary>
        public static BlibSpectrum FromLibraryEntry(LibraryEntry entry, bool annotate = true)
        {
            var fragments = entry.Fragments.OrderBy(f => f.Mz).ToArray();
            var mzs = new double[fragments.Length];
            var intensities = new float[fragments.Length];
            for (int i = 0; i < fragments.Length; i++)
            {
                mzs[i] = fragments[i].Mz;
                intensities[i] = fragments[i].RelativeIntensity;
            }
            var modMasses = PeptideFragmentMass.ModMassesByPosition(entry.Modifications);
            var byResidue = SumByResidue(modMasses, entry.Sequence.Length);
            return new BlibSpectrum(entry,
                FormatModifiedSequence(entry.Sequence, byResidue),
                byResidue.Select(pair => new Modification { Position = pair.Key, MassDelta = pair.Value }).ToArray(),
                BlibWriter.CompressMzs(mzs), BlibWriter.CompressIntensities(intensities), fragments.Length,
                annotate ? Annotate(entry.Sequence, modMasses, fragments) : Array.Empty<BlibPeakAnnotation>());
        }

        public string PeptideSeq { get; }
        public string ModifiedSequence { get; }
        public double PrecursorMz { get; }
        public int Charge { get; }

        /// <summary>One per modified residue, 0-based, with the modifications there summed.</summary>
        public IReadOnlyList<Modification> Modifications { get; }

        public IReadOnlyList<string> ProteinIds { get; }
        public byte[] MzBlob { get; }
        public byte[] IntensityBlob { get; }
        public int NumPeaks { get; }
        public IReadOnlyList<BlibPeakAnnotation> Annotations { get; }

        /// <summary>
        /// A peptide in blib modified-sequence form from its modification masses by residue:
        /// one signed four-decimal bracket after each modified residue.
        /// </summary>
        public static string FormatModifiedSequence(string sequence, IReadOnlyDictionary<int, double> massByResidue)
        {
            var result = new StringBuilder(sequence.Length + 12 * massByResidue.Count);
            for (int i = 0; i < sequence.Length; i++)
            {
                result.Append(sequence[i]);
                if (massByResidue.TryGetValue(i, out double mass))
                    result.Append(FormatMassDelta(mass));
            }
            return result.ToString();
        }

        /// <summary>
        /// A modification mass as a blib bracket, <c>[+57.0215]</c>: signed, four decimals, the
        /// text <see cref="BlibWriter.ConvertUnimodToMass"/> writes too.
        /// </summary>
        public static string FormatMassDelta(double mass)
        {
            return string.Format(CultureInfo.InvariantCulture, mass >= 0.0 ? @"[+{0:F4}]" : @"[{0:F4}]", mass);
        }

        /// <summary>
        /// Modification masses by residue, with a terminal position (before the first residue or
        /// past the last) moved onto its end residue and the masses at one residue summed.
        /// </summary>
        private static SortedDictionary<int, double> SumByResidue(Dictionary<int, double> modMasses, int length)
        {
            var byResidue = new SortedDictionary<int, double>();
            foreach (var pair in modMasses)
            {
                int position = Math.Min(Math.Max(pair.Key, 0), length - 1);
                byResidue.TryGetValue(position, out double mass);
                byResidue[position] = mass + pair.Value;
            }
            return byResidue;
        }

        private static BlibPeakAnnotation[] Annotate(string sequence, Dictionary<int, double> modMasses,
            LibraryFragment[] fragments)
        {
            var annotations = new List<BlibPeakAnnotation>(fragments.Length);
            for (int i = 0; i < fragments.Length; i++)
            {
                var annotation = fragments[i].Annotation;
                if ((annotation.IonType != IonType.B && annotation.IonType != IonType.Y) ||
                    annotation.Ordinal < 1 || annotation.Charge < 1)
                {
                    continue;
                }
                double? mz = PeptideFragmentMass.CalculateFragmentMz(annotation.IonType, annotation.Ordinal,
                    annotation.Charge, sequence, modMasses,
                    annotation.HasNeutralLoss ? annotation.NeutralLossMass : null);
                if (!mz.HasValue || !BlibPeakAnnotations.MatchesPeak(mz.Value, fragments[i].Mz))
                    continue;
                annotations.Add(new BlibPeakAnnotation
                {
                    PeakIndex = i,
                    Name = BlibPeakAnnotations.FormatName(annotation),
                    Charge = annotation.Charge,
                    MzTheoretical = mz.Value,
                    MzObserved = fragments[i].Mz
                });
            }
            return annotations.ToArray();
        }
    }
}
