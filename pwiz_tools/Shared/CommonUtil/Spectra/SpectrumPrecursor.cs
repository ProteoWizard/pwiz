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
 */
using System;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Spectra
{
    public class SpectrumPrecursor : Immutable
    {
        public SpectrumPrecursor(SignedMz precursorMz)
        {
            PrecursorMz = precursorMz;
        }

        public SignedMz PrecursorMz { get; }
        public double? CollisionEnergy { get; private set; }
        public double? IsolationWindowLowerWidth { get; private set; }
        public double? IsolationWindowUpperWidth { get; private set; }
        
        public SpectrumPrecursor ChangeCollisionEnergy(double? collisionEnergy)
        {
            return ChangeProp(ImClone(this), im => im.CollisionEnergy = collisionEnergy);
        }

        public SpectrumPrecursor ChangeIsolationWindowWidth(double lowerWindowWidth, double upperWindowWidth)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.IsolationWindowLowerWidth = lowerWindowWidth;
                im.IsolationWindowUpperWidth = upperWindowWidth;
            });
        }

        protected bool Equals(SpectrumPrecursor other)
        {
            return PrecursorMz.Equals(other.PrecursorMz) && Nullable.Equals(CollisionEnergy, other.CollisionEnergy) &&
                   Nullable.Equals(IsolationWindowLowerWidth, other.IsolationWindowLowerWidth) &&
                   Nullable.Equals(IsolationWindowUpperWidth, other.IsolationWindowUpperWidth);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SpectrumPrecursor)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = PrecursorMz.GetHashCode();
                hashCode = (hashCode * 397) ^ CollisionEnergy.GetHashCode();
                hashCode = (hashCode * 397) ^ IsolationWindowLowerWidth.GetHashCode();
                hashCode = (hashCode * 397) ^ IsolationWindowUpperWidth.GetHashCode();
                return hashCode;
            }
        }
    }
}
