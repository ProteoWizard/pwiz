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
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.SettingsUI
{
    public class ViewLibraryPepInfoList : AbstractReadOnlyList<ViewLibraryPepInfo>
    {
        private readonly ImmutableList<ViewLibraryPepInfo> _allEntries;
        public ViewLibraryPepInfoList(IEnumerable<ViewLibraryPepInfo> items)
        {
            _allEntries = ImmutableList.ValueOf(items.OrderBy(item=>item, Comparer<ViewLibraryPepInfo>.Create(ComparePepInfos)));
        }

        public override int Count
        {
            get { return _allEntries.Count; }
        }

        public override ViewLibraryPepInfo this[int index]
        {
            get { return _allEntries[index]; }
        }

        private List<int> SearchByProperty(PropertyInfo property, string filterText)
        {
            var orderedList = _allEntries.OrderBy(property.GetValue).ToList();
            var matchRange = CollectionUtil.BinarySearch(orderedList,
                info => string.Compare(property.GetValue(info).ToString(), 0, filterText, 0, filterText.Length,
                    StringComparison.OrdinalIgnoreCase));
            return orderedList.Skip(matchRange.Start).Take(matchRange.Length).Select(item => _allEntries.IndexOf(item)).ToList();
        }
        public IList<int> Filter(string filterText, ViewLibraryDlg.FilterType filterType = ViewLibraryDlg.FilterType.startsWith)
        {
            if (string.IsNullOrEmpty(filterText))
            {
                return new RangeList(0, Count);
            }
            // We have to deal with the UnmodifiedTargetText separately from the adduct because the
            // adduct has special sorting which is different than the way adduct.ToString() would sort.

            // First see if there are any entries whose UnmodifiedTargetText alone matches the entire filter string
            var range = CollectionUtil.BinarySearch(_allEntries, info => string.Compare(info.UnmodifiedTargetText, 0, filterText, 0, filterText.Length, StringComparison.OrdinalIgnoreCase));
            if (range.Length > 0)
            {
                return new RangeList(range);
            }

            if (true)
            {
                if (filterType == ViewLibraryDlg.FilterType.contains)
                {
                    var ret = (from entry in _allEntries
                        let combinedFields = entry.UnmodifiedTargetText + "\0" + entry.Formula + "\0" + entry.InchiKey
                        where combinedFields.Contains(filterText) select IndexOf(entry)).ToList();

                    return ImmutableList.ValueOf(ret);
                }
                else if(filterType == ViewLibraryDlg.FilterType.startsWith)
                {

                    // Fields of type string we want to compare to the search term
                    var stringSearchFields = new List<string> { @"Formula", @"InchiKey", @"UnmodifiedTargetText" };

                    // Find the indices of entries that have field that could match the search term if something was appended to it 
                    var rangeList = Enumerable.Empty<int>();
                    rangeList = stringSearchFields.Aggregate(rangeList, (current, str) =>
                        current.Union(SearchByProperty(typeof(ViewLibraryPepInfo).GetProperty(str), filterText)));

                    return ImmutableList.ValueOf(rangeList);
                }
            }
            // Now look at all the entries which could match the target text, if they had something appended to them.
            range = CollectionUtil.BinarySearch(_allEntries,
                info => string.Compare(info.UnmodifiedTargetText, 0, filterText, 0, info.UnmodifiedTargetText.Length,
                    StringComparison.OrdinalIgnoreCase));
            // Return the elements from the range whose DisplayText actually matches the filter text.
            return ImmutableList.ValueOf(new RangeList(range).Where(i => _allEntries[i].DisplayText
                .StartsWith(filterText, StringComparison.OrdinalIgnoreCase)));
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
