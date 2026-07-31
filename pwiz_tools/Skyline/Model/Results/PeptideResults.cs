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

        /// <summary>
        /// An empty set laid out to match the document's replicates and files, for a molecule which
        /// is about to be given one of the two values it can keep and has none yet. Built from the
        /// measured results rather than from any chrom infos, so it reads nothing.
        /// </summary>
        public static PeptideResults ForMeasuredResults(MeasuredResults measuredResults)
        {
            if (measuredResults == null)
            {
                return null;
            }

            var fileIds = new List<ChromFileInfoId>();
            var counts = new List<int>();
            foreach (var chromatogramSet in measuredResults.Chromatograms)
            {
                int count = 0;
                foreach (var fileInfo in chromatogramSet.MSDataFileInfos)
                {
                    fileIds.Add(fileInfo.FileId);
                    count++;
                }

                counts.Add(count);
            }

            return new PeptideResults(new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds));
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
        /// Whether the user left a replicate out of the calibration curve, asked of the replicate
        /// rather than of one of its files.
        /// <para>
        /// The entries here are per file, because that is what
        /// <see cref="PeptideDocNode.PeptideChromInfoListCalculator"/> produces - one
        /// <see cref="PeptideChromInfo"/> per file, keyed on FileIndex. Both of these values
        /// describe the sample rather than an injection of it, though, so the callers which matter
        /// ask at the replicate level, and a replicate counts as excluded when any of its files is.
        /// </para>
        /// </summary>
        public bool AnyExcludeFromCalibration(int replicateIndex)
        {
            foreach (int position in GetPositions(replicateIndex))
            {
                if (GetExcludeFromCalibration(position))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The first concentration entered for any file of a replicate, or null.
        /// See <see cref="AnyExcludeFromCalibration"/>.
        /// </summary>
        public double? GetAnalyteConcentrationForReplicate(int replicateIndex)
        {
            foreach (int position in GetPositions(replicateIndex))
            {
                var concentration = GetAnalyteConcentration(position);
                if (concentration.HasValue)
                {
                    return concentration;
                }
            }

            return null;
        }

        /// <summary>
        /// Sets the value at one position, starting the list from the default when there is none
        /// yet, which is how the usual document arrives here.
        /// </summary>
        public PeptideResults ChangeExcludeFromCalibration(int position, bool value)
        {
            return ChangeExcludeFromCalibration(SetAt(ExcludeFromCalibration, position, value, false));
        }

        public PeptideResults ChangeAnalyteConcentration(int position, double? value)
        {
            return ChangeAnalyteConcentrations(SetAt(AnalyteConcentrations, position, value, null));
        }

        private IEnumerable<T> SetAt<T>(ImmutableList<T> values, int position, T value, T defaultValue)
        {
            var list = new T[ChromFileIds.FileIds.Count];
            for (int i = 0; i < list.Length; i++)
            {
                list[i] = values == null ? defaultValue : values[i];
            }

            list[position] = value;
            return list;
        }

        private IEnumerable<int> GetPositions(int replicateIndex)
        {
            var replicatePositions = ChromFileIds.ReplicatePositions;
            if (replicateIndex < 0 || replicateIndex >= replicatePositions.ReplicateCount)
            {
                yield break;
            }

            int start = replicatePositions.GetStart(replicateIndex);
            for (int position = start; position < start + replicatePositions.GetCount(replicateIndex); position++)
            {
                yield return position;
            }
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
