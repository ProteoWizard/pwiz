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
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace pwiz.Common.DataBinding.Controls
{
    internal class ApplyRowFilterTask : IDisposable
    {
        private bool _disposed;
        private object[] _rows;
        private PropertyDescriptor[] _properties;
        public ApplyRowFilterTask(NavBar owner, object[] rows, PropertyDescriptor[] properties, string filterText)
        {
            Owner = owner;
            _rows = rows;
            _properties = properties;
            FilterText = filterText;
        }
        public NavBar Owner { get; private set; }
        private bool Disposed
        {
            get
            {
                lock(this)
                {
                    return _disposed;
                }
            }
            set
            {
                lock(this)
                {
                    _disposed = value;
                }
            }
        }
        public void Dispose()
        {
            Disposed = true;
        }
        public string FilterText
        { 
            get; private set;
        }
        public void FilterBackground()
        {
            var filteredRows = new List<object>();
            foreach (var row in _rows)
            {
                if (Disposed)
                {
                    return;
                }
                foreach (var property in _properties)
                {
                    var value = property.GetValue(row);
                    if (value == null)
                    {
                        continue;
                    }
                    if (value.ToString().IndexOf(FilterText) >= 0)
                    {
                        filteredRows.Add(row);
                    }
                } 
            }
            Owner.SetFilteredRows(this, filteredRows);
        }
    }
}
