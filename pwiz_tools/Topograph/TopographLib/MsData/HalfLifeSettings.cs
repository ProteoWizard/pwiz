/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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

using System.Xml.Serialization;

namespace pwiz.Topograph.MsData
{
    [XmlRoot("half_life_settings")]
    public struct HalfLifeSettings
    {
        public static readonly HalfLifeSettings Default = new HalfLifeSettings
                                                     {
                                                         NewlySynthesizedTracerQuantity = TracerQuantity.PartialLabelDistribution,
                                                         PrecursorPoolCalculation = PrecursorPoolCalculation.MedianPerSample,
                                                         SimpleLinearRegression = true,
                                                         CurrentPrecursorPool = 100,
                                                     };

        public bool ForceThroughOrigin { get; set; }
        public PrecursorPoolCalculation PrecursorPoolCalculation { get; set; }
        public TracerQuantity NewlySynthesizedTracerQuantity { get; set; }
        public EvviesFilterEnum EvviesFilter { get; set; }
        public double MinimumAuc { get; set; }
        public double MinimumDeconvolutionScore { get; set; }
        public double MinimumTurnoverScore { get; set; }
        public double InitialPrecursorPool { get; set; }
        public double CurrentPrecursorPool { get; set; }
        public bool ByProtein { get; set; }
        public bool BySample { get; set; }
        public bool SimpleLinearRegression { get; set; }

        public static double TryParseDouble(string strValue, double defaultValue)
        {
            double value;
            if (string.IsNullOrEmpty(strValue) || !double.TryParse(strValue, out value))
            {
                return defaultValue;
            }
            return value;
        }
    }
}
