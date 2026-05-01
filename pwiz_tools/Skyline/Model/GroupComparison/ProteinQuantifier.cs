/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.GroupComparison
{
    /// <summary>
    /// Quantifies a protein from an arbitrary collection of <see cref="PeptideQuantifier"/>
    /// instances. The peptides do not need to be children of any particular
    /// <see cref="PeptideGroupDocNode"/>.
    /// </summary>
    public class ProteinQuantifier
    {
        private readonly ImmutableList<PeptideQuantifier> _peptideQuantifiers;

        public ProteinQuantifier(SrmSettings srmSettings, IEnumerable<PeptideQuantifier> peptideQuantifiers)
        {
            SrmSettings = srmSettings;
            _peptideQuantifiers = ImmutableList.ValueOf(peptideQuantifiers);
        }

        public SrmSettings SrmSettings { get; }

        public IList<PeptideQuantifier> PeptideQuantifiers
        {
            get { return _peptideQuantifiers; }
        }

        /// <summary>
        /// Returns a map of replicate index to the protein abundance for that replicate.
        /// </summary>
        public IDictionary<int, Protein.AbundanceValue> CalculateProteinAbundances()
        {
            var summarizationMethod = SrmSettings.PeptideSettings.Quantification.SummarizationMethod;
            if (Equals(summarizationMethod, SummarizationMethod.MEDIANPOLISH))
            {
                return CalculateProteinAbundancesWithMedianPolish();
            }

            return CalculateProteinAbundancesWithAveraging();
        }

        private IDictionary<int, Protein.AbundanceValue> CalculateProteinAbundancesWithAveraging()
        {
            int replicateCount = SrmSettings.HasResults
                ? SrmSettings.MeasuredResults.Chromatograms.Count : 0;
            var replicateQuantities = new List<Dictionary<IdentityPath, PeptideQuantifier.Quantity>>();
            for (int iReplicate = 0; iReplicate < replicateCount; iReplicate++)
            {
                var quantities = new Dictionary<IdentityPath, PeptideQuantifier.Quantity>();
                foreach (var peptideQuantifier in _peptideQuantifiers)
                {
                    foreach (var entry in peptideQuantifier.GetTransitionIntensities(SrmSettings, iReplicate, false))
                    {
                        quantities.Add(entry.Key, entry.Value);
                    }
                }
                replicateQuantities.Add(quantities);
            }

            var allTransitionIdentityPaths = replicateQuantities
                .SelectMany(dict => dict.Where(kvp => !kvp.Value.Truncated).Select(kvp => kvp.Key)).ToHashSet();
            if (allTransitionIdentityPaths.Count == 0)
            {
                allTransitionIdentityPaths = replicateQuantities.SelectMany(dict => dict.Keys).ToHashSet();
            }

            var proteinAbundanceRecords = new Dictionary<int, Protein.AbundanceValue>();
            for (int iReplicate = 0; iReplicate < replicateCount; iReplicate++)
            {
                var rawAbundance = PeptideQuantifier.SumTransitionQuantities(allTransitionIdentityPaths,
                    replicateQuantities[iReplicate], SrmSettings.PeptideSettings.Quantification);
                if (rawAbundance != null)
                {
                    int quantityCount = replicateQuantities[iReplicate].Keys.Intersect(allTransitionIdentityPaths)
                        .Count();
                    proteinAbundanceRecords[iReplicate] = new Protein.AbundanceValue(rawAbundance.Raw,
                        rawAbundance.Raw * quantityCount, rawAbundance.Message);
                }
            }
            return proteinAbundanceRecords;
        }

        private IDictionary<int, Protein.AbundanceValue> CalculateProteinAbundancesWithMedianPolish()
        {
            int replicateCount = SrmSettings.MeasuredResults?.Chromatograms?.Count ?? 0;
            var replicateIndexes = PeptideQuantifier.GetMedianPolishReplicates(SrmSettings);
            if (replicateCount == 0)
            {
                return new Dictionary<int, Protein.AbundanceValue>();
            }

            var replicatePeptideAbundances =
                Enumerable.Range(0, replicateCount).Select(i => new Dictionary<IdentityPath, double>()).ToArray();
            // Stage 1: For each peptide, collect transition intensities across all replicates,
            // then median polish transitions -> peptide abundance per replicate
            foreach (var peptideQuantifier in _peptideQuantifiers)
            {
                var quantities = peptideQuantifier.GetMedianPolishQuantities(SrmSettings, replicateIndexes);
                if (quantities != null)
                {
                    var peptideIdentityPath = new IdentityPath(peptideQuantifier.PeptideGroup,
                        peptideQuantifier.PeptideDocNode.Peptide);
                    for (int iReplicate = 0; iReplicate < replicateCount; iReplicate++)
                    {
                        if (quantities[iReplicate].HasValue)
                        {
                            replicatePeptideAbundances[iReplicate]
                                .Add(peptideIdentityPath, quantities[iReplicate].Value);
                        }
                    }
                }
            }

            var polishedProteinAbundances = new MedianPolisher().Polish(replicatePeptideAbundances, replicateIndexes);

            // Convert to AbundanceValue records
            var proteinAbundanceRecords = new Dictionary<int, Protein.AbundanceValue>();
            for (int iReplicate = 0; iReplicate < replicateCount; iReplicate++)
            {
                double abundance = polishedProteinAbundances[iReplicate] ?? double.NaN;
                if (!double.IsNaN(abundance) && !double.IsInfinity(abundance))
                {
                    // Convert from log2 to linear scale
                    double linearAbundance = Math.Pow(2, abundance);
                    proteinAbundanceRecords[iReplicate] = new Protein.AbundanceValue(linearAbundance, linearAbundance, null);
                }
            }
            return proteinAbundanceRecords;
        }
    }
}
