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

namespace pwiz.Common.DataBinding.Internal
{
    internal class RowFilter
    {
        public static readonly RowFilter Empty = new RowFilter(null, true);
        private readonly string _normalizedText;

        public RowFilter(string text, bool caseSensitive)
        {
            Text = string.IsNullOrEmpty(text) ? null : text;
            CaseSensitive = caseSensitive;
            if (Text != null)
            {
                _normalizedText = CaseSensitive ? Text : Text.ToLower();
            }
        }

        public string Text { get; private set; }
        public bool CaseSensitive { get; private set; }
        public bool IsEmpty { get { return null == Text; } }
        public bool Matches(string value)
        {
            if (null == Text)
            {
                return true;
            }
            if (value == null)
            {
                return false;
            }
            if (CaseSensitive)
            {
                return value.IndexOf(_normalizedText, System.StringComparison.Ordinal) >= 0;
            }
            return value.ToLower().IndexOf(_normalizedText, System.StringComparison.Ordinal) >= 0;
        }

        public bool Equals(RowFilter other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Text, Text) && other.CaseSensitive.Equals(CaseSensitive);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (RowFilter)) return false;
            return Equals((RowFilter) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Text.GetHashCode()*397) ^ CaseSensitive.GetHashCode();
            }
        }
    }
}
