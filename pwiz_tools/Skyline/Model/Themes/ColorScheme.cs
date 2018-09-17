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
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
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

    public class ColorSchemeList : SettingsList<ColorScheme>, IListSerializer<ColorScheme>
    {
        // Great websites for generating/finding schemes
        // http://vrl.cs.brown.edu/color
        // http://colorbrewer2.org
        public static readonly ColorScheme DEFAULT = new ColorScheme(Resources.ColorSchemeList_DEFAULT_Skyline_classic).ChangePrecursorColors(new[]
            {
                Color.Red,
                Color.Blue,
                Color.Maroon,
                Color.Purple,
                Color.Orange,
                Color.Green,
                Color.Yellow,
                Color.LightBlue,
            })
            .ChangeTransitionColors(new[]
            {
                Color.Blue,
                Color.BlueViolet,
                Color.Brown,
                Color.Chocolate,
                Color.DarkCyan,
                Color.Green,
                Color.Orange,
//                Color.Navy,
                Color.FromArgb(0x75, 0x70, 0xB3),
                Color.Purple,
                Color.LimeGreen,
                Color.Gold,
                Color.Magenta,
                Color.Maroon,
                Color.OliveDrab,
                Color.RoyalBlue,
            });
        public override IEnumerable<ColorScheme> GetDefaults(int revisionIndex)
        {
            yield return DEFAULT;
            yield return DEFAULT.ChangeName(Resources.ColorSchemeList_GetDefaults_Eggplant_lemonade).ChangePrecursorColors(new[]
            {
                Color.FromArgb(213,62,79), 
                Color.FromArgb(102,194,165), 
                Color.FromArgb(253,174,97), 
                Color.FromArgb(210, 242, 53), 
                Color.FromArgb(50,136,189)
            }).ChangeTransitionColors(new[]
            {
                Color.FromArgb(94,79,162), 
                Color.FromArgb(50,136,189), 
                Color.FromArgb(102,194,165), 
                Color.FromArgb(171,221,164), 
                Color.FromArgb(210, 242, 53), 
//                Color.FromArgb(249, 249, 84), 
                Color.FromArgb(247, 207, 98), 
                Color.FromArgb(253,174,97), 
                Color.FromArgb(244,109,67), 
                Color.FromArgb(213,62,79), 
                Color.FromArgb(158,1,66)
            });
            yield return DEFAULT.ChangeName(Resources.ColorSchemeList_GetDefaults_Distinct).ChangePrecursorColors(new[]
            {
                Color.FromArgb(249, 104, 87), 
                Color.FromArgb(49, 191, 167), 
                Color.FromArgb(249, 155, 49), 
                Color.FromArgb(109, 95, 211), 
                Color.FromArgb(75, 159, 216), 
                Color.FromArgb(163, 219, 67), 
                Color.FromArgb(247, 138, 194), 
                Color.FromArgb(183, 183, 183), 
                Color.FromArgb(184, 78, 186), 
                Color.FromArgb(239, 233, 57), 
                Color.FromArgb(133, 211, 116)
            }).ChangeTransitionColors(new[]
            {
                Color.FromArgb(49, 191, 167), 
                Color.FromArgb(249, 155, 49), 
                Color.FromArgb(109, 95, 211), 
                Color.FromArgb(249, 104, 87), 
                Color.FromArgb(75, 159, 216), 
                Color.FromArgb(163, 219, 67), 
                Color.FromArgb(247, 138, 194), 
                Color.FromArgb(183, 183, 183), 
                Color.FromArgb(184, 78, 186), 
                Color.FromArgb(239, 233, 57), 
                Color.FromArgb(133, 211, 116)
            });
            yield return DEFAULT.ChangeName(Resources.ColorSchemeList_GetDefaults_High_contrast).ChangePrecursorColors(new[]
            {
                Color.FromArgb(179,70,126),
                Color.FromArgb(146,181,64),
                Color.FromArgb(90,58,142),
                Color.FromArgb(205,156,46),
                Color.FromArgb(109,131,218),
                Color.FromArgb(200,115,197),
                Color.FromArgb(69,192,151)
            }).ChangeTransitionColors(new[]
            {
                Color.FromArgb(179,70,126),
                Color.FromArgb(146,181,64),
                Color.FromArgb(90,58,142),
                Color.FromArgb(205,156,46),
                Color.FromArgb(109,131,218),
                Color.FromArgb(200,115,197),
                Color.FromArgb(69,192,151),
                Color.FromArgb(212,84,78),
                Color.FromArgb(90,165,84),
                Color.FromArgb(153,147,63),
                Color.FromArgb(221,91,107),
                Color.FromArgb(202,139,71),
                Color.FromArgb(159,55,74),
                Color.FromArgb(193,86,45),
                Color.FromArgb(150,73,41)
            });
        }

        public override string Title
        {
            get { return @"Color Scheme"; }
        }

        public override string Label
        {
            get { return @"Color Scheme"; }
        }

        public override ColorScheme EditItem(Control owner, ColorScheme item, IEnumerable<ColorScheme> existing, object tag)
        {
            // < Edit List.. > selected
            using (var dlg = new EditCustomThemeDlg(item, existing ?? this))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    return dlg.NewScheme;
                }
            }
            return null;
        }

        public override ColorScheme CopyItem(ColorScheme item)
        {
            return item.ChangeName(string.Empty);
        }

        public Type SerialType  { get { return typeof(ColorScheme); } }
        public Type DeserialType {
            get { return typeof(ColorScheme); }
        }

        public ICollection<ColorScheme> CreateEmptyList()
        {
            return new ColorSchemeList();
        }
    }
}
