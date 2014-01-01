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
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Databinding.Collections
{
    public abstract class MultiResultList<TMultiResult, TDocNode, TResult> : ResultList<TMultiResult> 
        where TResult : Result 
        where TDocNode : SkylineDocNode 
        where TMultiResult : MultiResult<TDocNode, TResult>
    {
        protected MultiResultList(SkylineDataSchema dataSchema, IEnumerable<TDocNode> docNodes) : base(dataSchema)
        {
            DocNodes = ImmutableList.ValueOf(docNodes);
        }

        public IList<TDocNode> DocNodes { get; private set; }
    }
    public class MultiTransitionResultList : MultiResultList<MultiTransitionResult, Entities.Transition, TransitionResult>
    {
        public MultiTransitionResultList(SkylineDataSchema dataSchema, IEnumerable<Entities.Transition> transitions)
            : base(dataSchema, transitions)
        {
            OnDocumentChanged();
        }

        protected override IList<ResultFileKey> ListKeys()
        {
            return DocNodes.SelectMany(transition => transition.Results.Values.Select(GetResultFileKey)).Distinct().ToArray();
        }

        protected override MultiTransitionResult ConstructItem(ResultFileKey key)
        {
            return new MultiTransitionResult(DataSchema, DocNodes, key);
        }

        public override IList<MultiTransitionResult> DeepClone()
        {
            var dataSchema = DataSchema.Clone();
            return new MultiTransitionResultList(dataSchema, DocNodes.Select(transition => new Entities.Transition(dataSchema, transition.IdentityPath)));
        }
    }

    public class MultiPrecursorResultList : MultiResultList<MultiPrecursorResult, Precursor, PrecursorResult>
    {
        public MultiPrecursorResultList(SkylineDataSchema dataSchema, IEnumerable<Precursor> precursors)
            : base(dataSchema, precursors)
        {
            OnDocumentChanged();
        }
        protected override IList<ResultFileKey> ListKeys()
        {
            return DocNodes.SelectMany(precursor => precursor.Results.Values.Select(GetResultFileKey)).Distinct().ToArray();
        }
        protected override MultiPrecursorResult ConstructItem(ResultFileKey key)
        {
            return new MultiPrecursorResult(DataSchema, DocNodes, key);
        }
        public override IList<MultiPrecursorResult> DeepClone()
        {
            var dataSchema = DataSchema.Clone();
            return new MultiPrecursorResultList(dataSchema, DocNodes.Select(precursor=>new Precursor(dataSchema, precursor.IdentityPath)));
        }
    }
}