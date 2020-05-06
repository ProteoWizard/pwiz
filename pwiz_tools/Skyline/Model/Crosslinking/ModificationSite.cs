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

namespace pwiz.Skyline.Model.Crosslinking
{
    public class ModificationSite : IComparable<ModificationSite>
    {
        public ModificationSite(int indexAa, string modName)
        {
            IndexAa = indexAa;
            ModName = modName;
        }

        public int IndexAa { get; private set; }
        public string ModName { get; private set; }

        protected bool Equals(ModificationSite other)
        {
            return IndexAa == other.IndexAa && ModName == other.ModName;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ModificationSite) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (IndexAa * 397) ^ (ModName != null ? ModName.GetHashCode() : 0);
            }
        }

        public int CompareTo(ModificationSite other)
        {
            if (other == null)
            {
                return 1;
            }

            int result = IndexAa.CompareTo(other.IndexAa);
            if (result == 0)
            {
                result = StringComparer.Ordinal.Compare(ModName, other.ModName);
            }

            return result;
        }

        public override string ToString()
        {
            return (IndexAa + 1) + @":" + ModName;
        }
    }
}
