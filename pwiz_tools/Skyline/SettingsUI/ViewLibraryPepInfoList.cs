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
using System.Globalization;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Util;
namespace pwiz.Skyline.SettingsUI
{
    public class ViewLibraryPepInfoList : AbstractReadOnlyList<ViewLibraryPepInfo>
    {
        private readonly ImmutableList<ViewLibraryPepInfo> _allEntries;
        private readonly LibKeyModificationMatcher _matcher;
        private readonly bool _allPeptides;
        public readonly List<string> _stringSearchFields;

        public readonly List<string> _accessionNumberTypes;
        // Tolerance for the numeric proximity of the precursor m/z to the filter text
        public const double MZ_FILTER_TOLERANCE = 0.1;

        // String names for properties
        private const string PRECURSOR_MZ = @"PrecursorMz";
        private const string UNMODIFIED_TARGET_TEXT = @"UnmodifiedTargetText";
        private const string INCHI_KEY = @"InchiKey";
        private const string FORMULA = @"Formula";
        private const string ADDUCT = @"Adduct";
        // The user may type in the adduct without including brackets
        private const string ADDUCT_MINUS_BRACKETS = @"AdductMinusBrackets";

        private OrderedListCache _listCache;
        private string _selectedFilterCategory = UNMODIFIED_TARGET_TEXT;

        public ViewLibraryPepInfoList(IEnumerable<ViewLibraryPepInfo> items, LibKeyModificationMatcher matcher, string selectedFilterCategory, out bool allPeptides)
        {
            _allEntries = ImmutableList.ValueOf(items.OrderBy(item => item, Comparer<ViewLibraryPepInfo>.Create(ComparePepInfos)));
            _selectedFilterCategory = selectedFilterCategory;
            _matcher = matcher; // Used to calculate precursor m/z
            allPeptides = _allEntries.All(key => key.Key.IsProteomicKey); // Are there any non-proteomic entries in the library?
            _allPeptides = allPeptides;
            _accessionNumberTypes = FindMatchCategories();
            // If there are any small molecules in the library, search by multiple fields at once
            _stringSearchFields = !_allPeptides ?  // Fields of type string we want to compare to the search term
                new List<string> { UNMODIFIED_TARGET_TEXT, FORMULA, INCHI_KEY, ADDUCT, ADDUCT_MINUS_BRACKETS }
                : new List<string> { UNMODIFIED_TARGET_TEXT };

            _listCache = new OrderedListCache(_allEntries, _accessionNumberTypes);
            InitializeSearchVariables();
        }

        public override int Count
        {
            get { return _allEntries.Count; }
        }

        public override ViewLibraryPepInfo this[int index]
        {
            get { return _allEntries[index]; }
        }

        private void InitializeSearchVariables()
        {
            // Calculate the mz of each entry to search if we can
            foreach (var entry in _allEntries)
            {
                if (entry.Target != null && entry.PrecursorMz == 0)
                {
                    entry.PrecursorMz = ViewLibraryDlg.CalcMz(entry, _matcher);
                }
            }
        }

        /// <summary>
        /// Go through our list and find categories in which we could find matches
        /// </summary>
        private List<string> FindMatchCategories()
        {
            var matchCategories = new List<string>();
            foreach (var entry in _allEntries)
            {
                if (entry.OtherKeys != null)
                {
                    var accDict = MoleculeAccessionNumbers.FormatAccessionNumbers(entry.OtherKeys);
                    entry.OtherKeysDict = accDict;
                    foreach (var pair in accDict.Where(pair => !matchCategories.Contains(pair.Key)))
                    {
                        matchCategories.Add(pair.Key);
                    }
                }
            }

            return matchCategories;
        }

        private class OrderedListCache
        {
            Dictionary<string, ImmutableList<int>> _cache = new Dictionary<string, ImmutableList<int>>();
            private ImmutableList<ViewLibraryPepInfo> _pepInfos;
            private List<string> _accessionCategories;
            public OrderedListCache(ImmutableList<ViewLibraryPepInfo> allEntries, List<string> matchCategories)
            {
                _pepInfos = allEntries;
                _accessionCategories = matchCategories;
            }
            public ImmutableList<int> GetOrCreate(string propertyName)
            {
                if (!_cache.ContainsKey(propertyName))
                {
                    _cache[propertyName] = CreateItem(propertyName);
                }
                return _cache[propertyName];
            }

            private ImmutableList<int> CreateItem(string propertyName)
            {
                var intList = new RangeList(new Range(0, _pepInfos.Count)).ToList();
                if (propertyName.Equals(ADDUCT))
                {
                    // The adduct has a special sort
                    return ImmutableList.ValueOf(intList.OrderBy(index => index, Comparer<int>.Create(CompareAdductsFromIndices)));
                }
                
                if (_accessionCategories.Contains(propertyName))
                {
                    intList = intList.Where(index => _pepInfos[index].OtherKeysDict.ContainsKey(propertyName)).ToList();
                    return ImmutableList.ValueOf(intList.OrderBy(index =>
                        _pepInfos[index].OtherKeysDict.ContainsKey(propertyName)).ThenBy(index => _pepInfos[index].OtherKeysDict[propertyName]));
                }
                var property = typeof(ViewLibraryPepInfo).GetProperty(propertyName);
                return ImmutableList.ValueOf(intList.OrderBy(index => property.GetValue(_pepInfos[index]).ToString()));
            }
            private int CompareAdductsFromIndices(int adduct1, int adduct2)
            {
                return Adduct.Compare(_pepInfos[adduct1].Adduct, _pepInfos[adduct2].Adduct);
            }
        }

        /// <summary>
        /// Find entries whose beginning matches the filter text in the given property
        /// </summary>
        private List<int> PrefixSearchByProperty(string filterText)
        {
            Range matchRange;
            var orderedList = _listCache.GetOrCreate(_selectedFilterCategory);
            // If an accession number type is set as the filter category then filter using 
            if (_accessionNumberTypes.Contains(_selectedFilterCategory))
            {
                matchRange = CollectionUtil.BinarySearch(orderedList,
                    index => string.Compare( _allEntries[index].OtherKeysDict[_selectedFilterCategory], 0, filterText, 0,
                        filterText.Length, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                var property = typeof(ViewLibraryPepInfo).GetProperty(_selectedFilterCategory);
                matchRange = CollectionUtil.BinarySearch(orderedList,
                    info => string.Compare(property.GetValue(_allEntries[info]).ToString(), 0, filterText, 0,
                        filterText.Length,
                        StringComparison.OrdinalIgnoreCase));
            }

            return orderedList.Skip(matchRange.Start).Take(matchRange.Length).ToList();

        }


        /// <summary>
        /// Find the indices of entries matching the filter text according to the filter type
        /// </summary>
        /// <param name="filterText"> Search term </param>
        public IList<int> Filter(string filterText, string filterCategory)
        {
            _selectedFilterCategory = filterCategory;
            if (string.IsNullOrEmpty(filterText))
            {
                return new RangeList(0, Count);
            }

            // We have to deal with the UnmodifiedTargetText separately from the adduct because the
            // adduct has special sorting which is different than the way adduct.ToString() would sort.

            // Find the indices of entries that have a field that could match the search term if something was appended to it 
            var filteredIndices = PrefixSearchByProperty(filterText);

            // If the filter text can be read as a number, we want to include spectra with a matching precursor m/z value
            if (double.TryParse(filterText, NumberStyles.Any, CultureInfo.CurrentCulture, out var result))
            {
                // Add entries that are close to the filter text numerically
                // Create a list of object references sorted by their absolute difference from target m/z
                var sortedMzList = _allEntries.OrderBy(entry => Math.Abs(entry.PrecursorMz - result));

                // Then return everything before the first entry with a m/z difference exceeding our match tolerance
                var results = sortedMzList.TakeWhile(entry => !(Math.Abs(
                    entry.PrecursorMz - result) > MZ_FILTER_TOLERANCE)).Select(IndexOf).ToList();
                filteredIndices = filteredIndices.Union(results).ToList();
            }

            // If we have not found any matches yet and it is a peptide list look at all the entries which could match
            // the target text, if they had something appended to them.
            if (!filteredIndices.Any())
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
