﻿using System;
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

        public GroupComparisonRefinementData(SrmDocument document, double adjustedPValCutoff,
            double foldChangeCutoff, int msLevel, List<GroupComparisonDef> groupComparisonDefs)
        {
            _document = document;
            _foldChangeCutoff = foldChangeCutoff;
            _adjustedPValCutoff = adjustedPValCutoff;
            _msLevel = msLevel;
            Data = new List<List<GroupComparisonRow>>();

            foreach (var groupComparisonDef in groupComparisonDefs)
            {
                var groupComparer = new GroupComparer(groupComparisonDef, _document, _qrFactorizationCache);
                List<GroupComparisonResult> results = new List<GroupComparisonResult>();

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
                var filterByCutoff = data.ToArray();

                var toRemove = filterByCutoff.Where(r =>
                        (!double.IsNaN(_adjustedPValCutoff) &&
                         r.FoldChangeResult.AdjustedPValue >= _adjustedPValCutoff) ||
                        (!double.IsNaN(_foldChangeCutoff) &&
                         r.FoldChangeResult.AbsLog2FoldChange <= _foldChangeCutoff) || r.MsLevel != _msLevel)
                    .Select(r => r.Peptide != null ? r.Peptide.Id.GlobalIndex : r.Protein.Id.GlobalIndex)
                    .Distinct()
                    .ToArray();

                var result = document.RemoveAll(toRemove).Children.Select(n => n.Id.GlobalIndex).Distinct();
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
