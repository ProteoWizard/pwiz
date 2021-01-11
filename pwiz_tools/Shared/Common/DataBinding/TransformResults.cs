/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding.Layout;

namespace pwiz.Common.DataBinding
{
    internal class TransformResults
    {
        public static readonly TransformResults EMPTY = new TransformResults {PivotedRows = ReportResults.EMPTY};
        public TransformResults(TransformResults parent, IRowTransform transform, ReportResults pivotedRows)
        {
            Parent = parent ?? EMPTY;
            RowTransform = transform;
            PivotedRows = pivotedRows;
            Depth = Parent.Depth + 1;
        }

        private TransformResults()
        {
        }

        public TransformResults Parent { get; private set; }
        public IRowTransform RowTransform { get; private set; }
        public ReportResults PivotedRows { get; private set; }
        public int Depth { get; private set; }
        public bool IsEmpty { get { return Depth == 0; } }

        public TransformResults GetPartialResults(TransformStack transformStack)
        {
            if (Parent == null)
            {
                return this;
            }
            var parentResults = Parent.GetPartialResults(transformStack);
            if (!ReferenceEquals(parentResults, Parent))
            {
                return parentResults;
            }
            int transformIndex = Depth - 1;
            if (transformIndex >= transformStack.RowTransforms.Count ||
                !Equals(RowTransform, transformStack.RowTransforms[transformIndex]))
            {
                return parentResults;
            }
            return this;
        }
    }
}
