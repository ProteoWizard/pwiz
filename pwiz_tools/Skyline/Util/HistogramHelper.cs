using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Util
{
    static class HistogramHelper
    {
        public const int MAX_FINDRESULTS_PEPTIDES = 100;

        public static PeptidesAndTransitionGroups GetSelectedPeptides(GraphSummary graphSummary)
        {
            return PeptidesAndTransitionGroups.Get(graphSummary.StateProvider.SelectedNodes, graphSummary.ResultsIndex, int.MaxValue);
        }

        public static void CreateAndShowFindResults(ZedGraphControl sender, GraphSummary graphSummary, SrmDocument document, CVData data)
        {
            var peptideAnnotationPairs = data.PeptideAnnotationPairs.ToList();
            var results = new List<FindResult>(peptideAnnotationPairs.Count);

            var pred = new FindPredicate(new FindOptions().ChangeCustomFinders(new[] { new PeptideAnnotationPairFinder(peptideAnnotationPairs, data.CV) }), Program.MainWindow.SequenceTree.GetDisplaySettings(null));
            for (var i = 0; i < Math.Min(peptideAnnotationPairs.Count, MAX_FINDRESULTS_PEPTIDES); i++)
            {
                var pair = peptideAnnotationPairs[i];
                var displayText = PeptideAnnotationPairFinder.GetDisplayText(data.CV, pair.Annotation);

                results.Add(new FindResult(pred,
                    new BookmarkEnumerator(document,
                        new Bookmark(document.GetPathTo((int)SrmDocument.Level.Molecules,
                            document.Molecules.ToList().IndexOf(pair.Peptide)))), new FindMatch(displayText)));
            }

            var count = peptideAnnotationPairs.Count;
            if (results.Count != count)
            {
                MessageDlg.Show(sender, string.Format(Resources.HistogramHelper_CreateAndShowFindResults_Only_showing__0___1__peptides, MAX_FINDRESULTS_PEPTIDES, count));
                results = results.GetRange(0, MAX_FINDRESULTS_PEPTIDES);
            }

            if (peptideAnnotationPairs.Count == 1)
            {
                var nodes = peptideAnnotationPairs[0];
                graphSummary.StateProvider.SelectedPath = new IdentityPath(nodes.PeptideGroup.PeptideGroup, nodes.Peptide.Peptide);
            }

            Program.MainWindow.ShowFindResults(results);
        }

        public static string FormatDouble(double d, int decimals)
        {
            return d.ToString(@"F0" + decimals, LocalizationHelper.CurrentCulture);
        }
    }
}
