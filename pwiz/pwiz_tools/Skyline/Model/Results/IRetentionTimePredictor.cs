/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Interface used by the ChromCacheBuilder to tell ChromDataProviders which
    /// peptides are necessary for predicting retention times ("first pass") and
    /// predicting retention times for the other peptides.
    /// </summary>
    public interface IRetentionTimePredictor
    {
        /// <summary>
        /// Returns true if the peptide is necessary for predicting retention time
        /// </summary>
        bool IsFirstPassPeptide(PeptideDocNode nodePep);
        double? GetPredictedRetentionTime(PeptideDocNode nodePep);
    }
}
