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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Themes
{
    [XmlRoot("color_scheme")]
    public class ColorScheme : XmlNamedElement
    {
        public ColorScheme(string name) : base(name)
        {
            
        }

        private ColorScheme()
        {            
        }

        public new ColorScheme ChangeName(string name)
        {
            return (ColorScheme) base.ChangeName(name);
        }

        public ImmutableList<Color> PrecursorColors { get; private set; }
        public ImmutableList<Color> TransitionColors { get; private set; }

        public static Color ChromGraphItemSelected => Color.Red;

        [Pure]
        public ColorScheme ChangePrecursorColors(IEnumerable<Color> precursorColors)
        {
            return ChangeProp(ImClone(this), im => im.PrecursorColors = ImmutableList.ValueOf(precursorColors));
        }

        [Pure]
        public ColorScheme ChangeTransitionColors(IEnumerable<Color> transitionColors)
        {
            return ChangeProp(ImClone(this), im => im.TransitionColors = ImmutableList.ValueOf(transitionColors));
        }

        #region Implementation of IXmlSerializable

        public static ColorScheme Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ColorScheme());
        }

        enum EL
        {
            color
        }

        enum ATTR
        {
            type,
            red,
            green,
            blue
        }

        private const string GROUP_NAME_PRECURSOR = "percursor";
        private const string GROUP_NAME_TRANSITION = "transition";

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            reader.ReadStartElement();  // Consume tag
            List<Color> groupColors = new List<Color>();
            List<Color> libraryColors = new List<Color>();
            while (reader.IsStartElement(EL.color))
            {
                string groupName = reader.GetAttribute(ATTR.type);
                Color color = Color.FromArgb(reader.GetIntAttribute(ATTR.red),
                    reader.GetIntAttribute(ATTR.green),
                    reader.GetIntAttribute(ATTR.blue));
                if (groupName == GROUP_NAME_PRECURSOR)
                {
                    groupColors.Add(color);
                }
                else if (groupName == GROUP_NAME_TRANSITION)
                {
                    libraryColors.Add(color);
                }
                reader.Read();  // Consume tag
            }
            PrecursorColors = ImmutableList.ValueOf(groupColors);
            TransitionColors = ImmutableList.ValueOf(libraryColors);
            reader.ReadEndElement(); // Consume end tag
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            foreach (var color in PrecursorColors)
            {
                WriteColor(writer, GROUP_NAME_PRECURSOR, color);
            }

            foreach (var color in TransitionColors)
            {
                WriteColor(writer, GROUP_NAME_TRANSITION, color);
            }
        }

        private void WriteColor(XmlWriter writer, string groupName, Color color)
        {
            writer.WriteStartElement(EL.color);
            writer.WriteAttributeString(ATTR.type, groupName);
            writer.WriteAttribute(ATTR.red, color.R);
            writer.WriteAttribute(ATTR.green, color.G);
            writer.WriteAttribute(ATTR.blue, color.B);
            writer.WriteEndElement();
        }

        #endregion

        #region object overrides

        protected bool Equals(ColorScheme other)
        {
            return base.Equals(other) &&
                Equals(PrecursorColors, other.PrecursorColors) &&
                Equals(TransitionColors, other.TransitionColors);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ColorScheme) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (PrecursorColors != null ? PrecursorColors.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (TransitionColors != null ? TransitionColors.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion

        public static ColorScheme ColorSchemeDemo { get; set; }

        public static ColorScheme CurrentColorScheme
        {
            get
            {
                if (null != ColorSchemeDemo)
                {
                    return ColorSchemeDemo;
                }
                String colorSchemeName = Settings.Default.CurrentColorScheme;
                if (string.IsNullOrEmpty(colorSchemeName))
                {
                    return ColorSchemeList.DEFAULT;
                }
                var colorSchemes = Settings.Default.ColorSchemes;
                ColorScheme colorScheme;
                if (colorSchemes.TryGetValue(colorSchemeName, out colorScheme))
                {
                    return colorScheme;
                }
                return ColorSchemeList.DEFAULT;
            }
        }
    }
}
