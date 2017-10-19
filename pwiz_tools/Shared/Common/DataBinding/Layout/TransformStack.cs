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
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Layout
{
    public class TransformStack : Immutable
    {
        public static readonly TransformStack EMPTY = new TransformStack(ImmutableList.Empty<IRowTransform>(), 0);
        public TransformStack(IEnumerable<IRowTransform> rowTransforms, int stackIndex)
        {
            RowTransforms = ImmutableList.ValueOf(rowTransforms);
            StackIndex = stackIndex;
        }

        public ImmutableList<IRowTransform> RowTransforms { get; private set; }

        public IRowTransform CurrentTransform
        {
            get
            {
                if (StackIndex >= RowTransforms.Count)
                {
                    return null;
                }
                return RowTransforms[StackIndex];
            }
        }

        public int StackIndex { get; private set; }

        /// <summary>
        /// Returns the depth of <see cref="TransformResults"/> that would be created by applying 
        /// this TransformStack.
        /// </summary>
        public int ResultDepth { get { return RowTransforms.Count - StackIndex + 1; } }

        public TransformStack ChangeStackIndex(int newValue)
        {
            return new TransformStack(RowTransforms, newValue);
        }

        public TransformStack PushTransform(IRowTransform rowTransform)
        {
            return new TransformStack(new []{rowTransform}.Concat(RowTransforms.Skip(StackIndex))
                .Where(transform=>!transform.IsEmpty), 0);
        }

        public TransformStack TrimTop()
        {
            if (StackIndex == 0)
            {
                return this;
            }
            return new TransformStack(RowTransforms.Skip(StackIndex), 0);
        }

        public bool IsEmpty
        {
            get { return RowTransforms.Count == 0; }
        }

        public TransformStack Predecessor
        {
            get
            {
                if (StackIndex >= RowTransforms.Count)
                {
                    return EMPTY;
                }
                return ChangeStackIndex(StackIndex + 1);
            }
        }

        protected bool Equals(TransformStack other)
        {
            return Equals(RowTransforms, other.RowTransforms) && StackIndex == other.StackIndex;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((TransformStack) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((RowTransforms != null ? RowTransforms.GetHashCode() : 0) * 397) ^ StackIndex;
            }
        }
    }
}
