using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NHibernate.Mapping;
using pwiz.Common.DataAnalysis;
using pwiz.Common.DataAnalysis.Matrices;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.Model
{
    class GroupComparisonRefinementData
    {
        private List<List<GroupComparisonRow>> Data { get; set; }
        private SrmDocument _document;

        private readonly double _adjustedPValCutoff;
        private readonly double _foldChangeCutoff;
        private readonly int _msLevel;
        private readonly QrFactorizationCache _qrFactorizationCache = new QrFactorizationCache();
        private SrmSettingsChangeMonitor _progressMonitor;

        public GroupComparisonRefinementData(SrmDocument document, double adjustedPValCutoff,
            double foldChangeCutoff, int msLevel, List<GroupComparisonDef> groupComparisonDefs, SrmSettingsChangeMonitor progressMonitor)
        {
            _document = document;
            _progressMonitor = progressMonitor;
            _foldChangeCutoff = foldChangeCutoff;
            _adjustedPValCutoff = adjustedPValCutoff;
            _msLevel = msLevel;
            Data = new List<List<GroupComparisonRow>>();

            foreach (var groupComparisonDef in groupComparisonDefs)
            {
                var groupComparer = new GroupComparer(groupComparisonDef, _document, _qrFactorizationCache);
                var results = new List<GroupComparisonResult>();

                foreach (var protein in _document.MoleculeGroups)
                {
                    IEnumerable<PeptideDocNode> peptides;
                    if (groupComparer.ComparisonDef.PerProtein)
                    {
                        peptides = new PeptideDocNode[] { null };
                    }
                    else
                    {
                        peptides = protein.Molecules;
                    }

                    foreach (var peptide in peptides)
                    {
                        if (_progressMonitor != null && _progressMonitor.IsCanceled())
                        {
                            throw new OperationCanceledException();
                        }

                        if (progressMonitor != null)
                        {
                            progressMonitor.ProcessMolecule(peptide);
                        }

                        results.AddRange(groupComparer.CalculateFoldChanges(protein, peptide));
                    }
                }

                var adjustedPValues = PValues.AdjustPValues(results.Select(
                    row => row.LinearFitResult.PValue)).ToArray();

                var foldChangeList = new List<GroupComparisonRow>();
                for (int iRow = 0; iRow < results.Count; iRow++)
                {
                    var resultRow = results[iRow];
                    FoldChangeResult foldChangeResult = new FoldChangeResult(groupComparisonDef.ConfidenceLevel,
                        adjustedPValues[iRow], resultRow.LinearFitResult, double.NaN);
                    foldChangeList.Add(new GroupComparisonRow(resultRow.Selector.Protein, resultRow.Selector.Peptide, resultRow.Selector.MsLevel,
                        foldChangeResult, resultRow.Selector.GroupIdentifier));
                }
                Data.Add(foldChangeList);
            }
        }


        public SrmDocument RemoveBelowCutoffs(SrmDocument document)
        {
            var union = new List<int>();
            foreach (var data in Data)
            {
                if (_progressMonitor != null && _progressMonitor.IsCanceled())
                {
                    throw new OperationCanceledException();
                }

                if (_progressMonitor != null)
                    _progressMonitor.ProcessMolecule(null);

                var filterByCutoff = data.ToArray();

                // Remove based on p value and fold change cutoffs
                // Retain all standard types
                var toRemove = filterByCutoff.Where(r =>
                        r.Peptide == null || r.Peptide != null && r.Peptide.GlobalStandardType == null &&
                        r.MsLevel == _msLevel &&
                        (!double.IsNaN(_adjustedPValCutoff) &&
                         r.FoldChangeResult.AdjustedPValue >= _adjustedPValCutoff) ||
                        (!double.IsNaN(_foldChangeCutoff) &&
                         r.FoldChangeResult.AbsLog2FoldChange <= _foldChangeCutoff))
                    .Select(r => r.Peptide != null ? r.Peptide.Id.GlobalIndex : r.Protein.Id.GlobalIndex)
                    .Distinct()
                    .ToArray();

                var result = ((SrmDocument) document.RemoveAll(toRemove)).Peptides.Select(p => p.Id.GlobalIndex);
                union.AddRange(result);
            }

            return (SrmDocument) document.RemoveAllBut(union);
        }
        
        private class GroupComparisonRow
        {
            public GroupComparisonRow(PeptideGroupDocNode protein, PeptideDocNode peptide, int? msLevel, FoldChangeResult result, GroupIdentifier group)
            {
                Protein = protein;
                Peptide = peptide;
                MsLevel = msLevel;
                Group = group;
                FoldChangeResult = result;
            }

            public PeptideGroupDocNode Protein { get; private set; }
            public PeptideDocNode Peptide { get; private set; }
            public int? MsLevel { get; private set; }
            public GroupIdentifier Group { get; private set; }
            public FoldChangeResult FoldChangeResult { get; private set; }
        }
    }

}
