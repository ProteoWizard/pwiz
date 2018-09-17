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

using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    /// <summary>
    /// Class for structured peak annotations as used in spectral libraries
    /// Contains a freetext comment, and a custom ion for holding things like mz,
    /// adduct (for tracking neutral loss, among other things), molecule name
    /// and CAS/HMDB/InChiKey etc
    /// Note that a peak in a spectral library may have no annotation, or several
    /// </summary>
    public sealed class SpectrumPeakAnnotation : IEquatable<SpectrumPeakAnnotation>
    {
        public static SpectrumPeakAnnotation EMPTY = new SpectrumPeakAnnotation();

        public CustomIon Ion { get; private set; }

        public string Comment { get; private set; }

        private SpectrumPeakAnnotation()
        {
            Ion = CustomIon.EMPTY;
            Comment = string.Empty;
        }

        private SpectrumPeakAnnotation(CustomIon ion, string comment)
        {
            Ion = ion ?? CustomIon.EMPTY;
            Comment = comment ?? string.Empty;
            Assume.IsFalse(IsNullOrEmpty(this), @"empty peak annotation"); // You should be using Create() if there's any risk of creating empty objects
        }

        public static SpectrumPeakAnnotation Create(CustomIon ion, string comment)
        {
            return ion.IsEmpty && string.IsNullOrEmpty(comment) ? 
                EMPTY : 
                new SpectrumPeakAnnotation(ion, comment);
        }

        public static SpectrumPeakAnnotation Create(SmallMoleculeLibraryAttributes mol, Adduct adduct, string comment, double? mzTheoretical)
        {
            double? massTheoretical = mzTheoretical.HasValue ? adduct.MassFromMz(mzTheoretical.Value, MassType.Monoisotopic).Value : (double?)null;
            var ion = new CustomIon(mol, adduct, massTheoretical);
            if ((mzTheoretical ?? 0.0) > 0)
            {
                if (Equals(ion.MonoisotopicMassMz, 0.0))
                {
                    // We didn't have enough info to calculate mz, use the provided theoretical value
                    var massMono = adduct.MassFromMz(mzTheoretical.Value, MassType.Monoisotopic);
                    var massAverage = adduct.MassFromMz(mzTheoretical.Value, MassType.Average);
                    ion = new CustomIon(ion.GetSmallMoleculeLibraryAttributes(), ion.Adduct, massMono, massAverage);
                }
                else
                {
                    // Check our calculated value against provided theoretical value, allowing quite a lot of wiggle (not everybody is using the same precision out there)
                    var delta = .5; // Generous error for sanity check
                    if (Math.Abs(ion.MonoisotopicMassMz - mzTheoretical.Value) > delta)
                    {
                        Assume.Fail(string.Format(@"SpectrumPeakAnnotation: mzTheoretical {0} and mzActual {1} disagree by more than {2} in {3} {4}",
                          mzTheoretical, ion.MonoisotopicMassMz, delta, ion, comment??string.Empty));
                    }
                }
            }
            return ion.IsEmpty && string.IsNullOrEmpty(comment) ? 
                EMPTY : 
                new SpectrumPeakAnnotation(ion, comment);
        }
        public static bool IsNullOrEmpty(SpectrumPeakAnnotation spa)
        {
            return spa == null || spa.Equals(EMPTY);
        }

        // Represent a single annotation in a serializable manner
        private static string ToCacheFormat(SpectrumPeakAnnotation annot)
        {
            if (IsNullOrEmpty(annot))
                return string.Empty;
            var fields = annot.Ion.AsFields();
            fields.Add(annot.Comment);
            var tsv = TextUtil.ToEscapedTSV(fields);
            return tsv;
        }

        // Represent a collection of annotations for a single peak in a serializable manner
        private static string ToCacheFormat(IEnumerable<SpectrumPeakAnnotation> annotations)
        {
            if (annotations == null)
                return string.Empty;
            // ReSharper disable once LocalizableElement
            return string.Join("\r", annotations.Select(ToCacheFormat));
        }

        // Represent a collection of annotations for a list of peaks in a serializable manner
        public static string ToCacheFormat(IEnumerable<IEnumerable<SpectrumPeakAnnotation>> annotsPerPeak)
        {
            if (annotsPerPeak == null)
                return string.Empty;
            // ReSharper disable LocalizableElement
            return string.Join("\n", annotsPerPeak.Select(annot => string.Join("\r", ToCacheFormat(annot))));
            // ReSharper restore LocalizableElement
        }

        // Deserialize  a collection of annotations (with multiple annotations per peak)
        public static List<List<SpectrumPeakAnnotation>> FromCacheFormat(string cached)
        {
            var result = new List<List<SpectrumPeakAnnotation>>();
            if (string.IsNullOrEmpty(cached))
            {
                return result;
            }
            var annotationsPerPeak = cached.Split('\n');
            foreach (var annotations in annotationsPerPeak)
            {
                if (string.IsNullOrEmpty(annotations))
                {
                    result.Add(null);
                }
                else
                {
                    var list = new List<SpectrumPeakAnnotation>();
                    foreach (var annot in annotations.Split('\r'))
                    {
                        var lastTab = annot.LastIndexOf(TextUtil.SEPARATOR_TSV_STR, StringComparison.Ordinal);
                        var ion = lastTab < 0 ? CustomIon.EMPTY : CustomIon.FromTSV(annot.Substring(0, lastTab));
                        var comment = lastTab < 0 ? string.Empty : annot.Substring(lastTab+1).UnescapeTabAndCrLf();
                        list.Add(ion.IsEmpty && string.IsNullOrEmpty(comment) ? EMPTY : new SpectrumPeakAnnotation(ion, comment));
                    }
                    result.Add(list);
                }
            }
            return result;
        }

        public override string ToString()
        {
            if (Equals(EMPTY))
                return string.Empty;
            return Ion.ToTSV().Replace(TextUtil.SEPARATOR_TSV_STR,@" ") + @" " + Comment;
        }

        public bool Equals(SpectrumPeakAnnotation other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Ion, other.Ion) && 
                string.Equals(Comment, other.Comment);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is SpectrumPeakAnnotation && Equals((SpectrumPeakAnnotation) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Ion.GetHashCode();
                hashCode = (hashCode * 397) ^ (Comment != null ? Comment.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(SpectrumPeakAnnotation left, SpectrumPeakAnnotation right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(SpectrumPeakAnnotation left, SpectrumPeakAnnotation right)
        {
            return !Equals(left, right);
        }
    }

}
