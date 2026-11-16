/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// The peaks of a library spectrum as a mass spectrometer could have produced them: one
    /// peak per m/z, scaled to the base peak. Applied by every library reader before typing, so
    /// a library is the same object whichever file it came from.
    /// </summary>
    public static class FragmentPeaks
    {
        /// <summary>
        /// Merges peaks of <paramref name="fragmentsByMz"/> (in m/z order) that share one m/z into
        /// one peak with their summed intensity, and scales the result so the base peak is 1.
        /// The merged peak keeps the first peak's annotation.
        ///
        /// <para>A predicted library can list two ions of one peptide at exactly one m/z, each
        /// with its own intensity (Carafe lists <c>b2</c> and <c>b4^2</c> of a peptide whose first
        /// four residues repeat a composition, or <c>b4</c> and <c>y3</c> of <c>AQIHQFR</c>, as two
        /// rows at the same written m/z). No instrument can separate them: the spectrum has one
        /// peak there, with the summed intensity, and a search that extracted and scored two
        /// chromatograms at one m/z would count one measurement twice. Two peaks at different
        /// m/z, however close, are what the library states and stay separate; whether they
        /// extract the same chromatogram is the extraction's question, at its own tolerance.</para>
        /// </summary>
        public static LibraryFragment[] MergeEqualMz(LibraryFragment[] fragmentsByMz)
        {
            int distinct = 0;
            for (int i = 0; i < fragmentsByMz.Length; i++)
            {
                if (i == 0 || fragmentsByMz[i].Mz != fragmentsByMz[i - 1].Mz)
                    distinct++;
            }
            var merged = fragmentsByMz;
            if (distinct != fragmentsByMz.Length)
            {
                merged = new LibraryFragment[distinct];
                int last = -1;
                for (int i = 0; i < fragmentsByMz.Length; i++)
                {
                    if (last >= 0 && fragmentsByMz[i].Mz == merged[last].Mz)
                        merged[last].RelativeIntensity += fragmentsByMz[i].RelativeIntensity;
                    else
                        merged[++last] = fragmentsByMz[i];
                }
            }

            float basePeak = 0f;
            foreach (var fragment in merged)
            {
                if (fragment.RelativeIntensity > basePeak)
                    basePeak = fragment.RelativeIntensity;
            }
            if (basePeak > 0f && basePeak != 1f)
            {
                if (ReferenceEquals(merged, fragmentsByMz))
                    merged = (LibraryFragment[])fragmentsByMz.Clone();
                for (int i = 0; i < merged.Length; i++)
                    merged[i].RelativeIntensity /= basePeak;
            }
            return merged;
        }
    }
}
