/*
 * Original author: Max Horowitz-Gelb <maxhg .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Threading;

namespace pwiz.Skyline.Model.RetentionTimes
{
    /// <summary>
    /// Aligner abstract class that defines a template for how Alligners should work for the Tric Algorithm
    /// In order to traverse the MST
    /// </summary>
    public abstract class Aligner
    {
        //The index of the the run that the independent values came from
        protected int _origXFileIndex;
        //The index of the the run that the Y values came from
        protected int _origYFileIndex;
        
        public bool CanCalculateReverseRegression { get; private set; }

        /// <summary>
        /// Generic Constructor for an Aligner. Sets values and trains aligner
        /// </summary>
        /// <param name="origXFileIndex">Index of the run from which the independent values come from</param>
        /// <param name="origYFileIndex">Index of the run from which the Y values come from</param>
        protected Aligner(int origXFileIndex, int origYFileIndex)
        {
            _origXFileIndex = origXFileIndex;
            _origYFileIndex = origYFileIndex;
            CanCalculateReverseRegression = true;
        }

        /// <summary>
        /// Generic Constructor for aligner when we don't care about file indexes
        /// </summary>
        protected Aligner()
        {
            _origXFileIndex = -1;
            _origYFileIndex = -1;
            CanCalculateReverseRegression = false;
        }

        public abstract void Train(double[] xArr, double[] yArr, CancellationToken token);

        /// <summary>
        /// Gets complementary run's retention time given a retention time.
        /// </summary>
        /// <param name="dependent">The retention time we know</param>
        /// <param name="dependentFileIndex">The run index that the value x comes from</param>
        /// <returns>The retention time from the complementary run using the precalculated alignment</returns>
        public double GetValueFor(double dependent, int dependentFileIndex)
        {
            if (!CanCalculateReverseRegression)
            {
                throw new ArgumentException(@"This method is not appropriate for this aligner. It does not contain knowledge on file indexes.");
            }
            // If x comes from the the same run as the original independent retention times
            // Calculate complement in normal order of alignment
            if (dependentFileIndex == _origXFileIndex)
            {
                return GetValue(dependent);
            }
            // If x comes from the same run as the x retention times
            // Calculate in reverse order
            else if (dependentFileIndex == _origYFileIndex)
            {
                return GetValueReversed(dependent);
            }
            else
            {
                throw new ArgumentException(@"Independent index is not related to this alignment.");
            }
        }

        public abstract double GetValue(double x);

        public abstract double GetValueReversed(double y);

        public abstract double GetRmsd();

        public abstract void GetSmoothedValues(out double[] xArr, out double[] yArr);
    }
}
