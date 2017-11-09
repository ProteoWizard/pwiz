/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
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
using System.Drawing;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.GroupComparison
{
    [XmlRoot(XML_ROOT)]
    public class MatchRgbHexColor : RgbHexColor, ICloneable
    {
        public const string XML_ROOT = "format_detail";
        private string _expression;
        private bool _labeled;

        public MatchRgbHexColor(string expression, bool labeled, Color color)
            : base(color)
        {
            Expression = expression;
            Labeled = labeled;
        }

        public MatchRgbHexColor(string expression, bool labeled)
            : this(expression, labeled, Color.Gray)
        {
        }

        public MatchRgbHexColor()
            : this("", false) // Not L10N
        {

        }

        public MatchExpression MatchExpression { get; private set; }

        // If the user entered invalid match options, we don't want to treat the expression
        // as a regular expression, so we set a flag if there is something wrong with the match options.
        public bool InvalidMatchOptions { get; private set; }

        public string Expression
        {
            get { return _expression; }
            set
            {
                _expression = value;

                try
                {
                    MatchExpression = MatchExpression.Parse(value);
                }
                catch (Exception ex)
                {
                    InvalidMatchOptions = ex is MatchExpression.InvalidMatchOptionException;
                    MatchExpression = null;
                }


                NotifyPropertyChanged();
            }
        }

        public bool Labeled
        {
            get { return _labeled; }
            set
            {
                _labeled = value;
                NotifyPropertyChanged();
            }
        }

        public object Clone()
        {
            return MemberwiseClone();
        }

        protected bool Equals(MatchRgbHexColor other)
        {
            return base.Equals(other) && string.Equals(_expression, other._expression) && _labeled == other._labeled;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((MatchRgbHexColor)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (_expression != null ? _expression.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ _labeled.GetHashCode();
                return hashCode;
            }
        }

        #region Implementation of IXmlSerializable

        private enum ATTR
        {
            color,
            expr,
            labeled
        }

        public static MatchRgbHexColor Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new MatchRgbHexColor());
        }

        public override void ReadXml(XmlReader reader)
        {
            Hex = reader.GetAttribute(ATTR.color);
            Expression = reader.GetAttribute(ATTR.expr);
            Labeled = reader.GetBoolAttribute(ATTR.labeled);
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.color, Hex);
            writer.WriteAttribute(ATTR.expr, Expression);
            writer.WriteAttribute(ATTR.labeled, Labeled);
        }

        #endregion
    }
}
