//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using IDPicker.DataModel;
using NHibernate.Linq;

namespace IDPicker.DataModel
{
    /// <summary>
    /// Provides HQL and SQL expressions that represent a "distinct match."
    /// </summary>
    public class DistinctMatchFormat
    {
        public bool IsChargeDistinct { get; set; }
        public bool IsAnalysisDistinct { get; set; }
        public bool AreModificationsDistinct { get; set; }
        public decimal? ModificationMassRoundToNearest { get; set; }

        public string SqlExpression
        {
            get
            {
                // Peptide + Charge? + Analysis? + Modifications? (possibly rounded)
                var expr = new StringBuilder("(psm.Peptide");
                if (IsChargeDistinct) expr.Append(" || ' ' || psm.Charge");
                if (IsAnalysisDistinct) expr.Append(" || ' ' || psm.Analysis");

                if (AreModificationsDistinct)
                {
                    expr.Append(" || ' ' || ");
                    if (ModificationMassRoundToNearest.HasValue)
                        expr.AppendFormat(@"IFNULL((SELECT GROUP_CONCAT((ROUND(mod.MonoMassDelta/{0}, 0)*{0}) || '@' || pm.Offset)
                                                        FROM PeptideModification pm
                                                        JOIN Modification mod ON pm.Modification=mod.Id
                                                        WHERE psm.Id=pm.PeptideSpectrumMatch), '')
                                               ", ModificationMassRoundToNearest.Value);
                    else
                        expr.Append(@"IFNULL((SELECT GROUP_CONCAT(pm.Modification || '@' || pm.Offset)
                                                  FROM PeptideModification pm
                                                  WHERE psm.Id=pm.PeptideSpectrumMatch), '')
                                         ");
                }
                expr.Append(")");

                return expr.ToString();
            }
        }

        public double Round (double mass)
        {
            if (!ModificationMassRoundToNearest.HasValue)
                return mass;
            double roundToNearest = (double) ModificationMassRoundToNearest.Value;
            return Math.Round(mass / roundToNearest, 0) * roundToNearest;
        }

        public override int GetHashCode()
        {
            return IsChargeDistinct.GetHashCode() ^
                   IsAnalysisDistinct.GetHashCode() ^
                   AreModificationsDistinct.GetHashCode() ^
                   ModificationMassRoundToNearest.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            return GetHashCode() == obj.GetHashCode();
        }

        public override string ToString()
        {
            return String.Format("{0} {1} {2} {3:0.0###}", IsChargeDistinct ? 1 : 0, IsAnalysisDistinct ? 1 : 0, AreModificationsDistinct ? 1 : 0, ModificationMassRoundToNearest.GetValueOrDefault(1.0m));
        }
    }

    public class DistinctMatchKey : IComparable<DistinctMatchKey>, IComparable
    {
        public long? Id { get; private set; }
        public Peptide Peptide { get; private set; }
        public string Key { get; private set; } // e.g. "<peptide> <charge?> <analysis?> <mods?>"
        public int? Charge { get; private set; }
        public Analysis Analysis { get; private set; }

        private DistinctMatchFormat Format { get; set; }

        public DistinctMatchKey (Peptide peptide, PeptideSpectrumMatch psm, DistinctMatchFormat format, string key, long? id)
        {
            Peptide = peptide;
            Key = key.Replace(",", Properties.Settings.Default.GroupConcatSeparator);
            Format = format;
            Id = id;

            if (format.IsChargeDistinct)
                Charge = psm.Charge;

            if (format.IsAnalysisDistinct)
                Analysis = psm.Analysis;
        }

        public int CompareTo (DistinctMatchKey other)
        {
            if (Id.HasValue && other.Id.HasValue)
                return Id.Value.CompareTo(other.Id.Value);

            int compare = Peptide.Sequence.CompareTo(other.Peptide.Sequence);
            if (compare != 0)
                return compare;

            if (Format.IsChargeDistinct)
            {
                compare = Charge.Value.CompareTo(other.Charge.Value);
                if (compare != 0)
                    return compare;
            }

            if (Format.IsAnalysisDistinct)
            {
                compare = Analysis.Name.CompareTo(other.Analysis.Name);
                if (compare != 0)
                    return compare;
            }

            return Key.CompareTo(other.Key);
        }

        public int CompareTo (object other)
        {
            var otherKey = other as DistinctMatchKey;
            if (otherKey == null)
                return 1;
            return CompareTo(otherKey);
        }

        public override string ToString ()
        {
            if (Key == null)
                return string.Empty;
            string[] tokens = Key.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int analysisIndex = 1;
            int modsIndex = 1;
            if (Format.IsChargeDistinct)
            {
                ++analysisIndex;
                ++modsIndex;
            }

            if (Format.IsAnalysisDistinct)
                ++modsIndex;

            var sb = new StringBuilder(Peptide.Sequence);
            if (Format.AreModificationsDistinct && tokens.Length > modsIndex)
            {
                var modifications = tokens[modsIndex].Split(' ').Last()
                                                     .Split(Properties.Settings.Default.GroupConcatSeparator[0])
                                                     .Select(o => o.Split('@'))
                                                     .Select(o => new { Offset = Convert.ToInt32(o[1]), DeltaMass = Convert.ToDouble(o[0], CultureInfo.InvariantCulture) })
                                                     .OrderByDescending(o => o.Offset);

                string formatString = "[{0}]";
                foreach (var mod in modifications)
                    if (mod.Offset == int.MinValue)
                        sb.Insert(0, String.Format(formatString, mod.DeltaMass));
                    else if (mod.Offset == int.MaxValue)
                        sb.AppendFormat(formatString, mod.DeltaMass);
                    else
                        sb.Insert(mod.Offset + 1, String.Format(formatString, mod.DeltaMass));
            }

            if (Format.IsChargeDistinct)
                sb.AppendFormat(" (+{0})", Charge.Value);

            if (Format.IsAnalysisDistinct)
                sb.AppendFormat(" ({0})", Analysis.Name);

            return sb.ToString();
        }

        public override int GetHashCode ()
        {
            return Key.GetHashCode();
        }

        public override bool Equals (object obj)
        {
            var other = obj as DistinctMatchKey;
            if (other == null)
                return false;
            return Key == other.Key;
        }
    }
}