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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [AnnotationTarget(AnnotationDef.AnnotationTarget.protein)]
    [ProteomicDisplayName(nameof(Protein))]
    [InvariantDisplayName("MoleculeList", ExceptInUiMode = UiModes.PROTEOMIC)]
    public class Protein : SkylineDocNode<PeptideGroupDocNode>
    {
        private readonly CachedValue<Peptide[]> _peptides;
        private readonly CachedValue<IDictionary<ResultKey, ResultFile>> _results;
        public Protein(SkylineDataSchema dataSchema, IdentityPath identityPath) : base(dataSchema, identityPath)
        {
            _peptides = CachedValue.Create(dataSchema, () => DocNode.Children
                .Select(node => new Peptide(DataSchema, new IdentityPath(IdentityPath, node.Id))).ToArray());
            _results = CachedValue.Create(dataSchema, MakeResults);
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

        [HideWhen(AncestorsOfAnyOfTheseTypes = new []{typeof(SkylineDocument), typeof(FoldChangeBindingSource.FoldChangeRow)})]
        [Obsolete]
        public IDictionary<ResultKey, ResultFile> Results
        {
            get { return _results.Value; }
        }

        private IDictionary<ResultKey, ResultFile> MakeResults()
        {
            var dict = new Dictionary<ResultKey, ResultFile>();
            var document = SrmDocument;
            if (document.Settings.HasResults)
            {
                for (int iResult = 0; iResult < document.Settings.MeasuredResults.Chromatograms.Count; iResult++)
                {
                    var replicate = new Replicate(DataSchema, iResult);
                    var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[iResult];
                    for (int iFile = 0; iFile < chromatogramSet.MSDataFileInfos.Count; iFile++)
                    {
                        var resultFile = new ResultFile(replicate, chromatogramSet.MSDataFileInfos[iFile].FileId, 0);
                        dict.Add(new ResultKey(replicate, iFile), resultFile);
                    }
                }
            }
            return dict;
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
    }
}
