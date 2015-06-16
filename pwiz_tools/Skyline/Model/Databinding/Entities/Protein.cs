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

using System.Collections.Generic;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [AnnotationTarget(AnnotationDef.AnnotationTarget.protein)]
    public class Protein : SkylineDocNode<PeptideGroupDocNode>
    {
        public Protein(SkylineDataSchema dataSchema, IdentityPath identityPath) : base(dataSchema, identityPath)
        {
        }

        private Peptides _peptides;
        [OneToMany(ForeignKey = "Protein")]
        [HideWhen(AncestorOfType = typeof(FoldChangeBindingSource.FoldChangeRow))]
        public Peptides Peptides
        {
            get { return _peptides = _peptides ?? new Peptides(this); }
        }

        [OneToMany(ItemDisplayName = "ResultFile")]
        [HideWhen(AncestorsOfAnyOfTheseTypes = new []{typeof(SkylineDocument), typeof(FoldChangeBindingSource.FoldChangeRow)})]
        public IDictionary<ResultKey, ResultFile> Results
        {
            get
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
        }

        protected override PeptideGroupDocNode CreateEmptyNode()
        {
            return new PeptideGroupDocNode(new PeptideGroup(), null, null, new PeptideDocNode[0]);
        }

        [InvariantDisplayName("ProteinName")]
        public string Name
        {
            get { return DocNode.Name; }
            set { ChangeDocNode(EditDescription.SetColumn("ProteinName", value), // Not L10N
                DocNode.ChangeName(value)); } // the user can overide this label
        }
        [InvariantDisplayName("ProteinDescription")]
        public string Description
        {
            get { return DocNode.Description; }
            set { ChangeDocNode(EditDescription.SetColumn("ProteinDescription", value), // Not L10N
                DocNode.ChangeDescription(value)); } // the user can ovveride this label
        }
        [InvariantDisplayName("ProteinAccession")]
        public string Accession
        {
            get { return DocNode.ProteinMetadata.Accession; }
        }

        [InvariantDisplayName("ProteinPreferredName")]
        public string PreferredName
        {
            get { return DocNode.ProteinMetadata.PreferredName; }
        }

        [InvariantDisplayName("ProteinGene")]
        public string Gene
        {
            get { return DocNode.ProteinMetadata.Gene; }
        }

        [InvariantDisplayName("ProteinSpecies")]
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
        public string Sequence 
        {
            get { return DocNode.PeptideGroup.Sequence; }
        }
        [InvariantDisplayName("ProteinNote")]
        public string Note
        {
            get { return DocNode.Note; }
            set { ChangeDocNode(EditDescription.SetColumn("ProteinNote", value), // Not L10N
                DocNode.ChangeAnnotations(DocNode.Annotations.ChangeNote(value)));}
        }
        public override string ToString()
        {
            return Name; // TODO nicksh this needs to agree with Targets view in Document Grid (By Name, By Accession etc)
        }
    }
}
