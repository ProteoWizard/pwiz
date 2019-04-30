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
using System.Xml;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Layout
{
    public class ViewLayoutList : Immutable
    {
        public static readonly ViewLayoutList EMPTY = new ViewLayoutList(null);
        public ViewLayoutList(string viewName)
        {
            ViewName = viewName;
            Layouts = ImmutableList<ViewLayout>.EMPTY;
        }
        public string ViewName { get; private set; }

        public ViewLayoutList ChangeViewName(string name)
        {
            return ChangeProp(ImClone(this), im => im.ViewName = name);
        }
        public string DefaultLayoutName { get; private set; }

        public ViewLayoutList ChangeDefaultLayoutName(string name)
        {
            return ChangeProp(ImClone(this), im => im.DefaultLayoutName = name);
        }

        public ViewLayout DefaultLayout
        {
            get
            {
                if (string.IsNullOrEmpty(DefaultLayoutName))
                {
                    return null;
                }

                return FindLayout(DefaultLayoutName);
            }
        }

        [TrackChildren]
        public ImmutableList<ViewLayout> Layouts { get; private set; }

        public ViewLayoutList ChangeLayouts(IEnumerable<ViewLayout> layouts)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.Layouts =
                    ImmutableList.ValueOf(layouts.OrderBy(layout => layout.Name, StringComparer.OrdinalIgnoreCase));
                if (null != im.DefaultLayoutName && im.Layouts.All(layout => layout.Name != im.DefaultLayoutName))
                {
                    im.DefaultLayoutName = null;
                }
            });
        }

        public ViewLayout FindLayout(string name)
        {
            return Layouts.FirstOrDefault(layout => layout.Name == name);
        }

        public bool IsEmpty { get { return Layouts.Count == 0; } }

        public ViewLayoutList Merge(IEnumerable<ViewLayout> layouts)
        {
            var newLayouts = new List<ViewLayout>();
            var names = new HashSet<string>();
            foreach (var layout in layouts.Concat(Layouts))
            {
                if (names.Add(layout.Name))
                {
                    newLayouts.Add(layout);
                }
            }
            return ChangeLayouts(newLayouts);
        }

        protected bool Equals(ViewLayoutList other)
        {
            return string.Equals(ViewName, other.ViewName) &&
                   string.Equals(DefaultLayoutName, other.DefaultLayoutName) && 
                   Equals(Layouts, other.Layouts);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ViewLayoutList) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (ViewName != null ? ViewName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (DefaultLayoutName != null ? DefaultLayoutName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Layouts != null ? Layouts.GetHashCode() : 0);
                return hashCode;
            }
        }

        // ReSharper disable LocalizableElement
        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeString("viewName", ViewName);
            if (!string.IsNullOrEmpty(DefaultLayoutName))
            {
                writer.WriteAttributeString("defaultLayout", DefaultLayoutName);
            }
            foreach (var viewLayout in Layouts)
            {
                writer.WriteStartElement("layout");
                viewLayout.WriteXml(writer);
                writer.WriteEndElement();
            }
        }

        public static ViewLayoutList ReadXml(XmlReader reader)
        {
            var layoutList = new ViewLayoutList(reader.GetAttribute("viewName"))
                .ChangeDefaultLayoutName(reader.GetAttribute("defaultLayout"));
            
            if (reader.IsEmptyElement)
            {
                reader.ReadElementString("layouts");
                return layoutList;
            }
            reader.Read();
            var layouts = new List<ViewLayout>();
            while (true)
            {
                if (reader.IsStartElement("layout"))
                {
                    layouts.Add(ViewLayout.ReadXml(reader));
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    reader.ReadEndElement();
                    break;
                }
                else
                {
                    reader.Skip();
                }
            }
            layoutList = layoutList.ChangeLayouts(layouts);
            return layoutList;
        }
        // ReSharper restore LocalizableElement
    }
}
