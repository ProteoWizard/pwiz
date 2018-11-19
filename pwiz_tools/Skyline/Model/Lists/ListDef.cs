/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Lists;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lists
{
    [XmlRoot("list_def")]
    public class ListDef : XmlNamedElement
    {
        public static readonly ListDef EMPTY = new ListDef {Properties = ImmutableList<AnnotationDef>.EMPTY};

        public ListDef(string name) : base(name)
        {
            Properties = ImmutableList<AnnotationDef>.EMPTY;
        }

        public override string AuditLogText { get { return null; } }
        public override bool IsName { get { return false; } }

        [TrackChildren]
        public ImmutableList<AnnotationDef> Properties { get; private set; }

        public ListDef ChangeProperties(IEnumerable<AnnotationDef> properties)
        {
            return ChangeProp(ImClone(this),
                im => im.Properties = ImmutableList.ValueOfOrEmpty(properties));
        }

        [Track]
        public string IdProperty { get; private set; }

        public ListDef ChangeIdProperty(string idProperty)
        {
            return ChangeProp(ImClone(this), im => im.IdProperty = string.IsNullOrEmpty(idProperty) ? null : idProperty);
        }

        public AnnotationDef FindProperty(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            return Properties.FirstOrDefault(def => def.Name == name);
        }

        protected bool Equals(ListDef other)
        {
            return base.Equals(other) 
                && Equals(Properties, other.Properties) 
                && string.Equals(IdProperty, other.IdProperty)
                && string.Equals(DisplayProperty, other.DisplayProperty);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ListDef) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (Properties != null ? Properties.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (IdProperty != null ? IdProperty.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (DisplayProperty != null ? DisplayProperty.GetHashCode() : 0);
                return hashCode;
            }
        }

        public AnnotationDef IdPropertyDef
        {
            get
            {
                return FindProperty(IdProperty);
            }
        }

        [Track]
        public string DisplayProperty { get; private set; }

        public AnnotationDef DisplayPropertyDef
        {
            get
            {
                if (string.IsNullOrEmpty(DisplayProperty))
                {
                    return IdPropertyDef;
                }
                return Properties.FirstOrDefault(p => p.Name == DisplayProperty);
            }
        }

        public ListDef ChangeDisplayProperty(string displayProperty)
        {
            return ChangeProp(ImClone(this), im => im.DisplayProperty = string.IsNullOrEmpty(displayProperty) ? null : displayProperty);
        }

        private ListDef()
        {
        }

        private enum El
        {
            annotation,
        }

        private enum Attr
        {
            display_property,
            id_property
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            if (Properties != null)
            {
                throw new ReadOnlyException();
            }
            var properties = new List<AnnotationDef>();
            DisplayProperty = reader.GetAttribute(Attr.display_property);
            IdProperty = reader.GetAttribute(Attr.id_property);
            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.Read();
                while (true)
                {
                    if (reader.IsStartElement(El.annotation))
                    {
                        properties.Add(AnnotationDef.Deserialize(reader));
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
            }
            Properties = ImmutableList.ValueOf(properties);
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttributeIfString(Attr.display_property, DisplayProperty);
            writer.WriteAttributeIfString(Attr.id_property, IdProperty);
            foreach (var property in Properties)
            {
                writer.WriteElement(property);
            }
        }

        public static ListDef Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ListDef());
        }
    }

    public class ListDefList : SettingsList<ListData>, IListSerializer<ListData>
    {
        public override ListData CopyItem(ListData item)
        {
            return new ListData((ListDef) item.ListDef.ChangeName(String.Empty), item.Columns);
        }

        public override ListData EditItem(Control owner, ListData item, IEnumerable<ListData> existing, object tag)
        {
            using (var dlg = new ListDesigner(item, existing))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    return dlg.GetListDef();
                }
            }
            return null;
        }

        public override IEnumerable<ListData> GetDefaults(int revisionIndex)
        {
            return new ListData[0];
        }

        public override string Label
        {
            get { return Resources.ListDefList_Label_Lists; }
        }

        public override string Title
        {
            get { return Resources.ListDefList_Title_Define_Lists; }
        }

        public ICollection<ListData> CreateEmptyList()
        {
            return new ListDefList();
        }

        public Type DeserialType { get { return SerialType; } }
        public Type SerialType { get { return typeof(ListDef); } }


    }
}
