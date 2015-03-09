/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.ComponentModel;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Controls
{
    internal class ApplyRowFilterTask : MustDispose
    {
        private readonly RowItem[] _rows;
        private readonly PropertyDescriptor[] _properties;
        public ApplyRowFilterTask(RowItem[] rows, PropertyDescriptor[] properties, string filterText, bool matchCase)
        {
            _rows = rows;
            _properties = properties;
            FilterText = filterText;
            MatchCase = matchCase;
        }
        public string FilterText
        { 
            get; private set;
        }

        public bool MatchCase
        {
            get; private set;
        }
        public RowItem[] FilteredRows { get; private set; }
        public void FilterBackground()
        {
            var filteredRows = new List<RowItem>();
            var normalizedFilterText = MatchCase ? FilterText : FilterText.ToLower();
            // toString on an enum is incredibly slow, so we cache the results in 
            // in a dictionary.
            var toStringCaches = new Dictionary<object, string>[_properties.Length];
            for (int i = 0; i < _properties.Length; i++ )
            {
                if (_properties[i].PropertyType.IsEnum)
                {
                    toStringCaches[i] = new Dictionary<object, string>();
                }
            }
            foreach (var row in _rows)
            {
                CheckDisposed();
                for (int i = 0; i < _properties.Length; i++) 
                {
                    var property = _properties[i];
                    var value = property.GetValue(row);
                    if (value == null)
                    {
                        continue;
                    }
                    var cache = toStringCaches[i];
                    string strValue;
                    if (cache == null)
                    {
                        strValue = value.ToString();
                    }
                    else
                    {
                        if (!cache.TryGetValue(value, out strValue))
                        {
                            strValue = value.ToString();
                            cache.Add(value, strValue);
                        }
                    }
                    if (!MatchCase)
                    {
                        strValue = strValue.ToLower();
                    }
                    if (strValue.IndexOf(normalizedFilterText) >= 0)
                    {
                        filteredRows.Add(row);
                        break;
                    }
                }
            }
            FilteredRows = filteredRows.ToArray();
        }
    }
}
