using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.PeakFinding;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Results
{
    public class PeakGroupIntegrator
    {
        public ImmutableList<DetailedPeakFeatureCalculator> DetailedPeakFeatureCalculators =
            ImmutableList.ValueOf(PeakFeatureCalculator.Calculators.OfType<DetailedPeakFeatureCalculator>());
        public static PeakGroupIntegrator GetPeakGroupIntegrator(SrmDocument document, IdentityPath peptideIdentityPath,
            IEnumerable<TransitionGroup> comparableGroup, ChromatogramSet chromatogramSet, ChromFileInfo chromFileInfo)
        {
            var peptideDocNode = (PeptideDocNode) document.FindNode(peptideIdentityPath);
            var identityHashSet = comparableGroup.ToHashSet(new IdentityEqualityComparer<TransitionGroup>());
            var transitionGroupDocNodes = peptideDocNode.TransitionGroups.Where(
                node => identityHashSet.Contains(node.TransitionGroup));

            return new PeakGroupIntegrator(document, peptideIdentityPath, peptideDocNode, transitionGroupDocNodes, chromatogramSet, chromFileInfo);
        }

        public static IEnumerable<PeakGroupIntegrator> GetPeakGroupIntegrators(SrmDocument document,
            IdentityPath peptideIdentityPath, ChromatogramSet chromatogramSet, ChromFileInfo chromFileInfo)
        {
            var peptideDocNode = (PeptideDocNode) document.FindNode(peptideIdentityPath);
            foreach (var comparableGroup in PeakFeatureEnumerator.ComparableGroups(peptideDocNode))
            {
                yield return GetPeakGroupIntegrator(document, peptideIdentityPath,
                    comparableGroup.Select(tg => tg.TransitionGroup), chromatogramSet, chromFileInfo);
            }
        }

        private PeakGroupIntegrator(SrmDocument document, IdentityPath peptideIdentityPath,
            PeptideDocNode peptideDocNode, IEnumerable<TransitionGroupDocNode> comparableGroup, ChromatogramSet chromatogramSet, ChromFileInfo chromFileInfo)
        {
            Document = document;
            PeptideIdentityPath = peptideIdentityPath;
            PeptideDocNode = peptideDocNode;
            ComparableGroup = ImmutableList.ValueOf(comparableGroup);
            ChromatogramSet = chromatogramSet;
            ChromFileInfo = chromFileInfo;
        }

        public SrmDocument Document { get; }
        public IdentityPath PeptideIdentityPath { get; }
        public PeptideDocNode PeptideDocNode { get; }
        public ImmutableList<TransitionGroupDocNode> ComparableGroup { get; }
        public ChromatogramSet ChromatogramSet { get; }
        public ChromFileInfo ChromFileInfo { get; }

        public float MzMatchTolerance
        {
            get
            {
                return (float) Document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            }
        }

        public IEnumerable<float> ScorePeak(double startTime, double endTime, IEnumerable<DetailedPeakFeatureCalculator> calculators)
        {
            var peptideChromDataSets = MakePeptideChromDataSets();
            var explicitPeakBounds = new PeakBounds(startTime, endTime);
            peptideChromDataSets.PickChromatogramPeaks(explicitPeakBounds);
            return peptideChromDataSets.DataSets[0].PeakSets.First().DetailScores;
        }

        internal PeptideChromDataSets MakePeptideChromDataSets()
        {
            var peptideChromDataSets = new PeptideChromDataSets(PeptideDocNode, Document, ChromFileInfo,
                DetailedPeakFeatureCalculators, false);
            foreach (var transitionGroup in ComparableGroup)
            {
                var chromDatas = new List<ChromData>();
                var chromatogramGroupInfo = LoadChromatogramGroupInfo(transitionGroup);
                if (chromatogramGroupInfo == null)
                {
                    continue;
                }
                foreach (var transition in transitionGroup.Transitions)
                {
                    var chromatogramInfo =
                        chromatogramGroupInfo.GetTransitionInfo(transition, MzMatchTolerance, TransformChrom.raw, null);
                    if (chromatogramInfo == null)
                    {
                        continue;
                    }
                    var rawTimeIntensities = chromatogramInfo.TimeIntensities;
                    chromatogramInfo.Transform(TransformChrom.interpolated);
                    var interpolatedTimeIntensities = chromatogramInfo.TimeIntensities;
                    chromDatas.Add(new ChromData(transition, rawTimeIntensities, interpolatedTimeIntensities));
                }

                if (!chromDatas.Any())
                {
                    continue;
                }

                var chromDataSet = new ChromDataSet(true, PeptideDocNode.ModifiedTarget,
                    Document.Settings.TransitionSettings.FullScan.AcquisitionMethod, chromDatas.ToArray())
                {
                    NodeGroup = transitionGroup
                };
                peptideChromDataSets.Add(PeptideDocNode, chromDataSet);
            }

            return peptideChromDataSets;
        }

        public ChromatogramGroupInfo LoadChromatogramGroupInfo(TransitionGroupDocNode transitionGroup)
        {
            if (!Document.Settings.MeasuredResults.TryLoadChromatogram(ChromatogramSet, PeptideDocNode, transitionGroup,
                MzMatchTolerance, true, out var infos))
            {
                return null;
            }

            foreach (var chromatogramInfo in infos)
            {
                if (Equals(chromatogramInfo.FilePath, ChromFileInfo.FilePath))
                {
                    return chromatogramInfo;
                }
            }

            return null;
        }
    }
}
