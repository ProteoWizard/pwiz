/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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

using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Databinding.Collections
{
    public class ResultFileKey
    {
        public ResultFileKey(int replicateIndex, ChromFileInfoId chromFileInfoId, int optimizationStep)
        {
            ReplicateIndex = replicateIndex;
            ChromFileInfoId = chromFileInfoId;
            OptimizationStep = optimizationStep;
        }

        public int ReplicateIndex { get; private set; }
        public ChromFileInfoId ChromFileInfoId { get; private set; }
        public int OptimizationStep { get; private set; }

        public ResultFile ToResultFile(SkylineDataSchema dataSchema)
        {
            return new ResultFile(new Replicate(dataSchema, ReplicateIndex), ChromFileInfoId, OptimizationStep);
        }

        protected bool Equals(ResultFileKey other)
        {
            return ReplicateIndex == other.ReplicateIndex && ReferenceEquals(ChromFileInfoId, other.ChromFileInfoId) && OptimizationStep == other.OptimizationStep;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ResultFileKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = ReplicateIndex;
                hashCode = (hashCode*397) ^ ChromFileInfoId.GetHashCode();
                hashCode = (hashCode*397) ^ OptimizationStep;
                return hashCode;
            }
        }
    }
}
