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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model.Data
{
    public class PeptideAnalysisData
    {
        public PeptideAnalysisData(DbPeptideAnalysis dbPeptideAnalysis)
        {
            Name = dbPeptideAnalysis.Name;
            PeptideId = dbPeptideAnalysis.Peptide.GetId();
            MinCharge = dbPeptideAnalysis.MinCharge;
            MaxCharge = dbPeptideAnalysis.MaxCharge;
            ExcludedMasses = ExcludedMasses.FromByteArray(dbPeptideAnalysis.ExcludedMasses);
            MassAccuracy = dbPeptideAnalysis.MassAccuracy;
            FileAnalyses = ImmutableSortedList<long, PeptideFileAnalysisData>.EMPTY;
        }

        public PeptideAnalysisData(PeptideAnalysisData peptideAnalysisData)
        {
            Name = peptideAnalysisData.Name;
            PeptideId = peptideAnalysisData.PeptideId;
            MinCharge = peptideAnalysisData.MinCharge;
            MaxCharge = peptideAnalysisData.MaxCharge;
            ExcludedMasses = peptideAnalysisData.ExcludedMasses;
            MassAccuracy = peptideAnalysisData.MassAccuracy;
            FileAnalyses = peptideAnalysisData.FileAnalyses;
            ChromatogramsWereLoaded = peptideAnalysisData.ChromatogramsWereLoaded;

        }

        public string Name { get; private set; }
        public long PeptideId { get; private set; }
        public int MinCharge { get; private set; }
        public PeptideAnalysisData SetMinCharge(int value)
        {
            return new PeptideAnalysisData(this) {MinCharge = value};
        }
        public int MaxCharge { get; private set; }
        public PeptideAnalysisData SetMaxCharge(int value)
        {
            return new PeptideAnalysisData(this) {MaxCharge = value};
        }

        public ExcludedMasses ExcludedMasses { get; private set; }
        public PeptideAnalysisData SetExcludedMasses(ExcludedMasses value)
        {
            return new PeptideAnalysisData(this) {ExcludedMasses = value};
        }
        public double? MassAccuracy { get; private set; }
        public PeptideAnalysisData SetMassAccuracy(double? value)
        {
            return new PeptideAnalysisData(this) {MassAccuracy = value};
        }
        public ImmutableSortedList<long, PeptideFileAnalysisData> FileAnalyses { get; private set; }
        public PeptideAnalysisData SetFileAnalyses(ImmutableSortedList<long, PeptideFileAnalysisData> value)
        {
            return new PeptideAnalysisData(this) {FileAnalyses = value};
        }
        public bool ChromatogramsWereLoaded { get; private set; }
        public PeptideAnalysisData SetFileAnalyses(ImmutableSortedList<long, PeptideFileAnalysisData> fileAnalyses, bool chromatogramsWereLoaded)
        {
            return new PeptideAnalysisData(this)
                       {
                           ChromatogramsWereLoaded = chromatogramsWereLoaded,
                           FileAnalyses = fileAnalyses,
                       };
        }
        public PeptideAnalysisData UnloadChromatograms()
        {
            var fileAnalyses = FileAnalyses.Select(entry => new KeyValuePair<long, PeptideFileAnalysisData>(
                entry.Key, entry.Value.UnloadChromatograms()));
            return new PeptideAnalysisData(this)
                       {
                           ChromatogramsWereLoaded = false,
                           FileAnalyses = ImmutableSortedList.FromValues(fileAnalyses),
                       };
        }
        
        protected bool Equals(PeptideAnalysisData other)
        {
            return string.Equals(Name, other.Name)
                && PeptideId == other.PeptideId
                && MinCharge == other.MinCharge
                && Equals(ExcludedMasses, other.ExcludedMasses)
                && MaxCharge == other.MaxCharge
                && MassAccuracy.Equals(other.MassAccuracy) 
                && Equals(FileAnalyses, other.FileAnalyses)
                && Equals(ChromatogramsWereLoaded, other.ChromatogramsWereLoaded);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PeptideAnalysisData) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ PeptideId.GetHashCode();
                hashCode = (hashCode*397) ^ MinCharge;
                hashCode = (hashCode*397) ^ (ExcludedMasses != null ? ExcludedMasses.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ MaxCharge;
                hashCode = (hashCode * 397) ^ MassAccuracy.GetHashCode();
                hashCode = (hashCode * 397) ^ (FileAnalyses != null ? FileAnalyses.GetHashCode() : 0);
                return hashCode;
            }
        }

        public bool CheckDirty(PeptideAnalysisData savedData)
        {
            if (!string.Equals(Name, savedData.Name)
                || PeptideId != savedData.PeptideId
                || MinCharge != savedData.MinCharge
                || MaxCharge != savedData.MaxCharge
                || !Equals(ExcludedMasses, savedData.ExcludedMasses)
                || !MassAccuracy.Equals(savedData.MassAccuracy))
            {
                return true;
            }
            if (!Equals(FileAnalyses.Keys, savedData.FileAnalyses.Keys))
            {
                return true;
            }
            for (int i = 0; i < FileAnalyses.Count; i++)
            {
                if (FileAnalyses.Values[i].CheckDirty(savedData.FileAnalyses.Values[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public bool CheckRecalculatePeaks(PeptideAnalysisData savedData)
        {
            if (Equals(this, savedData))
            {
                return false;
            }
            if (!Equals(MinCharge, savedData.MinCharge)
                || !Equals(MaxCharge, savedData.MaxCharge)
                || !Equals(ExcludedMasses, savedData.ExcludedMasses)
                || !Equals(MassAccuracy, savedData.MassAccuracy))
            {
                return true;
            }
            if (!Equals(FileAnalyses.Keys, savedData.FileAnalyses.Keys))
            {
                return true;
            }
            
            for (int i = 0; i < FileAnalyses.Count; i++)
            {
                if (FileAnalyses.Values[i].CheckRecalculatePeaks(savedData.FileAnalyses.Values[i]))
                {
                    return true;
                }
            }
            return false;
            
        }

        public static readonly IList<DataProperty<PeptideAnalysisData>> MergeableProperties = ImmutableList.ValueOf(
            new DataProperty<PeptideAnalysisData>[]
                {
                    new DataProperty<PeptideAnalysisData, double?>(data=>data.MassAccuracy, (data, value)=>data.SetMassAccuracy(value)),
                    new DataProperty<PeptideAnalysisData, int>(data=>data.MinCharge, (data, value)=>data.SetMinCharge(value)),
                    new DataProperty<PeptideAnalysisData, int>(data=>data.MaxCharge, (data, value)=>data.SetMaxCharge(value)),
                    new DataProperty<PeptideAnalysisData, ExcludedMasses>(data=>data.ExcludedMasses, (data, value)=>data.SetExcludedMasses(value))
                });
    }
}
