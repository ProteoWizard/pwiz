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
                                                                          {"DeisotopingMode", "int"},
                                                                          {"NumMinTerminiCleavages", "int"},
                                                                          {"CPUs", "int"},
                                                                          {"StartSpectraScanNum", "int"},
                                                                          {"StartProteinIndex", "int"},
                                                                          {"NumMaxMissedCleavages", "int"},
                                                                          {"EndSpectraScanNum", "int"},
                                                                          {"EndProteinIndex", "int"},
                                                                          {"ProteinSampleSize", "int"},
                                                                          {"MaxDynamicMods", "int"},
                                                                          {"MaxNumPreferredDeltaMasses", "int"},
                                                                          {"NumChargeStates", "int"},
                                                                          {"NumIntensityClasses", "int"},
                                                                          {"MaxResults", "int"},
                                                                          {"MaxPeakCount", "int"},
                                                                          {"TagLength", "int"},
                                                                          {"MinCandidateLength", "int"},
                                                                          {"MaxTagCount", "int"},
                                                                          {"MinSequenceMass", "double"},
                                                                          {"IsotopeMzTolerance", "double"},
                                                                          {"FragmentMzTolerance", "double"},
                                                                          {"ComplementMzTolerance", "double"},
                                                                          {"TicCutoffPercentage", "double"},
                                                                          {"PrecursorMzTolerance", "double"},
                                                                          {"MaxSequenceMass", "double"},
                                                                          {"ClassSizeMultiplier", "double"},
                                                                          {"MaxPrecursorAdjustment", "double"},
                                                                          {"MinPrecursorAdjustment", "double"},
                                                                          {"BlosumThreshold", "double"},
                                                                          {"MaxModificationMassPlus", "double"},
                                                                          {"MaxModificationMassMinus", "double"},
                                                                          {"NTerminusMzTolerance", "double"},
                                                                          {"CTerminusMzTolerance", "double"},
                                                                          {"NTerminusMassTolerance", "double"},
                                                                          {"CTerminusMassTolerance", "double"},
                                                                          {"IntensityScoreWeight", "double"},
                                                                          {"MzFidelityScoreWeight", "double"},
                                                                          {"ComplementScoreWeight", "double"},
                                                                          {"MaxTagScore", "double"},
                                                                          {"Name", "string"},
                                                                          {"CleavageRules", "string"},
                                                                          {"PrecursorMzToleranceUnits", "string"},
                                                                          {"FragmentMzToleranceUnits", "string"},
                                                                          {"Blosum", "string"},
                                                                          {"UnimodXML", "string"},
                                                                          {"ExplainUnknownMassShiftsAs", "string"},
                                                                          {"OutputSuffix", "string"},
                                                                          {"DecoyPrefix", "string"}
                                                                      };

        #endregion
    }
}
