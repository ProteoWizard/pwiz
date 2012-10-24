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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        public static bool EqualsDeep<K,V>(IDictionary<K,V> dict1, IDictionary<K,V> dict2)
        {
            if (dict1.Count != dict2.Count)
            {
                return false;
            }
            foreach (var entry in dict1)
            {
                V value2;
                if (!dict2.TryGetValue(entry.Key, out value2))
                {
                    return false;
                }
                if (!Equals(entry.Value, value2))
                {
                    return false;
                }
            }
            return true;
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
        public V GetValueOrDefault<K,V>(Dictionary<K,V> dict, K key, V defaultValue)
        {
            V result;
            if (!dict.TryGetValue(key, out result))
            {
                return defaultValue;
            }
            return result;
        }
    }
}
