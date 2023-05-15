/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
 */using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    
    public class PrecursorKey : Immutable
    {
        public static readonly PrecursorKey EMPTY = new PrecursorKey(Adduct.EMPTY);
        public PrecursorKey(Adduct adduct) : this(adduct, null)
        {
        }
        public PrecursorKey(Adduct adduct, SpectrumClassFilterClause spectrumClassFilter)
        {
            Adduct = adduct.Unlabeled;
            SpectrumClassFilter = spectrumClassFilter;
        }

        public Adduct Adduct { get; private set; }

        public SpectrumClassFilterClause SpectrumClassFilter { get; private set; }

        public PrecursorKey ChangeSpectrumClassFilter(SpectrumClassFilterClause spectrumClassFilter)
        {
            return ChangeProp(ImClone(this), im => im.SpectrumClassFilter = spectrumClassFilter);
        }

        protected bool Equals(PrecursorKey other)
        {
            return Equals(Adduct, other.Adduct) && Equals(SpectrumClassFilter, other.SpectrumClassFilter);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PrecursorKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Adduct.GetHashCode() * 397) ^
                       (SpectrumClassFilter != null ? SpectrumClassFilter.GetHashCode() : 0);
            }
        }

        public PrecursorKey Unlabeled
        {
            get
            {
                return ChangeProp(ImClone(this), im => im.Adduct = im.Adduct.Unlabeled);
            }
        }
    }
}
