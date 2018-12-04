/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
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
using System.Text.RegularExpressions;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.GroupComparison
{
    public enum MatchOption
    {
        ProteinName,
        ProteinAccession,
        ProteinPreferredName,
        ProteinGene,

        PeptideSequence,
        PeptideModifiedSequence,

        MoleculeGroupName,
        MoleculeName,
        CAS,
        HMDB,
        InChiKey,

        BelowLeftCutoff,
        AboveRightCutoff,
        BelowPValueCutoff,
        AbovePValueCutoff
    }

    public interface ICutoffSettings
    {
        double Log2FoldChangeCutoff { get; set; }
        double PValueCutoff { get; set; }

        bool FoldChangeCutoffValid { get; }

        bool PValueCutoffValid { get; }
    }

    public static class CutoffSettings
    {
        public static bool IsFoldChangeCutoffValid(double cutoff)
        {
            return !double.IsNaN(cutoff) && cutoff != 0.0;
        }

        public static bool IsPValueCutoffValid(double cutoff)
        {
            return !double.IsNaN(cutoff) && cutoff >= 0.0;
        }
    }

    public class MatchExpression
    {
        public MatchExpression(string regExpr, IEnumerable<MatchOption> matchOptions)
        {
            RegExpr = regExpr;
            this.matchOptions = matchOptions.ToList();
        }

        public List<MatchOption> matchOptions { get; private set; }
        public string RegExpr { get; private set; }

        public class ParseException : Exception { }
        public class InvalidMatchOptionException : Exception { }

        protected static bool IsNameMatch(MatchOption matchOption)
        {
            return matchOption >= MatchOption.ProteinName && matchOption <= MatchOption.InChiKey;
        }

        public override string ToString()
        {
            var result = string.Join(@" ", matchOptions);

            if (!string.IsNullOrWhiteSpace(RegExpr))
                result += @": " + RegExpr;

            return result;
        }

        public static MatchExpression Parse(string expression)
        {
            // An empty textbox in the grid will result in a null cell value
            if (expression == null)
                return new MatchExpression(string.Empty, new MatchOption[] { });

            var colonIndex = expression.IndexOf(@":", StringComparison.Ordinal);

            var matchOptionsStr = colonIndex < 0 ? expression : expression.Substring(0, colonIndex);
            var matchStrings = matchOptionsStr
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

            var options = new List<MatchOption>(matchStrings.Length);
            for (var i = 0; i < matchStrings.Length; ++i)
            {
                var m = matchStrings[i];
                MatchOption matchOption;
                if (Enum.TryParse(m, out matchOption))
                    options.Add(matchOption);
            }

            // If the string is not empty and there arent any options, parsing has failed and the expression
            // will be treated like a regular expression.
            // This means the user can enter a wrong match option, as long as there is at least a single correct one.
            if (!string.IsNullOrWhiteSpace(matchOptionsStr) && !options.Any())
                throw new ParseException();

            if (options.Count(IsNameMatch) > 1)
                throw new InvalidMatchOptionException();

            // Move name option to end of list
            var index = options.FindIndex(IsNameMatch);
            if (index >= 0)
            {
                var option = options[index];
                options.RemoveAt(index);
                options.Add(option);
            }

            var expr = colonIndex < 0 || colonIndex == expression.Length - 1
                ? string.Empty
                : expression.Substring(colonIndex + 1).Trim();

            return new MatchExpression(expr, options);
        }

        public static bool IsRegexValid(string regex)
        {
            try
            {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                // Throws an ArgumentException if the regular expression is invalid (There is no built in function for directly validating a regular expression)
                Regex.IsMatch(string.Empty, regex);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return true;
        }

        public bool IsRegexValid()
        {
            return IsRegexValid(RegExpr);
        }

        // We could just cast MatchOption to ProteinDisplayMode because the first 4 enum values correspond to eachother,
        // but this would break if any changes are made to the enums
        private static ProteinMetadataManager.ProteinDisplayMode MatchOptionToDisplayMode(MatchOption matchOption)
        {
            switch (matchOption)
            {
                case MatchOption.ProteinName:
                    return ProteinMetadataManager.ProteinDisplayMode.ByName;
                case MatchOption.ProteinAccession:
                    return ProteinMetadataManager.ProteinDisplayMode.ByAccession;
                case MatchOption.ProteinPreferredName:
                    return ProteinMetadataManager.ProteinDisplayMode.ByPreferredName;
                case MatchOption.ProteinGene:
                    return ProteinMetadataManager.ProteinDisplayMode.ByGene;
                default:
                    throw new ArgumentOutOfRangeException(nameof(matchOption), matchOption, null);
            }
        }

        public static string GetRowDisplayText(Protein protein, Databinding.Entities.Peptide peptide)
        {
            var proteomic = protein.DocNode.IsProteomic;

            if (peptide != null)
                return proteomic ? peptide.Sequence : peptide.MoleculeName;
            else
                return proteomic ? ProteinMetadataManager.ProteinModalDisplayText(protein.DocNode) : protein.Name;
        }

        public static string GetProteinText(Protein protein, MatchOption matchOption)
        {
            return protein != null
                ? ProteinMetadataManager.ProteinModalDisplayText(protein.DocNode.ProteinMetadata,
                    MatchOptionToDisplayMode(matchOption))
                : null;
        }

        public string GetDisplayString(SrmDocument document, Protein protein, Databinding.Entities.Peptide peptide)
        {
            return GetRowString(document, protein, peptide, true);
        }

        private string GetRowString(SrmDocument document, Protein protein, Databinding.Entities.Peptide peptide,
            bool showProteinForPeptides)
        {
            if (protein == null)
                return null;

            var perProtein = peptide == null;
            foreach (var m in matchOptions)
            {
                switch (m)
                {
                    case MatchOption.ProteinName:
                    case MatchOption.ProteinAccession:
                    case MatchOption.ProteinPreferredName:
                    case MatchOption.ProteinGene:
                        if (!perProtein && showProteinForPeptides)
                            return string.Format(@"{0} ({1})", GetRowDisplayText(protein, peptide),
                                GetProteinText(protein, m));
                        return GetProteinText(protein, m);
                    case MatchOption.PeptideSequence:
                        if (!perProtein)
                            return peptide.Sequence;
                        break;
                    case MatchOption.PeptideModifiedSequence:
                        if (!perProtein)
                            return peptide.ModifiedSequence.ToString();
                        break;
                    case MatchOption.MoleculeGroupName:
                        return protein.Name;
                    case MatchOption.MoleculeName:
                        if (!perProtein)
                            return peptide.MoleculeName;
                        break;
                    case MatchOption.CAS:
                        if (!perProtein)
                            return peptide.CAS;
                        break;
                    case MatchOption.HMDB:
                        if (!perProtein)
                            return peptide.HMDB;
                        break;
                    case MatchOption.InChiKey:
                        if (!perProtein)
                            return peptide.InChiKey;
                        break;
                }
            }

            return GetRowDisplayText(protein, peptide);
        }

        public string GetMatchString(SrmDocument document, Protein protein, Databinding.Entities.Peptide peptide)
        {
            return GetRowString(document, protein, peptide, false);
        }

        public bool Matches(SrmDocument document, Protein protein, Databinding.Entities.Peptide peptide, FoldChangeResult foldChangeResult, ICutoffSettings cutoffSettings)
        {
            foreach (var match in matchOptions)
            {
                switch (match)
                {
                    case MatchOption.ProteinName:
                    case MatchOption.ProteinAccession:
                    case MatchOption.ProteinPreferredName:
                    case MatchOption.ProteinGene:
                    case MatchOption.PeptideSequence:
                    case MatchOption.PeptideModifiedSequence:
                    case MatchOption.MoleculeGroupName:
                    case MatchOption.MoleculeName:
                    case MatchOption.CAS:
                    case MatchOption.HMDB:
                    case MatchOption.InChiKey:
                    {
                        var matchString = GetMatchString(document, protein, peptide);
                        if (matchString == null || !IsRegexValid() || !Regex.IsMatch(matchString, RegExpr))
                            return false;
                        break;
                    }
                    case MatchOption.BelowLeftCutoff:
                    {
                        if (!cutoffSettings.FoldChangeCutoffValid || foldChangeResult.Log2FoldChange >= -cutoffSettings.Log2FoldChangeCutoff)
                            return false;
                        break;
                    }
                    case MatchOption.AboveRightCutoff:
                    {
                        if (!cutoffSettings.FoldChangeCutoffValid || foldChangeResult.Log2FoldChange <= cutoffSettings.Log2FoldChangeCutoff)
                            return false;
                        break;
                    }
                    case MatchOption.BelowPValueCutoff:
                    {
                        if (!cutoffSettings.PValueCutoffValid || foldChangeResult.AdjustedPValue <= Math.Pow(10, -cutoffSettings.PValueCutoff))
                            return false;
                        break;
                    }
                    case MatchOption.AbovePValueCutoff:
                    {
                        if (!cutoffSettings.PValueCutoffValid || foldChangeResult.AdjustedPValue >= Math.Pow(10, -cutoffSettings.PValueCutoff))
                            return false;
                        break;
                    }
                }
            }

            return true;
        }

        protected bool Equals(MatchExpression other)
        {
            return ArrayUtil.EqualsDeep(matchOptions, other.matchOptions) && string.Equals(RegExpr, other.RegExpr);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((MatchExpression)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((matchOptions != null ? matchOptions.GetHashCode() : 0) * 397) ^ (RegExpr != null ? RegExpr.GetHashCode() : 0);
            }
        }
    }
}
