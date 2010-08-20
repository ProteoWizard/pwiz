using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using pwiz.Common.Collections;

namespace pwiz.Common.Chemistry
{
    public abstract class AbstractFormula<T,N> : ImmutableDictionary<String, N>, IComparable<T>
        where T : AbstractFormula<T, N>, new()
        where N : IComparable<N>
    {
        public static readonly T Empty = new T { Dictionary = new SortedDictionary<string, N>() };
        public override abstract string ToString();
        public virtual String ToDisplayString()
        {
            return ToString();
        }
        public T SetElementCount(String element, N count)
        {
            var dict = new SortedDictionary<String, N>(this);
            if (count.Equals(default(N)))
            {
                dict.Remove(element);
            }
            else
            {
                dict[element] = count;
            }
            return new T { Dictionary = dict };
        }
        public N GetElementCount(String element)
        {
            N atomCount;
            TryGetValue(element, out atomCount);
            return atomCount;
        }
        public override int GetHashCode()
        {
            int result = 0;
            foreach (var entry in this)
            {
                result += entry.Key.GetHashCode() * entry.Value.GetHashCode();
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
                N thatValue;
                if (!that.TryGetValue(entry.Key, out thatValue))
                {
                    return false;
                }
                if (!Equals(entry.Value, thatValue))
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
    public class Formula<T> : AbstractFormula<T, int>
        where T : Formula<T>, new()
    {
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
    }
}
