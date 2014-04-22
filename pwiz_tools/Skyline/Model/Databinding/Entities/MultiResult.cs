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

using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public abstract class MultiResult<TDocNode, TResult> : Result 
        where TDocNode : SkylineDocNode 
        where TResult : Result
    {
        protected MultiResult(SkylineDataSchema dataSchema, IEnumerable<TDocNode> docNodes, ResultFileKey resultKey) 
            : base(new SkylineDocument(dataSchema), resultKey.ToResultFile(dataSchema))
        {
            DocNodes = ImmutableList.ValueOf(docNodes);
        }

        protected IList<TDocNode> DocNodes { get; private set; }

        protected IEnumerable<TResult> ListResults()
        {
            return DocNodes.SelectMany(GetResults);
        }
        protected T GetValue<T>(Func<TResult, T> getterFunc)
        {
            T[] values = ListResults().Select(getterFunc).Distinct().ToArray();
            if (values.Length == 1)
            {
                return values[0];
            }
            return default(T);
        }

        protected void SetValue(Action<TResult> setterAction)
        {
            foreach (var result in ListResults())
            {
                setterAction(result);
            }
        }
        protected abstract IEnumerable<TResult> GetResults(TDocNode docNode);
        public override object GetAnnotation(AnnotationDef annotationDef)
        {
            return GetValue(result => result.GetAnnotation(annotationDef));
        }

        public override void SetAnnotation(AnnotationDef annotationDef, object value)
        {
            SetValue(result=>result.SetAnnotation(annotationDef, value));
        }

        public override string ToString()
        {
            return GetResultFile().ToString();
        }

        protected static IEnumerable<TResult> FindResults(IDictionary<ResultKey, TResult> results, ResultFile resultFile)
        {
            return results.Values.Where(result => MatchesWithoutOptStep(result.GetResultFile(), resultFile));
        }

        protected static bool MatchesWithoutOptStep(ResultFile resultFile1, ResultFile resultFile2)
        {
            return Equals(resultFile1.Replicate.ReplicateIndex, resultFile2.Replicate.ReplicateIndex)
                   && ReferenceEquals(resultFile1.ChromFileInfoId, resultFile2.ChromFileInfoId);
        }
        public ResultFile File { get { return GetResultFile(); }}
    }

    public class MultiPeptideResult : MultiResult<Peptide, PeptideResult>
    {
        public MultiPeptideResult(SkylineDataSchema dataSchema, IEnumerable<Peptide> peptides, ResultFileKey resultKey)
            : base(dataSchema, peptides, resultKey)
        {
            
        }

        protected override IEnumerable<PeptideResult> GetResults(Peptide docNode)
        {
            return FindResults(docNode.Results, GetResultFile());
        }
    }

    [AnnotationTarget(AnnotationDef.AnnotationTarget.precursor_result)]
    public class MultiPrecursorResult : MultiResult<Precursor, PrecursorResult>
    {
        public MultiPrecursorResult(SkylineDataSchema dataSchema, IEnumerable<Precursor> precursors, ResultFileKey resultKey)
            : base(dataSchema, precursors, resultKey)
        {
        }
        protected override IEnumerable<PrecursorResult> GetResults(Precursor docNode)
        {
            return FindResults(docNode.Results, GetResultFile());
        }
        [InvariantDisplayName("PrecursorReplicateNote")]
        public string Note
        {
            get
            {
                return GetValue(precursorResult => precursorResult.Note);
            }
            set
            {
                SetValue(precursorResult=>precursorResult.Note = value);
            }
        }
    }

    [AnnotationTarget(AnnotationDef.AnnotationTarget.transition_result)]
    public class MultiTransitionResult : MultiResult<Transition, TransitionResult>
    {
        public MultiTransitionResult(SkylineDataSchema dataSchema, IEnumerable<Transition> transitions,
            ResultFileKey resultKey) : base(dataSchema, transitions, resultKey)
        {
            
        }

        protected override IEnumerable<TransitionResult> GetResults(Transition docNode)
        {
            return FindResults(docNode.Results, GetResultFile());
        }

        [InvariantDisplayName("TransitionReplicateNote")]
        public string Note
        {
            get
            {
                return GetValue(transitionResult => transitionResult.Note);
            }
            set
            {
                SetValue(transitionResult=>transitionResult.Note = value);
            }
        }
    }
}
