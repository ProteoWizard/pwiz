using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace pwiz.Common.DataBinding
{
    public abstract class SerializableViewGroups : IXmlSerializable
    {
        protected IDictionary<string, ViewSpecList> _viewSpecLists = new Dictionary<string, ViewSpecList>();

        public void SetViewSpecList(ViewGroupId viewGroup, ViewSpecList viewSpecList)
        {
            viewSpecList = viewSpecList ?? ViewSpecList.EMPTY;
            ViewSpecList oldList;
            if (!_viewSpecLists.TryGetValue(viewGroup.Name, out oldList))
            {
                oldList = ViewSpecList.EMPTY;
            }
            if (Equals(oldList, viewSpecList))
            {
                return;
            }
            if (!viewSpecList.ViewSpecs.Any())
            {
                _viewSpecLists.Remove(viewGroup.Name);
            }
            else
            {
                _viewSpecLists[viewGroup.Name] = viewSpecList;
            }
            FireChanged();
        }

        public ViewSpecList GetViewSpecList(ViewGroupId group)
        {
            ViewSpecList viewSpecList;
            if (!_viewSpecLists.TryGetValue(@group.Name, out viewSpecList))
            {
                return ViewSpecList.EMPTY;
            }
            return viewSpecList;
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public virtual void ReadXml(XmlReader reader)
        {
            var viewSpecLists = new Dictionary<string, ViewSpecList>();
            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.Read();
                while (true)
                {
                    if (reader.IsStartElement("views")) // Not L10N
                    {
                        string groupName = reader.GetAttribute("name"); // Not L10N
                        // ReSharper disable AssignNullToNotNullAttribute
                        viewSpecLists.Add(groupName, ViewSpecList.Deserialize(reader));
                        // ReSharper restore AssignNullToNotNullAttribute
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
            }
            _viewSpecLists = viewSpecLists;
        }

        public virtual void WriteXml(XmlWriter writer)
        {
            foreach (var entry in _viewSpecLists)
            {
                writer.WriteStartElement("views"); // Not L10N
                writer.WriteAttributeString("name", entry.Key); // Not L10N
                entry.Value.WriteXml(writer);
                writer.WriteEndElement();
            }
        }

        public void RemoveView(ViewGroupId group, string viewName)
        {
            var viewSpecList = GetViewSpecList(@group);
            if (null == viewSpecList)
            {
                return;
            }
            viewSpecList = new ViewSpecList(viewSpecList.ViewSpecs.Where(spec => spec.Name != viewName));
            SetViewSpecList(@group, viewSpecList);
        }

        public void Clear()
        {
            _viewSpecLists.Clear();
        }

        public event Action Changed;

        public void FireChanged()
        {
            if (null != Changed)
            {
                Changed();
            }
        }
    }
}
