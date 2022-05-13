using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using Google.Protobuf;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    [XmlRoot("precursor_extra_info")]
    public class PrecursorExtraInfo : Immutable, IXmlSerializable
    {
        public static readonly PrecursorExtraInfo EMPTY = new PrecursorExtraInfo()
        {
            Ms1Precursors = ImmutableList<PrecursorIon>.EMPTY,
            Ms2Precursors = ImmutableList<PrecursorIon>.EMPTY,
            ScanDescription = string.Empty
        };
        private PrecursorExtraInfo()
        {
        }

        public ImmutableList<PrecursorIon> Ms1Precursors { get; private set; }
        public PrecursorExtraInfo ChangeMs1Precursors(IEnumerable<PrecursorIon> precursorIons)
        {
            return ChangeProp(ImClone(this),
                im => im.Ms1Precursors = SortPrecursors(precursorIons));

        }
        public ImmutableList<PrecursorIon> Ms2Precursors { get; private set; }

        public PrecursorExtraInfo ChangeMs2Precursors(IEnumerable<PrecursorIon> precursorIons)
        {
            return ChangeProp(ImClone(this),
                im => im.Ms2Precursors = SortPrecursors(precursorIons));

        }


        public double CollisionEnergy { get; private set; }

        public PrecursorExtraInfo ChangeCollisionEnergy(double collisionEnergy)
        {
            return ChangeProp(ImClone(this), im => im.CollisionEnergy = collisionEnergy);
        }
        public string ScanDescription { get; private set; }

        public PrecursorExtraInfo ChangeScanDescription(string scanDescription)
        {
            return ChangeProp(ImClone(this), im => im.ScanDescription = scanDescription ?? string.Empty);
        }
        public double ScanWindowWidth { get; private set; }

        public PrecursorExtraInfo ChangeScanWindowWith(double scanWindowWidth)
        {
            return ChangeProp(ImClone(this), im => im.ScanWindowWidth = scanWindowWidth);
        }

        protected bool Equals(PrecursorExtraInfo other)
        {
            return Equals(Ms1Precursors, other.Ms1Precursors) &&
                   Equals(Ms2Precursors, other.Ms2Precursors) &&
                   Equals(CollisionEnergy, other.CollisionEnergy) && 
                   ScanDescription == other.ScanDescription &&
                   Equals(ScanWindowWidth, other.ScanWindowWidth);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PrecursorExtraInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Ms1Precursors.GetHashCode();
                hashCode = (hashCode * 397) ^ Ms2Precursors.GetHashCode();
                hashCode = (hashCode * 397) ^ CollisionEnergy.GetHashCode();
                hashCode = (hashCode * 397) ^ ScanDescription.GetHashCode();
                hashCode = (hashCode * 397) ^ ScanWindowWidth.GetHashCode();
                return hashCode;
            }
        }

        public bool Matches(SpectrumMetadata spectrumMetadata, double tolerance)
        {
            if (!string.IsNullOrEmpty(ScanDescription))
            {
                if (ScanDescription != spectrumMetadata.ScanDescription)
                {
                    return false;
                }
            }

            if (ScanWindowWidth != 0)
            {
                var scanWindowWidth = spectrumMetadata.ScanWindowUpperLimit - spectrumMetadata.ScanWindowLowerLimit;
                if (!scanWindowWidth.HasValue || Math.Abs(scanWindowWidth.Value - ScanWindowWidth) > tolerance)
                {
                    return false;
                }
            }

            if (CollisionEnergy != 0 && CollisionEnergy != spectrumMetadata.CollisionEnergy.GetValueOrDefault())
            {
                return false;
            }

            if (Ms1Precursors.Any())
            {
                if (!PrecursorsMatch(Ms1Precursors, spectrumMetadata.GetPrecursors(1), tolerance))
                {
                    return false;
                }
            }

            if (!PrecursorsMatch(Ms2Precursors, spectrumMetadata.GetPrecursors(2), tolerance))
            {
                return false;
            }

            return true;
        }

        public static bool PrecursorsMatch(IList<PrecursorIon> expected, IList<SpectrumPrecursor> actual,
            double tolerance)
        {
            foreach (var ion in expected)
            {
                if (actual.All(p => p.PrecursorMz.CompareTolerant(ion.Mz, tolerance) != 0))
                {
                    return false;
                }
            }

            foreach (var ion in actual)
            {
                if (expected.All(p => p.Mz.CompareTolerant(ion.PrecursorMz, tolerance) == 0))
                {
                    return false;
                }
            }

            return true;
        }

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        private enum Attr
        {
            scan_description,
            collision_energy,
            scan_window_width,
            mz,
            ms_level
        }

        private enum El
        {
            precursor
        }

        public void ReadXml(XmlReader reader)
        {
            if (Ms1Precursors != null)
            {
                throw new InvalidOperationException();
            }

            XElement element = (XElement) XNode.ReadFrom(reader);
            CollisionEnergy = element.GetNullableDouble(Attr.collision_energy) ?? 0;
            ScanDescription = element.Attribute(Attr.scan_description)?.Value ?? string.Empty;
            ScanWindowWidth = element.GetNullableDouble(Attr.scan_window_width) ?? 0;
            var ms1Precursors = new List<PrecursorIon>();
            var ms2Precursors = new List<PrecursorIon>();
            foreach (var elPrecursor in element.Elements(El.precursor))
            {
                double? precursorMz = elPrecursor.GetNullableDouble(Attr.mz);
                if (!precursorMz.HasValue)
                {
                    continue;
                }

                var precursor = new PrecursorIon(new SignedMz(precursorMz.Value, precursorMz < 0));
                switch (elPrecursor.GetNullableInt(Attr.ms_level))
                {
                    case 1:
                        ms1Precursors.Add(precursor);
                        break;
                    case 2:
                        ms2Precursors.Add(precursor);
                        break;
                }
            }

            Ms1Precursors = SortPrecursors(ms1Precursors);
            Ms2Precursors = SortPrecursors(ms2Precursors);
        }

        private static ImmutableList<PrecursorIon> SortPrecursors(IEnumerable<PrecursorIon> ions)
        {
            if (ions == null)
            {
                return ImmutableList<PrecursorIon>.EMPTY;
            }

            return ImmutableList.ValueOf(ions.OrderBy(i => i));
        }

        public void WriteXml(XmlWriter writer)
        {
            if (!string.IsNullOrEmpty(ScanDescription))
            {
                writer.WriteAttribute(Attr.scan_description, ScanDescription);
            }

            if (0 != CollisionEnergy)
            {
                writer.WriteAttribute(Attr.collision_energy, CollisionEnergy);
            }

            if (0 != ScanWindowWidth)
            {
                writer.WriteAttribute(Attr.scan_window_width, ScanWindowWidth);
            }

            IEnumerable<Tuple<int, PrecursorIon>> precursors = Ms1Precursors.Select(p => Tuple.Create(1, p))
                .Concat(Ms2Precursors.Select(p => Tuple.Create(2, p)));
            foreach (var tuple in precursors)
            {
                writer.WriteStartElement(El.precursor);
                writer.WriteAttribute(Attr.mz, tuple.Item2.Mz.RawValue);
                writer.WriteAttribute(Attr.ms_level, tuple.Item1);
                writer.WriteEndElement();
            }
        }
    }

    public class PrecursorIon : IComparable<PrecursorIon>
    {
        public PrecursorIon(SignedMz mz)
        {
            Mz = mz;
        }

        public int MsLevel { get; private set; }
        public SignedMz Mz { get; private set; }

        public int CompareTo(PrecursorIon other)
        {
            return MsLevel.CompareTo(other.MsLevel);
        }

        protected bool Equals(PrecursorIon other)
        {
            return MsLevel == other.MsLevel && Mz.Equals(other.Mz);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PrecursorIon) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (MsLevel * 397) ^ Mz.GetHashCode();
            }
        }
    }

    public class PrecursorKey : Immutable
    {
        public static readonly PrecursorKey EMPTY = new PrecursorKey(Adduct.EMPTY);
        public PrecursorKey(Adduct adduct) : this(adduct, null)
        {
        }
        public PrecursorKey(Adduct adduct, PrecursorExtraInfo precursorExtraInfo)
        {
            Adduct = adduct.Unlabeled;
            PrecursorExtraInfo = precursorExtraInfo;
        }

        public Adduct Adduct { get; private set; }

        public PrecursorExtraInfo PrecursorExtraInfo { get; private set; }

        protected bool Equals(PrecursorKey other)
        {
            return Equals(Adduct, other.Adduct) && Equals(PrecursorExtraInfo, other.PrecursorExtraInfo);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PrecursorKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Adduct.GetHashCode() * 397) ^
                       (PrecursorExtraInfo != null ? PrecursorExtraInfo.GetHashCode() : 0);
            }
        }
    }
}
