/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Esp
{
    /// <summary>
    /// Provides a public interface for calculating all of the training and test
    /// features used in the original ESP paper:
    /// 
    /// Prediction of high-responding peptides for targeted protein assays by mass spectrometry
    /// Vincent A. Fusaro, D. R. Mani, Jill P. Mesirov & Steven A. Carr
    /// Nature Biotechnology (2009) 27:190-198.
    /// 
    /// http://www.nature.com/nbt/journal/v27/n2/abs/nbt.1524.html
    /// </summary>
    public class EspFeatureCalc
    {
        public const string EXT = ".csv"; // Not L10N

        private static readonly SequenceMassCalc MASS_CALC = new SequenceMassCalc(MassType.Monoisotopic);

        public static double CalculateFeature(EspFeatureDb.FeatureDef feature, string seq)
        {
            switch (feature)
            {
                case EspFeatureDb.FeatureDef.length:
                    return seq.Length;
                case EspFeatureDb.FeatureDef.mass:
                    return MASS_CALC.GetPrecursorMass(seq);
                case EspFeatureDb.FeatureDef.pI:
                    return PiCalc.Calculate(seq);
                case EspFeatureDb.FeatureDef.AVG_Gas_phase_basicity:
                    return GasPhaseBasicityCalc.Calculate(seq).Average();
                case EspFeatureDb.FeatureDef.nAcidic:
                    return AminoAcid.Count(seq, 'D', 'E');// Not L10N: Amino acid
                case EspFeatureDb.FeatureDef.nBasic:
                    return AminoAcid.Count(seq, 'R', 'H', 'K'); // Not L10N: Amino acid
                default:
                    return EspFeatureDb.CalculateFeature(feature, seq);
            }
        }

        public static IEnumerable<double> CalculateFeatures(IEnumerable<EspFeatureDb.FeatureDef> features, string seq)
        {
            return features.Select(f => CalculateFeature(f, seq));
        }

        public static IEnumerable<double> CalculateAllFeatures(string seq)
        {
            return CalculateFeatures(EspFeatureDb.AllFeatures, seq);
        }

        public static void WriteFeatures(string filePath, IEnumerable<string> seqs, CultureInfo cultureInfo)
        {
            using (var writer = new StreamWriter(filePath))
            {
                WriteFeatures(writer, seqs, cultureInfo);
            }
        }

        public static void WriteFeatures(TextWriter writer, IEnumerable<string> seqs, CultureInfo cultureInfo)
        {
            WriteRow(writer, "sequence", EspFeatureDb.AllFeatures.Cast<object>(), // Not L10N
                     cultureInfo);
            foreach (var seq in seqs)
                WriteRow(writer, seq, CalculateAllFeatures(seq).Cast<object>(), cultureInfo);
        }

        private static void WriteRow(TextWriter writer,
                                     string seqColumn,
                                     IEnumerable<object> featureColumns,
                                     CultureInfo cultureInfo)
        {
            char separator = TextUtil.GetCsvSeparator(cultureInfo);

            writer.Write(seqColumn);
            foreach (var featureColumn in featureColumns)
            {
                writer.Write(separator);
                if (featureColumn is double)
                    writer.Write(((double) featureColumn).ToString("0.######", cultureInfo)); // Not L10N
                else
                    writer.Write(featureColumn);
            }
            writer.WriteLine();
        }
    }
}
