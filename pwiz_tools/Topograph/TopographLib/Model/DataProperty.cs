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
using System.Collections.Generic;

namespace pwiz.Topograph.Model
{
    public abstract class DataProperty<TData>
    {
        public abstract TData Merge(TData current, TData mine, TData original, TData theirs);
        public static TData MergeAll(IEnumerable<DataProperty<TData>> properties, TData mine, TData original, TData theirs)
        {
            var current = theirs;
            foreach (var property in properties)
            {
                current = property.Merge(current, mine, original, theirs);
            }
            return current;
        }
    }

    public class DataProperty<TData, TValue> : DataProperty<TData>
    {
        public delegate TValue Getter(TData data);
        public delegate TData Setter(TData data, TValue value);
        private readonly Getter _getter;
        private readonly Setter _setter;
        public DataProperty(Getter getter, Setter setter)
        {
            _getter = getter;
            _setter = setter;
        }

        public TValue Get(TData data)
        {
            return _getter(data);
        }
        public TData Set(TData data, TValue value)
        {
            return _setter(data, value);
        }
        public override TData Merge(TData current, TData mine, TData original, TData theirs)
        {
            if (Equals(Get(mine), Get(original)))
            {
                return Set(current, Get(theirs));
            }
            return Set(current, Get(mine));
        }
    }
}