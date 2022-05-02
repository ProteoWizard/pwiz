/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Used by small molecule transition list reader to deal with lists with sparse accession values -
    /// that is, multiple references to the same molecule but with varying yet non-conflicting accessions
    /// </summary>
    internal class MoleculeNameAndAccessions : IEquatable<MoleculeNameAndAccessions>
    {
        public static MoleculeNameAndAccessions EMPTY =
            new MoleculeNameAndAccessions(null, MoleculeAccessionNumbers.EMPTY);

        public MoleculeNameAndAccessions(string name, MoleculeAccessionNumbers accessionNumbers)
        {
            Name = name;
            AccessionNumbers = accessionNumbers ?? MoleculeAccessionNumbers.EMPTY;
        }

        public string Name;
        public MoleculeAccessionNumbers AccessionNumbers;

        public bool IsMatchWith(MoleculeNameAndAccessions other)
        {
            return Equals(Name, other.Name) &&
                   ((AccessionNumbers.IsEmpty && other.AccessionNumbers.IsEmpty) || 
                    AccessionNumbers.Intersection(other.AccessionNumbers) != null);
        }

        public bool InconsistentWith(MoleculeNameAndAccessions other)
        {
            return Equals(Name, other.Name) && AccessionNumbers.InconsistentWith(other.AccessionNumbers);
        }

        public bool IsEmpty => string.IsNullOrEmpty(Name) && MoleculeAccessionNumbers.IsNullOrEmpty(AccessionNumbers);

        public override string ToString() => (Name??@"<no name>") + @" " + AccessionNumbers; // For debugging convenience

        public bool Equals(MoleculeNameAndAccessions other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && Equals(AccessionNumbers, other.AccessionNumbers);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MoleculeNameAndAccessions)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (AccessionNumbers != null ? AccessionNumbers.GetHashCode() : 0);
            }
        }

        /// <summary>
        /// Helps construct a mapping of transition list row numbers to molecule name and accessions - as each row is added, an attempt
        /// is made to combine with and update any existing entries which describe the same molecule but with a different set of
        /// non-conflicting accession values
        /// </summary>
        /// <param name="name">The molecule name of the proposed entry</param>
        /// <param name="accessionNumbers">The accession numbers parsed for the new entry</param>
        /// <param name="index">the row index of the proposed entry</param>
        /// <param name="consensusAccessionNumbers">The existing set of name/accessions</param>
        /// <returns>The current name/accession values for the index</returns>
        public static MoleculeNameAndAccessions GetConsensusAccessionNumbers(string name, MoleculeAccessionNumbers accessionNumbers, 
            int index, Dictionary<int, MoleculeNameAndAccessions> consensusAccessionNumbers)
        {
            if (!consensusAccessionNumbers.TryGetValue(index, out var result))
            {
                // Add this to the list - but does it add any information to any existing entries?
                var mergedAccessionNumbers = accessionNumbers;
                var mergedName = name;
                // May need multiple passes to handle merging i.e. 3 no-names with overlapping (inchi, hmdb) (hmdb, smiles) (smiles, inchikey)
                for (var changing = true; changing;)
                {
                    changing = false;
                    // Find any existing entries that might be complementary descriptions
                    foreach (var kvp in consensusAccessionNumbers)
                    {
                        if (string.IsNullOrEmpty(kvp.Value.Name) || string.IsNullOrEmpty(mergedName))
                        {
                            // If either or both lack names, see if any accessions agree
                            var intersect = kvp.Value.AccessionNumbers.Intersection(mergedAccessionNumbers);
                            if (MoleculeAccessionNumbers.IsNullOrEmpty(intersect))
                            {
                                continue; // No agreement
                            }

                            if (string.IsNullOrEmpty(mergedName))
                            {
                                mergedName = kvp.Value.Name;
                            }
                        }
                        // Names must agree if both are present 
                        else if (!Equals(kvp.Value.Name, mergedName))
                        {
                            continue;
                        }

                        var merged = kvp.Value.AccessionNumbers.Union(mergedAccessionNumbers);
                        if (merged != null)
                        {
                            mergedAccessionNumbers = merged;
                            if (!Equals(kvp.Value.Name, mergedName) || !Equals(kvp.Value.AccessionNumbers, merged))
                            {
                                changing = true;
                                kvp.Value.AccessionNumbers = mergedAccessionNumbers;
                                kvp.Value.Name = mergedName;
                            }
                        }
                    }
                }

                result = new MoleculeNameAndAccessions(mergedName, mergedAccessionNumbers);
                consensusAccessionNumbers.Add(index, result);
            }

            return result;
        }
    }
}