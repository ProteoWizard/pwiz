/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Globalization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public sealed class IsotopeLabelType : IComparable
    {
        // ReSharper disable InconsistentNaming 
        public const string LIGHT_NAME = "light"; // Not L10N
        public const string HEAVY_NAME = "heavy"; // Not L10N
        public const string NONE_NAME = "none"; // Not L10N

        public static readonly IsotopeLabelType light = new IsotopeLabelType(LIGHT_NAME, 0);
        // Default heavy label for testing
        public static readonly IsotopeLabelType heavy = new IsotopeLabelType(HEAVY_NAME, 1);
        // ReSharper restore InconsistentNaming

        public static int FirstHeavy { get { return light.SortOrder + 1; } }

        public IsotopeLabelType(string name, int sortOrder)
        {
            Name = name;
            SortOrder = sortOrder;
        }

        // NHibernate constructor
// ReSharper disable UnusedMember.Local
        private IsotopeLabelType()
        {            
        }
// ReSharper restore UnusedMember.Local

        public string Name { get; private set; }
        public int SortOrder { get; private set; }

        public bool IsLight { get { return ReferenceEquals(this, light); } }

        public string Title
        {
            get
            {
                if (char.IsUpper(Name[0]))
                    return Name;

                return Name[0].ToString(CultureInfo.InvariantCulture).ToUpperInvariant() +
                    (Name.Length > 1 ? Name.Substring(1) : string.Empty);
            }
        }

        public string Id
        {
            get { return Helpers.MakeId(Name); }
        }

        public int CompareTo(object obj)
        {
            return SortOrder - ((IsotopeLabelType) obj).SortOrder;
        }

        #region object overrides

        public bool Equals(IsotopeLabelType other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Name, Name) && other.SortOrder == SortOrder;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (IsotopeLabelType)) return false;
            return Equals((IsotopeLabelType) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Name.GetHashCode()*397) ^ SortOrder;
            }
        }

        /// <summary>
        /// Label type combo box in peptide settings Modifications tab depends on this
        /// </summary>
        public override string ToString()
        {
            return Name;
        }

        #endregion
    }
}