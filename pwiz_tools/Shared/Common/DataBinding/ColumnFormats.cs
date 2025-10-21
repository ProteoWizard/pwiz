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
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    public class ColumnFormats
    {
        private Dictionary<ColumnId, ColumnFormat> _formats = new Dictionary<ColumnId, ColumnFormat>();
        private int _defaultFrozenColumnCount;
        private bool? _defaultFrozenEnabled;
        public void SetFormat(ColumnId columnId, ColumnFormat columnFormat)
        {
            if (columnFormat.IsEmpty)
            {
                _formats.Remove(columnId);
            }
            else
            {
                _formats[columnId] = columnFormat;
            }
            FireFormatChanged();
        }

        public int DefaultFrozenColumnCount
        {
            get
            {
                return _defaultFrozenColumnCount;
            }
            set
            {
                _defaultFrozenColumnCount = value;
                FireFormatChanged();
            }
        }

        public bool DefaultFrozenEnabled
        {
            get
            {
                // Deduce value until explicitly set.
                return _defaultFrozenEnabled ?? _formats.Values.All(format => format.Frozen == null);
            }
            set
            {
                _defaultFrozenEnabled = value;
                FireFormatChanged();
            }
        }

        public ColumnFormat GetFormat(ColumnId columnId)
        {
            ColumnFormat columnFormat;
            if (_formats.TryGetValue(columnId, out columnFormat))
            {
                return columnFormat;
            }
            return ColumnFormat.EMPTY;
        }

        private void FireFormatChanged()
        {
            var formatChanged = FormatsChanged;
            if (formatChanged != null)
            {
                formatChanged();
            }
        }

        public event Action FormatsChanged;
    }

    public class ColumnFormat : Immutable
    {
        public static readonly ColumnFormat EMPTY = new ColumnFormat();
        public bool IsEmpty { get { return Equals(EMPTY); } }
        public string Format { get; private set; }

        public ColumnFormat ChangeFormat(string format)
        {
            return ChangeProp(ImClone(this), im => im.Format = format);
        }

        public int? Width { get; private set; }

        public ColumnFormat ChangeWidth(int? width)
        {
            return ChangeProp(ImClone(this), im => im.Width = width);
        }

        public bool? Frozen { get; private set; }

        public ColumnFormat ChangeFrozen(bool? frozen)
        {
            return ChangeProp(ImClone(this), im => im.Frozen = frozen);
        }

        protected bool Equals(ColumnFormat other)
        {
            return string.Equals(Format, other.Format) && Width == other.Width && Frozen == other.Frozen;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ColumnFormat) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Format != null ? Format.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Width.GetHashCode();
                hashCode = (hashCode * 397) ^ Frozen.GetHashCode();
                return hashCode;
            }
        }
    }
}
