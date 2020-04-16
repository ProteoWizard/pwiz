using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    [XmlRoot("crosslinking")]
    public class CrosslinkingSettings : Immutable, IXmlSerializable
    {
        public static readonly CrosslinkingSettings DEFAULT = new CrosslinkingSettings()
        {
            Crosslinkers = ImmutableList<CrosslinkerDef>.EMPTY,
            MaxFragmentations = 1,
        };
        public ImmutableList<CrosslinkerDef> Crosslinkers { get; private set; }

        public CrosslinkingSettings ChangeCrosslinkers(IEnumerable<CrosslinkerDef> crosslinkers)
        {
            return ChangeProp(ImClone(this), im => im.Crosslinkers = ImmutableList.ValueOfOrEmpty(crosslinkers));
        }
        public int MaxFragmentations { get; private set; }

        public CrosslinkingSettings ChangeMaxFragmentations(int maxFragmentations)
        {
            return ChangeProp(ImClone(this), im => im.MaxFragmentations = maxFragmentations);
        }

        protected bool Equals(CrosslinkingSettings other)
        {
            return Equals(Crosslinkers, other.Crosslinkers) && MaxFragmentations == other.MaxFragmentations;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CrosslinkingSettings) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return Crosslinkers.GetHashCode()* 397 ^ MaxFragmentations;
            }
        }

        private enum ATTR
        {
            max_fragmentations
        }

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            if (null != Crosslinkers)
            {
                throw new InvalidOperationException();
            }

            MaxFragmentations = reader.GetIntAttribute(ATTR.max_fragmentations);
            var crosslinkers = new List<CrosslinkerDef>();
            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.ReadStartElement();
                reader.ReadElements(crosslinkers);
                reader.ReadEndElement();
            }
            Crosslinkers = ImmutableList.ValueOf(crosslinkers);
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.max_fragmentations, MaxFragmentations);
            writer.WriteElements(Crosslinkers);
        }

        public static CrosslinkingSettings Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new CrosslinkingSettings());
        }
    }
  
}
