using System;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Feature set used for scoring coeluting fragment ions.
    /// Maps to osprey-core/src/types.rs CoelutionFeatureSet.
    /// </summary>
    public class CoelutionFeatureSet
    {
        #region Pairwise Coelution

        public double CoelutionSum { get; set; }
        public double CoelutionMin { get; set; }
        public double CoelutionMax { get; set; }
        public byte NCoelutingFragments { get; set; }
        public byte NFragmentPairs { get; set; }
        public double[] FragmentCorr { get; set; }

        #endregion

        #region Peak Shape

        public double PeakApex { get; set; }
        public double PeakArea { get; set; }
        public double PeakWidth { get; set; }
        public double PeakSymmetry { get; set; }
        public double SignalToNoise { get; set; }
        public double PeakSharpness { get; set; }
        public ushort NScans { get; set; }

        #endregion

        #region Spectral at Apex

        public double Hyperscore { get; set; }
        public double Xcorr { get; set; }
        public double DotProduct { get; set; }
        public double DotProductSmz { get; set; }
        public double DotProductTop6 { get; set; }
        public double DotProductTop5 { get; set; }
        public double DotProductTop4 { get; set; }
        public double DotProductSmzTop6 { get; set; }
        public double DotProductSmzTop5 { get; set; }
        public double DotProductSmzTop4 { get; set; }
        public double FragmentCoverage { get; set; }
        public double SequenceCoverage { get; set; }
        public double ElutionWeightedCosine { get; set; }
        public double ExplainedIntensity { get; set; }
        public byte ConsecutiveIons { get; set; }
        public byte BasePeakRank { get; set; }
        public byte Top6Matches { get; set; }

        #endregion

        #region Mass Accuracy

        public double MassAccuracyMean { get; set; }
        public double AbsMassAccuracyMean { get; set; }
        public double MassAccuracyStd { get; set; }

        #endregion

        #region RT Deviation

        public double RtDeviation { get; set; }
        public double AbsRtDeviation { get; set; }

        #endregion

        #region MS1 Features

        public double Ms1PrecursorCoelution { get; set; }
        public double Ms1IsotopeCosine { get; set; }

        #endregion

        #region Peptide Properties

        public byte ModificationCount { get; set; }
        public byte PeptideLength { get; set; }
        public byte MissedCleavages { get; set; }

        #endregion

        #region Median Polish

        public double MedianPolishCosine { get; set; }
        public double MedianPolishRsquared { get; set; }
        public double MedianPolishResidualRatio { get; set; }

        #endregion

        #region SG-Weighted

        public double SgWeightedXcorr { get; set; }
        public double SgWeightedCosine { get; set; }
        public double MedianPolishMinFragmentR2 { get; set; }
        public double MedianPolishResidualCorrelation { get; set; }

        #endregion

        public CoelutionFeatureSet()
        {
            FragmentCorr = new double[6];
        }
    }
}
