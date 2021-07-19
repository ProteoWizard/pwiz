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
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.SettingsUI
{
    public class ViewLibraryPepInfoList : AbstractReadOnlyList<ViewLibraryPepInfo>
    {
        private readonly ImmutableList<ViewLibraryPepInfo> _allEntries;
        private List<string> _matchTypes = new List<string>();
        private LibKeyModificationMatcher _matcher;
        private bool _allPeptides;

        public ViewLibraryPepInfoList(IEnumerable<ViewLibraryPepInfo> items, LibKeyModificationMatcher matcher)
        {
            _allEntries = ImmutableList.ValueOf(items.OrderBy(item=>item, Comparer<ViewLibraryPepInfo>.Create(ComparePepInfos)));
            _matcher = matcher;
            _allPeptides = _allEntries.All(key => key.Key.IsProteomicKey);
        }

        public override int Count
        {
            get { return _allEntries.Count; }
        }

        public override ViewLibraryPepInfo this[int index]
        {
            get { return _allEntries[index]; }
        }

        private void UpdateMatchTypes(int numMatches, PropertyInfo property)
        {
            // If there are items on our list of indices and our list of match types does not
            // already include this match type, then add it to the list
            if (numMatches > 0)
            {
                if (!_matchTypes.Contains(property.Name))
                {
                    if (property.Name == "UnmodifiedTargetText")
                    {
                        _matchTypes.Add(_allPeptides ? "Peptide" : "Molecule Name");
                    }
                    else
                    {
                        _matchTypes.Add(property.Name);
                    }
                }
            }
        }
        private List<int> ContainsSearchByProperty(PropertyInfo property, string filterText)
        {
            // Return all the entries where the value of the given property matches the filter text
            var indices = (from entry in _allEntries where property.GetValue(entry).ToString().IndexOf(filterText, StringComparison.CurrentCultureIgnoreCase) >= 0
                select IndexOf(entry)).ToList();
            UpdateMatchTypes(indices.Count, property);
            return indices;
        }
        private List<int> PrefixSearchByProperty(PropertyInfo property, string filterText)
        {
            // Sort with respect to the given property
            var orderedList = _allEntries.OrderBy(property.GetValue).ToList();
            var matchRange = CollectionUtil.BinarySearch(orderedList,
                info => string.Compare(property.GetValue(info).ToString(), 0, filterText, 0, filterText.Length,
                    StringComparison.OrdinalIgnoreCase));
            var matchIndices = orderedList.Skip(matchRange.Start).Take(matchRange.Length).Select(item => _allEntries.IndexOf(item)).ToList();
            UpdateMatchTypes(matchIndices.Count, property);
            return matchIndices;
        }
        public IList<int> Filter(string filterText, ViewLibraryDlg.FilterType filterType, out List<string> matchTypes)
        {
            _matchTypes = new List<string>();
            matchTypes = new List<string>();
            if (string.IsNullOrEmpty(filterText))
            {
                return new RangeList(0, Count);
            }
            // We have to deal with the UnmodifiedTargetText separately from the adduct because the
            // adduct has special sorting which is different than the way adduct.ToString() would sort.

            // If there are small molecules in the library, then search by multiple fields instead of just molecule name
            var stringSearchFields = !_allPeptides ? new List<string>{ @"UnmodifiedTargetText", @"Formula", @"InchiKey" } : new List<string> {@"UnmodifiedTargetText"};
            var filteredIndices = Enumerable.Empty<int>(); // The indices of entries in the peptide list that match our filter text
            if (filterType == ViewLibraryDlg.FilterType.contains)
            {
                var rangeList = Enumerable.Empty<int>();
                filteredIndices = stringSearchFields.Aggregate(rangeList, (current, str) =>
                    current.Union(ContainsSearchByProperty(typeof(ViewLibraryPepInfo).GetProperty(str), filterText)));
            }
            else if(filterType == ViewLibraryDlg.FilterType.startsWith)
            {

                // Fields of type string we want to compare to the search term

                // Find the indices of entries that have field that could match the search term if something was appended to it 
                var rangeList = Enumerable.Empty<int>();
                filteredIndices = stringSearchFields.Aggregate(rangeList, (current, str) =>
                    current.Union(PrefixSearchByProperty(typeof(ViewLibraryPepInfo).GetProperty(str), filterText)));
            }
            // If the filter text can be read as a number, we want to include spectra that match the precursor m/z as well
            if (double.TryParse(filterText, out var result))
            {
                // Find the precursor m/z for each entry
                foreach (var entry in _allEntries)
                {
                    entry.PrecursorMz = ViewLibraryDlg.getMz(entry, _matcher);
                }
                // Add entries that match the m/z tolerance lexicographically
                filteredIndices =
                    filteredIndices.Union(PrefixSearchByProperty(typeof(ViewLibraryPepInfo).GetProperty(@"PrecursorMz"),
                        filterText));
                
                // Add entries that are close to the filter text numerically
                // Set an arbitrary tolerance for m/z matching
                const double MZ_FILTER_TOLERANCE = 0.1;

                //Create a list of object references sorted by their absolute difference from target m/z
                var sortedMzList = _allEntries.OrderBy(entry => Math.Abs(ViewLibraryDlg.getMz(entry, _matcher) - result));

                // Then find the first entry with a precursor m/z exceeding our match tolerance
                var results = sortedMzList.TakeWhile(entry => !(Math.Abs(
                    ViewLibraryDlg.getMz(entry, _matcher) - result) > MZ_FILTER_TOLERANCE)).Select(IndexOf).ToList();
                filteredIndices = filteredIndices.Union(results);
                UpdateMatchTypes(results.Count, typeof(ViewLibraryPepInfo).GetProperty(@"PrecursorMz"));
            }

            matchTypes = _matchTypes;

            if (_allPeptides)
            {
                if (!filteredIndices.Any())
                {
                    // Now look at all the entries which could match the target text, if they had something appended to them.
                    var range = CollectionUtil.BinarySearch(_allEntries,
                        info => string.Compare(info.UnmodifiedTargetText, 0, filterText, 0,
                            info.UnmodifiedTargetText.Length,
                            StringComparison.OrdinalIgnoreCase));
                    // Return the elements from the range whose DisplayText actually matches the filter text.
                    return ImmutableList.ValueOf(new RangeList(range).Where(i => _allEntries[i].DisplayText
                        .StartsWith(filterText, StringComparison.OrdinalIgnoreCase)));
                }
            }

            return ImmutableList.ValueOf(filteredIndices.OrderBy(info => _allEntries[info].DisplayText));
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
