using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BumberDash.lib
{
    public static class Util
    {
        #region Lists of valid paramaters

        public static Dictionary<string, string> parameterTypes = new Dictionary<string, string>
                                                                      {
                                                                          {"UseChargeStateFromMS", "bool"},
                                                                          {"AdjustPrecursorMass", "bool"},
                                                                          {"DuplicateSpectra", "bool"},
                                                                          {"UseSmartPlusThreeModel", "bool"},
                                                                          {"MassReconMode", "bool"},
                                                                          {"UseNETAdjustment", "bool"},
                                                                          {"ComputeXCorr", "bool"},
                                                                          {"UseAvgMassOfSequences", "bool"},
                                                                          {"CleanLibSpectra", "bool"},
                                                                          {"FASTARefreshResults", "bool"},
                                                                          {"RecalculateLibPepMasses", "bool"},
                                                                          {"DeisotopingMode", "int"},
                                                                          {"MinTerminiCleavages", "int"},
                                                                          {"CPUs", "int"},
                                                                          {"MaxMissedCleavages", "int"},
                                                                          {"ProteinSamplingTime", "int"},
                                                                          {"MaxDynamicMods", "int"},
                                                                          {"MaxNumPreferredDeltaMasses", "int"},
                                                                          {"NumChargeStates", "int"},
                                                                          {"NumIntensityClasses", "int"},
                                                                          {"MaxResultRank", "int"},
                                                                          {"MaxPeakCount", "int"},
                                                                          {"TagLength", "int"},
                                                                          {"MinPeptideLength", "int"},
                                                                          {"MaxTagCount", "int"},
                                                                          {"MaxPeptideLength","int"},
                                                                          {"MaxAmbResultsForBlindMods","int"},
                                                                          {"LibMaxPeakCount", "int"},
                                                                          {"MinPeptideMass", "double"},
                                                                          {"IsotopeMzTolerance", "double"},
                                                                          {"ComplementMzTolerance", "double"},
                                                                          {"TicCutoffPercentage", "double"},
                                                                          {"PrecursorMzTolerance", "double"},
                                                                          {"MaxPeptideMass", "double"},
                                                                          {"ClassSizeMultiplier", "double"},
                                                                          {"MaxPrecursorAdjustment", "double"},
                                                                          {"MinPrecursorAdjustment", "double"},
                                                                          {"BlosumThreshold", "double"},
                                                                          {"MaxModificationMassPlus", "double"},
                                                                          {"MaxModificationMassMinus", "double"},
                                                                          {"IntensityScoreWeight", "double"},
                                                                          {"MzFidelityScoreWeight", "double"},
                                                                          {"ComplementScoreWeight", "double"},
                                                                          {"MaxTagScore", "double"},
                                                                          {"PrecursorAdjustmentStep", "double"},
                                                                          {"LibTicCutoffPercentage", "double"},
                                                                          {"Name", "string"},
                                                                          {"CleavageRules", "string"},
                                                                          {"FragmentationRule", "string"},
                                                                          {"PrecursorMzToleranceUnits", "string"},
                                                                          {"FragmentMzToleranceUnits", "string"},
                                                                          {"Blosum", "string"},
                                                                          {"UnimodXML", "string"},
                                                                          {"ExplainUnknownMassShiftsAs", "string"},
                                                                          {"OutputSuffix", "string"},
                                                                          {"DecoyPrefix", "string"},
                                                                          {"AvgPrecursorMzTolerance","string"},
                                                                          {"MonoPrecursorMzTolerance","string"},
                                                                          {"NTerminusMzTolerance", "string"},
                                                                          {"CTerminusMzTolerance", "string"},
                                                                          {"FragmentMzTolerance", "string"},
                                                                          {"MonoisotopeAdjustmentSet","string"},
                                                                          {"OutputFormat","string"},
                                                                          {"PrecursorMzToleranceRule","string"}
                                                                      };

        #endregion
    }
}
