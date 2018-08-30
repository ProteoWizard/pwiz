/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Util;

// ReSharper disable VirtualMemberCallInConstructor

namespace pwiz.Skyline.Model.IonMobility
{
    public class DbIonMobilityPeptide : DbAbstractPeptide
    {
        public override Type EntityClass
        {
            get { return typeof(DbIonMobilityPeptide); }
        }

        private Adduct _adduct;

        // public virtual long? ID { get; set; } // in DbEntity
        public virtual double CollisionalCrossSection { get; set; }

        public virtual double HighEnergyDriftTimeOffsetMsec { get; set; }

        public virtual string PrecursorAdduct // Adducts change the CCS for a molecule
        {
            get
            {
                return _adduct.AsFormulaOrSignedInt();
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _adduct = Adduct.EMPTY;
                }
                else if (int.TryParse(value, out _))
                {
                    _adduct = Adduct.FromStringAssumeProtonated(value);
                }
                else
                {
                    _adduct = Adduct.FromStringAssumeProtonatedNonProteomic(value);
                }
            }
        } 

        /// <summary>
        /// For NHibernate only
        /// </summary>
        protected DbIonMobilityPeptide()
        {
            _adduct = Adduct.EMPTY;
        }

        public DbIonMobilityPeptide(DbIonMobilityPeptide other)
            : this(other.ModifiedTarget, other.CollisionalCrossSection, other.HighEnergyDriftTimeOffsetMsec,
                other._adduct)
        {
            Id = other.Id;
        }

        public DbIonMobilityPeptide(Target sequence, Adduct precursorAdduct, double collisionalCrossSection,
            double highEnergyDriftTimeOffsetMsec) : this(sequence, collisionalCrossSection, highEnergyDriftTimeOffsetMsec,
            precursorAdduct)
        { }

        public DbIonMobilityPeptide(SmallMoleculeLibraryAttributes smallMoleculeLibraryAttributes,
            Adduct precursorAdduct, 
            double collisionalCrossSection,
            double highEnergyDriftTimeOffsetMsec)
            : this(new Target(smallMoleculeLibraryAttributes), collisionalCrossSection, highEnergyDriftTimeOffsetMsec,
            precursorAdduct)
        { }

        private DbIonMobilityPeptide(Target sequence, double collisionalCrossSection,
            double highEnergyDriftTimeOffsetMsec,
            Adduct precursorAdduct)
        {
            ModifiedTarget = sequence;
            CollisionalCrossSection = collisionalCrossSection;
            HighEnergyDriftTimeOffsetMsec = highEnergyDriftTimeOffsetMsec;
            _adduct = precursorAdduct;
        }

        public virtual Adduct GetPrecursorAdduct()
        {
            return _adduct;
        }

        public virtual LibKey GetLibKey()
        {
            if (ModifiedTarget.IsProteomic)
            {
                // Unnormalized modified sequences will not match anything.  The user interface
                // attempts to enforce only normalized modified sequences, but this extra protection
                // handles IonMobilitydb files edited outside Skyline.  TODO - copied from iRT code - is this an issue here?
                return new LibKey(GetNormalizedModifiedSequence(), _adduct.AdductCharge);
            }
            return new LibKey(ModifiedTarget.Molecule.PrimaryEquivalenceKey, _adduct);
        }

    #region object overrides

        public virtual bool Equals(DbIonMobilityPeptide other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                   Equals(other.ModifiedTarget, ModifiedTarget) &&
                   Equals(_adduct, other._adduct) &&
                   other.HighEnergyDriftTimeOffsetMsec.Equals(other.HighEnergyDriftTimeOffsetMsec) &&
                   other.CollisionalCrossSection.Equals(CollisionalCrossSection);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as DbIonMobilityPeptide);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ (ModifiedTarget != null ? ModifiedTarget.GetHashCode() : 0);
                result = (result*397) ^ CollisionalCrossSection.GetHashCode();
                result = (result*397) ^ HighEnergyDriftTimeOffsetMsec.GetHashCode();
                result = (result*397) ^ _adduct.GetHashCode();
                return result;
            }
        }

        #endregion
    }

}
