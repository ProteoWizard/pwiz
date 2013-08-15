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
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model.Data
{
    public class TracerDefData
    {
        public TracerDefData(DbTracerDef dbTracerDef)
        {
            Name = dbTracerDef.Name;
            TracerSymbol = dbTracerDef.TracerSymbol;
            DeltaMass = dbTracerDef.DeltaMass;
            AtomCount = dbTracerDef.AtomCount;
            AtomPercentEnrichment = dbTracerDef.AtomPercentEnrichment;
            InitialEnrichment = dbTracerDef.InitialEnrichment;
            FinalEnrichment = dbTracerDef.FinalEnrichment;
            IsotopesEluteEarlier = dbTracerDef.IsotopesEluteEarlier;
            IsotopesEluteLater = dbTracerDef.IsotopesEluteLater;
        }
        public TracerDefData(TracerDefData tracerDefData)
        {
            Name = tracerDefData.Name;
            TracerSymbol = tracerDefData.TracerSymbol;
            DeltaMass = tracerDefData.DeltaMass;
            AtomCount = tracerDefData.AtomCount;
            AtomPercentEnrichment = tracerDefData.AtomPercentEnrichment;
            InitialEnrichment = tracerDefData.InitialEnrichment;
            FinalEnrichment = tracerDefData.FinalEnrichment;
            IsotopesEluteEarlier = tracerDefData.IsotopesEluteEarlier;
            IsotopesEluteLater = tracerDefData.IsotopesEluteLater;
        }

        public string Name { get; private set; }
        public string TracerSymbol { get; private set; }
        public double DeltaMass { get; private set; }
        public int AtomCount { get; private set; }
        public double AtomPercentEnrichment { get; private set; }
        public double InitialEnrichment { get; private set; }
        public double FinalEnrichment { get; private set; }
        public bool IsotopesEluteEarlier { get; private set; }
        public bool IsotopesEluteLater { get; private set; }

        protected bool Equals(TracerDefData other)
        {
            return string.Equals(TracerSymbol, other.TracerSymbol) 
                && string.Equals(Name, other.Name) 
                && DeltaMass.Equals(other.DeltaMass) 
                && AtomCount == other.AtomCount 
                && AtomPercentEnrichment.Equals(other.AtomPercentEnrichment) 
                && InitialEnrichment.Equals(other.InitialEnrichment) 
                && FinalEnrichment.Equals(other.FinalEnrichment) 
                && IsotopesEluteEarlier.Equals(other.IsotopesEluteEarlier) 
                && IsotopesEluteLater.Equals(other.IsotopesEluteLater);
        }

        public bool EqualMasses(TracerDefData other)
        {
            return TracerSymbol.Equals(other.TracerSymbol)
                   && DeltaMass.Equals(other.DeltaMass)
                   && AtomCount.Equals(other.AtomCount)
                   && AtomPercentEnrichment.Equals(other.AtomPercentEnrichment);
        }
        public bool EqualPeakPicking(TracerDefData other)
        {
            return EqualMasses(other)
                   && IsotopesEluteEarlier.Equals(other.IsotopesEluteEarlier)
                   && IsotopesEluteLater.Equals(other.IsotopesEluteLater);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((TracerDefData) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (TracerSymbol != null ? TracerSymbol.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ DeltaMass.GetHashCode();
                hashCode = (hashCode*397) ^ AtomCount;
                hashCode = (hashCode*397) ^ AtomPercentEnrichment.GetHashCode();
                hashCode = (hashCode*397) ^ InitialEnrichment.GetHashCode();
                hashCode = (hashCode*397) ^ FinalEnrichment.GetHashCode();
                hashCode = (hashCode*397) ^ IsotopesEluteEarlier.GetHashCode();
                hashCode = (hashCode*397) ^ IsotopesEluteLater.GetHashCode();
                return hashCode;
            }
        }
    }
}
