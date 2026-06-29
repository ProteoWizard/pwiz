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
            var peptideMethod = SrmSettings.PeptideSettings.Quantification.PeptideSummarizationMethod
                                ?? SummarizationMethod.DEFAULT;
            var proteinMethod = SrmSettings.PeptideSettings.Quantification.ProteinSummarizationMethod
                                ?? SummarizationMethod.DEFAULT;
            bool peptideMed = Equals(peptideMethod, SummarizationMethod.MEDIANPOLISH);
            bool proteinMed = Equals(proteinMethod, SummarizationMethod.MEDIANPOLISH);

            // AVG/AVG retains the canonical "sum all transitions in one shot" path so that
            // truncated/missing-transition error messages from SumTransitionQuantities are
            // preserved in the common case.
            if (!peptideMed && !proteinMed)
            {
                return CalculateProteinAbundancesWithAveraging();
            }
            if (peptideMed && proteinMed)
            {
                return CalculateProteinAbundancesWithMedianPolish();
            }
            return CalculateProteinAbundancesTwoStage(peptideMethod, proteinMethod);
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

        private IDictionary<int, Protein.AbundanceValue> CalculateProteinAbundancesTwoStage(
            SummarizationMethod peptideMethod, SummarizationMethod proteinMethod)
        {
            int replicateCount = SrmSettings.MeasuredResults?.Chromatograms?.Count ?? 0;
            if (replicateCount == 0)
            {
                return new Dictionary<int, Protein.AbundanceValue>();
            }
            var replicateIndexes = PeptideQuantifier.GetMedianPolishReplicates(SrmSettings);

            // Stage 1: per-peptide, per-replicate log2 abundance using the chosen peptide method.
            var replicatePeptideAbundances =
                Enumerable.Range(0, replicateCount).Select(_ => new Dictionary<IdentityPath, double>()).ToArray();
            foreach (var peptideQuantifier in _peptideQuantifiers)
            {
                var log2Quantities = peptideQuantifier.GetPeptideLog2Abundances(SrmSettings, replicateIndexes,
                    peptideMethod);
                if (log2Quantities == null)
                {
                    continue;
                }
                var peptideIdentityPath = new IdentityPath(peptideQuantifier.PeptideGroup,
                    peptideQuantifier.PeptideDocNode.Peptide);
                for (int i = 0; i < replicateCount && i < log2Quantities.Length; i++)
                {
                    if (log2Quantities[i].HasValue)
                    {
                        replicatePeptideAbundances[i].Add(peptideIdentityPath, log2Quantities[i].Value);
                    }
                }
            }

            var result = new Dictionary<int, Protein.AbundanceValue>();
            if (Equals(proteinMethod, SummarizationMethod.MEDIANPOLISH))
            {
                var polished = new MedianPolisher() { IterateToConvergence = true }
                    .Polish(replicatePeptideAbundances, replicateIndexes);
                for (int i = 0; i < replicateCount; i++)
                {
                    double? abundance = polished[i];
                    if (abundance.HasValue && !double.IsNaN(abundance.Value) && !double.IsInfinity(abundance.Value))
                    {
                        double linear = Math.Pow(2, abundance.Value);
                        result[i] = new Protein.AbundanceValue(linear, linear, null);
                    }
                }
            }
            else
            {
                // AVERAGING at protein level: convert log2 peptide abundances to linear and sum.
                for (int i = 0; i < replicateCount; i++)
                {
                    double sum = 0;
                    int count = 0;
                    foreach (var log2Abundance in replicatePeptideAbundances[i].Values)
                    {
                        sum += Math.Pow(2, log2Abundance);
                        count++;
                    }
                    if (count > 0)
                    {
                        result[i] = new Protein.AbundanceValue(sum, sum * count, null);
                    }
                }
            }
            return result;
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

            var polishedProteinAbundances = new MedianPolisher() { IterateToConvergence = true }
                .Polish(replicatePeptideAbundances, replicateIndexes);

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
