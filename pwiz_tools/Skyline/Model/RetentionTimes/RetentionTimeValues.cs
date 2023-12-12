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

using System;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.RetentionTimes
{
    /// <summary>
    /// Holds the retention time, start/end retention times, and optional Fwhm.
    /// Additionally, it holds the "Height", which can be used by a precursor to
    /// decide which Transition's chromInfo to use for reporting "BestRetentionTime".
    /// </summary>
    public class RetentionTimeValues : Immutable
    {
        public RetentionTimeValues(double retentionTime, double startRetentionTime, double endRetentionTime, double height, double? fwhm)
        {
            RetentionTime = retentionTime;
            StartRetentionTime = startRetentionTime;
            EndRetentionTime = endRetentionTime;
            Height = height;
            Fwhm = fwhm;
        }
        public double RetentionTime { get; private set; }
        public double StartRetentionTime { get; private set; }
        public double EndRetentionTime { get; private set; }
        public double Height { get; private set; }
        public double Fwb { get { return EndRetentionTime - StartRetentionTime; } }

        public double? Fwhm { get; private set; }

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

            return ChangeProp(ImClone(this), im =>
            {
                im.RetentionTime = retentionTime;
                im.StartRetentionTime = startRetentionTime;
                im.EndRetentionTime = endRetentionTime;
                im.Fwhm = fwhm;
            });
        }

        public static RetentionTimeValues FromTransitionChromInfo(TransitionChromInfo transitionChromInfo)
        {
            if (null == transitionChromInfo || transitionChromInfo.IsEmpty)
            {
                return null;
            }

            double apexTime;
            if (transitionChromInfo.Height == 0)
            {
                // TransitionChromInfo.RetentionTime cannot be trusted if the height is zero, so always
                // set it to the midpoint between the start and end times.
                apexTime = (transitionChromInfo.StartRetentionTime + transitionChromInfo.EndRetentionTime) / 2;
            }
            else
            {
                apexTime = transitionChromInfo.RetentionTime;
            }

            return new RetentionTimeValues(apexTime,
                transitionChromInfo.StartRetentionTime, transitionChromInfo.EndRetentionTime,
                transitionChromInfo.Height, transitionChromInfo.Fwhm);
        }

        public static RetentionTimeValues FromTransitionGroupChromInfo(TransitionGroupChromInfo transitionGroupChromInfo)
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
                transitionGroupChromInfo.Height ?? 0, transitionGroupChromInfo.Fwhm);
        }

        /// <summary>
        /// Returns a RetentionTimeValues which is the result of merging the passed in set of values.
        /// The returned merge things will have a start and end time which encompasses all of the values.
        /// The RetentionTime and Fwhm will be set from the RetentionTimeValues with the greatest Height.
        /// </summary>
        public static RetentionTimeValues Merge(params RetentionTimeValues[] values)
        {
            RetentionTimeValues best = null;

            foreach (var value in values)
            {
                if (value == null)
                {
                    continue;
                }

                if (best == null || value.Height > best.Height)
                {
                    best = value;
                }

                if (best.StartRetentionTime > value.StartRetentionTime ||
                    best.EndRetentionTime < value.EndRetentionTime)
                {
                    best = ChangeProp(ImClone(best), im =>
                    {
                        im.StartRetentionTime = Math.Min(best.StartRetentionTime, value.StartRetentionTime);
                        im.EndRetentionTime = Math.Max(best.EndRetentionTime, value.EndRetentionTime);
                    });
                }
            }

            return best;
        }
    }
}