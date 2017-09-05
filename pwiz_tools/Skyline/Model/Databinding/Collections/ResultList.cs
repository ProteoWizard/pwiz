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

using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Databinding.Collections
{
    public abstract class ResultList<TResult> : SkylineObjectList<ResultFileKey, TResult> where TResult : Result
    {
        protected ResultList(SkylineDataSchema dataSchema) : base(dataSchema)
        {
        }

        public ResultFileKey GetKey(TResult value)
        {
            return GetResultFileKey(value);
        }

        public ResultFileKey GetResultFileKey(Result result)
        {
            return new ResultFileKey(result.GetResultFile().Replicate.ReplicateIndex, result.GetResultFile().ChromFileInfoId, result.GetResultFile().OptimizationStep);
        }

        protected ResultFile GetResultFile(ResultFileKey key)
        {
            return new ResultFile(new Replicate(DataSchema, key.ReplicateIndex), key.ChromFileInfoId, key.OptimizationStep);
        }
    }

    public class PeptideResultList : ResultList<PeptideResult>
    {
        public PeptideResultList(Entities.Peptide peptide) : base(peptide.DataSchema)
        {
            Peptide = peptide;
        }

        public Entities.Peptide Peptide { get; private set; }
        protected override IEnumerable<ResultFileKey> ListKeys()
        {
            return Peptide.Results.Values.Select(GetKey);
        }

        protected override PeptideResult ConstructItem(ResultFileKey key)
        {
            return new PeptideResult(Peptide, GetResultFile(key));
        }
    }

    public class PrecursorResultList : ResultList<PrecursorResult>
    {
        public PrecursorResultList(Precursor precursor) : base(precursor.DataSchema)
        {
            Precursor = precursor;
        }

        public Precursor Precursor { get; private set; }
        protected override IEnumerable<ResultFileKey> ListKeys()
        {
            return Precursor.Results.Values.Select(GetKey);
        }

        protected override PrecursorResult ConstructItem(ResultFileKey key)
        {
            return new PrecursorResult(Precursor, GetResultFile(key));
        }
    }

    public class TransitionResultList : ResultList<TransitionResult>
    {
        public TransitionResultList(Entities.Transition transition) : base(transition.DataSchema)
        {
            Transition = transition;
        }
        public Entities.Transition Transition { get; private set; }
        protected override IEnumerable<ResultFileKey> ListKeys()
        {
            return Transition.Results.Values.Select(GetKey);
        }

        protected override TransitionResult ConstructItem(ResultFileKey key)
        {
            return new TransitionResult(Transition, GetResultFile(key));
        }
    }
}
