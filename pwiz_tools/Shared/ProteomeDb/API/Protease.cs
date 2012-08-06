/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Text.RegularExpressions;

namespace pwiz.ProteomeDatabase.API
{
    public interface IProtease
    {
        IEnumerable<DigestedPeptide> Digest(Protein protein);
        String Name { get; }
    }

    public class DigestedPeptide
    {
        public int Index { get; set; }
        public String Sequence { get; set; }
    }

// ReSharper disable InconsistentNaming
    public enum SequenceTerminus
    {
        N,
        C,
    }
// ReSharper restore InconsistentNaming

    public class Protease : IProtease
    {
        public Protease(string cleavage, string restrict)
            : this(cleavage, restrict, SequenceTerminus.C)
        {
        }

        public Protease(string cleavage, string restrict, SequenceTerminus type)
        {
            Cleavage = cleavage;
            Restrict = restrict;
            Type = type;
        }

        public String Name { get { return Regex;}}
        public string Cleavage { get; private set; }

        public string Restrict { get; private set; }

        public SequenceTerminus Type { get; private set; }

        public string Regex
        {
            get
            {
                string cut = "[" + Cleavage + "]"; // Not L10N
                string nocut = (Restrict == "" ? "[A-Z]" : "[^" + Restrict + "]"); // Not L10N
                return (IsCTerm() ? cut + nocut : nocut + cut);
            }
        }

        public bool IsNTerm() { return Type == SequenceTerminus.N; }
        public bool IsCTerm() { return Type == SequenceTerminus.C; }

        public IEnumerable<DigestedPeptide> Digest(Protein protein)
        {
            Regex regex = new Regex(Regex);
            int begin = 0;
            int len = protein.Sequence.Length;
            while (begin < len)
            {
                int end = begin;
                int endFirst = begin;
                int missed = 0;
                do
                {
                    string sequence = protein.Sequence;
                    Match m = regex.Match(sequence, end);
                    end = (m.Success ? m.Index + 1 : len);

                    // Save the end of the first cleavage
                    if (missed == 0)
                        endFirst = end;
                    // TODO(nicksh)
                    //// Deal with 'ragged ends', or cleavages one amino acid apart
                    //// i.e. KR, RR, etc. for trypsin
                    //if (settings.ExcludeRaggedEnds && end < len)
                    //{
                    //    Match mNext = regex.Match(sequence, end);
                    //    if (mNext.Success && mNext.Index == end)
                    //    {
                    //        // If there are no missed cleavages, then move the
                    //        // begin index to the next cleavage point that is
                    //        // not part of a run.
                    //        if (missed == 0)
                    //            endFirst = GetDiscontiguousCleavageIndex(regex, mNext, sequence);
                    //        break;
                    //    }
                    //}

                    // Single amino acid peptides have no fragment ions.
                    int count = end - begin;
                    if (count > 1 && sequence.IndexOfAny(new[] { '*', '-' }, begin, count) == -1) // Not L10N
                    {
                        DigestedPeptide peptide = new DigestedPeptide
                                                      {
                                                          Index = begin,
                                                          Sequence = sequence.Substring(begin, end - begin)
                                                      };
                        yield return peptide;
                    }

                    // Increment missed cleavages for next loop.
                    missed++;

                }
                while (end < len);

                begin = endFirst;
            }
        }

        /// <summary>
        /// Counts the number of cleavage locations in a sequence.  Useful for
        /// calculating missed cleavages on peptide sequences that were not
        /// generated with the <see cref="Digest"/> function.
        /// </summary>
        /// <param name="seq">The sequence to inspect</param>
        /// <returns>Number of cleavage points</returns>
        public int CountCleavagePoints(string seq)
        {
            int count = 0;

            Regex regex = new Regex(Regex);
            Match m = regex.Match(seq);
            while (m.Success)
            {
                count++;
                m = m.NextMatch();
            }
            return count;
        }

/*
        /// <summary>
        /// Returns the next cleavage index that is more than one
        /// amino acid beyond the last.
        /// </summary>
        /// <param name="regex">Regex matching cleavage point</param>
        /// <param name="mNext">Previously matched cleavage</param>
        /// <param name="aa">The amino acid string being digested</param>
        /// <returns>The index of the next valid match</returns>
        private static int GetDiscontiguousCleavageIndex(Regex regex, Match mNext, string aa)
        {
            // Loop while matches are one amino acid apart.
            Match mLast;
            do
            {
                do
                {
                    mLast = mNext;
                    mNext = regex.Match(aa, mLast.Index + 1);
                }
                while (mNext.Success && mNext.Index == mLast.Index + 1);

                // Make sure this new cleavage point is not itself part of a run
                // of cleavage points.
                mLast = mNext;
                if (mNext.Success)
                    mNext = regex.Match(aa, mNext.Index + 1);
            }
            while (mNext.Success && mNext.Index == mLast.Index + 1);

            // Return the cleavage point, or the end of the string.
            return (mLast.Success ? mLast.Index + 1 : aa.Length);
        }
*/
    }
}
