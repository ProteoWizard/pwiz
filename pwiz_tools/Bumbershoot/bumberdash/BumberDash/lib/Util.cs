//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the Bumberdash project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari, Matt Chambers
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

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

        #region Version Info

        public static string Version { get { return GetAssemblyVersion(Assembly.GetExecutingAssembly().GetName()); } }
        public static DateTime LastModified { get { return GetAssemblyLastModified(Assembly.GetExecutingAssembly().GetName()); } }

        public static AssemblyName GetAssemblyByName(string assemblyName)
        {
            if (Assembly.GetCallingAssembly().GetName().FullName.Contains(assemblyName))
                return Assembly.GetCallingAssembly().GetName();

            foreach (AssemblyName a in Assembly.GetCallingAssembly().GetReferencedAssemblies())
            {
                if (a.FullName.Contains(assemblyName + ','))
                    return a;
            }
            return null;
        }

        public static string GetAssemblyVersion(AssemblyName assembly)
        {
            Match versionMatch = Regex.Match(assembly.ToString(), @"Version=([\d.]+)");
            return versionMatch.Groups[1].Success ? versionMatch.Groups[1].Value : "unknown";
        }

        public static DateTime GetAssemblyLastModified(AssemblyName assembly)
        {
            return File.GetLastWriteTime(Assembly.ReflectionOnlyLoad(assembly.FullName).Location);
        }

        #endregion
    }
}
