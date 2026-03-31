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
        public double?[] Polish(IList<IDictionary<IdentityPath, double>> values, HashSet<int> include)
        {
            List<int> includedIndexes =
                Enumerable.Range(0, values.Count).Where(i => false != include?.Contains(i)).ToList();
            var keys = includedIndexes.SelectMany(i => values[i].Keys).Distinct().ToList();
            var matrix = new double?[includedIndexes.Count, keys.Count];
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

            for (int iRow = 0; iRow < includedIndexes.Count; iRow++)
            {
                var replicateValues = values[includedIndexes[iRow]];
                for (int iKey = 0; iKey < keys.Count; iKey++)
                {
                    if (replicateValues.TryGetValue(keys[iKey], out var value))
                    {
                        matrix[iRow, iKey] = value;
                    }
                }
            }

            double scaleFactor = IncludeScaleFactor ? Math.Log(keys.Count, 2) : 0;
            var medianPolish = MedianPolish.GetMedianPolish(matrix);
            for (int iRow = 0; iRow < includedIndexes.Count; iRow++)
            {
                result[includedIndexes[iRow]] = medianPolish.OverallConstant + medianPolish.RowEffects[iRow] + scaleFactor;
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
                    for (int iKey = 0; iKey < keys.Count; iKey++)
                    {
                        if (replicateValues.TryGetValue(keys[iKey], out var value))
                        {
                            deviations.Add(value - medianPolish.ColumnEffects[iKey]);
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
