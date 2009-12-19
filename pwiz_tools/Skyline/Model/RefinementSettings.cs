namespace pwiz.Skyline.Model
{
    public sealed class RefinementSettings
    {
        public RefinementSettings(int? minPeptidesPerProtein,
                                  bool removeDuplicatePeptides,
                                  bool removeRepeatedPeptides,
                                  int? minTransitionsPepPrecursor,
                                  bool removeHeavyWithLight,
                                  double? minPeakFoundRatio,
                                  double? maxPeakFoundRatio,
                                  bool removeMissingResults,
                                  double? rtRegressionThreshold,
                                  double? dotProductThreshold)
        {
            MinPeptidesPerProtein = minPeptidesPerProtein;
            RemoveDuplicatePeptides = removeDuplicatePeptides;
            RemoveRepeatedPeptides = removeRepeatedPeptides;
            MinTransitionsPepPrecursor = minTransitionsPepPrecursor;
            RemoveHeavyWithLight = removeHeavyWithLight;
            MinPeakFoundRatio = minPeakFoundRatio;
            MaxPeakFoundRatio = maxPeakFoundRatio;
            RemoveMissingResults = removeMissingResults;
            RTRegressionThreshold = rtRegressionThreshold;
            DotProductThreshold = dotProductThreshold;
        }

        public int? MinPeptidesPerProtein { get; private set; }
        public bool RemoveDuplicatePeptides { get; private set; }
        public bool RemoveRepeatedPeptides { get; private set; }
        public int? MinTransitionsPepPrecursor { get; private set; }
        public bool RemoveHeavyWithLight { get; private set; }
        public double? MinPeakFoundRatio { get; private set; }
        public double? MaxPeakFoundRatio { get; private set; }
        public bool RemoveMissingResults { get; private set; }
        public double? RTRegressionThreshold { get; private set; }
        public double? DotProductThreshold { get; private set; }
    }
}