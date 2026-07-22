using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using pwiz.Common.DataAnalysis;

namespace pwiz.Skyline.Model.GroupComparison
{
    public class MedianPolisher
    {
        public bool IncludeScaleFactor { get; set; }

        /// <summary>
        /// When true, use <see cref="MedianPolish.GetConvergedMedianPolish(double?[,])"/>
        /// (R / skyline-prism stopping rule) instead of the legacy
        /// <see cref="MedianPolish.GetMedianPolish(double?[,])"/>. The original
        /// MSstats-style summarization leaves this false to keep its behavior unchanged.
        /// </summary>
        public bool IterateToConvergence { get; set; }
        public double?[] Polish(IList<IDictionary<IdentityPath, double>> values, HashSet<int> include)
        {
            List<int> includedIndexes =
                Enumerable.Range(0, values.Count).Where(i => false != include?.Contains(i)).ToList();
            var keys = includedIndexes.SelectMany(i => values[i].Keys).Distinct().ToList();
            // Build the polish matrix as (keys x replicates), so the algorithm sweeps
            // per-key (transition or peptide) medians first and per-replicate medians
            // second. Median polish is not transpose-invariant for finite iterations -
            // when many cells share the same imputed value, the sweep order chooses
            // which axis absorbs the few standout measurements. This orientation
            // matches skyline-prism's tukey_median_polish, where the matrix is shaped
            // (transitions x samples) / (peptides x samples).
            var matrix = new double?[keys.Count, includedIndexes.Count];
            var result = new double?[values.Count];
            if (keys.Count == 0)
            {
                return result;
            }

            if (keys.Count == 1)
            {
                for (int iReplicate = 0; iReplicate < values.Count; iReplicate++)
                {
                    if (values[iReplicate].TryGetValue(keys[0], out var value))
                    {
                        result[iReplicate] = value;
                    }
                }

                return result;
            }

            for (int iCol = 0; iCol < includedIndexes.Count; iCol++)
            {
                var replicateValues = values[includedIndexes[iCol]];
                for (int iRow = 0; iRow < keys.Count; iRow++)
                {
                    if (replicateValues.TryGetValue(keys[iRow], out var value))
                    {
                        matrix[iRow, iCol] = value;
                    }
                }
            }

            double scaleFactor = IncludeScaleFactor ? Math.Log(keys.Count, 2) : 0;
            var medianPolish = IterateToConvergence
                ? MedianPolish.GetConvergedMedianPolish(matrix)
                : MedianPolish.GetMedianPolish(matrix);
            for (int iCol = 0; iCol < includedIndexes.Count; iCol++)
            {
                result[includedIndexes[iCol]] = medianPolish.OverallConstant + medianPolish.ColumnEffects[iCol] + scaleFactor;
            }

            if (include != null)
            {
                for (int iReplicate = 0; iReplicate < result.Length; iReplicate++)
                {
                    if (includedIndexes.Contains(iReplicate))
                    {
                        continue;
                    }

                    var deviations = new List<double>();

                    var replicateValues = values[iReplicate];
                    for (int iRow = 0; iRow < keys.Count; iRow++)
                    {
                        if (replicateValues.TryGetValue(keys[iRow], out var value))
                        {
                            // Per-key effects (transitions/peptides) live in RowEffects
                            // now that the matrix is (keys x replicates).
                            deviations.Add(value - medianPolish.RowEffects[iRow]);
                        }
                    }

                    if (deviations.Count > 0)
                    {
                        result[iReplicate] = deviations.Median() + scaleFactor;
                    }
                }
            }

            return result;
        }
    }
}
