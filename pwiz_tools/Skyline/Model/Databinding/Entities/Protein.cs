/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [AnnotationTarget(AnnotationDef.AnnotationTarget.protein)]
    [ProteomicDisplayName(nameof(Protein))]
    [InvariantDisplayName("MoleculeList", ExceptInUiMode = UiModes.PROTEOMIC)]
    public class Protein : SkylineDocNode<PeptideGroupDocNode>
    {
        private readonly CachedValue<Peptide[]> _peptides;
        private readonly CachedValue<IDictionary<ResultKey, ProteinResult>> _results;
        private readonly CachedValue<IDictionary<int, Tuple<double, bool>>> _proteinAbundances;
        public Protein(SkylineDataSchema dataSchema, IdentityPath identityPath) : base(dataSchema, identityPath)
        {
            _peptides = CachedValue.Create(dataSchema, () => DocNode.Children
                .Select(node => new Peptide(DataSchema, new IdentityPath(IdentityPath, node.Id))).ToArray());
            _results = CachedValue.Create(dataSchema, MakeProteinResults);
            _proteinAbundances = CachedValue.Create(dataSchema, CalculateProteinAbundances);
        }

        [OneToMany(ForeignKey = "Protein")]
        [HideWhen(AncestorOfType = typeof(FoldChangeBindingSource.FoldChangeRow))]
        [InvariantDisplayName("Molecules", ExceptInUiMode = UiModes.PROTEOMIC)]
        public IList<Peptide> Peptides
        {
            get
            {
                return _peptides.Value;
            }
        }

        [InvariantDisplayName("MoleculeListResults")]
        [ProteomicDisplayName("ProteinResults")]
        public IDictionary<ResultKey, ProteinResult> Results
        {
            get { return _results.Value; }
        }

        private IDictionary<ResultKey, ProteinResult> MakeProteinResults()
        {
            return DataSchema.ReplicateList.ToDictionary(entry => entry.Key,
                entry => new ProteinResult(this, entry.Value));
        }

        public bool IsNonProteomic()
        {
            return Peptides.All(p => p.IsSmallMolecule());
        }

        protected override PeptideGroupDocNode CreateEmptyNode()
        {
            return new PeptideGroupDocNode(new PeptideGroup(), null, null, new PeptideDocNode[0]);
        }

        [ProteomicDisplayName("ProteinName")]
        [InvariantDisplayName("MoleculeListName")]
        public string Name
        {
            get { return DocNode.Name; }
            set { ChangeDocNode(EditColumnDescription(nameof(Name), value),
                docNode=>docNode.ChangeName(value)); } // the user can overide this label
        }
        [InvariantDisplayName("ProteinDescription")]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public string Description
        {
            get { return DocNode.Description; }
            set { ChangeDocNode(EditColumnDescription(nameof(Description), value),
                docNode => docNode.ChangeDescription(value));
            } // the user can ovveride this label
        }
        [InvariantDisplayName("ProteinAccession")]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public string Accession
        {
            get { return DocNode.ProteinMetadata.Accession; }
        }

        [InvariantDisplayName("ProteinPreferredName")]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public string PreferredName
        {
            get { return DocNode.ProteinMetadata.PreferredName; }
        }

        [InvariantDisplayName("ProteinGene")]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public string Gene
        {
            get { return DocNode.ProteinMetadata.Gene; }
        }

        [InvariantDisplayName("ProteinSpecies")]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public string Species
        {
            get { return DocNode.ProteinMetadata.Species; }
        }

        // We don't want this to appear in the Document Grid
        // [DisplayName("ProteinWebSearchStatus")]
        // public string WebSearchStatus
        // {
        //    get { return DocNode.ProteinMetadata.WebSearchInfo.ToString(); }
        // }

        [InvariantDisplayName("ProteinSequence")]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public string Sequence 
        {
            get { return DocNode.PeptideGroup.Sequence; }
        }

        [Importable]
        [InvariantDisplayName("AutoSelectMolecules", ExceptInUiMode = UiModes.PROTEOMIC)]
        public bool AutoSelectPeptides
        {
            get { return DocNode.AutoManageChildren; }
            set
            {
                if (value == AutoSelectPeptides)
                {
                    return;
                }
                ChangeDocNode(EditColumnDescription(nameof(AutoSelectPeptides), value), docNode=>
                {
                    docNode = (PeptideGroupDocNode) docNode.ChangeAutoManageChildren(value);
                    if (docNode.AutoManageChildren)
                    {
                        var srmSettingsDiff = new SrmSettingsDiff(true, false, false, false, false, false);
                        docNode = docNode.ChangeSettings(SrmDocument.Settings, srmSettingsDiff);
                    }

                    return docNode;
                });
            }
        }

        [Format(Formats.Percent, NullValue = TextUtil.EXCEL_NA)]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public double? ProteinSequenceCoverage
        {
            get
            {
                string proteinSequence = DocNode.PeptideGroup.Sequence;
                if (string.IsNullOrEmpty(proteinSequence))
                {
                    return null;
                }

                var peptideSequences = DocNode.Peptides.Select(p => p.Peptide.Sequence).Distinct();
                return FastaSequence.CalculateSequenceCoverage(proteinSequence, peptideSequences);
            }
        }
        [ProteomicDisplayName("ProteinNote")]
        [InvariantDisplayName("MoleculeListNote")]
        [Importable]
        public string Note
        {
            get { return DocNode.Note; }
            set { ChangeDocNode(EditColumnDescription(nameof(Note), value),
                docNode => (PeptideGroupDocNode) docNode.ChangeAnnotations(docNode.Annotations.ChangeNote(value)));
            }
        }
        public override string ToString()
        {
            return Name; // TODO nicksh this needs to agree with Targets view in Document Grid (By Name, By Accession etc)
        }

        public override string GetDeleteConfirmation(int nodeCount)
        {
            if (nodeCount == 1)
            {
                return string.Format(DataSchema.ModeUI == SrmDocument.DOCUMENT_TYPE.proteomic
                    ? Resources.Protein_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_the_protein___0___
                    : Resources.Protein_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_the_molecule_list___0___, this);
            }
            return string.Format(DataSchema.ModeUI == SrmDocument.DOCUMENT_TYPE.proteomic ? Resources.Protein_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_these__0__proteins_
                : Resources.Protein_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_these__0__molecule_lists_, nodeCount);
        }

        protected override NodeRef NodeRefPrototype
        {
            get { return MoleculeGroupRef.PROTOTYPE; }
        }

        protected override Type SkylineDocNodeType { get { return typeof(Protein); } }

        [ProteomicDisplayName("ProteinLocator")]
        [InvariantDisplayName("MoleculeListLocator")]
        public string Locator { get { return GetLocator(); } }

        /// <summary>
        /// Returns a map of replicate number to a tuple containing:
        /// Item1: abundance value
        /// Item2: boolean indicating which is true if transitions were missing in the abundance calculation making it
        /// not suitable for comparing between replicates
        /// </summary>
        /// <returns></returns>
        public IDictionary<int, Tuple<double, bool>> GetProteinAbundances()
        {
            return _proteinAbundances.Value;
        }

        private IDictionary<int, Tuple<double, bool>> CalculateProteinAbundances()
        {
            if (DocNode.IsDecoy)
            {
                // Don't bother calculating protein abundances for the "Decoy" peptide list,
                // since it can be very slow.
                return new Dictionary<int, Tuple<double, bool>>();
            }
            var allTransitionIdentityPaths = new HashSet<IdentityPath>();
            var quantifiers = Peptides.Select(peptide => peptide.GetPeptideQuantifier()).ToList();
            int replicateCount = SrmDocument.Settings.HasResults
                ? SrmDocument.Settings.MeasuredResults.Chromatograms.Count : 0;
            var abundances = new Dictionary<int, Tuple<double, int>>();
            var srmSettings = SrmDocument.Settings;
            bool allowMissingTransitions =
                srmSettings.PeptideSettings.Quantification.NormalizationMethod is NormalizationMethod.RatioToLabel;
            for (int iReplicate = 0; iReplicate < replicateCount; iReplicate++)
            {
                double totalNumerator = 0;
                double totalDenomicator = 0;
                int transitionCount = 0;
                foreach (var peptideQuantifier in quantifiers)
                {
                    foreach (var entry in peptideQuantifier.GetTransitionIntensities(SrmDocument.Settings, iReplicate,
                        false))
                    {
                        totalNumerator += Math.Max(entry.Value.Intensity, 1.0);
                        totalDenomicator += Math.Max(entry.Value.Denominator, 1.0);
                        allTransitionIdentityPaths.Add(entry.Key);
                        transitionCount++;
                    }
                }

                if (transitionCount != 0)
                {
                    var abundance = totalNumerator / totalDenomicator;
                    abundances.Add(iReplicate, Tuple.Create(abundance, transitionCount));
                }
            }

            var proteinAbundanceRecords = new Dictionary<int, Tuple<double, bool>>();
            foreach (var entry in abundances)
            {
                bool incomplete;
                if (allowMissingTransitions)
                {
                    incomplete = false;
                }
                else
                {
                    incomplete = entry.Value.Item2 != allTransitionIdentityPaths.Count;
                }
                proteinAbundanceRecords.Add(entry.Key, Tuple.Create(entry.Value.Item1, incomplete));
            }

            return proteinAbundanceRecords;
        }
    }
}
