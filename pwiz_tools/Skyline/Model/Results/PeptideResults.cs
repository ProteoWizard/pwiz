/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// What a molecule keeps about its results, which is only the two things nothing else can work
    /// out: whether the user left a replicate out of the calibration curve, and the concentration
    /// they entered for it.
    /// <para>
    /// Everything else a <see cref="PeptideChromInfo"/> has - the peak count ratio, the retention
    /// time, the label ratios - is aggregated from the precursors, so it is rebuilt on demand by
    /// <see cref="MoleculeResults.GetPeptideChromInfos()"/> rather than stored. That is why there is
    /// no unconverted state here, unlike <see cref="TransitionResults.ChromInfos"/>: there is
    /// nothing which has to wait for the .skyd.
    /// </para>
    /// </summary>
    public class PeptideResults : Immutable
    {
        /// <summary>
        /// Builds the columnar form from the chrom infos a document holds, keeping only the two
        /// values which cannot be derived. Returns null when there is nothing to keep, which is the
        /// usual case: most documents have no analyte concentrations and exclude no replicate.
        /// </summary>
        public static PeptideResults FromChromInfos(Results<PeptideChromInfo> results)
        {
            if (results == null)
            {
                return null;
            }

            var fileIds = new List<ChromFileInfoId>();
            var counts = new List<int>();
            var excludeFromCalibration = new List<bool>();
            var analyteConcentrations = new List<double?>();
            bool anythingToKeep = false;
            foreach (var chromInfoList in results)
            {
                int count = 0;
                foreach (var chromInfo in chromInfoList)
                {
                    fileIds.Add(chromInfo.FileId);
                    excludeFromCalibration.Add(chromInfo.ExcludeFromCalibration);
                    analyteConcentrations.Add(chromInfo.AnalyteConcentration);
                    anythingToKeep = anythingToKeep || chromInfo.ExcludeFromCalibration ||
                                     chromInfo.AnalyteConcentration.HasValue;
                    count++;
                }

                counts.Add(count);
            }

            if (!anythingToKeep)
            {
                return null;
            }

            return new PeptideResults(new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds))
                .ChangeExcludeFromCalibration(excludeFromCalibration)
                .ChangeAnalyteConcentrations(analyteConcentrations);
        }

        public PeptideResults(ChromFileIds chromFileIds)
        {
            ChromFileIds = chromFileIds;
        }

        public ChromFileIds ChromFileIds { get; private set; }

        /// <summary>
        /// Whether the user left each replicate out of the calibration curve. Almost always all
        /// false, which is why this goes through
        /// <see cref="ImmutableListFactory.MaybeConstant{T}"/>.
        /// </summary>
        public ImmutableList<bool> ExcludeFromCalibration { get; private set; }

        /// <summary>
        /// The concentration the user entered for each replicate, or null where they entered none.
        /// </summary>
        public ImmutableList<double?> AnalyteConcentrations { get; private set; }

        public PeptideResults ChangeExcludeFromCalibration(IEnumerable<bool> value)
        {
            return ChangeProp(ImClone(this),
                im => im.ExcludeFromCalibration = ImmutableList.ValueOf(value).MaybeConstant());
        }

        public PeptideResults ChangeAnalyteConcentrations(IEnumerable<double?> value)
        {
            return ChangeProp(ImClone(this),
                im => im.AnalyteConcentrations = ImmutableList.ValueOf(value).MaybeConstant());
        }

        /// <summary>
        /// The position of one file's entry in one replicate, or -1. See
        /// <see cref="TransitionGroupResults.IndexOfFile"/>.
        /// </summary>
        public int IndexOfFile(int replicateIndex, ChromFileInfoId fileId)
        {
            return ChromFileIds.IndexOfFile(replicateIndex, fileId);
        }

        public bool GetExcludeFromCalibration(int position)
        {
            return ExcludeFromCalibration != null && ExcludeFromCalibration[position];
        }

        public double? GetAnalyteConcentration(int position)
        {
            return AnalyteConcentrations?[position];
        }

        /// <summary>
        /// Compared by value. See
        /// <see cref="TransitionGroupResults.Equals(TransitionGroupResults)"/>.
        /// </summary>
        protected bool Equals(PeptideResults other)
        {
            return Equals(ChromFileIds, other.ChromFileIds) &&
                   Equals(ExcludeFromCalibration, other.ExcludeFromCalibration) &&
                   Equals(AnalyteConcentrations, other.AnalyteConcentrations);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == GetType() && Equals((PeptideResults) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = ChromFileIds.GetHashCode();
                result = (result * 397) ^ (ExcludeFromCalibration?.GetHashCode() ?? 0);
                result = (result * 397) ^ (AnalyteConcentrations?.GetHashCode() ?? 0);
                return result;
            }
        }
    }
}
