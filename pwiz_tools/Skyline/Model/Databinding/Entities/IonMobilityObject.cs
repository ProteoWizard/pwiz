using System.Collections.Generic;
using pwiz.Common.Chemistry;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
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

        [Format(Formats.IonMobility, NullValue = TextUtil.EXCEL_NA)]
        public double? CollisionCrossSection { get; private set; }
        [Format(Formats.IonMobility, NullValue = TextUtil.EXCEL_NA)]
        public double? HighEnergyOffset { get; private set; }

        public static IonMobilityObject FromIonMobilityAndCCS(IonMobilityAndCCS ionMobilityAndCcs)
        {
            if (ionMobilityAndCcs == null || ionMobilityAndCcs.IsEmpty)
            {
                return null;
            }
            return new IonMobilityObject
            {
                IonMobilityValue = ionMobilityAndCcs.IonMobility?.Mobility,
                _units = ionMobilityAndCcs.IonMobility?.Units ?? eIonMobilityUnits.none,
                CollisionCrossSection = ionMobilityAndCcs.CollisionalCrossSectionSqA,
                HighEnergyOffset = ionMobilityAndCcs.HighEnergyIonMobilityValueOffset
            };
        }

        public override string ToString()
        {
            List<string> parts = new List<string>();
            if (IonMobilityValue.HasValue)
            {
                parts.Add(IonMobilityValue.Value.ToString(Formats.IonMobility));
            }

            if (_units != eIonMobilityUnits.none)
            {
                parts.Add(IonMobilityUnits);
            }

            // Consider: maybe include CollisionCrossSection and HighEnergyOffset if present?
            return TextUtil.SpaceSeparate(parts);
        }
    }
}
