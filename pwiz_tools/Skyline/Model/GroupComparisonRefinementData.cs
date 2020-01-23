using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.DataAnalysis;
using pwiz.Common.DataAnalysis.Matrices;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.Model
{
    class GroupComparisonRefinementData
    {
        private List<List<GroupComparisonRow>> Data { get; set; }

        private readonly double _adjustedPValCutoff;
        private readonly double _foldChangeCutoff;
        private readonly int _msLevel;
        private readonly QrFactorizationCache _qrFactorizationCache = new QrFactorizationCache();
        private SrmSettingsChangeMonitor _progressMonitor;

        public GroupComparisonRefinementData(SrmDocument document, double adjustedPValCutoff,
            double foldChangeCutoff, int? msLevel, List<GroupComparisonDef> groupComparisonDefs,
            SrmSettingsChangeMonitor progressMonitor)
        {
            _progressMonitor = progressMonitor;
            _foldChangeCutoff = foldChangeCutoff;
            _adjustedPValCutoff = adjustedPValCutoff;
            if (msLevel.HasValue)
            {
                _msLevel = msLevel.Value;
            }
            else
            {
                _msLevel = document.PeptideTransitions.Any(t => !t.IsMs1) ? 2 : 1;
            }
            Data = new List<List<GroupComparisonRow>>();

            foreach (var groupComparisonDef in groupComparisonDefs)
            {
                var groupComparer = new GroupComparer(groupComparisonDef, document, _qrFactorizationCache);
                var results = GroupComparisonModel.ComputeResults(groupComparer, document, null, null, progressMonitor);


                var adjustedPValues = PValues.AdjustPValues(results.Select(
                    row => row.LinearFitResult.PValue)).ToArray();

                var foldChangeList = new List<GroupComparisonRow>();
                for (int iRow = 0; iRow < results.Count; iRow++)
                {
                    var resultRow = results[iRow];
                    FoldChangeResult foldChangeResult = new FoldChangeResult(groupComparisonDef.ConfidenceLevel,
                        adjustedPValues[iRow], resultRow.LinearFitResult, double.NaN);
                    foldChangeList.Add(new GroupComparisonRow(resultRow.Selector.Protein, resultRow.Selector.Peptide,
                        resultRow.Selector.MsLevel,
                        foldChangeResult));
                }

                Data.Add(foldChangeList);
            }
        }

        public SrmDocument RemoveBelowCutoffs(SrmDocument document)
        {
            var intersection = new HashSet<int>();
            foreach (var data in Data)
            {
                if (_progressMonitor != null && _progressMonitor.IsCanceled())
                {
                    throw new OperationCanceledException();
                }

                if (_progressMonitor != null)
                    _progressMonitor.ProcessMolecule(null);

                var filterByCutoff = data.ToArray();

                var toRemove = IndicesBelowCutoff(_adjustedPValCutoff, _foldChangeCutoff, _msLevel, null,
                    filterByCutoff);

                if (intersection.Count == 0)
                    intersection.UnionWith(toRemove);
                else
                    intersection.IntersectWith(toRemove);
            }

            return (SrmDocument) document.RemoveAll(intersection);
        }

        public static List<int> IndicesBelowCutoff(double adjustedPValueCutoff, double foldChangeCutoff, 
            double msLevel, FoldChangeBindingSource.FoldChangeRow[] foldChangeRows = null, GroupComparisonRow[] groupComparisonRows = null)
        {
            GroupComparisonRow[] rows;
            if (foldChangeRows != null)
            {
                var rowList = new List<GroupComparisonRow>();
                foreach (var row in foldChangeRows)
                {
                    rowList.Add(new GroupComparisonRow(row.Protein.DocNode, row.Peptide.DocNode, row.MsLevel, row.FoldChangeResult));
                }

                rows = rowList.ToArray();
            }
            else
            {
                if (groupComparisonRows != null)
                {
                    rows = groupComparisonRows;
                }
                else
                {
                    return new List<int>();
                }
            }

            // Remove based on p value and fold change cutoffs
            // Retain all standard types
            var toRemove = rows.Where(r =>
                (r.Peptide == null || r.Peptide != null && r.Peptide.GlobalStandardType == null) &&
                (!double.IsNaN(msLevel) && r.MsLevel == msLevel || double.IsNaN(msLevel)) &&
                (!double.IsNaN(adjustedPValueCutoff) &&
                 r.FoldChangeResult.AdjustedPValue >= adjustedPValueCutoff ||
                 !double.IsNaN(foldChangeCutoff) &&
                 r.FoldChangeResult.AbsLog2FoldChange <= foldChangeCutoff)).Select(r =>
                r.Peptide != null ? r.Peptide.Id.GlobalIndex : r.Protein.Id.GlobalIndex);

            return toRemove.Distinct().ToList();
        }
    }
    public class GroupComparisonRow
    {
        public GroupComparisonRow(PeptideGroupDocNode protein, PeptideDocNode peptide, int? msLevel, FoldChangeResult result)
        {
            Protein = protein;
            Peptide = peptide;
            MsLevel = msLevel;
            FoldChangeResult = result;
        }

        public PeptideGroupDocNode Protein { get; private set; }
        public PeptideDocNode Peptide { get; private set; }
        public int? MsLevel { get; private set; }
        public FoldChangeResult FoldChangeResult { get; private set; }
    }
}
