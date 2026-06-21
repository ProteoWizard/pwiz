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
                    _precursorMzPeptideList.Add(new PeptidePrecursorMz(peptideDocNode, transitionGroupDocNode, precursorMz));
                }
            }

            // Sort list by PrecursorMz.
            _precursorMzPeptideList.Sort((p1, p2) => p1.PrecursorMz.CompareTo(p2.PrecursorMz));

            _mzMatchTolerance = document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
        }

        /// <summary>
        /// Return the peptide doc node(s) the SRM data at this precursor m/z should be assigned to:
        /// peptides whose precursor Mz matches within tolerance and that match at least half of
        /// their own product ions against <paramref name="productMzs"/>. <paramref name="productMzs"/>
        /// is the union of product channels measured at this Q1 across the file (assembled by the
        /// caller), not a single spectrum -- a compound's transitions can arrive in separate scans, so
        /// the match is made against that aggregate. The half-or-more test lets genuinely co-targeted
        /// same-Q1 compounds each get their own chromatogram (including a real compound whose product
        /// set is a subset of a larger co-Q1 compound, or one with an occasional unmeasured transition),
        /// while an incidental Q1 neighbor that shares only a minority of its transitions -- e.g. a
        /// compound targeted by a different acquisition method -- is not handed this compound's signal.
        /// Returns nothing when no candidate matches at least half (the caller then emits the data
        /// unmatched so it still surfaces).
        /// </summary>
        public IEnumerable<PeptideDocNode> FindMatchingPeptides(SignedMz precursorMz, IList<SignedMz> productMzs)
        {
            var candidates = GetPrecursorMatches(precursorMz);
            if (candidates.Count == 0)
                yield break;

            // Dedupe by peptide GlobalIndex -- a peptide may have more than one transition group
            // landing on this Q1, and keying by PeptideDocNode would invoke its structural Equals,
            // the wrong primitive here.
            var seen = new HashSet<int>();
            foreach (var candidate in candidates)
            {
                if (!MatchesAtLeastHalfOfTransitions(candidate.NodeGroup, productMzs))
                    continue;
                if (seen.Add(candidate.NodePeptide.Id.GlobalIndex))
                    yield return candidate.NodePeptide;
            }
        }

        /// <summary>
        /// All candidate precursors (transition groups) whose precursor Mz matches within tolerance,
        /// found by binary search then walking outward in both directions until tolerance or polarity
        /// is exceeded (SignedMz orders negatives before positives).
        /// </summary>
        private List<PeptidePrecursorMz> GetPrecursorMatches(SignedMz precursorMz)
        {
            var matches = new List<PeptidePrecursorMz>();
            if (_precursorMzPeptideList.Count == 0)
                return matches;

            var lookup = new PeptidePrecursorMz(null, null, precursorMz);
            int i = _precursorMzPeptideList.BinarySearch(lookup, PeptidePrecursorMz.COMPARER);
            if (i < 0)
                i = ~i;

            for (int j = i - 1; j >= 0; j--)
            {
                var cand = _precursorMzPeptideList[j];
                if (cand.PrecursorMz.IsNegative != precursorMz.IsNegative ||
                    Math.Abs(cand.PrecursorMz - precursorMz) > _mzMatchTolerance)
                    break;
                matches.Add(cand);
            }
            for (int j = i; j < _precursorMzPeptideList.Count; j++)
            {
                var cand = _precursorMzPeptideList[j];
                if (cand.PrecursorMz.IsNegative != precursorMz.IsNegative ||
                    Math.Abs(cand.PrecursorMz - precursorMz) > _mzMatchTolerance)
                    break;
                matches.Add(cand);
            }
            return matches;
        }

        /// <summary>
        /// True if the transition group matches at least half of its own product ions among the
        /// spectrum's measured products (within tolerance, same polarity). A half-or-more match keeps a
        /// genuinely targeted compound -- even one whose product set is a subset of a larger co-Q1
        /// compound, or that has an occasional transition the file did not measure -- while rejecting
        /// an incidental Q1 neighbor that shares only a minority of its transitions. Where two kept
        /// precursors share transitions, those shared products are split between them downstream (each
        /// emitted peptide binds the channels that match its own transitions).
        /// </summary>
        private bool MatchesAtLeastHalfOfTransitions(TransitionGroupDocNode nodeGroup, IList<SignedMz> productMzs)
        {
            if (nodeGroup == null)
                return false;
            int total = 0, matched = 0;
            foreach (var nodeTran in nodeGroup.Transitions)
            {
                total++;
                var tranMz = nodeTran.Mz;
                for (int j = 0; j < productMzs.Count; j++)
                {
                    var productMz = productMzs[j];
                    if (productMz.IsNegative == tranMz.IsNegative &&
                        Math.Abs(productMz - tranMz) <= _mzMatchTolerance)
                    {
                        matched++;
                        break;
                    }
                }
            }
            return total > 0 && matched * 2 >= total;
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
            var lookup = new PeptidePrecursorMz(null, null, precursorMz);
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
            public PeptidePrecursorMz(PeptideDocNode nodePeptide, TransitionGroupDocNode nodeGroup, SignedMz precursorMz)
            {
                NodePeptide = nodePeptide;
                NodeGroup = nodeGroup;
                PrecursorMz = precursorMz;
            }

            public PeptideDocNode NodePeptide { get; private set; }
            public TransitionGroupDocNode NodeGroup { get; private set; }
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
