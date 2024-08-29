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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public class ViewLibraryPepInfoList : AbstractReadOnlyList<ViewLibraryPepInfo>
    {
        private readonly ImmutableList<ViewLibraryPepInfo> _allEntries; // Alpha sorted
        private readonly ImmutableList<int> _naturalSortIndex; // Natural sort order of the members of _allEntries
        private readonly LibKeyModificationMatcher _matcher;
        public bool _mzCalculated;
        public readonly List<string> _stringSearchFields;

        public readonly List<string> _accessionNumberTypes;
        // Tolerance for matching fields with continuous values to the filter text
        private const double FILTER_TOLERANCE = 0.1;

        // String names for properties
        private const string PRECURSOR_MZ = @"PrecursorMz";
        private const string UNMODIFIED_TARGET_TEXT = @"UnmodifiedTargetText";
        private const string INCHI_KEY = @"InchiKey";
        private const string FORMULA = @"Formula";
        private const string ADDUCT = @"AdductAsFormula";
        private const string ION_MOBILITY = @"IonMobility";
        private const string CCS = @"CCS";
        private const string CHARGE = @"Charge";
        
        // Fields for which we should include a match tolerance
        private readonly List<string> _continuousFields = new List<string> {PRECURSOR_MZ, CCS, ION_MOBILITY};

        // Property names and the resources we use to display them
        public readonly Dictionary<string, string> comboFilterCategoryDict = new Dictionary<string, string>()
        {
            {PRECURSOR_MZ, Resources.PeptideTipProvider_RenderTip_Precursor_m_z},
            {INCHI_KEY, Resources.SmallMoleculeLibraryAttributes_KeyValuePairs_InChIKey},
            {FORMULA, Resources.SmallMoleculeLibraryAttributes_KeyValuePairs_Formula},
            {ADDUCT, Resources.EditIonMobilityLibraryDlg_EditIonMobilityLibraryDlg_Adduct},
            {ION_MOBILITY, Resources.PeptideTipProvider_RenderTip_Ion_Mobility},
            {CCS, Resources.PeptideTipProvider_RenderTip_CCS},
            {CHARGE, Resources.TransitionTreeNode_RenderTip_Charge}
        };

        private OrderedListCache _listCache;
        private string _selectedFilterCategory;

        public ViewLibraryPepInfoList(IEnumerable<ViewLibraryPepInfo> items, LibKeyModificationMatcher matcher, string selectedFilterCategory, out bool allPeptides)
        {
            _allEntries = ImmutableList.ValueOf(items.OrderBy(item => item, Comparer<ViewLibraryPepInfo>.Create(ComparePepInfos)));
            var naturalSort = Enumerable.Range(0, _allEntries.Count).OrderBy(i => MakeCompareKey(_allEntries[i])).ToArray();
            var naturalSortMap = new int[_allEntries.Count];
            for (var order = 0; order < naturalSort.Length; order++)
            {
                naturalSortMap[naturalSort[order]] = order;
            }
            _naturalSortIndex = ImmutableList.ValueOf(naturalSortMap);
            _selectedFilterCategory = selectedFilterCategory;
            _matcher = matcher; // Used to calculate precursor m/z
            allPeptides = _allEntries.All(key => key.Key.IsProteomicKey); // Are there any non-proteomic entries in this library?
            _accessionNumberTypes = FindAccessionNumberTypes();

            // First add the fields we will display in all libraries
            _stringSearchFields = new List<string> {UNMODIFIED_TARGET_TEXT, PRECURSOR_MZ};

            // Then see which other fields we can offer
            // If there are any small molecules in the library we will check more fields
            _stringSearchFields.AddRange(
                FindValidCategories(!allPeptides ? 
                (new List<string> { FORMULA, INCHI_KEY, CHARGE, ADDUCT, ION_MOBILITY, CCS })
                : new List<string> { CHARGE, ION_MOBILITY, CCS }));

            _listCache = new OrderedListCache(_allEntries, _accessionNumberTypes);

            // Display "Peptide" if the library is all peptides, or "Name" if it is not
            comboFilterCategoryDict.Add(UNMODIFIED_TARGET_TEXT, allPeptides ? ColumnCaptions.Peptide : Resources.SmallMoleculeLibraryAttributes_KeyValuePairs_Name);

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
        /// Calculate the precursor m/z of every entry with the option to cancel
        /// </summary>
        public void CalculateEveryMz(IProgressMonitor progressMonitor)
        {
            _mzCalculated = true;
            foreach(var entry in _allEntries)
            {
                if (entry.Key.IsPrecursorKey)
                {
                    entry.PrecursorMz = entry.Key.PrecursorMz.GetValueOrDefault();
                }
                else if (entry.Target != null)
                {
                    entry.PrecursorMz = entry.CalcMz(_matcher);
                }
                if (progressMonitor.IsCanceled)
                {
                    _mzCalculated = false;
                    break;
                }
            }
        }

        /// <summary>
        /// Find categories for which at least one entry has a value
        /// </summary>
        private List<string> FindValidCategories(List<string> categories)
        {
            return categories.Where(category => _allEntries.Any(entry => !GetStringValue(category, entry).Equals(string.Empty))).ToList();
        }

        /// <summary>
        /// Find the string value of a property for a ViewLibraryPepInfo, formatted as in user display
        /// </summary>
        internal static string GetFormattedPropertyValue(string propertyName, ViewLibraryPepInfo pepInfo)
        {
            var property = typeof(ViewLibraryPepInfo).GetProperty(propertyName);
            if (property is null)
            {
                return string.Empty;
            }

            var value = property.GetValue(pepInfo);
            if (value is null)
            {
                return string.Empty;
            }

            string propertyValue;
            var dbl = value as double? ?? 0;

            // Shorten precursor m/z values to be uniform and match the tool tip
            if (propertyName.Equals(PRECURSOR_MZ))
            {
                propertyValue = ViewLibraryDlg.FormatPrecursorMz(dbl);
            }
            else if (propertyName.Equals(CCS))
            {
                propertyValue = ViewLibraryDlg.FormatCCS(dbl);
            }
            else if (propertyName.Equals(ION_MOBILITY))
            {
                propertyValue = ViewLibraryDlg.FormatIonMobility(dbl, pepInfo.IonMobilityUnits);
            }
            else
            {
                propertyValue = value.ToString();
            }

            return propertyValue;
        }

        /// <summary>
        /// Find the string value of a property for a ViewLibraryPepInfo
        /// </summary>
        public static string GetStringValue(string propertyName, ViewLibraryPepInfo pepInfo)
        {
            var property = typeof(ViewLibraryPepInfo).GetProperty(propertyName);
            if (!(property is null))
            {
                var value = property.GetValue(pepInfo);
                if (!(value is null))
                {
                    return value.ToString();
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Create a list of entry indices sorted by the given property
        /// </summary>
        public void CreateCachedList(string propertyName)
        {
            _selectedFilterCategory = comboFilterCategoryDict.ContainsValue(propertyName)
                ? comboFilterCategoryDict.FirstOrDefault(x => x.Value == propertyName).Key
                : propertyName;
            _listCache.GetOrCreate(_selectedFilterCategory);
        }

        /// <summary>
        /// Find accession number types like cas and HMDB that can be used to filter
        /// </summary>
        private List<string> FindAccessionNumberTypes()
        {
            var matchCategories = new HashSet<string>();
            foreach (var entry in _allEntries)
            {
                if (entry.OtherKeys != null)
                {
                    var accDict = MoleculeAccessionNumbers.FormatAccessionNumbers(entry.OtherKeys);
                    entry.OtherKeysDict = accDict;
                    foreach (var pair in accDict)
                    {
                        matchCategories.Add(pair.Key);
                    }
                }
            }

            return matchCategories.ToList();
        }

        /// <summary>
        /// For storing sorted lists of entry indices
        /// </summary>
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
                if (propertyName.Equals(UNMODIFIED_TARGET_TEXT))
                {
                    // The list of entries is already sorted by this field, so don't bother sorting it again
                    return ImmutableList.ValueOf(intList);
                }

                if (_accessionCategories.Contains(propertyName))
                {
                    // Narrow the list down to entries that actually contain the property
                    intList = intList.Where(index => _pepInfos[index].OtherKeysDict != null).Where(index => _pepInfos[index].OtherKeysDict.ContainsKey(propertyName)).ToList();
                    return ImmutableList.ValueOf(intList.OrderBy(index =>
                        _pepInfos[index].OtherKeysDict.ContainsKey(propertyName)).ThenBy(index => _pepInfos[index].OtherKeysDict[propertyName]));
                }

                intList = (from index in intList let entry = _pepInfos[index] where !GetStringValue(propertyName, entry).Equals("") && !GetStringValue(propertyName, entry).Equals(@"0") select index).ToList();
                return ImmutableList.ValueOf(intList.OrderBy(index => GetStringValue(propertyName, _pepInfos[index]), StringComparer.OrdinalIgnoreCase));
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
                matchRange = CollectionUtil.BinarySearch(orderedList,
                    index => string.Compare(GetStringValue(_selectedFilterCategory, _allEntries[index]), 0, filterText, 0,
                        filterText.Length,
                        StringComparison.OrdinalIgnoreCase));
            }

            return orderedList.Skip(matchRange.Start).Take(matchRange.Length).ToList();

        }

        /// <summary>
        /// Find entries which contain the filter text in the given property
        /// </summary>
        private List<int> ContainsSearchByProperty(string filterText)
        {
            if (string.IsNullOrEmpty(filterText))
            {
                return new List<int>();
            }
            var orderedList = _listCache.GetOrCreate(_selectedFilterCategory); // List of indexes of items that have values for property of interest
            IEnumerable<int> matches;
            if (_accessionNumberTypes.Contains(_selectedFilterCategory))
            {
                matches = orderedList.Where(item => _allEntries[item].OtherKeysDict[_selectedFilterCategory]
                    .IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            else
            {
                matches = orderedList.Where(item => GetFormattedPropertyValue(_selectedFilterCategory, _allEntries[item])
                    .IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            return matches.ToList();
        }

        /// <summary>
        /// Find the indices of entries matching the filter text according to the filter type
        /// </summary>
        public IList<int> Filter(string filterText, string filterCategory)
        {
            _selectedFilterCategory = comboFilterCategoryDict.ContainsValue(filterCategory)
                ? comboFilterCategoryDict.FirstOrDefault(x => x.Value == filterCategory).Key
                : filterCategory;

            if (string.IsNullOrEmpty(filterText))
            {
                // Only return entries that have the selected property
                var ret = _listCache.GetOrCreate(_selectedFilterCategory).OrderBy(info => _naturalSortIndex[info]);
                return ImmutableList.ValueOf(ret);
            }

            // We have to deal with the UnmodifiedTargetText separately from the adduct because the
            // adduct has special sorting which is different than the way adduct.ToString() would sort.

            List<int> filteredIndices;
            var isWildcard = filterText.StartsWith(@"*");
            if (isWildcard)
            {
                filterText = filterText.Substring(1);
            }
            if (isWildcard && filterText.Length>0)
            {
                // For strings starting in "*", find the indices of entries that have a field
                // that contain the search term
                filteredIndices = ContainsSearchByProperty(filterText);
            }
            else
            {
                // Otherwise, find the indices of entries that have a field that could match the search term
                // if something was appended to it 
                filteredIndices = PrefixSearchByProperty(filterText);

                // Special filtering for numeric properties
                if (double.TryParse(filterText, NumberStyles.Any, CultureInfo.CurrentCulture, out var result) && _continuousFields.Contains(_selectedFilterCategory))
                {
                    // Add entries that are close to the filter text numerically
                    // Create a list of object references sorted by their absolute difference from target
                    var sortedByDifference = _allEntries.OrderBy(entry => Math.Abs(double.Parse(GetStringValue(_selectedFilterCategory, entry), NumberStyles.Any, CultureInfo.CurrentCulture) - result));

                    // Then return everything before the first entry with a difference exceeding our match tolerance
                    var results = sortedByDifference.TakeWhile(entry => !(Math.Abs(
                        double.Parse(GetStringValue(_selectedFilterCategory, entry), NumberStyles.Any, CultureInfo.CurrentCulture) - result) >  FILTER_TOLERANCE)).Select(IndexOf).ToList();
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
                    filteredIndices = new RangeList(range).Where(i => _allEntries[i].DisplayText
                        .StartsWith(filterText, StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }
            // Return the indices of the matches sorted alphabetically by display text
            return ImmutableList.ValueOf(filteredIndices.OrderBy(info => _naturalSortIndex[info]));
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

        // Simple alphanumeric sort
        public static int ComparePepInfos(ViewLibraryPepInfo info1, ViewLibraryPepInfo info2)
        {
            int result = string.Compare(info1.UnmodifiedTargetText, info2.UnmodifiedTargetText,
                StringComparison.OrdinalIgnoreCase);
            if (result == 0)
            {
                // Same molecule, sort by adduct
                result = info1.Adduct.CompareTo(info2.Adduct);
                if (result == 0)
                {
                    // Last try, alpha sort on whatever Key.ToString() produced
                    result = string.Compare(info1.KeyString, info2.KeyString, StringComparison.OrdinalIgnoreCase);
                }
            }
            return result;
        }

        // Natural sort (e.g. "xyz200.5" comes before "xyz1200.5")
        public static Tuple<NaturalStringComparer.CompareKey, Adduct, NaturalStringComparer.CompareKey> MakeCompareKey(
            ViewLibraryPepInfo info)
        {
            return Tuple.Create(NaturalStringComparer.MakeCompareKey(info.UnmodifiedTargetText), info.Adduct,
                NaturalStringComparer.MakeCompareKey(info.KeyString));
        }
    }
}
