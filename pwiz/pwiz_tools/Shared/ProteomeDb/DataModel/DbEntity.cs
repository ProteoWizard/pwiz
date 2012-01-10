/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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

namespace pwiz.ProteomeDatabase.DataModel
{
    public class DbEntity<T> where T : DbEntity<T>, new()
    {
        public virtual long? Id { get; set; }
        public virtual int Version { get; set; }
        public override bool Equals(Object o)
        {
            if (Id == null)
            {
// ReSharper disable BaseObjectEqualsIsObjectEquals
                return base.Equals(o);
// ReSharper restore BaseObjectEqualsIsObjectEquals
            }
            DbEntity<T> that = o as DbEntity<T>;
            if (that == null)
            {
                return false;
            }
            return Equals(Id, that.Id);
        }
        public override int GetHashCode()
        {
            if (Id == null)
            {
// ReSharper disable BaseObjectGetHashCodeCallInGetHashCode
                return base.GetHashCode();
// ReSharper restore BaseObjectGetHashCodeCallInGetHashCode
            }
            return Id.GetHashCode() ^ typeof (T).GetHashCode();
        }
    }
}