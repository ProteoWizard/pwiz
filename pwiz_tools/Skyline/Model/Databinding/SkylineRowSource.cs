/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.ComponentModel;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Databinding
{
    public abstract class SkylineRowSource
    {
        protected SkylineRowSource(string caption)
        {
            Caption = caption;
        }
        public override string ToString()
        {
            return Caption;
        }

        public string Caption { get; private set; }
        public abstract IBindingList GetList(SkylineDataSchema dataSchema);

        public static SkylineRowSource<T> MakeRowSource<T>(string caption,
                                                           Func<SkylineDataSchema, NodeList<T>> makeNodeList)
            where T : SkylineDocNode
        {
            return new SkylineRowSource<T>(caption, makeNodeList);
        }

        public static readonly SkylineRowSource<Protein> Proteins
            = MakeRowSource("Proteins", schema=>new Proteins(schema)); // Not L10N
        public static readonly SkylineRowSource<Entities.Peptide> Peptides 
            = MakeRowSource("Peptides", schema => new Peptides(schema, new[]{IdentityPath.ROOT})); // Not L10N
        public static readonly SkylineRowSource<Precursor> Precursors
            = MakeRowSource("Precursors", schema => new Precursors(schema, new[] { IdentityPath.ROOT })); // Not L10N
        public static readonly SkylineRowSource<Entities.Transition> Transitions 
            = MakeRowSource("Transitions", schema => new Transitions(schema, new[] {IdentityPath.ROOT})); // Not L10N

        public static readonly IList<SkylineRowSource> RowSources =
            ImmutableList.ValueOf(new SkylineRowSource[] {Proteins, Peptides, Precursors, Transitions});
    }

    public class SkylineRowSource<T> : SkylineRowSource where T : SkylineDocNode
    {
        private readonly Func<SkylineDataSchema, NodeList<T>> _getListFunc;
        public SkylineRowSource(string caption, Func<SkylineDataSchema, NodeList<T>> getListFunc) : base(caption)
        {
            _getListFunc = getListFunc;
        }

        public override IBindingList GetList(SkylineDataSchema dataSchema)
        {
            return GetNodeList(dataSchema);
        }
        public NodeList<T> GetNodeList(SkylineDataSchema dataSchema)
        {
            return _getListFunc(dataSchema);
        }
    }
}
