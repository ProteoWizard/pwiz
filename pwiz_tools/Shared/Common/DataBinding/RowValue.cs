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
using System.Linq;
using System.Text;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// When a DataGridView has expanded a one-to-many relationship, holds the values of both the original object 
    /// (RowData) and the values of all of the many's (RowKey).
    /// </summary>
    public class RowValue
    {
        public RowValue(RowKey rowKey, object rowData)
        {
            RowKey = rowKey;
            RowData = rowData;
        }

        public RowKey RowKey { get; private set; }
        public object RowData { get; private set; }

        #region object overrides

        public bool Equals(RowValue other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.RowKey, RowKey) && Equals(other.RowData, RowData);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (RowValue)) return false;
            return Equals((RowValue) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (RowKey.GetHashCode()*397) ^ RowData.GetHashCode();
            }
        }

        #endregion
    }
}
