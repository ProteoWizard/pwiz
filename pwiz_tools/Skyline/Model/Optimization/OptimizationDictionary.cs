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
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Optimization
{
    public class OptimizationDictionary : AbstractReadOnlyDictionary<OptimizationKey, double>
    {
        private LibKeyMap<IDictionary<Tuple<OptimizationType, string, Adduct>, double>> _libKeyMap;
        private ICollection<OptimizationKey> _keys;
        private ICollection<double> _values;

        public OptimizationDictionary(IEnumerable<DbOptimization> optimizations)
        {
            var optimizationsByPrecursor = optimizations.ToLookup(opt => GetPrecursorKey(opt.Key));
            var items = ImmutableList.ValueOf(optimizationsByPrecursor.Select(MakeProductDictionary));
            _libKeyMap = new LibKeyMap<IDictionary<Tuple<OptimizationType, string, Adduct>, double>>(items, 
                optimizationsByPrecursor.Select(grouping=>grouping.Key));
            _keys = ImmutableList.ValueOf(optimizationsByPrecursor.SelectMany(grouping=>grouping.Select(opt=>opt.Key)));
            _values = ImmutableList.ValueOf(optimizationsByPrecursor.SelectMany(grouping => grouping.Select(opt => opt.Value)));
        }

        public override ICollection<OptimizationKey> Keys
        {
            get { return _keys; }
        }

        public override bool TryGetValue(OptimizationKey key, out double value)
        {
            var productKey = GetProductKey(key);
            foreach (var dict in _libKeyMap.ItemsMatching(GetPrecursorKey(key), LibKeyIndex.LibraryMatchType.ion))
            {
                if (dict.TryGetValue(productKey, out value))
                {
                    return true;
                }
            }
            value = default(double);
            return false;
        }

        public IEnumerable<DbOptimization> EntriesMatching(OptimizationKey optimizationKey)
        {
            var productKey = GetProductKey(optimizationKey);
            foreach (var indexItem in _libKeyMap.Index.ItemsMatching(GetPrecursorKey(optimizationKey), LibKeyIndex.LibraryMatchType.ion))
            {
                var dict = _libKeyMap[indexItem.OriginalIndex];
                double value;
                if (dict.TryGetValue(productKey, out value))
                {
                    var foundKey = new OptimizationKey(optimizationKey.OptType, indexItem.LibraryKey.Target, indexItem.LibraryKey.Adduct, indexItem.LibraryKey.PrecursorFilter, optimizationKey.FragmentIon, optimizationKey.ProductAdduct);
                    yield return new DbOptimization(foundKey, value);
                }
            }
        }

        public override ICollection<double> Values
        {
            get { return _values; }
        }

        private static LibraryKey GetPrecursorKey(OptimizationKey optimizationKey)
        {
            return new LibKey(optimizationKey.PeptideModSeq, optimizationKey.PrecursorAdduct, optimizationKey.PrecursorFilter).LibraryKey;
        }

        private static Tuple<OptimizationType, string, Adduct> GetProductKey(OptimizationKey optimizationKey)
        {
            return Tuple.Create(optimizationKey.OptType, optimizationKey.FragmentIon, optimizationKey.ProductAdduct);
        }

        private static IDictionary<Tuple<OptimizationType, string, Adduct>, double> MakeProductDictionary(
            IEnumerable<DbOptimization> optimizations)
        {
            var dict = new Dictionary<Tuple<OptimizationType, string, Adduct>, double>();
            foreach (var opt in optimizations)
            {
                var productKey = GetProductKey(opt.Key);
                if (!dict.ContainsKey(productKey))
                {
                    dict.Add(productKey, opt.Value);
                }
            }
            return dict;
        }
    }
}
