/*
 * Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
 *
 * Copyright 2022
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
using pwiz.Common.Progress;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Finds all peptides that occur more than once in the document.
    /// </summary>
    public class DuplicatedPeptideFinder : AbstractDocNodeFinder
    {
        private SrmDocument _lastSearchedDocument;
        private HashSet<PeptideSequenceModKey> _allPeptideKeys;
        private HashSet<PeptideSequenceModKey> _duplicatePeptideKeys;

        public override string Name
        {
            get
            {
                return @"duplicated_peptides";
            }
        }
        public override string DisplayName
        {
            get { return Resources.DuplicatedPeptideFinder_DisplayName_Duplicated_peptides; }
        }

        private void InitializeIndex(SrmDocument document)
        {
            if (ReferenceEquals(_lastSearchedDocument, document))
                return;
            _lastSearchedDocument = document;
            _allPeptideKeys = new HashSet<PeptideSequenceModKey>();
            _duplicatePeptideKeys = new HashSet<PeptideSequenceModKey>();
            foreach (var peptide in document.Peptides)
                if (!(_allPeptideKeys).Add(peptide.SequenceKey))
                    _duplicatePeptideKeys.Add(peptide.SequenceKey);
        }

        public override FindMatch Match(BookmarkEnumerator bookmarkEnumerator)
        {
            InitializeIndex(bookmarkEnumerator.Document);
            var peptide = bookmarkEnumerator.CurrentDocNode as PeptideDocNode;
            if (peptide == null)
                return base.Match(bookmarkEnumerator);
            return _duplicatePeptideKeys.Contains(peptide.SequenceKey)
                ? new FindMatch(DisplayName)
                : null;
        }

        public override IEnumerable<Bookmark> FindAll(SrmDocument document, IProgress progress)
        {
            InitializeIndex(document);
            var peptideCount = document.PeptideCount;
            int iPeptide = 0;
            foreach (var group in document.PeptideGroups)
            {
                foreach (var peptide in group.Peptides)
                {
                    progress.CancellationToken.ThrowIfCancellationRequested();
                    iPeptide++;
                    progress.Value = iPeptide * 100.0 / peptideCount;
                    if (_duplicatePeptideKeys.Contains(peptide.SequenceKey))
                        yield return new Bookmark(new IdentityPath(group.Id, peptide.Id));
                }
            }
        }
    }
}
