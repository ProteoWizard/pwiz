/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Attributes;

namespace pwiz.Common.DataBinding
{
    public interface IColumnCaption
    {
        string GetCaption(DataSchemaLocalizer localizer);
    }

    public class FormattableCaption : IColumnCaption
    {
        public FormattableCaption(IFormattable value) : this(@"G", value)
        {
        }

        public FormattableCaption(string format, IFormattable value)
        {
            Format = format;
            Value = value;
        }

        public string Format { get; private set; }
        public IFormattable Value { get; private set; }

        public string GetCaption(DataSchemaLocalizer localizer)
        {
            if (Value == null)
            {
                return string.Empty;
            }
            return Value.ToString(Format, localizer.FormatProvider);
        }

        protected bool Equals(FormattableCaption other)
        {
            return string.Equals(Format, other.Format) && Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((FormattableCaption) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Format != null ? Format.GetHashCode() : 0) * 397) ^ (Value != null ? Value.GetHashCode() : 0);
            }
        }
    }

    public class CaptionComponentList : IColumnCaption
    {
        public static readonly IColumnCaption SPACE = ColumnCaption.UnlocalizableCaption(@" ");
        public static readonly IColumnCaption EMPTY = ColumnCaption.UnlocalizableCaption(string.Empty);
        public CaptionComponentList(IEnumerable<IColumnCaption> components)
        {
            Components = ImmutableList.ValueOf(components);
            Separator = SPACE;
        }

        public IColumnCaption Separator { get; private set; }
        public ImmutableList<IColumnCaption> Components { get; private set; }

        public string GetCaption(DataSchemaLocalizer localizer)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var component in Components)
            {
                var part = component.GetCaption(localizer);
                if (part.Length == 0)
                {
                    continue;
                }
                if (stringBuilder.Length > 0)
                {
                    stringBuilder.Append(Separator.GetCaption(localizer));
                }
                stringBuilder.Append(part);
            }
            return stringBuilder.ToString();
        }

        public static IColumnCaption MakeCaptionComponent(object part)
        {
            if (part == null)
            {
                return EMPTY;
            }
            IColumnCaption captionComponent = part as IColumnCaption;
            if (captionComponent != null)
            {
                return captionComponent;
            }
            var formattable = part as IFormattable;
            if (formattable != null)
            {
                return new FormattableCaption(formattable);
            }
            return ColumnCaption.UnlocalizableCaption(part.ToString());
        }

        public static CaptionComponentList SpaceSeparate(IEnumerable<object> parts)
        {
            return new CaptionComponentList(parts.Select(MakeCaptionComponent));
        }

        protected bool Equals(CaptionComponentList other)
        {
            return Equals(Separator, other.Separator) && Equals(Components, other.Components);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((CaptionComponentList) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Separator != null ? Separator.GetHashCode() : 0) * 397) ^ (Components != null ? Components.GetHashCode() : 0);
            }
        }
    }

    public class ConstantCaption : IColumnCaption
    {
        public ConstantCaption(string value)
        {
            Value = value;
        }

        public string Value { get; private set; }
        public string GetCaption(DataSchemaLocalizer localizer)
        {
            return Value;
        }

        protected bool Equals(ConstantCaption other)
        {
            return string.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ConstantCaption) obj);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    /// <summary>
    /// Holds the unlocalized invariant column caption.  This column caption string is used as the key
    /// for <see cref="DataSchemaLocalizer" />
    /// </summary>
    public class ColumnCaption : IColumnCaption
    {
        public ColumnCaption(string invariantColumnCaption)
        {
            InvariantCaption = invariantColumnCaption;
        }

        public string InvariantCaption { get; private set; }

        public string GetCaption(DataSchemaLocalizer localizer)
        {
            return localizer.LookupColumnCaption(this);
        }


        public override string ToString()
        {
            return InvariantCaption;
        }

        /// <summary>
        /// Constructs an unlocalizable, where the caption will appear the same in all languages.
        /// This is the sort of caption that appears when the name of the column is something that the
        /// user defined, such as an annotation column.
        /// </summary>
        public static IColumnCaption UnlocalizableCaption(string caption)
        {
            return new ConstantCaption(caption);
        }

        public static IColumnCaption ExplicitCaption(string caption)
        {
            if (caption == null)
            {
                return null;
            }
            return UnlocalizableCaption(caption);
        }

        public static IColumnCaption GetColumnCaption(PropertyDescriptor propertyDescriptor)
        {
            var columnCaptionAttribute = (ColumnCaptionAttribute) propertyDescriptor.Attributes[typeof(ColumnCaptionAttribute)];
            if (columnCaptionAttribute != null)
            {
                return columnCaptionAttribute.ColumnCaption;
            }
            return UnlocalizableCaption(propertyDescriptor.DisplayName);
        }

        protected bool Equals(ColumnCaption other)
        {
            return string.Equals(InvariantCaption, other.InvariantCaption);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ColumnCaption) obj);
        }

        public override int GetHashCode()
        {
            return InvariantCaption.GetHashCode();
        }
    }

    public enum ColumnCaptionType
    {
        invariant,
        localized,
    }
}
