using System.Collections.Generic;
using pwiz.Common.Chemistry;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class IonMobilityObject
    {
        private eIonMobilityUnits _units;
        [Format(Formats.IonMobility, NullValue = TextUtil.EXCEL_NA)]
        public double? IonMobilityValue { get; private set; }
        public string IonMobilityUnits
        {
            get { return IonMobilityFilter.IonMobilityUnitsL10NString(_units); }
        }

        [Format(Formats.CCS, NullValue = TextUtil.EXCEL_NA)]
        public double? CollisionCrossSection { get; private set; }
        [Format(Formats.IonMobility, NullValue = TextUtil.EXCEL_NA)]
        public double? IonMobilityHighEnergyOffset { get; private set; }

        public static IonMobilityObject FromIonMobilityAndCCS(IonMobilityAndCCS ionMobilityAndCcs)
        {
            if (IonMobilityAndCCS.IsNullOrEmpty(ionMobilityAndCcs))
            {
                return null;
            }
            return new IonMobilityObject
            {
                IonMobilityValue = ionMobilityAndCcs.IonMobility?.Mobility,
                _units = ionMobilityAndCcs.IonMobility?.Units ?? eIonMobilityUnits.none,
                CollisionCrossSection = ionMobilityAndCcs.CollisionalCrossSectionSqA,
                IonMobilityHighEnergyOffset = ionMobilityAndCcs.HighEnergyIonMobilityValueOffset
            };
        }

        public override string ToString()
        {
            List<string> parts = new List<string>();
            if (IonMobilityValue.HasValue)
            {
                parts.Add(IonMobilityValue.Value.ToString(Formats.IonMobility));
                if (_units != eIonMobilityUnits.none)
                {
                    parts.Add(Common.Chemistry.IonMobilityValue.GetUnitsString(_units));
                }
            }

            if ((IonMobilityHighEnergyOffset ?? 0) != 0)
            {
                parts.Add(Resources.IonMobilityObject_ToString_HEO_); // Compact representation of "HIgh Energy Ion Mobility Offset"
                parts.Add(IonMobilityHighEnergyOffset.Value.ToString(Formats.IonMobility));
            }

            if ((CollisionCrossSection ?? 0) != 0)
            {
                parts.Add(Resources.IonMobilityObject_ToString_CCS_); // Compact representation of "Collision Cross Section"
                parts.Add(CollisionCrossSection.Value.ToString(Formats.CCS));
            }

            return TextUtil.SpaceSeparate(parts);
        }
    }
}
