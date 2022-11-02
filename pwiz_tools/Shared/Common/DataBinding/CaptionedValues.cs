/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Collections;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding
{
    public class CaptionedValues
    {
        public CaptionedValues(IColumnCaption caption, Type valueType, IEnumerable values)
        {
            Caption = caption;
            ValueType = valueType;
            Values = ImmutableList.ValueOf(values.Cast<object>());
        }

        public IColumnCaption Caption { get; private set; }

        public Type ValueType { get; private set; }
        public ImmutableList<object> Values { get; private set; }

        public int ValueCount {get{ return Values.Count; }}
    }
}
