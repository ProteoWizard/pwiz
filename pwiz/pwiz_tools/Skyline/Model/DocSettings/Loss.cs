/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    [XmlRoot("potential_loss")]
    public sealed class FragmentLoss : Immutable, IXmlSerializable
    {
        public const double MIN_LOSS_MASS = 0.0001;
        public const double MAX_LOSS_MASS = 600;

        private string _formula;

        public FragmentLoss(string formula)
            : this(formula, null, null)
        {            
        }

        public FragmentLoss(string formula, double? monoisotopicMass, double? averageMass)
        {
            MonoisotopicMass = monoisotopicMass ?? 0;
            AverageMass = averageMass ?? 0;
            Formula = formula;

            Validate();
        }

        public string Formula
        {
            get { return _formula; }
            private set
            {
                _formula = value;
                if (_formula != null)
                {
                    MonoisotopicMass = SequenceMassCalc.ParseModMass(BioMassCalc.MONOISOTOPIC, Formula);
                    AverageMass = SequenceMassCalc.ParseModMass(BioMassCalc.AVERAGE, Formula);
                }
            }
        }

        public double MonoisotopicMass { get; private set; }
        public double AverageMass { get; private set; }

        public double GetMass(MassType massType)
        {
            return massType == MassType.Monoisotopic ? MonoisotopicMass : AverageMass;
        }

        /// <summary>
        /// Losses are always sorted by m/z, to avoid the need for names and a
        /// full user interface around list editing.
        /// </summary>
        public static IList<FragmentLoss> SortByMz(IList<FragmentLoss> losses)
        {
            if (losses.Count < 2)
                return losses;
            var arrayLosses = losses.ToArray();
            Array.Sort(arrayLosses, (l1, l2) =>
                Comparer<double>.Default.Compare(l1.MonoisotopicMass, l2.MonoisotopicMass));
            return arrayLosses;
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private FragmentLoss()
        {
        }

        private enum ATTR
        {
            formula,
            massdiff_monoisotopic,
            massdiff_average
        }

        private void Validate()
        {
            if (MonoisotopicMass == 0 || AverageMass == 0)
                throw new InvalidDataException("Neutral losses must specify a formula or valid monoisotopic and average masses.");
            if (MonoisotopicMass <= MIN_LOSS_MASS || AverageMass <= MIN_LOSS_MASS)
                throw new InvalidDataException(string.Format("Neutral losses must be greater than or equal to {0}.", MIN_LOSS_MASS));
            if (MonoisotopicMass > MAX_LOSS_MASS || AverageMass > MAX_LOSS_MASS)
                throw new InvalidDataException(string.Format("Neutral losses must be less than or equal to {0}.", MAX_LOSS_MASS));
        }

        public static FragmentLoss Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new FragmentLoss());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            MonoisotopicMass = reader.GetNullableDoubleAttribute(ATTR.massdiff_monoisotopic) ?? 0;
            AverageMass = reader.GetNullableDoubleAttribute(ATTR.massdiff_average) ?? 0;
            Formula = reader.GetAttribute(ATTR.formula);

            // Consume tag
            reader.Read();

            Validate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            writer.WriteAttributeIfString(ATTR.formula, Formula);
            if (Formula == null)
            {
                writer.WriteAttribute(ATTR.massdiff_monoisotopic, MonoisotopicMass);
                writer.WriteAttribute(ATTR.massdiff_average, AverageMass);
            }
        }

        #endregion

        #region object overrides

        public bool Equals(FragmentLoss other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Formula, Formula) &&
                other.MonoisotopicMass.Equals(MonoisotopicMass) &&
                other.AverageMass.Equals(AverageMass);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(FragmentLoss)) return false;
            return Equals((FragmentLoss)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (Formula != null ? Formula.GetHashCode() : 0);
                result = (result * 397) ^ MonoisotopicMass.GetHashCode();
                result = (result * 397) ^ AverageMass.GetHashCode();
                return result;
            }
        }

        public override string ToString()
        {
            return ToString(MassType.Monoisotopic);
        }

        public string ToString(MassType massType)
        {
            return Formula != null ?
                string.Format("{0:F04} - {1}", GetMass(massType), Formula) :
                string.Format("{0:F04}", GetMass(massType));
        }

        #endregion
    }

    public sealed class TransitionLosses : Immutable
    {
        private OneOrManyList<TransitionLoss> _losses;

        public TransitionLosses(List<TransitionLoss> losses, MassType massType)
        {
            // Make sure losses are always in a consistent order, ascending my mass
            if (losses.Count > 1)
                losses.Sort((l1, l2) => Comparer<double>.Default.Compare(l1.Mass, l2.Mass));

            _losses = new OneOrManyList<TransitionLoss>(losses);
            MassType = massType;
            Mass = CalcLossMass(Losses);
        }

        public IList<TransitionLoss> Losses { get { return _losses; } }
        public MassType MassType { get; private set; }
        public double Mass { get; private set; }

        private static double CalcLossMass(IEnumerable<TransitionLoss> losses)
        {
            double mass = 0;
            foreach (TransitionLoss loss in losses)
                mass += loss.Mass;
            return mass;
        }

        #region Property change methods

        public TransitionLosses ChangeMassType(MassType massType)
        {
            var listLosses = new List<TransitionLoss>();
            foreach (var loss in Losses)
                listLosses.Add(new TransitionLoss(loss.PrecursorMod, loss.Loss, massType));
            if (ArrayUtil.EqualsDeep(listLosses, _losses))
                return this;
            return ChangeProp(ImClone(this), im =>
            {
                im._losses = new OneOrManyList<TransitionLoss>(listLosses);
                im.Mass = CalcLossMass(im.Losses);
            });
        }

        #endregion

        #region object overrides

        /// <summary>
        /// From a transition loss perspective, losses with equall masses
        /// are equal.  It is not necessary to compare the exact losses.
        /// </summary>
        public bool Equals(TransitionLosses other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.Mass == Mass;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(TransitionLosses)) return false;
            return Equals((TransitionLosses)obj);
        }

        public override int GetHashCode()
        {
            return Mass.GetHashCode();
        }

        #endregion

        public string[] ToStrings()
        {
            return (from loss in Losses
                    select loss.Loss.ToString(MassType)).ToArray();
        }
    }

    public struct TransitionLoss
    {
        public TransitionLoss(StaticMod precursorMod, FragmentLoss loss, MassType massType)
            : this()
        {
            PrecursorMod = precursorMod;
            Loss = loss;
            Mass = Loss.GetMass(massType);
        }

        public StaticMod PrecursorMod { get; private set; }
        public FragmentLoss Loss { get; private set; }
        public double Mass { get; private set; }

        public int LossIndex
        {
            get
            {
                int lossIndex = PrecursorMod.Losses.IndexOf(Loss);
                if (lossIndex == -1)
                    throw new InvalidDataException(string.Format("Expected loss {0} not found in the modification {1}", this, PrecursorMod.Name));
                return lossIndex;
            }
        }
    }

    /// <summary>
    /// An explicit transition loss with a specific amino acid location.  Only
    /// used in generating the full set of possible losses.  Loss position is
    /// not knowable in a triple-quadrupole.
    /// </summary>
    public struct ExplicitLoss
    {
        public ExplicitLoss(int indexAA, TransitionLoss transitionLoss)
            : this()
        {
            IndexAA = indexAA;
            TransitionLoss = transitionLoss;
        }

        public int IndexAA { get; private set; }
        public TransitionLoss TransitionLoss { get; private set; }
    }
}
