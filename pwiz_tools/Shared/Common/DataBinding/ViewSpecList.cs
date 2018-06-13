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
        {
            ViewSpecs = ImmutableList.ValueOf(viewSpecs);
            ViewLayouts = ImmutableList<ViewLayoutList>.EMPTY;
        }

        public ViewSpecList(IEnumerable<ViewSpec> viewSpecs, IEnumerable<ViewLayoutList> viewLayouts)
        {
            ViewSpecs = ImmutableList.ValueOfOrEmpty(viewSpecs);
            ViewLayouts = ImmutableList.ValueOfOrEmpty(viewLayouts);
        }

        public ImmutableList<ViewSpec> ViewSpecs { get;private set; }
        public ImmutableList<ViewLayoutList> ViewLayouts { get; private set; }
        
        [DiffParent]
        public ImmutableList<View> Views
        {
            get
            {
                return ImmutableList<View>.ValueOf(ViewSpecs.Select(v =>
                    new View(v, ViewLayouts.FirstOrDefault(vll => vll.ViewName == v.Name))));
            }
        }

        public class View : IAuditLogObject
        {
            private readonly ViewLayoutList _layouts;
            public View(ViewSpec spec, ViewLayoutList layouts)
            {
                ViewSpec = spec;
                _layouts = layouts;
            }

            [DiffParent(ignoreName:true)]
            public ViewSpec ViewSpec { get; private set; }

            [DiffParent]
            public ImmutableList<ViewLayout> Layouts
            {
                get
                {
                    if (_layouts == null)
                        return ImmutableList<ViewLayout>.EMPTY;

                    return _layouts.Layouts;
                }
            }

            public string AuditLogText { get { return ViewSpec.Name; } }
            public bool IsName { get { return true; } }
        }

        public ViewSpecList FilterRowSources(ICollection<string> rowSources)
        {
            return new ViewSpecList(ViewSpecs.Where(viewSpec=>rowSources.Contains(viewSpec.RowSource)));
        }

        public ViewSpecList Filter(Func<ViewSpec, bool> viewPredicate)
        {
            var viewSpecs = ImmutableList.ValueOf(ViewSpecs.Where(viewPredicate));
            var viewSpecNames = new HashSet<string>(viewSpecs.Select(viewSpec=>viewSpec.Name));
            return new ViewSpecList(viewSpecs, ViewLayouts.Where(layout=>viewSpecNames.Contains(layout.ViewName)));
        }

        public ViewSpec GetView(string name)
        {
            return ViewSpecs.FirstOrDefault(viewSpec => viewSpec.Name == name);
        }

        public ViewLayoutList GetViewLayouts(string name)
        {
            return ViewLayouts.FirstOrDefault(layout => layout.ViewName == name) ??
                   ViewLayoutList.EMPTY.ChangeViewName(name);
        }

        [Pure]
        public ViewSpecList SaveViewLayouts(ViewLayoutList viewLayoutList)
        {
            IEnumerable<ViewLayoutList> newLayouts;
            newLayouts = ViewLayouts.Where(layout => layout.ViewName != viewLayoutList.ViewName);
            if (!viewLayoutList.IsEmpty)
            {
                newLayouts = new[] {viewLayoutList}.Concat(newLayouts);
            }
            return new ViewSpecList(ViewSpecs, newLayouts);
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
            IEnumerable<ViewLayoutList> newLayouts;
            if (oldName != null)
            {
                if (newView == null)
                {
                    newLayouts = ViewLayouts.Where(layout => layout.ViewName != oldName);
                }
                else if (newView.Name == oldName)
                {
                    newLayouts = ViewLayouts;
                }
                else
                {
                    newLayouts = ViewLayouts.Where(layout => layout.ViewName != newView.Name)
                        .Select(layout => layout.ViewName == oldName ? layout.ChangeViewName(newView.Name) : layout);
                }
            }
            else
            {
                newLayouts = ViewLayouts;
            }
            return new ViewSpecList(items, newLayouts);
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
            var layouts = new List<ViewLayoutList>();
            while (true)
            {
                if (reader.IsStartElement("view")) // Not L10N
                {
                    viewItems.Add(ViewSpec.ReadXml(reader));
                }
                else if (reader.IsStartElement("layouts")) // Not L10N
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
            ViewSpecs = ImmutableList.ValueOf(viewItems);
            ViewLayouts = ImmutableList.ValueOf(layouts);
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (var viewItem in ViewSpecs)
            {
                writer.WriteStartElement("view"); // Not L10N
                viewItem.WriteXml(writer);
                writer.WriteEndElement();
            }
            foreach (var viewLayoutList in ViewLayouts)
            {
                writer.WriteStartElement("layouts"); // Not L10N
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
            return Equals(ViewSpecs, other.ViewSpecs) && Equals(ViewLayouts, other.ViewLayouts);
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
                return (ViewSpecs.GetHashCode() * 397) ^ ViewLayouts.GetHashCode();
            }
        }
        #endregion
    }
}