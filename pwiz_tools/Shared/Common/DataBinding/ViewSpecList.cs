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

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    [XmlRoot("views")]
    public class ViewSpecList : Immutable, IXmlSerializable
    {
        public static readonly ViewSpecList EMPTY = new ViewSpecList(ImmutableList.Empty<ViewSpec>());

        public ViewSpecList(IEnumerable<ViewSpec> viewSpecs)
            : this(viewSpecs.Select(vs=>new ViewSpecLayout(vs, ViewLayoutList.EMPTY)))
        {
        }

        public ViewSpecList(IEnumerable<ViewSpec> viewSpecs, IEnumerable<ViewLayoutList> viewLayouts) 
            : this(MakeViewSpecLayouts(viewSpecs, viewLayouts))
        {
        }

        public ViewSpecList(IEnumerable<ViewSpecLayout> viewSpecLayouts)
        {
            ViewSpecLayouts = ImmutableList.ValueOfOrEmpty(viewSpecLayouts);
        }

        public IEnumerable<ViewSpec> ViewSpecs
        {
            get { return Views.Select(vsl => vsl.ViewSpec); }
        }

        public IEnumerable<ViewLayoutList> ViewLayouts
        {
            get { return Views.Select(vsl => vsl.ViewLayoutList).Where(vll => !vll.IsEmpty); }
        }

        [TrackChildren]
        public ImmutableList<ViewSpecLayout> Views
        {
            get { return ViewSpecLayouts; }
        }

        public ImmutableList<ViewSpecLayout> ViewSpecLayouts { get; private set; }

        public ViewSpecList FilterRowSources(ICollection<string> rowSources)
        {
            return Filter(viewSpec => rowSources.Contains(viewSpec.RowSource));
        }

        public ViewSpecList Filter(Func<ViewSpec, bool> viewPredicate)
        {
            return new ViewSpecList(Views.Where(vsl => viewPredicate(vsl.ViewSpec)));
        }

        public ViewSpec GetView(string name)
        {
            return GetViewSpecLayout(name)?.ViewSpec;
        }

        public ViewSpecLayout GetViewSpecLayout(string name)
        {
            return ViewSpecLayouts.FirstOrDefault(viewSpecLayout => viewSpecLayout.Name == name);
        }

        public ViewLayoutList GetViewLayouts(string name)
        {
            return ViewLayouts.FirstOrDefault(layout => layout.ViewName == name) ??
                   ViewLayoutList.EMPTY.ChangeViewName(name);
        }

        [Pure]
        public ViewSpecList SaveViewLayouts(ViewLayoutList viewLayoutList)
        {
            IEnumerable<ViewLayoutList> newLayouts = ViewLayouts.Where(layout => layout.ViewName != viewLayoutList.ViewName);
            if (!viewLayoutList.IsEmpty)
            {
                newLayouts = new[] {viewLayoutList}.Concat(newLayouts);
            }
            return new ViewSpecList(ViewSpecs, newLayouts);
        }

        public ViewSpecList ReplaceView(string oldName, ViewSpecLayout newView)
        {
            List<ViewSpecLayout> items = new List<ViewSpecLayout>();
            bool found = false;
            foreach (var item in ViewSpecLayouts)
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
            var viewSpec = GetViewSpecLayout(oldName);
            if (null == viewSpec)
            {
                return this;
            }
            return ReplaceView(oldName, viewSpec.ChangeName(newName));
        }

        public ViewSpecList DeleteViews(IEnumerable<string> names)
        {
            var nameSet = names as HashSet<string> ?? new HashSet<string>(names);
            return new ViewSpecList(ViewSpecs.Where(spec=>!nameSet.Contains(spec.Name)));
        }

        public ViewSpecList AddOrReplaceViews(IEnumerable<ViewSpecLayout> viewSpecs)
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
            if (Views != null)
            {
                throw new ReadOnlyException();
            }

            if (reader.IsEmptyElement)
            {
                ViewSpecLayouts = ImmutableList.Empty<ViewSpecLayout>();
                reader.ReadElementString(@"views");
                return;
            }
            reader.Read();
            var viewItems = new List<ViewSpec>();
            var layouts = new List<ViewLayoutList>();
            while (true)
            {
                if (reader.IsStartElement(@"view"))
                {
                    viewItems.Add(ViewSpec.ReadXml(reader));
                }
                else if (reader.IsStartElement(@"layouts"))
                {
                    layouts.Add(ViewLayoutList.ReadXml(reader));
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

            ViewSpecLayouts = ImmutableList.ValueOf(MakeViewSpecLayouts(viewItems, layouts));
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (var viewItem in ViewSpecs)
            {
                writer.WriteStartElement(@"view");
                viewItem.WriteXml(writer);
                writer.WriteEndElement();
            }
            foreach (var viewLayoutList in ViewLayouts)
            {
                writer.WriteStartElement(@"layouts");
                viewLayoutList.WriteXml(writer);
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
            return Equals(ViewSpecLayouts, other.ViewSpecLayouts);
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
            return ViewSpecLayouts.GetHashCode();
        }
        #endregion

        private static IEnumerable<ViewSpecLayout> MakeViewSpecLayouts(IEnumerable<ViewSpec> viewSpecs,
            IEnumerable<ViewLayoutList> viewLayouts)
        {
            Dictionary<string, ViewLayoutList> layoutsByName = viewLayouts?.ToDictionary(layout => layout.ViewName);
            foreach (var viewSpec in viewSpecs)
            {
                ViewLayoutList layouts = null;
                if (viewSpec.Name != null)
                {
                    layoutsByName?.TryGetValue(viewSpec.Name, out layouts);
                }
                yield return new ViewSpecLayout(viewSpec, layouts);

            }
        }
    }
}
