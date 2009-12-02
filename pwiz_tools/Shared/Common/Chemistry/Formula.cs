using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using pwiz.Common.Collections;

namespace pwiz.Common.Chemistry
{
    public class Formula<T> : ImmutableDictionary<String, int>, IComparable<T>
        where T : Formula<T>, new()
    {
        public static readonly T Empty = new T { Dictionary = new SortedDictionary<string, int>() };
        public static T Parse(String formula)
        {
            var result = new SortedDictionary<String, int>();
            String currentElement = null;
            int currentQuantity = 0;
            for (int ich = 0; ich < formula.Length; ich++)
            {
                char ch = formula[ich];
                if (Char.IsDigit(ch))
                {
                    currentQuantity = currentQuantity * 10 + (ch - '0');
                }
                else if (Char.IsUpper(ch))
                {
                    if (currentElement != null)
                    {
                        if (currentQuantity == 0)
                        {
                            currentQuantity = 1;
                        }
                        if (result.ContainsKey(currentElement))
                        {
                            result[currentElement] = result[currentElement] + currentQuantity;
                        }
                        else
                        {
                            result[currentElement] = currentQuantity;
                        }
                    }
                    currentQuantity = 0;
                    currentElement = "" + ch;
                }
                else if (Char.IsLower(ch))
                {
                    currentElement = currentElement + ch;
                }
            }
            if (currentElement != null)
            {
                if (currentQuantity == 0)
                {
                    currentQuantity = 1;
                }
                if (result.ContainsKey(currentElement))
                {
                    result[currentElement] = result[currentElement] + currentQuantity;
                }
                else
                {
                    result[currentElement] = currentQuantity;
                }
            }
            return new T {Dictionary = result};
        }
        public T SetElementCount(String element, int count)
        {
            var dict = new SortedDictionary<String, int>(this);
            if (count == 0)
            {
                dict.Remove(element);
            }
            else
            {
                dict[element] = count;
            }
            return new T {Dictionary = dict};
        }
        public int GetElementCount(String element)
        {
            int atomCount;
            TryGetValue(element, out atomCount);
            return atomCount;
        }
        public override String ToString()
        {
            var result = new StringBuilder();
            foreach (var entry in this)
            {
                result.Append(entry.Key);
                if (entry.Value != 1)
                {
                    result.Append(entry.Value);
                }
            }
            return result.ToString();
        }
        public override int GetHashCode()
        {
            int result = 0;
            foreach (var entry in this)
            {
                result += entry.Key.GetHashCode() * entry.Value;
            }
            return result;
        }
        public override bool Equals(Object o)
        {
            if (o == this)
            {
                return true;
            }
            var that = o as T;
            if (that == null)
            {
                return false;
            }
            if (Count != that.Count)
            {
                return false;
            }
            foreach (var entry in this)
            {
                int thatValue;
                if (!that.TryGetValue(entry.Key, out thatValue))
                {
                    return false;
                }
                if (entry.Value != thatValue)
                {
                    return false;
                }
            }
            return true;
        }
        public int CompareTo(T that)
        {
            var thisEnumerator = GetEnumerator();
            var thatEnumerator = that.GetEnumerator();
            while (thisEnumerator.MoveNext())
            {
                if (!thatEnumerator.MoveNext())
                {
                    return 1;
                }
                int keyCompare = thisEnumerator.Current.Key.CompareTo(thatEnumerator.Current.Key);
                if (keyCompare != 0)
                {
                    return keyCompare;
                }
                int valueCompare = thisEnumerator.Current.Value.CompareTo(thatEnumerator.Current.Value);
                if (valueCompare != 0)
                {
                    return valueCompare;
                }
            }
            return thatEnumerator.MoveNext() ? -1 : 0;
        }
    }
}
