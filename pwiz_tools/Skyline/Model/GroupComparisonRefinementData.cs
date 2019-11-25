using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.DataAnalysis;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.Model
{
    class GroupComparisonRefinementData
    {

        public GroupComparisonRefinementData(IDocumentContainer docContainer, double adjustedPValCutoff,
            double foldChangCutoff, double msLevel, GroupComparisonDef groupComparisonDef)
        {
            var document = docContainer.Document;
            var groupComparer = new GroupComparer(groupComparisonDef, document, null);
            List<GroupComparisonResult> results = new List<GroupComparisonResult>();

            foreach (var protein in document.MoleculeGroups)
            {
                IEnumerable<PeptideDocNode> peptides;
                if (groupComparer.ComparisonDef.PerProtein)
                {
                    peptides = new PeptideDocNode[] {null};
                }
                else
                {
                    peptides = protein.Molecules;
                }

                foreach (var peptide in peptides)
                {
                    results.AddRange(groupComparer.CalculateFoldChanges(protein, peptide));
                }
            }

            var adjustedPValues = PValues.AdjustPValues(results.Select(
                row => row.LinearFitResult.PValue)).ToArray();

            var foldChangeList = new List<>();
            for (int iRow = 0; iRow < results.Count; iRow++)
            {
                var resultRow = results[iRow];
                FoldChangeResult foldChangeResult = new FoldChangeResult(groupComparisonDef.ConfidenceLevel,
                    adjustedPValues[iRow], resultRow.LinearFitResult, double.NaN);
                foldChangeResults.Add(foldChangeResult);
            }



//                var peptideGroups = document.MoleculeGroups.ToArray();
//                for (int i = 0; i < peptideGroups.Length; i++)
//                {
//                    var peptideGroup = peptideGroups[i];
//                    IEnumerable<PeptideDocNode> peptides;
//                    if (gcm.GroupComparer.ComparisonDef.PerProtein)
//                    {
//                        peptides = new PeptideDocNode[] { null };
//                    }
//                    else
//                    {
//                        peptides = peptideGroup.Molecules;
//                    }
//                    foreach (var peptide in peptides)
//                    {
//                        results.AddRange(gcm.GroupComparer.CalculateFoldChanges(peptideGroup, peptide));
//                    }
//                }
//            }
//
//            gcm.Results = new GroupComparisonResults(gcm.GroupComparer, results, DateTime.Now, DateTime.Now);
//
//
//            Dictionary<int, double> criticalValuesByDegreesOfFreedom = new Dictionary<int, double>();
//            var groupComparisonDef = gcm.Results.GroupComparer.ComparisonDef;
//            var adjustedPValues = PValues.AdjustPValues(gcm.Results.ResultRows.Select(
//                row => row.LinearFitResult.PValue)).ToArray();
//            for (int iRow = 0; iRow < gcm.Results.ResultRows.Count; iRow++)
//            {
//                var resultRow = gcm.Results.ResultRows[iRow];
//                if (null != resultRow.Selector.Peptide)
//                {
//                    peptide = new Model.Databinding.Entities.Peptide(_skylineDataSchema,
//                        new IdentityPath(protein.IdentityPath, resultRow.Selector.Peptide.Id));
//                }
//                double criticalValue;
//                if (!criticalValuesByDegreesOfFreedom.TryGetValue(resultRow.LinearFitResult.DegreesOfFreedom,
//                    out criticalValue))
//                {
//                    criticalValue = FoldChangeResult.GetCriticalValue(groupComparisonDef.ConfidenceLevel,
//                        resultRow.LinearFitResult.DegreesOfFreedom);
//                    criticalValuesByDegreesOfFreedom.Add(resultRow.LinearFitResult.DegreesOfFreedom, criticalValue);
//                }
//                FoldChangeResult foldChangeResult = new FoldChangeResult(groupComparisonDef.ConfidenceLevel,
//                    adjustedPValues[iRow], resultRow.LinearFitResult, criticalValue);
//                rows.Add(new FoldChangeBindingSource.FoldChangeRow(protein, peptide, resultRow.Selector.LabelType,
//                    resultRow.Selector.MsLevel, resultRow.Selector.GroupIdentifier, resultRow.ReplicateCount, foldChangeResult));
//            }
        }
        private class GroupComparisonRow
        {
            public GroupComparisonRow(PeptideGroupDocNode protein, PeptideDocNode peptide, int msLevel, FoldChangeResult result)
            {

            }
        }
    }

}
