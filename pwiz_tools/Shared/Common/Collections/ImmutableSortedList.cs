using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.Collections
{
    public static class ImmutableSortedList
    {
        public static ImmutableSortedList<TKey, TValue> FromValues<TKey,TValue>(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs, IComparer<TKey> keyComparer)
        {
            return ImmutableSortedList<TKey, TValue>.FromValues(keyValuePairs, keyComparer);
        }
        public static ImmutableSortedList<TKey, TValue> FromValues<TKey,TValue>(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
        {
            return FromValues(keyValuePairs, Comparer<TKey>.Default);
        }
    }
    public class ImmutableSortedList<TKey, TValue> : IList<KeyValuePair<TKey, TValue>>
    {
        private TKey[] _keys;

        public static ImmutableSortedList<TKey, TValue> FromValues(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs, IComparer<TKey> keyComparer)
        {
            var array = keyValuePairs.ToArray();
            Array.Sort(array, (kvp1,kvp2)=>keyComparer.Compare(kvp1.Key, kvp2.Key));
            return new ImmutableSortedList<TKey, TValue>
                       {
                           KeyComparer = keyComparer,
                           _keys = array.Select(kvp=>kvp.Key).ToArray(), 
                           Values = ImmutableList.ValueOf(array.Select(kvp=>kvp.Value))
                       };
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return new KeyValuePair<TKey, TValue>(Keys[i],Values[i]);
            }
        }

        public IList<TKey> Keys
        {
            get
            {
                return Array.AsReadOnly(_keys);
            }
        }

        public IList<TValue> Values
        {
            get;private set;
        }

        public int BinarySearch(TKey key, bool firstIndex)
        {
            int lo = 0;
            int hi = Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;

                int c = KeyComparer.Compare(Keys[mid], key);
                if (c == 0)
                {
                    if (lo == hi)
                    {
                        return lo;
                    }
                    if (firstIndex)
                    {
                        hi = mid;
                    }
                    else
                    {
                        lo = mid;
                    }
                }
                else if (c < 0)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return ~lo;
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return IndexOf(item) > 0;
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            for (int i = 0; i < Count; i++)
            {
                array.SetValue(this[i], arrayIndex + i);
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new NotSupportedException();
        }

        public int Count
        {
            get { return _keys.Length; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public int IndexOf(KeyValuePair<TKey, TValue> item)
        {
            int index = BinarySearch(item.Key, true);
            if (index < 0)
            {
                return -1;
            }
            while (true)
            {
                if (Equals(Values[index], item.Value))
                {
                    return index;
                }
                index++;
                if (index >= Count || !Equals(Keys[index], item.Key))
                {
                    return -1;
                }
            }
        }

        public void Insert(int index, KeyValuePair<TKey, TValue> item)
        {
            throw new NotSupportedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        public KeyValuePair<TKey, TValue> this[int index]
        {
            get { return new KeyValuePair<TKey, TValue>(Keys[index],Values[index]); }
            set { throw new NotSupportedException(); }
        }

        public IComparer<TKey> KeyComparer { get; private set; }
    }
}