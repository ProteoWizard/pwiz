/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Linq;
using System.Xml.Serialization;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    /// <summary>
    /// Options for the display of the calibration curve window, which get persisted in Settings.
    /// </summary>
    [XmlRoot("calibration_curve")]
    public class CalibrationCurveOptions
    {
        public CalibrationCurveOptions()
        {
            DisplaySampleTypes = new[] {SampleType.STANDARD.Name, SampleType.QC.Name, SampleType.UNKNOWN.Name};
            ShowLegend = true;
            ShowSelection = true;
        }

        public bool LogPlot { get; set; }
        public string[] DisplaySampleTypes { get; set; }

        public bool DisplaySampleType(SampleType sampleType)
        {
            return DisplaySampleTypes.Contains(sampleType.Name);
        }

        public bool ShowLegend { get; set; }
        public bool ShowSelection { get; set; }

        public void SetDisplaySampleType(SampleType sampleType, bool display)
        {
            if (display)
            {
                DisplaySampleTypes = DisplaySampleTypes.Concat(new[] {sampleType.Name}).Distinct().ToArray();
            }
            else
            {
                DisplaySampleTypes = DisplaySampleTypes.Except(new[] {sampleType.Name}).ToArray();
            }
        }
    }
}
