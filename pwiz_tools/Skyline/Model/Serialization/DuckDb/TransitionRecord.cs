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

namespace pwiz.Skyline.Model.Serialization.DuckDb
{
    /// <summary>
    /// Database record for transitions.
    /// </summary>
    internal class TransitionRecord : Record
    {
        private readonly TransitionDocNode _node;

        public TransitionRecord(TransitionDocNode node, long id, long transitionGroupId) : base(id)
        {
            _node = node;
            TransitionGroupId = transitionGroupId;
        }

        [Column]
        public long TransitionGroupId { get; }

        [Column]
        public string TransitionType => _node.Transition.IsCustom() ? "non_proteomic" : "proteomic";

        [Column]
        public string FragmentType => _node.Transition.IonType.ToString();

        [Column]
        public int? FragmentOrdinal => _node.Transition.Ordinal > 0 ? (int?)_node.Transition.Ordinal : null;

        [Column]
        public int? MassIndex => _node.Transition.MassIndex > 0 ? (int?)_node.Transition.MassIndex : null;

        [Column]
        public int ProductCharge => _node.Transition.Adduct.AdductCharge;

        [Column]
        public int? IsotopeDistRank => _node.IsotopeDistInfo?.Rank;

        [Column]
        public double? IsotopeDistProportion => _node.IsotopeDistInfo?.Proportion;

        [Column]
        public bool? Quantitative => _node.ExplicitQuantitative ? true : (bool?)null;

        [Column]
        public double? ExplicitCollisionEnergy => _node.ExplicitValues.CollisionEnergy;

        [Column]
        public double? ExplicitDeclusteringPotential => _node.ExplicitValues.DeclusteringPotential;

        [Column]
        public double? ExplicitIonMobilityHighEnergyOffset => _node.ExplicitValues.IonMobilityHighEnergyOffset;

        [Column]
        public double? ExplicitSLens => _node.ExplicitValues.SLens;

        [Column]
        public double? ExplicitConeVoltage => _node.ExplicitValues.ConeVoltage;

        [Column]
        public double? PrecursorMz => null;

        [Column]
        public double ProductMz => _node.Mz.Value;

        [Column]
        public double? CollisionEnergy => null;

        [Column]
        public double? DeclusteringPotential => null;

        [Column]
        public double? CalcNeutralMass => _node.Transition.IsCustom() ? (double?)null : _node.GetMoleculeMass();

        [Column]
        public double? LossNeutralMass => null;

        [Column]
        public string CleavageAa => _node.Transition.IsCustom() ? null : _node.Transition.AA.ToString();

        [Column]
        public double? DecoyMassShift => _node.Transition.DecoyMassShift;

        [Column]
        public string MeasuredIonName => _node.Transition.CustomIon?.Name;

        [Column]
        public string Note => _node.Note;
    }
}
