/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using pwiz.Common.Chemistry;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// This class helps find a peptide associated with a particular precursor Mz.
    /// </summary>
    public class PeptideFinder
    {
        private readonly List<PeptidePrecursorMz> _precursorMzPeptideList = new List<PeptidePrecursorMz>();
        private readonly double _mzMatchTolerance;

        public PeptideFinder(SrmDocument document)
        {
            // Create list of Peptide/PrecursorMz pairs.
            foreach (var peptideDocNode in document.Molecules)
            {
                foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups)
                {
                    var precursorMz = transitionGroupDocNode.PrecursorMz;
                    _precursorMzPeptideList.Add(new PeptidePrecursorMz(peptideDocNode, precursorMz));
                }
            }

            // Sort list by PrecursorMz.
            _precursorMzPeptideList.Sort((p1, p2) => p1.PrecursorMz.CompareTo(p2.PrecursorMz));

            _mzMatchTolerance = document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
        }

        /// <summary>
        /// Return all peptide doc nodes whose precursor Mz matches the given Mz within the
        /// document's match tolerance. Multiple peptides at the same Q1 (structural isomers,
        /// or SRM methods that schedule different compounds against a shared precursor) are
        /// all returned; callers that bind chromatograms per peptide can then emit one entry
        /// per match instead of collapsing same-Q1 peptides into a single chromatogram.
        /// </summary>
        public IEnumerable<PeptideDocNode> FindPeptides(SignedMz precursorMz)
        {
            if (_precursorMzPeptideList.Count == 0)
                yield break;

            var lookup = new PeptidePrecursorMz(null, precursorMz);
            int i = _precursorMzPeptideList.BinarySearch(lookup, PeptidePrecursorMz.COMPARER);
            if (i < 0)
                i = ~i;

            // Most SRM spectra match a single peptide; cache the first hit's
            // Id.GlobalIndex and only allocate a HashSet if a second distinct
            // peptide is found. A peptide with multiple transition groups at
            // the same Q1 is also deduped without any heap allocation. Keying
            // by PeptideDocNode directly would invoke its structural Equals,
            // which is the wrong primitive here; GlobalIndex is the preferred
            // key per Identity's docstring.
            int firstSeenIndex = -1;
            HashSet<int> additionalSeen = null;

            bool TryAddSeen(int globalIndex)
            {
                if (firstSeenIndex == -1)
                {
                    firstSeenIndex = globalIndex;
                    return true;
                }
                if (additionalSeen == null)
                {
                    if (globalIndex == firstSeenIndex)
                        return false;
                    additionalSeen = new HashSet<int> { firstSeenIndex, globalIndex };
                    return true;
                }
                return additionalSeen.Add(globalIndex);
            }

            // Walk outward from the landing index, stopping in each direction as
            // soon as the candidate falls outside tolerance or crosses polarity
            // (SignedMz orders negatives before positives).
            for (int j = i - 1; j >= 0; j--)
            {
                var cand = _precursorMzPeptideList[j];
                if (cand.PrecursorMz.IsNegative != precursorMz.IsNegative ||
                    Math.Abs(cand.PrecursorMz - precursorMz) > _mzMatchTolerance)
                    break;
                if (TryAddSeen(cand.NodePeptide.Id.GlobalIndex))
                    yield return cand.NodePeptide;
            }
            for (int j = i; j < _precursorMzPeptideList.Count; j++)
            {
                var cand = _precursorMzPeptideList[j];
                if (cand.PrecursorMz.IsNegative != precursorMz.IsNegative ||
                    Math.Abs(cand.PrecursorMz - precursorMz) > _mzMatchTolerance)
                    break;
                if (TryAddSeen(cand.NodePeptide.Id.GlobalIndex))
                    yield return cand.NodePeptide;
            }
        }

        /// <summary>
        /// Return doc node for a peptide associated with a given precursor Mz.  May return
        /// null if the precursor Mz lies outside the matching tolerance setting.
        /// </summary>
        public PeptideDocNode FindPeptide(SignedMz precursorMz)
        {
            if (_precursorMzPeptideList.Count == 0)
                return null;

            // Find closest precursor Mz match.
            var lookup = new PeptidePrecursorMz(null, precursorMz);
            int i = _precursorMzPeptideList.BinarySearch(lookup, PeptidePrecursorMz.COMPARER);
            if (i >= 0)
            {
                return _precursorMzPeptideList[i].NodePeptide; // Exact match
            }

            // BinarySearch returns bitwise complement of the index of the next larger element,
            // but the closest match might be the element we just passed.  Also, if precursor Mz is negative,
            // we have to make sure search hasn't crossed over into positive territory. (SignedMz sorts
            // negative values before positive ones e.g. -100, -200, 100, 200)
            i = ~i; 
            PeptidePrecursorMz closestMatch = null;
            var closestDistance = double.MaxValue;
            for (var candidateIndex = i - 1; candidateIndex <= i; candidateIndex++)
            {
                if (candidateIndex < 0 || candidateIndex >= _precursorMzPeptideList.Count)
                {
                    continue;
                }
                var candidate = _precursorMzPeptideList[candidateIndex];
                if (candidate.PrecursorMz.IsNegative != precursorMz.IsNegative)
                {
                    continue;
                }

                var distance = Math.Abs(candidate.PrecursorMz - precursorMz);
                if (distance < closestDistance)
                {
                    closestMatch = candidate;
                    closestDistance = distance;
                }
            }

            // Return only if the match is within allowed tolerance.
            return closestDistance > _mzMatchTolerance ? null : closestMatch?.NodePeptide;
        }

        private sealed class PeptidePrecursorMz
        {
            public PeptidePrecursorMz(PeptideDocNode nodePeptide, SignedMz precursorMz)
            {
                NodePeptide = nodePeptide;
                PrecursorMz = precursorMz;
            }

            public PeptideDocNode NodePeptide { get; private set; }
            public SignedMz PrecursorMz { get; private set; }

            public static readonly MzComparer COMPARER = new MzComparer();

            public class MzComparer : IComparer<PeptidePrecursorMz>
            {
                public int Compare(PeptidePrecursorMz p1, PeptidePrecursorMz p2)
                {
                    // ReSharper disable PossibleNullReferenceException
                    return p1.PrecursorMz.CompareTo(p2.PrecursorMz);
                    // ReSharper restore PossibleNullReferenceException
                }
            }

            public override string ToString()
            {
                return $@"{PrecursorMz.RawValue} {NodePeptide}"; // For debug convenience, not user-facing
            }
        }
    }
}
