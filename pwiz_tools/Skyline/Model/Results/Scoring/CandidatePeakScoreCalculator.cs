
namespace pwiz.Skyline.Model.Results.Scoring
{
    public class CandidatePeakScoreCalculator
    {
        private ChromatogramGroupInfo _chromatogramGroupInfo;
        private PeakScoringContext _peakScoringContext;
        private IPeptidePeakData<ISummaryPeakData> _summaryPeakData;
        public CandidatePeakScoreCalculator(int peakIndex, ChromatogramGroupInfo chromatogramGroup, PeakScoringContext context,
            IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            PeakIndex = peakIndex;
            _chromatogramGroupInfo = chromatogramGroup;
            _peakScoringContext = context;
            _summaryPeakData = summaryPeakData;
        }

        public int PeakIndex { get; }

        public float Calculate(IPeakFeatureCalculator peakFeatureCalculator)
        {
            if (peakFeatureCalculator is SummaryPeakFeatureCalculator)
            {
                return peakFeatureCalculator.Calculate(_peakScoringContext, _summaryPeakData);
            }

            return _chromatogramGroupInfo.GetScore(peakFeatureCalculator.GetType(), PeakIndex);
        }
    }
}
