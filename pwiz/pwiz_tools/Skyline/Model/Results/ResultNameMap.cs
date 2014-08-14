using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Results
{
    public static class ResultNameMap
    {
        public static ResultNameMap<T> FromKeyValuePairs<T>(IEnumerable<KeyValuePair<string, T>> items)
        {
            return new ResultNameMap<T>(items);
        }
        public static ResultNameMap<T> FromDictionary<T>(IDictionary<string, T> dictionary)
        {
            return FromKeyValuePairs(dictionary);
        }
        public static ResultNameMap<T> FromNamedElements<T>(IEnumerable<T> namedElements) where T : XmlNamedElement
        {
            return FromKeyValuePairs(namedElements.Select(namedElement=>new KeyValuePair<string, T>(namedElement.Name, namedElement)));
        }
    }
    
    
    /// <summary>
    /// Stores a list of items in a way to enable quickly finding items which have a name which matches a data file.
    /// Data file names sometimes have extensions on the end of them, so the rules for comparing them are complex.
    /// <see cref="MeasuredResults.IsBaseNameMatch" />
    /// </summary>
    public class ResultNameMap<T> : IEnumerable<KeyValuePair<string, T>>
    {
        public static readonly ResultNameMap<T> EMPTY = new ResultNameMap<T>(new KeyValuePair<string, T>[0]);

        private readonly ImmutableSortedList<string, T> _sortedList;
        public ResultNameMap(IEnumerable<KeyValuePair<string, T>> items)
        {
            _sortedList = ImmutableSortedList.FromValues(RemoveDuplicates(items), NameComparer);
        }

        private static IEnumerable<KeyValuePair<string, T>> RemoveDuplicates(IEnumerable<KeyValuePair<string, T>> items)
        {
            var nameSet = new HashSet<string>(NameComparer);
            foreach (var item in items)
            {
                if (nameSet.Add(item.Key))
                {
                    yield return item;
                }
            }
        }

        private static StringComparer NameComparer { get { return StringComparer.InvariantCultureIgnoreCase; } }

        public T Find(string name)
        {
            if (name == null)
            {
                return default(T);
            }
            int searchedIndex = _sortedList.BinarySearch(name, true);
            if (searchedIndex >= 0)
            {
                return _sortedList.Values[searchedIndex];
            }

            var nameLower = ToLower(name);
            var bestResult = default(T);
            var bestName = string.Empty;
            int firstDotOrUnderscore = nameLower.Length;
            int indexOfDot = nameLower.IndexOf('.');
            if (indexOfDot >= 0)
            {
                firstDotOrUnderscore = indexOfDot;
            }
            if (MeasuredResults.IsUnderscoreSuffix(name))
            {
                firstDotOrUnderscore = Math.Min(firstDotOrUnderscore, nameLower.IndexOf('_'));
            }
            string minPrefix = nameLower.Substring(0, firstDotOrUnderscore);
            // We didn't find an exact match.
            
            // Check whether anything earlier in the list is a prefix for the name we're looking for
            for (int prevIndex = Math.Min(~searchedIndex - 1, _sortedList.Count - 1); prevIndex >= 0; prevIndex--)
            {
                var nameCompare = _sortedList.Keys[prevIndex];
                if (bestName.Length < nameCompare.Length && MeasuredResults.IsBaseNameMatch(name, nameCompare))
                {
                    bestName = nameCompare;
                    bestResult = _sortedList.Values[prevIndex];
                }
                else
                {
                    // Check to see if we can break out of this loop
                    var nameCompareLower = ToLower(nameCompare);
                    if (!nameCompareLower.StartsWith(minPrefix))
                    {
                        break;
                    }
                }
            }
            // Check whether anything later in the list has our name as a prefix
            for (int nextIndex = ~searchedIndex; nextIndex < _sortedList.Count; nextIndex++)
            {
                var nameCompare = _sortedList.Keys[nextIndex];
                if (bestName.Length < nameCompare.Length && MeasuredResults.IsBaseNameMatch(name, nameCompare))
                {
                    bestName = nameCompare;
                    bestResult = _sortedList.Values[nextIndex];
                }
                else
                {
                    var nameCompareLower = ToLower(nameCompare);
                    if (!nameLower.StartsWith(nameCompareLower))
                    {
                        break;
                    }
                }
            }
            return bestResult;
        }

        private static string ToLower(string s)
        {
            return s.ToLowerInvariant();
        }

        public T Find(ChromFileInfo chromFileInfo)
        {
            if (null == chromFileInfo)
            {
                return default(T);
            }
            string name = chromFileInfo.FilePath.GetFileNameWithoutExtension();
            return Find(name);
        }

        public T FindExact(string name)
        {
            int index = _sortedList.BinarySearch(name, true);
            if (index >= 0)
            {
                return _sortedList.Values[index];
            }
            return default(T);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<string, T>> GetEnumerator()
        {
            return _sortedList.GetEnumerator();
        }

        public int Count
        {
            get { return _sortedList.Count; }
        }

        public bool IsEmpty
        {
            get { return Count == 0; }
        }

        public IList<T> Values
        {
            get { return _sortedList.Values; }
        }

        public IList<string> Keys
        {
            get
            {
                return _sortedList.Keys;
            }
        }

        #region Object Overrides
		public bool Equals(ResultNameMap<T> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other._sortedList, _sortedList);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (ResultNameMap<T>)) return false;
            return Equals((ResultNameMap<T>) obj);
        }

        public override int GetHashCode()
        {
            return _sortedList.GetHashCode();
        }
        #endregion    
    }
}
