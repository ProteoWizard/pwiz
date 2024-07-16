using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    public class LabelLayout : Immutable, IXmlSerializable
    {
        public static LabelLayout DEFAULT = new LabelLayout() { _labels = ImmutableList<PointLayout>.EMPTY };

        public class PointLayout : ICloneable, IXmlSerializable, INotifyPropertyChanged
        {
            private string _identity;
            private PointF _labelLocation;
            private PointF _pontLocation;


            [Track]
            public string Identity
            {
                get { return _identity; }
                private set
                {
                    _identity = value;
                    NotifyPropertyChanged();
                }
            }
            [Track]
            public PointF PointLocation {
                get
                { return _pontLocation; }
                private set
                {
                    _pontLocation = value;
                    NotifyPropertyChanged();
                }
            }
            [Track]
            public PointF LabelLocation
            {
                get { return _labelLocation; }
                private set
                {
                    _labelLocation = value;
                    NotifyPropertyChanged();
                }
            }

            object ICloneable.Clone()
            {
                return MemberwiseClone();
            }

            public PointLayout Clone()
            {
                return (PointLayout)MemberwiseClone();
            }
            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = 397 ^ (Identity != null ? Identity.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ PointLocation.GetHashCode();
                    hashCode = (hashCode * 397) ^ LabelLocation.GetHashCode();
                    return hashCode;
                }
            }
            XmlSchema IXmlSerializable.GetSchema()
            {
                return null;
            }

            private enum ATTR
            {
                identity,
                pointX,
                pointY,
                labelX,
                labelY
            }

            public void ReadXml(XmlReader reader)
            {
                Identity = reader.GetAttribute(ATTR.identity);
                PointLocation = new PointF(reader.GetFloatAttribute(ATTR.pointX),
                    reader.GetFloatAttribute(ATTR.pointY));
                LabelLocation = new PointF(reader.GetFloatAttribute(ATTR.labelX), reader.GetFloatAttribute(ATTR.labelY));
            }

            public void WriteXml(XmlWriter writer)
            {
                writer.WriteAttributeString(ATTR.identity, Identity);
                writer.WriteAttribute(ATTR.pointX, PointLocation.X);
                writer.WriteAttribute(ATTR.pointY, PointLocation.Y);
                writer.WriteAttribute(ATTR.labelX, LabelLocation.X);
                writer.WriteAttribute(ATTR.labelY, LabelLocation.Y);
            }
            protected virtual void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;


        }

        protected ImmutableList<PointLayout> _labels;

        protected LabelLayout() { }
        
        [TrackChildren] 
        public IEnumerable<PointLayout> LabeledPoints => _labels.Select(l => l.Clone());

        public LabelLayout ChangeLabelLayout(IEnumerable<PointLayout> labels)
        {
            var newLabels= ImmutableList.ValueOfOrEmpty(labels?.Select(label => label.Clone()));
            if (Equals(newLabels, _labels))
            {
                return this;
            }
            return ChangeProp(ImClone(this), im => im._labels = newLabels);
        }

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            if (_labels != null)
            {
                throw new InvalidOperationException();
            }

            bool empty = reader.IsEmptyElement;
            reader.Read();
            if (!empty)
            {
                var labels = new List<PointLayout>();
                reader.ReadElements(labels);
                _labels = ImmutableList.ValueOf(labels);
                reader.ReadEndElement();
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteElements(_labels);
        }

        protected bool Equals(LabelLayout other)
        {
            return _labels.Equals(other._labels);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((LabelLayout)obj);
        }

        public override int GetHashCode()
        {
            return _labels.GetHashCode();
        }
    }

    [XmlRoot("group_comparison_label_layout")]
    public class GroupComparisonLabelLayout : LabelLayout
    {
        public new static GroupComparisonLabelLayout DEFAULT = new GroupComparisonLabelLayout() { _labels = ImmutableList<PointLayout>.EMPTY };
    }
    [XmlRoot("relative_abundance_label_layout")]
    public class RelativeAbundanceLabelLayout : LabelLayout
    {
        public new static RelativeAbundanceLabelLayout DEFAULT = new RelativeAbundanceLabelLayout() { _labels = ImmutableList<PointLayout>.EMPTY };
    }


}
