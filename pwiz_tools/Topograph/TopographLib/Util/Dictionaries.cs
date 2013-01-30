/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.Linq;

namespace pwiz.Topograph.Util
{
    public class Dictionaries
    {
        public static Dictionary<T, double> Normalize<T>(IDictionary<T, double> dict, double target)
        {
            double sum = dict.Values.Sum();
            double factor = sum == 0 ? 1 : target/sum;
            return Scale(dict, factor);
        }

        public static Dictionary<T, double> Scale<T>(IDictionary<T, double> dict, double factor)
        {
            Dictionary<T, double> result = new Dictionary<T, double>();
            foreach (var pair in dict)
            {
                result.Add(pair.Key, pair.Value*factor);
            }
            return result;
        }
        public static Dictionary<T,double> Sum<T>(IDictionary<T,double> dict1, Dictionary<T,double> dict2)
        {
            Dictionary<T,double> result = new Dictionary<T, double>(dict1);
            foreach (var entry in dict2)
            {
                double value;
                result.TryGetValue(entry.Key, out value);
                result[entry.Key] = value + entry.Value;
            }
            return result;
        }

        public static T KeyWithMaxValue<T>(Dictionary<T, double> dict)
        {
            double maxValue = -double.MaxValue;
            
            T key = default(T);
            foreach (var pair in dict)
            {
                if (pair.Value > maxValue)
                {
                    maxValue = pair.Value;
                    key = pair.Key;
                }
            }
            return key;
        }
        public static Dictionary<double,T> OffsetKeys<T>(IDictionary<double,T> dict, double offset)
        {
            var result = new Dictionary<double, T>();
            foreach (var entry in dict)
            {
                result.Add(entry.Key + offset, entry.Value);
            }
            return result;
        }

        public static Dictionary<double, double> SetResolution(Dictionary<double, double> dict, double resolution, double threshhold)
        {
            var sortedDict = new SortedDictionary<double, double>(dict);
            var result = new Dictionary<double, double>();
            double currentKey = 0;
            double currentValue = 0;
            foreach (var entry in sortedDict)
            {
                if (entry.Key - currentKey > resolution)
                {
                    if (currentValue > threshhold)
                    {
                        result.Add(currentKey, currentValue);
                    }
                    currentKey = 0;
                    currentValue = 0;
                }
                currentKey = (currentKey*currentValue + entry.Key*entry.Value)/(currentValue + entry.Value);
                currentValue += entry.Value;
            }
            if (currentValue > threshhold)
            {
                result.Add(currentKey, currentValue);
            }
            return result;
        }

        public IDictionary<TKey, TValue> Merge<TKey, TValue>(IDictionary<TKey, TValue> mine,
                                                             IDictionary<TKey, TValue> original,
                                                             IDictionary<TKey, TValue> theirs)
        {
            var result = new Dictionary<TKey, TValue>(theirs);
            foreach (var entry in mine.Except(original))
            {
                result[entry.Key] = entry.Value;
            }
            foreach (var key in original.Keys.Except(mine.Keys))
            {
                result.Remove(key);
            }
            return result;
        }

        public delegate TValue MergeFunc<TValue>(TValue mine, TValue original, TValue theirs);
        public IDictionary<TKey, TValue> Merge<TKey, TValue>(IDictionary<TKey, TValue> mine,
                                                             IDictionary<TKey, TValue> original,
                                                             IDictionary<TKey, TValue> theirs,
                                                             MergeFunc<TValue> mergeFunc)
        {
            var result = new Dictionary<TKey, TValue>(theirs);
            foreach (var entry in mine.Except(original))
            {
                TValue theirValue;
                if (result.TryGetValue(entry.Key, out theirValue))
                {
                    TValue originalValue;
                    original.TryGetValue(entry.Key, out originalValue);
                    result[entry.Key] = mergeFunc(entry.Value, originalValue, theirValue);
                }
            }
            foreach (var key in original.Keys.Except(mine.Keys))
            {
                result.Remove(key);
            }
            return result;
        }
    }
}
