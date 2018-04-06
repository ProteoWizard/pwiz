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

namespace pwiz.Common.DataBinding.Attributes
{
    public class ColumnCaptionAttribute : Attribute
    {
        public ColumnCaptionAttribute(IColumnCaption columnCaption)
        {
            ColumnCaption = columnCaption;
        }
        public IColumnCaption ColumnCaption { get; private set; }

        protected bool Equals(ColumnCaptionAttribute other)
        {
            return base.Equals(other) && ColumnCaption.Equals(other.ColumnCaption);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ColumnCaptionAttribute) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ ColumnCaption.GetHashCode();
            }
        }
    }
}
