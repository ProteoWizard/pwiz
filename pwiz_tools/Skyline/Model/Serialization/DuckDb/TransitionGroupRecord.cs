/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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

using pwiz.Common.Chemistry;

namespace pwiz.Skyline.Model.Serialization.DuckDb
{
    /// <summary>
    /// Database record for transition groups (precursors).
    /// </summary>
    internal class TransitionGroupRecord : Record
    {
        private readonly TransitionGroupDocNode _node;

        public TransitionGroupRecord(TransitionGroupDocNode node, long id, long moleculeId) : base(id)
        {
            _node = node;
            MoleculeId = moleculeId;
        }

        [Column]
        public long MoleculeId { get; }

        [Column]
        public string TransitionGroupType => _node.TransitionGroup.IsCustomIon ? "non_proteomic" : "proteomic";

        [Column]
        public int Charge => _node.TransitionGroup.PrecursorAdduct.AdductCharge;

        [Column]
        public double PrecursorMz => _node.PrecursorMz.Value;

        [Column]
        public string IsotopeLabel => _node.TransitionGroup.LabelType?.Name;

        [Column]
        public double? CollisionEnergy => _node.ExplicitValues.CollisionEnergy;

        [Column]
        public double? DeclusteringPotential => null;

        [Column]
        public double? Ccs => _node.ExplicitValues.CollisionalCrossSectionSqA;

        [Column]
        public double? ExplicitCollisionEnergy => _node.ExplicitValues.CollisionEnergy;

        [Column]
        public double? ExplicitIonMobility => _node.ExplicitValues.IonMobility;

        [Column]
        public string ExplicitIonMobilityUnits => _node.ExplicitValues.IonMobilityUnits == eIonMobilityUnits.none ? null : _node.ExplicitValues.IonMobilityUnits.ToString();

        [Column]
        public double? ExplicitCcsSqa => null;

        [Column]
        public double? ExplicitCompensationVoltage => _node.ExplicitValues.CompensationVoltage;

        [Column]
        public double? PrecursorConcentration => null;

        [Column]
        public double? CalcNeutralMass => _node.TransitionGroup.IsCustomIon ? (double?)null : _node.GetPrecursorIonMass();

        [Column]
        public double? DecoyMassShift => _node.TransitionGroup.DecoyMassShift;

        [Column]
        public string ModifiedSequence => _node.TransitionGroup.IsCustomIon ? null : _node.TransitionGroup.Peptide?.Sequence;

        [Column]
        public string IonFormula => null;

        [Column]
        public string CustomIonName => null;

        [Column]
        public double? NeutralMassMonoisotopic => null;

        [Column]
        public double? NeutralMassAverage => null;

        [Column]
        public string PrecursorIdExternal => null;

        [Column]
        public bool? AutoManageChildren => _node.AutoManageChildren ? true : (bool?)null;

        [Column]
        public string Note => _node.Note;
    }
}
