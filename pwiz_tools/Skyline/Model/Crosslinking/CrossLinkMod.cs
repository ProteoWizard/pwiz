using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Util;


namespace pwiz.Skyline.Model.Crosslinking
{
    public class CrosslinkMod : Immutable
    {
        public CrosslinkMod(int aaIndex, CrosslinkerDef crosslinkerDef, IEnumerable<LinkedPeptide> linkedPeptides)
        {
            AaIndex = aaIndex;
            CrosslinkerDef = crosslinkerDef;
            LinkedPeptides = ImmutableList.ValueOf(linkedPeptides);
        }

        public int AaIndex { get; private set; }

        public CrosslinkerDef CrosslinkerDef { get; private set; }

        public ImmutableList<LinkedPeptide> LinkedPeptides { get; private set; }

        public MoleculeMassOffset GetNeutralFormula(SrmSettings settings, IsotopeLabelType labelType)
        {
            var massType = settings.TransitionSettings.Prediction.PrecursorMassType;
            MoleculeMassOffset moleculeMassOffset = CrosslinkerDef.IntactFormula.GetMoleculeMassOffset(massType);
            foreach (var linkedPeptide in LinkedPeptides)
            {
                moleculeMassOffset = moleculeMassOffset.Plus(linkedPeptide.GetNeutralFormula(settings, labelType));
            }

            return moleculeMassOffset;
        }

        public ModificationSite ModificationSite
        {
            get
            {
                return new ModificationSite(AaIndex, CrosslinkerDef.Name);
            }
        }

        private enum EL
        {
            crosslink,
            linked_peptide
        }

        private enum ATTR
        {
            aa_index,
            crosslinker_name,
        }

        public void WriteToXml(DocumentWriter documentWriter, XmlWriter writer)
        {
            writer.WriteStartElement(EL.crosslink);
            writer.WriteAttribute(ATTR.crosslinker_name, CrosslinkerDef.Name);
            writer.WriteAttribute(ATTR.aa_index, AaIndex);
            foreach (var linkedPeptide in LinkedPeptides)
            {
                linkedPeptide.WriteToXml(documentWriter, writer);
            }
            writer.WriteEndElement();
        }

        public static CrosslinkMod ReadFromXml(DocumentReader documentReader, XmlReader reader)
        {
            if (!reader.IsStartElement(EL.crosslink))
            {
                return null;
            }
            var crosslinkerName = reader.GetAttribute(ATTR.crosslinker_name);
            var aaIndex = reader.GetIntAttribute(ATTR.aa_index);
            var linkedPeptides = new List<LinkedPeptide>();
            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.ReadStartElement();
                while (true)
                {
                    var linkedPeptide = LinkedPeptide.ReadFromXml(documentReader, reader);
                    if (linkedPeptide == null)
                    {
                        break;
                    }
                    linkedPeptides.Add(linkedPeptide);
                }
                reader.ReadEndElement();
            }

            var crosslinkerDef = documentReader.Settings.PeptideSettings.Modifications.CrosslinkingSettings
                .Crosslinkers.FirstOrDefault(linker => linker.Name == crosslinkerName);
            if (crosslinkerDef == null)
            {
                throw new InvalidOperationException(string.Format("Unable to find crosslinker definition for '{0}", crosslinkerName));
            }

            return new CrosslinkMod(aaIndex, crosslinkerDef, linkedPeptides);
        }
    }
}
