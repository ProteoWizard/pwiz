/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using Type = System.Type;

namespace pwiz.Skyline.Model.Lib.BlibData
{
    public class DbRefSpectraPeakAnnotations : DbEntity
    {
        public override Type EntityClass
        {
            get { return typeof(DbRefSpectraPeakAnnotations); }
        }
        public virtual DbRefSpectra RefSpectra { get; set; }
        public virtual int PeakIndex { get; set; }
        public virtual string Name { get; set; } 
        public virtual string Formula { get; set; } 
        public virtual string InchiKey { get; set; } 
        public virtual string OtherKeys { get; set; } 
        public virtual int Charge { get; set; } 
        public virtual string Adduct { get; set; } 
        public virtual string Comment { get; set; } 
        public virtual double mzTheoretical { get; set; }
        public virtual double mzObserved { get; set; }

        public static ICollection<DbRefSpectraPeakAnnotations> Create(DbRefSpectra refSpectra, SpectrumPeaksInfo peaks)
        {
            List<DbRefSpectraPeakAnnotations> resultList = null;
            // Each peak may have more than one annotation
            if (peaks.Peaks.Any(p => p.Annotations != null && p.Annotations.Any(a => !SpectrumPeakAnnotation.IsNullOrEmpty(a))))
            {
                resultList = new List<DbRefSpectraPeakAnnotations>();
                var i = 0;
                foreach (var peak in peaks.Peaks)
                {
                    if (peak.Annotations != null)
                    {
                        foreach(var annotation in peak.Annotations.Where(a => !SpectrumPeakAnnotation.IsNullOrEmpty(a)))
                        {
                            var result = new DbRefSpectraPeakAnnotations
                            {
                                RefSpectra = refSpectra,
                                PeakIndex = i,
                                Name = annotation.Ion.Name,
                                Formula = annotation.Ion.NeutralFormula,
                                InchiKey = annotation.Ion.AccessionNumbers.GetInChiKey(),
                                OtherKeys = annotation.Ion.AccessionNumbers.GetNonInChiKeys(),
                                Adduct = annotation.Ion.Adduct.ToString(),
                                Charge = annotation.Ion.Adduct.AdductCharge,
                                Comment = annotation.Comment,
                                mzTheoretical = annotation.Ion.MonoisotopicMassMz,
                                mzObserved = peak.Mz
                            };
                            resultList.Add(result);
                        }
                    }
                    i++;
                }
            }
            return resultList;
        }

        public override string ToString()
        {
            return (Name ?? @"(null)") + @" #" + PeakIndex; // For debugging convenience
        }
    }

    // CONSIDER(bspratt) : this pattern where we have largely identical class definitions for redundant vs nr seems clunky and hard to maintain
    public class DbRefSpectraPeakAnnotationsRedundant : DbEntity
    {
        public override Type EntityClass
        {
            get { return typeof(DbRefSpectraPeakAnnotationsRedundant); }
        }
        public virtual DbRefSpectraRedundant RefSpectra { get; set; }
        public virtual int PeakIndex { get; set; }
        public virtual string Name { get; set; }
        public virtual string Formula { get; set; }
        public virtual string InchiKey { get; set; }
        public virtual string OtherKeys { get; set; }
        public virtual int Charge { get; set; }
        public virtual string Adduct { get; set; }
        public virtual string Comment { get; set; }
        public virtual double mzTheoretical { get; set; }
        public virtual double mzObserved { get; set; }
    }
}