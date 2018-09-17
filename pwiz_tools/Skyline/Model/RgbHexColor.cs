/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
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
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class RgbHexColor : IXmlSerializable, INotifyPropertyChanged
    {
        private Color _color;

        public RgbHexColor()
        {
            Color = Color.Empty;
        }

        public RgbHexColor(Color color)
        {
            Color = color;
        }

        public Color Color
        {
            get { return _color; }
            set { _color = value; NotifyPropertyChanged(); }
        }

        [Track]
        public string Rgb
        {
            get
            {
                if (Color == Color.Empty)
                {
                    return string.Empty;
                }
                return GetRgb(Color);
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    Color = Color.Empty;
                    return;
                }

                var newColor = ParseRgb(value);
                if (newColor == null)
                {
                    throw new FormatException();
                }
                Color = newColor.Value;
            }
        }

        public string Hex
        {
            get
            {
                if (Color == Color.Empty)
                {
                    return string.Empty;
                }
                return GetHex(Color);
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    Color = Color.Empty;
                    return;
                }

                var newColor = ParseHtmlColor(value);
                if (!newColor.HasValue)
                {
                    throw new FormatException();
                }
                Color = newColor.Value;
            }
        }

        public static string GetRgb(Color color)
        {
            return String.Format(@"{0}, {1}, {2}", color.R, color.G, color.B);
        }

        public static string GetHex(Color color)
        {
            return @"#" + color.R.ToString(@"X2") + color.G.ToString(@"X2") + color.B.ToString(@"X2");
        }

        public static Color? ParseHtmlColor(string value)
        {
            if (value.Length == 6 || value.Length == 3)
                value = @"#" + value;
            Color color;
            try
            {
                color = ColorTranslator.FromHtml(value);
            }
            catch
            {
                return null;
            }
            return color;
        }

        public static Color? ParseRgb(string value)
        {
            if (value == null)
            {
                return null;
            }
            else
            {
                var RGB = value.Split(',');
                if (RGB.Length != 3)
                    return null;
                else
                {
                    bool isValid = true;
                    foreach (var s in RGB)
                    {
                        try
                        {
                            var num = int.Parse(s);
                            if (num < 0 || num > 255)
                                isValid = false;
                        }
                        catch
                        {
                            isValid = false;
                        }
                    }
                    if (isValid)
                        return Color.FromArgb(int.Parse(RGB[0]), int.Parse(RGB[1]), int.Parse(RGB[2]));
                    else
                        return null;
                }
            }
        }

        // ReSharper disable once LocalizableElement
        protected virtual void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public override int GetHashCode()
        {
            return _color.GetHashCode();
        }

        protected bool Equals(RgbHexColor other)
        {
            return _color.Equals(other._color);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((RgbHexColor)obj);
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        private enum ATTR
        {
            color
        }

        public virtual void ReadXml(XmlReader reader)
        {
            Hex = reader.GetAttribute(ATTR.color);
        }

        public virtual void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.color, Hex);
        }
    }
}
