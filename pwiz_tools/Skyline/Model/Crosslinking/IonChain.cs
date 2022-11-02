/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Crosslinking
{
    /// <summary>
    /// A list of fragment ions crosslinked together.
    /// </summary>
    public class IonChain : IReadOnlyList<IonOrdinal>
    {
        public IonChain(IEnumerable<IonOrdinal> parts)
        {
            Ions = ImmutableList.ValueOfOrEmpty(parts);
        }

        public static IonChain FromIons(IEnumerable<IonOrdinal> ions)
        {
            return ions as IonChain ?? new IonChain(ions);
        }

        public static IonChain FromIons(params IonOrdinal[] ions)
        {
            return new IonChain(ions);
        }

        public ImmutableList<IonOrdinal> Ions { get; private set; }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<IonOrdinal> GetEnumerator()
        {
            return Ions.GetEnumerator();
        }

        public int Count => Ions.Count;

        public IonOrdinal this[int index] => Ions[index];

        public IEnumerable<IonType> IonTypes
        {
            get
            {
                return Ions.Select(part => part.Type).OfType<IonType>();
            }
        }

        public override string ToString()
        {
            return string.Join(@"-", Ions);
        }

        protected bool Equals(IonChain other)
        {
            return Ions.Equals(other.Ions);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((IonChain) obj);
        }

        public override int GetHashCode()
        {
            return Ions.GetHashCode();
        }

        public bool IsEmpty
        {
            get
            {
                return Ions.All(ion => ion.IsEmpty);
            }
        }

        public bool IsPrecursor
        {
            get
            {
                return Ions.All(ion => ion.Type == IonType.precursor);
            }
        }
    }
}
