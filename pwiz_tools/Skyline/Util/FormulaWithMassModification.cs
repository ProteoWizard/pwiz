/*
 * Original author: Brian Pratt <bspratt .at. protein.ms>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Based on Formula class in this same namespace, this adds the ability to describe a mass
    /// offset of unknown origin.
    /// </summary>
    public class FormulaWithMassModification : IDictionary<string, int>, IComparable
    {
        // ReSharper disable InconsistentNaming
        public static readonly FormulaWithMassModification Empty = new FormulaWithMassModification { Dictionary = ImmutableSortedList<string, int>.EMPTY };
        // ReSharper disable StaticMemberInGenericType

        // For describing mass modifications e.g. [+1.2] or [+1.2/1.21] or [-2.3] etc
        public static readonly char MASS_MOD_START_CH = '[';
        public static readonly char MASS_MOD_END_CH = ']';
        public static readonly string MASS_MOD_START = @"[";
        public static readonly string MASS_MOD_START_PLUS = @"[+";
        public static readonly string MASS_MOD_START_MINUS = @"[-";
        // ReSharper restore StaticMemberInGenericType

        // ReSharper restore InconsistentNaming
        private int _hashCode;
        private ImmutableSortedList<string, int> _dict;
        public virtual String ToDisplayString()
        {
            return ToString();
        }

        public FormulaWithMassModification SetElementCount(string element, int count)
        {
            if (count.Equals(0) && Dictionary.ContainsKey(element))
            {
                return new FormulaWithMassModification { Dictionary = Dictionary.RemoveKey(element)};
            }

            TryGetValue(element, out var currentCount);
            return count == currentCount ? 
                this :
                new FormulaWithMassModification { Dictionary = Dictionary.Replace(element, count) };
        }

        public int GetElementCount(String element)
        {
            if (!TryGetValue(element, out var atomCount))
            {
                atomCount = 0;
            }
            return atomCount;
        }

        public override int GetHashCode()
        {
// ReSharper disable NonReadonlyFieldInGetHashCode
            return _hashCode;
// ReSharper restore NonReadonlyFieldInGetHashCode
        }

        public override bool Equals(Object o)
        {
            if (ReferenceEquals(o, this))
            {
                return true;
            }

            if (!(o is FormulaWithMassModification that))
            {
                return false;
            }
            return Dictionary.Equals(that.Dictionary);
        }

        public int CompareTo(object o)
        {
            if (ReferenceEquals(this, o))
            {
                return 0;
            }
            var that = (FormulaWithMassModification)o;
            if (that == null)
            {
                return 1;
            }

            using (var thisEnumerator = GetEnumerator())
            using (var thatEnumerator = that.GetEnumerator())
            {
                while (thisEnumerator.MoveNext())
                {
                    if (!thatEnumerator.MoveNext())
                    {
                        return 1;
                    }
                    int keyCompare = string.CompareOrdinal(thisEnumerator.Current.Key, thatEnumerator.Current.Key);
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
        public ImmutableSortedList<string, int> Dictionary
        {
            get => _dict;
            protected set 
            { 
                _dict = value;
                if (_dict.Values.Contains(default(int)))
                {
                    // Zeroes should have been filtered out before getting here.
                    throw new ArgumentException($@"The formula {this} cannot contain zero");
                }
                _hashCode = value.GetHashCode();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<string, int>> GetEnumerator()
        {
            return Dictionary.GetEnumerator();
        }
        public int Count => Dictionary.Count;

        public bool TryGetValue(string key, out int value)
        {
            return Dictionary.TryGetValue(key, out value);
        }
        public ICollection<string> Keys => Dictionary.Keys;
        public ICollection<int> Values => Dictionary.Values;

        public int this[string key]
        {
            get 
            { 
                int value;
                TryGetValue(key, out value);
                return value;
            }
            set => throw new InvalidOperationException();
        }

        public void Add(KeyValuePair<string, int> item)
        {
            throw new InvalidOperationException();
        }

        public void Clear()
        {
            throw new InvalidOperationException();
        }

        public bool Contains(KeyValuePair<string, int> item)
        {
            return Dictionary.Contains(item);
        }

        public bool Remove(KeyValuePair<string, int> item)
        {
            throw new InvalidOperationException();
        }

        public void CopyTo(KeyValuePair<string, int>[] array, int arrayIndex)
        {
            Dictionary.CopyTo(array, arrayIndex);
        }

        public bool IsReadOnly => true;

        public bool ContainsKey(string key)
        {
            return Dictionary.ContainsKey(key);
        }

        public void Add(string key, int value)
        {
            throw new InvalidOperationException();
        }

        public bool Remove(string key)
        {
            throw new InvalidOperationException();
        }

        public static FormulaWithMassModification Parse(String formula)
        {
            return new FormulaWithMassModification {Dictionary = ImmutableSortedList.FromValues(ParseToDictionary(formula))};
        }

        public static Dictionary<string, int> ParseToDictionary(string formula)
        {
            var result = new Dictionary<string, int>();
            string currentElement = null;
            int? currentQuantity = null;
            foreach (char ch in formula)
            {
                if (currentElement != null && currentElement.StartsWith(MASS_MOD_START))
                {
                    // Parsing a mass modification
                    currentElement = currentElement + ch;
                    if (ch == MASS_MOD_END_CH)
                    {
                        result.Add(currentElement, 1);
                        currentElement = null;
                    }
                }
                else if (Char.IsDigit(ch))
                {
                    currentQuantity = (currentQuantity??0) * 10 + (ch - '0');
                }
                else if (Char.IsUpper(ch) || (ch == MASS_MOD_START_CH))
                {
                    // Close out current element, if any
                    if (currentElement != null)
                    {
                        int previousAtomCount;
                        var currentAtomCount = currentQuantity ?? 1; // No count declared implies 1
                        if (result.TryGetValue(currentElement, out previousAtomCount))
                        {
                            result[currentElement] = previousAtomCount + currentAtomCount;
                        }
                        else if (currentAtomCount != 0) // Beware explicitly declared 0 count
                        {
                            result.Add(currentElement, previousAtomCount + currentAtomCount);
                        }
                    }
                    currentQuantity = null;
                    currentElement = string.Empty + ch;
                }
                // Allow apostrophe for heavy isotopes (e.g. C' for 13C)
                else if (!Char.IsWhiteSpace(ch))
                {
                    currentElement = currentElement + ch;
                }
            }
            if (currentElement != null)
            {
                int previousAtomCount;
                var currentAtomCount = currentQuantity ?? 1; // No count declared implies 1
                if (result.TryGetValue(currentElement, out previousAtomCount))
                {
                    result[currentElement] = previousAtomCount + currentAtomCount;
                }
                else if (currentAtomCount != 0) // Beware explicitly declared 0 count
                {
                    result.Add(currentElement, currentAtomCount);
                }
            }
            return result;
        }

        // Parse a mass modification e.g. e.g. [+1.23] or [-0.256] or mono,average like [+1.23/1.24] or [-0.256/0.257]
        public static double ParseMassModification(string sym, bool getAverageMass)
        {
            if (sym.StartsWith(MASS_MOD_START))
            {
                var pair = sym.Substring(1, sym.Length - 2).Split('/');
                if (double.TryParse(pair[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var monoMass))
                {
                    if (pair.Length == 1 || !getAverageMass)
                    {
                        return monoMass;
                    }
                    if (double.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var massAverage))
                    {
                        return (monoMass < 0) ? -massAverage : massAverage; // The sign is implied on second half of pair
                    }
                    return monoMass;
                }
            }
            return 0;
        }

        // Handle formulae which may contain subtractions, as is deprotonation description ie C12H8O2-H (=C12H7O2) or even C12H8O2-H2O (=C12H6O)
        public static FormulaWithMassModification ParseExpression(String formula)
        {
            return FromDict(ParseExpressionToDictionary(formula));
        }

        public static Dictionary<String, int> ParseExpressionToDictionary(string expression)
        {
            var parts = expression.Split('-');
            for (var i = 0; i < parts.Length;) // Make sure we didn't hit a mass modification by accident e.g. C12H5[-0.33]
            {
                if (parts[i].EndsWith(MASS_MOD_START))
                {
                    // Oops we split C12H5[-0.33]3H-2C into C12H5[ and 0.33]3H and 2C - undo that
                    parts[i] +=  @"-";
                    if (i < parts.Length - 1)
                    {
                        parts[i] += parts[i+1];
                        var list = parts.ToList();
                        list.RemoveRange(i+1,1);
                        parts = list.ToArray();
                    }
                }
                else
                {
                    i++;
                }
            }
            if (parts.Length > 2)
            {
                throw new ArgumentException(@"Molecular formula subtraction expressions are limited a single operation");
            }
            var result = ParseToDictionary(parts[0]);
            if (parts.Length > 1)
            {
                var subtractive = ParseToDictionary(parts[1]);
                foreach (var element in subtractive)
                {
                    int previous;
                    if (result.TryGetValue(element.Key, out previous))
                    {
                        int newCount = previous - element.Value;
                        if (newCount == 0)
                        {
                            result.Remove(element.Key);
                        }
                        else
                        {
                            result[element.Key] = newCount;
                        }
                    }
                    else
                    {
                        result.Add(element.Key, -element.Value);  // Seems weird, but possibly describing a proton lost from something other that H?
                    }
                }
            }

            // Deal with mass modifications - combine if more than one, ensure positive count if only one
            RegularizeMassModifications(result);
            return result;
        }

        /// <summary>
        /// Return true if symbol is found in mass table, or it's understood as a mass modification e.g. [+1.2/1.21]
        /// </summary>
        /// <param name="sym"></param>
        /// <returns></returns>
        public static bool IsKnownSymbol(string sym)
        {
            return BioMassCalc.AVERAGE.IsKnownSymbol(sym) || // Known symbol, e.g. Cl, O etc
                   sym.StartsWith(MASS_MOD_START_PLUS) || sym.StartsWith(MASS_MOD_START_MINUS); // Looks like a mass modification
        }

        public static void AddMassModification(IDictionary<string, int> result, double modMass)
        {
            var massModStr = MoleculeMassOffset.FormatMassModification(modMass);
            if (result.TryGetValue(massModStr, out var exist))
            {
                result[massModStr] = exist + 1;
            }
            else
            {
                result.Add(massModStr, 1);
            }
            RegularizeMassModifications(result); // Combine with any existing mass modifications
        }

        // Tidy up any mass modifications (e.g. [+2.45], [-3.55] etc) in the dictionary - combine if more than one, ensure positive count if only one
        public static void RegularizeMassModifications(IDictionary<string, int> result)
        {
            var nMassMods = result.Keys.Count(k => k.StartsWith(MASS_MOD_START));
            if (nMassMods == 1)
            {
                var kvp = result.First(k => k.Key.StartsWith(MASS_MOD_START));
                if (kvp.Value < 0)
                {
                    // Just invert polarity
                    var newKey = kvp.Key.Contains(@"+") ? kvp.Key.Replace(@"+", @"-") : kvp.Key.Replace(@"-", @"+");
                    result.Add(newKey, -kvp.Value);
                    result.Remove(kvp.Key);
                }
            }
            else if (nMassMods > 1)
            {
                var massValueMono = 0.0;
                var massValueAverage = 0.0;
                var observedDecimals = 1; // Always show at least one decimal place
                foreach (var kvp in result.Where(kvp => kvp.Key.StartsWith(MASS_MOD_START)).ToArray())
                {
                    var decimals = (kvp.Key.Length - kvp.Key.LastIndexOf('.')) - 2; // Omit . and ] in the count
                    observedDecimals = Math.Max(decimals, observedDecimals);
                    massValueMono += ParseMassModification(kvp.Key, false) * kvp.Value;
                    massValueAverage += ParseMassModification(kvp.Key, true) * kvp.Value;
                    result.Remove(kvp.Key);
                }

                var newMassMod = MoleculeMassOffset.FormatMassModification(massValueMono, massValueAverage, observedDecimals);
                result.Add(newMassMod, 1);
            }
        }

        public bool HasMassModifications => Keys.Any(k => k.StartsWith(MASS_MOD_START));

        // Subtract other's atom counts from ours
        public FormulaWithMassModification Difference(FormulaWithMassModification other)
        {
            var resultDict = new Dictionary<string, int>(this);
            foreach (var kvp in other)
            {
                TryGetValue(kvp.Key, out var count);
                count -= kvp.Value;
                if (count == 0)
                {
                    resultDict.Remove(kvp.Key);
                }
                else
                {
                    resultDict[kvp.Key] = count;
                }
            }

            // Deal with mass modifications - combine if more than one, ensure positive count if only one
            RegularizeMassModifications(resultDict);

            return FromDict(resultDict);
        }

        public FormulaWithMassModification WithoutMassModifications => new FormulaWithMassModification {Dictionary = ImmutableSortedList.FromValues(this.Where(entry=>!entry.Key.StartsWith(MASS_MOD_START)))};

        public double GetMonoMassOffset()
        {
            double mass = 0;
            foreach (var kvp in this)
            {
                mass += ParseMassModification(kvp.Key, false) * kvp.Value;
            }
            return mass;
        }

        public double GetAverageMassOffset()
        {
            double mass = 0;
            foreach (var kvp in this)
            {
                mass += ParseMassModification(kvp.Key, true) * kvp.Value;
            }
            return mass;
        }

        public static FormulaWithMassModification FromDict(IDictionary<string, int> dict)
        {
            return new FormulaWithMassModification {Dictionary = ImmutableSortedList.FromValues(dict.Where(entry=>entry.Value != 0))};
        }

        public static string AdjustElementCount(string formula, string element, int delta)
        {
            var dict = ParseToDictionary(formula);
            int count;
            if (!dict.TryGetValue(element, out count))
                count = 0;
            if ((count > 0) || (delta > 0)) // There are some to take away, or we're planning to add some
            {
                count += delta;
                if (count >= 0)
                {
                    dict[element] = count;
                    return FromDict(dict).ToString();
                }
            }
            return formula;
        }

        public static bool AreEquivalentFormulas(string formulaLeft, string formulaRight)
        {
            if (string.Equals(formulaLeft, formulaRight))
            {
                return true;
            }
            // Consider C2C'4H5 to be same as H5C'4C2, or "C10H30Si5O5H-CH4" same as "C9H26O5Si5", etc
            var left = ParseExpression(formulaLeft);
            var right = ParseExpression(formulaRight);
            return left.Equals(right);
        }

        public override string ToString()
        {
            var result = new StringBuilder();
            bool anyNegative = false;
            foreach (var entry in this)
            {
                if (entry.Value >= 0)
                {
                    if (entry.Key.StartsWith(MASS_MOD_START))
                    {
                        continue; // Move mass modifications to end of string
                    }
                    result.Append(entry.Key);
                    if (entry.Value != 1)
                    {
                        result.Append(entry.Value);
                    }
                }
                else
                {
                    anyNegative = true;
                }
            }

            foreach (var entry in this.Where(k => k.Key.StartsWith(MASS_MOD_START) && k.Value > 0))
            {
                result.Append(entry.Key);
            }

            if (!anyNegative)
            {
                return result.ToString();
            }

            result.Append(@"-");
            foreach (var entry in this)
            {
                if (entry.Value < 0)
                {
                    if (entry.Key.StartsWith(MASS_MOD_START))
                    {
                        continue; // Move mass modifications to end of string
                    }
                    result.Append(entry.Key);
                    if (entry.Value != -1)
                    {
                        result.Append(-entry.Value);
                    }
                }
            }
            foreach (var entry in this.Where(k => k.Key.StartsWith(MASS_MOD_START) && k.Value < 0))
            {
                result.Append(entry.Key.Contains(@"+") ? entry.Key.Replace(@"+", @"-") : entry.Key.Replace(@"-", @"+"));
            }
            return result.ToString();
        }

        public static FormulaWithMassModification Sum(IEnumerable<FormulaWithMassModification> items)
        {
            var dictionary = new Dictionary<string, int>();
            foreach (var item in items)
            {
                foreach (var entry in item)
                {
                    int count;
                    if (dictionary.TryGetValue(entry.Key, out count))
                    {
                        dictionary[entry.Key] = count + entry.Value;
                    }
                    else
                    {
                        dictionary.Add(entry.Key, entry.Value);
                    }
                }
            }

            return FromDict(dictionary);
        }
    }
}
