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
using System.Linq;
using System.Text;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Holds a set of values for when a DataGridView has expanded a One-To-Many relationship.
    /// </summary>
    public class RowKey : ImmutableDictionary<IdentifierPath, object>
    {
        private RowKey(IDictionary<IdentifierPath, object> dict) : base(dict)
        {
        }
        public RowKey Of(IEnumerable<KeyValuePair<IdentifierPath, object>> values)
        {
            var dict = new Dictionary<IdentifierPath, object>();
            dict.Union(values);
            return new RowKey(dict);
        }
        #region object overrides

        public bool Equals(RowKey other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return CollectionUtil.EqualsDeep(this, other);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (RowKey)) return false;
            return Equals((RowKey) obj);
        }

        public override int GetHashCode()
        {
            return CollectionUtil.GetHashCodeDeep(this);
        }
        #endregion
    }
}
