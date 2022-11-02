/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;

namespace pwiz.Common.Colors
{
    public class DiscreteColorScheme : IColorScheme
    {
        private IList<Color> _palette = ColorPalettes.LARGE_PALETTE;
        private IDictionary<object, int> _objectIndexes = new Dictionary<object, int>();

        public void AddValues(IEnumerable values)
        {
            int index;
            IEnumerable<object> newValues = values.OfType<object>().Distinct();
            if (_objectIndexes.Count == 0)
            {
                index = 2;
            }
            else
            {
                index = _objectIndexes.Values.Max() + 1;
                newValues = newValues.Where(v=>!_objectIndexes.ContainsKey(v));
            }
            var valueToIndex = new Dictionary<object, int>();
            var valuesByType = newValues.ToLookup(v => v.GetType());
            foreach (var grouping in valuesByType.OrderBy(group => group.Count()))
            {
                foreach (var v in grouping)
                {
                    valueToIndex.Add(v, index++);
                }
            }

            _objectIndexes = valueToIndex;
        }


        public Color? GetColor(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (_objectIndexes == null || !_objectIndexes.TryGetValue(value, out int index))
            {
                return ColorFromIndex(0);
            }

            return ColorFromIndex(index);
        }

        public Color ColorFromIndex(int i)
        {
            return _palette[Math.Abs(i) % _palette.Count];
        }
    }
}
