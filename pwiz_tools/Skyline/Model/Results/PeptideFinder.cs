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
using System.Collections;

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
        /// Return doc node for a peptide associated with a given precursor Mz.  May return
        /// null if the precursor Mz lies outside the matching tolerance setting.
        /// </summary>
        public PeptideDocNode FindPeptide(double precursorMz)
        {
            if (_precursorMzPeptideList.Count == 0)
                return null;

            // Find closest precursor Mz match.
            var lookup = new PeptidePrecursorMz(null, precursorMz);
            int i = _precursorMzPeptideList.BinarySearch(lookup, PeptidePrecursorMz.COMPARER);
            if (i < 0)
            {
                i = ~i;
                if (i >= _precursorMzPeptideList.Count)
                    i = _precursorMzPeptideList.Count - 1;
                else if (i > 0 &&
                    precursorMz - _precursorMzPeptideList[i - 1].PrecursorMz <
                    _precursorMzPeptideList[i].PrecursorMz - precursorMz)
                    i--;
            }
            var closestMatch = _precursorMzPeptideList[i];

            // Return color seed only if the match is within allowed tolerance.
            return Math.Abs(closestMatch.PrecursorMz - precursorMz) > _mzMatchTolerance
                ? null
                : closestMatch.NodePeptide;
        }

        private sealed class PeptidePrecursorMz
        {
            public PeptidePrecursorMz(PeptideDocNode nodePeptide, double precursorMz)
            {
                NodePeptide = nodePeptide;
                PrecursorMz = precursorMz;
            }

            public PeptideDocNode NodePeptide { get; private set; }
            public double PrecursorMz { get; private set; }

            public static readonly MzComparer COMPARER = new MzComparer();

            public class MzComparer : IComparer<PeptidePrecursorMz>
            {
                public int Compare(PeptidePrecursorMz p1, PeptidePrecursorMz p2)
                {
                    return Comparer.Default.Compare(p1.PrecursorMz, p2.PrecursorMz);
                }
            }
        }
    }
}
