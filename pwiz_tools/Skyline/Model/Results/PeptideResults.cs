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

using System;
using System.Collections.Generic;
using System.Linq;
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
    /// no unconverted state here, unlike <see cref="TransitionGroupResults.NeedsPeakIndexes"/>:
    /// there is nothing which has to wait for the .skyd.
    /// </para>
    /// </summary>
    public class PeptideResults : Immutable
    {
        public static readonly PeptideResults EMPTY = new PeptideResults();

        private PeptideResults()
        {
        }

        /// <summary>
        /// Whether the user left a replicate out of the calibration curve. Almost always nothing at
        /// all: only the files a value was actually set for have an entry, so a document which
        /// excludes no replicate keeps a null map rather than a list of falses.
        /// </summary>
        public ChromFileIdMap<bool> ExcludeFromCalibration { get; private set; } = ChromFileIdMap<bool>.Empty;

        /// <summary>
        /// The concentration the user entered, for the files they entered one for. Null where they
        /// entered none, and a null map when they entered none anywhere.
        /// </summary>
        public ChromFileIdMap<double> AnalyteConcentrations { get; private set; } = ChromFileIdMap<double>.Empty;

        /// <summary>
        /// Whether there is nothing here at all, which is what a molecule with neither value has and
        /// what the callers store as no results rather than as an empty object.
        /// </summary>
        /// <summary>
        /// Null when neither value is set anywhere, which is what a molecule with nothing to keep
        /// stores rather than an object holding two nulls.
        /// </summary>
        public PeptideResults NullIfEmpty()
        {
            return Equals(EMPTY) ? null : this;
        }

        /// <summary>
        /// The molecule level counterpart of
        /// <see cref="TransitionGroupResults.ClearChromFileIds"/>. See
        /// <see cref="ChromFileIds.ClearFileIds"/>, which says why.
        /// </summary>
        public PeptideResults ClearChromFileIds()
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.ExcludeFromCalibration = ExcludeFromCalibration.ClearFileIds();
                im.AnalyteConcentrations = AnalyteConcentrations.ClearFileIds();
            });
        }

        public PeptideResults ChangeExcludeFromCalibration(ChromFileIdMap<bool> value)
        {
            return ChangeProp(ImClone(this), im => im.ExcludeFromCalibration = value.WithoutDefaultOrEmpty());
        }

        public PeptideResults ChangeAnalyteConcentrations(ChromFileIdMap<double> value)
        {
            return ChangeProp(ImClone(this), im => im.AnalyteConcentrations = value.NormalizeOrEmpty());
        }

        public PeptideResults ChangeAnalyteConcentrations(ChromFileIdMap<double?> value)
        {
            return ChangeAnalyteConcentrations(value.FromNullables());
        }

        public bool GetExcludeFromCalibration(int replicateIndex, ChromFileInfoId fileId)
        {
            return ExcludeFromCalibration?.TryGetValue(replicateIndex, fileId, out var value) == true && value;
        }

        public double? GetAnalyteConcentration(int replicateIndex, ChromFileInfoId fileId)
        {
            if (AnalyteConcentrations?.TryGetValue(replicateIndex, fileId, out var value) == true)
            {
                return value;
            }

            return null;
        }

        /// <summary>
        /// Whether the user left a replicate out of the calibration curve, asked of the replicate
        /// rather than of one of its files.
        /// <para>
        /// The entries are per file, because that is what
        /// <see cref="PeptideDocNode.PeptideChromInfoListCalculator"/> produces - one
        /// <see cref="PeptideChromInfo"/> per file. Both of these values describe the sample rather
        /// than an injection of it, though, so the callers which matter ask at the replicate level,
        /// and a replicate counts as excluded when any of its files is.
        /// </para>
        /// </summary>
        public bool AnyExcludeFromCalibration(int replicateIndex)
        {
            return GetReplicateValues(ExcludeFromCalibration, replicateIndex).Contains(true);
        }

        /// <summary>
        /// The first concentration entered for any file of a replicate, or null.
        /// See <see cref="AnyExcludeFromCalibration"/>.
        /// </summary>
        public double? GetAnalyteConcentrationForReplicate(int replicateIndex)
        {
            return GetReplicateValues(AnalyteConcentrations, replicateIndex).Cast<double?>().FirstOrDefault();
        }

        private static IEnumerable<T> GetReplicateValues<T>(ChromFileIdMap<T> map, int replicateIndex)
        {
            if (map == null || replicateIndex < 0 || replicateIndex >= map.Count)
            {
                return Array.Empty<T>();
            }

            return map.Values[replicateIndex];
        }

        /// <summary>
        /// Compared by value. See
        /// <see cref="TransitionGroupResults.Equals(TransitionGroupResults)"/>.
        /// </summary>
        protected bool Equals(PeptideResults other)
        {
            return Equals(ExcludeFromCalibration, other.ExcludeFromCalibration) &&
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
                int result = ExcludeFromCalibration?.GetHashCode() ?? 0;
                result = (result * 397) ^ (AnalyteConcentrations?.GetHashCode() ?? 0);
                return result;
            }
        }
    }
}
