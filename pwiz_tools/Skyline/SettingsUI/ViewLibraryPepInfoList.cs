/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Linq;
using System.Reflection;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Lib;
using Resources = pwiz.Skyline.Properties.Resources;

namespace pwiz.Skyline.SettingsUI
{
    public class ViewLibraryPepInfoList : AbstractReadOnlyList<ViewLibraryPepInfo>
    {
        private readonly ImmutableList<ViewLibraryPepInfo> _allEntries;
        private List<string> _matchTypes = new List<string>();
        private readonly LibKeyModificationMatcher _matcher;
        private readonly bool _allPeptides;

        // String names for properties
        private const string PRECURSOR_MZ = @"PrecursorMz";
        private const string UNMODIFIED_TARGET_TEXT = @"UnmodifiedTargetText";
        private const string INCHI_KEY = @"InchiKey";
        private const string FORMULA = @"Formula";
        private const string ADDUCT = @"Adduct";
        private const string ADDUCT_MINUS_BRACKETS = @"AdductMinusBrackets";

        public ViewLibraryPepInfoList(IEnumerable<ViewLibraryPepInfo> items, LibKeyModificationMatcher matcher, out bool allPeptides)
        {
            _allEntries = ImmutableList.ValueOf(items.OrderBy(item => item, Comparer<ViewLibraryPepInfo>.Create(ComparePepInfos)));
            _matcher = matcher; // Used to calculate precursor m/z
            allPeptides = _allEntries.All(key => key.Key.IsProteomicKey); // Are there any non-proteomic entries in the library?
            _allPeptides = allPeptides;
        }

        public override int Count
        {
            get { return _allEntries.Count; }
        }

        public override ViewLibraryPepInfo this[int index]
        {
            get { return _allEntries[index]; }
        }

        /// <summary>
        /// Add a string to display to our list based on the given property
        /// </summary>
        private void UpdateMatchTypes(string propertyName)
        {
            // If there are items on our list of indices and our list of match types does not
            // already include this match type, then add it to the list
            if (propertyName == UNMODIFIED_TARGET_TEXT)
            {
                // Because the target text can be a molecule name or a peptide sequence only display
                // peptide if every entry on the list is a peptide
                _matchTypes.Add(_allPeptides ? ColumnCaptions.Peptide : ColumnCaptions.MoleculeName);
            }
            else if (propertyName == PRECURSOR_MZ)
            {
                if (!_matchTypes.Contains(ColumnCaptions.PrecursorMz))
                {
                    _matchTypes.Add(ColumnCaptions.PrecursorMz);
                }
            }else if (propertyName == FORMULA)
            {
                _matchTypes.Add(Resources.SmallMoleculeLibraryAttributes_KeyValuePairs_Formula);
            } else if (propertyName == INCHI_KEY)
            {
                _matchTypes.Add(ColumnCaptions.InChiKey);
            } else if (propertyName == ADDUCT || propertyName == ADDUCT_MINUS_BRACKETS)
            {
                if(!_matchTypes.Contains(ColumnCaptions.PrecursorAdduct))
                _matchTypes.Add(ColumnCaptions.PrecursorAdduct);
            }
            else if (!_matchTypes.Contains(propertyName))
            {
                _matchTypes.Add(propertyName);
            }
        }

        /// <summary>
        /// Find entries which contain the filter text in the given property
        /// </summary>
        private List<int> SubstringSearchByProperty(PropertyInfo property, string filterText)
        {
            // Return all the entries where the value of the given property matches the filter text
            var indices = (from entry in _allEntries where property.GetValue(entry).ToString()
                .IndexOf(filterText, StringComparison.CurrentCultureIgnoreCase) >= 0 select IndexOf(entry)).ToList();
            if (indices.Any())
            {
                UpdateMatchTypes(property.Name);
            }

            return indices;
        }

        /// <summary>
        /// Find entries whose beginning matches the filter text in the given property
        /// </summary>
        private List<int> PrefixSearchByProperty(PropertyInfo property, string filterText)
        {
            // Sort with respect to the given property
            // Use ToString to sort correctly in case of double value
            var orderedList = _allEntries.OrderBy(info => property.GetValue(info).ToString()).ToList();

            // Binary search for entries starting with the filter text
            var matchRange = CollectionUtil.BinarySearch(orderedList,
                info => string.Compare(property.GetValue(info).ToString(), 0, filterText, 0, filterText.Length,
                    StringComparison.OrdinalIgnoreCase));
            // Return the indices of entries that matched the filter text
            var matchIndices = orderedList.Skip(matchRange.Start).Take(matchRange.Length).Select(item => _allEntries.IndexOf(item)).ToList();
            if (matchIndices.Any())
            {
                UpdateMatchTypes(property.Name);
            }
            return matchIndices;
        }

        /// <summary>
        /// Find the indices of entries whose "OtherIDs" field contain matches, and update the match types
        /// with the correct category (HMDB, SMILES, etc.)
        /// </summary>
        private List<int> AccessionNumberSearch(string filterText, ViewLibraryDlg.FilterType filterType)
        {
            var matchIndices = new List<int>();
            foreach (var entry in _allEntries)
            {
                if (entry.OtherKeys != null)
                {
                    // Split the string containing all molecular IDs into separate entries
                    var accessionNumDict = MoleculeAccessionNumbers.FormatAccessionNumbers(entry.OtherKeys);
                    foreach (var pair in accessionNumDict.Where(pair => filterType == ViewLibraryDlg.FilterType.contains ? 
                        pair.Value.Contains(filterText) : pair.Value.StartsWith(filterText)))
                    {
                        matchIndices.Add(IndexOf(entry));
                        UpdateMatchTypes(pair.Key);
                    }
                }
            }

            return matchIndices;
        }

        /// <summary>
        /// Find the indices of entries matching the filter text according to the filter type
        /// </summary>
        /// <param name="filterText"> Search term </param>
        /// <param name="filterType"> "Starts with" or "Contains"</param>
        /// <param name="matchTypes"> Categories in which matches were found</param>
        public IList<int> Filter(string filterText, ViewLibraryDlg.FilterType filterType, out List<string> matchTypes)
        {
            const double mzFilterTolerance = 0.1; // Tolerance for the numeric proximity to the filter text
            _matchTypes = new List<string>();

            if (string.IsNullOrEmpty(filterText))
            {
                // Don't filter anything out and don't indicate matches in any category
                matchTypes = new List<string>();
                return new RangeList(0, Count);
            }
            // We have to deal with the UnmodifiedTargetText separately from the adduct because the
            // adduct has special sorting which is different than the way adduct.ToString() would sort.

            // If there are small molecules in the library then search by multiple fields instead of just molecule name
            var stringSearchFields = !_allPeptides ?  // Fields of type string we want to compare to the search term
                new List<string>{ UNMODIFIED_TARGET_TEXT, FORMULA, INCHI_KEY, ADDUCT, ADDUCT_MINUS_BRACKETS } // Order of properties does not matter as results are sorted by molecule or peptide name
                : new List<string> {UNMODIFIED_TARGET_TEXT};

            var filteredIndices = Enumerable.Empty<int>().ToList(); // The indices of entries in the peptide list that match our filter text
            var rangeList = Enumerable.Empty<int>();
            if (filterType == ViewLibraryDlg.FilterType.contains)
            {
                // Find the indices of entries that contain the filter text
                filteredIndices = stringSearchFields.Aggregate(rangeList, (current, str) =>
                    current.Union(SubstringSearchByProperty(typeof(ViewLibraryPepInfo).GetProperty(str), filterText))).ToList();
            }
            else if(filterType == ViewLibraryDlg.FilterType.starts_with)
            {
                // Find the indices of entries that have a field that could match the search term if something was appended to it 
                filteredIndices = stringSearchFields.Aggregate(rangeList, (current, str) =>
                    current.Union(PrefixSearchByProperty(typeof(ViewLibraryPepInfo).GetProperty(str), filterText))).ToList();
            }
            // If the filter text can be read as a number, we want to include spectra that match the precursor m/z as well
            if (double.TryParse(filterText, out var result))
            {
                // Calculate the mz of each entry to search if we can
                foreach (var entry in _allEntries)
                {
                    if (entry.Target != null && entry.PrecursorMz == 0)
                    {
                        entry.PrecursorMz = ViewLibraryDlg.CalcMz(entry, _matcher);
                    }
                }
                
                // Find the entries that match the m/z lexicographically
                filteredIndices =
                    filteredIndices.Union(PrefixSearchByProperty(typeof(ViewLibraryPepInfo).GetProperty(PRECURSOR_MZ),
                        filterText)).ToList();

                // Add entries that are close to the filter text numerically
                // Create a list of object references sorted by their absolute difference from target m/z
                var sortedMzList = _allEntries.OrderBy(entry => Math.Abs(entry.PrecursorMz - result));

                // Then find the first entry with a precursor m/z exceeding our match tolerance
                var results = sortedMzList.TakeWhile(entry => !(Math.Abs(
                    entry.PrecursorMz - result) > mzFilterTolerance)).Select(IndexOf).ToList();
                filteredIndices = filteredIndices.Union(results).ToList();
                if (results.Any())
                {
                    UpdateMatchTypes(PRECURSOR_MZ);
                }
            }

            matchTypes = _matchTypes;

            filteredIndices = filteredIndices.Union(AccessionNumberSearch(filterText, filterType)).ToList();
            var enumerable = filteredIndices.ToList(); // Avoid multiple enumeration

            // If we have not found any matches yet and it is a peptide list look at all the entries which could match
            // the target text, if they had something appended to them.
            if (!enumerable.Any())
            {
                var range = CollectionUtil.BinarySearch(_allEntries,
                    info => string.Compare(info.UnmodifiedTargetText, 0, filterText, 0,
                        info.UnmodifiedTargetText.Length,
                        StringComparison.OrdinalIgnoreCase));
                // Return the elements from the range whose DisplayText actually matches the filter text.
                return ImmutableList.ValueOf(new RangeList(range).Where(i => _allEntries[i].DisplayText
                    .StartsWith(filterText, StringComparison.OrdinalIgnoreCase)));
            }
            // Return the indices of the matches sorted alphabetically by display text
            return ImmutableList.ValueOf(enumerable.OrderBy(info => _allEntries[info].DisplayText));
        }

        public int IndexOf(LibraryKey libraryKey)
        {
            if (libraryKey == null)
            {
                return -1;
            }
            var keyToFind = new ViewLibraryPepInfo(new LibKey(libraryKey));
            var range = CollectionUtil.BinarySearch(_allEntries, entry => ComparePepInfos(entry, keyToFind));
            return range.Length > 0 ? range.Start : -1;
        }

        public static int ComparePepInfos(ViewLibraryPepInfo info1, ViewLibraryPepInfo info2)
        {
            int result = string.Compare(info1.UnmodifiedTargetText, info2.UnmodifiedTargetText,
                StringComparison.OrdinalIgnoreCase);
            if (result == 0)
            {
                result = info1.Adduct.CompareTo(info2.Adduct);
            }
            if (result == 0)
            {
                result = string.Compare(info1.Key.ToString(), info2.Key.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            return result;
        }
    }
}
