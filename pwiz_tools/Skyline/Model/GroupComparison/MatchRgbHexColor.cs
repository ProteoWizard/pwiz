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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.GroupComparison
{
    public enum PointSize
    {
        x_small,
        small,
        normal,
        large,
        x_large
    };

    public enum PointSymbol
    {
        Circle,
        Square,
        Triangle,
        TriangleDown,
        Diamond,
        XCross,
        Plus,
        Star
    }

    [XmlRoot(XML_ROOT)]
    public class MatchRgbHexColor : RgbHexColor, ICloneable
    {
        public const string XML_ROOT = "format_detail";
        private string _expression;
        private bool _labeled;
        private PointSymbol _pointSymbol;
        private PointSize _pointSize;

        public MatchRgbHexColor(string expression, bool labeled, Color color, PointSymbol pointSymbol, PointSize pointSize)
            : base(color)
        {
            Expression = expression;
            Labeled = labeled;
            PointSymbol = pointSymbol;
            PointSize = pointSize;
        }

        public MatchRgbHexColor()
            // ReSharper disable once LocalizableElement
            : this("", false, Color.Gray, PointSymbol.Circle, PointSize.normal)
        {

        }

        public MatchExpression MatchExpression { get; private set; }

        // If the user entered invalid match options, we don't want to treat the expression
        // as a regular expression, so we set a flag if there is something wrong with the match options.
        public bool InvalidMatchOptions { get; private set; }

        [Track]
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

        [Track]
        public bool Labeled
        {
            get { return _labeled; }
            set
            {
                _labeled = value;
                NotifyPropertyChanged();
            }
        }

        [Track]
        public PointSize PointSize
        {
            get { return _pointSize; }
            set
            {
                _pointSize = value;
                NotifyPropertyChanged();
            }
        }

        [Track]
        public PointSymbol PointSymbol
        {
            get { return _pointSymbol; }
            set
            {
                _pointSymbol = value;
                NotifyPropertyChanged();
            }
        }

        public object Clone()
        {
            return MemberwiseClone();
        }

        protected bool Equals(MatchRgbHexColor other)
        {
            return base.Equals(other) && string.Equals(_expression, other._expression) && _labeled == other._labeled &&
                   _pointSymbol == other._pointSymbol && _pointSize == other._pointSize;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((MatchRgbHexColor) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (_expression != null ? _expression.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ _labeled.GetHashCode();
                hashCode = (hashCode * 397) ^ (int) _pointSymbol;
                hashCode = (hashCode * 397) ^ (int) _pointSize;
                return hashCode;
            }
        }

        #region Implementation of IXmlSerializable

        private enum ATTR
        {
            expr,
            labeled,
            symbol_type,
            point_size
        }

        public static MatchRgbHexColor Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new MatchRgbHexColor());
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            Expression = reader.GetAttribute(ATTR.expr);
            Labeled = reader.GetBoolAttribute(ATTR.labeled);

            var symbol = reader.GetAttribute(ATTR.symbol_type);
            PointSymbol = symbol == null ? PointSymbol.Circle : Helpers.ParseEnum(symbol, PointSymbol.Circle);

            var pointSize = reader.GetAttribute(ATTR.point_size);
            PointSize = pointSize == null ? PointSize.normal : Helpers.ParseEnum(pointSize, PointSize.normal);

            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.expr, Expression);
            writer.WriteAttribute(ATTR.labeled, Labeled);
            writer.WriteAttribute(ATTR.symbol_type, PointSymbol.ToString());
            writer.WriteAttribute(ATTR.point_size, PointSize.ToString());
        }

        #endregion
    }
}
