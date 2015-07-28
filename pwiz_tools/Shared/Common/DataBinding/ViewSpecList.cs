/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding
{
    [XmlRoot("views")]
    public class ViewSpecList : IXmlSerializable
    {
        public static readonly ViewSpecList EMPTY = new ViewSpecList(ImmutableList.Empty<ViewSpec>());

        public ViewSpecList(IEnumerable<ViewSpec> viewSpecs)
        {
            ViewSpecs = ImmutableList.ValueOf(viewSpecs);
        }

        
        public ImmutableList<ViewSpec> ViewSpecs { get;private set; }

        public ViewSpecList FilterRowSources(ICollection<string> rowSources)
        {
            return new ViewSpecList(ViewSpecs.Where(viewSpec=>rowSources.Contains(viewSpec.RowSource)));
        }

        public ViewSpec GetView(string name)
        {
            return ViewSpecs.FirstOrDefault(viewSpec => viewSpec.Name == name);
        }

        public ViewSpecList ReplaceView(string oldName, ViewSpec newView)
        {
            List<ViewSpec> items = new List<ViewSpec>();
            bool found = false;
            foreach (var item in ViewSpecs)
            {
                if (item.Name != oldName)
                {
                    items.Add(item);
                    continue;
                }
                found = true;
                if (newView != null)
                {
                    items.Add(newView);
                }
            }
            if (!found && null != newView)
            {
                items.Add(newView);
            }
            return new ViewSpecList(items);
        }

        public ViewSpecList RenameView(string oldName, string newName)
        {
            var viewSpec = GetView(oldName);
            if (null == viewSpec)
            {
                return this;
            }
            return ReplaceView(oldName, viewSpec.SetName(newName));
        }

        public ViewSpecList DeleteViews(IEnumerable<string> names)
        {
            var nameSet = names as HashSet<string> ?? new HashSet<string>(names);
            return new ViewSpecList(ViewSpecs.Where(spec=>!nameSet.Contains(spec.Name)));
        }

        public ViewSpecList AddOrReplaceViews(IEnumerable<ViewSpec> viewSpecs)
        {
            var result = this;
            foreach (var viewSpec in viewSpecs)
            {
                result = result.ReplaceView(viewSpec.Name, viewSpec);
            }
            return result;
        }

        #region XML Serialization
        private ViewSpecList()
        {
        }

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            ReadXml(reader);
        }

        private void ReadXml(XmlReader reader)
        {
            if (ViewSpecs != null)
            {
                throw new ReadOnlyException();
            }
            if (reader.IsEmptyElement)
            {
                ViewSpecs = ImmutableList.Empty<ViewSpec>();
                reader.ReadElementString("views"); // Not L10N
                return;
            }
            reader.Read();
            var viewItems = new List<ViewSpec>();
            while (true)
            {
                if (reader.IsStartElement("view")) // Not L10N
                {
                    viewItems.Add(ViewSpec.ReadXml(reader));
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    reader.ReadEndElement();
                    break;
                }
                else
                {
                    reader.Read();
                }
            }
            ViewSpecs = ImmutableList.ValueOf(viewItems);
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (var viewItem in ViewSpecs)
            {
                writer.WriteStartElement("view"); // Not L10N
                viewItem.WriteXml(writer);
                writer.WriteEndElement();
            }
        }

        public static ViewSpecList Deserialize(XmlReader reader)
        {
            ViewSpecList viewSpecList = new ViewSpecList();
            viewSpecList.ReadXml(reader);
            return viewSpecList;
        }
        #endregion

        #region Equality Members
        protected bool Equals(ViewSpecList other)
        {
            return Equals(ViewSpecs, other.ViewSpecs);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ViewSpecList) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ViewSpecs.GetHashCode();
            }
        }
        #endregion
    }
}