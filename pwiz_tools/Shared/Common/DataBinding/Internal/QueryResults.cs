/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.ComponentModel;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Internal
{
    internal class QueryResults : Immutable
    {
        public static readonly QueryResults Empty = new QueryResults();
        private QueryResults()
        {
            Parameters = QueryParameters.Empty;
            TransformResults = TransformResults.EMPTY;
        }

        public QueryParameters Parameters { get; private set; }
        public QueryResults SetParameters(QueryParameters newParameters)
        {
            var result = new QueryResults
                             {
                                 Parameters = newParameters,
                                 TransformResults = TransformResults.EMPTY
                             };
            return result;
        }

        public ImmutableList<DataPropertyDescriptor> ItemProperties
        {
            get { return (TransformResults ?? TransformResults.EMPTY).PivotedRows.ItemProperties; }
        }

        public IList<RowItem> SourceRows { get; private set; }
        public QueryResults SetSourceRows(IEnumerable<RowItem> sourceRows)
        {
            if (ReferenceEquals(sourceRows, SourceRows))
            {
                return this;
            }
            return ChangeProp(ImClone(this), im =>
            {
                im.SourceRows = ImmutableList.ValueOf(sourceRows);
                im.TransformResults = TransformResults.EMPTY;
            });
        }

        public TransformResults TransformResults { get; private set; }

        public QueryResults ChangeTransformResults(TransformResults partialResults)
        {
            TransformResults transformResults = partialResults;
            while (transformResults != null && transformResults.Depth > Parameters.TransformStack.ResultDepth)
            {
                transformResults = transformResults.Parent;
            }
            return ChangeProp(ImClone(this), im =>
            {
                im.TransformResults = transformResults ?? TransformResults.EMPTY;
            });
        }

        public IList<RowItem> ResultRows
        {
            get
            {
                return TransformResults.PivotedRows.RowItems;
            }
        }
    }

    internal class QueryParameters
    {
        public static readonly QueryParameters Empty = new QueryParameters();
        private QueryParameters()
        {
            ViewInfo = null;
            TransformStack = TransformStack.EMPTY;
        }
        public QueryParameters(ViewInfo viewInfo, TransformStack rowTransform, ListSortDescriptionCollection sortDescriptions)
        {
            ViewInfo = viewInfo;
            TransformStack = rowTransform;
        }
        public QueryParameters(QueryParameters that)
        {
            ViewInfo = that.ViewInfo;
            TransformStack = that.TransformStack;
        }
        public ViewInfo ViewInfo { get; private set;}
        public QueryParameters SetViewInfo(ViewInfo value)
        {
            return new QueryParameters(this) {ViewInfo = value};
        }
        public TransformStack TransformStack { get; private set; }
        public QueryParameters SetTransformStack(TransformStack value)
        {
            return new QueryParameters(this) { TransformStack = value };
        }
        public bool QueryValid(QueryParameters that)
        {
            return ReferenceEquals(ViewInfo, that.ViewInfo);
        }
        public bool FilterValid(QueryParameters that)
        {
            return QueryValid(that) && Equals(TransformStack, that.TransformStack);
        }
    }
}