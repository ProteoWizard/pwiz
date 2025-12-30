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
    /// Database record for molecule groups (proteins/peptide lists).
    /// Each property with [Column] attribute maps to a database column.
    /// </summary>
    internal class MoleculeGroupRecord : Record
    {
        private readonly PeptideGroupDocNode _node;

        public MoleculeGroupRecord(PeptideGroupDocNode node, long id) : base(id)
        {
            _node = node;
        }

        [Column(IsRequired = true)]
        public string MoleculeGroupType => _node.IsProtein ? "protein" : "peptide_list";

        [Column(IsRequired = true)]
        public string Name => _node.Name;

        [Column]
        public string Description => _node.Description;

        [Column]
        public string LabelName => _node.ProteinMetadata.Name;

        [Column]
        public string LabelDescription => _node.ProteinMetadata.Description;

        [Column]
        public string Sequence => _node.PeptideGroup.Sequence;

        [Column]
        public string Accession => _node.ProteinMetadata.Accession;

        [Column]
        public string PreferredName => _node.ProteinMetadata.PreferredName;

        [Column]
        public string Gene => _node.ProteinMetadata.Gene;

        [Column]
        public string Species => _node.ProteinMetadata.Species;

        [Column]
        public string WebsearchStatus => _node.ProteinMetadata.WebSearchInfo?.ToString();

        [Column]
        public bool? IsDecoy => _node.IsDecoy ? true : (bool?)null;

        [Column]
        public double? DecoyMatchProportion => _node.ProportionDecoysMatch;

        [Column]
        public bool? AutoManageChildren => _node.AutoManageChildren ? true : (bool?)null;

        [Column]
        public string Note => _node.Note;
    }
}
