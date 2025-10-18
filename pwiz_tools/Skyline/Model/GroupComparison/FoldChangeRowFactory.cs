using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis;
using pwiz.Common.DataAnalysis.Matrices;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace pwiz.Skyline.Model.GroupComparison
{
    public class FoldChangeRowFactory
    {
        public FoldChangeRowFactory(SkylineDataSchema dataSchema) : this(dataSchema.QueryLock.CancellationToken, dataSchema)
        {
            
        }
        public FoldChangeRowFactory(CancellationToken cancellationToken, SkylineDataSchema dataSchema)
        {
            CancellationToken = cancellationToken;
            DataSchema = dataSchema;
        }
        
        public SkylineDataSchema DataSchema { get; }
        
        public CancellationToken CancellationToken
        {
            get;
        }

        public IEnumerable<FoldChangeRow> GetFoldChangeRows(GroupComparisonResults groupComparisonResults)
        {
            if (groupComparisonResults == null)
            {
                return Array.Empty<FoldChangeRow>();
            }
            return GetFoldChangeRows(groupComparisonResults.GroupComparer.ComparisonDef,
                groupComparisonResults.ResultRows);
        }

        public IEnumerable<FoldChangeRow> GetAllFoldChangeRows()
        {
            return DataSchema.Document.Settings.DataSettings.GroupComparisonDefs.SelectMany(def =>
                GetFoldChangeRows(def, GetResults(def).ToList()));
        }

        public IEnumerable<FoldChangeDetailRow> GetAllFoldChangeDetailRows()
        {
            return GetFoldChangeDetailRows(GetAllFoldChangeRows());
        }
        
        public IEnumerable<FoldChangeDetailRow> GetFoldChangeDetailRows(IEnumerable<FoldChangeRow> foldChangeRows)
        {
            foreach (var grouping in foldChangeRows.GroupBy(row =>
                         Tuple.Create(row.Protein, row.Peptide, row.IsotopeLabelType, row.MsLevel)))
            {
                var foldChangeResults = grouping.ToDictionary(row => row.Group, row => row.FoldChangeResult);
                var runAbundances = new Dictionary<Replicate, ReplicateRow>();
                foreach (var abundance in grouping.SelectMany(row => row.ReplicateAbundances))
                {
                    runAbundances[abundance.Key] = abundance.Value;
                }

                yield return new FoldChangeDetailRow(grouping.Key.Item1, grouping.Key.Item2, grouping.Key.Item3,
                    grouping.Key.Item4, foldChangeResults, runAbundances);
            }
        }

        public IEnumerable<GroupComparisonResult> GetResults(GroupComparisonDef groupComparisonDef)
        {
            return GetGroupComparisonResults(new GroupComparer(groupComparisonDef, DataSchema.Document,
                new QrFactorizationCache()));
        }

        private IEnumerable<GroupComparisonResult> GetGroupComparisonResults(GroupComparer groupComparer)
        {
            var groupComparisonDef = groupComparer.ComparisonDef;
            
            foreach (var peptideGroupDocNode in DataSchema.Document.MoleculeGroups)
            {
                foreach (var peptideDocNode in groupComparisonDef.PerProtein
                             ? new PeptideDocNode[] { null }
                             : peptideGroupDocNode.Molecules)
                {
                    CancellationToken.ThrowIfCancellationRequested();
                    foreach (var result in groupComparer.CalculateFoldChanges(peptideGroupDocNode, peptideDocNode))
                    {
                        yield return result;
                    }
                }
            }
        }

        public IEnumerable<FoldChangeRow> GetFoldChangeRows(GroupComparisonDef groupComparisonDef,
            IList<GroupComparisonResult> results)
        {
            var controlGroupIdentifier = groupComparisonDef.GetControlGroupIdentifier(DataSchema.Document
                .Settings);
            Dictionary<int, double> criticalValuesByDegreesOfFreedom = new Dictionary<int, double>();
            var adjustedPValues = PValues.AdjustPValues(results.Select(row => row.LinearFitResult.PValue)).ToList();
            var resultTuples = new List<Tuple<int, GroupComparisonResult, FoldChangeResult>>();
            for (int iRow = 0; iRow < results.Count; iRow++)
            {
                var resultRow = results[iRow];
                double criticalValue;
                if (!criticalValuesByDegreesOfFreedom.TryGetValue(resultRow.LinearFitResult.DegreesOfFreedom,
                        out criticalValue))
                {
                    criticalValue = FoldChangeResult.GetCriticalValue(groupComparisonDef.ConfidenceLevel,
                        resultRow.LinearFitResult.DegreesOfFreedom);
                    criticalValuesByDegreesOfFreedom.Add(resultRow.LinearFitResult.DegreesOfFreedom, criticalValue);
                }

                FoldChangeResult foldChangeResult = new FoldChangeResult(groupComparisonDef.ConfidenceLevel,
                    adjustedPValues[iRow], resultRow.LinearFitResult, criticalValue);
                resultTuples.Add(Tuple.Create(iRow, resultRow, foldChangeResult));
            }

            var foldChangeRows = new FoldChangeRow[results.Count];
            foreach (var proteinResults in resultTuples.GroupBy(tuple =>
                         ReferenceValue.Of(tuple.Item2.Selector.Protein.PeptideGroup)))
            {
                var protein = new Protein(DataSchema, new IdentityPath(proteinResults.Key));
                foreach (var peptideResults in proteinResults.GroupBy(tuple =>
                             ReferenceValue.Of(tuple.Item2.Selector.Peptide?.Peptide)))
                {
                    Databinding.Entities.Peptide peptide = null;
                    if (peptideResults.Key.Value != null)
                    {
                        peptide = new Databinding.Entities.Peptide(DataSchema,
                            new IdentityPath(proteinResults.Key.Value, peptideResults.Key.Value));
                    }

                    foreach (var resultRow in peptideResults)
                    {
                        var selector = resultRow.Item2.Selector;
                        var replicateRows = new Dictionary<Replicate, ReplicateRow>();
                        foreach (var runAbundance in resultRow.Item2.RunAbundances)
                        {
                            var replicate = DataSchema.ReplicateList.Values[runAbundance.ReplicateIndex];
                            var groupIdentifier =
                                runAbundance.Control ? controlGroupIdentifier : selector.GroupIdentifier;
                            var replicateRow = new ReplicateRow(replicate, groupIdentifier, runAbundance.BioReplicate,
                                Math.Pow(2, runAbundance.Log2Abundance));
                            replicateRows.Add(replicate, replicateRow);
                        }

                        var foldChangeRow = new FoldChangeRow(protein, peptide, selector.LabelType, selector.MsLevel,
                            selector.GroupIdentifier, resultRow.Item2.ReplicateCount, resultRow.Item3, replicateRows);
                        foldChangeRows[resultRow.Item1] = foldChangeRow;
                    }
                }
            }

            return foldChangeRows;
        }

        public void Register(RowFactories rowFactories)
        {
            rowFactories.RegisterFactory(FoldChangeRow.ROW_SOURCE_NAME, GetAllFoldChangeRows);
            rowFactories.RegisterFactory(FoldChangeDetailRow.ROW_SOURCE_NAME, GetAllFoldChangeDetailRows);
        }
    }
}
