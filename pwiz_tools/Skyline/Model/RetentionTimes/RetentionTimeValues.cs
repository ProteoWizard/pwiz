/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.RetentionTimes
{
    /// <summary>
    /// Holds the retention time, start/end retention times, and an 
    /// optional full width half max (fwhm).
    /// </summary>
    public struct RetentionTimeValues
    {
        public RetentionTimeValues(double retentionTime, 
            double startRetentionTime, double endRetentionTime, 
            double? fwhm) : this()
        {
            RetentionTime = retentionTime;
            StartRetentionTime = startRetentionTime;
            EndRetentionTime = endRetentionTime;
            Fwhm = fwhm;
        }
        public double RetentionTime { get; private set; }
        public double StartRetentionTime { get; private set; }
        public double EndRetentionTime { get; private set; }
        public double? Fwhm { get; private set; }
        public double Fwb { get { return EndRetentionTime - StartRetentionTime; } }

        public RetentionTimeValues Scale(IRegressionFunction regressionFunction)
        {
            if (null == regressionFunction)
            {
                return this;
            }
            double startRetentionTime = regressionFunction.GetY(StartRetentionTime);
            double endRetentionTime = regressionFunction.GetY(EndRetentionTime);
            double retentionTime = regressionFunction.GetY(RetentionTime);
            double? fwhm;
            if (Fwhm.HasValue)
            {
                double t1 = regressionFunction.GetY(RetentionTime - Fwhm.Value/2);
                double t2 = regressionFunction.GetY(RetentionTime + Fwhm.Value/2);
                fwhm = t2 - t1;
            }
            else
            {
                fwhm = null;
            }
            return new RetentionTimeValues
                {
                    RetentionTime = retentionTime,
                    StartRetentionTime = startRetentionTime,
                    EndRetentionTime = endRetentionTime,
                    Fwhm = fwhm,
                };
        }

        public static RetentionTimeValues? GetValues(TransitionChromInfo transitionChromInfo)
        {
            if (null == transitionChromInfo || transitionChromInfo.StartRetentionTime == 0 ||
                transitionChromInfo.EndRetentionTime == 0)
            {
                return null;
            }
            return new RetentionTimeValues(transitionChromInfo.RetentionTime, 
                transitionChromInfo.StartRetentionTime, transitionChromInfo.EndRetentionTime, 
                transitionChromInfo.Fwhm);
        }

        public static RetentionTimeValues? GetValues(TransitionGroupChromInfo transitionGroupChromInfo)
        {
            if (null == transitionGroupChromInfo ||
                !transitionGroupChromInfo.RetentionTime.HasValue ||
                !transitionGroupChromInfo.StartRetentionTime.HasValue ||
                !transitionGroupChromInfo.EndRetentionTime.HasValue)
            {
                return null;
            }
            return new RetentionTimeValues(transitionGroupChromInfo.RetentionTime.Value,
                transitionGroupChromInfo.StartRetentionTime.Value, transitionGroupChromInfo.EndRetentionTime.Value,
                transitionGroupChromInfo.Fwhm);
        }
    }
}