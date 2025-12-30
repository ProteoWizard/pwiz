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
    /// Database record for molecules (peptides/small molecules).
    /// </summary>
    internal class MoleculeRecord : Record
    {
        private readonly PeptideDocNode _node;

        public MoleculeRecord(PeptideDocNode node, long id, long moleculeGroupId) : base(id)
        {
            _node = node;
            MoleculeGroupId = moleculeGroupId;
        }

        [Column(IsRequired = true)]
        public long MoleculeGroupId { get; }

        [Column(IsRequired = true)]
        public string MoleculeType => _node.Peptide.IsCustomMolecule ? "molecule" : "peptide";

        [Column]
        public string Sequence => _node.Peptide.Sequence;

        [Column]
        public string ModifiedSequence => _node.ModifiedSequenceDisplay;

        [Column]
        public string LookupSequence => null;

        [Column]
        public int? StartIndex => _node.Peptide.Begin;

        [Column]
        public int? EndIndex => _node.Peptide.End;

        [Column]
        public string PrevAa => _node.Peptide.Begin.HasValue ? _node.Peptide.PrevAA.ToString() : null;

        [Column]
        public string NextAa => _node.Peptide.End.HasValue ? _node.Peptide.NextAA.ToString() : null;

        [Column]
        public bool? IsDecoy => _node.IsDecoy ? true : (bool?)null;

        [Column]
        public double? CalcNeutralPepMass => _node.Peptide.IsCustomMolecule ? _node.Peptide.CustomMolecule.MonoisotopicMass : (double?)null;

        [Column]
        public int? NumMissedCleavages => _node.Peptide.MissedCleavages;

        [Column]
        public int? Rank => _node.Rank;

        [Column]
        public double? RtCalculatorScore => null;

        [Column]
        public double? PredictedRetentionTime => null;

        [Column]
        public double? AvgMeasuredRetentionTime => _node.AverageMeasuredRetentionTime;

        [Column]
        public string StandardType => _node.GlobalStandardType?.Name;

        [Column]
        public double? ExplicitRetentionTime => _node.ExplicitRetentionTime?.RetentionTime;

        [Column]
        public double? ExplicitRetentionTimeWindow => _node.ExplicitRetentionTime?.RetentionTimeWindow;

        [Column]
        public double? ConcentrationMultiplier => null;

        [Column]
        public double? InternalStandardConcentration => null;

        [Column]
        public string NormalizationMethod => null;

        [Column]
        public string AttributeGroupId => null;

        [Column]
        public string SurrogateCalibrationCurve => null;

        [Column]
        public string NeutralFormula => _node.Peptide.IsCustomMolecule ? _node.Peptide.CustomMolecule.Formula : null;

        [Column]
        public double? NeutralMassMonoisotopic => _node.Peptide.IsCustomMolecule ? _node.Peptide.CustomMolecule.MonoisotopicMass : (double?)null;

        [Column]
        public double? NeutralMassAverage => _node.Peptide.IsCustomMolecule ? _node.Peptide.CustomMolecule.AverageMass : (double?)null;

        [Column]
        public string CustomIonName => _node.Peptide.IsCustomMolecule ? _node.Peptide.CustomMolecule.Name : null;

        [Column]
        public string MoleculeIdExternal => null;

        [Column]
        public string ChromatogramTarget => null;

        [Column]
        public bool? AutoManageChildren => _node.AutoManageChildren ? true : (bool?)null;

        [Column]
        public string Note => _node.Note;
    }
}
